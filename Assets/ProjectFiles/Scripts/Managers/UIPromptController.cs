using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class UIPromptController : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private PageData[] pages;

    [Header("Dialog UI")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private Image dialogImage;
    [SerializeField] private TextMeshProUGUI dialogText;

    [Header("Common Dialog Sprite")]
    [SerializeField] private Sprite commonDialogSprite;

    private int currentPageIndex = -1;

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void HandlePageChanged(int index)
    {
        if (index < 0 || index >= pages.Length)
            return;

        currentPageIndex = index;
        ShowPage(index);
    }

    private void ShowPage(int index)
    {
        PageData page = pages[index];

        if (dialogPanel)
            dialogPanel.SetActive(false);

        ResetAllPanels();

        if (!page.showDialogBox && !page.showAlternatePanels)
            return;

        if (page.showDialogBox)
        {
            if (dialogPanel)
                dialogPanel.SetActive(true);

            if (dialogText)
                dialogText.text = page.pageText;

            if (dialogImage)
                dialogImage.sprite = commonDialogSprite;
        }

        ApplyPanelVisibility(index);
    }

    private void ResetAllPanels()
    {
        foreach (var p in pages)
        {
            if (p.alternatePanels == null)
                continue;

            foreach (var panelData in p.alternatePanels)
            {
                if (panelData != null && panelData.panel != null)
                    panelData.panel.SetActive(false);
            }
        }
    }

    private void ApplyPanelVisibility(int currentIndex)
    {
        for (int i = 0; i <= currentIndex; i++)
        {
            PageData page = pages[i];

            if (!page.showAlternatePanels || page.alternatePanels == null)
                continue;

            foreach (var panelData in page.alternatePanels)
            {
                if (panelData == null || panelData.panel == null)
                    continue;

                // 🚀 NEW: Enable Once Logic
                if (panelData.enableOnce && panelData.hasBeenEnabledOnce)
                    continue;

                if (i == currentIndex)
                {
                    panelData.panel.SetActive(true);

                    if (panelData.enableOnce)
                        panelData.hasBeenEnabledOnce = true;
                }
                else if (panelData.stayInUpcomingPages)
                {
                    // Only allow staying panels if not restricted by enableOnce
                    if (!panelData.enableOnce || !panelData.hasBeenEnabledOnce)
                    {
                        panelData.panel.SetActive(true);
                    }
                }
            }
        }
    }
}

[System.Serializable]
public class PageData
{
    [Header("Page Name / Page No")]
    public string pageName;

    [TextArea]
    public string pageText;

    [Header("Display Options")]
    public bool showDialogBox;
    public bool showAlternatePanels;

    [Header("Alternate Panels For This Page")]
    public List<AlternatePanelData> alternatePanels;
}

[System.Serializable]
public class AlternatePanelData
{
    public GameObject panel;

    [Tooltip("If enabled, this panel will remain active in upcoming pages")]
    public bool stayInUpcomingPages;

    [Header("Enable Once Feature")]
    [Tooltip("If enabled, panel will activate only once and never again on revisit")]
    public bool enableOnce;

    [HideInInspector] public bool hasBeenEnabledOnce;
}

#if UNITY_EDITOR
[CustomEditor(typeof(UIPromptController))]
[CanEditMultipleObjects]
public class UIPromptControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        if (GUILayout.Button("Name Pages"))
        {
            foreach (var t in targets)
            {
                UIPromptController controller = (UIPromptController)t;
                NamePages(controller);
            }
        }
    }

    private void NamePages(UIPromptController controller)
    {
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty pagesProp = so.FindProperty("pages");

        if (pagesProp == null || pagesProp.arraySize == 0)
        {
            Debug.LogWarning("No pages found to rename.");
            return;
        }

        for (int i = 0; i < pagesProp.arraySize; i++)
        {
            SerializedProperty page = pagesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = page.FindPropertyRelative("pageName");

            if (nameProp != null)
            {
                nameProp.stringValue = $"Page {i + 1}";
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
    }
}
#endif