using UnityEngine;
using Zenject;

public class GameInstaller : MonoInstaller
{

    [SerializeField] private Control _controll;
    [SerializeField] private DataManager _data;
    [SerializeField] private Inventory _inventory;
    [SerializeField] private GameModeManager _gameModeManager;
    [SerializeField] private CraftManager _craftManager;
    [SerializeField] private ItemsManager _itemsManager;
    [SerializeField] private MarketManager _market;
    [SerializeField] private PlayerState _playerState;
    [SerializeField] private GameManager _manager;
    [SerializeField] private HoldProgressBar _holdProgress;
    [SerializeField] private PlayerMovement _player;
    [SerializeField] private Sounds _sounds;
    [SerializeField] private DialogManager _dialogManager;
    [SerializeField] private QuestManager _quest;
    [SerializeField] private Tooltipe _tooltipe;
    public override void InstallBindings()
    {
        Container.Bind<Control>().FromInstance(_controll).AsSingle();
        Container.Bind<Inventory>().FromInstance(_inventory).AsSingle();
        Container.Bind<GameModeManager>().FromInstance(_gameModeManager).AsSingle();
        Container.Bind<CraftManager>().FromInstance(_craftManager).AsSingle();
        Container.Bind<ItemsManager>().FromInstance(_itemsManager).AsSingle();
        Container.Bind<DataManager>().FromInstance(_data).AsSingle();
        Container.Bind<MarketManager>().FromInstance(_market).AsSingle();
        Container.Bind<GameManager>().FromInstance(_manager).AsSingle();
        Container.Bind<PlayerState>().FromInstance(_playerState).AsSingle();
        Container.Bind<HoldProgressBar>().FromInstance(_holdProgress).AsSingle();
        Container.Bind<PlayerMovement>().FromInstance(_player).AsSingle();
        Container.Bind<Sounds>().FromInstance(_sounds).AsSingle();
        Container.Bind<DialogManager>().FromInstance(_dialogManager).AsSingle();
        Container.Bind<QuestManager>().FromInstance(_quest).AsSingle();
        Container.Bind<Tooltipe>().FromInstance(_tooltipe).AsSingle();
    }
}