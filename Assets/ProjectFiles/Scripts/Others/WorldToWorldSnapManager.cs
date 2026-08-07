using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag & Snap manager for UI elements where BOTH the draggables and the
/// snap targets live on the SAME World Space canvas (or on World Space
/// canvases sharing one worldCamera). No reparenting across canvases and
/// no Screen Space math is needed for the drop check itself - drops are
/// detected in World Space using the canvas's own camera. Dragging still
/// tracks the pointer using that camera to convert screen position into
/// this canvas's local space each frame.
///
/// - draggables[i] can ONLY snap correctly onto snapTargets[i] (index-paired).
/// - Wrong drop -> returns to its original parent/local position/rotation/scale.
/// - Correct drop -> reparents onto the target, zeroes local position/rotation,
///   resets scale to one, and locks permanently (no further dragging).
/// - Once every assigned draggable is correctly snapped, calls
///   PageNavigationController.RequestNavigationUnlock() exactly once.
///
/// SETUP:
/// 1. Put this script on any GameObject.
/// 2. Assign "worldCanvas" (the World Space canvas both draggables and
///    targets live on). Its Event Camera / worldCamera is used for all
///    screen<->world conversions.
/// 3. Assign draggables[] and snapTargets[] (World Space RectTransforms),
///    index-matched.
/// 4. Each draggable needs a CanvasGroup (auto-added if missing).
/// </summary>
public class WorldToWorldSnapManager : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("The World Space canvas both draggables and snap targets live on.")]
    public Canvas worldCanvas;

    [Header("Index-paired lists: draggables[i] snaps correctly onto snapTargets[i]")]
    public List<RectTransform> draggables = new List<RectTransform>();
    public List<RectTransform> snapTargets = new List<RectTransform>();

    [Header("Snap animation")]
    [Tooltip("Seconds to animate into place on snap / return. Set 0 for instant.")]
    public float snapAnimDuration = 0.15f;

    private class DragState
    {
        public RectTransform rect;
        public RectTransform correctTarget;

        // Original placement, restored on a wrong drop.
        public Transform originalParent;
        public int originalSiblingIndex;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
        public Vector3 originalLocalScale;

        public bool isLocked;
        public CanvasGroup canvasGroup;
        public DragHandler handler;

        // Offset (in the dragged item's parent-local space) between the
        // pointer's local point and the item's localPosition at the moment
        // the drag started. Keeping this constant for the whole drag is what
        // makes it track the finger/cursor precisely instead of snapping its
        // pivot to the pointer.
        public Vector3 grabOffset;
    }

    private readonly Dictionary<RectTransform, DragState> _states = new Dictionary<RectTransform, DragState>();
    private int _correctSnapCount;
    private bool _allSnappedFired;

    /// <summary>The camera used for all World Space screen<->local conversions.</summary>
    private Camera WorldCam => worldCanvas != null && worldCanvas.worldCamera != null
        ? worldCanvas.worldCamera
        : Camera.main;

    private void Awake()
    {
        if (worldCanvas == null)
        {
            Debug.LogError("[WorldToWorldSnapManager] worldCanvas must be assigned.");
        }

        SetupAll();
    }

    private void SetupAll()
    {
        if (draggables.Count != snapTargets.Count)
        {
            Debug.LogError($"[WorldToWorldSnapManager] draggables ({draggables.Count}) and snapTargets ({snapTargets.Count}) counts must match (index-paired).");
        }

        int count = Mathf.Min(draggables.Count, snapTargets.Count);
        for (int i = 0; i < count; i++)
        {
            RectTransform d = draggables[i];
            RectTransform t = snapTargets[i];
            if (d == null || t == null)
            {
                Debug.LogWarning($"[WorldToWorldSnapManager] Null entry at index {i}, skipping.");
                continue;
            }

            var state = new DragState
            {
                rect = d,
                correctTarget = t,
                originalParent = d.parent,
                originalSiblingIndex = d.GetSiblingIndex(),
                originalLocalPos = d.localPosition,
                originalLocalRot = d.localRotation,
                originalLocalScale = d.localScale,
                isLocked = false
            };

            var cg = d.GetComponent<CanvasGroup>();
            if (cg == null) cg = d.gameObject.AddComponent<CanvasGroup>();
            state.canvasGroup = cg;

            // Remove any leftover DragHandler left behind by a DIFFERENT snap
            // manager script (e.g. WorldToScreenSnapManager, ScreenToScreenSnapManager)
            // that was previously set up on this same GameObject. Each manager's
            // DragHandler is its own nested type, so any component named
            // "DragHandler" that isn't THIS manager's own type is a leftover from
            // another manager and would keep forwarding drag events to it,
            // silently fighting with this manager for control of the object.
            RemoveForeignDragHandlers(d);

            var handler = d.GetComponent<DragHandler>();
            if (handler == null) handler = d.gameObject.AddComponent<DragHandler>();
            handler.Init(this, d);
            state.handler = handler;

            _states[d] = state;
        }
    }

    /// <summary>
    /// Destroys any component named "DragHandler" on this GameObject that does
    /// NOT belong to this manager (i.e. was added by a different snap manager
    /// script). Safe to call even if none exist.
    /// </summary>
    private void RemoveForeignDragHandlers(RectTransform target)
    {
        var allBehaviours = target.GetComponents<MonoBehaviour>();
        foreach (var behaviour in allBehaviours)
        {
            if (behaviour == null) continue;
            if (behaviour.GetType().Name == nameof(DragHandler) && behaviour.GetType() != typeof(DragHandler))
            {
                Debug.LogWarning($"[WorldToWorldSnapManager] Removing leftover DragHandler from another manager ({behaviour.GetType().DeclaringType?.Name}) on '{target.name}'.");
                Destroy(behaviour);
            }
        }
    }

    internal void HandleBeginDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = false;
        draggedRect.SetAsLastSibling(); // render above everything while dragging

        // Convert the pointer's current screen position into this item's
        // parent-local space (World Space, using the canvas's own camera),
        // then store the offset from that point to the item's current
        // localPosition. Applying this same offset every frame keeps the
        // exact grab point glued to the pointer for the rest of the drag.
        RectTransform parentRect = draggedRect.parent as RectTransform;
        if (TryGetWorldLocalPoint(parentRect, eventData.position, out Vector3 pointerLocal))
        {
            state.grabOffset = draggedRect.localPosition - pointerLocal;
        }
        else
        {
            state.grabOffset = Vector3.zero;
        }
    }

    internal void HandleDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        RectTransform parentRect = draggedRect.parent as RectTransform;
        if (TryGetWorldLocalPoint(parentRect, eventData.position, out Vector3 pointerLocal))
        {
            draggedRect.localPosition = pointerLocal + state.grabOffset;
        }
    }

    internal void HandleEndDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = true;
        HandleDrop(draggedRect, eventData);
    }

    internal void HandleDrop(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        RectTransform hitTarget = FindTargetUnderPointer(eventData);

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
    /// Converts a screen point into a local point inside the given World
    /// Space RectTransform, using this manager's worldCanvas camera. Uses
    /// Unity's built-in ScreenPointToWorldPointInRectangle rather than a
    /// manual plane raycast - a manual raycast can return a wildly wrong,
    /// far-away distance when the ray is nearly parallel to the canvas
    /// plane (a tiny numerical error gets massively amplified), which is
    /// what causes items to "fly away"/repel on drop. This built-in utility
    /// avoids that failure mode entirely.
    /// </summary>
    private bool TryGetWorldLocalPoint(RectTransform targetRect, Vector2 screenPoint, out Vector3 localPoint)
    {
        localPoint = Vector3.zero;
        if (targetRect == null || WorldCam == null) return false;

        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                targetRect, screenPoint, WorldCam, out Vector3 worldPoint))
            return false;

        localPoint = targetRect.InverseTransformPoint(worldPoint);
        return true;
    }

    /// <summary>
    /// Checks every snap target's RectTransform world-space bounds against the
    /// pointer's current screen position, using the canvas's own camera.
    /// </summary>
    private RectTransform FindTargetUnderPointer(PointerEventData eventData)
    {
        for (int i = 0; i < snapTargets.Count; i++)
        {
            RectTransform target = snapTargets[i];
            if (target == null) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(target, eventData.position, WorldCam))
            {
                return target;
            }
        }
        return null;
    }

    /// <summary>
    /// Correct drop: reparent onto the target and zero out local position/
    /// rotation/scale. Since both draggable and target are on the same
    /// World Space canvas, no cross-canvas conversion is needed.
    /// </summary>
    private void SnapCorrect(DragState state)
    {
        state.isLocked = true;
        state.canvasGroup.blocksRaycasts = false;
        state.handler.enabled = false;

        // Cancel any in-flight snap-back animation from a previous wrong
        // drop attempt - otherwise that coroutine keeps running and will
        // overwrite this correct placement a moment later, making the item
        // appear to "snap to the target then jump back".
        state.handler.StopAllCoroutines();

        state.rect.SetParent(state.correctTarget, false);
        state.rect.SetAsLastSibling();
        state.rect.localRotation = Quaternion.identity;
        state.rect.localScale = Vector3.one;
        state.rect.localPosition = Vector3.zero;

        _correctSnapCount++;
        CheckAllSnapped();
    }

    /// <summary>
    /// Wrong drop: restore original parent, sibling order, local position,
    /// rotation, and scale exactly.
    /// </summary>
    private void SnapBack(DragState state)
    {
        if (state.rect.parent != state.originalParent)
        {
            state.rect.SetParent(state.originalParent, false);
            state.rect.SetSiblingIndex(state.originalSiblingIndex);
        }
        state.rect.localRotation = state.originalLocalRot;
        state.rect.localScale = state.originalLocalScale;

        var handler = state.rect.GetComponent<DragHandler>();
        if (handler != null)
        {
            handler.StopAllCoroutines();
            handler.StartCoroutine(handler.AnimateLocalPositionTo(state.rect, state.originalLocalPos, snapAnimDuration));
        }
        else
        {
            state.rect.localPosition = state.originalLocalPos;
        }
    }

    private void CheckAllSnapped()
    {
        if (_allSnappedFired) return;
        if (_correctSnapCount >= _states.Count && _states.Count > 0)
        {
            _allSnappedFired = true;
            PageNavigationController.RequestNavigationUnlock();
        }
    }

    public bool AllSnapped => _allSnappedFired;
    public int CorrectSnapCount => _correctSnapCount;
    public int TotalDraggables => _states.Count;

    /// <summary>
    /// Lightweight per-element drag handler. Auto-attached by WorldToWorldSnapManager.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private WorldToWorldSnapManager _manager;
        private RectTransform _rect;

        public void Init(WorldToWorldSnapManager manager, RectTransform rect)
        {
            _manager = manager;
            _rect = rect;
        }

        public void OnBeginDrag(PointerEventData eventData) => _manager.HandleBeginDrag(_rect, eventData);
        public void OnDrag(PointerEventData eventData) => _manager.HandleDrag(_rect, eventData);
        public void OnEndDrag(PointerEventData eventData) => _manager.HandleEndDrag(_rect, eventData);

        public IEnumerator AnimateLocalPositionTo(RectTransform rect, Vector3 target, float duration)
        {
            if (duration <= 0f) { rect.localPosition = target; yield break; }
            Vector3 start = rect.localPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                rect.localPosition = Vector3.Lerp(start, target, k);
                yield return null;
            }
            rect.localPosition = target;
        }
    }
}
