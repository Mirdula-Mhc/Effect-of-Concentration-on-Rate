using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(Image))]
public class DropSlot_SS : MonoBehaviour, IDropHandler
{
    [Header("Correct Draggable ID")]
    [SerializeField] private string correctItemID;

    [Header("Slot Settings")]
    [SerializeField] private int totalSlotsInPage = 4;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectPlaced;

    private bool occupied = false;

    // Shared counter for the current page
    private static int filledSlotCount = 0;

    private void OnEnable()
    {
        filledSlotCount = 0;
        occupied = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (occupied)
            return;

        if (eventData.pointerDrag == null)
            return;

        Dragabble_SS draggedItem = eventData.pointerDrag.GetComponent<Dragabble_SS>();

        if (draggedItem == null)
            return;

        // Correct item
        if (draggedItem.itemID == correctItemID)
        {
            draggedItem.SnapToSlot(transform as RectTransform);

            occupied = true;
            filledSlotCount++;

            // Optional sound/event
            onCorrectPlaced?.Invoke();

            // Unlock Next only after all slots are filled
            if (filledSlotCount >= totalSlotsInPage)
            {
                PageNavigationController.RequestNavigationUnlock();
            }
        }
        // Wrong item is handled by Dragabble_SS
    }
}