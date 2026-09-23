using UnityEngine;

/// <summary>
/// One-slot save system. Stores a JSON blob in PlayerPrefs under
/// <see cref="SaveKey"/>, which works in Editor, standalone Player,
/// and WebGL (PlayerPrefs in WebGL is backed by IndexedDB).
///
/// Callers populate a <see cref="SaveData"/>, hand it to Save(), and
/// later call Load() to read it back. The translation between scene
/// state and SaveData lives in dedicated hooks (DataManager,
/// Inventory, DialogManager, FogController, GameManager) - this class
/// just owns persistence.
///
/// DeleteSave() is the "New Game" entry point: it wipes the slot
/// and the next launch starts from scene defaults.
/// </summary>
public static class SaveSystem
{
    private const string SaveKey = "gazipur.save.v1";

    /// <summary>Persist <paramref name="data"/> to PlayerPrefs.</summary>
    public static void Save(SaveData data)
    {
        if (data == null) return;
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Wrote {json.Length}B to PlayerPrefs key='{SaveKey}'");
    }

    /// <summary>Read the saved blob, or null if no save / corrupted.</summary>
    public static SaveData Load()
    {
        Debug.Log($"[SaveSystem] Load() HasKey={HasSave()}");
        if (!HasSave()) return null;
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            Debug.Log($"[SaveSystem] Loaded save blob OK: money={data.money} inv={data.inventory?.Count} fog={data.fogDensity}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Failed to parse save blob: {e.Message}");
            return null;
        }
    }

    /// <summary>True if a save blob exists in PlayerPrefs.</summary>
    public static bool HasSave()
    {
        return PlayerPrefs.HasKey(SaveKey);
    }

    /// <summary>Wipe the save. Use for "New Game".</summary>
    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
}
