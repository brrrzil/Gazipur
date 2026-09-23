using System;
using System.Collections.Generic;

/// <summary>
/// Plain data class serialised by Unity's JsonUtility. Backed by
/// PlayerPrefs in SaveSystem, which means the same JSON works in
/// the Editor, a standalone Player build, and a WebGL build (where
/// System.IO.File is sandboxed and PlayerPrefs is backed by the
/// browser's IndexedDB).
///
/// One save slot only. Adding more slots later is a matter of
/// namespacing the PlayerPrefs key.
/// </summary>
[Serializable]
public class SaveData
{
    // Player progression
    public int money;
    public HeroData hero;
    public WorldPosition position;
    public string currentLocation;

    // Inventory: list of (item index, count) pairs. Item index is the
    // position in ItemsManager._items array, which is stable across
    // runs because the array is built from a Resources folder.
    public List<InventoryEntry> inventory = new List<InventoryEntry>();

    // Dialog completion flags. Only filled when the player finishes a
    // dialog (DialogData.isUsed flipped to true). An unfinished dialog
    // is not recorded, so reopening the game replays it from the start.
    public List<int> completedDialogs = new List<int>();

    // Fog state. Stored so pickups + aim-zoom thinning persist across
    // sessions. Reset only on New Game via SaveSystem.DeleteSave().
    public float fogDensity;

    // Map state. The MapMarker.collected set already lives in
    // PlayerPrefs (per-marker keys), but a single "is map unlocked"
    // flag for the minimap itself lives here so loading doesn't have
    // to scan every map_marker_* key.
    public bool mapUnlocked;

    // (round 102) One-shot flag: have we played the opening comics
    // sequence yet? Set true by ComicsController after the last slide
    // closes, restored on Continue. Keeps New Game at fresh state and
    // Continue from re-playing the intro.
    public bool comicsCompleted;
}

[Serializable]
public class HeroData
{
    public float health;
    public float hunger;
    public float thirst;
}

[Serializable]
public class WorldPosition
{
    public float x;
    public float y;
    public float z;

    public WorldPosition() { }
    public WorldPosition(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
}

[Serializable]
public class InventoryEntry
{
    public int itemIndex;
    public int count;

    public InventoryEntry() { }
    public InventoryEntry(int itemIndex, int count) { this.itemIndex = itemIndex; this.count = count; }
}
