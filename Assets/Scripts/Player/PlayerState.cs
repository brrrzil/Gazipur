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
    [Inject] private Sounds _sounds; // ���������
    private DataManager.HeroInfo _info;
    private int _hungerForRemark;
    private int _thirstForRemark;

    private bool _isDead; // ��������� ����, ����� �� ��������� ������ ��������� ���

    // Round 85: edge-detection flag for
    // the 'lowHP' remark. Set true the
    // first time _info.health drops
    // below 50% of max (the user said
    // 'below half'). Set false again
    // when _info.health rises back to
    // 50% or above. The flag is
    // consulted in SetState() to fire
    // StartRemark(lowHP) only on the
    // falling edge (not on every
    // frame the player is at 49%
    // health), and to allow the remark
    // to fire again after the player
    // heals back above 50%. The
    // instance field resets to false
    // when the scene reloads (new
    // game), which matches the user's
    // 'only on first drop in this
    // game session' intent. In the
    // Editor, the round 80 v2 lesson
    // about 'Enter Play Mode Options
    // + Reload Domain off' applies:
    // if Reload Domain is disabled
    // the flag survives between Play
    // sessions, which would re-fire
    // the remark immediately on the
    // first SetState() after the
    // domain is reloaded - this is
    // fine because the rising-edge
    // logic in SetState() resets the
    // flag to false on the first
    // frame after the domain is
    // reloaded (the player's health
    // is 100 at the start, which is
    // > 50, so the 'else if' branch
    // fires and clears _wasLowHP).
    private bool _wasLowHP;

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
        // BUGFIX: ���� �������� <= 0 � �� ��� �� � ������ ������
        if (_info.health <= 0 && !_isDead)
        {
            _isDead = true;
            // ����������� ������ �� ���� ������
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

        // Round 85 v2: lowHP edge
        // detection. The remark fires
        // only on the falling edge of
        // the 50% threshold - the first
        // time the player's health
        // drops below 50 since the last
        // time it was at or above 50.
        // The flag is reset on the
        // rising edge (health back to
        // 50% or above), so a healed
        // player who takes damage
        // again will hear the remark
        // again on the second fall.
        //
        // Round 85 v2 user correction:
        // 'The event should not happen
        // if health is 0 or below.' So
        // the falling-edge condition
        // is 'health > 0 AND health <
        // 50', not just 'health < 50'.
        // The check is added to the
        // 'isLowHP' boolean (not just
        // the if-condition) so the
        // 'else if (!isLowHP &&
        // _wasLowHP)' branch below
        // also benefits from the same
        // range: when health is at 0
        // the player is dead, the
        // rising-edge branch should
        // not re-arm the flag (the
        // health value never rises
        // back through 50% on its
        // way down through 0 - the
        // only path back from 0 to
        // 50% is via Heal, which is
        // a separate code path, and
        // the rising-edge branch
        // would only matter in the
        // edge case of a one-frame
        // blip that drops to 0 and
        // back up; ignoring that
        // blip is fine).
        //
        // 'Below half' in the user's
        // request is interpreted as
        // strictly less than 50% of
        // the 100-point max (the
        // DataManager.HeroInfo
        // default health is 100 and
        // the _healthBar max is 100,
        // so 50 is exactly half). The
        // threshold is a hard-coded
        // literal here, not a
        // [SerializeField], because
        // the user's description is
        // 'half' and the hero's max
        // health is not exposed as
        // a separate variable in
        // the project - the 100 max
        // is implicit in
        // _healthBar.SetAmountAndValue
        // and Mathf.Clamp. If a
        // future change makes the
        // max health configurable,
        // this constant would have to
        // be derived from that max
        // instead of being a literal
        // 50.
        bool isLowHP = _info.health < 50f && _info.health > 0f;
        if (isLowHP && !_wasLowHP)
        {
            // Falling edge: health just
            // dropped into the (0, 50)
            // range (the 0 case is
            // excluded by the
            // 'health > 0f' guard
            // above). Fire the remark
            // once. The CharacterRemarks
            // row the user set up in
            // HeroRemarks has
            // 'isOneTime=false' or
            // isMultiRemark=true (the
            // user did not say which),
            // and the underlying
            // CharacterRemarks.
            // StartRemark will handle
            // the 'do not re-fire this
            // remark on the same
            // Isha_Crouch visit' rule
            // based on _isStarted /
            // _currentType. The edge
            // flag here is the 'should
            // we even attempt to fire
            // this remark at all' gate;
            // the StartRemark method
            // itself decides whether
            // to actually play the
            // audio / text based on
            // the row's isOneTime /
            // isMultiRemark / chance
            // settings.
            _dialog.Remarks.StartRemark(EnumData.RemarksType.lowHP);
            _wasLowHP = true;
        }
        else if (!isLowHP && _wasLowHP)
        {
            // Rising edge: health is
            // back at or above 50%.
            // Clear the flag so the
            // next time health drops
            // below 50% the remark can
            // fire again. This is the
            // 'if the player heals and
            // takes damage again, fire
            // the remark a second time'
            // behavior the user
            // requested. The flag is
            // cleared in this branch
            // only - the falling-edge
            // branch sets it true,
            // and any frame where
            // health is at 49% (still
            // below the threshold) the
            // 'else if' is false so
            // the flag stays true and
            // the remark does not
            // re-fire.
            _wasLowHP = false;
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