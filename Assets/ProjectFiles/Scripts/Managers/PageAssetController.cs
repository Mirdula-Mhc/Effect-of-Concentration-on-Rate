using UnityEngine;
using System.Collections.Generic;

public class PageAssetController : MonoBehaviour
{
    [System.Serializable]
    public class ObjectState
    {
        public GameObject asset;

        [Tooltip("Should this object be active on this page?")]
        public bool active;
    }

    [System.Serializable]
    public class PageAssets
    {
        [Tooltip("The page index this list applies to (0-based, matches PageNavigationController's currentIndex)")]
        public int pageIndex;

        [Tooltip("Every object and its state for this page. Add as many objects as you need here.")]
        public List<ObjectState> objects = new List<ObjectState>();
    }

    [Header("One entry per page. Inside each, list every object and whether it should be active.")]
    [SerializeField] private List<PageAssets> pageAssets = new List<PageAssets>();

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void HandlePageChanged(int pageIndex)
    {
        foreach (var page in pageAssets)
        {
            if (page == null || page.objects == null)
                continue;

            if (page.pageIndex != pageIndex)
                continue; // not this page, don't touch these objects

            foreach (var obj in page.objects)
            {
                if (obj == null || obj.asset == null)
                    continue;

                obj.asset.SetActive(obj.active);
            }
        }
    }
}