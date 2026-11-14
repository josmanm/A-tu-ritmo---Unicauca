using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProfileMenuController : MonoBehaviour
{
    public static ProfileMenuController Instance { get; private set; }
    private static bool autoShownThisAppSession;

    [Header("Defaults")]
    [SerializeField] private string defaultAvatar = "conejo_azul";

    [Header("Scene References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newPerfilButton;
    [SerializeField] private Button updateButton;
    [SerializeField] private bool useProfileArtwork;
    [SerializeField] private GameObject selectionContent;
    [SerializeField] private GameObject profileBackdrop;
    private Button exitButton;

    [Header("Cards")]
    [SerializeField] private GameObject cardProfilePrefab;
    [SerializeField] private RectTransform cardsContainer;
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Estilo de botones del juego")]
    [SerializeField] private Sprite buttonNormal;
    [SerializeField] private Sprite buttonHover;
    [SerializeField] private Sprite buttonPressed;
    [SerializeField] private Sprite buttonDisabled;
    [SerializeField] private Sprite buttonFocused;
    [SerializeField] private Sprite createPanelSprite;
    [SerializeField] private Sprite confirmNormal;
    [SerializeField] private Sprite confirmHover;
    [SerializeField] private Sprite confirmPressed;
    [SerializeField] private Sprite confirmDisabled;
    [SerializeField] private Sprite cancelNormal;
    [SerializeField] private Sprite cancelHover;
    [SerializeField] private Sprite cancelPressed;
    [SerializeField] private Sprite cancelDisabled;
    [SerializeField] private Sprite refreshNormal;
    [SerializeField] private Sprite refreshPressed;

    private readonly List<PlayerProfileData> loadedProfiles = new List<PlayerProfileData>();
    private readonly List<Button> profileCardButtons = new List<Button>();
    private readonly List<Button> avatarButtons = new List<Button>();

    private static readonly Color DarkText = new Color(0.094f, 0.188f, 0.31f, 1f);
    private static Sprite roundedNameInputSprite;

    private ScrollRect profilesScrollRect;
    private TMP_InputField createNameInput;
    private TMP_Text createAvatarLabel;
    private GameObject createPopupRoot;
    private int selectedAvatarIndex;
    private bool waitingForFirebase;
    private Vector2 lastPanelCanvasSize;

    private const float CardsViewportWidth = 820f;
    private const float CardsViewportHeight = 260f;
    private const float CardWidth = 150f;
    private const float CardHeight = 182f;
    private const float CardSpacing = 18f;
    private const float ArtworkCardWidth = 180f;
    private const float ArtworkCardSpacing = 22f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MainScene")
        {
            Destroy(this);
            return;
        }

        ResolveReferences();
        GameCanvasBackgroundCover.Attach(GetComponentInParent<Canvas>(), "Background");
        if (!useProfileArtwork)
            StyleStatusAndRefreshButton();
        EnsureCardsContainer();
        EnsureCreatePopup();
        ConfigureButtons();
        if (useProfileArtwork && panelRoot != null)
        {
            // Match the blue already authored on CardProfile's text. Do not
            // recolor labels that deliberately use another palette color.
            foreach (TMP_Text text in panelRoot.GetComponentsInChildren<TMP_Text>(true))
                if (text.color == Color.white) text.color = DarkText;
        }

        if (PlayerProfileManager.Instance != null)
            PlayerProfileManager.Instance.ActiveProfileChanged += HandleActiveProfileChanged;

        RefreshActiveProfileDisplay();

        if (!autoShownThisAppSession)
        {
            autoShownThisAppSession = true;
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (PlayerProfileManager.Instance != null)
            PlayerProfileManager.Instance.ActiveProfileChanged -= HandleActiveProfileChanged;

        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.Ready -= HandleFirebaseReady;
    }

    public void Show()
    {
        ResolveReferences();
        EnsureCardsContainer();
        EnsureCreatePopup();

        if (panelRoot == null)
            return;

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        if (profileBackdrop != null)
        {
            profileBackdrop.transform.SetSiblingIndex(Mathf.Max(0, panelRoot.transform.GetSiblingIndex() - 1));
            profileBackdrop.SetActive(true);
        }
        if (selectionContent != null)
            selectionContent.SetActive(true);
        if (createPopupRoot != null)
            createPopupRoot.SetActive(false);
        RefreshProfileButtonSkins();
        FitPanelToCanvas();
        RefreshActiveProfileDisplay();
        RefreshContinueButton();
        NarrationPlayer.Say("perfil_bienvenida");
        LoadProfilesOrWaitForFirebase();
    }

    public void Hide()
    {
        if (createPopupRoot != null)
            createPopupRoot.SetActive(false);

        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (profileBackdrop != null)
            profileBackdrop.SetActive(false);
    }

    private void LateUpdate()
    {
        if (panelRoot != null && panelRoot.activeSelf)
            FitPanelToCanvas();
    }

    private void FitPanelToCanvas()
    {
        RectTransform panel = panelRoot != null ? panelRoot.transform as RectTransform : null;
        RectTransform canvas = panel != null && panel.GetComponentInParent<Canvas>() != null
            ? panel.GetComponentInParent<Canvas>().transform as RectTransform : null;
        if (canvas == null || canvas.rect.width <= 0f || canvas.rect.height <= 0f ||
            canvas.rect.size == lastPanelCanvasSize)
            return;

        lastPanelCanvasSize = canvas.rect.size;
        float scale = Mathf.Min(1f, (canvas.rect.width - 60f) / panel.rect.width,
            (canvas.rect.height - 100f) / panel.rect.height);
        scale = Mathf.Max(0.55f, scale);
        panel.localScale = Vector3.one * scale;
        if (createPopupRoot != null && !useProfileArtwork)
            createPopupRoot.transform.localScale = Vector3.one;
    }

    private void ResolveReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (statusText == null)
            statusText = FindTextByName(panelRoot != null ? panelRoot.transform : null, "Status");

        if (continueButton == null)
            continueButton = FindButtonByName(panelRoot != null ? panelRoot.transform : null, "ContinueButton");

        if (newPerfilButton == null)
            newPerfilButton = FindButtonByName(panelRoot != null ? panelRoot.transform : null, "NewPerfilButton");

        if (updateButton == null)
            updateButton = FindButtonByName(panelRoot != null ? panelRoot.transform : null, "Update");

        if (useProfileArtwork && panelRoot != null)
        {
            if (selectionContent == null)
            {
                Transform selection = panelRoot.transform.Find("ProfileSelectionContent");
                if (selection != null) selectionContent = selection.gameObject;
            }
            if (profileBackdrop == null)
            {
                Transform backdrop = panelRoot.transform.parent != null
                    ? panelRoot.transform.parent.Find("ProfileBackdrop") : null;
                if (backdrop != null) profileBackdrop = backdrop.gameObject;
            }
        }
    }

    private void StyleStatusAndRefreshButton()
    {
        if (statusText != null)
        {
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchoredPosition = new Vector2(-132f, 190f);
            statusRect.sizeDelta = new Vector2(540f, 76f);
            statusText.color = DarkText;
            statusText.fontSize = 28f;
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 26f;
            statusText.fontSizeMax = 28f;
            statusText.fontStyle = FontStyles.Bold;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.textWrappingMode = TextWrappingModes.Normal;
            statusText.raycastTarget = false;

            Transform surface = panelRoot.transform.Find("StatusSurface");
            if (surface == null)
            {
                GameObject surfaceObject = new GameObject("StatusSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
                surfaceRect.SetParent(panelRoot.transform, false);
                surfaceRect.anchorMin = surfaceRect.anchorMax = new Vector2(0.5f, 0.5f);
                surfaceRect.pivot = new Vector2(0.5f, 0.5f);
                surfaceRect.anchoredPosition = statusRect.anchoredPosition;
                surfaceRect.sizeDelta = statusRect.sizeDelta;
                Image surfaceImage = surfaceObject.GetComponent<Image>();
                surfaceImage.color = new Color(1f, 0.941f, 0.816f, 1f);
                surfaceImage.raycastTarget = false;
                surfaceRect.SetSiblingIndex(statusRect.GetSiblingIndex());
            }
        }

        if (updateButton != null)
        {
            RectTransform refreshRect = updateButton.GetComponent<RectTransform>();
            refreshRect.anchoredPosition = new Vector2(325f, 190f);
            refreshRect.sizeDelta = new Vector2(208f, 72f);
            Image refreshImage = updateButton.GetComponent<Image>();
            if (refreshImage != null)
                refreshImage.preserveAspect = false;
            StyleGameButton(updateButton, refreshNormal != null ? refreshNormal : buttonNormal,
                refreshNormal != null ? refreshNormal : buttonHover,
                refreshPressed != null ? refreshPressed : buttonPressed, buttonDisabled);
            SetButtonText(updateButton, "Actualizar", 26f);
        }
    }

    private void ConfigureButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (newPerfilButton != null)
        {
            newPerfilButton.interactable = true;
            newPerfilButton.onClick.RemoveAllListeners();
            newPerfilButton.onClick.AddListener(OnNewProfileClicked);
        }

        if (updateButton != null)
        {
            updateButton.interactable = true;
            updateButton.onClick.RemoveAllListeners();
            updateButton.onClick.AddListener(LoadProfilesOrWaitForFirebase);
        }

        foreach (Button button in new[] { continueButton, newPerfilButton })
        {
            if (button == null) continue;
            if (useProfileArtwork) continue;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250f, 82f);
            bool isCreate = button == newPerfilButton;
            StyleGameButton(button, isCreate ? buttonNormal : confirmNormal,
                isCreate ? buttonHover : confirmHover, isCreate ? buttonPressed : confirmPressed,
                isCreate ? buttonDisabled : confirmDisabled);
            SetButtonText(button, isCreate ? "Crear perfil" : "Entrar", 30f);
        }

        EnsureExitButton();
    }

    private void EnsureExitButton()
    {
        if (panelRoot == null)
            return;

        Transform existing = FindChild(panelRoot.transform, "ExitProfileButton");
        if (existing != null)
            exitButton = existing.GetComponent<Button>();
        if (exitButton == null)
        {
            exitButton = CreateActionButton(selectionContent != null ? selectionContent.transform : panelRoot.transform,
                "Salir", new Vector2(0f, -295f),
                new Vector2(208f, 72f), Color.white);
            exitButton.gameObject.name = "ExitProfileButton";
        }

        if (!useProfileArtwork)
        {
            StyleGameButton(exitButton, cancelNormal, cancelHover, cancelPressed, cancelDisabled);
            SetButtonText(exitButton, "Salir", 28f);
        }
        exitButton.onClick.RemoveAllListeners();
        exitButton.onClick.AddListener(Hide);
        // A scene-saved popup must still cover the navigation buttons when it is open.
        if (createPopupRoot != null)
            createPopupRoot.transform.SetAsLastSibling();
    }

    private void SetButtonText(Button button, string value, float fontSize)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            label = CreateLabel(value, button.transform, Vector2.zero, Vector2.zero,
                fontSize, FontStyles.Bold, DarkText, TextAlignmentOptions.Center);

        label.gameObject.SetActive(true);
        label.text = value;
        if (statusText != null && statusText.font != null)
            label.font = statusText.font;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);
        label.color = DarkText;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = fontSize;
        label.fontSizeMin = 21f;
        label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.transform.SetAsLastSibling();
    }

    private void EnsureCardsContainer()
    {
        if (panelRoot == null)
            return;

        if (cardsContainer != null)
        {
            if (profilesScrollRect == null)
                profilesScrollRect = cardsContainer.GetComponentInParent<ScrollRect>();
            if (useProfileArtwork && cardsContainer.TryGetComponent(out HorizontalLayoutGroup artworkLayout))
                artworkLayout.spacing = ArtworkCardSpacing;
            return;
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        GameObject viewportObject = new GameObject("ProfilesViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.SetParent(panelRect, false);
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -18f);
        viewportRect.sizeDelta = new Vector2(CardsViewportWidth, CardsViewportHeight);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        profilesScrollRect = viewportObject.GetComponent<ScrollRect>();
        profilesScrollRect.horizontal = true;
        profilesScrollRect.vertical = false;
        profilesScrollRect.movementType = ScrollRect.MovementType.Clamped;
        profilesScrollRect.scrollSensitivity = 18f;

        GameObject contentObject = new GameObject("ProfilesContent", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        cardsContainer = contentObject.GetComponent<RectTransform>();
        cardsContainer.SetParent(viewportRect, false);
        cardsContainer.anchorMin = new Vector2(0f, 0.5f);
        cardsContainer.anchorMax = new Vector2(0f, 0.5f);
        cardsContainer.pivot = new Vector2(0f, 0.5f);
        cardsContainer.anchoredPosition = Vector2.zero;
        cardsContainer.sizeDelta = new Vector2(CardsViewportWidth, CardsViewportHeight);

        HorizontalLayoutGroup layout = contentObject.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = CardSpacing;
        layout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        profilesScrollRect.viewport = viewportRect;
        profilesScrollRect.content = cardsContainer;
    }

    private void EnsureCreatePopup()
    {
        if (panelRoot == null || createPopupRoot != null)
            return;

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        Transform existingPopup = panelRoot.transform.Find("CreateProfilePopup");
        if (existingPopup != null)
        {
            if (useProfileArtwork)
                ConfigureArtworkCreatePopup(existingPopup);
            else
                ConfigureExistingCreatePopup(existingPopup);
            return;
        }

        createPopupRoot = new GameObject("CreateProfilePopup", typeof(RectTransform), typeof(Image));
        RectTransform popupRect = createPopupRoot.GetComponent<RectTransform>();
        popupRect.SetParent(panelRect, false);
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = new Vector2(600f, 500f);

        Image popupImage = createPopupRoot.GetComponent<Image>();
        popupImage.sprite = createPanelSprite;
        popupImage.type = createPanelSprite != null && createPanelSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        popupImage.color = createPanelSprite != null ? Color.white : new Color(1f, 0.99f, 0.97f, 0.98f);

        CreateLabel("Crear perfil", createPopupRoot.transform, new Vector2(0f, 190f), new Vector2(340f, 48f), 34f,
            FontStyles.Bold, DarkText, TextAlignmentOptions.Center);

        createNameInput = CreateInputField(createPopupRoot.transform, new Vector2(0f, 105f), new Vector2(440f, 72f), "Nombre del jugador");
        StyleNameInput();

        createAvatarLabel = CreateLabel("Elige tu avatar", createPopupRoot.transform, new Vector2(0f, 36f), new Vector2(360f, 38f), 26f, FontStyles.Bold, DarkText, TextAlignmentOptions.Center);

        CreateAvatarSelectorButtons();

        Button cancelButton = CreateActionButton(createPopupRoot.transform, "Cancelar", new Vector2(-114f, -190f), new Vector2(208f, 72f), new Color(0.86f, 0.86f, 0.89f, 1f));
        ConfigurePopupAction(cancelButton, new Vector2(-114f, -190f), cancelNormal, cancelHover,
            cancelPressed, cancelDisabled, CloseCreatePopup);

        Button confirmButton = CreateActionButton(createPopupRoot.transform, "Guardar", new Vector2(114f, -190f), new Vector2(208f, 72f), new Color(0.52f, 0.8f, 0.45f, 1f));
        ConfigurePopupAction(confirmButton, new Vector2(114f, -190f), confirmNormal, confirmHover,
            confirmPressed, confirmDisabled, ConfirmCreateProfile);

        createPopupRoot.SetActive(false);
    }

    private void ConfigureExistingCreatePopup(Transform popup)
    {
        createPopupRoot = popup.gameObject;
        RectTransform popupRect = popup as RectTransform;
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = new Vector2(600f, 500f);
        Image panelImage = popup.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = createPanelSprite;
            panelImage.type = createPanelSprite != null && createPanelSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = createPanelSprite != null ? Color.white : new Color(1f, 0.99f, 0.97f, 0.98f);
        }

        createNameInput = popup.GetComponentInChildren<TMP_InputField>(true);
        if (createNameInput != null)
        {
            createNameInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 105f);
            createNameInput.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 72f);
            StyleNameInput();
        }

        foreach (Transform child in popup)
        {
            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text == null) continue;
            if (text.text == "Crear perfil")
            {
                text.color = DarkText;
                text.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            }
            else
            {
                createAvatarLabel = text;
                text.color = DarkText;
                text.rectTransform.anchoredPosition = new Vector2(0f, 36f);
            }
        }

        avatarButtons.Clear();
        foreach (Transform child in popup)
        {
            if (child.name == "AvatarButton" && child.TryGetComponent(out Button button))
                avatarButtons.Add(button);
        }
        avatarButtons.Sort((a, b) => a.GetComponent<RectTransform>().anchoredPosition.x
            .CompareTo(b.GetComponent<RectTransform>().anchoredPosition.x));
        for (int i = 0; i < avatarButtons.Count; i++)
        {
            int avatarIndex = i;
            Button button = avatarButtons[i];
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(-130f + i * 130f, -58f);
            rect.sizeDelta = new Vector2(104f, 104f);
            StyleGameButton(button);
            Image icon = GetOrCreateAvatarImage(button.transform, new Vector2(82f, 82f), Vector2.zero);
            icon.rectTransform.sizeDelta = new Vector2(82f, 82f);
            icon.sprite = GetAvatarSpriteByIndex(i);
            icon.color = icon.sprite != null ? Color.white : GetFallbackAvatarColor(i);
            icon.raycastTarget = false;
            EnsureAvatarButtonLabel(button, i);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectAvatarIndex(avatarIndex));
        }

        ConfigurePopupAction(FindButtonByName(popup, "CancelarButton"), new Vector2(-114f, -190f),
            cancelNormal, cancelHover, cancelPressed, cancelDisabled, CloseCreatePopup);
        ConfigurePopupAction(FindButtonByName(popup, "GuardarButton"), new Vector2(114f, -190f),
            confirmNormal, confirmHover, confirmPressed, confirmDisabled, ConfirmCreateProfile);
        SelectAvatarIndex(0);
        createPopupRoot.SetActive(false);
    }

    private void ConfigureArtworkCreatePopup(Transform popup)
    {
        createPopupRoot = popup.gameObject;
        createNameInput = popup.GetComponentInChildren<TMP_InputField>(true);
        createAvatarLabel = null;
        foreach (Transform child in popup)
        {
            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text != null && text.text != "Crear perfil")
                createAvatarLabel = text;
        }

        avatarButtons.Clear();
        foreach (Transform child in popup)
            if (child.name == "AvatarButton" && child.TryGetComponent(out Button button))
                avatarButtons.Add(button);
        avatarButtons.Sort((a, b) => a.GetComponent<RectTransform>().anchoredPosition.x
            .CompareTo(b.GetComponent<RectTransform>().anchoredPosition.x));

        for (int i = 0; i < avatarButtons.Count; i++)
        {
            int avatarIndex = i;
            Button button = avatarButtons[i];
            Image icon = button.transform.Find("AvatarImage")?.GetComponent<Image>();
            if (icon != null)
                icon.sprite = GetAvatarSpriteByIndex(i);
            TMP_Text avatarName = button.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (avatarName != null)
            {
                avatarName.text = GetAvatarDisplayName(i);
                avatarName.gameObject.SetActive(true);
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectAvatarIndex(avatarIndex));
        }

        ConfigurePopupAction(FindButtonByName(popup, "CancelarButton"), Vector2.zero,
            null, null, null, null, CloseCreatePopup);
        ConfigurePopupAction(FindButtonByName(popup, "GuardarButton"), Vector2.zero,
            null, null, null, null, ConfirmCreateProfile);
        SelectAvatarIndex(0);
        createPopupRoot.SetActive(false);
    }

    private void ConfigurePopupAction(Button button, Vector2 position, Sprite normal, Sprite hover,
        Sprite pressed, Sprite disabled, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        if (useProfileArtwork)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            return;
        }
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(208f, 72f);
        StyleGameButton(button, normal, hover, pressed, disabled);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            label = CreateLabel(button.name == "GuardarButton" ? "Guardar" : "Cancelar",
                button.transform, Vector2.zero, new Vector2(185f, 54f), 28f, FontStyles.Bold, DarkText, TextAlignmentOptions.Center);
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = button.name == "GuardarButton" ? "Guardar" : "Cancelar";
            if (statusText != null)
                label.font = statusText.font;
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(185f, 54f);
            label.enableAutoSizing = false;
            label.color = DarkText;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void StyleNameInput()
    {
        if (createNameInput == null)
            return;

        Image background = createNameInput.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = GetRoundedNameInputSprite();
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }

        if (createNameInput.textViewport != null)
        {
            createNameInput.textViewport.offsetMin = new Vector2(22f, 8f);
            createNameInput.textViewport.offsetMax = new Vector2(-22f, -8f);
        }

        foreach (TMP_Text text in new[] { createNameInput.textComponent, createNameInput.placeholder as TMP_Text })
        {
            if (text == null)
                continue;
            if (statusText != null)
                text.font = statusText.font;
            text.enableAutoSizing = false;
            text.fontSize = 27f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.color = text == createNameInput.textComponent ? DarkText : new Color(0.38f, 0.42f, 0.48f);
        }
    }

    private static Sprite GetRoundedNameInputSprite()
    {
        if (roundedNameInputSprite != null)
            return roundedNameInputSprite;

        // Borde negro de un píxel en la textura y esquinas que mantienen su forma al estirar el campo.
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Vector2 p = new Vector2(Mathf.Abs(x - 31.5f), Mathf.Abs(y - 31.5f)) - new Vector2(16.5f, 16.5f);
                float distance = new Vector2(Mathf.Max(p.x, 0f), Mathf.Max(p.y, 0f)).magnitude +
                    Mathf.Min(Mathf.Max(p.x, p.y), 0f) - 14f;
                float outerCoverage = Mathf.Clamp01(0.5f - distance);
                float innerCoverage = Mathf.Clamp01(-0.5f - distance);
                Color pixel = Color.Lerp(Color.black, Color.white,
                    outerCoverage > 0f ? innerCoverage / outerCoverage : 0f);
                pixel.a = outerCoverage;
                texture.SetPixel(x, y, pixel);
            }
        }
        texture.Apply();
        roundedNameInputSprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
        return roundedNameInputSprite;
    }

    private void EnsureAvatarButtonLabel(Button button, int avatarIndex)
    {
        Transform existing = button.transform.Find("AvatarNameLabel");
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (label == null)
            label = CreateLabel(string.Empty, button.transform, new Vector2(0f, -73f),
                new Vector2(130f, 30f), 19f, FontStyles.Bold, DarkText, TextAlignmentOptions.Center);
        label.gameObject.name = "AvatarNameLabel";
        label.text = GetAvatarDisplayName(avatarIndex);
        label.rectTransform.anchoredPosition = new Vector2(0f, -73f);
        label.rectTransform.sizeDelta = new Vector2(130f, 30f);
        if (statusText != null)
            label.font = statusText.font;
        label.fontSize = 19f;
        label.fontSizeMin = 17f;
        label.color = DarkText;
        label.raycastTarget = false;
    }

    private void CreateAvatarSelectorButtons()
    {
        for (int i = 0; i < 3; i++)
        {
            int avatarIndex = i;
            Button avatarButton = CreateIconButton(createPopupRoot.transform, new Vector2(-130f + i * 130f, -58f), new Vector2(104f, 104f));
            StyleGameButton(avatarButton);
            avatarButtons.Add(avatarButton);
            Image icon = GetOrCreateAvatarImage(avatarButton.transform, new Vector2(82f, 82f), Vector2.zero);
            icon.sprite = GetAvatarSpriteByIndex(avatarIndex);
            icon.color = icon.sprite != null ? Color.white : GetFallbackAvatarColor(avatarIndex);
            icon.raycastTarget = false;

            TMP_Text fallback = CreateLabel(GetAvatarShortName(avatarIndex), avatarButton.transform, Vector2.zero, new Vector2(70f, 32f), 26f, FontStyles.Bold, DarkText, TextAlignmentOptions.Center);
            fallback.raycastTarget = false;
            fallback.gameObject.SetActive(icon.sprite == null);
            EnsureAvatarButtonLabel(avatarButton, avatarIndex);

            avatarButton.onClick.AddListener(() => SelectAvatarIndex(avatarIndex));
        }

        SelectAvatarIndex(0);
    }

    private void SelectAvatarIndex(int avatarIndex)
    {
        selectedAvatarIndex = Mathf.Clamp(avatarIndex, 0, 2);
        if (createAvatarLabel != null)
            createAvatarLabel.text = GetAvatarDisplayName(selectedAvatarIndex);

        for (int i = 0; i < avatarButtons.Count; i++)
        {
            Outline outline = avatarButtons[i].GetComponent<Outline>();
            if (outline == null)
                outline = avatarButtons[i].gameObject.AddComponent<Outline>();
            outline.effectColor = i == selectedAvatarIndex ? DarkText : new Color(0.094f, 0.188f, 0.31f, 0.2f);
            outline.effectDistance = i == selectedAvatarIndex ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
        }
    }

    private void StyleGameButton(Button button)
    {
        StyleGameButton(button, buttonNormal, buttonHover, buttonPressed, buttonDisabled);
    }

    private void StyleGameButton(Button button, Sprite normal, Sprite hover, Sprite pressed, Sprite disabled)
    {
        if (button == null || normal == null)
            return;

        CartoonButtonSkin skin = button.GetComponent<CartoonButtonSkin>();
        if (skin == null)
            skin = button.gameObject.AddComponent<CartoonButtonSkin>();
        // Keep the button's colour visible even when entering is not yet available.
        skin.Configure(normal, hover, pressed, disabled != null ? disabled : normal, normal);
        // A selected UI button must keep its saturated normal colour on touch devices.
        SpriteState states = button.spriteState;
        states.selectedSprite = normal;
        button.spriteState = states;
    }

    private void RefreshProfileButtonSkins()
    {
        if (useProfileArtwork) return;
        StyleGameButton(updateButton, refreshNormal != null ? refreshNormal : buttonNormal,
            refreshNormal != null ? refreshNormal : buttonHover,
            refreshPressed != null ? refreshPressed : buttonPressed, buttonDisabled);
        StyleGameButton(newPerfilButton);
        StyleGameButton(continueButton, confirmNormal, confirmHover, confirmPressed, confirmDisabled);
        StyleGameButton(exitButton, cancelNormal, cancelHover, cancelPressed, cancelDisabled);
    }

    private void LoadProfilesOrWaitForFirebase()
    {
        FirebaseManager firebaseManager = FirebaseManager.Instance;
        waitingForFirebase = firebaseManager == null || !firebaseManager.IsReady;
        RefreshButtonState(true);
        LoadProfiles();

        if (waitingForFirebase && firebaseManager != null)
        {
            firebaseManager.Ready -= HandleFirebaseReady;
            firebaseManager.Ready += HandleFirebaseReady;
        }
    }

    private void LoadProfiles()
    {
        if (PlayerProfileManager.Instance == null)
            return;

        ClearProfileCards();
        profileCardButtons.Clear();

        PlayerProfileManager.Instance.LoadProfiles(profiles =>
        {
            ClearProfileCards();
            profileCardButtons.Clear();
            loadedProfiles.Clear();
            loadedProfiles.AddRange(profiles);

            for (int i = 0; i < loadedProfiles.Count; i++)
                CreateProfileCard(loadedProfiles[i], i);

            UpdateProfileCardAlignment();

            if (waitingForFirebase)
                ShowStatusMessage("Sin conexion. Selecciona o crea un perfil en este dispositivo.");
            else
                RefreshActiveProfileDisplay();
            RefreshContinueButton();
        }, error =>
        {
            ShowStatusMessage(error);
            NarrationPlayer.Say("perfil_sin_conexion");
            RefreshContinueButton();
        });
    }

    private void UpdateProfileCardAlignment()
    {
        if (cardsContainer == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsContainer);
        float viewportWidth = profilesScrollRect != null && profilesScrollRect.viewport != null
            ? profilesScrollRect.viewport.rect.width : CardsViewportWidth;
        bool centered = cardsContainer.rect.width <= viewportWidth;
        Vector2 anchor = new Vector2(centered ? 0.5f : 0f, 0.5f);
        cardsContainer.anchorMin = anchor;
        cardsContainer.anchorMax = anchor;
        cardsContainer.pivot = anchor;
        cardsContainer.anchoredPosition = Vector2.zero;
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsContainer);

        if (profilesScrollRect != null)
        {
            profilesScrollRect.StopMovement();
            profilesScrollRect.horizontal = !centered;
            if (!centered)
                profilesScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void CreateProfileCard(PlayerProfileData profile, int index)
    {
        GameObject card = CreateCardInstance();
        if (card == null)
            return;

        card.name = profile.playerId;
        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = useProfileArtwork
                ? new Vector2(ArtworkCardWidth, rect.sizeDelta.y)
                : new Vector2(CardWidth, CardHeight);

        Image background = card.GetComponent<Image>();
        TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
        Image avatarImage = GetOrCreateAvatarImage(card.transform, new Vector2(72f, 72f), new Vector2(0f, 20f));

        if (avatarImage != null)
        {
            avatarImage.sprite = GetAvatarSprite(profile.avatar);
            avatarImage.color = avatarImage.sprite != null ? Color.white : GetFallbackAvatarColor(index);
        }

        if (label != null)
        {
            label.text = GetProfileDisplayName(profile.name);
            label.enableAutoSizing = true;
            if (!useProfileArtwork)
            {
                label.rectTransform.anchoredPosition = new Vector2(0f, -56f);
                label.rectTransform.sizeDelta = new Vector2(132f, 38f);
            }
            label.fontSizeMin = useProfileArtwork ? 12f : 20f;
            label.fontSizeMax = useProfileArtwork ? 22f : 25f;
            label.fontSize = useProfileArtwork ? 22f : 25f;
            label.color = DarkText;
            if (useProfileArtwork)
                label.margin = new Vector4(4f, 0f, 4f, 0f);
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        Button button = card.GetComponent<Button>();
        if (button == null)
            button = card.AddComponent<Button>();

        if (button.GetComponent<UIButtonPulse>() == null)
            button.gameObject.AddComponent<UIButtonPulse>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectProfile(profile));
        profileCardButtons.Add(button);

        ApplyCardVisualState(background, label, profile, index);
    }

    private GameObject CreateCardInstance()
    {
        if (cardsContainer == null || cardProfilePrefab == null)
            return null;

        return Instantiate(cardProfilePrefab, cardsContainer, false);
    }

    private void SelectProfile(PlayerProfileData profile)
    {
        if (PlayerProfileManager.Instance != null)
            PlayerProfileManager.Instance.SetActiveProfile(profile);

        RefreshActiveProfileDisplay();
        RepaintCards();
    }

    private void OnContinueClicked()
    {
        if (PlayerProfileManager.Instance == null || !PlayerProfileManager.Instance.HasActiveProfile)
        {
            ShowStatusMessage("Selecciona un perfil para continuar.");
            return;
        }

        if (SessionManager.Instance == null)
        {
            ShowStatusMessage("No se puede iniciar la sesion.");
            return;
        }

        SessionManager.Instance.StartSessionForPlayer(PlayerProfileManager.Instance.ActiveProfile, _ => Hide(), ShowStatusMessage);
    }

    private void OnNewProfileClicked()
    {
        EnsureCreatePopup();
        if (createPopupRoot == null)
            return;

        if (createNameInput != null)
            createNameInput.text = string.Empty;

        SelectAvatarIndex(0);
        if (selectionContent != null)
            selectionContent.SetActive(false);
        createPopupRoot.SetActive(true);
        NarrationPlayer.Say("perfil_crear");
    }

    private void CloseCreatePopup()
    {
        if (createPopupRoot != null)
            createPopupRoot.SetActive(false);
        if (selectionContent != null)
            selectionContent.SetActive(true);
    }

    private void ConfirmCreateProfile()
    {
        if (PlayerProfileManager.Instance == null)
            return;

        string playerName = createNameInput != null ? createNameInput.text : string.Empty;
        string avatarName = GetAvatarOptionName(selectedAvatarIndex);

        PlayerProfileManager.Instance.CreateProfile(playerName, avatarName, profile =>
        {
            CloseCreatePopup();

            NarrationPlayer.Say("perfil_listo");
            RefreshActiveProfileDisplay();
            LoadProfilesOrWaitForFirebase();
        }, ShowStatusMessage);
    }

    private void RefreshActiveProfileDisplay()
    {
        if (PlayerProfileManager.Instance != null && PlayerProfileManager.Instance.HasActiveProfile)
            ShowStatusMessage("Perfil activo: " + PlayerProfileManager.Instance.ActiveProfile.name);
        else if (!waitingForFirebase)
            ShowStatusMessage("Selecciona o crea un perfil.");

        RefreshContinueButton();
        RepaintCards();
    }

    private void RepaintCards()
    {
        if (cardsContainer == null)
            return;

        for (int i = 0; i < profileCardButtons.Count && i < loadedProfiles.Count; i++)
        {
            Transform child = profileCardButtons[i].transform;
            Image background = child.GetComponent<Image>();
            TMP_Text label = child.GetComponentInChildren<TMP_Text>(true);
            ApplyCardVisualState(background, label, loadedProfiles[i], i);
        }
    }

    private void ApplyCardVisualState(Image background, TMP_Text label, PlayerProfileData profile, int index)
    {
        if (background == null || profile == null)
            return;

        bool isActive = PlayerProfileManager.Instance != null && PlayerProfileManager.Instance.HasActiveProfile && PlayerProfileManager.Instance.ActiveProfile.playerId == profile.playerId;
        Color accent = GetAccentColor(index);
        if (useProfileArtwork)
        {
            background.color = Color.white;
            Outline selectedOutline = background.GetComponent<Outline>();
            if (selectedOutline == null && isActive)
                selectedOutline = background.gameObject.AddComponent<Outline>();
            if (selectedOutline != null)
            {
                selectedOutline.enabled = isActive;
                selectedOutline.effectColor = accent;
                selectedOutline.effectDistance = new Vector2(5f, -5f);
            }
            if (label != null)
            {
                label.color = DarkText;
                label.text = GetProfileDisplayName(profile.name);
            }
            return;
        }
        background.color = isActive ? Color.white : new Color(1f, 1f, 1f, 0.94f);

        Outline outline = background.GetComponent<Outline>();
        if (outline == null)
            outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = isActive ? accent : new Color(accent.r, accent.g, accent.b, 0.75f);
        outline.effectDistance = isActive ? new Vector2(8f, -8f) : new Vector2(5f, -5f);

        Shadow shadow = background.GetComponent<Shadow>();
        if (shadow == null)
            shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.45f, 0.36f, 0.24f, 0.16f);
        shadow.effectDistance = new Vector2(0f, -10f);

        if (label != null)
        {
            label.color = DarkText;
            label.text = GetProfileDisplayName(profile.name);
        }
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null)
            return;

        continueButton.interactable = PlayerProfileManager.Instance != null && PlayerProfileManager.Instance.HasActiveProfile;
    }

    private void RefreshButtonState(bool ready)
    {
        if (newPerfilButton != null)
            newPerfilButton.interactable = ready;

        if (updateButton != null)
            updateButton.interactable = ready;

        RefreshContinueButton();
    }

    private void HandleActiveProfileChanged(PlayerProfileData profile)
    {
        RefreshActiveProfileDisplay();
    }

    private void HandleFirebaseReady()
    {
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.Ready -= HandleFirebaseReady;

        if (panelRoot != null && panelRoot.activeSelf)
            LoadProfilesOrWaitForFirebase();
    }

    private void ClearProfileCards()
    {
        if (cardsContainer == null)
            return;

        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
        {
            GameObject card = cardsContainer.GetChild(i).gameObject;
            card.SetActive(false); // No recibe clics durante la destrucción diferida de Unity.
            card.transform.SetParent(null, false);
            Destroy(card);
        }
    }

    private void ShowStatusMessage(string message)
    {
        if (statusText != null)
            statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
    }

    private static string GetProfileDisplayName(string profileName)
    {
        return profileName?.Trim() ?? string.Empty;
    }

    private Sprite GetAvatarSprite(string avatarName)
    {
        string[] avatarOptions = PlayerProfileManager.Instance != null ? PlayerProfileManager.Instance.GetAvatarOptions() : null;
        if (avatarOptions == null || avatarOptions.Length == 0)
            return GetAvatarSpriteByIndex(0);

        for (int i = 0; i < avatarOptions.Length; i++)
        {
            if (avatarOptions[i] == avatarName)
                return GetAvatarSpriteByIndex(i);
        }

        return GetAvatarSpriteByIndex(0);
    }

    private Sprite GetAvatarSpriteByIndex(int index)
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
            return null;

        return avatarSprites[Mathf.Clamp(index, 0, avatarSprites.Length - 1)];
    }

    private string GetAvatarOptionName(int index)
    {
        string[] avatarOptions = PlayerProfileManager.Instance != null ? PlayerProfileManager.Instance.GetAvatarOptions() : null;
        if (avatarOptions == null || avatarOptions.Length == 0)
            return defaultAvatar;

        return avatarOptions[Mathf.Clamp(index, 0, avatarOptions.Length - 1)];
    }

    private string GetAvatarDisplayName(int index)
    {
        // Los identificadores guardados pertenecen a los avatares antiguos;
        // el texto visible debe describir los dibujos actuales sin migrar perfiles.
        switch (index)
        {
            case 0: return "Oso naranja";
            case 1: return "Conejo azul";
            default: return "Estrella amarilla";
        }
    }

    private static string GetAvatarShortName(int index)
    {
        switch (index)
        {
            case 0: return "A1";
            case 1: return "A2";
            default: return "A3";
        }
    }

    private static Color GetFallbackAvatarColor(int index)
    {
        switch (index % 3)
        {
            case 0: return new Color(1f, 0.76f, 0.83f, 1f);
            case 1: return new Color(0.76f, 0.92f, 0.9f, 1f);
            default: return new Color(1f, 0.88f, 0.54f, 1f);
        }
    }

    private static Color GetAccentColor(int index)
    {
        switch (index % 4)
        {
            case 0: return new Color(1f, 0.45f, 0.67f, 1f);
            case 1: return new Color(0.28f, 0.82f, 0.79f, 1f);
            case 2: return new Color(1f, 0.86f, 0.28f, 1f);
            default: return new Color(0.68f, 0.54f, 1f, 1f);
        }
    }

    private static Button FindButtonByName(Transform root, string objectName)
    {
        Transform child = FindChild(root, objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static TMP_Text FindTextByName(Transform root, string objectName)
    {
        Transform child = FindChild(root, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChild(root.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static TMP_Text CreateLabel(string value, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(fontSize, 16f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static TMP_InputField CreateInputField(Transform parent, Vector2 anchoredPosition, Vector2 size, string placeholder)
    {
        GameObject root = new GameObject("NameInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        root.GetComponent<Image>().color = Color.white;
        TMP_InputField input = root.GetComponent<TMP_InputField>();

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.SetParent(rect, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(18f, 10f);
        viewportRect.offsetMax = new Vector2(-18f, -10f);

        TMP_Text text = CreateLabel(string.Empty, viewportRect, Vector2.zero, Vector2.zero, 26f, FontStyles.Normal, new Color(0.14f, 0.14f, 0.22f, 1f), TextAlignmentOptions.Left);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        TMP_Text placeholderText = CreateLabel(placeholder, viewportRect, Vector2.zero, Vector2.zero, 24f, FontStyles.Italic, new Color(0.52f, 0.52f, 0.6f, 1f), TextAlignmentOptions.Left);
        placeholderText.rectTransform.anchorMin = Vector2.zero;
        placeholderText.rectTransform.anchorMax = Vector2.one;
        placeholderText.rectTransform.offsetMin = Vector2.zero;
        placeholderText.rectTransform.offsetMax = Vector2.zero;

        input.textViewport = viewportRect;
        input.textComponent = text as TextMeshProUGUI;
        input.placeholder = placeholderText;
        return input;
    }

    private static Button CreateActionButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject root = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        root.GetComponent<Image>().color = color;
        TMP_Text buttonText = CreateLabel(label, root.transform, Vector2.zero, size - new Vector2(16f, 12f), 26f, FontStyles.Bold, DarkText, TextAlignmentOptions.Center);
            buttonText.fontSizeMin = 22f;
        buttonText.raycastTarget = false;
        return root.GetComponent<Button>();
    }

    private static Button CreateIconButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject root = new GameObject("AvatarButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        root.GetComponent<Image>().color = new Color(0.95f, 0.94f, 0.9f, 1f);
        return root.GetComponent<Button>();
    }

    private static Image GetOrCreateAvatarImage(Transform parent, Vector2 size, Vector2 anchoredPosition)
    {
        Transform child = FindChild(parent, "AvatarImage");
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image != null)
            return image;

        GameObject obj = new GameObject("AvatarImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        image = obj.GetComponent<Image>();
        image.preserveAspect = true;
        return image;
    }
}
