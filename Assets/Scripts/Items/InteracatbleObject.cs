using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Zenject;

[RequireComponent(typeof(Outline))]
public abstract class InteractObject : MonoBehaviour
{
    private Outline _outline;
    [SerializeField] private string _tooltipeText;
    [SerializeField] private string _playerAnimTrigger;
    [SerializeField] private float _animDuration = 0.5f;
    private Tween _tween;
    [Inject] private Tooltipe _tooltipe;
    [Inject] protected PlayerMovement _movement;
    public virtual void Select(bool isSelect)
    {
        _outline.enabled = isSelect;
        if (_tooltipeText!="")
        {
            if (isSelect)
                _tooltipe.Show(_tooltipeText);
            else
                _tooltipe.Hide();
        }
    }
    private void OnEnable()
    {
        _outline ??= GetComponent<Outline>();
        _outline.enabled = false;
    }
    private void OnDestroy()
    {
        _tooltipe.Hide();
    }
    public abstract void Intearct(bool isDowwn);
    protected void PlayInteractAnimation()
    {
        if (_playerAnimTrigger != "" && _movement != null)
            _movement.PlayLockedAnimation(_playerAnimTrigger, _animDuration);
    }
    protected void RefreshInteractAnimation()
    {
        if (_playerAnimTrigger != "" && _movement != null)
            _movement.RefreshLock(_playerAnimTrigger, _animDuration);
    }
    protected void StopInteractAnimation()
    {
        if (_playerAnimTrigger != "" && _movement != null)
            _movement.UnlockAnimation(_playerAnimTrigger);
    }
    protected void KeepAnimationLockAlive()
    {
        if (_playerAnimTrigger != "" && _movement != null)
            _movement.KeepLockAlive(_animDuration);
    }
}

