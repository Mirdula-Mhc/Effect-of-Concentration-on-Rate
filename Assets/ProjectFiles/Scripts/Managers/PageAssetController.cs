using UnityEngine;
using System.Collections.Generic;

public class PageAssetController : MonoBehaviour
{
    [System.Serializable]
    public class PageAssetItem
    {
        public GameObject asset;

        [Tooltip("If true, this asset will be enabled only the first time this page is reached")]
        public bool enableOnce = false;

        [HideInInspector] public bool hasBeenActivated = false;
    }

    [System.Serializable]
    public class PageAssets
    {
        [Header("Page Info")]
        [Tooltip("Name of the page (for easier identification in Inspector)")]
        public string pageName;

        [Tooltip("Assets configuration for this page")]
        public List<PageAssetItem> assets = new List<PageAssetItem>();
    }

    [Header("All Page Assets (Assign every asset once here)")]
    [SerializeField] private List<GameObject> allAssets = new List<GameObject>();

    [Header("Per Page Asset Configuration (Index = Page Index)")]
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
        if (pageIndex < 0 || pageIndex >= pageAssets.Count)
            return;

        DisableAllAssets();

        PageAssets currentPage = pageAssets[pageIndex];

        if (currentPage == null || currentPage.assets == null)
            return;

        foreach (var item in currentPage.assets)
        {
            if (item == null || item.asset == null)
                continue;

            if (item.enableOnce)
            {
                if (item.hasBeenActivated)
                    continue;

                item.asset.SetActive(true);
                item.hasBeenActivated = true;
            }
            else
            {
                item.asset.SetActive(true);
            }
        }
    }

    private void DisableAllAssets()
    {
        foreach (GameObject obj in allAssets)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
}