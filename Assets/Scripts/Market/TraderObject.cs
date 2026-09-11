using UnityEngine;
using Zenject;

public class TraderObject : InteractObject
{
    [SerializeField] private ItemData _medicine;
    [SerializeField] private ItemData _map;
    [Inject] private DialogManager _dialog;
    [Inject] private GameModeManager _gameMode;
    [Inject] private Inventory _inventory;
    [Inject] private MarketManager _market;
    [Inject] private QuestManager _quest;

    private void Start()
    {
        _inventory.onTakeItem += itm =>
        {
            ToolItem tIt = itm.ItemPrefab as ToolItem;
            if (tIt != null && tIt.ToolType == EnumData.ToolsType.cutter)
            {
                if (_quest.QuestsState[EnumData.Quests.healMother] == 0)
                {
                    _market.AddItem(_medicine, true);
                    _quest.HealMother(false);
                }
                _dialog.StartDialog(EnumData.DialogType.traderAfterBuy);
            }
        };

        // The map is now added through the MarketManager._items array
        // (in the Inspector) so the user can place it at any position
        // in the shop between the other items. The _map field is still
        // kept on this component for reference but is no longer added
        // to the market here - the user drags the ItemData into the
        // MarketManager's _items array directly.
    }

    public override void Intearct(bool isDown)
    {
        if (isDown)
        {
            if (!_dialog.StartDialog(EnumData.DialogType.startTrader))
            {
                _dialog.Remarks.StartRemark(EnumData.RemarksType.rohulSellBuy);
                _gameMode.ChangeMode(EnumData.GameMode.trade);
            }
        }
    }
}