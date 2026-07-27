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
    [SerializeField] private Text _healtToInventoryText;
    [SerializeField] private Text _hungryToInventoryText;
    [SerializeField] private Text _thirstToInventoryText;

    [Inject] private DataManager _data;
    [Inject] private GameModeManager _modeManager;
    [Inject] private DialogManager _dialog;
    [Inject] private Sounds _sounds; // ƒќЅј¬Ћя≈ћ
    private DataManager.HeroInfo _info;
    private int _hungerForRemark;
    private int _thirstForRemark;

    private bool _isDead; // ƒќЅј¬Ћя≈ћ флаг, чтобы не запускать смерть несколько раз

    private void Start()
    {
        _data.SetDeffoultHeroState();
        _info = _data.Hero;
        _isDead = false;
        StartCoroutine(Tic());
        SetState();
    }
    private IEnumerator Tic()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);

            if (_data.gameMode != EnumData.GameMode.outdors)
                continue;

            _info.hunger -= _hungerPerSecond;
            _info.thirst -= _thirstPerSecond;
            if (_info.hunger <= 0 || _info.thirst <= 0)
            {
                _info.health -= _damagePerSecond;
            }
            if (_info.hunger >= _hungerForHealing && _info.thirst >= _thristForHealing)
            {
                _info.health += _healingPerSecond;
            }
            SetState();
        }
    }
    public void SetState()
    {
        // BUGFIX: если здоровье <= 0 и мы еще не в режиме смерти
        if (_info.health <= 0 && !_isDead)
        {
            _isDead = true;
            // ѕереключаем музыку на трек смерти
            _sounds.SwitchToDieBackground();
            _modeManager.ChangeMode(EnumData.GameMode.die);
        }

        if (_info.hunger <= 30 && (int)(_info.hunger) % 5 == 0
            && (int)_info.hunger != _hungerForRemark)
        {
            _dialog.Remarks.StartRemark(EnumData.RemarksType.hungry);
            _hungerForRemark = (int)_info.hunger;
        }

        if (_info.thirst <= 30 && (int)(_info.thirst) % 5 == 0
            && (int)_info.thirst != _thirstForRemark)
        {
            _dialog.Remarks.StartRemark(EnumData.RemarksType.thirst);
            _thirstForRemark = (int)_info.thirst;
        }

        _info.health = Mathf.Clamp(_info.health, 0, 100);
        _info.hunger = Mathf.Clamp(_info.hunger, 0, 100);
        _info.thirst = Mathf.Clamp(_info.thirst, 0, 100);
        _healthBar.SetAmountAndValue(_info.health, 100);
        _hungerBar.SetAmountAndValue(_info.hunger, 100);
        _thirstBar.SetAmountAndValue(_info.thirst, 100);
        _healtToInventoryText.text = (int)_info.health + "/" + 100;
        _hungryToInventoryText.text = (int)_info.hunger + "/" + 100;
        _thirstToInventoryText.text = (int)_info.thirst + "/" + 100;
    }
    public void Heal(int count)
    {
        _info.health += count;
        SetState();
    }
    public void Eat(int count)
    {
        _info.hunger += count;
        SetState();
    }
    public void Drink(int count)
    {
        _info.thirst += count;
        SetState();
    }
    public void TakeDamage(int damage)
    {
        _info.health -= damage;
        SetState();
    }
}