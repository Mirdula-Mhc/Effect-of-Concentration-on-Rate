using UnityEngine;
using UnityEngine.Events;

public class EventTrigger : MonoBehaviour
{
    [Header("Event To Trigger")]
    [SerializeField] private UnityEvent onTriggered;

    [Header("Navigation")]
    [Tooltip("If enabled, navigation will be unlocked when this event is triggered.")]
    [SerializeField] private bool enableNavigation = true;

    /// <summary>
    /// Call this method to invoke the event
    /// </summary>
    public void TriggerEvent()
    {
        // Invoke assigned event
        onTriggered?.Invoke();

        // Unlock navigation if enabled
        if (enableNavigation)
        {
            PageNavigationController.RequestNavigationUnlock();
        }
    }
}