using UnityEngine;

public class ApparatusButton : MonoBehaviour
{
    [TextArea(5, 10)]
    public string description;

    public void OnClick()
    {
        UIManager.Instance.ShowDescription(description);
    }
}