using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag & Snap manager for UI Text elements that START on a World Space
/// canvas and must be DROPPED onto slots on a Screen Space - Overlay canvas.
///
/// - draggables[i] (World Space) can ONLY snap correctly onto snapTargets[i]
///   (Screen Space - Overlay), index-paired.
/// - While dragging, the item is moved onto the Overlay canvas so it follows
///   the pointer in screen space (dragging a World Space object directly with
///   screen-space math would not track the pointer correctly).
/// - Wrong drop -> returns to its original World Space parent/position/rotation/scale.
/// - Correct drop -> reparents onto the target on the Overlay canvas and locks
///   permanently (no further dragging).
/// - Once every assigned draggable is correctly snapped, calls
///   PageNavigationController.RequestNavigationUnlock() exactly once.
///
/// SETUP:
/// 1. Put this script on any GameObject.
/// 2. Assign "worldCanvas" (the World Space canvas the draggables start on)
///    and "overlayCanvas" (the Screen Space - Overlay canvas the targets live on).
/// 3. Assign draggables[] (World Space RectTransforms) and snapTargets[]
///    (Overlay RectTransforms), index-matched.
/// 4. Each draggable needs a CanvasGroup (auto-added if missing).
/// </summary>
public class WorldToScreenSnapManager : MonoBehaviour
{
    [Header("Canvases")]
    [Tooltip("The World Space canvas the draggable texts start on.")]
    public Canvas worldCanvas;
    [Tooltip("The Screen Space - Overlay canvas the snap targets live on.")]
    public Canvas overlayCanvas;

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

        // Original World Space placement, restored on a wrong drop.
        public Transform originalParent;
        public int originalSiblingIndex;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
        public Vector3 originalLocalScale;

        public bool isLocked;
        public CanvasGroup canvasGroup;
        public DragHandler handler;

        // Offset (in overlay-canvas local space) between the pointer's local
        // point and the item's anchoredPosition, captured at drag start.
        public Vector2 grabOffset;
    }

    private readonly Dictionary<RectTransform, DragState> _states = new Dictionary<RectTransform, DragState>();
    private int _correctSnapCount;
    private bool _allSnappedFired;

    private void Awake()
    {
        if (worldCanvas == null || overlayCanvas == null)
        {
            Debug.LogError("[WorldToScreenSnapManager] worldCanvas and overlayCanvas must both be assigned.");
        }

        SetupAll();
    }

    private void SetupAll()
    {
        if (draggables.Count != snapTargets.Count)
        {
            Debug.LogError($"[WorldToScreenSnapManager] draggables ({draggables.Count}) and snapTargets ({snapTargets.Count}) counts must match (index-paired).");
        }

        int count = Mathf.Min(draggables.Count, snapTargets.Count);
        for (int i = 0; i < count; i++)
        {
            RectTransform d = draggables[i];
            RectTransform t = snapTargets[i];
            if (d == null || t == null)
            {
                Debug.LogWarning($"[WorldToScreenSnapManager] Null entry at index {i}, skipping.");
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

            var handler = d.GetComponent<DragHandler>();
            if (handler == null) handler = d.gameObject.AddComponent<DragHandler>();
            handler.Init(this, d);
            state.handler = handler;

            _states[d] = state;
        }
    }

    /// <summary>Camera used to read World Space canvas positions (its own worldCamera, falling back to Camera.main).</summary>
    private Camera WorldCam => worldCanvas != null && worldCanvas.worldCamera != null
        ? worldCanvas.worldCamera
        : Camera.main;
    internal void HandleBeginDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = false;

        // Capture the item's CURRENT on-screen position (in screen pixels) while
        // it's still on the World Space canvas, using that canvas's own camera.
        // This is what actually determines where it visually sits right now -
        // its World Space anchoredPosition/localPosition can't be compared
        // directly to Overlay space, but a screen point can.
        Camera worldCam = WorldCam;
        Vector2 screenPointBeforeReparent = RectTransformUtility.WorldToScreenPoint(worldCam, draggedRect.position);

        // Now move the item onto the Overlay canvas for the duration of the drag,
        // so it can be positioned with plain screen-space math (no camera needed)
        // and will render above everything else while dragging.
        draggedRect.SetParent(overlayCanvas.transform, true);
        draggedRect.SetAsLastSibling();

        // Reset scale/rotation - the World Space canvas is usually scaled way
        // down (e.g. 0.001-0.02), and that leftover scale/rotation would skew
        // all anchoredPosition math from here on.
        draggedRect.localRotation = Quaternion.identity;
        draggedRect.localScale = Vector3.one;

        RectTransform overlayRect = overlayCanvas.transform as RectTransform;

        // Convert the item's PRE-reparent screen position into a local point in
        // the Overlay canvas - this is its correct starting anchoredPosition,
        // regardless of whatever SetParent() left anchoredPosition as.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayRect, screenPointBeforeReparent, null, out Vector2 itemLocalInOverlay);
        draggedRect.anchoredPosition = itemLocalInOverlay;

        // Now compute the grab offset using that CORRECT anchoredPosition against
        // the pointer's current local point, so the exact grab point stays glued
        // to the finger/cursor for the rest of the drag.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRect, eventData.position, null, out Vector2 pointerLocal))
        {
            state.grabOffset = itemLocalInOverlay - pointerLocal;
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

        // Now parented under the Overlay canvas, so this is plain screen-space
        // math with a null camera - same as ScreenToScreenSnapManager.
        RectTransform overlayRect = overlayCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRect, eventData.position, null, out Vector2 pointerLocal))
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
    /// All snap targets live on the Overlay canvas, so this always uses a null camera.
    /// </summary>
    private RectTransform FindTargetUnderPointer(PointerEventData eventData)
    {
        for (int i = 0; i < snapTargets.Count; i++)
        {
            RectTransform target = snapTargets[i];
            if (target == null) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(target, eventData.position, null))
            {
                return target;
            }
        }
        return null;
    }

    /// <summary>
    /// Correct drop: item is already parented under the Overlay canvas (moved
    /// there at drag start), so this is a simple reparent onto the target with
    /// a zeroed anchored position - no further conversion needed.
    /// </summary>
    private void SnapCorrect(DragState state)
    {
        state.isLocked = true;
        state.canvasGroup.blocksRaycasts = false;
        state.handler.enabled = false;

        state.rect.SetParent(state.correctTarget, false);
        state.rect.SetAsLastSibling();
        state.rect.localRotation = Quaternion.identity;
        state.rect.localScale = Vector3.one;
        state.rect.anchoredPosition = Vector2.zero;

        _correctSnapCount++;
        CheckAllSnapped();
    }

    /// <summary>
    /// Wrong drop: send the item back to its ORIGINAL World Space parent,
    /// sibling index, local position, rotation, and scale.
    /// </summary>
    private void SnapBack(DragState state)
    {
        state.rect.SetParent(state.originalParent, false);
        state.rect.SetSiblingIndex(state.originalSiblingIndex);
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
    /// Lightweight per-element drag handler. Auto-attached by WorldToScreenSnapManager.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private WorldToScreenSnapManager _manager;
        private RectTransform _rect;

        public void Init(WorldToScreenSnapManager manager, RectTransform rect)
        {
            _manager = manager;
            _rect = rect;
        }

        public void OnBeginDrag(PointerEventData eventData) => _manager.HandleBeginDrag(_rect, eventData);
        public void OnDrag(PointerEventData eventData) => _manager.HandleDrag(_rect, eventData);
        public void OnEndDrag(PointerEventData eventData) => _manager.HandleEndDrag(_rect, eventData);

        public IEnumerator AnimateTo(RectTransform rect, Vector2 target, float duration)
        {
            if (duration <= 0f) { rect.anchoredPosition = target; yield break; }
            Vector2 start = rect.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = 1f - Mathf.Pow(1f - k, 3f);
                rect.anchoredPosition = Vector2.Lerp(start, target, k);
                yield return null;
            }
            rect.anchoredPosition = target;
        }

        public IEnumerator AnimateLocalPositionTo(RectTransform rect, Vector3 target, float duration)
        {
            if (duration <= 0f) { rect.localPosition = target; yield break; }
            Vector3 start = rect.localPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = 1f - Mathf.Pow(1f - k, 3f);
                rect.localPosition = Vector3.Lerp(start, target, k);
                yield return null;
            }
            rect.localPosition = target;
        }
    }
}