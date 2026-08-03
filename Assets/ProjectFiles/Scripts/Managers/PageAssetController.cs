using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PageAssetController : MonoBehaviour
{
    [System.Serializable]
    public class PageAssetGroup
    {
        [Tooltip("The page index this group belongs to (0-based, matches PageNavigationController's currentIndex)")]
        public int pageIndex;

        [Tooltip("All assets that should be active on this page")]
        public List<GameObject> assets = new List<GameObject>();
    }

    [Header("Assign each group of assets directly to the page index it belongs to")]
    [SerializeField] private List<PageAssetGroup> pageAssets = new List<PageAssetGroup>();

    // Derived automatically from pageAssets - no manual syncing needed
    private List<GameObject> AllAssets => pageAssets
        .Where(group => group != null && group.assets != null)
        .SelectMany(group => group.assets)
        .Where(asset => asset != null)
        .Distinct()
        .ToList();

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
        DisableAllAssets();

        foreach (var group in pageAssets)
        {
            if (group == null || group.assets == null)
                continue;

            if (group.pageIndex != pageIndex)
                continue; // not this page - stays disabled

            foreach (var asset in group.assets)
            {
                if (asset != null)
                    asset.SetActive(true);
            }
        }
    }

    private void DisableAllAssets()
    {
        foreach (GameObject obj in AllAssets)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
}