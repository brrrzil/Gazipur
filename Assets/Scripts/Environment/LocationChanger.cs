using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class LocationChanger : MonoBehaviour
{
    [Inject] private Sounds _sounds;
    [SerializeField] private Text _locationText;

    private string currentTag;
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
                    // Round 79: one-time remark on the
                    // first entry into the AreaRich
                    // volume. CharacterRemarks uses
                    // 'isOneTime=true' on its
                    // firstEnterRichZone row to set
                    // 'chance = 0' after the first
                    // play, so even if the player
                    // walks AreaVillage -> AreaRich ->
                    // AreaDanger -> AreaRich the
                    // remark only fires the first
                    // time. The 'if (_remarks == null)'
                    // lookup is skipped on every
                    // subsequent AreaRich entry.
                    if (_remarks == null)
                        _remarks = FindFirstObjectByType<CharacterRemarks>();
                    if (_remarks != null)
                        _remarks.StartRemark(RemarksType.firstEnterRichZone);
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