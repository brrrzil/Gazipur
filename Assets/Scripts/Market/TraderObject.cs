using UnityEngine;
using Zenject;

public class TraderObject : InteractObject
{
    [Inject] private DialogManager _dialog;
    [Inject] private GameModeManager _gameMode;
    [Inject] private Inventory _inventory;
    [Inject] private Sounds _sounds;

    [Header("Voice distance gate")]
    [Tooltip("If the player walks further than this from the trader while a trader remark is still playing, stop the voice.")]
    [SerializeField] private float _maxVoiceDistance = 12f;

    private AudioSource _speaker => _sounds.DialogSource;

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

    void Update()
    {
        // Distance gate: if the player is far from the trader AND the global dialog
        // speaker is still playing, stop it so the trader remark doesn't follow the player
        // across the map. Only fires when the dialog source is in use.
        if (_speaker == null || !_speaker.isPlaying) return;
        var cam = Camera.main;
        if (cam == null) return;
        float sqr = (cam.transform.position - transform.position).sqrMagnitude;
        if (sqr > _maxVoiceDistance * _maxVoiceDistance)
        {
            _speaker.Stop();
            // Also hide the on-screen remark bubble if it's still up.
            if (_dialog != null && _dialog.Remarks != null)
                _dialog.Remarks.ForceHide();
        }
    }
}
