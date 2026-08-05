using UnityEngine;
using TMPro;

/// <summary>
/// Drop this on a coefficient's TMP_Text GameObject. Tracks whether that
/// number currently equals "correctValue". Once the value reaches
/// correctValue, it locks permanently - further Increment()/Decrement()
/// calls are ignored, so the player can't move it away from the correct
/// answer afterwards. Does NOT call PageNavigationController.RequestNavigationUnlock()
/// itself - that's decided by EquationCompletionChecker, which combines
/// this with the drag-drop state before unlocking Next.
///
/// SETUP:
/// 1. Put this on every coefficient TMP_Text that must equal 2.
/// 2. Wire its Up/Down Buttons -> OnClick() -> this GameObject ->
///    Increment() / Decrement().
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class CoefficientUnlockChecker : MonoBehaviour
{
    [Header("Values")]
    public int correctValue = 2;
    public int startValue = 1;
    public int minValue = 0;
    public int maxValue = 9;

    private int _currentValue;
    private TMP_Text _numberText;

    // Once true, Increment()/Decrement() no longer change the value.
    private bool _isLocked;

    /// <summary>True whenever this slot's current value equals correctValue.</summary>
    public bool IsCorrect => _currentValue == correctValue;

    private void Awake()
    {
        _numberText = GetComponent<TMP_Text>();
        _currentValue = startValue;
        UpdateText();
    }

    /// <summary>Wire this to the matching Up Arrow Button's OnClick().</summary>
    public void Increment()
    {
        if (_isLocked) return;

        _currentValue = Mathf.Clamp(_currentValue + 1, minValue, maxValue);
        UpdateText();
        CheckLock();
    }

    /// <summary>Wire this to the matching Down Arrow Button's OnClick().</summary>
    public void Decrement()
    {
        if (_isLocked) return;

        _currentValue = Mathf.Clamp(_currentValue - 1, minValue, maxValue);
        UpdateText();
        CheckLock();
    }

    private void UpdateText()
    {
        _numberText.text = _currentValue.ToString();
    }

    /// <summary>Locks the value permanently the moment it reaches correctValue.</summary>
    private void CheckLock()
    {
        if (_currentValue == correctValue)
        {
            _isLocked = true;
        }
    }
}