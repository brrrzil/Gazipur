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
    public override void InstallBindings()
    {
        Container.Bind<Control>().FromInstance(_controll).AsSingle();
        Container.Bind<Inventory>().FromInstance(_inventory).AsSingle();
        Container.Bind<GameModeManager>().FromInstance(_gameModeManager).AsSingle();
        Container.Bind<CraftManager>().FromInstance(_craftManager).AsSingle();
        Container.Bind<ItemsManager>().FromInstance(_itemsManager).AsSingle();
        Container.Bind<DataManager>().FromInstance(_data).AsSingle();
    }
}