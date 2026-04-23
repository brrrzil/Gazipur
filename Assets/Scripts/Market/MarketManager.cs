using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MarketManager : MonoBehaviour
{
    [field: SerializeField] public float TraderPriceMultiplicator; 
    [Inject] private GameModeManager _modeManager;
    [Inject] private Inventory _inventory;
    [field: SerializeField] public TradePanel TradePanel;
    public void StartSellTrade()
    {
        TradePanel.Show();
        _modeManager.ChangeMode(EnumData.GameMode.market);
        _inventory.ShowPanel(true);
    }
    public void Exit()
    {
        TradePanel.gameObject.SetActive(false);
        _modeManager.ChangeMode(EnumData.GameMode.market);
        _inventory.ShowPanel(false);
    }
}
