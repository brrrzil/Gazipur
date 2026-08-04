using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

public class GarbageObject : InteractObject
{
    [SerializeField] private float _holdTaime = 1f;
    [SerializeField] private PlayerSound _pickSound;
    [SerializeField] private Vector2Int _ItemsCount = new Vector2Int(6, 10);
    [SerializeField] private Chances[] _dropChances;

    private List<ItemData> _items = new List<ItemData>();
    private int _count;
    [Inject] private HoldProgressBar _holdBar;
    [Inject] protected Inventory _inventory;
    [Inject] private Sounds _sounds;

    [System.Serializable]
    public struct Chances
    {
        public ItemData item;
        public int chance;
    }
    
    private void Start()
    {
        _count = Random.Range(_ItemsCount.x, _ItemsCount.y + 1);
        foreach (var ch in _dropChances)
        {
            for (int i = 0; i < ch.chance; i++)
            {
                _items.Add(ch.item);
            }
        }
    }
    public override void Select(bool isSelect)
    {
        if (!isSelect)
        {
            _holdBar.OnHoldComplete -= PicItem;
            Intearct(false);
        }

        base.Select(isSelect);
    }
    public override void Intearct(bool isDown)
    {
        if (isDown)
        {
            // loop: true so the player can keep E held and loot multiple items
            // from the same prefab without releasing. Without this the progress
            // bar disappears after the first loot.
            _holdBar.StartHold(_holdTaime, loop: true);
            _holdBar.OnHoldComplete += PicItem;
            _sounds.PlayerPlay(_pickSound, true);
            PlayInteractAnimation();
        }
        else
        {
            _sounds.PlayerStop();
            _holdBar.CancelHold();
            _holdBar.OnHoldComplete -= PicItem;
            StopInteractAnimation();
        }
    }
    protected virtual void PicItem()
    {
        int rnd = Random.Range(0, _items.Count);
        if (_inventory.AddItem(_items[rnd], 1) > 0)
            return;

        _count--;
        _items.RemoveAt(rnd);
        if (_count == 0)
        {
            _holdBar.CancelHold();
            StopInteractAnimation();
            Intearct(false);
            DecreaseFog();
            Destroy(gameObject);
        }
        else
        {
            RefreshInteractAnimation();
        }
    }

    private void DecreaseFog()
    {
        RenderSettings.fogDensity -= 0.005f;
        if (RenderSettings.fogDensity < 0f) RenderSettings.fogDensity = 0f;
    }
}