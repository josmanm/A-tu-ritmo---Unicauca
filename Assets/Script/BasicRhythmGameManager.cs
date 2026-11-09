using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BasicRhythmGameManager : MonoBehaviour
{
    private const int TapChunkSize = 256;
    private enum FigureType
    {
        Redonda,
        Blanca,
        Negra,
        Corchea,
        Silencio,
    }

    [System.Serializable]
    private class FigureDefinition
    {
        public FigureType type;
        public string displayName;
        public float noteBeats;
        public Sprite sprite;
        public Sprite[] orderedSprites;
    }

    [Header("Figuras")]
    [SerializeField] private Sprite redondaSprite;
    [SerializeField] private Sprite blancaSprite;
    [SerializeField] private Sprite blancaASprite;
    [SerializeField] private Sprite blancaBSprite;
    [SerializeField] private Sprite negraSprite;
    [SerializeField] private Sprite corcheaSprite;
    [SerializeField] private Sprite corcheaMaSprite;
    [SerializeField] private Sprite corcheaRiSprite;
    [SerializeField] private Sprite corcheaPoSprite;
    [SerializeField] private Sprite corcheaSaSprite;
    [SerializeField] private Sprite silencioSprite;
    [SerializeField] private float silencioBeats = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip metronomeClip;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip failClip;

    [Header("Audio Tutorial")]
    [SerializeField] private AudioClip tutorialIntroClip;
    [SerializeField] private AudioClip redondaVoiceClip;
    [SerializeField] private AudioClip blancaVoiceClip;
    [SerializeField] private AudioClip negraVoiceClip;
    [SerializeField] private AudioClip corcheaVoiceClip;
    [SerializeField] private AudioClip silencioVoiceClip;
    [SerializeField] private AudioClip drumDiscoveryVoiceClip;
    [SerializeField] private AudioClip fillTimingVoiceClip;
    [SerializeField] private AudioClip endVoiceClip;

    [Header("Audio Juego")]
    [SerializeField] private AudioClip yourTurnVoiceClip;
    [SerializeField] private AudioClip veryGoodVoiceClip;
    [SerializeField] private AudioClip waitALittleVoiceClip;
    [SerializeField] private AudioClip observeVoiceClip;
    [SerializeField] private AudioClip tryAgainVoiceClip;

    [Header("Juego")]
    [SerializeField] private float bpm = 40f;
    [SerializeField] private float totalPatternBeats = 4f;
    [SerializeField] private int patternMeasures = 4;
    [SerializeField] [Range(0.4f, 1f)] private float longPatternRequiredHitRatio = 0.55f;
    [SerializeField] private float roundIntroDelay = 0.6f;
    [SerializeField] private float roundOutroDelay = 1f;
    [SerializeField] private float perfectToleranceBeats = 0.12f;
    [SerializeField] private float goodToleranceBeats = 0.25f;
    [SerializeField] private int maxLives = 3;
    [SerializeField] private int maxConsecutiveFigureRepeats = 2;
    [SerializeField] private float figureRecencyWeightStep = 0.75f;

    [Header("Feedback")]
    [SerializeField] private Color normalFeedbackColor = Color.white;
    [SerializeField] private Color successFeedbackColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color failFeedbackColor = new Color(1f, 0.75f, 0.3f, 1f);
    [SerializeField] private Color timingCueColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color noteLitColor = Color.white;
    [SerializeField] private Color noteDimColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color longNoteFillColor = new Color(0.35f, 0.8f, 1f, 0.75f);
    [SerializeField] private Color silenceProgressColor = new Color(0.62f, 0.38f, 0.87f, 1f);
    [SerializeField] private Color failureProgressColor = new Color(0.93f, 0.2f, 0.24f, 1f);
    [SerializeField] private float iconSpacing = 16f;
    [SerializeField] private float drumPulseScale = 1.08f;
    [SerializeField] private float drumPulseDuration = 0.12f;

    [Header("UI Juice")]
    [SerializeField] private float hudPopScale = 1.08f;
    [SerializeField] private float hudAnimDuration = 0.12f;
    [SerializeField] private float drumReadyPulseScale = 1.03f;
    [SerializeField] private float drumReadyPulseSpeed = 4.6f;
    [SerializeField] private int focusedPatternThreshold = 5;
    [SerializeField] private float focusedPatternIconSize = 240f;
    [SerializeField] private Vector2 focusedPatternPosition = new Vector2(0f, 40f);
    [SerializeField] private Vector2 focusedPatternAreaSize = new Vector2(760f, 360f);
    [SerializeField] private float focusedBlockSlideDistance = 90f;
    [SerializeField] private float focusedBlockTransitionDuration = 0.18f;
    [SerializeField] private float activeFillMoveDistance = 10f;
    [SerializeField] private float activeFillMoveSpeed = 4.5f;
    [SerializeField] private float statusMessageDuration = 0.45f;
    [SerializeField] private float statusMessageLongDuration = 0.65f;
    [SerializeField] private float statusMessagePopScale = 1.08f;
    [SerializeField] private float statusMessageAnimDuration = 0.16f;
    [SerializeField] private Sprite successStatusSprite;
    [SerializeField] private OutcomeFeedbackView outcomeFeedback;
    [SerializeField] private Color roundCelebrationColor = new Color(1f, 0.94f, 0.72f, 0.92f);
    [SerializeField] private Color perfectRoundCelebrationColor = new Color(1f, 0.98f, 0.82f, 0.95f);
    [SerializeField] private Color tutorialCompletionCelebrationColor = new Color(0.78f, 0.95f, 1f, 0.95f);
    [SerializeField] private float roundCelebrationDuration = 0.42f;

    private readonly List<FigureDefinition> figures = new List<FigureDefinition>();
    private readonly List<Image> patternIcons = new List<Image>();
    private readonly List<FigureDefinition> currentPattern = new List<FigureDefinition>();
    private readonly List<int> currentPatternBlockNoteCounts = new List<int>();

    private TopHudPanelView topHudPanelView;
    private TMP_Text scoreText;
    private TMP_Text levelText;
    private Image figureIconTemplate;
    private RectTransform rhythmPanel;
    private Button drumButton;
    [SerializeField] private Button startButton;
    private Button menuButton;
    private GameObject pausePanel;
    private Button resumeButton;
    private Button restartButton;
    private Button closeButton;
    private Button pauseMenuButton;
    private Image[] lifeImages;
    private AudioSource sfxSource;
    private AudioSource voiceSource;
    private Image drumButtonImage;
    private Color drumButtonBaseColor = Color.white;
    private PausePanelView pausePanelView;
    private float sfxBaseVolume = 1f;
    private float voiceBaseVolume = 1f;
    private Canvas rootCanvas;
    private TMP_Text statusOverlayText;
    private CanvasGroup statusOverlayGroup;
    private RectTransform statusOverlayRect;
    private Image statusOverlayIconImage;
    private RectTransform scorePanelRect;
    private RectTransform levelPanelRect;
    private RectTransform livesPanelRect;
    private RectTransform noteProgressRoot;
    private RectTransform noteProgressTrack;
    private Image noteProgressFill;
    private Image noteProgressTrackImage;
    private Image tutorialTapHandImage;
    private Image progressTapHandImage;
    private Texture2D tutorialHandTexture;
    private Sprite tutorialHandSprite;
    private readonly Image[] beatDividers = new Image[3];
    private Texture2D noteProgressTexture;
    private Sprite noteProgressSprite;
    private RectTransform drumRect;
    private Vector2 originalDrumPosition;
    private Vector2 lastProgressCanvasSize;
    private int progressNoteIndex = -1;
    private Vector3 scorePanelBaseScale = Vector3.one;
    private Vector3 levelPanelBaseScale = Vector3.one;
    private Vector3 livesPanelBaseScale = Vector3.one;

    private Coroutine roundRoutine;
    private Coroutine drumPulseRoutine;
    private Coroutine drumReadyRoutine;
    private Coroutine statusOverlayRoutine;
    private Coroutine scoreHudRoutine;
    private Coroutine levelHudRoutine;
    private Coroutine livesHudRoutine;
    private Coroutine focusedBlockTransitionRoutine;
    private Coroutine roundCelebrationRoutine;
    private Coroutine failureProgressRoutine;
    private bool failureProgressActive;
    private bool waitingForInput;
    private double inputPhaseStartDspTime;
    private double pausedAtDspTime;
    private double backgroundAtDspTime;
    private int score;
    private int level;
    private int lives;
    private int currentNoteIndex;
    private int noteCountInPattern;
    private bool roundUsedOnlyPerfectTiming;
    private int roundSuccessfulHits;
    private int roundFailures;
    private int assistedAttempts;
    private int recoverySuccessStreak;
    private float initialBpm;
    private int initialPatternMeasures;
    private bool roundCompletedSuccessfully;
    private bool attemptFinished;
    private bool isPaused;
    private bool gameFinished;
    private TMP_Text gameSummaryText;
    private bool tutorialActive = true;
    private bool introPlayedByOverlay;
    private int tutorialStepIndex;
    private bool tutorialDemoShownForCurrentStep;
    private bool tutorialWarmupCompleted;
    private int tutorialDrumWarmupRemaining;
    private int tutorialFillWarmupRemaining;
    private bool tutorialFillWarmupWaitingForTap;
    private bool tutorialFillWarmupAttemptCompleted;
    private int currentNoteTapCount;
    private int sustainedBeatCuesPlayed;
    private int lastShownScore = -1;
    private int lastShownLevel = -999;
    private int lastShownLives = -1;
    private bool useFocusedPatternPresentation;
    private readonly List<int> patternBlockIndexByNote = new List<int>();
    private readonly List<Vector2> focusedBasePositions = new List<Vector2>();
    private int lastFocusedBlockIndex = -1;
    private bool noteTransitionLocked;
    private FigureType? lastGeneratedFigureType;
    private int consecutiveGeneratedFigureRepeats;
    private TMP_Text roundCelebrationRingText;
    private Color currentRoundCelebrationColor;
    private readonly Dictionary<FigureType, int> roundsSinceFigureShown = new Dictionary<FigureType, int>();
    private readonly Dictionary<FigureType, BasicRhythmFigureMetric> figureCounters = new Dictionary<FigureType, BasicRhythmFigureMetric>();
    private BasicRhythmMetricsData figureMetrics;
    private int reportChunkIndex;
    private bool isShowingDemoPattern;
    private float attemptStartedAt;
    private double attemptStartedDspTime;
    private int attemptErrors;
    private int attemptCorrectAnswers;
    private int attemptLevelRepetitions;
    private bool attemptReportSubmitted;
    private string attemptExitReason = "completed";

    private void Start()
    {
        LandscapePhoneCanvas.Configure(GetComponent<Canvas>());
        GameCanvasBackgroundCover.Attach(GetComponent<Canvas>(), "Background");
        GameSettings.EnsureInitialized();
        ResolveReferences();
        if (!ValidateReferences())
            return;

        BuildFigureDefinitions();
        initialBpm = bpm;
        initialPatternMeasures = patternMeasures;
        InitializeFigureMetrics();
        AssignChatterboxVoices();
        EnsureAudioSource();
        EnsureDrumInput();
        EnsureStatusOverlay();
        EnsureNoteProgressBar();
        EnsureTutorialTapHint();
        EnsureRoundCelebrationRing();

        score = 0;
        level = 0;
        lives = maxLives;
        if (livesPanelRect != null)
            livesPanelRect.gameObject.SetActive(false);
        attemptStartedAt = Time.time;
        attemptStartedDspTime = AudioSettings.dspTime;
        attemptErrors = 0;
        attemptCorrectAnswers = 0;
        attemptLevelRepetitions = 0;
        attemptReportSubmitted = false;
        attemptExitReason = "completed";

        UpdateHud();
        if (startButton != null)
        {
            SetFeedback(ComposeFeedback("Pulsa JUGAR para comenzar"), normalFeedbackColor);
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGameFromButton);
            startButton.gameObject.SetActive(true);
            Canvas tutorialCanvas = startButton.GetComponentInParent<Canvas>();
            Transform drumFocus = drumButton != null ? drumButton.transform : null;
            Transform figureFocus = rhythmPanel != null ? rhythmPanel : drumFocus;
            if (drumFocus != null)
                introPlayedByOverlay = GameTutorialOverlay.Show(tutorialCanvas, "basico_tutorial_intro",
                    new[] { drumFocus, figureFocus, startButton.transform },
                    new[] { "Este es tu tambor. Primero, tócalo para conocer su sonido.",
                        "Mira cómo se llena la señal y escucha el ritmo.",
                        "Toca el tambor cuando llegue el momento. ¡Jugar!" },
                    playButton: startButton);
        }
        else
            StartGameFromButton();
    }

    public void StartGameFromButton()
    {
        if (roundRoutine != null || gameFinished)
            return;

        if (startButton != null)
            startButton.gameObject.SetActive(false);
        attemptStartedAt = Time.time;
        attemptStartedDspTime = AudioSettings.dspTime;
        SetFeedback(ComposeFeedback("Escucha"), normalFeedbackColor);
        roundRoutine = StartCoroutine(RunRoundLoop());
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSettings.SettingsChanged -= ApplyGameSettings;
    }

    private void OnDestroy()
    {
        if (tutorialHandSprite != null)
            Destroy(tutorialHandSprite);
        if (tutorialHandTexture != null)
            Destroy(tutorialHandTexture);
        if (noteProgressSprite != null)
            Destroy(noteProgressSprite);
        if (noteProgressTexture != null)
            Destroy(noteProgressTexture);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            backgroundAtDspTime = AudioSettings.dspTime;
        }
        else if (backgroundAtDspTime > 0d)
        {
            if (waitingForInput && !isPaused)
                inputPhaseStartDspTime += System.Math.Max(0d, AudioSettings.dspTime - backgroundAtDspTime);
            backgroundAtDspTime = 0d;
        }
    }

    private void OnEnable()
    {
        GameSettings.SettingsChanged += ApplyGameSettings;
        ApplyGameSettings();
    }

    private bool InTutorialDrumWarmup()
    {
        return tutorialActive && !tutorialWarmupCompleted && tutorialDrumWarmupRemaining > 0;
    }

    private bool InTutorialFillWarmup()
    {
        return tutorialActive && !tutorialWarmupCompleted && tutorialFillWarmupRemaining > 0;
    }

    private void Update()
    {
        if (isPaused || gameFinished)
            return;

        if (InTutorialDrumWarmup() || InTutorialFillWarmup())
            return;

        if (noteTransitionLocked)
            return;

        FigureDefinition currentNote = GetCurrentNote();
        if (!waitingForInput || currentNote == null)
            return;

        float beatDuration = GetBeatDuration();
        double now = AudioSettings.dspTime;
        double expectedStart = inputPhaseStartDspTime + GetPatternBeatsBefore(currentNoteIndex) * beatDuration;
        double expectedEnd = expectedStart + currentNote.noteBeats * beatDuration;

        UpdateAllLongNoteFills(now);

        if (IsSilenceFigure(currentNote))
        {
            UpdateTimingCue(false);

            if (now >= expectedEnd)
                CompleteSilenceNote();

            return;
        }

        if (UsesSegmentedTapFigure(currentNote))
        {
            float progress = Mathf.Clamp01((float)((now - expectedStart) / Mathf.Max(0.01f, currentNote.noteBeats * beatDuration)));
            UpdateTimingCue(System.Math.Abs(now - GetExpectedTapTime(currentNote, currentNoteTapCount + 1, expectedStart, beatDuration)) <= perfectToleranceBeats * beatDuration);
            UpdateSegmentedTapCues(currentNote, progress);

            if (now > GetExpectedTapTime(currentNote, currentNoteTapCount + 1, expectedStart, beatDuration) + goodToleranceBeats * beatDuration)
            {
                ResolveFailure("Un poco después");
                return;
            }

            return;
        }

        if (UsesLongNoteTapCue(currentNote))
        {
            UpdateTimingCue(System.Math.Abs(now - expectedEnd) <= perfectToleranceBeats * beatDuration);

            if (now > expectedEnd + goodToleranceBeats * beatDuration)
            {
                ResolveFailure("Un poco después");
                return;
            }

            return;
        }
    }

    public void OnDrumPointerDown()
    {
        double tapDspTime = AudioSettings.dspTime;
        if (gameFinished)
            return;

        if (HandleTutorialWarmupTap())
            return;

        if (isPaused || !waitingForInput || noteTransitionLocked)
            return;

        FigureDefinition currentNote = GetCurrentNote();
        if (currentNote == null)
            return;

        if (IsSilenceFigure(currentNote))
        {
            RecordTimedTap(currentNote, tapDspTime - (inputPhaseStartDspTime +
                GetPatternBeatsBefore(currentNoteIndex) * GetBeatDuration()));
            ResolveFailure("Ahora escucha y espera", true);
            return;
        }

        if (UsesSegmentedTapFigure(currentNote))
        {
            EvaluateSegmentedTap(tapDspTime);
            if (drumButton != null)
                drumButton.transform.localScale = Vector3.one * 0.95f;
            return;
        }

        if (UsesLongNoteTapCue(currentNote))
        {
            EvaluateLongNoteTap(tapDspTime);
            if (drumButton != null)
                drumButton.transform.localScale = Vector3.one * 0.95f;
            return;
        }
    }

    public void OnDrumPointerUp()
    {
        if (isPaused || gameFinished)
            return;

        if (drumButton != null)
            drumButton.transform.localScale = Vector3.one;
    }

    private IEnumerator RunRoundLoop()
    {
        yield return new WaitForSeconds(roundIntroDelay);

        if (tutorialActive && !tutorialWarmupCompleted)
            yield return StartCoroutine(RunTutorialWarmup());

        while (lives > 0)
        {
            PrepareNextPattern();
            roundFailures = 0;
            roundCompletedSuccessfully = false;

            while (!roundCompletedSuccessfully && lives > 0)
            {
                attemptFinished = false;

                if (!tutorialActive || !tutorialDemoShownForCurrentStep)
                {
                    yield return StartCoroutine(PlayPreRoundVoiceIfNeeded());
                    yield return StartCoroutine(ShowDemoPattern());
                    tutorialDemoShownForCurrentStep = true;
                }

                SetFeedback(ComposeFeedback(GetInputInstruction()), normalFeedbackColor);
                yield return StartCoroutine(PlayBlockingVoice(yourTurnVoiceClip));

                waitingForInput = true;
                currentNoteIndex = 0;
                currentNoteTapCount = 0;
                ShowProgressForNote(0);
                sustainedBeatCuesPlayed = 0;
                noteTransitionLocked = false;
                roundUsedOnlyPerfectTiming = true;
                roundSuccessfulHits = 0;
                inputPhaseStartDspTime = AudioSettings.dspTime;

                if (focusedBlockTransitionRoutine != null)
                {
                    StopCoroutine(focusedBlockTransitionRoutine);
                    focusedBlockTransitionRoutine = null;
                }

                if (useFocusedPatternPresentation)
                {
                    lastFocusedBlockIndex = GetPatternBlockIndex(0);
                    ApplyFocusedBlockVisibilityInstant(lastFocusedBlockIndex);
                    ResetPatternPositions();
                }

                SetDrumInteractable(true);
                ResetPatternVisuals(false);

                yield return new WaitUntil(() => attemptFinished || lives <= 0);
            }

            if (lives <= 0)
                yield break;

            yield return new WaitForSeconds(roundOutroDelay);
        }
    }

    private void PrepareNextPattern()
    {
        BuildCurrentPattern();
        noteCountInPattern = currentPattern.Count;
        tutorialDemoShownForCurrentStep = false;
        currentNoteIndex = 0;
        currentNoteTapCount = 0;
        sustainedBeatCuesPlayed = 0;
        noteTransitionLocked = false;
        lastFocusedBlockIndex = -1;

        BuildPatternVisuals();
        SetDrumInteractable(false);
        drumButton.transform.localScale = Vector3.one;
    }

    private IEnumerator ShowDemoPattern()
    {
        SetFeedback(ComposeFeedback(GetDemoInstruction()), normalFeedbackColor);
        ResetPatternVisuals(true);
        isShowingDemoPattern = true;

        for (int i = 0; i < noteCountInPattern; i++)
        {
            FigureDefinition note = GetPatternNoteAt(i);
            float noteDuration = note.noteBeats * GetBeatDuration();

            if (useFocusedPatternPresentation)
                yield return StartCoroutine(EnsureFocusedBlockForNote(i, animate: true));

            HighlightPatternIcon(i, noteLitColor);
            yield return StartCoroutine(AnimateFillForDemo(i, note, noteDuration));

            HighlightPatternIcon(i, noteLitColor);
            SetFillAmount(i, 0f);
        }

        isShowingDemoPattern = false;
        ResetPatternPositions();
    }

    private IEnumerator AnimateFillForDemo(int noteIndex, FigureDefinition note, float duration)
    {
        ShowProgressForNote(noteIndex);
        SetFillAmount(noteIndex, 0f);

        float elapsed = 0f;

        if (UsesSegmentedTapFigure(note))
            sustainedBeatCuesPlayed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetFillAmount(noteIndex, progress);
            UpdateActiveFillMotion(noteIndex);

            if (UsesSegmentedTapFigure(note))
            {
                UpdateSegmentedTapCues(note, progress);
            }

            yield return null;
        }

        SetFillAmount(noteIndex, 1f);

        if (UsesSegmentedTapFigure(note))
        {
            UpdateSegmentedTapCues(note, 1f);
        }
        else if (!IsSilenceFigure(note))
        {
            PlaySfx(metronomeClip);
            PulseDrum(timingCueColor);
        }
    }

    private IEnumerator RunTutorialWarmup()
    {
        while (NarrationPlayer.IsSpeaking)
            yield return null;

        if (!introPlayedByOverlay && tutorialIntroClip != null)
            yield return StartCoroutine(PlayBlockingVoice(tutorialIntroClip));

        if (drumDiscoveryVoiceClip != null)
            yield return StartCoroutine(PlayBlockingVoice(drumDiscoveryVoiceClip));
        yield return StartCoroutine(RunDrumDiscoveryWarmup());
        if (fillTimingVoiceClip != null)
            yield return StartCoroutine(PlayBlockingVoice(fillTimingVoiceClip));
        yield return StartCoroutine(RunFillTimingWarmup());

        tutorialWarmupCompleted = true;
        ClearPatternVisuals();
    }

    private IEnumerator RunDrumDiscoveryWarmup()
    {
        tutorialDrumWarmupRemaining = 5;
        SetDrumInteractable(true);
        SetFeedback("Toca el tambor 5 veces", normalFeedbackColor);

        while (tutorialDrumWarmupRemaining > 0)
            yield return null;

        SetDrumInteractable(false);
        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator RunFillTimingWarmup()
    {
        FigureDefinition practiceFigure = GetFigure(FigureType.Negra);
        if (practiceFigure == null)
            yield break;

        currentPattern.Clear();
        currentPatternBlockNoteCounts.Clear();
        currentPattern.Add(practiceFigure);
        currentPatternBlockNoteCounts.Add(1);
        noteCountInPattern = 1;
        BuildPatternVisuals();
        ShowProgressForNote(0);
        SetDrumInteractable(true);

        tutorialFillWarmupRemaining = 3;
        while (tutorialFillWarmupRemaining > 0)
        {
            tutorialFillWarmupAttemptCompleted = false;
            tutorialFillWarmupWaitingForTap = false;
            HighlightPatternIcon(0, noteLitColor);
            SetFillColor(0, longNoteFillColor);
            SetFillAmount(0, 0f);
            SetFeedback("Toca cuando se llene: " + tutorialFillWarmupRemaining, normalFeedbackColor);

            float duration = practiceFigure.noteBeats * GetBeatDuration();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                SetFillAmount(0, progress);
                yield return null;
            }

            SetFillAmount(0, 1f);
            tutorialFillWarmupWaitingForTap = true;
            PlaySfx(metronomeClip);
            PulseDrum(timingCueColor);
            SetFeedback("Ahora toca el tambor: " + tutorialFillWarmupRemaining, timingCueColor);

            while (!tutorialFillWarmupAttemptCompleted)
                yield return null;

            tutorialFillWarmupRemaining--;
            yield return new WaitForSeconds(0.2f);
        }

        tutorialFillWarmupWaitingForTap = false;
        SetDrumInteractable(false);
    }

    private bool HandleTutorialWarmupTap()
    {
        if (InTutorialDrumWarmup())
        {
            tutorialDrumWarmupRemaining--;
            AccessibleHaptics.Success();
            PlaySfx(metronomeClip);
            PulseDrum(timingCueColor);

            if (tutorialDrumWarmupRemaining > 0)
                SetFeedback("Toca el tambor: " + tutorialDrumWarmupRemaining, normalFeedbackColor);
            else
                SetFeedback("Muy bien", successFeedbackColor);

            return true;
        }

        if (InTutorialFillWarmup())
        {
            if (!tutorialFillWarmupWaitingForTap)
            {
                AccessibleHaptics.Error();
                SetFeedback("Espera a que se llene", normalFeedbackColor);
                PulseDrum(timingCueColor);
                return true;
            }

            tutorialFillWarmupWaitingForTap = false;
            tutorialFillWarmupAttemptCompleted = true;
            AccessibleHaptics.Success();
            SetPatternResultColor(0, successFeedbackColor, true);
            PlaySfx(metronomeClip);
            PulseDrum(successFeedbackColor);
            SetFeedback("Muy bien", successFeedbackColor);
            return true;
        }

        return false;
    }

    private void UpdateAllLongNoteFills(double currentDspTime)
    {
        if (currentPattern.Count == 0)
            return;

        float beatDuration = GetBeatDuration();
        ResetPatternPositions();

        for (int i = 0; i < noteCountInPattern; i++)
        {
            FigureDefinition note = GetPatternNoteAt(i);
            float noteDuration = note.noteBeats * beatDuration;
            double noteStart = inputPhaseStartDspTime + GetPatternBeatsBefore(i) * beatDuration;

            if (i < currentNoteIndex)
                continue;

            if (i == currentNoteIndex)
            {
                float progress = Mathf.Clamp01((float)((currentDspTime - noteStart) / Mathf.Max(0.01f, noteDuration)));
                SetFillAmount(i, progress);
                UpdateActiveFillMotion(i);
            }
            if (i > currentNoteIndex)
            {
                HighlightPatternIcon(i, noteLitColor);
                SetFillColor(i, longNoteFillColor);
            }
        }

        if (currentNoteIndex >= 0 && currentNoteIndex < patternIcons.Count)
        {
            HighlightPatternIcon(currentNoteIndex, noteLitColor);
            SetFillColor(currentNoteIndex, longNoteFillColor);
        }
    }

    private void EvaluateLongNoteTap(double tapDspTime)
    {
        FigureDefinition currentNote = GetCurrentNote();
        if (currentNote == null)
            return;

        float beatDuration = GetBeatDuration();
        double expectedStart = inputPhaseStartDspTime + GetPatternBeatsBefore(currentNoteIndex) * beatDuration;
        double expectedEnd = expectedStart + currentNote.noteBeats * beatDuration;
        double timingErrorBeats = System.Math.Abs(tapDspTime - expectedEnd) / beatDuration;
        RecordTimedTap(currentNote, tapDspTime - expectedEnd);

        if (timingErrorBeats <= perfectToleranceBeats)
        {
            roundSuccessfulHits++;
            attemptCorrectAnswers++;
            SetPatternResultColor(currentNoteIndex, successFeedbackColor, true);
            PlaySuccessCue(currentNote);
            PulseDrum(successFeedbackColor);
        }
        else if (timingErrorBeats <= goodToleranceBeats)
        {
            roundSuccessfulHits++;
            attemptCorrectAnswers++;
            roundUsedOnlyPerfectTiming = false;
            SetPatternResultColor(currentNoteIndex, successFeedbackColor, true);
            PlaySuccessCue(currentNote);
            PulseDrum(successFeedbackColor);
        }
        else
        {
            ResolveFailure(tapDspTime < expectedEnd ? "Un poco antes" : "Un poco después", true);
            return;
        }

        AccessibleHaptics.Success();
        RecordFigureSuccess(currentNote);
        QueueAdvanceToNextNote();
    }

    private void EvaluateSegmentedTap(double tapDspTime)
    {
        FigureDefinition currentNote = GetCurrentNote();
        if (!UsesSegmentedTapFigure(currentNote))
            return;

        float beatDuration = GetBeatDuration();
        double expectedStart = inputPhaseStartDspTime + GetPatternBeatsBefore(currentNoteIndex) * beatDuration;
        int nextTapIndex = currentNoteTapCount + 1;
        double expectedTapTime = GetExpectedTapTime(currentNote, nextTapIndex, expectedStart, beatDuration);
        double timingErrorBeats = System.Math.Abs(tapDspTime - expectedTapTime) / beatDuration;
        RecordTimedTap(currentNote, tapDspTime - expectedTapTime);

        if (timingErrorBeats > goodToleranceBeats)
        {
            ResolveFailure(tapDspTime < expectedTapTime ? "Un poco antes" : "Un poco después", true);
            return;
        }

        if (timingErrorBeats > perfectToleranceBeats)
            roundUsedOnlyPerfectTiming = false;

        currentNoteTapCount++;
        AccessibleHaptics.Success();
        PulseDrum(successFeedbackColor);

        if (currentNoteTapCount < GetRequiredTapCount(currentNote))
            return;

        roundSuccessfulHits++;
        attemptCorrectAnswers++;
        RecordFigureSuccess(currentNote);
        SetPatternResultColor(currentNoteIndex, successFeedbackColor, true);
        QueueAdvanceToNextNote();
    }

    private void CompleteSilenceNote()
    {
        FigureDefinition currentNote = GetCurrentNote();
        if (!IsSilenceFigure(currentNote))
            return;

        roundSuccessfulHits++;
        attemptCorrectAnswers++;
        RecordFigureSuccess(currentNote);
        SetPatternResultColor(currentNoteIndex, successFeedbackColor, true);
        QueueAdvanceToNextNote(true);
    }

    private void CompletePatternSuccess()
    {
        waitingForInput = false;
        if (noteProgressRoot != null)
            noteProgressRoot.gameObject.SetActive(false);
        roundCompletedSuccessfully = true;

        assistedAttempts = 0;
        if (!tutorialActive && roundFailures == 0 && ++recoverySuccessStreak >= 3)
        {
            bpm = Mathf.Min(initialBpm, bpm + 5f);
            patternMeasures = Mathf.Min(initialPatternMeasures, patternMeasures + 1);
            recoverySuccessStreak = 0;
        }
        else if (roundFailures > 0)
        {
            recoverySuccessStreak = 0;
        }

        bool isPerfectAttempt = roundUsedOnlyPerfectTiming && roundFailures == 0 && roundSuccessfulHits == noteCountInPattern;
        bool tutorialWillComplete = tutorialActive && tutorialStepIndex + 1 >= 5;

        score += isPerfectAttempt ? 10 : 5;

        if (tutorialActive)
        {
            tutorialStepIndex++;
            if (tutorialStepIndex >= 5)
            {
                tutorialActive = false;
                level = 1;
            }
        }
        else
        {
            level++;
        }

        UpdateHud();
        PlaySfx(successClip);
        currentRoundCelebrationColor = tutorialWillComplete
            ? tutorialCompletionCelebrationColor
            : isPerfectAttempt ? perfectRoundCelebrationColor : roundCelebrationColor;
        PlayRoundCelebration();
        attemptFinished = true;
        waitingForInput = false;

        if (isPerfectAttempt)
        {
            SetFeedback(ComposeFeedback("Perfecto +10"), successFeedbackColor);
            PlayVoice(veryGoodVoiceClip);
        }
        else
        {
            SetFeedback(ComposeFeedback("Bien +5"), successFeedbackColor);
        }
    }

    private void ResolveFailure(string reason, bool fromTap = false)
    {
        if (fromTap)
            AccessibleHaptics.Error();
        currentNoteTapCount = 0;
        sustainedBeatCuesPlayed = 0;

        if (drumButton != null)
        {
            if (drumPulseRoutine != null)
            {
                StopCoroutine(drumPulseRoutine);
                drumPulseRoutine = null;
            }
            drumButton.transform.localScale = Vector3.one;
            if (drumButtonImage != null)
                drumButtonImage.color = drumButtonBaseColor;
            SetDrumInteractable(false);
        }

        if (currentNoteIndex >= 0 && currentNoteIndex < patternIcons.Count)
            HighlightPatternIcon(currentNoteIndex, failFeedbackColor);
        ResetPatternPositions();

        roundFailures++;
        attemptErrors++;
        RecordFigureFailure(GetCurrentNote());
        if (outcomeFeedback != null)
            outcomeFeedback.Hide();
        if (statusOverlayRoutine != null)
        {
            StopCoroutine(statusOverlayRoutine);
            statusOverlayRoutine = null;
        }
        HideStatusOverlay();

        QueueAdvanceToNextNote(true);
        if (noteProgressRoot != null && noteProgressRoot.gameObject.activeSelf)
        {
            if (failureProgressRoutine != null)
                StopCoroutine(failureProgressRoutine);
            failureProgressActive = true;
            noteProgressFill.color = failureProgressColor;
            noteProgressTrackImage.color = failureProgressColor;
            failureProgressRoutine = StartCoroutine(RestoreProgressColorAfterFailure());
        }
    }

    private IEnumerator RestoreProgressColorAfterFailure()
    {
        yield return new WaitForSeconds(0.3f);
        failureProgressActive = false;
        failureProgressRoutine = null;
        if (noteProgressTrackImage != null)
            noteProgressTrackImage.color = new Color(0.89f, 0.93f, 0.95f, 1f);
        if (progressNoteIndex >= 0)
            SetFillColor(progressNoteIndex, longNoteFillColor);
    }

    private void AdvanceToNextNote(bool resetTimingWindow = false)
    {
        currentNoteIndex++;
        currentNoteTapCount = 0;
        sustainedBeatCuesPlayed = 0;
        if (currentNoteIndex < noteCountInPattern)
            ShowProgressForNote(currentNoteIndex);

        if (currentNoteIndex >= noteCountInPattern)
        {
            FinishCurrentAttempt();
            return;
        }

        if (resetTimingWindow)
            inputPhaseStartDspTime = AudioSettings.dspTime - GetPatternBeatsBefore(currentNoteIndex) * GetBeatDuration();

        if (useFocusedPatternPresentation && GetPatternBlockIndex(currentNoteIndex) != lastFocusedBlockIndex)
        {
            lastFocusedBlockIndex = GetPatternBlockIndex(currentNoteIndex);
            ApplyFocusedBlockVisibilityInstant(lastFocusedBlockIndex);
        }

        if (drumButton != null)
            SetDrumInteractable(true);

        SetFeedback(ComposeFeedback(GetInputInstruction()), normalFeedbackColor);
    }

    private void QueueAdvanceToNextNote(bool resetTimingWindow = false)
    {
        if (noteTransitionLocked)
            return;

        AdvanceToNextNote(resetTimingWindow);
    }

    private void FinishCurrentAttempt()
    {
        waitingForInput = false;
        attemptFinished = true;

        if (drumButton != null)
            SetDrumInteractable(false);

        int requiredHits = GetRequiredHitsForPattern();
        if (roundSuccessfulHits >= requiredHits)
        {
            CompletePatternSuccess();
            return;
        }

        if (tutorialActive)
            tutorialDemoShownForCurrentStep = false;

        attemptLevelRepetitions++;
        assistedAttempts++;
        recoverySuccessStreak = 0;
        if (assistedAttempts >= 2)
        {
            assistedAttempts = 0;
            if (!tutorialActive)
                patternMeasures = Mathf.Max(1, patternMeasures - 1);
            bpm = Mathf.Max(40f, bpm - 5f);
            PrepareNextPattern();
            roundFailures = 0;
            SetFeedback(ComposeFeedback("Vamos más despacio con una pista"), normalFeedbackColor, true);
        }
        else
        {
            SetFeedback(ComposeFeedback("Probemos otra vez con ayuda"), normalFeedbackColor, true);
        }
        PlayVoice(tryAgainVoiceClip);
    }

    private void ShowGameSummary()
    {
        if (gameFinished)
            return;

        gameFinished = true;
        waitingForInput = false;
        attemptFinished = true;
        noteTransitionLocked = false;
        SetDrumInteractable(false);
        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        if (pausePanel == null)
        {
            SetFeedback("¡Buen trabajo! Puedes volver a intentarlo desde el menú.", successFeedbackColor);
            return;
        }

        if (resumeButton != null) resumeButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(true);
        if (pauseMenuButton != null) pauseMenuButton.gameObject.SetActive(true);

        if (gameSummaryText == null)
        {
            Transform container = pausePanel.transform.Find("PauseBox") ?? pausePanel.transform;
            GameObject label = new GameObject("GameSummaryText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.SetParent(container, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = new Vector2(0f, 160f);
            rect.sizeDelta = new Vector2(450f, 150f);
            gameSummaryText = label.GetComponent<TMP_Text>();
            gameSummaryText.font = statusOverlayText != null ? statusOverlayText.font : levelText != null ? levelText.font : gameSummaryText.font;
            gameSummaryText.fontSize = 26f;
            gameSummaryText.alignment = TextAlignmentOptions.Center;
            gameSummaryText.color = new Color(0.1f, 0.2f, 0.35f, 1f);
            gameSummaryText.raycastTarget = false;
        }

        gameSummaryText.text = $"¡Buen trabajo!\nPuntos: {score}\nAciertos: {attemptCorrectAnswers}\nReinicia o vuelve al menú";
        PlayVoice(endVoiceClip);
        pausePanel.transform.SetAsLastSibling();
        if (pausePanelView != null)
            pausePanelView.Show();
        else
            pausePanel.SetActive(true);
    }

    private int GetRequiredHitsForPattern()
    {
        if (tutorialActive)
            return noteCountInPattern;

        if (!tutorialActive && patternMeasures > 1)
            return Mathf.Clamp(Mathf.CeilToInt(noteCountInPattern * longPatternRequiredHitRatio), 1, noteCountInPattern);

        if (noteCountInPattern <= 1)
            return 1;

        if (noteCountInPattern == 2)
            return 2;

        if (noteCountInPattern == 4)
            return 3;

        if (noteCountInPattern == 8)
            return 5;

        return Mathf.Max(1, noteCountInPattern);
    }

    private void BuildCurrentPattern()
    {
        currentPattern.Clear();
        currentPatternBlockNoteCounts.Clear();
        patternBlockIndexByNote.Clear();

        if (tutorialActive)
        {
            switch (tutorialStepIndex)
            {
                case 0:
                    AddPatternBlock(FigureType.Redonda, 1);
                    break;
                case 1:
                    AddPatternBlock(FigureType.Blanca, 2);
                    break;
                case 2:
                    AddPatternBlock(FigureType.Negra, 4);
                    break;
                case 3:
                    AddPatternBlock(FigureType.Silencio, 4);
                    break;
                default:
                    AddPatternBlock(FigureType.Corchea, 8);
                    break;
            }
            return;
        }

        for (int i = 0; i < Mathf.Max(1, patternMeasures); i++)
            BuildHomogeneousPatternBlock();
    }

    private void BuildHomogeneousPatternBlock()
    {
        List<FigureDefinition> candidates = new List<FigureDefinition>();
        for (int i = 0; i < figures.Count; i++)
        {
            float repetitions = totalPatternBeats / figures[i].noteBeats;
            if (Mathf.Abs(repetitions - Mathf.Round(repetitions)) <= 0.001f)
                candidates.Add(figures[i]);
        }

        if (candidates.Count == 0)
            return;

        if (lastGeneratedFigureType.HasValue && consecutiveGeneratedFigureRepeats >= maxConsecutiveFigureRepeats)
            candidates.RemoveAll(candidate => candidate.type == lastGeneratedFigureType.Value);

        if (candidates.Count == 0)
        {
            for (int i = 0; i < figures.Count; i++)
            {
                float repetitions = totalPatternBeats / figures[i].noteBeats;
                if (Mathf.Abs(repetitions - Mathf.Round(repetitions)) <= 0.001f)
                    candidates.Add(figures[i]);
            }
        }

        FigureDefinition chosen = ChooseWeightedCandidate(candidates);
        if (chosen != null && chosen.type == FigureType.Silencio)
        {
            if (TryAddMixedSilenceBlock(chosen, out FigureType soundingType))
            {
                RegisterGeneratedFigure(FigureType.Silencio);
                roundsSinceFigureShown[soundingType] = 0;
                return;
            }

            candidates.Remove(chosen);
            chosen = ChooseWeightedCandidate(candidates);
        }

        if (chosen == null)
            return;

        RegisterGeneratedFigure(chosen.type);
        int repetitionsCount = Mathf.RoundToInt(totalPatternBeats / chosen.noteBeats);
        AddPatternBlock(chosen.type, repetitionsCount);
    }

    private bool TryAddMixedSilenceBlock(FigureDefinition silence, out FigureType soundingType)
    {
        soundingType = FigureType.Negra;
        float remainingBeats = totalPatternBeats - silence.noteBeats;
        if (remainingBeats <= 0f)
            return false;

        FigureDefinition sounding = GetFigure(FigureType.Negra);
        if (!FitsInRemainingBeats(sounding, remainingBeats))
        {
            sounding = null;
            for (int i = 0; i < figures.Count; i++)
            {
                if (figures[i].type != FigureType.Silencio && FitsInRemainingBeats(figures[i], remainingBeats))
                {
                    sounding = figures[i];
                    break;
                }
            }
        }

        if (sounding == null)
            return false;

        soundingType = sounding.type;
        int soundingCount = Mathf.RoundToInt(remainingBeats / sounding.noteBeats);
        int silencePosition = Random.Range(0, soundingCount + 1);
        int blockIndex = currentPatternBlockNoteCounts.Count;
        for (int i = 0; i <= soundingCount; i++)
        {
            currentPattern.Add(i == silencePosition ? silence : sounding);
            patternBlockIndexByNote.Add(blockIndex);
        }

        currentPatternBlockNoteCounts.Add(soundingCount + 1);
        return true;
    }

    private static bool FitsInRemainingBeats(FigureDefinition figure, float remainingBeats)
    {
        if (figure == null || figure.noteBeats <= 0f)
            return false;

        float repetitions = remainingBeats / figure.noteBeats;
        return repetitions >= 1f && Mathf.Abs(repetitions - Mathf.Round(repetitions)) <= 0.001f;
    }

    private void RegisterGeneratedFigure(FigureType type)
    {
        List<FigureType> figureTypes = new List<FigureType>();
        for (int i = 0; i < figures.Count; i++)
        {
            if (!figureTypes.Contains(figures[i].type))
                figureTypes.Add(figures[i].type);
        }

        for (int i = 0; i < figureTypes.Count; i++)
        {
            FigureType figureType = figureTypes[i];
            if (figureType == type)
                roundsSinceFigureShown[figureType] = 0;
            else
                roundsSinceFigureShown[figureType] = GetRoundsSinceShown(figureType) + 1;
        }

        if (lastGeneratedFigureType.HasValue && lastGeneratedFigureType.Value == type)
            consecutiveGeneratedFigureRepeats++;
        else
        {
            lastGeneratedFigureType = type;
            consecutiveGeneratedFigureRepeats = 1;
        }
    }

    private FigureDefinition ChooseWeightedCandidate(List<FigureDefinition> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        float totalWeight = 0f;
        float[] weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            float weight = 1f + GetRoundsSinceShown(candidates[i].type) * figureRecencyWeightStep;
            weights[i] = Mathf.Max(0.01f, weight);
            totalWeight += weights[i];
        }

        float randomPoint = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (randomPoint <= cumulative)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    private int GetRoundsSinceShown(FigureType type)
    {
        return roundsSinceFigureShown.TryGetValue(type, out int value) ? value : 0;
    }

    private void AddPatternBlock(FigureType type, int count)
    {
        FigureDefinition figure = GetFigure(type);
        if (figure == null)
            return;

        int blockIndex = currentPatternBlockNoteCounts.Count;

        for (int i = 0; i < count; i++)
        {
            currentPattern.Add(figure);
            patternBlockIndexByNote.Add(blockIndex);
        }

        currentPatternBlockNoteCounts.Add(count);
    }

    private FigureDefinition GetFigure(FigureType type)
    {
        for (int i = 0; i < figures.Count; i++)
        {
            if (figures[i].type == type)
                return figures[i];
        }

        return null;
    }

    private FigureDefinition GetPatternNoteAt(int index)
    {
        if (index < 0 || index >= currentPattern.Count)
            return null;

        return currentPattern[index];
    }

    private FigureDefinition GetCurrentNote()
    {
        return GetPatternNoteAt(currentNoteIndex);
    }

    private float GetPatternBeatsBefore(int index)
    {
        float beats = 0f;
        for (int i = 0; i < index && i < currentPattern.Count; i++)
            beats += currentPattern[i].noteBeats;

        return beats;
    }

    private void BuildPatternVisuals()
    {
        ClearPatternVisuals();

        if (figureIconTemplate == null || rhythmPanel == null)
            return;

        figureIconTemplate.gameObject.SetActive(false);

        useFocusedPatternPresentation = noteCountInPattern >= focusedPatternThreshold;
        if (useFocusedPatternPresentation)
        {
            BuildFocusedPatternVisuals();
            ResetPatternVisuals(true);
            return;
        }

        RectTransform templateRect = figureIconTemplate.rectTransform;
        float availableWidth = Mathf.Max(560f, rhythmPanel.rect.width - 90f);
        float availableHeight = Mathf.Max(340f, rhythmPanel.rect.height - 70f);
        float templateWidth = templateRect.rect.width > 0f ? templateRect.rect.width : 140f;
        float templateHeight = templateRect.rect.height > 0f ? templateRect.rect.height : 140f;
        int blockCount = Mathf.Max(1, currentPatternBlockNoteCounts.Count);
        int blocksPerRow = blockCount <= 2 ? blockCount : 2;
        int blockRows = Mathf.CeilToInt(blockCount / 2f);
        float blockSpacing = iconSpacing * 1.2f;
        float blockWidth = (availableWidth - blockSpacing * (blocksPerRow - 1)) / blocksPerRow;
        float blockHeight = (availableHeight - blockSpacing * (blockRows - 1)) / blockRows;

        int maxBlockColumns = 1;
        int maxBlockRows = 1;
        for (int i = 0; i < currentPatternBlockNoteCounts.Count; i++)
        {
            maxBlockColumns = Mathf.Max(maxBlockColumns, Mathf.Min(4, currentPatternBlockNoteCounts[i]));
            maxBlockRows = Mathf.Max(maxBlockRows, Mathf.CeilToInt(currentPatternBlockNoteCounts[i] / 4f));
        }

        float iconWidth = (blockWidth - iconSpacing * (maxBlockColumns - 1)) / maxBlockColumns;
        float iconHeight = (blockHeight - iconSpacing * (maxBlockRows - 1)) / maxBlockRows;
        float iconSize = Mathf.Clamp(Mathf.Min(iconWidth, iconHeight, templateWidth * 2f, templateHeight * 2f), 84f, 210f);
        float rowHeight = iconSize + iconSpacing;
        int patternIndex = 0;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            int notesInBlock = blockIndex < currentPatternBlockNoteCounts.Count ? currentPatternBlockNoteCounts[blockIndex] : noteCountInPattern;
            int blockRow = blockIndex / blocksPerRow;
            int blockColumn = blockIndex % blocksPerRow;
            float blockGridWidth = blocksPerRow * blockWidth + (blocksPerRow - 1) * blockSpacing;
            float blockGridHeight = blockRows * blockHeight + (blockRows - 1) * blockSpacing;
            float blockX = -blockGridWidth * 0.5f + blockWidth * 0.5f + blockColumn * (blockWidth + blockSpacing);
            float blockY = blockGridHeight * 0.5f - blockHeight * 0.5f - blockRow * (blockHeight + blockSpacing) + 10f;
            int noteRowsInBlock = Mathf.CeilToInt(notesInBlock / 4f);

            for (int noteIndex = 0; noteIndex < notesInBlock && patternIndex < noteCountInPattern; noteIndex++, patternIndex++)
            {
                Image icon = Instantiate(figureIconTemplate, rhythmPanel);
                icon.name = "PatternIcon_" + patternIndex;
                icon.sprite = GetVisualSpriteForPatternIndex(patternIndex, noteIndex);
                icon.preserveAspect = true;
                icon.gameObject.SetActive(true);

                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);

                int row = noteIndex / 4;
                int itemsInRow = row < noteRowsInBlock - 1 ? 4 : notesInBlock - row * 4;
                float rowWidth = itemsInRow * iconSize + (itemsInRow - 1) * iconSpacing;
                float rowStartX = -rowWidth * 0.5f + iconSize * 0.5f;
                int column = noteIndex % 4;
                float localX = rowStartX + column * (iconSize + iconSpacing);
                float localY = (noteRowsInBlock - 1) * rowHeight * 0.5f - row * rowHeight;

                iconRect.anchoredPosition = new Vector2(blockX + localX, blockY + localY);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);

                patternIcons.Add(icon);
                focusedBasePositions.Add(iconRect.anchoredPosition);

            }
        }

        ResetPatternVisuals(true);
    }

    private void BuildFocusedPatternVisuals()
    {
        focusedBasePositions.Clear();
        lastFocusedBlockIndex = -1;

        int patternIndex = 0;
        for (int blockIndex = 0; blockIndex < currentPatternBlockNoteCounts.Count; blockIndex++)
        {
            int notesInBlock = currentPatternBlockNoteCounts[blockIndex];
            int columns = Mathf.Min(4, Mathf.Max(1, notesInBlock));
            int rows = Mathf.CeilToInt(notesInBlock / 4f);
            float cellWidth = focusedPatternAreaSize.x / columns;
            float cellHeight = focusedPatternAreaSize.y / rows;
            float canvasHeight = rootCanvas != null ? ((RectTransform)rootCanvas.transform).rect.height : 800f;
            float heightLimit = Mathf.Max(72f, (canvasHeight - 360f) * 0.4f);
            float iconSize = Mathf.Min(Mathf.Clamp(Mathf.Min(cellWidth, cellHeight) - iconSpacing, 72f, focusedPatternIconSize), heightLimit);
            Vector2 patternPosition = focusedPatternPosition + new Vector2(0f, Mathf.Max(0f, (800f - canvasHeight) * 0.25f));

            for (int noteIndex = 0; noteIndex < notesInBlock && patternIndex < noteCountInPattern; noteIndex++, patternIndex++)
            {
                Image icon = Instantiate(figureIconTemplate, rhythmPanel);
                icon.name = "PatternIcon_" + patternIndex;
                icon.sprite = GetVisualSpriteForPatternIndex(patternIndex, noteIndex);
                icon.preserveAspect = true;
                icon.gameObject.SetActive(true);

                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);

                int row = noteIndex / 4;
                int column = noteIndex % 4;
                float rowItems = Mathf.Min(4, notesInBlock - row * 4);
                float rowWidth = rowItems * iconSize + (rowItems - 1) * iconSpacing;
                float localX = -rowWidth * 0.5f + iconSize * 0.5f + column * (iconSize + iconSpacing);
                float localY = ((rows - 1) * (iconSize + iconSpacing) * 0.5f) - row * (iconSize + iconSpacing);
                Vector2 anchored = patternPosition + new Vector2(localX, localY);
                iconRect.anchoredPosition = anchored;
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);

                patternIcons.Add(icon);
                focusedBasePositions.Add(anchored);

            }
        }
    }

    private void ClearPatternVisuals()
    {
        for (int i = 0; i < patternIcons.Count; i++)
        {
            if (patternIcons[i] != null)
                Destroy(patternIcons[i].gameObject);
        }

        patternIcons.Clear();
        focusedBasePositions.Clear();
        progressNoteIndex = -1;
        if (noteProgressRoot != null)
            noteProgressRoot.gameObject.SetActive(false);
    }

    private Sprite GetVisualSpriteForPatternIndex(int patternIndex, int indexWithinBlock)
    {
        FigureDefinition figure = GetPatternNoteAt(patternIndex);
        if (figure == null)
            return null;

        if (figure.orderedSprites != null && figure.orderedSprites.Length > 0)
        {
            Sprite orderedSprite = figure.orderedSprites[indexWithinBlock % figure.orderedSprites.Length];
            if (orderedSprite != null)
                return orderedSprite;
        }

        return figure.sprite;
    }

    private void ResetPatternVisuals(bool dimIcons)
    {
        Color iconColor = noteLitColor;

        for (int i = 0; i < patternIcons.Count; i++)
        {
            HighlightPatternIcon(i, iconColor);
            SetFillColor(i, longNoteFillColor);
        }

        if (noteCountInPattern > 0)
            ShowProgressForNote(0);

        if (useFocusedPatternPresentation)
        {
            lastFocusedBlockIndex = GetPatternBlockIndex(0);
            ApplyFocusedBlockVisibilityInstant(lastFocusedBlockIndex);
        }

        UpdateTimingCue(false);
    }

    private void HighlightPatternIcon(int index, Color color)
    {
        if (index < 0 || index >= patternIcons.Count || patternIcons[index] == null)
            return;

        // Conservar las ilustraciones originales; sólo las tarjetas ya evaluadas reciben un tinte suave.
        patternIcons[index].color = color == failFeedbackColor ? new Color(1f, 0.38f, 0.38f, 1f) :
            color == successFeedbackColor ? new Color(0.62f, 0.95f, 0.64f, 1f) : Color.white;
    }

    private void SetPatternResultColor(int index, Color color, bool fillFully)
    {
        HighlightPatternIcon(index, color);
        SetFillColor(index, color);
        if (fillFully)
            SetFillAmount(index, 1f);
    }

    private void SetFillAmount(int index, float amount)
    {
        if (index != progressNoteIndex || noteProgressFill == null)
            return;

        noteProgressFill.fillAmount = Mathf.Clamp01(amount);
        noteProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            noteProgressTrack.rect.width * noteProgressFill.fillAmount);
    }

    private void SetFillColor(int index, Color color)
    {
        if (index != progressNoteIndex || noteProgressFill == null)
            return;

        if (!failureProgressActive || color != longNoteFillColor)
            noteProgressFill.color = color == longNoteFillColor && IsSilenceFigure(GetPatternNoteAt(index))
                ? silenceProgressColor : color;
    }

    private void ApplyFocusedBlockVisibilityInstant(int activeBlockIndex)
    {
        for (int i = 0; i < patternIcons.Count; i++)
        {
            bool visible = GetPatternBlockIndex(i) == activeBlockIndex;
            if (patternIcons[i] != null)
            {
                patternIcons[i].gameObject.SetActive(visible);
                if (visible)
                {
                    patternIcons[i].rectTransform.anchoredPosition = focusedBasePositions[i];
                    Color iconColor = patternIcons[i].color;
                    iconColor.a = 1f;
                    patternIcons[i].color = iconColor;
                }
            }

        }
    }

    private IEnumerator EnsureFocusedBlockForNote(int noteIndex, bool animate)
    {
        if (!useFocusedPatternPresentation)
            yield break;

        int targetBlockIndex = GetPatternBlockIndex(noteIndex);
        if (targetBlockIndex < 0)
            yield break;

        if (targetBlockIndex == lastFocusedBlockIndex)
        {
            ApplyFocusedBlockVisibilityInstant(targetBlockIndex);
            yield break;
        }

        lastFocusedBlockIndex = targetBlockIndex;

        if (focusedBlockTransitionRoutine != null)
        {
            StopCoroutine(focusedBlockTransitionRoutine);
            focusedBlockTransitionRoutine = null;
        }

        if (animate)
            yield return StartCoroutine(AnimateFocusedBlockTransition(targetBlockIndex));
        else
            ApplyFocusedBlockVisibilityInstant(targetBlockIndex);
    }

    private void ResetPatternPositions()
    {
        int count = Mathf.Min(patternIcons.Count, focusedBasePositions.Count);
        for (int i = 0; i < count; i++)
        {
            if (patternIcons[i] != null)
                patternIcons[i].rectTransform.anchoredPosition = focusedBasePositions[i];
        }
    }

    private void UpdateActiveFillMotion(int activeIndex)
    {
        if (activeIndex < 0 || activeIndex >= patternIcons.Count || activeIndex >= focusedBasePositions.Count)
            return;

        float offsetY = Mathf.Sin(Time.time * activeFillMoveSpeed) * activeFillMoveDistance;

        if (patternIcons[activeIndex] != null)
            patternIcons[activeIndex].rectTransform.anchoredPosition = focusedBasePositions[activeIndex] + new Vector2(0f, offsetY);
    }

    private IEnumerator AnimateFocusedBlockTransition(int activeBlockIndex)
    {
        List<int> activeIndices = new List<int>();
        for (int i = 0; i < patternIcons.Count; i++)
        {
            bool visible = GetPatternBlockIndex(i) == activeBlockIndex;
            if (patternIcons[i] != null)
            {
                patternIcons[i].gameObject.SetActive(visible);
                if (visible)
                    activeIndices.Add(i);
            }

        }

        float elapsed = 0f;
        while (elapsed < focusedBlockTransitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / focusedBlockTransitionDuration);
            for (int i = 0; i < activeIndices.Count; i++)
            {
                int iconIndex = activeIndices[i];
                if (patternIcons[iconIndex] == null)
                    continue;

                RectTransform rect = patternIcons[iconIndex].rectTransform;
                Vector2 targetPosition = focusedBasePositions[iconIndex];
                rect.anchoredPosition = Vector2.Lerp(targetPosition + new Vector2(focusedBlockSlideDistance, 0f), targetPosition, t);

                Color iconColor = patternIcons[iconIndex].color;
                iconColor.a = t;
                patternIcons[iconIndex].color = iconColor;

            }

            yield return null;
        }

        for (int i = 0; i < activeIndices.Count; i++)
        {
            int iconIndex = activeIndices[i];
            if (patternIcons[iconIndex] == null)
                continue;

            patternIcons[iconIndex].rectTransform.anchoredPosition = focusedBasePositions[iconIndex];
            Color iconColor = patternIcons[iconIndex].color;
            iconColor.a = 1f;
            patternIcons[iconIndex].color = iconColor;

        }

        focusedBlockTransitionRoutine = null;
    }

    private int GetPatternBlockIndex(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= patternBlockIndexByNote.Count)
            return -1;

        return patternBlockIndexByNote[noteIndex];
    }

    private void EnsureRoundCelebrationRing()
    {
        if (rhythmPanel == null)
            return;

        Transform existing = rhythmPanel.Find("RoundCelebrationRing");
        if (existing == null)
        {
            GameObject ringObject = new GameObject("RoundCelebrationRing", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.SetParent(rhythmPanel, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = focusedPatternPosition;
            rect.sizeDelta = new Vector2(420f, 420f);

            roundCelebrationRingText = ringObject.GetComponent<TextMeshProUGUI>();
            roundCelebrationRingText.text = "◌";
            roundCelebrationRingText.alignment = TextAlignmentOptions.Center;
            roundCelebrationRingText.enableAutoSizing = false;
            roundCelebrationRingText.fontSize = 220f;
            roundCelebrationRingText.color = roundCelebrationColor;
            roundCelebrationRingText.raycastTarget = false;
            if (levelText != null)
                roundCelebrationRingText.font = levelText.font;
        }
        else
        {
            roundCelebrationRingText = existing.GetComponent<TMP_Text>();
        }

        if (roundCelebrationRingText != null)
            roundCelebrationRingText.gameObject.SetActive(false);
    }

    private void PlayRoundCelebration()
    {
        if (roundCelebrationRingText == null)
            return;

        if (currentRoundCelebrationColor.a <= 0f)
            currentRoundCelebrationColor = roundCelebrationColor;

        if (roundCelebrationRoutine != null)
            StopCoroutine(roundCelebrationRoutine);

        roundCelebrationRoutine = StartCoroutine(AnimateRoundCelebration());
    }

    private IEnumerator AnimateRoundCelebration()
    {
        RectTransform rect = roundCelebrationRingText.rectTransform;
        Color startColor = currentRoundCelebrationColor;
        startColor.a = 0f;
        Color peakColor = currentRoundCelebrationColor;
        Color endColor = currentRoundCelebrationColor;
        endColor.a = 0f;

        roundCelebrationRingText.gameObject.SetActive(true);
        roundCelebrationRingText.color = startColor;
        rect.localScale = Vector3.one * 0.55f;

        float elapsed = 0f;
        while (elapsed < roundCelebrationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / roundCelebrationDuration);
            rect.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.4f, t);
            roundCelebrationRingText.color = Color.Lerp(t < 0.35f ? startColor : peakColor, t < 0.35f ? peakColor : endColor, t < 0.35f ? t / 0.35f : (t - 0.35f) / 0.65f);
            yield return null;
        }

        roundCelebrationRingText.gameObject.SetActive(false);
        rect.localScale = Vector3.one;
        roundCelebrationRoutine = null;
    }

    private void EnsureNoteProgressBar()
    {
        if (rootCanvas == null || drumButton == null || noteProgressRoot != null)
            return;

        drumRect = drumButton.transform as RectTransform;
        originalDrumPosition = drumRect.anchoredPosition;
        GameObject bar = new GameObject("NoteProgressBar", typeof(RectTransform));
        noteProgressRoot = bar.GetComponent<RectTransform>();
        noteProgressRoot.SetParent(rootCanvas.transform, false);
        noteProgressRoot.SetSiblingIndex(drumRect.GetSiblingIndex() + 1);
        noteProgressRoot.anchorMin = new Vector2(0.2f, 0f);
        noteProgressRoot.anchorMax = new Vector2(0.8f, 0f);
        noteProgressRoot.pivot = new Vector2(0.5f, 0f);
        noteProgressRoot.anchoredPosition = new Vector2(0f, 14f);
        noteProgressRoot.sizeDelta = new Vector2(0f, 36f);

        noteProgressTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        noteProgressTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x - 15.5f) - 1.5f);
                float dy = Mathf.Max(0f, Mathf.Abs(y - 15.5f) - 1.5f);
                noteProgressTexture.SetPixel(x, y, new Color(1f, 1f, 1f,
                    Mathf.Clamp01(14.5f - Mathf.Sqrt(dx * dx + dy * dy))));
            }
        }
        noteProgressTexture.Apply();
        noteProgressSprite = Sprite.Create(noteProgressTexture, new Rect(0f, 0f, 32f, 32f),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));

        GameObject track = new GameObject("NoteProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        noteProgressTrack = track.GetComponent<RectTransform>();
        noteProgressTrack.SetParent(noteProgressRoot, false);
        noteProgressTrack.anchorMin = Vector2.zero;
        noteProgressTrack.anchorMax = Vector2.one;
        noteProgressTrack.sizeDelta = Vector2.zero;
        Image trackImage = track.GetComponent<Image>();
        noteProgressTrackImage = trackImage;
        trackImage.sprite = noteProgressSprite;
        trackImage.type = Image.Type.Sliced;
        trackImage.color = new Color(0.89f, 0.93f, 0.95f, 1f);
        trackImage.raycastTarget = false;

        GameObject fill = new GameObject("NoteProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.SetParent(noteProgressTrack, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = Vector2.zero;
        noteProgressFill = fill.GetComponent<Image>();
        noteProgressFill.sprite = noteProgressSprite;
        noteProgressFill.type = Image.Type.Sliced;
        noteProgressFill.color = longNoteFillColor;
        noteProgressFill.raycastTarget = false;

        for (int i = 0; i < 3; i++)
        {
            GameObject divider = new GameObject("BeatDivider" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = divider.GetComponent<RectTransform>();
            rect.SetParent(noteProgressTrack, false);
            rect.anchorMin = rect.anchorMax = new Vector2((i + 1) / 4f, 0.5f);
            rect.sizeDelta = new Vector2(5f, 32f);
            beatDividers[i] = divider.GetComponent<Image>();
            beatDividers[i].color = new Color(0.08f, 0.19f, 0.28f, 0.9f);
            beatDividers[i].raycastTarget = false;
        }

        LayoutNoteProgressBar();
        noteProgressRoot.gameObject.SetActive(false);
    }

    private void LayoutNoteProgressBar()
    {
        if (noteProgressRoot == null || drumRect == null)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        lastProgressCanvasSize = canvasRect.rect.size;
        // Reservar sitio debajo del tambor sin desplazar las tarjetas.
        float drumY = Mathf.Max(originalDrumPosition.y, -canvasRect.rect.height * 0.5f + drumRect.rect.height * 0.5f + 65f);
        drumRect.anchoredPosition = new Vector2(originalDrumPosition.x, drumY);
        noteProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            noteProgressTrack.rect.width * noteProgressFill.fillAmount);
    }

    private void LateUpdate()
    {
        if (noteProgressRoot != null && rootCanvas != null &&
            ((RectTransform)rootCanvas.transform).rect.size != lastProgressCanvasSize)
            LayoutNoteProgressBar();
        UpdateTutorialTapHint();
        UpdateProgressTapHint();
    }

    private void EnsureTutorialTapHint()
    {
        if (rootCanvas == null || drumButton == null || tutorialTapHandImage != null)
            return;

        // Mano dibujada como sprite para no depender de fuentes de emoji del dispositivo.
        tutorialHandTexture = new Texture2D(80, 96, TextureFormat.RGBA32, false);
        tutorialHandTexture.filterMode = FilterMode.Bilinear;
        Vector2 fingerStart = new Vector2(41f, 37f);
        Vector2 fingerEnd = new Vector2(41f, 79f);
        Vector2 thumbStart = new Vector2(26f, 28f);
        Vector2 thumbEnd = new Vector2(15f, 44f);
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 80; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 palm = (point - new Vector2(40f, 27f)) / new Vector2(19f, 22f);
                float palmDistance = (palm.magnitude - 1f) * 19f;
                float fingerDistance = DistanceToHandStroke(point, fingerStart, fingerEnd) - 8f;
                float thumbDistance = DistanceToHandStroke(point, thumbStart, thumbEnd) - 7f;
                float cuffDistance = Mathf.Max(Mathf.Abs(point.x - 40f) - 15f,
                    Mathf.Abs(point.y - 9f) - 9f);
                float skinDistance = Mathf.Min(palmDistance, fingerDistance, thumbDistance);
                float outline = Mathf.Clamp01(1f - Mathf.Min(skinDistance, cuffDistance) / 1.5f);
                float skin = Mathf.Clamp01(0.5f - skinDistance);
                float cuff = Mathf.Clamp01(0.5f - cuffDistance);
                Color pixel = new Color(0.13f, 0.22f, 0.32f, outline);
                pixel = Color.Lerp(pixel, new Color(0.99f, 0.79f, 0.58f, 1f), skin);
                pixel = Color.Lerp(pixel, new Color(0.29f, 0.75f, 0.85f, 1f), cuff);
                tutorialHandTexture.SetPixel(x, y, pixel);
            }
        }
        tutorialHandTexture.Apply();
        tutorialHandSprite = Sprite.Create(tutorialHandTexture, new Rect(0f, 0f, 80f, 96f),
            new Vector2(0.5f, 0.5f), 100f);

        GameObject hint = new GameObject("TutorialTapHand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = hint.GetComponent<RectTransform>();
        rect.SetParent(rootCanvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(62f, 76f);
        tutorialTapHandImage = hint.GetComponent<Image>();
        tutorialTapHandImage.sprite = tutorialHandSprite;
        tutorialTapHandImage.raycastTarget = false;
        hint.SetActive(false);

        if (noteProgressRoot != null)
        {
            GameObject progressHint = new GameObject("NextTapHand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform pointer = progressHint.GetComponent<RectTransform>();
            pointer.SetParent(noteProgressRoot, false);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0f);
            pointer.pivot = new Vector2(0.5f, 0f);
            pointer.sizeDelta = new Vector2(50f, 60f);
            progressTapHandImage = progressHint.GetComponent<Image>();
            progressTapHandImage.sprite = tutorialHandSprite;
            progressTapHandImage.raycastTarget = false;
            progressHint.SetActive(false);
        }
    }

    private void UpdateProgressTapHint()
    {
        if (progressTapHandImage == null || noteProgressTrack == null)
            return;

        FigureDefinition note = GetCurrentNote();
        bool visible = waitingForInput && !isPaused && !gameFinished &&
            progressNoteIndex == currentNoteIndex && UsesSegmentedTapFigure(note) &&
            currentNoteTapCount < GetRequiredTapCount(note);
        progressTapHandImage.gameObject.SetActive(visible);
        if (!visible)
            return;

        int required = GetRequiredTapCount(note);
        float nextFraction = (currentNoteTapCount + 1f) / required;
        RectTransform pointer = progressTapHandImage.rectTransform;
        pointer.anchoredPosition = new Vector2((nextFraction - 0.5f) * noteProgressTrack.rect.width, 47f);
        float pulse = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 9f);
        pointer.localScale = Vector3.one * pulse;
        progressTapHandImage.color = new Color(1f, 1f, 1f, 0.95f);
    }

    private static float DistanceToHandStroke(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        return Vector2.Distance(point, start + segment * Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude));
    }

    private void UpdateTutorialTapHint()
    {
        if (tutorialTapHandImage == null || drumRect == null)
            return;

        bool discovery = InTutorialDrumWarmup();
        bool fillPractice = InTutorialFillWarmup();
        FigureDefinition note = GetCurrentNote();
        bool input = waitingForInput && note != null && !IsSilenceFigure(note);
        bool visible = tutorialActive && !isPaused && !gameFinished &&
            (discovery || fillPractice || input);
        tutorialTapHandImage.gameObject.SetActive(visible);
        if (!visible)
            return;

        bool tapNow = discovery || (fillPractice && tutorialFillWarmupWaitingForTap);
        if (input)
        {
            float beatDuration = GetBeatDuration();
            double expectedStart = inputPhaseStartDspTime + GetPatternBeatsBefore(currentNoteIndex) * beatDuration;
            double expectedTap = UsesSegmentedTapFigure(note)
                ? GetExpectedTapTime(note, currentNoteTapCount + 1, expectedStart, beatDuration)
                : expectedStart + note.noteBeats * beatDuration;
            tapNow = System.Math.Abs(AudioSettings.dspTime - expectedTap) <= goodToleranceBeats * beatDuration;
        }

        RectTransform rect = tutorialTapHandImage.rectTransform;
        rect.position = drumRect.TransformPoint(new Vector3(drumRect.rect.width * 0.5f + 26f,
            -drumRect.rect.height * 0.15f, 0f));
        rect.localScale = Vector3.one * (tapNow ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 13f) : 0.85f);
        tutorialTapHandImage.color = new Color(1f, 1f, 1f, tapNow ? 1f : 0.38f);
    }

    private void ShowProgressForNote(int index)
    {
        if (noteProgressRoot == null || index < 0 || index >= noteCountInPattern)
            return;

        progressNoteIndex = index;
        FigureDefinition note = GetPatternNoteAt(index);
        if (failureProgressRoutine != null)
        {
            StopCoroutine(failureProgressRoutine);
            failureProgressRoutine = null;
        }
        failureProgressActive = false;
        noteProgressTrackImage.color = new Color(0.89f, 0.93f, 0.95f, 1f);
        noteProgressFill.color = IsSilenceFigure(note) ? silenceProgressColor : longNoteFillColor;
        noteProgressFill.fillAmount = 0f;
        noteProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
        int sections = UsesSegmentedTapFigure(note) ? GetRequiredTapCount(note) : 1;
        for (int i = 0; i < beatDividers.Length; i++)
        {
            bool visible = i < sections - 1;
            beatDividers[i].gameObject.SetActive(visible);
            if (visible)
            {
                RectTransform divider = beatDividers[i].rectTransform;
                divider.anchorMin = divider.anchorMax = new Vector2((i + 1f) / sections, 0.5f);
            }
        }
        noteProgressRoot.gameObject.SetActive(true);
    }

    private bool UsesSegmentedTapFigure(FigureDefinition figure)
    {
        return figure != null && (figure.type == FigureType.Redonda || figure.type == FigureType.Blanca);
    }

    private bool IsSilenceFigure(FigureDefinition figure)
    {
        return figure != null && figure.type == FigureType.Silencio;
    }

    private bool UsesLongNoteTapCue(FigureDefinition figure)
    {
        return figure != null && !UsesSegmentedTapFigure(figure) && !IsSilenceFigure(figure);
    }

    private int GetRequiredTapCount(FigureDefinition figure)
    {
        if (figure == null)
            return 0;

        switch (figure.type)
        {
            case FigureType.Redonda:
                return 4;
            case FigureType.Blanca:
                return 2;
            default:
                return 0;
        }
    }

    private double GetExpectedTapTime(FigureDefinition figure, int tapIndex, double expectedStart, float beatDuration)
    {
        return expectedStart + Mathf.Clamp(tapIndex, 1, GetRequiredTapCount(figure)) * beatDuration;
    }

    private void UpdateSegmentedTapCues(FigureDefinition figure, float progress)
    {
        int beatCueCount = GetRequiredTapCount(figure);
        if (beatCueCount <= 0)
            return;

        while (sustainedBeatCuesPlayed < beatCueCount)
        {
            float cueThreshold = (sustainedBeatCuesPlayed + 1) / (float)beatCueCount;
            if (progress < cueThreshold)
                break;

            PlaySfx(metronomeClip);
            PulseDrum(timingCueColor);
            sustainedBeatCuesPlayed++;
        }
    }

    private void PlaySuccessCue(FigureDefinition figure)
    {
        if (!UsesSegmentedTapFigure(figure) && !IsSilenceFigure(figure))
            PlaySfx(metronomeClip);
    }

    private float GetBeatDuration()
    {
        return 60f / Mathf.Max(1f, bpm * GameSettings.GameplaySpeed);
    }

    private string GetDemoInstruction(FigureDefinition figure)
    {
        switch (figure.type)
        {
            case FigureType.Redonda:
                return "Redonda: 4 toques";
            case FigureType.Blanca:
                return "Blanca: 2 toques";
            case FigureType.Negra:
                return "Negra: escucha";
            case FigureType.Silencio:
                return "Silencio: espera";
            default:
                return "Corchea: escucha";
        }
    }

    private string GetDemoInstruction()
    {
        if (tutorialActive)
            return "Tutorial: " + GetPatternNoteAt(0).displayName;

        return "Escucha la secuencia";
    }

    private string GetInputInstruction(FigureDefinition figure)
    {
        switch (figure.type)
        {
            case FigureType.Redonda:
                return "Toca 4 veces";
            case FigureType.Blanca:
                return "Toca a la mitad y al final";
            case FigureType.Negra:
                return "Toca al llenarse";
            case FigureType.Silencio:
                return "No toques";
            default:
                return "Toca al llenarse";
        }
    }

    private string GetInputInstruction()
    {
        FigureDefinition note = GetCurrentNote();
        if (note == null)
            note = GetPatternNoteAt(0);

        if (note != null)
            return GetInputInstruction(note);

        return "Sigue la secuencia";
    }

    private void UpdateHud()
    {
        bool scoreChanged = lastShownScore != score;
        int shownLevel = tutorialActive ? -1 : level;
        bool levelChanged = lastShownLevel != shownLevel;
        bool livesChanged = lastShownLives != lives;

        if (scoreText != null)
            scoreText.text = "Puntaje " + score;

        if (levelText != null)
            levelText.text = tutorialActive ? "Tutorial" : "Nivel " + level;

        if (lifeImages == null)
            return;

        for (int i = 0; i < lifeImages.Length; i++)
        {
            if (lifeImages[i] == null)
                continue;

            Color color = lifeImages[i].color;
            color.a = i < lives ? 1f : 0.25f;
            lifeImages[i].color = color;
        }

        if (scoreChanged)
            AnimateScorePanel();

        if (levelChanged)
            AnimateLevelPanel();

        if (livesChanged)
            AnimateLivesPanel();

        lastShownScore = score;
        lastShownLevel = shownLevel;
        lastShownLives = lives;
    }

    private void SetFeedback(string message, Color color, bool forceVisible = false)
    {
        if (statusOverlayText == null)
            return;

        if (!forceVisible && color == normalFeedbackColor && waitingForInput)
        {
            HideStatusOverlay();
            return;
        }

        if (statusOverlayRoutine != null)
            StopCoroutine(statusOverlayRoutine);

        float duration = forceVisible ? Mathf.Max(1.2f, statusMessageLongDuration) :
            color == failFeedbackColor ? statusMessageLongDuration : statusMessageDuration;
        statusOverlayRoutine = StartCoroutine(ShowStatusOverlay(message, color, duration));
    }

    private string ComposeFeedback(string status)
    {
        return status;
    }

    private void UpdateTimingCue(bool active)
    {
        if (currentNoteIndex < 0 || currentNoteIndex >= patternIcons.Count)
            return;

        if (active)
            SetPatternPreviewColor(currentNoteIndex, timingCueColor);
        else
            SetPatternPreviewColor(currentNoteIndex, noteLitColor);
    }

    private void SetPatternPreviewColor(int index, Color color)
    {
        if (index < 0 || index >= patternIcons.Count)
            return;

        HighlightPatternIcon(index, color);
        SetFillColor(index, longNoteFillColor);
    }

    private void PulseDrum(Color pulseColor)
    {
        if (drumButton == null)
            return;

        StopDrumReadyPulse();

        if (drumPulseRoutine != null)
            StopCoroutine(drumPulseRoutine);

        drumPulseRoutine = StartCoroutine(PulseDrumRoutine(pulseColor));
    }

    private IEnumerator PulseDrumRoutine(Color pulseColor)
    {
        Vector3 baseScale = Vector3.one;
        Vector3 targetScale = baseScale * drumPulseScale;

        if (drumButtonImage != null)
            drumButtonImage.color = pulseColor;

        float elapsed = 0f;
        while (elapsed < drumPulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / drumPulseDuration);
            drumButton.transform.localScale = Vector3.Lerp(baseScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < drumPulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / drumPulseDuration);
            drumButton.transform.localScale = Vector3.Lerp(targetScale, baseScale, t);
            yield return null;
        }

        drumButton.transform.localScale = baseScale;
        if (drumButtonImage != null)
            drumButtonImage.color = drumButtonBaseColor;
        drumPulseRoutine = null;

        if (drumButton != null && drumButton.interactable)
            StartDrumReadyPulse();
    }

    private IEnumerator ShowStatusOverlay(string message, Color color, float visibleDuration)
    {
        if (statusOverlayText == null || statusOverlayGroup == null || statusOverlayRect == null)
            yield break;

        statusOverlayText.text = message;
        statusOverlayText.color = color == successFeedbackColor ? new Color(0.05f, 0.36f, 0.19f)
            : color == failFeedbackColor ? new Color(0.49f, 0.12f, 0.17f)
            : new Color(0.08f, 0.18f, 0.3f);
        UpdateStatusOverlayIcon(color);
        PositionStatusAboveFigures();
        statusOverlayGroup.gameObject.SetActive(true);

        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;
        Vector3 peakScale = baseScale * statusMessagePopScale;
        statusOverlayGroup.alpha = 0f;
        statusOverlayRect.localScale = baseScale * 0.92f;

        while (elapsed < statusMessageAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / statusMessageAnimDuration);
            statusOverlayGroup.alpha = t;
            statusOverlayRect.localScale = Vector3.Lerp(baseScale * 0.92f, peakScale, t);
            yield return null;
        }

        statusOverlayGroup.alpha = 1f;
        statusOverlayRect.localScale = peakScale;

        yield return new WaitForSecondsRealtime(visibleDuration);

        elapsed = 0f;
        while (elapsed < statusMessageAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / statusMessageAnimDuration);
            statusOverlayGroup.alpha = 1f - t;
            statusOverlayRect.localScale = Vector3.Lerp(peakScale, baseScale, t);
            yield return null;
        }

        HideStatusOverlay();
        statusOverlayRoutine = null;
    }

    private void HideStatusOverlay()
    {
        if (statusOverlayGroup == null || statusOverlayRect == null)
            return;

        statusOverlayGroup.alpha = 0f;
        statusOverlayRect.localScale = Vector3.one;
        statusOverlayGroup.gameObject.SetActive(false);

        if (statusOverlayIconImage != null)
            statusOverlayIconImage.gameObject.SetActive(false);
    }

    private void EnsureStatusOverlay()
    {
        if (rootCanvas == null)
            return;

        Transform existing = rootCanvas.transform.Find("BasicRhythmStatusOverlay");
        if (existing == null)
        {
            GameObject overlayObject = new GameObject("BasicRhythmStatusOverlay", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.SetParent(rootCanvas.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 210f);
            rect.sizeDelta = new Vector2(620f, 100f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = iconRect.anchorMin;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-125f, 0f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            statusOverlayIconImage = iconObject.GetComponent<Image>();
            statusOverlayIconImage.preserveAspect = true;
            statusOverlayIconImage.raycastTarget = false;

            statusOverlayText = textObject.GetComponent<TextMeshProUGUI>();
            statusOverlayText.alignment = TextAlignmentOptions.Center;
            statusOverlayText.enableAutoSizing = true;
            statusOverlayText.fontSizeMin = 18f;
            statusOverlayText.fontSizeMax = 28f;
            statusOverlayText.fontStyle = FontStyles.Bold;
            statusOverlayText.textWrappingMode = TextWrappingModes.Normal;
            statusOverlayText.raycastTarget = false;
            statusOverlayText.margin = Vector4.zero;

            if (levelText != null)
                statusOverlayText.font = levelText.font;

            statusOverlayGroup = overlayObject.GetComponent<CanvasGroup>();
            statusOverlayRect = rect;
        }
        else
        {
            statusOverlayGroup = existing.GetComponent<CanvasGroup>();
            statusOverlayRect = existing as RectTransform;
            statusOverlayText = existing.GetComponentInChildren<TMP_Text>(true);
            statusOverlayIconImage = existing.GetComponentInChildren<Image>(true);
        }

        // Dejar libre el espacio sobre las figuras para el mensaje, sin invadir el tambor.
        if (rhythmPanel != null)
            rhythmPanel.anchoredPosition = new Vector2(rhythmPanel.anchoredPosition.x, -40f);

        if (statusOverlayRect != null)
        {
            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            statusOverlayRect.anchorMin = statusOverlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusOverlayRect.pivot = new Vector2(0.5f, 0.5f);
            statusOverlayRect.sizeDelta = new Vector2(Mathf.Min(620f, canvasRect.rect.width - 32f), 100f);
            PositionStatusAboveFigures();
            statusOverlayRect.SetAsLastSibling();
        }

        if (statusOverlayGroup != null)
        {
            statusOverlayGroup.interactable = false;
            statusOverlayGroup.blocksRaycasts = false;
        }

        HideStatusOverlay();
    }

    private void PositionStatusAboveFigures()
    {
        if (statusOverlayRect == null || rootCanvas == null)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        float figureTop = 120f;
        foreach (Image figure in patternIcons)
        {
            if (figure != null && figure.gameObject.activeInHierarchy)
                figureTop = Mathf.Max(figureTop,
                    RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, figure.rectTransform).max.y);
        }

        statusOverlayRect.anchoredPosition = new Vector2(0f,
            Mathf.Max(210f, figureTop + statusOverlayRect.rect.height * Mathf.Max(1f, statusMessagePopScale) * 0.5f + 18f));
    }

    private void UpdateStatusOverlayIcon(Color color)
    {
        if (statusOverlayIconImage == null)
            return;

        // Las instrucciones van sin ilustración: sólo el acierto muestra un check junto al mensaje.
        Sprite icon = color == successFeedbackColor ? successStatusSprite : null;

        if (icon == null)
        {
            statusOverlayIconImage.gameObject.SetActive(false);
            return;
        }

        statusOverlayIconImage.sprite = icon;
        statusOverlayIconImage.color = Color.white;
        statusOverlayIconImage.gameObject.SetActive(true);
    }

    private void AnimateScorePanel()
    {
        scoreHudRoutine = RestartHudRoutine(scoreHudRoutine, scorePanelRect, scorePanelBaseScale);
    }

    private void AnimateLevelPanel()
    {
        levelHudRoutine = RestartHudRoutine(levelHudRoutine, levelPanelRect, levelPanelBaseScale);
    }

    private void AnimateLivesPanel()
    {
        livesHudRoutine = RestartHudRoutine(livesHudRoutine, livesPanelRect, livesPanelBaseScale);
    }

    private Coroutine RestartHudRoutine(Coroutine currentRoutine, RectTransform rect, Vector3 baseScale)
    {
        if (rect == null)
            return currentRoutine;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        return StartCoroutine(AnimateHudPanelRoutine(rect, baseScale));
    }

    private IEnumerator AnimateHudPanelRoutine(RectTransform rect, Vector3 baseScale)
    {
        float elapsed = 0f;
        Vector3 peakScale = baseScale * hudPopScale;
        while (elapsed < hudAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / hudAnimDuration);
            rect.localScale = Vector3.Lerp(baseScale, peakScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < hudAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / hudAnimDuration);
            rect.localScale = Vector3.Lerp(peakScale, baseScale, t);
            yield return null;
        }

        rect.localScale = baseScale;
    }

    private void SetDrumInteractable(bool value)
    {
        if (drumButton == null)
            return;

        drumButton.interactable = value;
        if (value)
            StartDrumReadyPulse();
        else
            StopDrumReadyPulse();
    }

    private void StartDrumReadyPulse()
    {
        if (drumButton == null || !drumButton.interactable || drumPulseRoutine != null || drumReadyRoutine != null)
            return;

        drumReadyRoutine = StartCoroutine(DrumReadyPulseRoutine());
    }

    private void StopDrumReadyPulse()
    {
        if (drumReadyRoutine != null)
        {
            StopCoroutine(drumReadyRoutine);
            drumReadyRoutine = null;
        }

        if (drumButton != null)
            drumButton.transform.localScale = Vector3.one;
    }

    private IEnumerator DrumReadyPulseRoutine()
    {
        while (drumButton != null && drumButton.interactable)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * drumReadyPulseSpeed) * (drumReadyPulseScale - 1f);
            drumButton.transform.localScale = Vector3.one * pulse;
            yield return null;
        }

        if (drumButton != null)
            drumButton.transform.localScale = Vector3.one;

        drumReadyRoutine = null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    private void PlayVoice(AudioClip clip)
    {
        if (clip == null || voiceSource == null || GameSettings.EffectsVolume <= 0f)
            return;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    private IEnumerator PlayPreRoundVoiceIfNeeded()
    {
        if (voiceSource == null)
            yield break;

        if (tutorialActive)
        {
            AudioClip tutorialClip = GetTutorialVoiceClip();
            if (tutorialClip != null)
                yield return StartCoroutine(PlayBlockingVoice(tutorialClip));

            yield break;
        }

        if (observeVoiceClip != null)
            yield return StartCoroutine(PlayBlockingVoice(observeVoiceClip));
    }

    private IEnumerator PlayBlockingVoice(AudioClip clip)
    {
        if (clip == null || voiceSource == null || GameSettings.EffectsVolume <= 0f)
            yield break;

        PlayVoice(clip);
        while (voiceSource.isPlaying)
            yield return null;
    }

    private AudioClip GetTutorialVoiceClip()
    {
        FigureDefinition firstNote = GetPatternNoteAt(0);
        if (firstNote == null)
            return null;

        switch (firstNote.type)
        {
            case FigureType.Redonda:
                return redondaVoiceClip;
            case FigureType.Blanca:
                return blancaVoiceClip;
            case FigureType.Negra:
                return negraVoiceClip;
            case FigureType.Silencio:
                return silencioVoiceClip;
            default:
                return corcheaVoiceClip;
        }
    }

    private void BuildFigureDefinitions()
    {
        figures.Clear();
        AddFigure(FigureType.Redonda, "Redonda", 4f, redondaSprite);
        AddFigure(FigureType.Blanca, "Blanca", 2f, blancaSprite, blancaASprite, blancaBSprite);
        AddFigure(FigureType.Negra, "Negra", 1f, negraSprite);
        AddFigure(FigureType.Corchea, "Corchea", 0.5f, corcheaSprite, corcheaMaSprite, corcheaRiSprite, corcheaPoSprite, corcheaSaSprite);
        AddFigure(FigureType.Silencio, "Silencio", silencioBeats, silencioSprite);
    }

    private void AssignChatterboxVoices()
    {
        tutorialIntroClip = NarrationPlayer.FindClip("basico_tutorial_intro") ?? tutorialIntroClip;
        drumDiscoveryVoiceClip = NarrationPlayer.FindClip("basico_descubre_tambor") ?? drumDiscoveryVoiceClip;
        fillTimingVoiceClip = NarrationPlayer.FindClip("basico_espera_llenado") ?? fillTimingVoiceClip;
        redondaVoiceClip = NarrationPlayer.FindClip("basico_redonda") ?? redondaVoiceClip;
        blancaVoiceClip = NarrationPlayer.FindClip("basico_blanca") ?? blancaVoiceClip;
        negraVoiceClip = NarrationPlayer.FindClip("basico_negra") ?? negraVoiceClip;
        corcheaVoiceClip = NarrationPlayer.FindClip("basico_corchea") ?? corcheaVoiceClip;
        silencioVoiceClip = NarrationPlayer.FindClip("basico_silencio") ?? silencioVoiceClip;
        observeVoiceClip = NarrationPlayer.FindClip("voz_observa") ?? observeVoiceClip;
        yourTurnVoiceClip = NarrationPlayer.FindClip("voz_tu_turno") ?? yourTurnVoiceClip;
        veryGoodVoiceClip = NarrationPlayer.FindClip("voz_muy_bien") ?? veryGoodVoiceClip;
        waitALittleVoiceClip = NarrationPlayer.FindClip("voz_espera") ?? waitALittleVoiceClip;
        tryAgainVoiceClip = NarrationPlayer.FindClip("voz_intenta_otra_vez") ?? tryAgainVoiceClip;
        endVoiceClip = NarrationPlayer.FindClip("basico_fin") ?? endVoiceClip;
    }

    private void InitializeFigureMetrics()
    {
        figureCounters.Clear();
        figureMetrics = new BasicRhythmMetricsData();
        foreach (FigureType type in System.Enum.GetValues(typeof(FigureType)))
        {
            BasicRhythmFigureMetric metric = new BasicRhythmFigureMetric { figure = type.ToString().ToLowerInvariant() };
            figureCounters.Add(type, metric);
            figureMetrics.figures.Add(metric);
        }
    }

    private void RecordFigureSuccess(FigureDefinition figure)
    {
        if (figure == null || !figureCounters.TryGetValue(figure.type, out BasicRhythmFigureMetric metric))
            return;

        if (tutorialActive)
            metric.tutorialCorrectNotes++;
        else
            metric.correctNotes++;
        RefreshFigureAccuracy(metric);
    }

    private void RecordFigureFailure(FigureDefinition figure)
    {
        if (figure == null || !figureCounters.TryGetValue(figure.type, out BasicRhythmFigureMetric metric))
            return;

        if (tutorialActive)
            metric.tutorialFailedNotes++;
        else
            metric.failedNotes++;
        RefreshFigureAccuracy(metric);
    }

    private static void RefreshFigureAccuracy(BasicRhythmFigureMetric metric)
    {
        int assessed = metric.correctNotes + metric.failedNotes;
        metric.accuracyPercent = assessed > 0 ? 100f * metric.correctNotes / assessed : 0f;
    }

    private void RecordTimedTap(FigureDefinition figure, double signedOffsetSeconds)
    {
        if (figure == null || figureMetrics == null)
            return;

        float offsetMs = (float)(signedOffsetSeconds * 1000.0);
        float beatMs = Mathf.Max(1f, GetBeatDuration() * 1000f);
        figureMetrics.taps.Add(new BasicRhythmTapMetric
        {
            figure = figure.type.ToString().ToLowerInvariant(),
            noteIndex = currentNoteIndex,
            tapIndex = currentNoteTapCount + 1,
            timeSeconds = (float)(AudioSettings.dspTime - attemptStartedDspTime),
            offsetMs = offsetMs,
            result = figure.type == FigureType.Silencio ? "unexpected" :
                Mathf.Abs(offsetMs) <= perfectToleranceBeats * beatMs ? "perfect" :
                Mathf.Abs(offsetMs) <= goodToleranceBeats * beatMs ? "good" :
                offsetMs < 0f ? "early" : "late",
            tutorial = tutorialActive,
        });

        if (figureMetrics.taps.Count >= TapChunkSize && !attemptReportSubmitted &&
            SessionManager.Instance != null && SessionManager.Instance.HasActiveSession &&
            GameReportManager.Instance != null && PlayerProfileManager.Instance != null &&
            PlayerProfileManager.Instance.HasActiveProfile)
            ReportAttempt(false, true);

        if (tutorialActive || figure.type == FigureType.Silencio ||
            !figureCounters.TryGetValue(figure.type, out BasicRhythmFigureMetric metric))
            return;

        metric.judgedTaps++;
        if (offsetMs < 0f) metric.earlyTaps++;
        else if (offsetMs > 0f) metric.lateTaps++;
        metric.meanSignedOffsetMs += (offsetMs - metric.meanSignedOffsetMs) / metric.judgedTaps;
        metric.meanAbsoluteOffsetMs += (Mathf.Abs(offsetMs) - metric.meanAbsoluteOffsetMs) / metric.judgedTaps;
    }

    private void AddFigure(FigureType type, string displayName, float noteBeats, Sprite sprite, params Sprite[] orderedSprites)
    {
        if (sprite == null)
            return;

        figures.Add(new FigureDefinition
        {
            type = type,
            displayName = displayName,
            noteBeats = noteBeats,
            sprite = sprite,
            orderedSprites = orderedSprites,
        });
    }

    private void ResolveReferences()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
            return;

        rootCanvas = canvas;
        Transform topHudPanel = FindChild(canvas.transform, "TopHudPanel");
        ApplyTopHudStyle(topHudPanel);
        ResolveTopHudView(topHudPanel);

        figureIconTemplate = FindImage(canvas, "FigureIcon");
        drumButton = FindButton(canvas, "DrumButton");
        menuButton = topHudPanelView != null && topHudPanelView.MenuButton != null
            ? topHudPanelView.MenuButton
            : FindButton(canvas, "MenuButton");
        Transform pausePanelTransform = FindChild(canvas.transform, "PausePanel");
        if (pausePanelTransform != null)
        {
            pausePanel = pausePanelTransform.gameObject;
            resumeButton = FindButton(pausePanelTransform, "ResumenButton") ?? FindButton(pausePanelTransform, "ResumeButton");
            restartButton = FindButton(pausePanelTransform, "RestartButton");
            closeButton = FindButton(pausePanelTransform, "CloseButton");
            pauseMenuButton = FindButton(pausePanelTransform, "MenuButton");
        }

        if (drumButton != null)
        {
            drumButtonImage = drumButton.GetComponent<Image>();
            if (drumButtonImage != null)
                drumButtonBaseColor = drumButtonImage.color;
        }

        Transform rhythmPanelTransform = FindChild(canvas.transform, "RhythmPanel");
        if (rhythmPanelTransform != null)
            rhythmPanel = rhythmPanelTransform as RectTransform;

        Transform scorePanel = FindChild(topHudPanel, "ScorePanel") ?? FindChild(canvas.transform, "ScorePanel");
        Transform levelPanel = FindChild(topHudPanel, "LevelPanel") ?? FindChild(canvas.transform, "LevelPanel");
        Transform livesPanel = FindChild(topHudPanel, "LifeIcons") ?? FindChild(topHudPanel, "LivesPanel") ?? FindChild(canvas.transform, "LivesPanel");

        if (topHudPanelView != null)
        {
            scoreText = topHudPanelView.ScoreText;
            lifeImages = topHudPanelView.LifeIcons;
        }

        if (scorePanel != null)
        {
            if (scoreText == null)
                scoreText = scorePanel.GetComponentInChildren<TMP_Text>(true);
            scorePanelRect = scorePanel as RectTransform;
            if (scorePanelRect != null)
                scorePanelBaseScale = scorePanelRect.localScale;
        }

        if (levelPanel != null)
        {
            levelText = levelPanel.GetComponentInChildren<TMP_Text>(true);
            levelPanelRect = levelPanel as RectTransform;
            if (levelPanelRect != null)
                levelPanelBaseScale = levelPanelRect.localScale;
        }

        if (livesPanel != null)
        {
            livesPanelRect = livesPanel as RectTransform;
            if (livesPanelRect != null)
                livesPanelBaseScale = livesPanelRect.localScale;

            if (lifeImages == null || lifeImages.Length < 3)
                lifeImages = FindLifeImages(livesPanel);
        }

        HideLegacyFigurePlaceholders();

        if (pausePanel != null)
        {
            pausePanelView = pausePanel.GetComponent<PausePanelView>();
            if (pausePanelView == null)
                pausePanelView = pausePanel.AddComponent<PausePanelView>();

            pausePanelView.InitializeHidden();
        }
    }

    private void ResolveTopHudView(Transform topHudPanel)
    {
        if (topHudPanel == null)
            return;

        topHudPanelView = topHudPanel.GetComponent<TopHudPanelView>();
        if (topHudPanelView == null)
            topHudPanelView = topHudPanel.gameObject.AddComponent<TopHudPanelView>();

        topHudPanelView.ResolveMissingReferences();
    }

    private void EnsureAudioSource()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxBaseVolume = sfxSource.volume;

        AudioSource[] sources = GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != sfxSource)
            {
                voiceSource = sources[i];
                break;
            }
        }

        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceBaseVolume = voiceSource.volume;
        ApplyGameSettings();
    }

    private void ApplyGameSettings()
    {
        if (sfxSource != null)
            sfxSource.volume = sfxBaseVolume * GameSettings.EffectsVolume;

        if (voiceSource != null)
            voiceSource.volume = voiceBaseVolume * GameSettings.EffectsVolume;
    }

    private void EnsureDrumInput()
    {
        if (drumButton == null)
            return;

        EventTrigger trigger = drumButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = drumButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();
        AddTrigger(trigger, EventTriggerType.PointerDown, _ => OnDrumPointerDown());
        AddTrigger(trigger, EventTriggerType.PointerUp, _ => OnDrumPointerUp());
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => OnDrumPointerUp());

        if (menuButton != null)
        {
            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(PauseGame);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ResumeGame);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartScene);
        }

        if (pauseMenuButton != null)
        {
            pauseMenuButton.onClick.RemoveAllListeners();
            pauseMenuButton.onClick.AddListener(LoadMainScene);
        }
    }

    private void PauseGame()
    {
        if (pausePanel == null || isPaused || gameFinished)
            return;

        isPaused = true;
        pausedAtDspTime = AudioSettings.dspTime;
        if (pausePanelView != null)
            pausePanelView.Show();
        else
            pausePanel.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (drumButton != null)
            SetDrumInteractable(false);
    }

    private void ResumeGame()
    {
        if (!isPaused || gameFinished)
            return;

        if (waitingForInput)
            inputPhaseStartDspTime += System.Math.Max(0d, AudioSettings.dspTime - pausedAtDspTime);
        isPaused = false;
        if (pausePanelView != null)
            pausePanelView.Hide();
        else if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (drumButton != null)
            SetDrumInteractable(waitingForInput && lives > 0);
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        attemptExitReason = "restart";
        ReportAttempt(false);
        SceneTransitionController.ReloadCurrentScene();
    }

    private void LoadMainScene()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        attemptExitReason = lives > 0 ? "return_to_menu" : "game_over_return";
        ReportAttempt(false);
        SceneTransitionController.LoadScene("MainScene");
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (figureIconTemplate == null || rhythmPanel == null)
        {
            Debug.LogError("BasicRhythmGameManager: Falta FigureIcon o RhythmPanel.");
            valid = false;
        }

        if (drumButton == null)
        {
            Debug.LogError("BasicRhythmGameManager: Falta DrumButton.");
            valid = false;
        }

        if (scoreText == null)
        {
            Debug.LogError("BasicRhythmGameManager: No se pudo resolver ScoreText.");
            valid = false;
        }

        if (livesPanelRect != null &&
            (lifeImages == null || lifeImages.Length < 3 || lifeImages[0] == null || lifeImages[1] == null || lifeImages[2] == null))
        {
            Debug.LogError("BasicRhythmGameManager: No se pudieron resolver las 3 vidas.");
            valid = false;
        }

        if (redondaSprite == null || blancaSprite == null || negraSprite == null || corcheaSprite == null)
        {
            Debug.LogError("BasicRhythmGameManager: Faltan sprites de figuras.");
            valid = false;
        }

        if (blancaASprite == null || blancaBSprite == null)
        {
            Debug.LogError("BasicRhythmGameManager: Faltan BlancaA o BlancaB.");
            valid = false;
        }

        if (corcheaMaSprite == null || corcheaRiSprite == null || corcheaPoSprite == null || corcheaSaSprite == null)
        {
            Debug.LogError("BasicRhythmGameManager: Faltan variantes de corchea ma-ri-po-sa.");
            valid = false;
        }

        if (silencioSprite == null)
        {
            Debug.LogError("BasicRhythmGameManager: Falta SilencioSprite.");
            valid = false;
        }

        return valid;
    }

    private static Image FindImage(Component root, string objectName)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].name == objectName)
                return images[i];
        }

        return null;
    }

    private static Button FindButton(Component root, string objectName)
    {
        if (root == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
                return buttons[i];
        }

        return null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == objectName)
                return child;

            Transform nested = FindChild(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static Image[] FindLifeImages(Transform livesPanel)
    {
        Image[] namedLives =
        {
            FindImage(livesPanel, "Life1"),
            FindImage(livesPanel, "Life2"),
            FindImage(livesPanel, "Life3"),
        };

        if (namedLives[0] != null || namedLives[1] != null || namedLives[2] != null)
            return namedLives;

        return livesPanel != null ? livesPanel.GetComponentsInChildren<Image>(true) : new Image[0];
    }

    private static void ApplyTopHudStyle(Transform topHud)
    {
        if (topHud == null)
            return;

        TopHudPanelStyle style = topHud.GetComponent<TopHudPanelStyle>();
        if (style == null)
            style = topHud.gameObject.AddComponent<TopHudPanelStyle>();

        style.Apply();
    }

    private void HideLegacyFigurePlaceholders()
    {
        if (rhythmPanel == null)
            return;

        string[] namesToHide =
        {
            "FigureIcon1",
            "FigureIcon2",
            "FigureIcon3",
            "FigureIcon4",
            "FigureNameText",
        };

        for (int i = 0; i < namesToHide.Length; i++)
        {
            Transform target = FindChild(rhythmPanel, namesToHide[i]);
            if (target != null)
                target.gameObject.SetActive(false);
        }
    }

    private void ReportAttempt(bool completed, bool checkpoint = false)
    {
        if ((!checkpoint && attemptReportSubmitted) || GameReportManager.Instance == null ||
            SessionManager.Instance == null || !SessionManager.Instance.HasActiveSession ||
            (checkpoint && figureMetrics.taps.Count < TapChunkSize))
            return;

        if (!checkpoint)
            attemptReportSubmitted = true;
        int savedTapCount = figureMetrics.taps.Count;
        figureMetrics.chunkIndex = reportChunkIndex;
        AttemptReportData report = new AttemptReportData
        {
            miniGame = "basic_rhythm",
            level = tutorialActive ? 0 : Mathf.Max(1, level),
            difficulty = tutorialActive ? "tutorial" : (patternMeasures > 2 ? "media" : "facil"),
            bpm = Mathf.RoundToInt(bpm * GameSettings.GameplaySpeed),
            errors = attemptErrors,
            correctAnswers = attemptCorrectAnswers,
            levelRepetitions = attemptLevelRepetitions,
            completed = completed,
            timeSeconds = Mathf.Max(1, Mathf.RoundToInt(Time.time - attemptStartedAt)),
            scoreFinal = score,
            wasTutorial = tutorialStepIndex > 0 || tutorialActive,
            exitReason = checkpoint ? "checkpoint" : attemptExitReason,
            basicRhythm = figureMetrics,
        };

        GameReportManager.Instance.ReportAttempt(report, () =>
        {
            if (!checkpoint) return;
            figureMetrics.taps.RemoveRange(0, Mathf.Min(savedTapCount, figureMetrics.taps.Count));
            reportChunkIndex++;
        }, error => Debug.LogWarning("BasicRhythm report error: " + error));
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
