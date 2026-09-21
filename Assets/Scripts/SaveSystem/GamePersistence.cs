using System.Linq;
using UnityEngine;

/// <summary>
/// Glue between game state (DataManager, Inventory, FogController,
/// etc.) and SaveSystem. Each producer calls <see cref="SaveNow"/>
/// after a meaningful change; this helper snapshots the world into a
/// <see cref="SaveData"/> and hands it to SaveSystem.
///
/// Producers do not need to know what's saved - they just call
/// SaveNow() and trust the snapshot. Consumers (GameManager on
/// scene load) call <see cref="LoadIntoGame"/> which applies a loaded
/// SaveData to the running systems.
/// </summary>
public static class GamePersistence
{
    /// <summary>Snapshot the current game state and persist it.</summary>
    public static void SaveNow()
    {
        var data = Collect();
        SaveSystem.Save(data);
    }

    /// <summary>Build a SaveData from live game state without writing
    /// anywhere. Useful for tests or for "before New Game" debugging.</summary>
    public static SaveData Collect()
    {
        var data = new SaveData();

        var dm = DataManager.Instance;
        if (dm != null)
        {
            data.money = dm.Money;
            if (dm.Hero != null)
            {
                data.hero = new HeroData
                {
                    health = dm.Hero.health,
                    hunger = dm.Hero.hunger,
                    thirst = dm.Hero.thirst
                };
            }
            if (dm.Inventory != null)
            {
                foreach (var info in dm.Inventory)
                {
                    if (info != null && info.index >= 0 && info.count > 0)
                    {
                        data.inventory.Add(new InventoryEntry(info.index, info.count));
                    }
                }
            }
        }

        var player = PlayerMovement.Instance;
        if (player != null)
        {
            var p = player.transform.position;
            data.position = new WorldPosition(p.x, p.y, p.z);
        }

        var fog = FogController.Instance;
        if (fog != null)
        {
            data.fogDensity = fog.ClearedDensity;
        }

        var map = MapUI.Instance;
        if (map != null)
        {
            data.mapUnlocked = map.IsUnlocked;
        }

        // Completed dialogs: any DialogData with isUsed == isOneTime is
        // counted as finished. We persist the DialogType enum value so
        // we can match it on load.
        var dialog = DialogManager.Instance;
        if (dialog != null)
        {
            var allDialogs = dialog.AllDialogs;
            if (allDialogs != null)
            {
                foreach (var d in allDialogs)
                {
                    if (d != null && d.isUsed)
                        data.completedDialogs.Add((int)d.dialogType);
                }
            }
        }

        return data;
    }

    /// <summary>Apply <paramref name="data"/> to the running systems.
    /// Call after the game scene has finished its Awake/Start so
    /// every reference is non-null. Player position is applied last
    /// so the player doesn't see a one-frame teleport.</summary>
    public static void LoadIntoGame(SaveData data)
    {
        if (data == null) return;

        var dm = DataManager.Instance;
        if (dm != null)
        {
            dm.SetMoney(data.money);
            if (data.hero != null)
            {
                if (dm.Hero == null) dm.SetDeffoultHeroState();
                dm.Hero.health = data.hero.health;
                dm.Hero.hunger = data.hero.hunger;
                dm.Hero.thirst = data.hero.thirst;
            }
            // Inventory: find InventoryCell[] via Inventory singleton and
            // call AddItem on each cell that has a saved entry. The saved
            // itemIndex goes through ItemsManager.GetByIndex (Resources.LoadAll)
            // so the persisted index is the same one DataManager.UpdateInventory
            // would have written from a fresh pickup.
            if (data.inventory != null && data.inventory.Count > 0)
            {
                var inv = Inventory.Instance;
                var items = ItemsManager.Instance;
                if (inv != null && items != null)
                {
                    var cells = inv.Cells;
                    if (cells != null)
                    {
                        // Reset every cell to empty first so a partially filled
                        // save can shrink to a smaller set without leftovers.
                        for (int i = 0; i < cells.Length; i++)
                            if (cells[i] != null) cells[i].RemoveItem();

                        for (int i = 0; i < data.inventory.Count && i < cells.Length; i++)
                        {
                            var entry = data.inventory[i];
                            if (entry == null || entry.count <= 0) continue;
                            var itemData = items.GetByIndex(entry.itemIndex);
                            if (itemData == null) continue;
                            // AddItem splits overflow off - we cap at item.MaxInInventoryCell
                            // and discard the rest. Most stacks fit, otherwise the
                            // player lost a few to overflow (acceptable trade-off).
                            cells[i].AddItem(itemData, entry.count);
                        }
                    }
                }
            }
        }

        var fog = FogController.Instance;
        if (fog != null && data.fogDensity > 0f)
        {
            fog.SetClearedDensity(data.fogDensity);
        }

        var map = MapUI.Instance;
        if (map != null && data.mapUnlocked)
        {
            map.Unlock();
        }

        // Apply completed-dialogs flag back to DialogManager.
        var dialog = DialogManager.Instance;
        if (dialog != null && data.completedDialogs != null && data.completedDialogs.Count > 0)
        {
            dialog.MarkDialogsUsedFromSave(
                data.completedDialogs.Select(i => (DialogType)i));
        }

        // Position is applied by GameManager AFTER cells are filled so
        // the player teleport doesn't fight the cell initialisation.
    }
}
