using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MCQController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mcqPanel;
    public GameObject explanationPanel;

    [Header("Question UI")]
    public TMP_Text questionText;
    public Image referenceImage;

    [Header("Options")]
    public Button[] optionButtons;
    public TMP_Text[] optionTexts;

    [Header("Explanation UI")]
    public TMP_Text explanationText;
    public Button explanationActionButton;

    [Header("Explanation Entry Buttons")]
    public Button rightExplanationButton;
    public Button wrongExplanationButton;

    [Header("Sprites")]
    public Sprite defaultButtonSprite;
    public Sprite correctSprite;
    public Sprite wrongSprite;

    [Header("Data")]
    public MCQQuestionData questionData;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctClip;
    public AudioClip wrongClip;

    // ================= STATE =================
    class MCQState
    {
        public bool answeredCorrectly;
        public HashSet<int> wrongAttempts = new HashSet<int>();
    }

    private MCQState state = new MCQState();

    // =================================================

    private void OnEnable()
    {
        LoadQuestion();
        BindButtons();
        RestoreState();
    }

    void LoadQuestion()
    {
        questionText.text = questionData.questionText;
        explanationText.text = questionData.explanationText;

        if (referenceImage != null)
        {
            if (questionData.referenceImage != null)
            {
                referenceImage.gameObject.SetActive(true);
                referenceImage.sprite = questionData.referenceImage;
                referenceImage.color = Color.white;
            }
            else
            {
                referenceImage.gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;

            optionTexts[i].text = questionData.options[i];
            optionButtons[i].interactable = true;
            optionButtons[i].GetComponent<Image>().sprite = defaultButtonSprite;

            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }

        mcqPanel.SetActive(true);
        explanationPanel.SetActive(false);
        HideExplanationButtons();
    }

    void RestoreState()
    {
        foreach (int wrong in state.wrongAttempts)
        {
            optionButtons[wrong].GetComponent<Image>().sprite = wrongSprite;
            optionButtons[wrong].interactable = false;
        }

        if (state.answeredCorrectly)
        {
            optionButtons[questionData.correctOptionIndex]
                .GetComponent<Image>().sprite = correctSprite;

            DisableAllOptions();
            ShowRightExplanation();
        }
    }

    void OnOptionSelected(int index)
    {
        bool isCorrect = index == questionData.correctOptionIndex;

        if (isCorrect)
        {
            if (state.answeredCorrectly)
                return; // 🔒 Prevent double trigger

            state.answeredCorrectly = true;

            PlaySound(correctClip);

            optionButtons[index].GetComponent<Image>().sprite = correctSprite;
            DisableAllOptions();
            ShowRightExplanation();

            // 🔥 KEY CHANGE → Unlock Navigation
            PageNavigationController.RequestNavigationUnlock();
        }
        else
        {
            if (!state.wrongAttempts.Contains(index))
                state.wrongAttempts.Add(index);

            PlaySound(wrongClip);

            optionButtons[index].GetComponent<Image>().sprite = wrongSprite;
            optionButtons[index].interactable = false;
            ShowWrongExplanation();
        }
    }

    void BindButtons()
    {
        rightExplanationButton.onClick.RemoveAllListeners();
        wrongExplanationButton.onClick.RemoveAllListeners();
        explanationActionButton.onClick.RemoveAllListeners();

        rightExplanationButton.onClick.AddListener(OpenExplanation);
        wrongExplanationButton.onClick.AddListener(OpenExplanation);
        explanationActionButton.onClick.AddListener(CloseExplanation);
    }

    void OpenExplanation()
    {
        mcqPanel.SetActive(false);
        explanationPanel.SetActive(true);
    }

    void CloseExplanation()
    {
        explanationPanel.SetActive(false);
        mcqPanel.SetActive(true);
    }

    void DisableAllOptions()
    {
        foreach (var btn in optionButtons)
            btn.interactable = false;
    }

    void HideExplanationButtons()
    {
        rightExplanationButton.gameObject.SetActive(false);
        wrongExplanationButton.gameObject.SetActive(false);
    }

    void ShowRightExplanation()
    {
        rightExplanationButton.gameObject.SetActive(true);
        wrongExplanationButton.gameObject.SetActive(false);
    }

    void ShowWrongExplanation()
    {
        wrongExplanationButton.gameObject.SetActive(true);
        rightExplanationButton.gameObject.SetActive(false);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}