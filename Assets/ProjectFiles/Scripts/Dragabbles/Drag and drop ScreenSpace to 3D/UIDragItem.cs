using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections;


namespace DeterminingMassofaBodyUsingMeterscale
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class UIDragItem : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
    {
        [Header("Identification")]
        [SerializeField] private string itemID;

        [Header("Drag Settings")]
        [SerializeField] private float dragScale = 1.2f;
        [SerializeField] private float scaleLerpSpeed = 12f;
        [SerializeField] private float returnSpeed = 12f;

        [Header("Events")]
        public UnityEvent OnEnabled;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Canvas canvas;

        private Vector2 originalPosition;
        private Vector3 originalScale;

        private Vector2 pointerOffset;   // 🔥 critical fix

        private Coroutine moveRoutine;
        private Coroutine scaleRoutine;

        public string ItemID => itemID;

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                Debug.LogError("UIDragItem: No parent Canvas found.");
                enabled = false;
                return;
            }

            originalScale = rectTransform.localScale;
        }

        private void OnEnable()
        {
            OnEnabled?.Invoke();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        #endregion

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            originalPosition = rectTransform.anchoredPosition;

            canvasGroup.blocksRaycasts = false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPoint);

            pointerOffset = rectTransform.anchoredPosition - localPoint;

            StartScale(originalScale * dragScale);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint + pointerOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            Ray ray = cam != null
                ? cam.ScreenPointToRay(eventData.position)
                : Camera.main != null
                    ? Camera.main.ScreenPointToRay(eventData.position)
                    : default;

            if (ray.direction != Vector3.zero && Physics.Raycast(ray, out RaycastHit hit))
            {
                GhostDropTarget target = hit.collider.GetComponent<GhostDropTarget>();

                if (target != null && target.TryDrop(this))
                {
                    StartScale(originalScale);
                    return;
                }
            }

            StartReturn();
            StartScale(originalScale);
        }

        #endregion

        #region Movement

        private void StartReturn()
        {
            if (moveRoutine != null)
                StopCoroutine(moveRoutine);

            moveRoutine = StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            while (Vector2.Distance(rectTransform.anchoredPosition, originalPosition) > 0.01f)
            {
                rectTransform.anchoredPosition =
                    Vector2.Lerp(rectTransform.anchoredPosition,
                                 originalPosition,
                                 Time.deltaTime * returnSpeed);

                yield return null;
            }

            rectTransform.anchoredPosition = originalPosition;
            moveRoutine = null;
        }

        #endregion

        #region Scaling

        private void StartScale(Vector3 target)
        {
            if (scaleRoutine != null)
                StopCoroutine(scaleRoutine);

            scaleRoutine = StartCoroutine(ScaleRoutine(target));
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            while (Vector3.Distance(rectTransform.localScale, target) > 0.001f)
            {
                rectTransform.localScale =
                    Vector3.Lerp(rectTransform.localScale,
                                 target,
                                 Time.deltaTime * scaleLerpSpeed);

                yield return null;
            }

            rectTransform.localScale = target;
            scaleRoutine = null;
        }

        #endregion
    }
}