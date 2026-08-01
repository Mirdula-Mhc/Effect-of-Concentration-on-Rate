using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Dropdown/Dropdown Data")]
public class DropdownData : ScriptableObject
{
    public List<string> options = new List<string>();
    public int correctIndex;
}