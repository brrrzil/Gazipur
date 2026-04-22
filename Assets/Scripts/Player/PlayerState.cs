using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class PlayerState : MonoBehaviour
{
    public float CurCapacity { get; private set; }
    public float MaxCapacity { get; private set; }
    [SerializeField] private float _hungerPerSecond;
    [SerializeField] private float _thirstPerSecond;
    [SerializeField] private float _hungerForHealing;
    [SerializeField] private float _thristForHealing;
    [SerializeField] private float _damagePerSecond;
    [SerializeField] private float _healingPerSecond;
    
    [SerializeField] private ProgressBar _healthBar;
    [SerializeField] private ProgressBar _hungerBar;
    [SerializeField] private ProgressBar _thirstBar;

    [Inject] private DataManager _data;
    private DataManager.HeroInfo _info;
    private void Start()
    {
        _data.SetDeffoultHeroState();
        _info = _data.Hero;
        StartCoroutine(Tic());
        SetState();
    }
    private IEnumerator Tic()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);
            _info.hunger -= _hungerPerSecond;
            _info.thirst -= _thirstPerSecond;
            if(_info.hunger <= 0 || _info.thirst <= 0)
            {
                _info.health -= _damagePerSecond;
            }
            if(_info.hunger>=_hungerForHealing && _info.thirst >= _thirstPerSecond)
            {
                _info.health += _healingPerSecond;
            }
            SetState();
        }
    }
    public void SetState()
    {
        _info.health = Mathf.Clamp(_info.health, 0, 100);
        _info.hunger = Mathf.Clamp(_info.hunger, 0, 100);
        _info.thirst = Mathf.Clamp(_info.thirst, 0, 100);
        _healthBar.SetAmountAndValue(_info.health, 100);
        _hungerBar.SetAmountAndValue(_info.hunger, 100);
        _thirstBar.SetAmountAndValue(_info.thirst, 100);
    }
}
