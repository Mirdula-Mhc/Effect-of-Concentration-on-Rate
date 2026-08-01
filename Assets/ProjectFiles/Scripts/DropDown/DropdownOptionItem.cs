using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropdownOptionItem : MonoBehaviour, IMenuOption
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;

    private int index;
    private DropdownPopup parent;

    public void Initialize(string text, int index, DropdownPopup parent)
    {
        this.index = index;
        this.parent = parent;

        label.text = text;
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        parent.OnOptionClicked(index);
    }
}