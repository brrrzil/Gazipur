using UnityEngine;
using UnityEngine.UI;
using Zenject;


public class CraftUIItem : MonoBehaviour
{
    [Inject] private CraftManager _cManager;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Text _neenCountText;
    [SerializeField] private Image _lockImage;
}
