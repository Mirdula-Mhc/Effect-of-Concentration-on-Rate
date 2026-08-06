using UnityEngine;
using TMPro;

// -----------------------------------------------------------------
// Displays a running count in SIMULATED seconds, which can run
// faster than real time via timeScale. E.g. timeScale = 4 means 4
// simulated seconds pass per 1 real second - so a 68-"second" timer
// finishes in 17 real seconds.
//
// FlaskColorChanger reads GetElapsedSimSeconds() from this each
// frame instead of running its own independent timer, so the
// material swap happens at EXACTLY the simulated second you set,
// in lockstep with what's displayed - no drift between the two.
// -----------------------------------------------------------------
public class TimerClock : MonoBehaviour
{
    [Header("Timer UI")]
    public TMP_Text timerText;

    [Header("Timer Settings")]
    [Tooltip("Simulated seconds shown on screen when the timer completes on its own (only relevant if nothing else calls StopClock() first).")]
    public float targetTime = 120f;

    [Tooltip("How many simulated seconds pass per 1 real second. 1 = real time. 4 = four times faster (68 sim-seconds in 17 real seconds).")]
    public float timeScale = 4f;

    private float elapsedSimSeconds = 0f;
    private bool isRunning = false;

    void Start()
    {
        timerText.text = "";
    }

    void Update()
    {
        if (!isRunning)
            return;

        elapsedSimSeconds += Time.deltaTime * timeScale;

        int displaySeconds = Mathf.FloorToInt(elapsedSimSeconds);
        timerText.text = displaySeconds.ToString();

        if (elapsedSimSeconds >= targetTime)
        {
            elapsedSimSeconds = targetTime;
            timerText.text = Mathf.FloorToInt(targetTime).ToString();
            isRunning = false;
            TimerFinished();
        }
    }

    // Call this from your Button/Event, or from a Timeline Signal
    // Receiver. Activates the GameObject first if it starts
    // SetActive(false), then begins timing.
    public void StartClock()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        elapsedSimSeconds = 0f;
        isRunning = true;
        timerText.text = "0";
    }

    public void StopClock()
    {
        isRunning = false;
    }

    public void ResetClock()
    {
        elapsedSimSeconds = 0f;
        isRunning = false;
        timerText.text = "";
    }

    // Read by FlaskColorChanger to stay in exact lockstep with what's
    // displayed - so "color changes at 68" means exactly when this
    // crosses 68, not a separately-run real-time wait.
    public float GetElapsedSimSeconds() => elapsedSimSeconds;
    public bool IsRunning => isRunning;

    private void TimerFinished()
    {
        Debug.Log($"[TimerClock] {name}: reached targetTime ({targetTime}) without being stopped externally.");
    }
}