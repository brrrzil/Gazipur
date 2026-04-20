using UnityEngine;
using UnityEngine.Events;
using Zenject;
using static EnumData;

public class GameModeManager : MonoBehaviour
{
    public UnityEvent homeModeEvent = new UnityEvent();
    public UnityEvent marketMode = new UnityEvent();
    [Inject] DataManager _data;
    public void ChangeMode(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.home:
                {
                    _data.gameMode = GameMode.home;
                    homeModeEvent?.Invoke();
                    break;
                }

            case GameMode.market:
                {
                    _data.gameMode = GameMode.market;
                    marketMode?.Invoke();
                    break;
                }
        }
    }
}
