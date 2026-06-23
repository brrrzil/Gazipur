using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Zenject;
using static EnumData;

public class GameModeManager : MonoBehaviour
{
    public UnityEvent<bool> OnOutdors = new UnityEvent<bool>();
    public UnityEvent<bool> OnTrade = new UnityEvent<bool>();
    public UnityEvent<bool> OnInventory = new UnityEvent<bool>();
    public UnityEvent<bool> OnCraft = new UnityEvent<bool>();
    public UnityEvent<bool> OnStorage = new UnityEvent<bool>();
    public UnityEvent<bool> OnDialog = new UnityEvent<bool>();
    public UnityEvent<bool> OnMenu = new UnityEvent<bool>();
    public UnityEvent<bool> OnDie = new UnityEvent<bool>();
    public UnityEvent<bool> OnOtherPanels = new UnityEvent<bool>();
    public UnityEvent<bool> OnWin = new UnityEvent<bool>();
    public System.Action<GameMode> onChangeMode;

    // Explicit list of modes where the player cannot move/look and the cursor
    // is shown. Use IsUIMode() to check, not the implicit `mode != outdors`
    // trick — the new `win` mode is UI too.
    private static readonly HashSet<GameMode> UIModes = new HashSet<GameMode>
    {
        GameMode.trade,
        GameMode.inventory,
        GameMode.dialog,
        GameMode.craft,
        GameMode.storage,
        GameMode.menu,
        GameMode.die,
        GameMode.otherPanels,
        GameMode.win,
    };

    public static bool IsUIMode(GameMode mode) => UIModes.Contains(mode);

    private Dictionary<GameMode, UnityEvent<bool>> _mods;
    [Inject] DataManager _data;
    [Inject] DialogManager _dialog;
    [Inject] Control _control;

    [Inject]
    private void InitMods()
    {
        Time.timeScale = 1;
        _mods = new Dictionary<GameMode, UnityEvent<bool>>
        {
            [GameMode.outdors] = OnOutdors,
            [GameMode.trade] = OnTrade,
            [GameMode.inventory] = OnInventory,
            [GameMode.craft] = OnCraft,
            [GameMode.storage] = OnStorage,
            [GameMode.dialog] = OnDialog,
            [GameMode.menu] = OnMenu,
            [GameMode.die] = OnDie,
            [GameMode.otherPanels] = OnOtherPanels,
            [GameMode.win] = OnWin,
        };
        _control.OnEsc += () =>
        {
            if (_data.gameMode == GameMode.outdors)
            {
                // Open the pause menu.
                ChangeMode(GameMode.menu);
                Time.timeScale = 0;
                return;
            }
            if (_data.gameMode != GameMode.die)
            {
                // BUGFIX (round 12): when exiting any UI mode via Esc, make
                // sure the cursor is actually hidden. PlayerMovement.SetMode
                // usually handles this via the onChangeMode callback, but if
                // a panel's OnOutdors handler re-shows the cursor (or if the
                // player movement component is somehow not in the scene) the
                // cursor stays visible. Hide it defensively right here.
                OutDors();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            // If we're in `die`, do nothing — the player needs to use the
            // die panel to restart or quit.
        };
    }
    public void ChangeMode(GameMode mode)
    {
        _mods[_data.gameMode]?.Invoke(false);
        _data.gameMode = mode;
        onChangeMode?.Invoke(mode);
        _mods[mode]?.Invoke(true);
    }
    public void OutDors()
    {
        Time.timeScale = 1;
        ChangeMode(GameMode.outdors);
    }

}
