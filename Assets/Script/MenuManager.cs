using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class MenuManager : MonoBehaviour
{
    private enum MenuPanelState
    {
        MainOptions,
        Games,
        Settings,
    }

    [Header("Animated UI")]
    [Tooltip("Assign the main menu logo RectTransform here. If left empty, the script will try to find an object named 'Logo'.")]
    [SerializeField] private RectTransform logoRect;
    [Tooltip("Assign the Play button RectTransform here. If left empty, the script will try to find an object named 'Play'.")]
    [SerializeField] private RectTransform playButtonRect;
    [Tooltip("Assign the Settings button RectTransform here. If left empty, the script will try to find an object named 'Settings'.")]
    [SerializeField] private RectTransform settingsButtonRect;
    [Tooltip("Assign the Exit button RectTransform here. If left empty, the script will try to find an object named 'Exit'.")]
    [SerializeField] private RectTransform exitButtonRect;

    [Header("Idle Animation")]
    [SerializeField] private bool enableIdleAnimation = true;
    [SerializeField] private float logoFloatAmount = 8f;
    [SerializeField] private float logoFloatSpeed = 0.8f;
    [SerializeField] private float logoWobbleAngle = 2f;
    [SerializeField] private float logoWobbleSpeed = 0.7f;

    [Header("Panels")]
    [SerializeField] private GameObject mainOptionsPanel;
    [SerializeField] private GameObject gamesPanel;
    [SerializeField] private Sprite bandGameLogo;
    [SerializeField] private GameObject settingsPanel;

    [Header("Optional")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button perfilButton;

    [Header("Settings Box")]
    [SerializeField] private GameObject settingsBox;
    [Tooltip("Sprites 0-11 de 'assets menu de configuraciones', en orden numérico.")]
    [SerializeField] private Sprite[] settingsArtwork;
    [SerializeField] private Scrollbar musicScrollbar;
    [SerializeField] private Scrollbar effectsScrollbar;
    [SerializeField] private Scrollbar speedScrollbar;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private Button resetSettingsButton;

    [Header("Panel Transition Juice")]
    [SerializeField] private float panelAccentDuration = 0.22f;
    [SerializeField] private float logoAccentScale = 1.08f;
    [SerializeField] private float logoShiftAmount = 34f;

    private Vector2 logoBasePosition;
    private Quaternion logoBaseRotation;
    private Vector3 logoBaseScale = Vector3.one;
    private UIPanelTransition mainOptionsTransition;
    private UIPanelTransition gamesTransition;
    private UIPanelTransition settingsTransition;
    private Coroutine panelSwitchRoutine;
    private Coroutine menuAccentRoutine;
    private Vector2 logoTransitionOffset = Vector2.zero;
    private float logoTransitionRotation;
    private float logoTransitionScale = 1f;
    private TMP_Text musicSettingsText;
    private TMP_Text effectsSettingsText;
    private TMP_Text speedSettingsText;
    private Button hapticsButton;
    private Button muteButton;
    private Image vibrationSwitchImage;
    private Image muteSwitchImage;

    private void Awake()
    {
        EnsureCoreServices();

        if (gameObject.scene.name != "MainScene")
            return;

        LandscapePhoneCanvas.Configure(GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>());
        ResolveReferences();
        ConfigureBandGameCard();
        ResolvePanelTransitions();
        CacheBaseTransforms();
        ConfigureSettingsBox();
        InitializePanels();

        ProfileMenuController profileMenu = FindFirstObjectByType<ProfileMenuController>(FindObjectsInactive.Include);
        if (profileMenu == null)
            gameObject.AddComponent<ProfileMenuController>();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ShowMainOptions);
        }

        if (perfilButton != null)
        {
            perfilButton.onClick.RemoveAllListeners();
            perfilButton.onClick.AddListener(ShowProfileMenu);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(ShowMainOptions);
        }

        if (resetSettingsButton != null)
        {
            resetSettingsButton.onClick.RemoveAllListeners();
            resetSettingsButton.onClick.AddListener(ResetSettingsToDefaults);
        }

        GameSettings.SettingsChanged += RefreshSettingsBoxLabels;
        RefreshSettingsBoxLabels();
    }

    private void OnDestroy()
    {
        GameSettings.SettingsChanged -= RefreshSettingsBoxLabels;
    }

    private void Update()
    {
        UpdatePhoneMenuLayout();
        if (!enableIdleAnimation)
            return;

        float time = Time.unscaledTime;
        AnimateLogo(time);
    }

    private void UpdatePhoneMenuLayout()
    {
        if (perfilButton != null)
        {
            RectTransform profileRect = perfilButton.transform as RectTransform;
            if (profileRect != null)
                profileRect.anchoredPosition = LandscapePhoneCanvas.IsWidePhone
                    ? new Vector2(500f, 260f) : new Vector2(550f, 330f);
        }
        if (backButton != null)
        {
            RectTransform backRect = backButton.transform as RectTransform;
            if (backRect != null)
                backRect.anchoredPosition = LandscapePhoneCanvas.IsWidePhone
                    ? new Vector2(190f, -84f) : new Vector2(149f, -84f);
        }
    }

    public void LoadScene(string sceneName)
    {
        if (PlayerProfileManager.Instance == null || !PlayerProfileManager.Instance.HasActiveProfile)
        {
            ProfileMenuController profileMenu = FindFirstObjectByType<ProfileMenuController>(FindObjectsInactive.Include);
            if (profileMenu != null)
                profileMenu.Show();
            return;
        }

        if (SessionManager.Instance != null && !SessionManager.Instance.HasActiveSession)
        {
            SessionManager.Instance.StartSessionForPlayer(PlayerProfileManager.Instance.ActiveProfile, null, null);
        }

        if (sceneName == "SimonScene") NarrationPlayer.Say("menu_simon");
        else if (sceneName == "BasicrhythmScene") NarrationPlayer.Say("menu_ritmo_basico");
        else if (sceneName == "TempoTapScene") NarrationPlayer.Say("menu_tempo_tap");
        SceneTransitionController.LoadScene(sceneName);
    }

    private void ConfigureBandGameCard()
    {
        if (gamesPanel == null) return;
        Transform panel = gamesPanel.transform;
        Transform basicRhythm = panel.Find("BasicrhythmScene");
        if (basicRhythm != null)
            basicRhythm.gameObject.SetActive(false);

        string[] names = { "SimonScene", "TempoTapScene" };
        Vector2[] positions = { new Vector2(-400f, -70f), new Vector2(0f, -70f) };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform card = panel.Find(names[i]) as RectTransform;
            if (card == null) return; // Never disturb a different menu layout.
            card.anchoredPosition = positions[i];
            card.sizeDelta = new Vector2(300f, 300f);
        }
        RectTransform rect = panel.Find("BandaMusicalScene") as RectTransform;
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(400f, -70f);
        rect.sizeDelta = new Vector2(300f, 300f);
        UnityEngine.UI.Image image = rect.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.sprite = bandGameLogo;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        Button button = rect.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => LoadScene("BandaMusical"));
            if (button.GetComponent<UIButtonPulse>() == null)
                button.gameObject.AddComponent<UIButtonPulse>();
        }
    }

    public void ShowMainOptions()
    {
        NarrationPlayer.Say("menu_volver");
        ShowPanel(mainOptionsPanel, mainOptionsTransition, gamesPanel, gamesTransition, settingsPanel, settingsTransition);
        ApplyMenuState(MenuPanelState.MainOptions);
    }

    public void ShowGames()
    {
        NarrationPlayer.Say("menu_elegir_juego");
        ShowPanel(gamesPanel, gamesTransition, mainOptionsPanel, mainOptionsTransition, settingsPanel, settingsTransition);
        ApplyMenuState(MenuPanelState.Games);
    }

    public void ShowSettings()
    {
        NarrationPlayer.Say("menu_ajustes");
        ShowPanel(settingsPanel, settingsTransition, mainOptionsPanel, mainOptionsTransition, gamesPanel, gamesTransition);
        ApplyMenuState(MenuPanelState.Settings);
    }

    private void ApplyMenuState(MenuPanelState state)
    {
        if (logoRect != null)
            logoRect.gameObject.SetActive(state != MenuPanelState.Settings);

        SetBackButtonVisible(state != MenuPanelState.MainOptions);
        StartMenuAccent(state);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Salir (en el editor no se cierra, solo en el .exe)");
    }

    public void ShowProfileMenu()
    {
        ProfileMenuController profileMenu = FindFirstObjectByType<ProfileMenuController>(FindObjectsInactive.Include);
        if (profileMenu != null)
            profileMenu.Show();
    }

    private void ResolveReferences()
    {
        if (logoRect == null)
            logoRect = FindRectTransformInScene("Logo");

        if (playButtonRect == null)
            playButtonRect = FindRectTransformInScene("Play");

        if (settingsButtonRect == null)
            settingsButtonRect = FindRectTransformInScene("Settings");

        if (exitButtonRect == null)
            exitButtonRect = FindRectTransformInScene("Exit");

        if (mainOptionsPanel == null)
            mainOptionsPanel = GameObject.Find("MainOptionsPanel");

        if (gamesPanel == null)
            gamesPanel = GameObject.Find("GamesPanel");

        if (settingsPanel == null)
            settingsPanel = GameObject.Find("SettingsPanel");

        if (settingsBox == null)
            settingsBox = GameObject.Find("SettingsBox");

        if (musicScrollbar == null && settingsBox != null)
            musicScrollbar = FindScrollbar(settingsBox.transform, "MusicaButton");

        if (effectsScrollbar == null && settingsBox != null)
            effectsScrollbar = FindScrollbar(settingsBox.transform, "EfectosButton");

        if (speedScrollbar == null && settingsBox != null)
            speedScrollbar = FindScrollbar(settingsBox.transform, "VelocidadButton");

        if (closeSettingsButton == null && settingsBox != null)
        {
            Transform closeTransform = FindChild(settingsBox.transform, "CloseButton");
            if (closeTransform != null)
                closeSettingsButton = closeTransform.GetComponent<Button>();
        }

        if (resetSettingsButton == null && settingsBox != null)
        {
            Transform resetTransform = FindChild(settingsBox.transform, "ResetButton");
            if (resetTransform != null)
                resetSettingsButton = resetTransform.GetComponent<Button>();
        }

        if (musicSettingsText == null && settingsBox != null)
            musicSettingsText = FindButtonLabel(settingsBox.transform, "MusicaButton");

        if (effectsSettingsText == null && settingsBox != null)
            effectsSettingsText = FindButtonLabel(settingsBox.transform, "EfectosButton");

        if (speedSettingsText == null && settingsBox != null)
            speedSettingsText = FindButtonLabel(settingsBox.transform, "VelocidadButton");

        if (backButton == null)
        {
            GameObject backButtonObject = GameObject.Find("BackButton");
            if (backButtonObject != null)
                backButton = backButtonObject.GetComponent<Button>();
        }

        if (perfilButton == null)
        {
            GameObject perfilButtonObject = GameObject.Find("PerfilButton");
            if (perfilButtonObject != null)
                perfilButton = perfilButtonObject.GetComponent<Button>();
        }
    }

    private void SetPanelState(GameObject panel, bool visible)
    {
        if (panel != null)
            panel.SetActive(visible);
    }

    private void InitializePanels()
    {
        SetPanelInstant(mainOptionsPanel, mainOptionsTransition, true);
        SetPanelInstant(gamesPanel, gamesTransition, false);
        SetPanelInstant(settingsPanel, settingsTransition, false);
        if (logoRect != null)
            logoRect.gameObject.SetActive(true);
        SetBackButtonVisible(false);
    }

    private void ConfigureSettingsBox()
    {
        GameSettings.EnsureInitialized();
        ConfigureHapticsControl();
        ConfigureMuteControl();

        if (musicScrollbar != null)
        {
            musicScrollbar.onValueChanged.RemoveAllListeners();
            musicScrollbar.size = 0.2f;
            musicScrollbar.value = GameSettings.MusicVolume;
            musicScrollbar.onValueChanged.AddListener(OnMusicScrollbarChanged);
        }

        if (effectsScrollbar != null)
        {
            effectsScrollbar.onValueChanged.RemoveAllListeners();
            effectsScrollbar.size = 0.2f;
            effectsScrollbar.value = GameSettings.EffectsVolume;
            effectsScrollbar.onValueChanged.AddListener(OnEffectsScrollbarChanged);
        }

        if (speedScrollbar != null)
        {
            speedScrollbar.onValueChanged.RemoveAllListeners();
            speedScrollbar.size = 0.2f;
            speedScrollbar.value = Mathf.InverseLerp(0.75f, 1.5f, GameSettings.GameplaySpeed);
            speedScrollbar.onValueChanged.AddListener(OnSpeedScrollbarChanged);
        }

        RefreshSettingsBoxLabels();
    }

    private void ConfigureHapticsControl()
    {
        if (settingsBox == null)
            return;

        Transform sceneButton = FindChild(settingsBox.transform, "VibrationButton");
        if (sceneButton != null)
        {
            hapticsButton = sceneButton.GetComponent<Button>();
            vibrationSwitchImage = sceneButton.Find("Switch")?.GetComponent<Image>();
            if (hapticsButton != null)
            {
                hapticsButton.onClick.RemoveListener(OnHapticsButtonClicked);
                hapticsButton.onClick.AddListener(OnHapticsButtonClicked);
            }
        }
    }

    private void ConfigureMuteControl()
    {
        if (settingsBox == null) return;
        Transform sceneButton = FindChild(settingsBox.transform, "SilenButton");
        if (sceneButton == null) return;
        muteButton = sceneButton.GetComponent<Button>();
        muteSwitchImage = sceneButton.Find("Switch")?.GetComponent<Image>();
        if (muteButton != null)
        {
            muteButton.onClick.RemoveListener(OnMuteButtonClicked);
            muteButton.onClick.AddListener(OnMuteButtonClicked);
        }
    }

    private void OnMuteButtonClicked()
    {
        GameSettings.SetMuted(!GameSettings.Muted);
    }

    private void OnHapticsButtonClicked()
    {
        GameSettings.SetHapticsEnabled(!GameSettings.HapticsEnabled);
        if (GameSettings.HapticsEnabled)
            AccessibleHaptics.Success();
    }

    private void OnMusicScrollbarChanged(float value)
    {
        GameSettings.SetMusicVolume(value);
    }

    private void OnEffectsScrollbarChanged(float value)
    {
        GameSettings.SetEffectsVolume(value);
    }

    private void OnSpeedScrollbarChanged(float value)
    {
        float mapped = Mathf.Lerp(0.75f, 1.5f, value);
        GameSettings.SetGameplaySpeed(mapped);
    }

    private void ResetSettingsToDefaults()
    {
        GameSettings.ResetDefaults();

        if (musicScrollbar != null)
            musicScrollbar.SetValueWithoutNotify(GameSettings.MusicVolume);

        if (effectsScrollbar != null)
            effectsScrollbar.SetValueWithoutNotify(GameSettings.EffectsVolume);

        if (speedScrollbar != null)
            speedScrollbar.SetValueWithoutNotify(Mathf.InverseLerp(0.75f, 1.5f, GameSettings.GameplaySpeed));

        RefreshSettingsBoxLabels();
    }

    private void RefreshSettingsBoxLabels()
    {
        if (musicSettingsText != null)
            musicSettingsText.text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%";

        if (effectsSettingsText != null)
            effectsSettingsText.text = $"{Mathf.RoundToInt(GameSettings.EffectsVolume * 100f)}%";

        if (speedSettingsText != null)
            speedSettingsText.text = $"{Mathf.RoundToInt(GameSettings.GameplaySpeed * 100f)}%";

        if (settingsArtwork != null && settingsArtwork.Length > 11)
        {
            if (vibrationSwitchImage != null)
                vibrationSwitchImage.sprite = settingsArtwork[GameSettings.HapticsEnabled ? 11 : 10];
            if (muteSwitchImage != null)
                muteSwitchImage.sprite = settingsArtwork[GameSettings.Muted ? 11 : 10];
        }
    }

    private void ResolvePanelTransitions()
    {
        mainOptionsTransition = mainOptionsPanel != null ? mainOptionsPanel.GetComponent<UIPanelTransition>() : null;
        gamesTransition = gamesPanel != null ? gamesPanel.GetComponent<UIPanelTransition>() : null;
        settingsTransition = settingsPanel != null ? settingsPanel.GetComponent<UIPanelTransition>() : null;
    }

    private void ShowPanel(GameObject targetPanel, UIPanelTransition targetTransition, GameObject panelToHideA, UIPanelTransition hideTransitionA, GameObject panelToHideB, UIPanelTransition hideTransitionB)
    {
        if (panelSwitchRoutine != null)
            StopCoroutine(panelSwitchRoutine);

        panelSwitchRoutine = StartCoroutine(SwitchPanelsRoutine(targetPanel, targetTransition, panelToHideA, hideTransitionA, panelToHideB, hideTransitionB));
    }

    private IEnumerator SwitchPanelsRoutine(GameObject targetPanel, UIPanelTransition targetTransition, GameObject panelToHideA, UIPanelTransition hideTransitionA, GameObject panelToHideB, UIPanelTransition hideTransitionB)
    {
        HidePanel(panelToHideA, hideTransitionA);
        HidePanel(panelToHideB, hideTransitionB);

        yield return null;

        ShowPanelInternal(targetPanel, targetTransition);
        panelSwitchRoutine = null;
    }

    private void ShowPanelInternal(GameObject panel, UIPanelTransition transition)
    {
        if (panel == null)
            return;

        if (transition != null)
        {
            panel.SetActive(true);
            transition.PlayShow();
        }
        else
        {
            SetPanelState(panel, true);
        }
    }

    private void HidePanel(GameObject panel, UIPanelTransition transition)
    {
        if (panel == null)
            return;

        // Un panel ya oculto no puede iniciar una corrutina de salida.
        if (!panel.activeInHierarchy)
        {
            if (transition != null)
                transition.SetHiddenInstant();
            return;
        }

        if (transition != null)
        {
            transition.PlayHide(() =>
            {
                if (panel != null)
                    panel.SetActive(false);
            });
        }
        else
        {
            SetPanelState(panel, false);
        }
    }

    private void SetPanelInstant(GameObject panel, UIPanelTransition transition, bool visible)
    {
        if (panel == null)
            return;

        panel.SetActive(visible);
        if (transition != null)
        {
            if (visible)
                transition.SetShownInstant();
            else
                transition.SetHiddenInstant();
        }
    }

    private void CacheBaseTransforms()
    {
        if (logoRect != null)
        {
            logoBasePosition = logoRect.anchoredPosition;
            logoBaseRotation = logoRect.localRotation;
            logoBaseScale = logoRect.localScale;
        }

    }

    private void AnimateLogo(float time)
    {
        if (logoRect == null)
            return;

        float floatOffset = Mathf.Sin(time * logoFloatSpeed) * logoFloatAmount;
        float wobbleOffset = Mathf.Sin(time * logoWobbleSpeed) * logoWobbleAngle;

        logoRect.anchoredPosition = logoBasePosition + logoTransitionOffset + new Vector2(0f, floatOffset);
        logoRect.localRotation = logoBaseRotation * Quaternion.Euler(0f, 0f, wobbleOffset + logoTransitionRotation);
        float phoneGameLogoScale = LandscapePhoneCanvas.IsWidePhone && gamesPanel != null && gamesPanel.activeSelf
            ? 0.72f : 1f;
        logoRect.localScale = logoBaseScale * logoTransitionScale * phoneGameLogoScale;
    }

    private void StartMenuAccent(MenuPanelState panelState)
    {
        if (menuAccentRoutine != null)
            StopCoroutine(menuAccentRoutine);

        menuAccentRoutine = StartCoroutine(AnimateMenuAccent(panelState));
    }

    private IEnumerator AnimateMenuAccent(MenuPanelState panelState)
    {
        Vector2 targetOffset = panelState switch
        {
            MenuPanelState.Games => new Vector2(-logoShiftAmount, 0f),
            MenuPanelState.Settings => new Vector2(0f, -logoShiftAmount * 0.45f),
            _ => Vector2.zero,
        };

        float targetRotation = panelState switch
        {
            MenuPanelState.Games => -3.5f,
            MenuPanelState.Settings => 2.5f,
            _ => 0f,
        };

        float targetLogoScale = panelState == MenuPanelState.MainOptions ? logoAccentScale : 1.02f;
        yield return StartCoroutine(AnimateMenuAccentPhase(targetOffset, targetRotation, targetLogoScale));

        yield return StartCoroutine(AnimateMenuAccentPhase(targetOffset * 0.2f, targetRotation * 0.2f, 1f));
        menuAccentRoutine = null;
    }

    private IEnumerator AnimateMenuAccentPhase(Vector2 targetOffset, float targetRotation, float targetLogoScale)
    {
        Vector2 startOffset = logoTransitionOffset;
        float startRotation = logoTransitionRotation;
        float startLogoScale = logoTransitionScale;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, panelAccentDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            logoTransitionOffset = Vector2.Lerp(startOffset, targetOffset, k);
            logoTransitionRotation = Mathf.Lerp(startRotation, targetRotation, k);
            logoTransitionScale = Mathf.Lerp(startLogoScale, targetLogoScale, k);
            yield return null;
        }

        logoTransitionOffset = targetOffset;
        logoTransitionRotation = targetRotation;
        logoTransitionScale = targetLogoScale;
    }

    private static RectTransform FindRectTransformInScene(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    private static Scrollbar FindScrollbar(Transform root, string parentName)
    {
        Transform parent = FindChild(root, parentName);
        if (parent == null)
            return null;

        return parent.GetComponentInChildren<Scrollbar>(true);
    }

    private static TMP_Text FindButtonLabel(Transform root, string parentName)
    {
        Transform parent = FindChild(root, parentName);
        if (parent == null)
            return null;

        return parent.GetComponentInChildren<TMP_Text>(true);
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void SetBackButtonVisible(bool visible)
    {
        if (backButton != null)
            backButton.gameObject.SetActive(visible);
    }

    private void EnsureCoreServices()
    {
        EnsureSingleton<FirebaseManager>("FirebaseManager");
        EnsureSingleton<PlayerProfileManager>("PlayerProfileManager");
        EnsureSingleton<SessionManager>("SessionManager");
        EnsureSingleton<GameReportManager>("GameReportManager");
        EnsureSingleton<ReportOutbox>("ReportOutbox");
    }

    private static T EnsureSingleton<T>(string objectName) where T : Component
    {
        T existing = FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject root = new GameObject(objectName);
        return root.AddComponent<T>();
    }
}
