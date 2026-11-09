using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BeatGuideUI : MonoBehaviour
{
    public BeatController beat;
    public Image guideFill; // Compatibilidad con guías de imagen en otros niveles.
    public PulsePathIndicatorGraphic proceduralGuide;
    public TempoTapGameManager game;
    [Range(0f, 1f)] public float idleFillAmount = 0f;

    float lastProgress;
    bool wasRunning;
    TMP_Text actionLabel;
    bool portraitLayout;
    bool tutorialCue;

    void Start()
    {
        if (game == null)
            game = FindFirstObjectByType<TempoTapGameManager>();

        RectTransform guideRect = transform as RectTransform;
        if (guideRect != null)
            guideRect.anchoredPosition = new Vector2(0f, 40f);

        Transform background = transform.Find("ActionBackground");
        if (background == null)
        {
            GameObject surface = new GameObject("ActionBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = surface.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -148f);
            rect.sizeDelta = new Vector2(242f, 70f);
            surface.GetComponent<Image>().color = new Color(0.04f, 0.15f, 0.34f, 0.94f);
            surface.GetComponent<Image>().raycastTarget = false;
            rect.SetAsFirstSibling();
        }
        Transform existing = transform.Find("ActionLabel");
        if (existing == null)
        {
            GameObject label = new GameObject("ActionLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            existing = rect;
        }
        actionLabel = existing.GetComponent<TMP_Text>();
        actionLabel.rectTransform.anchoredPosition = new Vector2(0f, -148f);
        actionLabel.rectTransform.sizeDelta = new Vector2(228f, 64f);
        actionLabel.fontSize = 29f;
        actionLabel.fontStyle = FontStyles.Bold;
        actionLabel.alignment = TextAlignmentOptions.Center;
        actionLabel.color = new Color(1f, 0.94f, 0.82f);
        actionLabel.raycastTarget = false;
        actionLabel.text = "ESCUCHA";
        ApplyLayout();
        SetFill(idleFillAmount);
        lastProgress = idleFillAmount;
    }

    void Update()
    {
        if (tutorialCue)
            return;
        bool isPortrait = Screen.height > Screen.width;
        if (portraitLayout != isPortrait ||
            (game != null && game.feedbackText != null &&
             !Mathf.Approximately(game.feedbackText.rectTransform.anchoredPosition.y, isPortrait ? 265f : 240f)))
            ApplyLayout();

        if (beat == null || (guideFill == null && proceduralGuide == null))
            return;

        if (!beat.IsRunning || beat.IntervalSec <= 0.0)
        {
            if (actionLabel != null) actionLabel.text = "ESCUCHA";
            if (proceduralGuide != null) proceduralGuide.SetPointerVisible(false);
            SetFill(idleFillAmount);
            lastProgress = idleFillAmount;
            wasRunning = false;
            return;
        }

        if (!wasRunning)
        {
            lastProgress = 0f;
            wasRunning = true;
        }

        double now = AudioSettings.dspTime;
        double next = beat.NextBeatDspTime;
        if (actionLabel != null && game != null)
        {
            string prompt = game.GetPromptForBeat(next);
            if (actionLabel.text != prompt) actionLabel.text = prompt;
        }
        double previous = next - beat.IntervalSec;

        if (now <= previous)
        {
            if (proceduralGuide != null) proceduralGuide.SetPointerVisible(false);
            SetFill(0f);
            return;
        }

        if (proceduralGuide != null) proceduralGuide.SetPointerVisible(true);
        double progress = (now - previous) / beat.IntervalSec;
        float normalizedProgress = Mathf.Clamp01((float)progress);

        // Cuando el ciclo reinicia tras completar un beat, hacemos un flash breve.
        if (lastProgress > 0.95f && normalizedProgress < 0.2f && proceduralGuide != null)
            proceduralGuide.TriggerFlash();

        SetFill(normalizedProgress);
        lastProgress = normalizedProgress;
    }

    public void ShowTutorialCue(float progress, string prompt)
    {
        tutorialCue = true;
        if (actionLabel != null) actionLabel.text = prompt;
        if (proceduralGuide != null) proceduralGuide.SetPointerVisible(true);
        SetFill(progress);
    }

    public void EndTutorialCue()
    {
        tutorialCue = false;
        if (proceduralGuide != null) proceduralGuide.SetPointerVisible(false);
        SetFill(idleFillAmount);
    }

    void ApplyLayout()
    {
        portraitLayout = Screen.height > Screen.width;
        RectTransform path = transform.Find("PulsePath") as RectTransform;
        if (path != null)
            path.sizeDelta = portraitLayout ? new Vector2(214f, 214f) : new Vector2(204f, 204f);

        Vector2 labelPosition = new Vector2(0f, portraitLayout ? -168f : -155f);
        RectTransform background = transform.Find("ActionBackground") as RectTransform;
        if (background != null)
            background.anchoredPosition = labelPosition;
        if (actionLabel != null)
            actionLabel.rectTransform.anchoredPosition = labelPosition;

        // Keep the explanation centred above the pulse indicator on every aspect ratio.
        if (game != null && game.feedbackText != null)
        {
            RectTransform feedback = game.feedbackText.rectTransform;
            feedback.anchorMin = feedback.anchorMax = new Vector2(0.5f, 0.5f);
            feedback.pivot = new Vector2(0.5f, 0.5f);
            feedback.anchoredPosition = new Vector2(0f, portraitLayout ? 265f : 240f);
            feedback.sizeDelta = new Vector2(portraitLayout ? 450f : 520f, 90f);
        }
    }

    void SetFill(float amount)
    {
        if (guideFill != null)
            guideFill.fillAmount = amount;

        if (proceduralGuide != null)
            proceduralGuide.SetProgress(amount);
    }
}
