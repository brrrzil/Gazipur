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

        // Always offer the map (cheaper than the medicine, available from
        // the start of the game). The map is a one-time unlock - after
        // purchase and use, the map UI stays open for the rest of the
        // session, so the player only needs to buy it once.
        if (_map != null)
        {
            _market.AddItem(_map, true);
        }
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