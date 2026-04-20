using UnityEngine;
using UnityEngine.UI;

public class MarketItem : MonoBehaviour
{
    public ItemData Item { get; private set;}
    public int Count { get; private set; }
    [SerializeField] private Image _iconImage; 
    [SerializeField] private Text _countText;

    
    public void SetItem(ItemData item, int count)
    {
        Item = item;
        _iconImage.sprite = item.Icon;
        Count = count;
    }
}
