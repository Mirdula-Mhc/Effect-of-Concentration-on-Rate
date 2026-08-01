using System.Collections.Generic;
using UnityEngine;

public class MeshRendererHighlighter : MonoBehaviour
{
    [System.Serializable]
    public class HighlightElement
    {
        public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
    }

    [Header("Elements")]
    public List<HighlightElement> elements = new List<HighlightElement>();

    [Header("Materials")]
    public Material normalMaterial;
    public Material highlightMaterial;

    /// <summary>
    /// Highlights all MeshRenderers in the specified element.
    /// Every occurrence of the normal material is replaced with the highlight material.
    /// </summary>
    public void Highlight(int index)
    {
        if (index < 0 || index >= elements.Count)
        {
            Debug.LogWarning($"Highlight index {index} is out of range.");
            return;
        }

        foreach (MeshRenderer renderer in elements[index].meshRenderers)
        {
            if (renderer == null)
                continue;

            Material[] mats = renderer.materials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == normalMaterial)
                {
                    mats[i] = highlightMaterial;
                    changed = true;
                }
            }

            if (changed)
                renderer.materials = mats;
        }
    }

    /// <summary>
    /// Resets every MeshRenderer in every element.
    /// Every occurrence of the highlight material is replaced with the normal material.
    /// </summary>
    public void Reset()
    {
        foreach (HighlightElement element in elements)
        {
            foreach (MeshRenderer renderer in element.meshRenderers)
            {
                if (renderer == null)
                    continue;

                Material[] mats = renderer.materials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == highlightMaterial)
                    {
                        mats[i] = normalMaterial;
                        changed = true;
                    }
                }

                if (changed)
                    renderer.materials = mats;
            }
        }
    }
}