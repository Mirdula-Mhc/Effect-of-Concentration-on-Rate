using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;

    [Header("Target")]
    [SerializeField] private float targetValue = 50;

    [Tooltip("Allowed difference from target.")]
    [SerializeField] private float tolerance = 0.1f;

    [Tooltip("Display values as whole numbers.")]
    [SerializeField] private bool wholeNumbers = true;

    public bool IsCorrect =>
        Mathf.Abs(slider.value - targetValue) <= tolerance;

    public Slider Slider => slider;

    private void Awake()
    {
        slider.onValueChanged.AddListener(UpdateValue);
        UpdateValue(slider.value);
    }

    private void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(UpdateValue);
    }

    private void UpdateValue(float value)
    {
        if (valueText == null)
            return;

        valueText.text = wholeNumbers
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.0");
    }

    public void SetInteractable(bool value)
    {
        slider.interactable = value;
    }
}