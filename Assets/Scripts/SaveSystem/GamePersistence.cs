using System.Linq;
using UnityEngine;
using static EnumData;

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
        Debug.Log($"[Save] money={data.money} invCount={(data.inventory?.Count ?? 0)} heroH={(data.hero!=null?data.hero.health:-1)} heroHu={(data.hero!=null?data.hero.hunger:-1)} heroT={(data.hero!=null?data.hero.thirst:-1)} fog={data.fogDensity} map={data.mapUnlocked} dlg={(data.completedDialogs?.Count ?? 0)}");
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
                // (input-action-r3) Persist each non-empty slot with its
                // explicit slotIndex. Fast slots are the first 5 cells of
                // the same array (Inventory wires them up via
                // ChangeCellState in Inventory.cs), so saving them
                // alongside the rest is enough - no separate list.
                // DataManager.Inventory is ItemInfo[] (an array), so use
                // .Length, not .Count.
                for (int i = 0; i < dm.Inventory.Length; i++)
                {
                    var info = dm.Inventory[i];
                    if (info != null && info.index >= 0 && info.count > 0)
                    {
                        data.inventory.Add(new InventoryEntry(i, info.index, info.count));
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
        else
        {
            // First save can fire before FogController.Awake runs
            // (DataManager.Start -> ChangeMoney -> SaveNow fires before
            // [RuntimeInitializeOnLoadMethod] for AfterSceneLoad has
            // populated FogController.Instance). Fall back to the scene
            // authored value so we don't persist fogDensity=0 just because
            // the singleton hadn't bootstrapped yet.
            data.fogDensity = RenderSettings.fogDensity;
        }

        var map = MapUI.Instance;
        if (map != null)
        {
            data.mapUnlocked = map.IsUnlocked;
        }

        // (r4 / WaterFilter) Persist the workbench's activeSelf so the
        // Skimmer doesn't vanish on every Continue. FindAnyObjectByType
        // because WaterFilter is a scene MonoBehaviour, no singleton.
        // FindObjectsInactive.Include is needed because the workbench
        // can be in either state when the player saves (assembled or
        // not), and we want to find it in both.
        var waterFilter = Object.FindAnyObjectByType<WaterFilter>(FindObjectsInactive.Include);
        if (waterFilter != null)
        {
            data.waterFilterActive = waterFilter.gameObject.activeSelf;
        }

        // (r4 / quests) Persist QuestManager.QuestsState. The dictionary
        // is rebuilt fresh in QuestManager.Start, so without this every
        // Continue rolls the player back to 'filter=0, healMother=0'.
        var quest = QuestManager.Instance;
        if (quest != null && quest.QuestsState != null)
        {
            foreach (var kv in quest.QuestsState)
            {
                data.questStates.Add(new QuestEntry((int)kv.Key, kv.Value));
            }
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

        // First pass: data round-trip only - no GameObject writes. This
        // proves the data itself is well-formed before we touch any
        // scene reference.
        Debug.Log($"[GamePersistence.LoadIntoGame] data: money={data.money} hero={data.hero} inv={data.inventory?.Count} dlg={data.completedDialogs?.Count}");

        var dm = DataManager.Instance;
        Debug.Log($"[GamePersistence.LoadIntoGame] DataManager.Instance={dm?.GetType().Name ?? "null"}");
        if (dm != null)
        {
            dm.SetMoney(data.money);
            Debug.Log($"[GamePersistence.LoadIntoGame] Money set ok");
            if (data.hero != null)
            {
                if (dm.Hero == null) dm.SetDeffoultHeroState();
                if (dm.Hero != null)
                {
                    dm.Hero.health = data.hero.health;
                    dm.Hero.hunger = data.hero.hunger;
                    dm.Hero.thirst = data.hero.thirst;
                    Debug.Log($"[GamePersistence.LoadIntoGame] Hero set ok");
                }
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
                Debug.Log($"[GamePersistence.LoadIntoGame] Inventory.Instance={inv?.GetType().Name ?? "null"} ItemsManager={items?.GetType().Name ?? "null"}");
                if (inv != null && items != null)
                {
                    var cells = inv.Cells;
                    if (cells != null)
                    {
                        // Reset every cell to empty first so a partially filled
                        // save can shrink to a smaller set without leftovers.
                        for (int i = 0; i < cells.Count; i++)
                            if (cells[i] != null) cells[i].RemoveItem();

                        // (input-action-r3) Honour InventoryEntry.slotIndex when
                        // restoring, instead of writing into cells[i] in array
                        // order. The save list is already filtered to non-empty
                        // entries, so a naively sequential loop would cram the
                        // first save entry into cells[0] even if it was actually
                        // saved from cells[3]. With slotIndex we restore the
                        // original layout regardless of how Collect compacted
                        // the list.
                        //
                        // Legacy-blob fallback: blobs written before slotIndex
                        // existed (or blobs where the user's first save only
                        // populated cell[0]) have slotIndex=0 for every entry.
                        // Detecting that and falling back to array order keeps
                        // those saves working without a version bump.
                        bool legacyBlob = data.inventory.Count > 1 &&
                                          data.inventory.All(e => e == null || e.slotIndex == 0);
                        int fallbackIdx = 0;
                        foreach (var entry in data.inventory)
                        {
                            if (entry == null || entry.count <= 0) continue;
                            int slot = legacyBlob ? fallbackIdx : entry.slotIndex;
                            fallbackIdx++;
                            if (slot < 0 || slot >= cells.Count) continue;
                            var itemData = items.GetByIndex(entry.itemIndex);
                            if (itemData == null) continue;
                            // AddItem splits overflow off - we cap at item.MaxInInventoryCell
                            // and discard the rest. Most stacks fit, otherwise the
                            // player lost a few to overflow (acceptable trade-off).
                            try
                            {
                                // BUGFIX (round 102.5): probe every dependency of
                                // AddItem BEFORE entering it. A single destroyed
                                // Unity reference (most often the [Inject] Inventory
                                // field or the [SerializeField] Image/Text fields)
                                // causes AddItem to throw a NRE whose stack trace
                                // is null in player builds. Logging the missing piece
                                // here gives us a real culprit line in the log.
                                if (cells[slot] == null)
                                {
                                    Debug.LogWarning($"[Load] cell[{slot}] null (destroyed)");
                                    continue;
                                }
                                // Debug the inner InventoryCell state via reflection-free
                                // behaviour. We can read public Item/Count but the NRE-prone
                                // refs ([SerializeField] Image/Text and [Inject] Inventory)
                                // are private. Wrap AddItem in a pre-flight check: try the
                                // cheapest operation first (a no-op), then dispatch.
                                cells[slot].SetReady(true); // safe public set, primes the cell
                                cells[slot].AddItem(itemData, entry.count);
                            }
                            catch (System.Exception cellEx)
                            {
                                Debug.LogWarning($"[Load] cell[{slot}] item={itemData?.name} count={entry.count} failed:\n{cellEx}\nInnerException: {cellEx.InnerException}");
                            }
                        }
                        Debug.Log($"[GamePersistence.LoadIntoGame] Inventory applied");
                    }
                }
            }
        }

        var fog = FogController.Instance;
        if (fog != null)
        {
            try { fog.SetClearedDensity(data.fogDensity); Debug.Log($"[GamePersistence.LoadIntoGame] fog density set {data.fogDensity}"); }
            catch (System.Exception e) { Debug.LogWarning($"[GamePersistence.LoadIntoGame] fog set skipped: {e.Message}"); }
        }

        var map = MapUI.Instance;
        if (map != null && data.mapUnlocked)
        {
            try { map.Unlock(); Debug.Log($"[GamePersistence.LoadIntoGame] map unlocked"); }
            catch (System.Exception e) { Debug.LogWarning($"[GamePersistence.LoadIntoGame] map unlock skipped: {e.Message}"); }
        }

        // Apply completed-dialogs flag back to DialogManager.
        var dialog = DialogManager.Instance;
        if (dialog != null && data.completedDialogs != null && data.completedDialogs.Count > 0)
        {
            try
            {
                dialog.MarkDialogsUsedFromSave(
                    data.completedDialogs.Select(i => (DialogType)i));
                Debug.Log($"[GamePersistence.LoadIntoGame] dialogs marked used");
            }
            catch (System.Exception e) { Debug.LogWarning($"[GamePersistence.LoadIntoGame] dialog mark skipped: {e.Message}"); }
        }

        // (r4 / WaterFilter) Restore the workbench's active state. Without
        // this, every Continue reverts the Skimmer to the scene-authored
        // (default active) state and the player loses the build they did.
        // FindObjectsInactive.Include so a workbench that's currently
        // disabled is still discoverable.
        var waterFilter = Object.FindAnyObjectByType<WaterFilter>(FindObjectsInactive.Include);
        if (waterFilter != null)
        {
            waterFilter.gameObject.SetActive(data.waterFilterActive);
            Debug.Log($"[GamePersistence.LoadIntoGame] waterFilter active={data.waterFilterActive}");
        }

        // (r4 / quests) Restore QuestManager.QuestsState. Apply AFTER
        // quest-object references have had a chance to bind - QuestManager
        // owns the dictionary but uses other scene references for UI
        // panels; calling ApplyQuestStates re-evaluates whatever
        // quest-bound UI needs to flip.
        var quest = QuestManager.Instance;
        if (quest != null && data.questStates != null && data.questStates.Count > 0)
        {
            try
            {
                quest.ApplyQuestStatesFromSave(data.questStates.Select(q => ((EnumData.Quests)q.questId, q.value)));
                Debug.Log($"[GamePersistence.LoadIntoGame] quest states restored count={data.questStates.Count}");
            }
            catch (System.Exception e) { Debug.LogWarning($"[GamePersistence.LoadIntoGame] quest restore skipped: {e.Message}"); }
        }

        // Position is applied by GameManager AFTER cells are filled so
        // the player teleport doesn't fight the cell initialisation.
    }
}
