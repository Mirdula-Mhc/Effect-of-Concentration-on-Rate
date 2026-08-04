using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// -----------------------------------------------------------------
// One mechanism per page assumed. Page-indexed list of entries -
// each points at a Collider (the draggable) + a snap-point Transform
// + the AnimationSource to play on a precise snap. No separate
// "object" component needed - manager owns all drag state directly,
// keyed by page (only one entry is ever live at a time).
//
// Input is routed through Pointer.current (new Input System) rather
// than OnMouseDown, so this behaves consistently for touch on WebGL
// builds, matching ClickAnimManager's raycast approach.
// -----------------------------------------------------------------
public class DragDropAnimManager : MonoBehaviour
{
    [System.Serializable]
    public class PageEntry
    {
        public int pageIndex;

        [Tooltip("The collider on the object the user drags.")]
        public Collider dragTarget;

        [Tooltip("Where the object must be dropped to snap.")]
        public Transform snapPoint;

        [Tooltip("Max distance (world units) from snapPoint to count as a valid drop.")]
        public float snapDistance = 0.05f;
        [Tooltip("Max rotation difference (degrees) from snapPoint to count as a valid drop.")]
        public float snapAngle = 5f;

        [Tooltip("Renderers to highlight while this page's object is waiting to be dragged.")]
        public List<Renderer> targetRenderers = new List<Renderer>();
        public Material highlightMaterial;

        public AnimationSource animation;

        [HideInInspector] public List<Material> originalMaterials;
    }

    [Header("Per-Page Drag-Drop Entries (one per page)")]
    public List<PageEntry> entries = new List<PageEntry>();

    [Header("Drag Detection")]
    public Camera raycastCamera;
    public LayerMask draggableLayers = ~0;

    private int currentPageIndex = -1;
    private readonly HashSet<int> finishedPages = new HashSet<int>();

    private bool dragging = false;
    private Transform draggedTransform;
    private Vector3 dragPlaneOffset;
    private float dragPlaneHeight;

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += SetPageContext;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= SetPageContext;
    }

    private void OnDestroy()
    {
        PageNavigationController.OnPageChanged -= SetPageContext;
    }

    private void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;

        SetPageContext(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        if (Pointer.current == null) return;
        if (currentPageIndex < 0) return;
        if (finishedPages.Contains(currentPageIndex)) return;

        PageEntry entry = FindEntry(currentPageIndex);
        if (entry == null || entry.dragTarget == null || entry.snapPoint == null) return;

        if (raycastCamera == null) raycastCamera = Camera.main;
        if (raycastCamera == null) return;

        if (!dragging && Pointer.current.press.wasPressedThisFrame)
        {
            TryBeginDrag(entry);
        }
        else if (dragging && Pointer.current.press.isPressed)
        {
            ContinueDrag();
        }
        else if (dragging && Pointer.current.press.wasReleasedThisFrame)
        {
            EndDrag(currentPageIndex, entry);
        }
    }

    private void TryBeginDrag(PageEntry entry)
    {
        Ray ray = raycastCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, draggableLayers))
            return;

        if (hit.collider != entry.dragTarget &&
            hit.collider.transform.GetComponentInParent<Collider>() != entry.dragTarget)
            return;

        draggedTransform = entry.dragTarget.transform;
        dragPlaneHeight = draggedTransform.position.y;
        dragging = true;

        Vector3 pointerWorld = ScreenToPlanePoint(Pointer.current.position.ReadValue());
        dragPlaneOffset = draggedTransform.position - pointerWorld;
    }

    private void ContinueDrag()
    {
        if (draggedTransform == null) return;
        Vector3 pointerWorld = ScreenToPlanePoint(Pointer.current.position.ReadValue());
        draggedTransform.position = pointerWorld + dragPlaneOffset;
    }

    private void EndDrag(int pageIndex, PageEntry entry)
    {
        dragging = false;
        EvaluateDrop(pageIndex, entry);
        draggedTransform = null;
    }

    private Vector3 ScreenToPlanePoint(Vector2 screenPos)
    {
        Ray ray = raycastCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0, dragPlaneHeight, 0));
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return draggedTransform != null ? draggedTransform.position : Vector3.zero;
    }

    private void EvaluateDrop(int pageIndex, PageEntry entry)
    {
        Transform obj = entry.dragTarget.transform;
        float dist = Vector3.Distance(obj.position, entry.snapPoint.position);
        float angle = Quaternion.Angle(obj.rotation, entry.snapPoint.rotation);

        if (dist <= entry.snapDistance && angle <= entry.snapAngle)
        {
            obj.position = entry.snapPoint.position;
            obj.rotation = entry.snapPoint.rotation;
            OnSnapped(pageIndex, entry);
        }
        // else: leave it where released; user can pick it back up and try again
    }

    private void OnSnapped(int pageIndex, PageEntry entry)
    {
        finishedPages.Add(pageIndex);

        ClearHighlight(entry);

        if (entry.animation != null && entry.animation.IsValid)
        {
            StartCoroutine(entry.animation.Play(this, () => PageNavigationController.RequestNavigationUnlock()));
        }
        else
        {
            Debug.LogWarning($"[DragDropAnimManager] Page {pageIndex}: no valid AnimationSource - unlocking immediately.");
            PageNavigationController.RequestNavigationUnlock();
        }
    }

    private void SetPageContext(int pageIndex)
    {
        currentPageIndex = pageIndex;
        dragging = false;
        draggedTransform = null;

        PageEntry entry = FindEntry(pageIndex);
        if (entry == null || finishedPages.Contains(pageIndex))
            return;

        ApplyHighlight(entry);
    }

    private void ApplyHighlight(PageEntry entry)
    {
        if (entry.targetRenderers == null || entry.targetRenderers.Count == 0 || entry.highlightMaterial == null)
            return;

        if (entry.originalMaterials == null)
        {
            entry.originalMaterials = new List<Material>();
            foreach (var r in entry.targetRenderers)
                entry.originalMaterials.Add(r != null ? r.material : null);
        }

        foreach (var r in entry.targetRenderers)
            if (r != null) r.material = entry.highlightMaterial;
    }

    private void ClearHighlight(PageEntry entry)
    {
        if (entry.targetRenderers == null || entry.originalMaterials == null) return;

        for (int i = 0; i < entry.targetRenderers.Count; i++)
            if (entry.targetRenderers[i] != null && entry.originalMaterials[i] != null)
                entry.targetRenderers[i].material = entry.originalMaterials[i];
    }

    private PageEntry FindEntry(int pageIndex)
    {
        return entries.Find(e => e != null && e.pageIndex == pageIndex);
    }

    public bool OwnsPage(int pageIndex) => FindEntry(pageIndex) != null;
}