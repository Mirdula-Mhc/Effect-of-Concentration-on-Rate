using UnityEngine;
using System.Collections.Generic;

// -----------------------------------------------------------------
// One mechanism per page assumed. Page-indexed single AnimationSource
// that auto-plays the moment its page becomes current, no click or
// drag needed. Plays once; calls
// PageNavigationController.RequestNavigationUnlock() on completion.
// -----------------------------------------------------------------
public class PageEnterAnimManager : MonoBehaviour
{
    [System.Serializable]
    public class PageEntry
    {
        public int pageIndex;
        public AnimationSource animation;
    }

    [Header("Per-Page Auto-Play Entries (one per page)")]
    public List<PageEntry> entries = new List<PageEntry>();

    private readonly HashSet<int> finishedPages = new HashSet<int>();
    private int lastPageIndex = -1;

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
        SetPageContext(PageNavigationController.CurrentIndex);
    }

    private void SetPageContext(int pageIndex)
    {
        if (pageIndex == lastPageIndex) return;
        lastPageIndex = pageIndex;

        if (finishedPages.Contains(pageIndex)) return;

        PageEntry entry = FindEntry(pageIndex);
        if (entry == null || entry.animation == null || !entry.animation.IsValid)
            return; // nothing configured for this page - no gate to clear

        finishedPages.Add(pageIndex);
        StartCoroutine(entry.animation.Play(this, () => PageNavigationController.RequestNavigationUnlock()));
    }

    private PageEntry FindEntry(int pageIndex)
    {
        return entries.Find(e => e != null && e.pageIndex == pageIndex);
    }

    public bool OwnsPage(int pageIndex) => FindEntry(pageIndex) != null;
}