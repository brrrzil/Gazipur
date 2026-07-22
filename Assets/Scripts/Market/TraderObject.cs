using UnityEngine;
using Zenject;

public class TraderObject : InteractObject
{
    [SerializeField] private ItemData _medicine;
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
    }

    public override void Intearct(bool isDown)
    {
        if (isDown)
        {
            if (!_dialog.StartDialog(EnumData.DialogType.startTrader))
            {
                _dialog.Remarks.StartRemark(EnumData.RemarksType.rohulSelBuy);
                _gameMode.ChangeMode(EnumData.GameMode.trade);
            }
        }
    }
}