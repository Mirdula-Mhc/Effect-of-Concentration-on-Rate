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

        // Delta-tracking fields: instead of recomputing an absolute position
        // each frame (sensitive to canvas tilt/camera angle distortion), we
        // track how far the POINTER has moved on screen since the drag
        // started, then apply that same movement as a world-space
        // displacement using the camera at a fixed depth. This tracks the
        // cursor/finger reliably regardless of the canvas's own rotation.
        public Vector2 dragStartScreenPos;
        public Vector3 dragStartWorldPos;
        public float dragDepth;
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

        // Record the pointer's starting screen position, the item's starting
        // WORLD position, and its distance from the camera (depth). Every
        // frame, we'll measure how far the pointer has moved in screen space
        // since this moment, convert that same movement into a world-space
        // displacement at this fixed depth, and apply it to the item's
        // starting world position. This tracks the cursor/finger reliably
        // even if the canvas itself is tilted relative to the camera.
        state.dragStartScreenPos = eventData.position;
        state.dragStartWorldPos = draggedRect.position;
        state.dragDepth = WorldCam != null
            ? Vector3.Distance(WorldCam.transform.position, draggedRect.position)
            : 1f;
    }

    internal void HandleDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;
        if (WorldCam == null) return;

        // How far has the pointer moved on screen since the drag started?
        Vector2 currentScreenPos = eventData.position;

        // Convert both the start and current screen positions into world
        // points at the SAME fixed depth (distance from camera captured at
        // drag start). The difference between those two world points is the
        // exact world-space displacement the pointer has made - applying it
        // to the item's starting world position tracks the cursor/finger
        // precisely, regardless of the canvas's own tilt or rotation.
        Vector3 startWorld = WorldCam.ScreenToWorldPoint(
            new Vector3(state.dragStartScreenPos.x, state.dragStartScreenPos.y, state.dragDepth));
        Vector3 currentWorld = WorldCam.ScreenToWorldPoint(
            new Vector3(currentScreenPos.x, currentScreenPos.y, state.dragDepth));

        Vector3 worldDelta = currentWorld - startWorld;
        draggedRect.position = state.dragStartWorldPos + worldDelta;
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
            Debug.Log($"[WorldToWorldSnapManager] All {_states.Count} draggables correctly snapped - unlocking Next page.");
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