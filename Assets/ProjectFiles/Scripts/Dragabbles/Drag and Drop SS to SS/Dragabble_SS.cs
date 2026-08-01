using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class Dragabble_SS : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Must match DropSlot correctItemID")]
    public string itemID;

    [Header("Smooth Return Settings")]
    public float returnDuration = 0.25f;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 startPosition;
    private Transform startParent;

    private bool droppedCorrectly = false;
    private Coroutine returnCoroutine;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 🚫 BLOCK drag if already placed correctly
        if (droppedCorrectly)
            return;

        startPosition = rectTransform.anchoredPosition;
        startParent = transform.parent;

        canvasGroup.blocksRaycasts = false;

        transform.SetParent(canvas.transform);

        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (droppedCorrectly)
            return;

        rectTransform.anchoredPosition +=
            eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (droppedCorrectly)
            return;

        canvasGroup.blocksRaycasts = true;

        returnCoroutine = StartCoroutine(SmoothReturn());
    }

    // ✅ CALLED ONLY BY DropSlot_S (correct match)
    public void SnapToSlot(RectTransform slot)
    {
        droppedCorrectly = true;

        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        transform.SetParent(slot);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        // 🔒 Lock interaction permanently
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    IEnumerator SmoothReturn()
    {
        transform.SetParent(startParent);

        Vector2 currentPos = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            rectTransform.anchoredPosition =
                Vector2.Lerp(currentPos, startPosition, t);

            yield return null;
        }

        rectTransform.anchoredPosition = startPosition;
        rectTransform.localScale = Vector3.one;
    }
}
