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
                // BUGFIX: the medicine unlock used to be wired to the
                // traderAfterBuy dialog (as an action on the iteration-1
                // answer). If the player skipped the dialog or didn't click
                // that answer, the medicine never appeared in the shop. The
                // dialog is narrative only — the gameplay effect must not
                // depend on the player completing it. Now we fire the unlock
                // directly on cutter pickup. The dialog still plays for
                // story purposes (it's isOneTime, so it won't repeat).
                //
                // (round 62b) Dialog trigger moved out of this
                // callback. It used to fire on AddItem — i.e. the
                // instant the cutter was added to the inventory, which
                // the player perceived as 'right after I close the
                // inventory'. Per user feedback in this round, the
                // dialog belongs on the trade-panel-close event in
                // MarketManager.StartTrade(false), so it plays once
                // when the player walks away from the trader, not
                // while the inventory panel is still dismissing.
                // The StartDialog call itself now lives in
                // MarketManager.
                //
                // Idempotency guard: if the player picks up a second cutter
                // (drops the first and re-picks), don't add a duplicate
                // BuyItemObject in the trade panel. We key off the quest
                // state — HealMother == 0 means the unlock hasn't fired yet.
                if (_quest.QuestsState[EnumData.Quests.healMother] == 0)
                {
                    _market.AddItem(_medicine, true);
                    _quest.HealMother(false);
                }
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
