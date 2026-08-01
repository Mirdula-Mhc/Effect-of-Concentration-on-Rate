using UnityEngine;
using UnityEngine.Events;

public class EnableEventBroadcaster : MonoBehaviour
{
    public UnityEvent OnEnabled;
    public UnityEvent OnDisabled;
    public UnityEvent OnStart; // renamed for clarity

    private void Start()
    {
        OnStart?.Invoke();    // triggers Start event
        OnEnabled?.Invoke();  // optional: also trigger OnEnabled at Start
    }

    private void OnEnable()
    {
        OnEnabled?.Invoke();
    }

    private void OnDisable()
    {
        OnDisabled?.Invoke();
    }
}
