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
            // Inventory items are written into cells by GameManager once
            // it has applied the inventory list - GameManager owns the
            // cell array and is the only place that knows which cells
            // are real.
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

        // Position is applied by GameManager AFTER cells are filled so
        // the player teleport doesn't fight the cell initialisation.
    }
}
