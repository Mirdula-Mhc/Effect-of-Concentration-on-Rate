using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// All-in-one Drag & Snap manager for UI (Canvas / RectTransform) elements.
///
/// Designed for a Screen Space - Overlay -> Screen Space - Overlay workflow:
/// both the draggable UI images and the snap targets live on the SAME
/// Screen Space - Overlay Canvas. There is no World Space canvas, no
/// Screen Space - Camera canvas, and no camera-based coordinate conversion
/// anywhere in this script.
///
/// - draggables[i] can ONLY snap correctly onto snapTargets[i] (index-paired).
/// - Drop is checked using the target's RectTransform bounds (UI bounds check),
///   not distance.
/// - Wrong drop (empty space or wrong target) -> snaps back to its original position.
/// - Correct drop -> snaps to target position and locks permanently (no further
///   dragging, cannot be reset).
/// - Once every assigned draggable has been correctly snapped, this calls
///   PageNavigationController.RequestNavigationUnlock() exactly once to unlock
///   the Next button. No separate event system is used.
///
/// SETUP:
/// 1. Put this script on any GameObject (e.g. an empty "ScreenToScreenSnapManager" under your Canvas).
/// 2. Assign draggables[] and snapTargets[] in the Inspector, index-matched
///    (draggables[0] belongs to snapTargets[0], etc).
/// 3. Each draggable needs a CanvasGroup component (auto-added at runtime if missing)
///    so it can ignore raycasts while dragging.
/// 4. Each snap target needs a RectTransform with a size (it's used as the drop bounds).
/// </summary>
public class ScreenToScreenSnapManager : MonoBehaviour
{
    [Header("Index-paired lists: draggables[i] snaps correctly onto snapTargets[i]")]
    public List<RectTransform> draggables = new List<RectTransform>();
    public List<RectTransform> snapTargets = new List<RectTransform>();

    [Header("Optional: parent Canvas (auto-found if left empty)")]
    public Canvas canvas;

    [Header("Snap animation")]
    [Tooltip("Seconds to animate into place on snap / return. Set 0 for instant.")]
    public float snapAnimDuration = 0.15f;

    // Per-draggable runtime state
    private class DragState
    {
        public RectTransform rect;
        public RectTransform correctTarget;
        public Vector2 originalAnchoredPos;
        public Transform originalParent;
        public int originalSiblingIndex;
        public bool isLocked; // permanently snapped, never resets
        public CanvasGroup canvasGroup;
        public DragHandler handler;

        // Offset (in the dragged item's parent-local space) between the pointer's
        // local point and the item's anchoredPosition at the moment the drag started.
        // Keeping this constant for the whole drag is what makes it track the
        // finger/cursor precisely instead of snapping its pivot to the pointer.
        public Vector2 grabOffset;
    }

    private readonly Dictionary<RectTransform, DragState> _states = new Dictionary<RectTransform, DragState>();
    private int _correctSnapCount;
    private bool _allSnappedFired;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        SetupAll();
    }

    private void SetupAll()
    {
        if (draggables.Count != snapTargets.Count)
        {
            Debug.LogError($"[ScreenToScreenSnapManager] draggables ({draggables.Count}) and snapTargets ({snapTargets.Count}) counts must match (index-paired). Fix the lists in the Inspector.");
        }

        int count = Mathf.Min(draggables.Count, snapTargets.Count);
        for (int i = 0; i < count; i++)
        {
            RectTransform d = draggables[i];
            RectTransform t = snapTargets[i];
            if (d == null || t == null)
            {
                Debug.LogWarning($"[ScreenToScreenSnapManager] Null entry at index {i}, skipping.");
                continue;
            }

            var state = new DragState
            {
                rect = d,
                correctTarget = t,
                originalAnchoredPos = d.anchoredPosition,
                originalParent = d.parent,
                originalSiblingIndex = d.GetSiblingIndex(),
                isLocked = false
            };

            var cg = d.GetComponent<CanvasGroup>();
            if (cg == null) cg = d.gameObject.AddComponent<CanvasGroup>();
            state.canvasGroup = cg;

            var handler = d.GetComponent<DragHandler>();
            if (handler == null) handler = d.gameObject.AddComponent<DragHandler>();
            handler.Init(this, d);
            state.handler = handler;

            _states[d] = state;
        }
    }

    /// <summary>Called by DragHandler when a drag ends.</summary>
    internal void HandleDrop(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return; // safety: locked items shouldn't be draggable at all

        RectTransform hitTarget = FindTargetUnderPointer(draggedRect, eventData);

        if (hitTarget != null && hitTarget == state.correctTarget)
        {
            SnapCorrect(state);
        }
        else
        {
            SnapBack(state);
        }
    }

    /// <summary>
    /// Checks every snap target's RectTransform screen-space bounds against the
    /// pointer's current screen position, returns the one it was dropped on (if any).
    /// Since everything lives on a single Screen Space - Overlay Canvas, the camera
    /// argument is always null.
    /// </summary>
    private RectTransform FindTargetUnderPointer(RectTransform draggedRect, PointerEventData eventData)
    {
        for (int i = 0; i < snapTargets.Count; i++)
        {
            RectTransform target = snapTargets[i];
            if (target == null) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    target,
                    eventData.position,
                    null))
            {
                return target;
            }
        }
        return null;
    }

    /// <summary>
    /// Snap onto the correct target. Since draggable and target share the same
    /// Screen Space - Overlay Canvas, this is a simple reparent with a zeroed
    /// anchored position   no coordinate conversion of any kind is needed.
    /// </summary>
    private void SnapCorrect(DragState state)
    {
        state.isLocked = true;
        state.canvasGroup.blocksRaycasts = false; // no longer needs to block/receive drag raycasts
        state.handler.enabled = false; // permanently disable further dragging

        state.rect.SetParent(state.correctTarget, false);
        state.rect.SetAsLastSibling();
        state.rect.localRotation = Quaternion.identity;
        state.rect.localScale = Vector3.one;
        state.rect.anchoredPosition = Vector2.zero;

        _correctSnapCount++;
        CheckAllSnapped();
    }

    /// <summary>
    /// Wrong drop: restore original parent, sibling order, and anchored position exactly.
    /// </summary>
    private void SnapBack(DragState state)
    {
        if (state.rect.parent != state.originalParent)
        {
            state.rect.SetParent(state.originalParent, false);
            state.rect.SetSiblingIndex(state.originalSiblingIndex);
        }
        StopAndAnimate(state.rect, state.originalAnchoredPos);
    }

    private void StopAndAnimate(RectTransform rect, Vector2 targetAnchoredPos)
    {
        var handler = rect.GetComponent<DragHandler>();
        if (handler != null)
        {
            handler.StopAllCoroutines();
            handler.StartCoroutine(handler.AnimateTo(rect, targetAnchoredPos, snapAnimDuration));
        }
        else
        {
            rect.anchoredPosition = targetAnchoredPos;
        }
    }

    private void CheckAllSnapped()
    {
        if (_allSnappedFired)
            return;
        if (_correctSnapCount >= _states.Count && _states.Count > 0)
        {
            _allSnappedFired = true;
            // No longer unlocks directly - EquationCompletionChecker below
            // decides when to actually unlock, once BOTH the drag-drop AND
            // the coefficient numbers are correct.
        }
    }

    // ---- Public helpers ----

    /// <summary>True once every assigned draggable has been correctly snapped.</summary>
    public bool AllSnapped => _allSnappedFired;

    /// <summary>How many draggables are correctly snapped right now.</summary>
    public int CorrectSnapCount => _correctSnapCount;

    public int TotalDraggables => _states.Count;

    /// <summary>
    /// Begin drag: called by DragHandler. Brings element to front and lets raycasts pass through it
    /// so drop-target detection under the pointer works.
    /// </summary>
    internal void HandleBeginDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = false;
        draggedRect.SetAsLastSibling(); // render above everything while dragging

        // Compute where in the parent's local space the pointer currently is,
        // then store the offset from that point to the item's current anchoredPosition.
        // Applying this same offset every frame keeps the exact grab point glued to
        // the pointer instead of snapping the item's pivot to the pointer.
        RectTransform parentRect = draggedRect.parent as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, null, out Vector2 pointerLocal))
        {
            state.grabOffset = draggedRect.anchoredPosition - pointerLocal;
        }
        else
        {
            state.grabOffset = Vector2.zero;
        }
    }

    internal void HandleDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        RectTransform parentRect = draggedRect.parent as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, null, out Vector2 pointerLocal))
        {
            draggedRect.anchoredPosition = pointerLocal + state.grabOffset;
        }
    }

    internal void HandleEndDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = true;
        HandleDrop(draggedRect, eventData);
    }

    /// <summary>
    /// Lightweight per-element drag handler. Auto-attached by ScreenToScreenSnapManager.
    /// Forwards all pointer events to the manager, which owns all the drag/snap logic.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ScreenToScreenSnapManager _manager;
        private RectTransform _rect;

        public void Init(ScreenToScreenSnapManager manager, RectTransform rect)
        {
            _manager = manager;
            _rect = rect;
        }

        public void OnBeginDrag(PointerEventData eventData) => _manager.HandleBeginDrag(_rect, eventData);
        public void OnDrag(PointerEventData eventData) => _manager.HandleDrag(_rect, eventData);
        public void OnEndDrag(PointerEventData eventData) => _manager.HandleEndDrag(_rect, eventData);

        public IEnumerator AnimateTo(RectTransform rect, Vector2 target, float duration)
        {
            if (duration <= 0f)
            {
                rect.anchoredPosition = target;
                yield break;
            }

            Vector2 start = rect.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                rect.anchoredPosition = Vector2.Lerp(start, target, k);
                yield return null;
            }
            rect.anchoredPosition = target;
        }
    }
}