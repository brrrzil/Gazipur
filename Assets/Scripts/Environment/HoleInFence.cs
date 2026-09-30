using UnityEngine;
using Zenject;
using static EnumData;

public class HoleInFence : InteractObject
{
    // (r5 / fence-persistence) Unique id used by LootPersistence to
    // remember that this fence has been cut. If two fences share an
    // id, cutting one will mark the other as cut too - assign a
    // distinct string to every fence in the Inspector.
    [Tooltip("Unique id for save persistence. Each fence must have a distinct id or cutting one will mark all of them.")]
    [SerializeField] private string _saveId;
    [SerializeField] private GameObject _holeFence;
    [SerializeField] private MeshRenderer _fence;
    [SerializeField] private float _holdTime;
    [SerializeField] private PlayerSound _openSound;
    [SerializeField] private ToolsType _tool;
    [SerializeField] private RemarksType _remark;

    [Inject] Inventory _inventory;
    [Inject] HoldProgressBar _holdBar;
    [Inject] DialogManager _dialog;
    [Inject] Sounds _sounds;

    // (r5 / fence-persistence) Track the hole instance we spawned so
    // ResetToSceneDefaults can destroy it cleanly. Without this, a
    // New Game would leave every cut fence as a hole and the player
    // would see double-stacked holes on the next reload.
    private GameObject _spawnedHole;

    private void Awake()
    {
        // If the previous session already cut this fence, the hole
        // prefab should already exist on the scene (spawned by Open()
        // before the original was hidden). Hide the original now so
        // the player sees only the hole, and skip the Open() prompt
        // because there's nothing left to cut.
        //
        // Note: with GameScene in DDOL Awake fires once per session,
        // not per Continue. That's the whole point - we don't need
        // to re-check on every Continue because nothing in this scene
        // is recreated.
        if (LootPersistence.IsCollected(_saveId))
        {
            gameObject.SetActive(false);
        }
    }

    // (r5 / fence-persistence) Called by GameSession.ResetForNewGame.
    // Re-enable the original fence, destroy the spawned hole, and
    // clear the LootPersistence entry so a fresh playthrough shows
    // the intact fence again.
    public void ResetToSceneDefaults()
    {
        gameObject.SetActive(true);
        if (_spawnedHole != null)
        {
            Destroy(_spawnedHole);
            _spawnedHole = null;
        }
        // LootPersistence is wiped wholesale by ResetForNewGame ->
        // ClearAll, so we don't need to delete our own id here.
        Debug.Log($"[HoleInFence] ResetToSceneDefaults id={_saveId}");
    }

    public override void Intearct(bool isDown)
    {

        if (_inventory.HaveTools.Contains(_tool))
        {
            if (isDown)
            {
                _holdBar.StartHold(_holdTime);
                _holdBar.OnHoldComplete += Open;
                _sounds.PlayerPlay(_openSound, false);
                PlayInteractAnimation();
            }
            else
            {
                _sounds.PlayerStop();
                _holdBar.CancelHold();
                _holdBar.OnHoldComplete -= Open;
                StopInteractAnimation();
            }
        }
        else
        {
            _dialog.Remarks.StartRemark(_remark);
        }
    }

    private void Update()
    {
        if (_holdBar != null && _holdBar.IsActive)
            KeepAnimationLockAlive();
    }

    private void Open()
    {
        //_sounds.PlayerStop();
        _holdBar.CancelHold();
        _holdBar.OnHoldComplete -= Open;
        StopInteractAnimation();
        if (_holeFence)
        {
            // (r5 / fence-persistence) Spawn the hole, hide the
            // original (rather than Destroy, which would lose the
            // ability to reset for New Game), and remember the
            // spawned hole so ResetToSceneDefaults can clean it up.
            // With GameScene in DDOL the spawned hole lives forever
            // after this, so we don't need to also persist it via
            // LootPersistence - we only need the id to know the
            // fence was cut, and Awake uses the id to hide the
            // original on next session start.
            _spawnedHole = Instantiate(_holeFence, transform.position, Quaternion.identity);
            LootPersistence.MarkCollected(_saveId);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[HoleInFence] {gameObject.name}: _holeFence prefab is not assigned. The fence is destroyed without a 'hole' replacement. Drag a hole fence prefab onto the _holeFence field in the Inspector.");
            // Still hide the original even without a hole replacement
            // so the player can't keep retrying an empty interactable.
            // No persistence needed - there's nothing to remember.
            gameObject.SetActive(false);
        }
    }
}