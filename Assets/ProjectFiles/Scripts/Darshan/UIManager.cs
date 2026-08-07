using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public GameObject informationPanel;
    public TMP_Text descriptionText;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        informationPanel.SetActive(false);
    }

    public void ShowDescription(string description)
    {
        informationPanel.SetActive(true);
        descriptionText.text = description;
    }

    public void ClosePanel()
    {
        informationPanel.SetActive(false);
    }
}