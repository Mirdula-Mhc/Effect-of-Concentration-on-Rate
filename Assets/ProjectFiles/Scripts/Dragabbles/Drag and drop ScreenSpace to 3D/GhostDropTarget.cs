using UnityEngine;
using System;
using System.Collections.Generic;

namespace DeterminingMassofaBodyUsingMeterscale
{
    [RequireComponent(typeof(Collider))]
    public class GhostDropTarget : MonoBehaviour
    {
        [Header("Identification")]
        [SerializeField] private string correctItemID;

        [Header("Renderers To Control")]
        [SerializeField] private List<Renderer> targetRenderers = new();

        [Header("Highlight Material")]
        [SerializeField] private Material highlightMaterial;

        public event Action OnCorrectDropped;

        private readonly Dictionary<Renderer, Material[]> originalMaterials =
            new();

        private bool completed;

        private void Awake()
        {
            CacheOriginalMaterials();
        }

        private void Start()
        {
            completed = false;
            ApplyHighlightMaterial();
        }

        private void CacheOriginalMaterials()
        {
            originalMaterials.Clear();

            foreach (var rend in targetRenderers)
            {
                if (rend == null) continue;
                originalMaterials[rend] = rend.sharedMaterials;
            }
        }

        private void ApplyHighlightMaterial()
        {
            if (highlightMaterial == null) return;

            foreach (var rend in targetRenderers)
            {
                if (rend == null) continue;

                var materials = rend.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                    materials[i] = highlightMaterial;

                rend.sharedMaterials = materials;
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var pair in originalMaterials)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMaterials = pair.Value;
            }
        }

        public bool TryDrop(UIDragItem item)
        {
            if (completed || item == null)
                return false;

            if (item.ItemID != correctItemID)
                return false;

            completed = true;

            RestoreOriginalMaterials();
            item.gameObject.SetActive(false);

            PageNavigationController.RequestNavigationUnlock();
            OnCorrectDropped?.Invoke();
            return true;
        }


    }
}
