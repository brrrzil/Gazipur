using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class LocationChanger : MonoBehaviour
{
    [Inject] private Sounds _sounds;
    [SerializeField] private Text _locationText;

    private string currentTag;
    // Round 80: one-shot guard so the
    // firstEnterRichZone remark plays only on the
    // FIRST entry into AreaRich in the player's
    // play history, not on every transition back
    // into the zone. The flag is persisted to
    // PlayerPrefs so closing and reopening the
    // game does not re-arm the remark. The
    // CharacterRemarks.isOneTime flag on the
    // _remarks[] row would also have stopped the
    // second play, but that flag resets every
    // time CharacterRemarks reloads (which is
    // every scene load and every game launch),
    // so it is a session-only guard. The user
    // wants the remark to be one-shot across
    // sessions, which means the guard has to
    // live somewhere persistent. LocationChanger
    // is the place the remark is fired from, so
    // LocationChanger is the right place for the
    // flag.
    private bool _richZoneRemarkPlayed;
    // Round 79: cached reference to the CharacterRemarks
    // MonoBehaviour so OnTriggerEnter can ask it to play
    // the one-time 'firstEnterRichZone' remark when the
    // player first steps into the AreaRich trigger
    // volume. FindFirstObjectByType is the Unity 6
    // replacement for the deprecated FindObjectOfType.
    // The lookup runs at most once per LocationChanger
    // instance, so the scene-graph cost is paid only on
    // the first AreaRich entry.
    private CharacterRemarks _remarks;

    private void Awake()
    {
        // Round 80: load the one-shot flag from
        // PlayerPrefs so the firstEnterRichZone
        // remark does not replay on the second
        // game launch. Default value 0 means
        // 'not yet played', so a player who
        // reaches AreaRich on their very first
        // session still hears the remark.
        // PlayerPrefs.GetInt returns the saved
        // value or 0 if the key has never been
        // set, which is the correct default for
        // a fresh install.
        _richZoneRemarkPlayed = PlayerPrefs.GetInt("RichZoneRemarkPlayed", 0) == 1;
    }

    private void OnTriggerEnter(Collider other)
    {
        string tag = other.tag;

        if (currentTag != tag)
        {
            switch (tag)
            {
                case "AreaVillage":
                    _sounds.ChangeBackground(_sounds.Background[0]);
                    _locationText.text = "Сурьятал";
                    break;

                case "AreaRich":
                    _sounds.ChangeBackground(_sounds.Background[1]);
                    _locationText.text = "Рангаредди";
                    // Round 80: gate the remark behind
                    // a one-shot flag that is loaded
                    // once in Awake and persisted to
                    // PlayerPrefs. The flag means
                    // 'the player has already heard
                    // the firstEnterRichZone remark
                    // at least once in the lifetime
                    // of this PlayerPrefs file', and
                    // it survives scene loads and
                    // game restarts. CharacterRemarks'
                    // own isOneTime is still in place
                    // as a backstop (the second play
                    // would have been short-circuited
                    // by CharacterRemarks anyway),
                    // but the persistence here is
                    // the guarantee the user asked
                    // for: 'only the first time'.
                    if (!_richZoneRemarkPlayed)
                    {
                        if (_remarks == null)
                            _remarks = FindFirstObjectByType<CharacterRemarks>();
                        if (_remarks != null)
                            _remarks.StartRemark(RemarksType.firstEnterRichZone);
                        _richZoneRemarkPlayed = true;
                        PlayerPrefs.SetInt("RichZoneRemarkPlayed", 1);
                        PlayerPrefs.Save();
                    }
                    break;

                case "AreaDanger":
                    _sounds.ChangeBackground(_sounds.Background[2]);
                    _locationText.text = "Роро-Хиллз";
                    break;
            }

            currentTag = tag;
        }
    }
}