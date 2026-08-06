using UnityEngine;
using System.Collections;

// -----------------------------------------------------------------
// Assigns startMaterial to the target renderer immediately, then
// waits until the linked TimerClock's SIMULATED time reaches
// changeAtSimSeconds (not real seconds) before swapping to
// endMaterial. This stays in exact lockstep with what the timer
// displays - "changes at 68" means exactly when the on-screen number
// hits 68, regardless of timeScale.
//
// Requires BOTH BeginColorChange() AND timerClock.StartClock() to be
// called together (e.g. from the same Timeline Signal) - this script
// reads the timer's elapsed time each frame rather than running its
// own independent countdown, so the two can never drift apart.
// -----------------------------------------------------------------
public class FlaskColorChanger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The renderer whose material will change.")]
    public Renderer targetRenderer;

    [Header("Material Change")]
    [Tooltip("Assigned immediately when BeginColorChange() is called.")]
    public Material startMaterial;
    [Tooltip("Assigned when the linked TimerClock's displayed simulated seconds reaches this value.")]
    public Material endMaterial;
    [Tooltip("Simulated seconds (matches what's shown on the timer) at which the swap happens. E.g. 68 means 'when the clock shows 68'.")]
    public float changeAtSimSeconds = 68f;

    [Header("Linked Timer")]
    [Tooltip("Must be started (StartClock()) at the same time as BeginColorChange() is called - this script reads its elapsed simulated time each frame.")]
    public TimerClock timerClock;

    [Header("Page Completion")]
    [Tooltip("Reports here when this flask's material change finishes - the gate waits for ALL flasks before unlocking Next.")]
    public FlaskColorGate colorGate;

    private Coroutine activeRoutine;

    // Call this to begin - e.g. from a Timeline Signal Receiver right
    // after the pour animation completes. Call timerClock.StartClock()
    // at the same moment (same Signal Receiver can call both).
    public void BeginColorChange()
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning($"[FlaskColorChanger] {name}: no target renderer assigned.");
            return;
        }

        if (timerClock == null)
        {
            Debug.LogWarning($"[FlaskColorChanger] {name}: no TimerClock assigned - cannot sync to simulated time.");
            return;
        }

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(WaitForSimTimeRoutine());
    }

    private IEnumerator WaitForSimTimeRoutine()
    {
        if (startMaterial != null)
            targetRenderer.material = startMaterial;

        // Poll the timer's own simulated elapsed time every frame -
        // this is what keeps the swap exactly in sync with the
        // displayed number, instead of running an independent wait
        // that could drift from what's on screen.
        while (timerClock.GetElapsedSimSeconds() < changeAtSimSeconds)
            yield return null;

        if (endMaterial != null)
            targetRenderer.material = endMaterial;

        timerClock.StopClock();

        if (colorGate != null)
            colorGate.ReportFinished();

        activeRoutine = null;
    }
}