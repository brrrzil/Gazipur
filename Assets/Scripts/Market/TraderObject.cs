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
                // (round 62) Dialog removed per user request — when
                // closing the inventory after buying/picking up the
                // cutter, a text panel with the trader's voice line
                // popped up. User asked for it gone. The medicine unlock
                // and HealMother quest flag below are gameplay and
                // stay — only the dialog/voice is removed. The
                // DialogType.traderAfterBuy enum value is left in
                // EnumData so anything that imports the enum type
                // still compiles.
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
