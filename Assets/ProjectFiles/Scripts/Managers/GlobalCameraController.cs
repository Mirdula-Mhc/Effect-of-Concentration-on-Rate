using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class GlobalCameraController : MonoBehaviour
{
    [System.Serializable]
    public class SecondaryCameraPoint
    {
        public int pageIndex;
        public Transform target;
    }

    [Header("Primary Page Camera Points (Index = Page Index)")]
    [SerializeField] private List<Transform> pageCameraPoints = new();

    [Header("Secondary Camera Points (Used after first visit)")]
    [SerializeField] private List<SecondaryCameraPoint> secondaryCameraPoints = new();

    [Header("Movement")]
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Events")]
    public UnityEvent OnMoveStart;
    public UnityEvent OnMoveEnd;

    private Coroutine routine;
    private int currentPageIndex = 0;

    private Camera mainCamera;

    // Tracks how many times each page was visited
    private Dictionary<int, int> pageVisitCount = new();

    // ================= LIFECYCLE =================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += MoveToPage;
        mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= MoveToPage;
    }

    // ================= PAGE MOVEMENT =================

    private void MoveToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pageCameraPoints.Count)
            return;

        currentPageIndex = pageIndex;

        // Track visits
        if (!pageVisitCount.ContainsKey(pageIndex))
            pageVisitCount[pageIndex] = 0;

        pageVisitCount[pageIndex]++;

        Transform target = GetTargetForPage(pageIndex);

        if (target != null)
            StartMove(target);
    }

    private Transform GetTargetForPage(int pageIndex)
    {
        int visitCount = pageVisitCount[pageIndex];

        // First visit → use primary
        if (visitCount == 1)
            return pageCameraPoints[pageIndex];

        // Second+ visits → try secondary
        for (int i = 0; i < secondaryCameraPoints.Count; i++)
        {
            if (secondaryCameraPoints[i].pageIndex == pageIndex)
            {
                if (secondaryCameraPoints[i].target != null)
                    return secondaryCameraPoints[i].target;
            }
        }

        // Fallback to primary if no secondary found
        return pageCameraPoints[pageIndex];
    }

    public void ResetToPageDefault()
    {
        MoveToPage(currentPageIndex);
    }

    // ================= DIRECT MOVEMENT =================

    public void MoveTo(Transform target)
    {
        if (target == null) return;
        StartMove(target);
    }

    // ================= MOVEMENT CORE =================

    private void StartMove(Transform target)
    {
        if (mainCamera == null) return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(MoveRoutine(target));
    }

    private IEnumerator MoveRoutine(Transform target)
    {
        OnMoveStart?.Invoke();

        Transform camTransform = mainCamera.transform;

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;

        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;

        float t = 0f;

        while (t < moveDuration)
        {
            float progress = ease.Evaluate(t / moveDuration);

            camTransform.position = Vector3.Lerp(startPos, endPos, progress);
            camTransform.rotation = Quaternion.Slerp(startRot, endRot, progress);

            t += Time.deltaTime;
            yield return null;
        }

        camTransform.position = endPos;
        camTransform.rotation = endRot;

        OnMoveEnd?.Invoke();
    }
}