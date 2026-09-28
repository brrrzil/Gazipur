using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
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
    public UnityEvent<bool> OnComics = new UnityEvent<bool>();
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
        GameMode.comics,
    };

    public static bool IsUIMode(GameMode mode) => UIModes.Contains(mode);

    private Dictionary<GameMode, UnityEvent<bool>> _mods;
    [Inject] DataManager _data;
    [Inject] DialogManager _dialog;
    [Inject] Control _control;
    [Inject] Sounds _sounds; // ДОБАВЛЯЕМ

    // Флаг для TradePanel
    public bool IsTransitioningToDialog { get; private set; }
    public GameMode PreviousMode { get; private set; }

    // (fix/save-load-subscriptions) Cached so OnDestroy can remove exactly
    // this delegate from _control.OnEsc. InitMods used to inline a lambda
    // without unsubscribing - the lambda captured `this`, so it survived
    // GameModeManager across scene reloads (Control is scene-bound, so its
    // event gets reset on each rebuild, but if Zenject ever keeps Control
    // alive we end up with stacked NRE-throwing delegates calling into a
    // destroyed GameModeManager).
    private System.Action _onEscHandler;

    [Inject]
    private void InitMods()
    {
        Time.timeScale = 1;
        // (diag/input-after-continue) Trace that the lambda gets (re)wired on each scene reload.
        Debug.Log($"[GameModeManager] InitMods Control={(_control != null ? _control.GetInstanceID().ToString() : "null")} " +
                  $"subsOnEsc={_control?.OnEsc?.GetInvocationList().Length ?? 0} " +
                  $"scene='{SceneManager.GetActiveScene().name}'");
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
            [GameMode.comics] = OnComics,
        };
        _onEscHandler = () =>
        {
            // (fix/save-load-subscriptions) Guard against a destroyed
            // GameModeManager still wired into Control.OnEsc after a scene
            // reload. Without this, the first Esc press after Continue
            // throws MissingReferenceException and Control stops dispatching
            // - which is one of the visible 'subscriptions break on load'
            // symptoms.
            if (this == null) return;
            if (_data == null) return;
            if (_data.gameMode == GameMode.outdors)
            {
                // Open the pause menu.
                ChangeMode(GameMode.menu);
                Time.timeScale = 0;

                // BUGFIX: переключаем музыку на трек меню
                if (_sounds != null) _sounds.SwitchToMenuBackground();
                return;
            }

            // (Trader shortcut) Esc on the startTrader dialog skips the
            // conversation and opens the trade panel immediately. The
            // dialog is marked completed (same as a terminal answer)
            // so a subsequent interaction with the trader goes straight
            // to the trade panel via the TraderObject.Intearct branch.
            if (_data.gameMode == GameMode.dialog
                && _dialog != null && _dialog.Dialog == DialogType.startTrader)
            {
                _dialog.MarkCurrentDialogUsed();
                GamePersistence.SaveNow();
                ChangeMode(GameMode.trade);
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

                // BUGFIX: возвращаем игровую музыку при выходе из меню
                if (_sounds != null) _sounds.SwitchToGameBackground();
            }
            // If we're in `die`, do nothing — the player needs to use the
            // die panel to restart or quit.
        };
        if (_control != null) _control.OnEsc += _onEscHandler;
    }

    private void OnDestroy()
    {
        // (fix/save-load-subscriptions) Symmetric teardown for the OnEsc
        // lambda registered in InitMods. Without this the lambda survives
        // on Control.OnEsc after a scene reload, calling into a destroyed
        // GameModeManager on the next Esc press.
        if (_control != null && _onEscHandler != null)
            _control.OnEsc -= _onEscHandler;
    }

    public void ChangeMode(GameMode mode)
    {
        PreviousMode = _data.gameMode;
        IsTransitioningToDialog = (mode == GameMode.dialog);

        _mods[_data.gameMode]?.Invoke(false);
        _data.gameMode = mode;
        onChangeMode?.Invoke(mode);
        _mods[mode]?.Invoke(true);

        IsTransitioningToDialog = false;

        // BUGFIX: переключаем фоновую музыку в зависимости от режима
        switch (mode)
        {
            case GameMode.menu:
                _sounds.SwitchToMenuBackground();
                break;
            case GameMode.die:
                _sounds.SwitchToDieBackground();
                break;
            case GameMode.win:
                _sounds.SwitchToWinBackground();
                break;
            case GameMode.outdors:
                // Возвращаем игровую музыку только если выходим не из меню
                // (меню обрабатывается отдельно в OnEsc)
                if (PreviousMode != GameMode.menu)
                {
                    _sounds.SwitchToGameBackground();
                }
                break;
        }
    }

    public void OutDors()
    {
        Time.timeScale = 1;
        ChangeMode(GameMode.outdors);
    }
}