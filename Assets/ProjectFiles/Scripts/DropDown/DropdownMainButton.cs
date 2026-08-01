using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class DropdownMainButton : MonoBehaviour
{
    public enum SpawnSide { Left, Right }

    [Header("Data")]
    [SerializeField] private DropdownData data;

    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private RectTransform popupPrefab;
    [SerializeField] private Transform popupParent;

    [Header("Output")]
    [SerializeField] private TMP_Text outputText;

    [Header("Feedback Prefabs")]
    [SerializeField] private GameObject correctPrefab;
    [SerializeField] private GameObject wrongPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private SpawnSide spawnSide = SpawnSide.Right;
    [SerializeField] private float extraOffset = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctClip;
    [SerializeField] private AudioClip wrongClip;

    [Header("Optional Behaviour")]
    [SerializeField] private bool disableButtonOnCorrect = true;

    [Header("Popup Animation")]
    [SerializeField] private DropdownPopup.Direction direction;
    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private float distance = 100f;

    private RectTransform rectTransform;
    private RectTransform currentPopup;
    private DropdownPopup popupScript;
    private GameObject currentFeedback;

    private bool hasSelected = true;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (button != null)
            button.onClick.AddListener(OnMainClick);
    }

    private void OnMainClick()
    {
        if (currentPopup != null && !hasSelected)
            return;

        if (currentPopup != null)
        {
            currentPopup.gameObject.SetActive(true);

            popupScript.ApplyAnimationSettings(direction, animDuration, distance);
            popupScript.PlayAnimation();

            hasSelected = false;
            return;
        }

        currentPopup = Instantiate(popupPrefab, popupParent);
        currentPopup.position = transform.position;

        popupScript = currentPopup.GetComponent<DropdownPopup>();

        popupScript.ApplyAnimationSettings(direction, animDuration, distance);
        popupScript.Initialize(data, this);

        hasSelected = false;
    }

    public void OnOptionSelected(int index, bool isCorrect, string text)
    {
        hasSelected = true;

        // Update text
        if (outputText != null)
            outputText.text = text;

        // Remove previous feedback
        if (currentFeedback != null)
            Destroy(currentFeedback);

        // Choose prefab
        GameObject prefab = isCorrect ? correctPrefab : wrongPrefab;

        if (prefab != null)
        {
            currentFeedback = Instantiate(prefab, transform);

            RectTransform feedbackRect = currentFeedback.GetComponent<RectTransform>();

            float halfWidth = rectTransform.rect.width * 0.5f;
            float directionMultiplier = (spawnSide == SpawnSide.Right) ? 1f : -1f;
            float xPos = (halfWidth * directionMultiplier) + (extraOffset * directionMultiplier);

            feedbackRect.anchoredPosition = new Vector2(xPos, 0f);
            feedbackRect.localScale = Vector3.one;
        }

        // Audio
        if (audioSource != null)
            audioSource.PlayOneShot(isCorrect ? correctClip : wrongClip);

        if (isCorrect)
        {
            // 🔥 KEY CHANGE → Unlock Navigation
            PageNavigationController.RequestNavigationUnlock();

            if (disableButtonOnCorrect)
            {
                var img = GetComponent<Image>();
                if (img) Destroy(img);

                if (button) Destroy(button);
            }

            if (currentPopup != null)
                Destroy(currentPopup.gameObject);
        }
        else
        {
            if (currentPopup != null)
                currentPopup.gameObject.SetActive(false);
        }
    }
}