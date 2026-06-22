using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class FilterBlueprint : MonoBehaviour
{
     [SerializeField] private PartData[] _parts;
    [System.Serializable]
    public class PartData
    {
        public FilterParts part;
        public Image partImage;
        public Toggle checkBox;
    }
    [Inject] DialogManager _dialog;
    public void AddPart(FilterParts part)
    {
        var prt = System.Array.Find(_parts, i => i.part == part);

        // BUGFIX (M7): if the part isn't in our _parts array, Find returns
        // null and the next line throws NullReferenceException.
        if (prt == null) return;

        if(prt.partImage)
            prt.partImage.enabled = true;

        prt.checkBox.isOn = true;
        if (CheckComplete())
            _dialog.Remarks.StartRemark(RemarksType.foolParts);
    }
    public bool CheckComplete()
    {
        foreach (var p in _parts)
        {
            if (!p.checkBox.isOn)
                return false;
        }
        return true;
    }
}
