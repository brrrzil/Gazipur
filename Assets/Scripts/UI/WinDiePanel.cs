using UnityEngine;
using Zenject;

public class WinDiePanel : MonoBehaviour
{
    [Inject] private GameModeManager _gameMode;

    public void ContinueButton()
    {
        // Close the panel AND return the game to outdors mode. Without this
        // the mode stays at `die` or `win` and PlayerMovement.SetMode keeps
        // _isUIMode=true, so the player can't move and the cursor stays
        // visible after clicking Continue.
        gameObject.SetActive(false);
        if (_gameMode != null)
            _gameMode.OutDors();
    }
}
