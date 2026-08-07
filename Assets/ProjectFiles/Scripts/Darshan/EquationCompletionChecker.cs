using UnityEngine;

/// <summary>
/// Gates the Next button behind TWO conditions together:
/// 1. Every drag-drop item is correctly snapped (ScreenToScreenSnapManager.AllSnapped)
/// 2. The required coefficient(s) - e.g. first and last - equal 2
///    (tracked via CoefficientUnlockChecker instances, which no longer
///    unlock on their own - see below).
///
/// Next only unlocks once BOTH are true at the same time.
///
/// SETUP:
/// 1. Put this on any GameObject on the question page.
/// 2. Assign "snapManager" to this page's ScreenToScreenSnapManager.
/// 3. Assign "coefficientCheckers" to every CoefficientUnlockChecker on this
///    page that must show correctValue (e.g. first + last coefficient).
/// </summary>
public class EquationCompletionChecker : MonoBehaviour
{
    [Header("Drag-and-drop manager for this page")]
    public ScreenToScreenSnapManager snapManager;

    [Header("Coefficient checkers that must all be correct")]
    public CoefficientUnlockChecker[] coefficientCheckers;

    private bool _unlockFired;

    private void Update()
    {
        // Polling is simplest here since both systems already track their own
        // state internally; this just checks once per frame until it fires.
        if (_unlockFired) return;

        if (snapManager == null || !snapManager.AllSnapped)
            return;

        foreach (var checker in coefficientCheckers)
        {
            if (checker == null || !checker.IsCorrect)
                return; // at least one required coefficient still wrong
        }

        _unlockFired = true;
        PageNavigationController.RequestNavigationUnlock();
    }
}