using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Control : MonoBehaviour
{
    // (m5) Removed the unused `OnMouseDownInObject` delegate — no one subscribed to it.
    public Action<InteractObject> OnSelectObject;
    public Action OnInteractObject;        // �������� ������� E (Tap)
    public Action<bool> OnHoldInteract;   // ������� ������� E (Hold)
    public Action OnOpenInventory;
    public Action OnEsc;
    public Action<int> OnFastSlotUse;

    private PlayerInputActions inputActions;
    private bool isHoldInProgress = false;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();

        inputActions.Player.Interact.performed += OnInteractPerformed;
        // (m1) Replaced lambda subscriptions with named methods so OnDisable
        // can unsubscribe cleanly. Lambdas cannot be unsubscribed by reference.
        inputActions.Player.HoldInteract.started += OnHoldInteractStart;
        inputActions.Player.HoldInteract.canceled += OnHoldInteractCancel;

        inputActions.Player.Inventory.performed += OnInventoryButtonPressed;
        inputActions.Player.Escape.performed += OnEscape;

        inputActions.Player.Slot1.performed += OnSlot1Performed;
        inputActions.Player.Slot2.performed += OnSlot2Performed;
        inputActions.Player.Slot3.performed += OnSlot3Performed;
        inputActions.Player.Slot4.performed += OnSlot4Performed;
        inputActions.Player.Slot5.performed += OnSlot5Performed;
    }

    // (m1) Previously commented out — without this, disabling the Control
    // GameObject (or reloads) leaks subscriptions and leads to double-firing
    // of input callbacks on the next OnEnable.
    private void OnDisable()
    {
        inputActions.Player.Interact.performed -= OnInteractPerformed;
        inputActions.Player.HoldInteract.started -= OnHoldInteractStart;
        inputActions.Player.HoldInteract.canceled -= OnHoldInteractCancel;

        inputActions.Player.Inventory.performed -= OnInventoryButtonPressed;
        inputActions.Player.Escape.performed -= OnEscape;

        inputActions.Player.Slot1.performed -= OnSlot1Performed;
        inputActions.Player.Slot2.performed -= OnSlot2Performed;
        inputActions.Player.Slot3.performed -= OnSlot3Performed;
        inputActions.Player.Slot4.performed -= OnSlot4Performed;
        inputActions.Player.Slot5.performed -= OnSlot5Performed;

        inputActions.Disable();
    }

    private void Update()
    {
        InteractObject iObject = GetInteractObjectUnderCursor();
        OnSelectObject?.Invoke(iObject);
    }

    // ���������� ��� �� ������ ��� ������ ����, ��������� UI
    private InteractObject GetInteractObjectUnderCursor()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return null;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            return hit.collider.GetComponent<InteractObject>();
        }

        return null;
    }

    private void OnHoldInteractStart(InputAction.CallbackContext context)
    {
        isHoldInProgress = true;
        OnHoldInteract?.Invoke(true);
    }

    private void OnHoldInteractCancel(InputAction.CallbackContext context)
    {
        isHoldInProgress = false;
        OnHoldInteract?.Invoke(false);
    }

    // �������� ������� ������������, ���� ����� � �������� ���������
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        // (m2) Re-enable the hold-guard: while the player is holding E, don't
        // also fire the tap-E event. Without this, both OnInteractObject and
        // OnHoldInteract fire for a single press-and-hold, and the gameplay
        // path that's supposed to be tap-only (e.g. trader dialog start) ends
        // up triggering the hold path (trade mode).
        if (isHoldInProgress) return;
        OnInteractObject?.Invoke();
    }

    private void OnInventoryButtonPressed(InputAction.CallbackContext context)
    {
        OnOpenInventory?.Invoke();
    }

    private void OnEscape(InputAction.CallbackContext context) => OnEsc?.Invoke();

    private void OnSlot1Performed(InputAction.CallbackContext context) => OnFastSlotUse?.Invoke(1);
    private void OnSlot2Performed(InputAction.CallbackContext context) => OnFastSlotUse?.Invoke(2);
    private void OnSlot3Performed(InputAction.CallbackContext context) => OnFastSlotUse?.Invoke(3);
    private void OnSlot4Performed(InputAction.CallbackContext context) => OnFastSlotUse?.Invoke(4);
    private void OnSlot5Performed(InputAction.CallbackContext context) => OnFastSlotUse?.Invoke(5);
}
