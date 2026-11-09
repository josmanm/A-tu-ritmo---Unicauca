using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CompassGuideView : MonoBehaviour
{
    [SerializeField] private Image[] beatSlots;
    [SerializeField] private TMP_Text[] beatTexts;
    [SerializeField] private TMP_Text measureText;
    [SerializeField] [Range(0f, 1f)] private float minimumSlotAlpha = 0.68f;
    [SerializeField] private float activeBeatScale = 1.14f;
    [SerializeField] private float pulseSpeed = 10f;
    [SerializeField] private float ringSize = 74f;
    [SerializeField] private float beatSpacing = 132f;
    [SerializeField] private float glowFadeSpeed = 8f;
    [SerializeField] private Color pathBaseColor = new Color(0.71f, 0.79f, 0.79f, 1f);
    [SerializeField] private Color pathProgressColor = new Color(0.09f, 0.48f, 0.52f, 1f);
    [SerializeField] private float pathThickness = 14f;

    private Sprite[] defaultSprites;
    private CompassBeatRingGraphic[] ringSlots;
    private Vector3[] baseScales;
    private float[] pulseTargets;
    private float[] glowTargets;
    private Image[] pathFills;
    private RectTransform pathMarker;
    private int displayedMeasure = -1;
    private static Sprite pathSprite;

    private void Awake()
    {
        CacheDefaults();
        EnsureRingSlots();
        ArrangeBeatSlots();
        EnsureBeatPath();
        DisableDecorativeRaycasts();
    }

    public void InitializeIfNeeded(int beatsPerMeasure)
    {
        if ((beatSlots == null || beatSlots.Length == 0) && transform.childCount > 0)
        {
            beatSlots = new Image[beatsPerMeasure];
            beatTexts = new TMP_Text[beatsPerMeasure];
            for (int i = 0; i < beatsPerMeasure; i++)
            {
                Transform beatTransform = transform.Find("Beat_" + (i + 1));
                if (beatTransform == null)
                    beatTransform = transform.Find("Time_" + (i + 1));

                if (beatTransform != null)
                {
                    beatSlots[i] = beatTransform.GetComponent<Image>();
                    beatTexts[i] = beatTransform.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (measureText == null)
                measureText = transform.Find("MeasureText")?.GetComponent<TMP_Text>();
        }

        CacheDefaults();
        EnsureRingSlots();
        ArrangeBeatSlots();
        EnsureBeatPath();
        DisableDecorativeRaycasts();
    }

    private void Update()
    {
        if (beatSlots == null || baseScales == null || pulseTargets == null || glowTargets == null)
            return;

        for (int i = 0; i < beatSlots.Length; i++)
        {
            if (beatSlots[i] == null)
                continue;

            float current = beatSlots[i].rectTransform.localScale.x;
            float next = Mathf.Lerp(current, pulseTargets[i], Time.unscaledDeltaTime * pulseSpeed);
            beatSlots[i].rectTransform.localScale = baseScales[i] * next;
            pulseTargets[i] = Mathf.Lerp(pulseTargets[i], 1f, Time.unscaledDeltaTime * pulseSpeed);

            if (ringSlots != null && i < ringSlots.Length && ringSlots[i] != null)
            {
                glowTargets[i] = Mathf.Lerp(glowTargets[i], 0f, Time.unscaledDeltaTime * glowFadeSpeed);
                ringSlots[i].SetGlow(glowTargets[i]);
            }
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void RefreshMeasure(CompassBeatDisplay[] steps, int measureNumber, int totalMeasures, Color idleColor, Color silenceColor, Sprite silenceSprite)
    {
        if (beatSlots == null || beatTexts == null)
            return;

        if (measureText != null)
            measureText.text = $"Compas {measureNumber}/{totalMeasures}";

        if (displayedMeasure != measureNumber)
        {
            displayedMeasure = measureNumber;
            ResetPath();
        }

        for (int i = 0; i < beatSlots.Length && i < beatTexts.Length; i++)
        {
            CompassBeatDisplay step = i < steps.Length ? steps[i] : null;
            if (beatSlots[i] != null)
            {
                beatSlots[i].sprite = step != null && step.isSilence && silenceSprite != null ? silenceSprite : defaultSprites[i];
                beatSlots[i].preserveAspect = step != null && step.isSilence && silenceSprite != null;
                beatSlots[i].enabled = step != null && step.isSilence && silenceSprite != null;
                beatSlots[i].color = new Color(1f, 1f, 1f, step != null && step.isSilence && silenceSprite != null ? 1f : 0f);
            }

            if (ringSlots != null && i < ringSlots.Length && ringSlots[i] != null)
            {
                Color ringColor = step != null && step.isSilence ? EnsureReadableAlpha(silenceColor) : WithReadableAlpha(step != null ? step.color : idleColor, idleColor.a);
                ringSlots[i].SetColor(ringColor);
                pulseTargets[i] = 1f;
                glowTargets[i] = 0f;
                ringSlots[i].gameObject.SetActive(!(step != null && step.isSilence && silenceSprite != null));
            }

            if (beatTexts[i] != null)
            {
                beatTexts[i].text = step == null ? "-" : step.isSilence ? string.Empty : step.label;
                beatTexts[i].color = new Color(0.094f, 0.188f, 0.31f, 1f);
                beatTexts[i].fontStyle = FontStyles.Bold;
                beatTexts[i].fontSize = 34f;
            }
        }
    }

    public void UpdateBeatState(int beatIndex, Color color, CompassBeatDisplay step)
    {
        if (beatSlots == null || beatIndex < 0 || beatIndex >= beatSlots.Length)
            return;

        if (ringSlots != null && beatIndex < ringSlots.Length && ringSlots[beatIndex] != null)
        {
            ringSlots[beatIndex].SetColor(step != null && !step.isSilence ? WithReadableAlpha(step.color, color.a) : EnsureReadableAlpha(color));
            pulseTargets[beatIndex] = activeBeatScale;
            glowTargets[beatIndex] = 1f;
        }

        if (beatSlots[beatIndex] != null && step != null && step.isSilence)
            beatSlots[beatIndex].color = EnsureReadableAlpha(color);
    }

    public void ShowTimingFeedback(int beatIndex, Color feedbackColor)
    {
        if (ringSlots == null || ringSlots.Length == 0)
            return;

        int slot = beatIndex % ringSlots.Length;
        if (ringSlots[slot] == null)
            return;
        ringSlots[slot].SetColor(feedbackColor);
        pulseTargets[slot] = activeBeatScale;
        glowTargets[slot] = 1f;
    }

    public void ResetPath()
    {
        if (pathFills == null)
            return;

        foreach (Image pathFill in pathFills)
            if (pathFill != null) pathFill.fillAmount = 0f;

        if (pathMarker != null && beatSlots != null && beatSlots.Length > 0 && beatSlots[0] != null)
            pathMarker.anchoredPosition = beatSlots[0].rectTransform.anchoredPosition + Vector2.right * (ringSize * 0.5f);
    }

    public void SetPathProgress(int beatIndex, float normalizedProgress)
    {
        if (pathFills == null || pathFills.Length == 0 || beatSlots == null)
            return;

        int slot = Mathf.Clamp(beatIndex % beatSlots.Length, 0, beatSlots.Length - 1);
        float progress = Mathf.Clamp01(normalizedProgress);
        if (slot == beatSlots.Length - 1)
        {
            for (int i = 0; i < pathFills.Length; i++)
                pathFills[i].fillAmount = 1f;
            if (pathMarker != null)
                pathMarker.anchoredPosition = beatSlots[slot].rectTransform.anchoredPosition - Vector2.right * (ringSize * 0.5f);
            return;
        }
        for (int i = 0; i < pathFills.Length; i++)
            pathFills[i].fillAmount = i < slot ? 1f : i == slot ? progress : 0f;

        if (pathMarker != null && beatSlots[slot] != null)
        {
            Vector2 from = beatSlots[slot].rectTransform.anchoredPosition + Vector2.right * (ringSize * 0.5f);
            Vector2 to = slot + 1 < beatSlots.Length && beatSlots[slot + 1] != null
                ? beatSlots[slot + 1].rectTransform.anchoredPosition - Vector2.right * (ringSize * 0.5f) : from;
            pathMarker.anchoredPosition = Vector2.Lerp(from, to, progress);
        }
    }

    private void EnsureBeatPath()
    {
        if (beatSlots == null || beatSlots.Length < 2 || pathFills != null)
            return;

        if (pathSprite == null)
        {
            Texture2D white = Texture2D.whiteTexture;
            pathSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        pathFills = new Image[beatSlots.Length - 1];
        // Scene values may come from an older color palette: apply the current contrast consistently.
        pathBaseColor = new Color(0.71f, 0.79f, 0.79f, 1f);
        pathProgressColor = new Color(0.09f, 0.48f, 0.52f, 1f);
        pathThickness = 14f;
        for (int i = 0; i < pathFills.Length; i++)
        {
            if (beatSlots[i] == null || beatSlots[i + 1] == null)
                continue;

            float startX = beatSlots[i].rectTransform.anchoredPosition.x + ringSize * 0.5f;
            float endX = beatSlots[i + 1].rectTransform.anchoredPosition.x - ringSize * 0.5f;
            float width = Mathf.Max(4f, endX - startX);
            float centerX = (startX + endX) * 0.5f;

            GameObject track = new GameObject("BeatPath_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.SetParent(transform, false);
            trackRect.anchorMin = new Vector2(0.5f, 0.5f);
            trackRect.anchorMax = trackRect.anchorMin;
            trackRect.anchoredPosition = new Vector2(centerX, 0f);
            trackRect.sizeDelta = new Vector2(width, pathThickness);
            trackRect.SetAsFirstSibling();
            Image trackImage = track.GetComponent<Image>();
            trackImage.sprite = pathSprite;
            trackImage.color = pathBaseColor;
            trackImage.raycastTarget = false;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(trackRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.sprite = pathSprite;
            fillImage.color = pathProgressColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
            pathFills[i] = fillImage;
        }

        GameObject marker = new GameObject("BeatPathMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pathMarker = marker.GetComponent<RectTransform>();
        pathMarker.SetParent(transform, false);
        pathMarker.anchorMin = new Vector2(0.5f, 0.5f);
        pathMarker.anchorMax = pathMarker.anchorMin;
        pathMarker.sizeDelta = new Vector2(16f, 16f);
        pathMarker.SetSiblingIndex(pathFills.Length);
        Image markerImage = marker.GetComponent<Image>();
        markerImage.sprite = pathSprite;
        markerImage.color = pathProgressColor;
        markerImage.raycastTarget = false;
        ResetPath();
    }

    private void CacheDefaults()
    {
        if (beatSlots == null)
            return;

        defaultSprites = new Sprite[beatSlots.Length];
        baseScales = new Vector3[beatSlots.Length];
        pulseTargets = new float[beatSlots.Length];
        glowTargets = new float[beatSlots.Length];
        for (int i = 0; i < beatSlots.Length; i++)
        {
            defaultSprites[i] = beatSlots[i] != null ? beatSlots[i].sprite : null;
            baseScales[i] = beatSlots[i] != null ? beatSlots[i].rectTransform.localScale : Vector3.one;
            pulseTargets[i] = 1f;
            glowTargets[i] = 0f;
        }
    }

    private Color WithReadableAlpha(Color color, float alpha)
    {
        color.a = Mathf.Max(minimumSlotAlpha, alpha);
        return color;
    }

    private Color EnsureReadableAlpha(Color color)
    {
        color.a = Mathf.Max(minimumSlotAlpha, color.a);
        return color;
    }

    private void DisableDecorativeRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void EnsureRingSlots()
    {
        if (beatSlots == null)
            return;

        ringSlots = new CompassBeatRingGraphic[beatSlots.Length];
        for (int i = 0; i < beatSlots.Length; i++)
        {
            if (beatSlots[i] == null)
                continue;

            Transform ringTransform = beatSlots[i].transform.Find("Ring");
            CompassBeatRingGraphic ring = ringTransform != null ? ringTransform.GetComponent<CompassBeatRingGraphic>() : null;
            if (ring == null)
            {
                GameObject ringObject = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(CompassBeatRingGraphic));
                RectTransform ringRect = ringObject.GetComponent<RectTransform>();
                ringRect.SetParent(beatSlots[i].transform, false);
                ringRect.anchorMin = Vector2.zero;
                ringRect.anchorMax = Vector2.one;
                ringRect.offsetMin = Vector2.zero;
                ringRect.offsetMax = Vector2.zero;
                ring = ringObject.GetComponent<CompassBeatRingGraphic>();
            }

            beatSlots[i].raycastTarget = false;
            beatSlots[i].enabled = false;
            // The opaque face must render behind the numeral, not cover it.
            ring.transform.SetAsFirstSibling();
            ringSlots[i] = ring;
        }
    }

    private void ArrangeBeatSlots()
    {
        if (beatSlots == null || beatSlots.Length == 0)
            return;

        float totalWidth = (beatSlots.Length - 1) * beatSpacing;
        float startX = -totalWidth * 0.5f;

        for (int i = 0; i < beatSlots.Length; i++)
        {
            if (beatSlots[i] == null)
                continue;

            RectTransform beatRect = beatSlots[i].rectTransform;
            beatRect.anchorMin = new Vector2(0.5f, 0.5f);
            beatRect.anchorMax = new Vector2(0.5f, 0.5f);
            beatRect.pivot = new Vector2(0.5f, 0.5f);
            beatRect.anchoredPosition = new Vector2(startX + (i * beatSpacing), 0f);
            beatRect.sizeDelta = new Vector2(ringSize, ringSize);

            if (beatTexts != null && i < beatTexts.Length && beatTexts[i] != null)
            {
                RectTransform textRect = beatTexts[i].rectTransform;
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(ringSize + 12f, ringSize + 12f);
            }

        }

        if (measureText != null)
        {
            RectTransform measureRect = measureText.rectTransform;
            measureRect.anchorMin = new Vector2(0.5f, 0f);
            measureRect.anchorMax = new Vector2(0.5f, 0f);
            measureRect.pivot = new Vector2(0.5f, 0.5f);
            measureRect.anchoredPosition = new Vector2(0f, -44f);
        }
    }
}

public class CompassBeatDisplay
{
    public bool isSilence;
    public string label;
    public Color color;
}
