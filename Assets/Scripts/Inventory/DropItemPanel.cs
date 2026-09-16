using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class DropItemPanel : MonoBehaviour
{
    [SerializeField] private Slider _countSlider;
    [SerializeField] private Text _curCountText;
    [SerializeField] private Text _dropCountText;
    [SerializeField] private Button _dropButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private float _spawnRadius;

    [Inject] PlayerMovement _player;
    [Inject] DiContainer _conteiner;
    private InventoryCell _currentCell;
    private int _count;
    private int _dropCount;
    private void Start()
    {
        _dropButton.onClick.AddListener(Drop);
        _cancelButton.onClick.AddListener(Hide);
        _countSlider.onValueChanged.AddListener(ChangeCount);        
    }
    public void SetItem(InventoryCell cell)
    {
        gameObject.SetActive(cell.Item!=null);
        _currentCell = cell;
        _dropCount = cell.Count;
        _count = cell.Count;
        _countSlider.maxValue = _dropCount;
        SetUI();

    }
    private void ChangeCount(float value)
    {
        _dropCount = (int)value;
        _dropButton.interactable = _dropCount > 0;
        SetUI();
    }
    private void SetUI()
    {
        _countSlider.value = _dropCount;
        _dropCountText.text = _dropCount.ToString();
        _curCountText.text = (_count - _dropCount).ToString();
    }
    public void Drop()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
        Vector3 spawnPosition = new Vector3(randomCircle.x, 0, randomCircle.y) + _player.transform.position;
        RaycastHit hit;
        if (Physics.Raycast(spawnPosition + Vector3.up * 1, Vector3.down, out hit, 200f))
        {
            spawnPosition = hit.point;
            var obj = _conteiner.InstantiatePrefabForComponent<ItemObject>(_currentCell.Item.ItemPrefab);
            spawnPosition.y += obj.GetComponent<Collider>().bounds.size.y / 2;
            obj.transform.position = spawnPosition;
            obj.SetData(_currentCell.Item, _dropCount);
            _currentCell.RemoveItem(_dropCount);
        }
        Hide();
    }
    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
