using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class LocationChanger : MonoBehaviour
{
    [Inject] private Sounds _sounds;
    [SerializeField] private Text _locationText;

    private string currentTag;
    // Round 80: simple one-shot guard so the
    // firstEnterRichZone remark plays only on
    // the FIRST entry into AreaRich in this
    // session (LocationChanger lives on
    // PLAYER.prefab, so the flag is reset on
    // game restart - this is the user's
    // preferred behaviour; PlayerPrefs was
    // rejected as overkill for a single
    // voice line). CharacterRemarks
    // 'isOneTime=true' on the _remarks[] row
    // is the second layer of the same guard
    // and would also stop the second play
    // even if this flag were stripped out.
    private bool _richZoneRemarkPlayed = false;
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

    private void OnEnable()
    {
        // Round 80 (v2): explicit reset of the
        // one-shot flag on every enable. The
        // default C# value of a bool field is
        // 'false', so a fresh instance would
        // start with '_richZoneRemarkPlayed
        // == false' anyway, BUT - in Unity
        // 2019.3+ the Editor can be configured
        // to skip the Domain Reload on 'Enter
        // Play Mode' (Project Settings ->
        // Editor -> 'Enter Play Mode
        // Options' with 'Reload Domain'
        // unchecked). With that setting on,
        // MonoBehaviour instances and their
        // instance fields are NOT reset
        // between Play sessions in the
        // Editor, so a '_richZoneRemarkPlayed
        // == true' from a previous run would
        // survive into the next run. OnEnable
        // is called whenever the GameObject
        // is enabled, which covers: the
        // initial scene load (a fresh
        // instance, OnEnable fires once),
        // the 'Reload Scene' case (OnEnable
        // fires again on the recreated
        // instances), and the 'skip domain
        // reload' Editor case (OnEnable still
        // fires on entering Play Mode because
        // the scene is reloaded even when the
        // domain is not). The one-liner
        // '_richZoneRemarkPlayed = false;'
        // is the cheapest, most robust
        // defence against the flag surviving
        // across Play sessions.
        _richZoneRemarkPlayed = false;
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
                    // Round 80: simple one-shot gate
                    // in front of the StartRemark
                    // call. After the first entry
                    // the flag stays true for the
                    // rest of the LocationChanger
                    // instance's lifetime, so every
                    // subsequent entry into
                    // AreaRich in the same session
                    // is silent. Game restart resets
                    // the flag (this is the
                    // user-requested behaviour).
                    if (!_richZoneRemarkPlayed)
                    {
                        if (_remarks == null)
                            _remarks = FindFirstObjectByType<CharacterRemarks>();
                        if (_remarks != null)
                            _remarks.StartRemark(RemarksType.firstEnterRichZone);
                        _richZoneRemarkPlayed = true;
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