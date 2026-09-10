using UnityEngine;

public class MapItem : ItemObject, IUsebleItem
{
    public bool Use(GameManager manager)
    {
        if (MapUI.Instance != null)
        {
            MapUI.Instance.Unlock();
            return true;
        }
        return false;
    }
}
