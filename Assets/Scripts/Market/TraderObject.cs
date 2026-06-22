using UnityEngine;
using Zenject;

public class TraderObject : InteractObject
{
    [Inject] private DialogManager _dialog;
    [Inject] private GameModeManager _gameMode;
    [Inject] private Inventory _inventory;

    private void Start()
    {
        _inventory.onTakeItem += itm =>
        {
            ToolItem tIt = itm.ItemPrefab as ToolItem;
           if(tIt!=null && tIt.ToolType == EnumData.ToolsType.crowbar)
                _dialog.StartDialog(EnumData.DialogType.traderAfterBuy);
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
