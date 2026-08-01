using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class TouchOrClickEvent : MonoBehaviour
{
    // ========================= BASE EVENT =========================

    [Header("Base Touch Event")]
    public UnityEvent OnTouched;

    // ========================= CONDITIONAL EVENTS =========================

    [System.Serializable]
    public class ConditionalEvent
    {
        [Header("Page Condition")]
        [Tooltip("Event will trigger only when current page index matches this value.")]
        public int requiredPageIndex;

        public UnityEvent onInvoked;

        [Header("Trigger Settings")]
        public bool allowMultipleTriggers = true;

        [HideInInspector] public bool hasTriggered;
    }

    [Header("Invoke When Page Index Matches")]
    public List<ConditionalEvent> conditionalEvents = new List<ConditionalEvent>();

    // ========================= SETTINGS =========================

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Behavior")]
    [SerializeField] private bool ignoreUI = true;

    // ========================= INTERNAL =========================

    private Collider cachedCollider;

    // ========================= LIFECYCLE =========================

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();

        if (targetCamera == null)
        {
            // Debug.LogError($"[{nameof(TouchOrClickEvent)}] Camera is not assigned on {gameObject.name}");
             targetCamera = Camera.main;

        }
    }

    private void OnEnable()
    {
        ResetAllConditionalTriggers();
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

        // -------- MOUSE --------
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ProcessPointer(Mouse.current.position.ReadValue());
        }

        // -------- TOUCH --------
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                ProcessPointer(touch.position.ReadValue());
            }
        }
    }

    // ========================= INPUT PROCESSING =========================

    private void ProcessPointer(Vector2 screenPosition)
    {
        if (ignoreUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (hit.collider != cachedCollider)
            return;

        InvokeEvents();
    }

    private void InvokeEvents()
    {
        OnTouched?.Invoke();

        int currentPage = PageNavigationController.CurrentIndex;

        foreach (var entry in conditionalEvents)
        {
            // PAGE INDEX CHECK
            if (entry.requiredPageIndex != currentPage)
                continue;

            if (!entry.allowMultipleTriggers && entry.hasTriggered)
                continue;

            entry.hasTriggered = true;
            entry.onInvoked?.Invoke();
        }
    }

    // ========================= PUBLIC API =========================

    public void ResetAllConditionalTriggers()
    {
        foreach (var entry in conditionalEvents)
        {
            entry.hasTriggered = false;
        }
    }
}