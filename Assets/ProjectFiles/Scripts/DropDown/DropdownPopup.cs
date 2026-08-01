using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class DropdownPopup : MonoBehaviour
{
    public enum Direction { Up, Down, Left, Right }

    [Header("Manual Options (Assign in Inspector)")]
    [SerializeField] private List<DropdownOptionItem> options = new List<DropdownOptionItem>();

    [Header("Container")]
    [SerializeField] private RectTransform container;

    [Header("Animation")]
    [SerializeField] private Direction direction = Direction.Down;
    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private float distance = 100f;

    private DropdownData data;
    private DropdownMainButton owner;

    private Vector2 initialPosition;
    private bool initialized = false;

    #region INIT

    public void Initialize(DropdownData data, DropdownMainButton owner)
    {
        this.data = data;
        this.owner = owner;

        if (container == null)
        {
            Debug.LogError("DropdownPopup: Container not assigned.");
            return;
        }

        initialPosition = container.anchoredPosition;

        BindOptions();

        initialized = true;
        PlayAnimation();
    }

    #endregion

    #region BIND OPTIONS + STRETCH

    private void BindOptions()
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (i >= data.options.Count)
            {
                options[i].gameObject.SetActive(false);
                continue;
            }

            options[i].gameObject.SetActive(true);
            options[i].Initialize(data.options[i], i, this);

            // ✅ FORCE STRETCH
            RectTransform rt = options[i].GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);

            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }
    }

    #endregion

    #region CLICK

    public void OnOptionClicked(int index)
    {
        if (!initialized) return;

        bool isCorrect = index == data.correctIndex;
        string text = data.options[index];

        owner.OnOptionSelected(index, isCorrect, text);
    }

    #endregion

    #region ANIMATION CONTROL

    public void ApplyAnimationSettings(Direction dir, float duration, float dist)
    {
        direction = dir;
        animDuration = Mathf.Max(0.01f, duration);
        distance = dist;
    }

    public void PlayAnimation()
    {
        if (!initialized) return;

        StopAllCoroutines();
        StartCoroutine(AnimateOutward());
    }

    #endregion

    #region ANIMATION

    private IEnumerator AnimateOutward()
    {
        Vector2 dir = GetDirectionVector();

        Vector2 start = initialPosition;
        Vector2 end = start + dir * distance;

        container.anchoredPosition = start;
        container.localScale = Vector3.zero;

        float t = 0f;

        while (t < animDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.SmoothStep(0f, 1f, t / animDuration);

            container.anchoredPosition = Vector2.Lerp(start, end, lerp);
            container.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, lerp);

            yield return null;
        }

        container.anchoredPosition = end;
        container.localScale = Vector3.one;
    }

    private Vector2 GetDirectionVector()
    {
        switch (direction)
        {
            case Direction.Up: return Vector2.up;
            case Direction.Down: return Vector2.down;
            case Direction.Left: return Vector2.left;
            case Direction.Right: return Vector2.right;
        }
        return Vector2.down;
    }

    #endregion
}