using UnityEngine;
using Zenject;

public class ItemsManager : MonoBehaviour
{

    [Inject] private DiContainer _container;
    public void DropItem(ItemData item, int count, Vector3 position)
    {
        var obj = _container.InstantiatePrefabForComponent<ItemObject>(item.ItemPrefab);
        obj.transform.position = position;
        obj.SetData(item, count); 
    }
}
