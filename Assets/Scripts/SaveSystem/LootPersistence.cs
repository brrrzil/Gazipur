using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of picked-up loot objects (garbage piles, skimmer parts,
/// etc.). Each object that wants to persist its pickup state sets a unique
/// [SerializeField] string `_saveId` in the Inspector - any string works as
/// long as no two objects share it. On Start we ask the registry if the id
/// was collected; if it was, we Destroy the GameObject before the player
/// can see or interact with it. On pickup the producer (GarbageObject /
/// SkimmerPart / whatever) calls <see cref="MarkCollected"/> and a Save
/// fires automatically.
///
/// Persistence is piggy-backed on the existing SaveSystem slot - we keep
/// the collected-ids list inside SaveData.completedDialogs for now would
/// be a stretch (different semantics). Instead, we use a parallel PlayerPrefs
/// key `gazipur.loot.v1`. The trade-off vs. expanding SaveData: no need
/// to renumber SaveData schema, but the loot list won't survive a Delete
/// of the main save unless we also clear it. We DO clear it on New Game
/// via <see cref="ClearAll"/> from MainMenuScript.OnNewGame.
///
/// Reading and writing happen entirely in-memory; PlayerPrefs.SetString
/// is the only disk touch and it batches like the main save.
/// </summary>
public static class LootPersistence
{
    private const string SaveKey = "gazipur.loot.v1";
    private static HashSet<string> _collected = new HashSet<string>();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            // Stored as a flat JSON array of strings.
            var arr = JsonUtility.FromJson<StringList>(WrapJsonArray(json));
            if (arr != null && arr.items != null)
            {
                foreach (var id in arr.items)
                    if (!string.IsNullOrEmpty(id)) _collected.Add(id);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LootPersistence] Failed to load: {e.Message}");
        }
    }

    /// <summary>True if the object with this id was already picked up in a
    /// previous session.</summary>
    public static bool IsCollected(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureLoaded();
        return _collected.Contains(id);
    }

    /// <summary>Mark an object as collected and persist immediately.
    /// Called from GarbageObject / SkimmerPart pickup handlers.</summary>
    public static void MarkCollected(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        EnsureLoaded();
        if (_collected.Add(id))
        {
            SaveToPrefs();
        }
    }

    /// <summary>Wipe the entire collected list. Call from MainMenuScript.OnNewGame
    /// so a fresh run starts with all piles/parts visible.</summary>
    public static void ClearAll()
    {
        _collected.Clear();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    private static void SaveToPrefs()
    {
        // JsonUtility cannot serialise HashSet<string> directly. Use a
        // wrapper class.
        var arr = new StringList();
        arr.items = new string[_collected.Count];
        int i = 0;
        foreach (var s in _collected) arr.items[i++] = s;
        string json = JsonUtility.ToJson(arr);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>JsonUtility insists on a class instance; if the stored blob
    /// was saved via JsonUtility.ToJson(arr) the outer object IS the array.
    /// We defensively wrap bare array strings in {} so the parser is happy
    /// with both shapes.</summary>
    private static string WrapJsonArray(string json)
    {
        string trimmed = json.TrimStart();
        if (trimmed.StartsWith("{")) return json;
        if (trimmed.StartsWith("[")) return "{\"items\":" + json + "}";
        return json;
    }

    [System.Serializable]
    private class StringList
    {
        public string[] items;
    }
}
