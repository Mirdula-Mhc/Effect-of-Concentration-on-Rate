using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

public class SliderMechanismController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private List<SliderTarget> sliders = new();

    [Header("Submit")]
    [SerializeField] private Button submitButton;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector timeline;

    private bool completed;

    private void Start()
    {
        if (submitButton != null)
        {
            submitButton.gameObject.SetActive(false);
            submitButton.onClick.AddListener(OnSubmitPressed);
        }

        foreach (var slider in sliders)
        {
            if (slider != null)
            {
                slider.Slider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        if (timeline != null)
        {
            timeline.stopped += OnTimelineFinished;
        }

        OnSliderChanged(0);
    }

    private void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmitPressed);

        foreach (var slider in sliders)
        {
            if (slider != null)
            {
                slider.Slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        if (timeline != null)
        {
            timeline.stopped -= OnTimelineFinished;
        }
    }

    private void OnSliderChanged(float value)
    {
        if (completed)
            return;

        bool allCorrect = true;

        foreach (var slider in sliders)
        {
            if (!slider.IsCorrect)
            {
                allCorrect = false;
                break;
            }
        }

        if (submitButton != null)
            submitButton.gameObject.SetActive(allCorrect);
    }

    private void OnSubmitPressed()
    {
        if (completed)
            return;

        completed = true;

        if (submitButton != null)
        {
            submitButton.interactable = false;
        }

        foreach (var slider in sliders)
        {
            slider.SetInteractable(false);
        }

        if (timeline != null)
        {
            timeline.Play();
        }
        else
        {
            FinishInteraction();
        }
    }

    private void OnTimelineFinished(PlayableDirector director)
    {
        FinishInteraction();
    }

    private void FinishInteraction()
    {
        PageNavigationController.RequestNavigationUnlock();
    }
}