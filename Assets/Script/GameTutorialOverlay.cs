using System.Collections;
using TMPro;
using UnityEngine;

public sealed class GameTutorialOverlay : MonoBehaviour
{
    private const float Margin = 14f;
    private readonly UnityEngine.UI.Image[] shade = new UnityEngine.UI.Image[4];
    private UnityEngine.UI.Image backdrop;
    private UnityEngine.UI.Image highlightedImage;
    private UnityEngine.UI.Outline drumOutline;
    private TMP_Text highlightedLabel;
    private UnityEngine.UI.Button playButton;
    private CanvasGroup playButtonGroup;
    private bool addedPlayButtonGroup;
    private bool playButtonWasInteractable;
    private float playButtonAlpha;
    private bool playButtonGroupWasInteractable;
    private bool playButtonBlockedRaycasts;
    private Renderer[] hiddenRenderers;
    private bool[] originalRendererStates;
    private Transform glowTarget;
    private Canvas overlayCanvas;
    private Canvas focusedCanvas;
    private AudioClip narrationClip;
    private RectTransform bubbleRect;
    private BeatGuideUI tutorialBeatGuide;
    private Canvas canvas;
    private RectTransform overlayRect;
    private UnityEngine.UI.Image boy;
    private TMP_Text caption;
    private Transform[] focus;
    private string[] captions;
    private float duration;
    private float elapsed;
    private Sprite[] frames;
    private int stage = -1;
    private static GameTutorialOverlay current;

    public static bool Show(Canvas canvas, string clipId, Transform[] targets, string[] lines,
        Transform hideWorld = null, Transform demoGlow = null, UnityEngine.UI.Button playButton = null)
    {
        AudioClip clip = NarrationPlayer.FindClip(clipId);
        if (canvas == null || clip == null || targets == null || lines == null ||
            targets.Length == 0 || targets.Length != lines.Length)
            return false;

        if (current != null) current.Close();
        GameObject root = new GameObject("GameTutorialOverlay", typeof(RectTransform));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
        GameTutorialOverlay tutorial = root.AddComponent<GameTutorialOverlay>();
        tutorial.Initialize(canvas, clip, clipId, targets, lines, hideWorld, demoGlow, playButton);
        current = tutorial;
        return true;
    }

    private void Initialize(Canvas canvas, AudioClip clip, string clipId, Transform[] targets, string[] lines,
        Transform hideWorld, Transform demoGlow, UnityEngine.UI.Button playButton)
    {
        this.canvas = canvas;
        overlayRect = transform as RectTransform;
        focus = targets;
        captions = lines;
        duration = Mathf.Max(clip.length, 0.1f);
        narrationClip = clip;
        glowTarget = demoGlow;
        HidePlayButton(playButton);
        overlayCanvas = gameObject.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = canvas.rootCanvas.sortingOrder + 10;
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        BoySpeakFrames boyFrames = Resources.Load<BoySpeakFrames>("BoySpeakFrames");
        frames = boyFrames != null ? boyFrames.frames : null;

        if (hideWorld != null)
        {
            hiddenRenderers = hideWorld.GetComponentsInChildren<Renderer>(true);
            originalRendererStates = new bool[hiddenRenderers.Length];
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                originalRendererStates[i] = hiddenRenderers[i].enabled;
                hiddenRenderers[i].enabled = false;
            }
        }

        backdrop = AddImage("FondoOscuro", overlayRect, null, new Color(0.02f, 0.06f, 0.16f, 0.78f));
        Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

        for (int i = 0; i < shade.Length; i++)
        {
            shade[i] = AddImage("Oscurecer" + i, overlayRect, null,
                new Color(0.02f, 0.06f, 0.16f, 0.72f));
            shade[i].raycastTarget = false;
            shade[i].enabled = false;
        }

        // An invisible full-screen blocker also covers the highlighted opening.
        UnityEngine.UI.Image blocker = AddImage("BloquearJuego", overlayRect, null, new Color(0f, 0f, 0f, 0.001f));
        Stretch(blocker.rectTransform, 0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        highlightedImage = AddImage("ElementoDestacado", overlayRect, null, Color.white);
        highlightedImage.raycastTarget = false;
        highlightedImage.enabled = false;
        drumOutline = highlightedImage.gameObject.AddComponent<UnityEngine.UI.Outline>();
        drumOutline.effectDistance = new Vector2(8f, -8f);
        drumOutline.useGraphicAlpha = true;
        drumOutline.enabled = false;
        GameObject highlightedText = new GameObject("TextoDestacado", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        highlightedText.transform.SetParent(overlayRect, false);
        highlightedLabel = highlightedText.GetComponent<TMP_Text>();
        highlightedLabel.raycastTarget = false;
        highlightedLabel.enabled = false;

        boy = AddImage("Boy_speak", overlayRect, frames != null && frames.Length > 0 ? frames[0] : null, Color.white);
        boy.raycastTarget = false;
        boy.preserveAspect = true;
        RectTransform boyRect = boy.rectTransform;
        boyRect.anchorMin = boyRect.anchorMax = new Vector2(0f, 0f);
        boyRect.pivot = new Vector2(0f, 0f);
        boyRect.anchoredPosition = new Vector2(26f, 16f);
        float boySize = Mathf.Min(230f, overlayRect.rect.width * 0.25f);
        boyRect.sizeDelta = new Vector2(boySize, boySize);

        GameObject bubble = new GameObject("Explicacion", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        bubbleRect = bubble.GetComponent<RectTransform>();
        bubbleRect.SetParent(overlayRect, false);
        bubbleRect.anchorMin = new Vector2(0.26f, 0f);
        bubbleRect.anchorMax = new Vector2(0.78f, 0f);
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.anchoredPosition = new Vector2(0f, 24f);
        bubbleRect.sizeDelta = new Vector2(0f, 112f);
        bubble.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.97f, 0.87f, 0.97f);

        GameObject text = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.SetParent(bubbleRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 10f);
        textRect.offsetMax = new Vector2(-24f, -10f);
        caption = text.GetComponent<TMP_Text>();
        caption.fontSize = 28f;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 18f;
        caption.fontSizeMax = 28f;
        caption.alignment = TextAlignmentOptions.Center;
        caption.color = new Color(0.09f, 0.16f, 0.3f);
        caption.raycastTarget = false;

        GameObject skip = new GameObject("OmitirTutorial", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        RectTransform skipRect = skip.GetComponent<RectTransform>();
        skipRect.SetParent(overlayRect, false);
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(0.79f, 0f);
        skipRect.pivot = new Vector2(0f, 0.5f);
        skipRect.anchoredPosition = new Vector2(0f, 80f);
        skipRect.sizeDelta = new Vector2(Mathf.Min(140f, overlayRect.rect.width * 0.18f), 60f);
        skip.GetComponent<UnityEngine.UI.Image>().color = new Color(0.13f, 0.37f, 0.78f);
        skip.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Skip);
        GameObject skipText = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform skipTextRect = skipText.GetComponent<RectTransform>();
        skipTextRect.SetParent(skipRect, false);
        skipTextRect.anchorMin = Vector2.zero;
        skipTextRect.anchorMax = Vector2.one;
        skipTextRect.offsetMin = skipTextRect.offsetMax = Vector2.zero;
        TMP_Text skipLabel = skipText.GetComponent<TMP_Text>();
        skipLabel.text = "Omitir";
        skipLabel.fontSize = 27f;
        skipLabel.enableAutoSizing = true;
        skipLabel.fontSizeMin = 17f;
        skipLabel.fontSizeMax = 27f;
        skipLabel.alignment = TextAlignmentOptions.Center;
        skipLabel.color = Color.white;
        skipLabel.raycastTarget = false;

        NarrationPlayer.Say(clipId);
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        while (elapsed < duration || NarrationPlayer.IsPlaying(narrationClip))
        {
            elapsed += Time.unscaledDeltaTime;
            int next = Mathf.Min(focus.Length - 1, Mathf.FloorToInt(elapsed / duration * focus.Length));
            if (next != stage)
            {
                if (tutorialBeatGuide != null) tutorialBeatGuide.EndTutorialCue();
                stage = next;
                caption.text = captions[stage];
                tutorialBeatGuide = focus[stage] != null ? focus[stage].GetComponentInParent<BeatGuideUI>() : null;
            }
            if (tutorialBeatGuide != null)
                tutorialBeatGuide.ShowTutorialCue(Mathf.Repeat(Time.unscaledTime * 0.65f, 1f), "SALTA\nAL PULSO");
            if (frames != null && frames.Length > 0)
                boy.sprite = frames[Mathf.FloorToInt(Time.unscaledTime * 7f) % frames.Length];
            Transform target = focus[stage];
            if (playButton != null && target == playButton.transform && stage > 0)
                target = focus[stage - 1];
            UpdateSpotlight(target);
            yield return null;
        }
        Close();
    }

    private void Skip()
    {
        NarrationPlayer.Stop();
        Close();
    }

    private void OnDestroy()
    {
        if (tutorialBeatGuide != null) tutorialBeatGuide.EndTutorialCue();
        ReleaseFocusedCanvas();
        RestoreHiddenRenderers();
        RestorePlayButton();
        if (current == this) current = null;
    }

    private void HidePlayButton(UnityEngine.UI.Button button)
    {
        if (button == null) return;
        playButton = button;
        playButtonWasInteractable = button.interactable;
        playButtonGroup = button.GetComponent<CanvasGroup>();
        if (playButtonGroup == null)
        {
            playButtonGroup = button.gameObject.AddComponent<CanvasGroup>();
            addedPlayButtonGroup = true;
        }
        playButtonAlpha = playButtonGroup.alpha;
        playButtonGroupWasInteractable = playButtonGroup.interactable;
        playButtonBlockedRaycasts = playButtonGroup.blocksRaycasts;
        playButtonGroup.alpha = 0f;
        playButtonGroup.interactable = false;
        playButtonGroup.blocksRaycasts = false;
        button.interactable = false;
    }

    private void RestorePlayButton()
    {
        if (playButtonGroup != null)
        {
            playButtonGroup.alpha = playButtonAlpha;
            playButtonGroup.interactable = playButtonGroupWasInteractable;
            playButtonGroup.blocksRaycasts = playButtonBlockedRaycasts;
            if (addedPlayButtonGroup) Destroy(playButtonGroup);
        }
        if (playButton != null) playButton.interactable = playButtonWasInteractable;
        playButton = null;
        playButtonGroup = null;
    }

    private void RestoreHiddenRenderers()
    {
        if (hiddenRenderers != null)
            for (int i = 0; i < hiddenRenderers.Length; i++)
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = originalRendererStates[i];
        hiddenRenderers = null;
    }

    private void Close()
    {
        ReleaseFocusedCanvas();
        RestoreHiddenRenderers();
        RestorePlayButton();
        if (this != null) Destroy(gameObject);
    }

    private void ReleaseFocusedCanvas()
    {
        if (focusedCanvas == null) return;
        focusedCanvas.enabled = false;
        Destroy(focusedCanvas);
        focusedCanvas = null;
    }

    private void UpdateSpotlight(Transform target)
    {
        highlightedImage.enabled = false;
        highlightedLabel.enabled = false;
        drumOutline.enabled = false;
        if (target != null && target.GetComponent<BeatGuideUI>() != null)
        {
            if (focusedCanvas == null)
            {
                focusedCanvas = target.gameObject.AddComponent<Canvas>();
                focusedCanvas.overrideSorting = true;
                focusedCanvas.sortingOrder = overlayCanvas.sortingOrder + 1;
            }
            backdrop.enabled = true;
            for (int i = 0; i < shade.Length; i++) shade[i].enabled = false;
            return;
        }
        ReleaseFocusedCanvas();
        RectTransform rect = target as RectTransform;
        Vector3[] corners = new Vector3[8];
        Camera targetCamera;
        SpriteRenderer spriteRenderer = null;
        if (rect != null && rect.gameObject.activeInHierarchy)
        {
            rect.GetWorldCorners(corners);
            targetCamera = rect.GetComponentInParent<Canvas>()?.worldCamera;
        }
        else
        {
            Renderer renderer = target != null ? target.GetComponentInChildren<Renderer>() : null;
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
            {
                Rect whole = overlayRect.rect;
                SetShade(shade[0], whole.xMin, whole.yMin, whole.xMax, whole.yMax);
                backdrop.enabled = true;
                for (int i = 0; i < shade.Length; i++) shade[i].enabled = false;
                return;
            }
            spriteRenderer = renderer as SpriteRenderer;
            Bounds bounds = renderer.bounds;
            targetCamera = Camera.main;
            if (targetCamera == null) targetCamera = FindFirstObjectByType<Camera>();
            if (targetCamera == null) return;
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
        }

        Camera overlayCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        int count = rect != null && rect.gameObject.activeInHierarchy ? 4 : 8;
        for (int i = 0; i < count; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screen, overlayCamera, out Vector2 point);
            left = Mathf.Min(left, point.x);
            right = Mathf.Max(right, point.x);
            bottom = Mathf.Min(bottom, point.y);
            top = Mathf.Max(top, point.y);
        }
        Rect area = overlayRect.rect;
        left = Mathf.Clamp(left, area.xMin, area.xMax);
        right = Mathf.Clamp(right, area.xMin, area.xMax);
        bottom = Mathf.Clamp(bottom, area.yMin, area.yMax);
        top = Mathf.Clamp(top, area.yMin, area.yMax);

        UnityEngine.UI.Image originalImage = rect != null ? rect.GetComponent<UnityEngine.UI.Image>() : null;
        Sprite sprite = originalImage != null ? originalImage.sprite : spriteRenderer != null ? spriteRenderer.sprite : null;
        bool canCopy = (originalImage != null && originalImage.color.a > 0.05f) ||
            (spriteRenderer != null && spriteRenderer.color.a > 0.05f && sprite != null);
        if (canCopy && right > left && top > bottom)
        {
            backdrop.enabled = true;
            for (int i = 0; i < shade.Length; i++) shade[i].enabled = false;
            highlightedImage.enabled = true;
            highlightedImage.sprite = sprite;
            highlightedImage.type = originalImage != null ? originalImage.type : UnityEngine.UI.Image.Type.Simple;
            highlightedImage.preserveAspect = originalImage != null && originalImage.preserveAspect;
            highlightedImage.color = originalImage != null ? originalImage.color : spriteRenderer.color;
            float displayOffset = 0f;
            if (target == glowTarget)
            {
                float footerTop = area.yMin + bubbleRect.anchoredPosition.y + bubbleRect.rect.height;
                displayOffset = Mathf.Min(Mathf.Max(0f, footerTop + 22f - bottom),
                    Mathf.Max(0f, area.yMax - 20f - top));
            }
            SetShade(highlightedImage, left, bottom + displayOffset, right, top + displayOffset);

            TMP_Text originalLabel = target.GetComponentInChildren<TMP_Text>(true);
            if (originalLabel != null && originalLabel.gameObject != target.gameObject)
            {
                highlightedLabel.enabled = true;
                highlightedLabel.text = originalLabel.text;
                highlightedLabel.font = originalLabel.font;
                highlightedLabel.fontSize = originalLabel.fontSize;
                highlightedLabel.color = originalLabel.color;
                highlightedLabel.alignment = originalLabel.alignment;
                SetShade(highlightedLabel.rectTransform, left, bottom, right, top);
            }

            if (target == glowTarget)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                drumOutline.enabled = true;
                drumOutline.effectColor = new Color(1f, 0.78f, 0.17f, Mathf.Lerp(0.45f, 0.95f, pulse));
            }
            return;
        }

        backdrop.enabled = false;
        // The runner's action label sits below the circular beat guide's own RectTransform.
        if (target.GetComponent<BeatGuideUI>() != null)
            bottom -= 185f;
        left = Mathf.Clamp(left - Margin, area.xMin, area.xMax);
        right = Mathf.Clamp(right + Margin, area.xMin, area.xMax);
        bottom = Mathf.Clamp(bottom - Margin, area.yMin, area.yMax);
        top = Mathf.Clamp(top + Margin, area.yMin, area.yMax);
        SetShade(shade[0], area.xMin, top, area.xMax, area.yMax);
        SetShade(shade[1], area.xMin, area.yMin, area.xMax, bottom);
        SetShade(shade[2], area.xMin, bottom, left, top);
        SetShade(shade[3], right, bottom, area.xMax, top);
        for (int i = 0; i < shade.Length; i++) shade[i].enabled = true;
    }

    private static UnityEngine.UI.Image AddImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void SetShade(UnityEngine.UI.Image image, float left, float bottom, float right, float top)
    {
        SetShade(image.rectTransform, left, bottom, right, top);
    }

    private static void SetShade(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom));
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
