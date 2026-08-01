using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;




    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class ButtonPrefabSpawner : MonoBehaviour
    {
        [Header("Buttons (Order Matters)")]
        [SerializeField] private List<Button> buttons = new List<Button>();
    
        [Header("Prefab Mappings")]
        [SerializeField] private List<PrefabMapping> prefabMappings = new List<PrefabMapping>();
    
        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;
        [SerializeField] private Transform spawnParent;
    
        [Header("Rotation Offset")]
        [SerializeField] private bool useRotationOffset = false;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
    
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
    
        [Header("Debug")]
        [SerializeField] private bool enableDebug = false;
    
        private bool[] hasSpawned;
    
        // =====================================================
        private void Awake()
        {
            hasSpawned = new bool[buttons.Count];
    
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
    
            RegisterButtons();
        }
    
        // =====================================================
        private void RegisterButtons()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i;
    
                if (buttons[i] == null)
                    continue;
    
                buttons[i].onClick.AddListener(() => OnButtonPressed(index));
            }
        }
    
        // =====================================================
        private void OnButtonPressed(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= hasSpawned.Length)
                return;
    
            if (hasSpawned[buttonIndex])
            {
                Log($"Button {buttonIndex} already used");
                return;
            }
    
            PrefabMapping mapping = GetMappingForButton(buttonIndex);
            if (mapping == null || mapping.Prefab == null)
            {
                Log($"No prefab mapped for button index {buttonIndex}");
                return;
            }
    
            Transform pivot = buttons[buttonIndex].transform;
    
            Vector3 spawnPos = pivot.position + spawnOffset;
    
            Quaternion spawnRot = Quaternion.identity;
    
            if (useRotationOffset)
                spawnRot = Quaternion.Euler(rotationOffset);
    
            Instantiate(mapping.Prefab, spawnPos, spawnRot, spawnParent);
    
            PlayAudio(mapping.AudioClip);
    
            hasSpawned[buttonIndex] = true;
    
            mapping.RegisterButtonPress(buttonIndex);
    
            Log($"Spawned prefab & played audio for button index {buttonIndex}");
        }
    
        // =====================================================
        private PrefabMapping GetMappingForButton(int buttonIndex)
        {
            for (int i = 0; i < prefabMappings.Count; i++)
            {
                if (prefabMappings[i].ContainsIndex(buttonIndex))
                    return prefabMappings[i];
            }
    
            return null;
        }
    
        // =====================================================
        private void PlayAudio(AudioClip clip)
        {
            if (clip == null || audioSource == null)
                return;
    
            audioSource.PlayOneShot(clip);
        }
    
        // =====================================================
        private void Log(string msg)
        {
            if (enableDebug)
                Debug.Log($"[ButtonPrefabSpawner] {msg}", this);
        }
    }
    
    // =====================================================
    // PREFAB MAPPING (WITH EVENT)
    // =====================================================
    [System.Serializable]
    public class PrefabMapping
    {
        [Header("Prefab")]
        [SerializeField] private GameObject prefab;
    
        [Tooltip("Button indices that should spawn this prefab")]
        [SerializeField] private List<int> buttonIndices = new List<int>();
    
        [Header("Audio")]
        [SerializeField] private AudioClip audioClip;
    
        [Header("Events")]
        public UnityEvent OnAllButtonsPressed;
    
        private HashSet<int> pressedButtons = new HashSet<int>();
        private bool eventFired;
    
        public GameObject Prefab => prefab;
        public AudioClip AudioClip => audioClip;
    
        public bool ContainsIndex(int index)
        {
            return buttonIndices.Contains(index);
        }
    
        public void RegisterButtonPress(int index)
        {
            if (eventFired)
                return;
    
            if (!buttonIndices.Contains(index))
                return;
    
            pressedButtons.Add(index);
    
            for (int i = 0; i < buttonIndices.Count; i++)
            {
                if (!pressedButtons.Contains(buttonIndices[i]))
                    return;
            }
    
            eventFired = true;
            OnAllButtonsPressed?.Invoke();
        }
    }
    
