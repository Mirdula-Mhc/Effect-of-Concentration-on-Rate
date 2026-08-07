using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// -----------------------------------------------------------------
// One mechanism per page assumed. Page-indexed list of entries -
// each just points at a Collider + Renderers to highlight, plus the
// AnimationSource to play on click. No separate "object" component
// needed - the manager owns all click/highlight state directly,
// keyed by page (only one entry is ever live at a time).
// -----------------------------------------------------------------
public class ClickAnimManager : MonoBehaviour
{
    [System.Serializable]
    public class PageEntry
    {
        public int pageIndex;

        [Tooltip("The collider to raycast against for this page's click target.")]
        public Collider clickTarget;

        [Tooltip("Renderers to highlight while waiting for the click.")]
        public List<Renderer> targetRenderers = new List<Renderer>();
        public Material highlightMaterial;

        public AnimationSource animation;

        [HideInInspector] public List<Material> originalMaterials;
    }

    [Header("Per-Page Click-Anim Entries (one per page)")]
    public List<PageEntry> entries = new List<PageEntry>();

    [Header("3D Click Detection")]
    public Camera raycastCamera;
    public LayerMask clickableLayers = ~0;

    private int currentPageIndex = -1;
    private readonly HashSet<int> finishedPages = new HashSet<int>();

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
        if (!Pointer.current.press.wasPressedThisFrame) return;
        if (currentPageIndex < 0) return;
        if (finishedPages.Contains(currentPageIndex)) return;

        PageEntry entry = FindEntry(currentPageIndex);
        if (entry == null || entry.clickTarget == null) return;

        if (raycastCamera == null) raycastCamera = Camera.main;
        if (raycastCamera == null) return;

        Ray ray = raycastCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, clickableLayers))
            return;

        // Match either the exact collider or something parented under it.
        if (hit.collider != entry.clickTarget &&
            hit.collider.transform.GetComponentInParent<Collider>() != entry.clickTarget)
            return;

        OnEntryClicked(currentPageIndex, entry);
    }

    private void SetPageContext(int pageIndex)
    {
        // Clear whatever was highlighted on the page we're leaving.
        PageEntry previousEntry = FindEntry(currentPageIndex);
        if (previousEntry != null)
            ClearHighlight(previousEntry);

        currentPageIndex = pageIndex;

        PageEntry entry = FindEntry(pageIndex);
        if (entry == null || finishedPages.Contains(pageIndex))
            return;

        ApplyHighlight(entry);
    }

    private void ApplyHighlight(PageEntry entry)
    {
        if (entry.targetRenderers == null || entry.targetRenderers.Count == 0 || entry.highlightMaterial == null)
            return;

        // Rebuild the cache if it's missing or out of sync with the current
        // renderer list (e.g. the list was edited in the Inspector after a
        // previous highlight pass already cached it).
        if (entry.originalMaterials == null || entry.originalMaterials.Count != entry.targetRenderers.Count)
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

        int count = Mathf.Min(entry.targetRenderers.Count, entry.originalMaterials.Count);
        for (int i = 0; i < count; i++)
            if (entry.targetRenderers[i] != null && entry.originalMaterials[i] != null)
                entry.targetRenderers[i].material = entry.originalMaterials[i];
    }

    private void OnEntryClicked(int pageIndex, PageEntry entry)
    {
        finishedPages.Add(pageIndex); // mark immediately, prevents double-trigger

        ClearHighlight(entry);

        if (entry.animation != null && entry.animation.IsValid)
        {
            StartCoroutine(entry.animation.Play(this, () => PageNavigationController.RequestNavigationUnlock()));
        }
        else
        {
            Debug.LogWarning($"[ClickAnimManager] Page {pageIndex}: no valid AnimationSource - unlocking immediately.");
            PageNavigationController.RequestNavigationUnlock();
        }
    }

    private PageEntry FindEntry(int pageIndex)
    {
        return entries.Find(e => e != null && e.pageIndex == pageIndex);
    }

    public bool OwnsPage(int pageIndex) => FindEntry(pageIndex) != null;
}