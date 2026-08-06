using UnityEngine;

// -----------------------------------------------------------------
// Put this on the page itself (or any manager object). Assign all 4
// FlaskColorChangers. Each one calls ReportFinished() on this when
// ITS OWN color change completes (regardless of how long each one
// took - they can all finish at different times). Once all 4 have
// reported, unlocks page navigation ONCE.
//
// Call BeginTracking() at the same moment the pour Timeline starts
// (e.g. from the Timeline's start, or the page-enter that triggers
// the pour) so the logged times below are measured from a known
// reference point - use them to place a camera-move trigger at
// exactly the right frame.
// -----------------------------------------------------------------
public class FlaskColorGate : MonoBehaviour
{
    [Tooltip("All 4 FlaskColorChangers this page needs to finish before Next is allowed.")]
    public FlaskColorChanger[] flasks;

    private int finishedCount = 0;
    private bool unlocked = false;
    private float trackingStartTime = -1f;

    // Call this once, at the same moment the pour Timeline begins -
    // e.g. wire it into the same Signal/event that starts the pour,
    // or call it manually right before pressing Play if you're just
    // timing the sequence. All the elapsed-time logs below are
    // measured from this call.
    public void BeginTracking()
    {
        trackingStartTime = Time.time;
        finishedCount = 0;
        unlocked = false;
        Debug.Log("[FlaskColorGate] Tracking started - timing all flasks from this point.");
    }

    // Called by each FlaskColorChanger when ITS color change finishes.
    public void ReportFinished()
    {
        finishedCount++;

        float elapsed = trackingStartTime >= 0f ? Time.time - trackingStartTime : -1f;
        Debug.Log($"[FlaskColorGate] {finishedCount}/{flasks.Length} flasks finished. " +
                  $"Elapsed since BeginTracking: {elapsed:F2}s (approx frame {Mathf.RoundToInt(elapsed * 60f)} at 60fps, " +
                  $"frame {Mathf.RoundToInt(elapsed * 30f)} at 30fps).");

        if (unlocked) return;
        if (finishedCount < flasks.Length) return;

        unlocked = true;
        Debug.Log($"[FlaskColorGate] ALL {flasks.Length} FLASKS DONE at {elapsed:F2}s since BeginTracking - unlocking navigation now. " +
                  $"Use this timestamp to place your camera-move trigger.");
        PageNavigationController.RequestNavigationUnlock();
    }
}