using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
#if ENABLE_INPUT_SYSTEM && (UNITY_EDITOR || UNITY_STANDALONE)
using UnityEngine.InputSystem;
#endif

public class TempoTapGameManager : MonoBehaviour
{
    private const int TapChunkSize = 256;
    public BeatController beatController;

    [Header("UI")]
    public TMP_Text feedbackText;
    [SerializeField] private OutcomeFeedbackView outcomeFeedback;
    public TMP_Text timerText;
    public TMP_Text scoreText;
    public TMP_Text streakText;
    public TMP_Text bpmText; // Insignia central del HUD; se conserva el nombre para las escenas existentes.
    public Image stabilityFill;

    [Header("UI Panels")]
    public GameObject startPanel;
    [SerializeField] private GameObject pausePanel;

    TMP_Text startButtonText;
    PausePanelView pausePanelView;
    Button pauseTrigger;
    bool isPaused;

    [Header("Timing Windows (ms)")]
    public float perfectMs = 60f;
    public float goodMs = 120f;

    [Header("Inicio")]
    [Range(3, 6)] public int warmupBeats = 5;
    [Range(1, 3)] public int observeBeats = 2;
    [Header("Tempo adaptativo")]
    public float minimumAdaptiveBpm = 30f;
    public float maximumAdaptiveBpm = 90f;
    [Range(4, 16)] public int beatsPerAdjustment = 8;

    [Header("Score")]
    public int perfectScore = 100;
    public int goodScore = 60;
    public int timingOffsetScore = 20;
    [Tooltip("Penalización mínima solo para toques a 40 % del intervalo o más del pulso más cercano.")]
    [Min(0)] public int veryFarPenalty = 5;

    [Header("Stability")]
    [Range(0f, 1f)] public float stability = 1f;
    public float gainOnHit = 0.06f;
    public float lossOnMiss = 0.12f;

    [Header("Tap SFX (opcional)")]
    public AudioSource sfxSource;
    public AudioClip tapCorrect;
    public AudioClip tapWrong;

    [Header("Runner")]
    public RunnerController2D runner;
    public bool jumpOnGood = true; // si "Bien" también salta
    public float perfectJumpMultiplier = 1f;
    public float goodJumpMultiplier = 1f;
    public float earlyAssistJumpMultiplier = 1f;
    public float lateAssistJumpMultiplier = 1.08f;

    [Header("Adaptive Jump")]
    public float perfectTargetContactTime = 0.34f;
    public float goodEarlyTargetContactTime = 0.4f;
    public float goodLateTargetContactTime = 0.3f;
    public float assistEarlyTargetContactTime = 0.46f;
    public float assistLateTargetContactTime = 0.24f;

    [Header("Asistencia (siempre activa)")]
    public float assistMs = 180f;   // ventana extra para que igual salte
    public float assistedJumpMultiplier = 0.9f; // salto un poco más bajo si quieres

    [Header("Control")]
    public bool ignoreExtraTapsOnSameBeat = true;

    public BeatSyncedObstacleSpawner obstacleSpawner;

    [Header("Música Runner: un kit aleatorio durante 60 segundos")]
    public MusicKit[] musicKits;
    [Min(1f)] public float levelDurationSeconds = 60f;
    private MusicKit currentKit;
    private AudioSource songSource;
    private AudioClip originalClick;
    private double levelEndDspTime;
    private double remainingLevelSeconds;

    bool running;
    float baseBpm;
    float sessionStartedAt;
    double lastJudgedBeatTime = double.NaN;
    double lastMissedBeatTime = double.NaN;
    double firstJudgedBeatDspTime;
    bool warmupPending;
    bool turnVoicePlayed;
    float originalBeatDelay;
    double firstDemoBeatDspTime;
    double calibrationStartDspTime;
    double sessionStartDspTime;
    float startingBpm;
    float pendingBpm;
    int recentBeats;
    int recentAccurateBeats;
    int highestDifficultyStage;
    Coroutine tutorialRoutine;
    readonly List<double> calibrationTaps = new List<double>();
    readonly List<TempoTapTapMetric> tapDetails = new List<TempoTapTapMetric>();
    int reportChunkIndex;
    bool pausedByApplication;
    float lastStartPressTime = -10f;
    Coroutine feedbackClearRoutine;
    int score;
    int streak;
    int totalTapsJudged;
    int physicalTapsJudged;
    int bestStreak;
    int perfectCount;
    int goodCount;
    int earlyCount;
    int lateCount;
    int missCount;
    int farNoPointsCount;
    int veryFarCount;
    int collisionCount;
    double sumOffsetMs;
    double sumAbsoluteOffsetMs;
    double lastTapDspTime = double.NaN;
    int intervalCount;
    double sumIntervalMs;
    double sumSquaredIntervalMs;
    TMP_Text stabilityLabel;

    public bool IsAcceptingTaps => running && !pausedByApplication && !warmupPending && beatController != null && beatController.IsRunning;
    public bool IsSessionActive => running;
    public float CurrentPerfectMs => Mathf.Lerp(perfectMs, perfectMs * 0.72f, Mathf.Clamp01(streak / 20f));
    public float CurrentGoodMs => beatController == null ? goodMs : Mathf.Min(
        Mathf.Lerp(goodMs, goodMs * 0.8f, Mathf.Clamp01(streak / 20f)),
        (float)beatController.IntervalSec * 230f);
    // Keep the assisted window wider than the Good window, leaving room for
    // near taps and for non-scoring taps before the very-far penalty.
    public float CurrentAssistMs => beatController == null ? assistMs : Mathf.Min(assistMs, (float)beatController.IntervalSec * 320f);

    public string GetPromptForBeat(double beatTime)
    {
        if (warmupPending && beatController != null && beatController.IsRunning)
            return beatTime >= calibrationStartDspTime - 0.01 ? "PRUEBA\nAL LLEGAR" : "OBSERVA";

        if (!IsAcceptingTaps || beatTime < firstJudgedBeatDspTime - 0.01)
            return "ESCUCHA";

        BeatSyncedObstacleSpawner.ObstacleAction action = obstacleSpawner != null
            ? obstacleSpawner.GetActionForBeat(beatTime) : BeatSyncedObstacleSpawner.ObstacleAction.Tap;
        return action == BeatSyncedObstacleSpawner.ObstacleAction.Jump ? "SALTA\nAL PULSO"
            : action == BeatSyncedObstacleSpawner.ObstacleAction.Duck ? "DESLÍZATE\nAL PULSO" : "TOCA\nAL PULSO";
    }


    void Start()
    {
        LandscapePhoneCanvas.Configure(GetComponentInChildren<Canvas>() ?? FindFirstObjectByType<Canvas>());
        Time.timeScale = 1f;
        AudioListener.pause = false;
        running = false;
        ConfigureHud();
        ResetSessionStats();
        UpdateUI();

        if (beatController != null)
        {
            baseBpm = beatController.bpm;
            originalClick = beatController.metronomeClick;
            originalBeatDelay = beatController.startDelaySeconds;
            beatController.StopBeats();
            beatController.OnBeat += CheckMissedBeat;
            beatController.OnBeat += HandleTutorialBeat;
        }

        if (runner != null)
            runner.SetGameStarted(false);

        if (obstacleSpawner != null)
            obstacleSpawner.StopSpawner();

        if (startPanel != null)
        {
            startPanel.SetActive(true);
            Image panelImage = startPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.clear;
                panelImage.raycastTarget = false;
            }
            startButtonText = startPanel.GetComponentInChildren<TMP_Text>(true);
            if (startButtonText != null)
            {
                startButtonText.text = "JUGAR";
                startButtonText.raycastTarget = false;
            }
        }

        ConfigurePauseMenu();

        SetFeedback("Pulsa JUGAR para escuchar el pulso", 0f);
        Canvas tutorialCanvas = startPanel != null ? startPanel.GetComponentInParent<Canvas>() : null;
        BeatGuideUI tutorialGuide = FindFirstObjectByType<BeatGuideUI>(FindObjectsInactive.Include);
        Transform jumpFocus = tutorialGuide != null ? tutorialGuide.transform : null;
        Transform startFocus = startPanel != null ? startPanel.transform.Find("StartButton") : null;
        if (jumpFocus == null || startFocus == null ||
            !GameTutorialOverlay.Show(tutorialCanvas, "tempo_tutorial",
                new[] { jumpFocus, jumpFocus, startFocus },
                new[] { "Escucha los pulsos y toca la pantalla siguiendo el ritmo.",
                    "Mira el indicador: cuando diga SALTA, toca justo al pulso.",
                    "Sigue la señal para saltar o deslizarte. ¡Jugar!" },
                hideWorld: runner != null ? runner.transform : null,
                playButton: startFocus.GetComponent<UnityEngine.UI.Button>()))
            NarrationPlayer.Say("tempo_intro", interrupt: false);
    }

    private void OnDestroy()
    {
        if (beatController != null) beatController.metronomeClick = originalClick;
        if (beatController != null)
        {
            beatController.OnBeat -= CheckMissedBeat;
            beatController.OnBeat -= HandleTutorialBeat;
        }
    }

    private void LateUpdate()
    {
        if (feedbackText != null)
            feedbackText.rectTransform.anchoredPosition = new Vector2(0f,
                LandscapePhoneCanvas.IsWidePhone ? 150f : 240f);
    }

    private void ConfigureHud()
    {
        if (startPanel != null)
        {
            RectTransform panel = startPanel.GetComponent<RectTransform>();
            if (panel != null && bpmText != null)
            {
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.one;
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = Vector2.zero;
            }
            RectTransform start = startPanel.transform.Find("StartButton") as RectTransform;
            RectTransform back = startPanel.transform.Find("BackButton") as RectTransform;
            if (start != null)
            {
                start.anchorMin = start.anchorMax = new Vector2(0.5f, 0.5f);
                start.pivot = new Vector2(0.5f, 0.5f);
                start.anchoredPosition = new Vector2(0f, -245f);
                start.sizeDelta = new Vector2(280f, 90f);
            }
            if (back != null && bpmText == null)
            {
                back.anchorMin = back.anchorMax = new Vector2(1f, 0.5f);
                back.pivot = new Vector2(1f, 0.5f);
                back.anchoredPosition = new Vector2(-32f, 0f);
                back.sizeDelta = new Vector2(170f, 72f);
                back.GetComponent<Image>().preserveAspect = true;
            }
        }

        if (timerText != null)
        {
            // La racha pasa a la insignia central, no se duplica sobre el paisaje.
            timerText.gameObject.SetActive(bpmText == null);
            RectTransform rect = timerText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(32f, -210f);
            rect.sizeDelta = new Vector2(346f, 132f);
            timerText.fontSize = 32f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.raycastTarget = false;
        }
        if (streakText != null && streakText != bpmText)
            streakText.gameObject.SetActive(bpmText == null);

        if (feedbackText != null)
        {
            RectTransform rect = feedbackText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 240f);
            rect.sizeDelta = new Vector2(520f, 90f);
            feedbackText.enableAutoSizing = true;
            feedbackText.fontSizeMin = 22f;
            feedbackText.fontSizeMax = 30f;
            feedbackText.fontSize = 30f;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.color = new Color(0.08f, 0.22f, 0.32f, 1f);
            feedbackText.textWrappingMode = TextWrappingModes.Normal;
            feedbackText.raycastTarget = false;
        }

        // Stability still drives adaptive difficulty, but its bar is not a player goal.
        if (stabilityFill != null)
            stabilityFill.transform.parent.gameObject.SetActive(false);
    }

    private void CheckMissedBeat(double currentBeatTime)
    {
        if (!IsAcceptingTaps || beatController == null)
            return;

        double previousBeat = currentBeatTime - beatController.IntervalSec;
        bool missed = previousBeat >= firstJudgedBeatDspTime - 0.01 &&
            (double.IsNaN(lastJudgedBeatTime) || Math.Abs(previousBeat - lastJudgedBeatTime) >= 0.03) &&
            (double.IsNaN(lastMissedBeatTime) || Math.Abs(previousBeat - lastMissedBeatTime) >= 0.03);

        if (missed)
        {
            lastMissedBeatTime = previousBeat;
            missCount++;
            totalTapsJudged++;
            streak = 0;
            stability = Mathf.Max(0f, stability - lossOnMiss);
            obstacleSpawner?.SetDifficulty(0);
            RecordTapDetail(previousBeat, previousBeat, 0f, 0f, "no_tap", false);
            RecordRecentBeat(false);
            SetFeedback("Escucha el pulso. Vamos de nuevo", 0.8f);
            UpdateUI();
        }

        ApplyPendingTempo(currentBeatTime);
    }

    private void HandleTutorialBeat(double beatTime)
    {
        if (!running || pausedByApplication || !warmupPending || beatController == null)
            return;

        if (!turnVoicePlayed && beatTime >= calibrationStartDspTime - 0.01)
        {
            turnVoicePlayed = true;
            NarrationPlayer.Say("tempo_turno");
            SetFeedback("Ahora toca al ritmo para encontrar tu pulso", 0f);
        }

        if (beatTime >= firstJudgedBeatDspTime - 0.01)
        {
            ApplyMeasuredStartingTempo();
            warmupPending = false;
            if (currentKit != null) beatController.metronomeClick = null;
            obstacleSpawner?.StartSpawner();
            SetFeedback("¡Sigue tu propio ritmo!", 1.2f);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (isPaused)
            return;
        if (paused)
        {
            if (!running)
                return;

            if (!warmupPending && levelEndDspTime > 0d)
                remainingLevelSeconds = Math.Max(0d, levelEndDspTime - AudioSettings.dspTime);
            levelEndDspTime = 0d;
            if (songSource != null) songSource.Stop();
            beatController.metronomeClick = originalClick;
            pausedByApplication = true;
            if (tutorialRoutine != null)
            {
                StopCoroutine(tutorialRoutine);
                tutorialRoutine = null;
            }
            if (beatController != null) beatController.StopBeats();
            if (obstacleSpawner != null) obstacleSpawner.StopSpawner();
            if (runner != null) runner.SetGameStarted(false);
        }
        else if (pausedByApplication)
        {
            pausedByApplication = false;
            if (!running || beatController == null)
                return;

            warmupPending = true;
            turnVoicePlayed = false;
            lastJudgedBeatTime = double.NaN;
            lastMissedBeatTime = double.NaN;
            lastTapDspTime = double.NaN;
            calibrationTaps.Clear();
            if (obstacleSpawner != null) obstacleSpawner.StopSpawner();
            if (runner != null) runner.SetGameStarted(true);
            float voiceSeconds = NarrationPlayer.Say("voz_observa");
            tutorialRoutine = StartCoroutine(BeginTutorialAfterVoice(voiceSeconds));
            SetFeedback("Observa el pulso antes de tocar", 0f);
        }
    }

    public void StartSession()
    {
        if (running || isPaused || beatController == null)
            return;

        running = true;
        pausedByApplication = false;
        sessionStartedAt = Time.time;
        sessionStartDspTime = AudioSettings.dspTime;
        remainingLevelSeconds = levelDurationSeconds;
        levelEndDspTime = 0d;
        currentKit = null;
        if (musicKits != null)
        {
            var valid = new List<MusicKit>();
            foreach (MusicKit kit in musicKits)
                if (kit != null && kit.IsValid()) valid.Add(kit);
            if (valid.Count > 0) currentKit = valid[UnityEngine.Random.Range(0, valid.Count)];
        }
        if (currentKit == null) Debug.LogWarning("Runner: no hay kits musicales válidos; se usará el metrónomo.");
        if (currentKit != null) BackgroundMusicPlayer.SetGameMusicFocus(true);
        if (songSource != null) songSource.Stop();
        beatController.metronomeClick = originalClick;
        stability = 1f;
        lastJudgedBeatTime = double.NaN;
        lastMissedBeatTime = double.NaN;
        ResetSessionStats();
        beatController.bpm = currentKit != null ? currentKit.originalBpm :
            Mathf.Clamp(baseBpm * GameSettings.GameplaySpeed, 30f, 160f);
        startingBpm = beatController.bpm;
        warmupPending = true;
        turnVoicePlayed = false;
        if (obstacleSpawner != null) obstacleSpawner.StopSpawner();
        float voiceSeconds = NarrationPlayer.Say("voz_observa");
        tutorialRoutine = StartCoroutine(BeginTutorialAfterVoice(voiceSeconds));
        SetFeedback("Escucha el pulso; después vendrán los obstáculos", 0f);
        UpdateUI();
    }

    private IEnumerator BeginTutorialAfterVoice(float voiceSeconds)
    {
        if (voiceSeconds > 0f)
            yield return new WaitForSecondsRealtime(voiceSeconds + 0.12f);
        while (isPaused)
            yield return null;
        if (!running || pausedByApplication || beatController == null)
            yield break;

        float listenSeconds = NarrationPlayer.Say("tempo_escucha_dos_pulsos");
        beatController.startDelaySeconds = Mathf.Max(originalBeatDelay, listenSeconds + 0.2f);
        beatController.StartBeats();
        beatController.startDelaySeconds = originalBeatDelay;
        firstDemoBeatDspTime = beatController.NextBeatDspTime;
        calibrationStartDspTime = firstDemoBeatDspTime + beatController.IntervalSec * Mathf.Clamp(observeBeats, 1, warmupBeats - 2);
        firstJudgedBeatDspTime = firstDemoBeatDspTime + beatController.IntervalSec * warmupBeats;
        levelEndDspTime = firstJudgedBeatDspTime + remainingLevelSeconds;
        if (currentKit != null)
        {
            if (songSource == null)
            {
                songSource = gameObject.AddComponent<AudioSource>();
                songSource.playOnAwake = false;
            }
            songSource.clip = currentKit.withPulse;
            songSource.pitch = beatController.bpm / currentKit.originalBpm;
            songSource.volume = GameSettings.MusicVolume;
            songSource.loop = true;
            songSource.PlayScheduled(firstJudgedBeatDspTime);
        }
        calibrationTaps.Clear();
        tutorialRoutine = null;
    }

    private void RecordCalibrationTap(double dspTime)
    {
        if (beatController == null || !beatController.IsRunning ||
            dspTime < calibrationStartDspTime - 0.05 || dspTime >= firstJudgedBeatDspTime)
            return;

        if (calibrationTaps.Count > 0 && dspTime - calibrationTaps[calibrationTaps.Count - 1] < 0.32)
            return;

        if (calibrationTaps.Count >= 3)
        {
            RecordTapDetail(dspTime, beatController.NextBeatDspTime, 0f, 0f, "extra_calibration", true);
            return;
        }

        calibrationTaps.Add(dspTime);
        float intervalMs = calibrationTaps.Count > 1
            ? (float)((dspTime - calibrationTaps[calibrationTaps.Count - 2]) * 1000.0) : 0f;
        double nearest = beatController.NextBeatDspTime;
        if (Math.Abs(nearest - dspTime) > Math.Abs(nearest - beatController.IntervalSec - dspTime))
            nearest -= beatController.IntervalSec;
        RecordTapDetail(dspTime, nearest, (float)((dspTime - nearest) * 1000.0), intervalMs, "calibration", true);
        SetFeedback($"Pulso de prueba {calibrationTaps.Count}", 0.5f);
    }

    private void ApplyMeasuredStartingTempo()
    {
        // A prerecorded WAV cannot follow an independently adaptive beat clock.
        if (currentKit != null) return;
        if (beatController == null || calibrationTaps.Count < 2)
            return;

        double intervalSum = 0d;
        int validIntervals = 0;
        for (int i = 1; i < calibrationTaps.Count; i++)
        {
            double interval = calibrationTaps[i] - calibrationTaps[i - 1];
            if (interval < 0.55 || interval > 2.2)
                continue;
            intervalSum += interval;
            validIntervals++;
        }

        if (validIntervals == 0)
            return;

        float measuredBpm = (float)(60.0 * validIntervals / intervalSum);
        startingBpm = Mathf.Clamp(measuredBpm, minimumAdaptiveBpm, maximumAdaptiveBpm);
        beatController.ChangeTempoOnBeat(startingBpm);
    }

    private void RecordRecentBeat(bool accurate)
    {
        if (currentKit != null) return;
        recentBeats++;
        if (accurate) recentAccurateBeats++;

        if (recentBeats < Mathf.Max(4, beatsPerAdjustment) || beatController == null)
            return;

        int needed = Mathf.CeilToInt(recentBeats * 0.75f);
        int tooFew = Mathf.FloorToInt(recentBeats * 0.4f);
        float change = recentAccurateBeats >= needed ? 5f : recentAccurateBeats <= tooFew ? -5f : 0f;
        if (change != 0f)
            pendingBpm = Mathf.Clamp(beatController.bpm + change, minimumAdaptiveBpm, maximumAdaptiveBpm);
        recentBeats = 0;
        recentAccurateBeats = 0;
    }

    private void ApplyPendingTempo(double beatTime)
    {
        if (currentKit != null) { pendingBpm = 0f; return; }
        if (pendingBpm <= 0f || beatController == null || Mathf.Approximately(pendingBpm, beatController.bpm))
        {
            pendingBpm = 0f;
            return;
        }

        double oldInterval = beatController.IntervalSec;
        double newInterval = 60.0 / pendingBpm;
        obstacleSpawner?.RescheduleTempo(beatTime, oldInterval, newInterval);
        beatController.ChangeTempoOnBeat(pendingBpm);
        SetFeedback(pendingBpm < 60f / oldInterval ? "Vamos más despacio" : "¡Muy bien! Un poco más rápido", 1f);
        pendingBpm = 0f;
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Use the same timing and action evaluation as a touch on the play area.
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            RegisterTap();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space))
            RegisterTap();
#endif
#endif
        if (!running || pausedByApplication || beatController == null || !beatController.IsRunning) return;

        if (!warmupPending && levelEndDspTime > 0d && AudioSettings.dspTime >= levelEndDspTime)
        {
            EndSession(true, "time_completed");
            return;
        }

        if (warmupPending)
        {
            if (AudioSettings.dspTime < firstJudgedBeatDspTime)
            {
                if (timerText != null && bpmText == null)
                    timerText.text = AudioSettings.dspTime < calibrationStartDspTime
                        ? "Observa el ejemplo" : "Prueba el pulso";
                return;
            }

            return;
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (timerText)
            timerText.text = $"Racha: {streak}";
        if (scoreText) scoreText.text = $"Puntaje {score}";
        if (bpmText) bpmText.text = $"Racha {streak}";
        if (streakText) streakText.text = $"Racha: {streak}";
        if (stabilityFill) stabilityFill.fillAmount = stability;
        if (stabilityLabel) stabilityLabel.text = $"ENERGÍA  {Mathf.RoundToInt(stability * 100f)}%";
    }

    void EndSession(bool completed, string exitReason)
    {
        if (!running)
            return;

        running = false;
        pausedByApplication = false;
        isPaused = false;
        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }

        if (beatController) beatController.StopBeats();
        if (beatController != null) beatController.metronomeClick = originalClick;
        if (songSource != null) songSource.Stop();
        BackgroundMusicPlayer.SetGameMusicFocus(false);

        if (obstacleSpawner != null)
            obstacleSpawner.StopSpawner();

        if (runner != null)
            runner.SetGameStarted(false);

        ReportAttempt(completed, exitReason);
        SetFeedback($"¡Buen trabajo! {score} puntos. Toca Reiniciar para otra ronda.", 0f);
        if (exitReason != "restart")
            NarrationPlayer.Say("tempo_fin");
    }

    public void ReturnToMenu()
    {
        EndSession(false, "return_to_menu");
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneTransitionController.LoadScene("MainScene");
    }

    public void RegisterTap()
    {
        if (!running || pausedByApplication || isPaused || beatController == null)
            return;

        double tapTime = AudioSettings.dspTime;
        if (warmupPending)
        {
            JumpWithoutBeatAction();
            RecordCalibrationTap(tapTime);
            return;
        }
        if (!IsAcceptingTaps)
        {
            JumpWithoutBeatAction();
            return;
        }

        if (tapTime < firstJudgedBeatDspTime - assistMs * 0.001 || !beatController.IsRunning)
        {
            JumpWithoutBeatAction();
            return;
        }

        float deltaMs = beatController.GetSignedDeltaToNearestBeatMs(tapTime, out double judgedBeatTime);
        if (judgedBeatTime < firstJudgedBeatDspTime - 0.0001)
        {
            JumpWithoutBeatAction();
            return;
        }

        if (ignoreExtraTapsOnSameBeat && !double.IsNaN(lastJudgedBeatTime) && Math.Abs(judgedBeatTime - lastJudgedBeatTime) < 0.0001)
        {
            JumpWithoutBeatAction();
            return;
        }

        float effectiveAssistMs = CurrentAssistMs;
        float effectiveGoodMs = Mathf.Min(CurrentGoodMs, effectiveAssistMs);
        float effectivePerfectMs = Mathf.Min(CurrentPerfectMs, effectiveGoodMs, (float)beatController.IntervalSec * 120f);
        float absMs = Mathf.Abs(deltaMs);
        BeatSyncedObstacleSpawner.ObstacleAction action = obstacleSpawner != null
            ? obstacleSpawner.GetActionForBeat(judgedBeatTime) : BeatSyncedObstacleSpawner.ObstacleAction.Tap;

        if (absMs > effectiveAssistMs)
        {
            if (!double.IsNaN(lastMissedBeatTime) && Math.Abs(judgedBeatTime - lastMissedBeatTime) < 0.0001)
            {
                JumpWithoutBeatAction();
                return;
            }
            lastMissedBeatTime = judgedBeatTime;
        }
        else
        {
            lastJudgedBeatTime = judgedBeatTime;
        }

        sumOffsetMs += deltaMs;
        sumAbsoluteOffsetMs += absMs;
        physicalTapsJudged++;
        float intervalMs = 0f;
        if (!double.IsNaN(lastTapDspTime))
        {
            intervalMs = (float)((tapTime - lastTapDspTime) * 1000.0);
            intervalCount++;
            sumIntervalMs += intervalMs;
            sumSquaredIntervalMs += intervalMs * intervalMs;
        }
        lastTapDspTime = tapTime;

        string result;
        int points = 0;

        if (absMs <= effectivePerfectMs)
        {
            stability = Mathf.Clamp01(stability + gainOnHit);
            perfectCount++;
            totalTapsJudged++;
            streak++;
            points = perfectScore * Mathf.Max(1, 1 + streak / 5);
            score += points;
            SetFeedback("Perfecto", 0.5f);
            if (outcomeFeedback != null)
                outcomeFeedback.ShowSuccess(feedbackText != null ? feedbackText.rectTransform : null);
            result = "perfect";
            AccessibleHaptics.Success();
            if (sfxSource && tapCorrect) sfxSource.PlayOneShot(tapCorrect);
            PerformBeatAction(judgedBeatTime, action, perfectJumpMultiplier, perfectTargetContactTime);
        }
        else if (absMs <= effectiveGoodMs)
        {
            stability = Mathf.Clamp01(stability + gainOnHit * 0.5f);
            goodCount++;
            totalTapsJudged++;
            streak++;
            points = goodScore * Mathf.Max(1, 1 + streak / 6);
            score += points;
            SetFeedback("Bien", 0.5f);
            if (outcomeFeedback != null)
                outcomeFeedback.ShowSuccess(feedbackText != null ? feedbackText.rectTransform : null);
            result = "good";
            AccessibleHaptics.Success();
            if (sfxSource && tapCorrect) sfxSource.PlayOneShot(tapCorrect);
            if (jumpOnGood)
            {
                float targetTime = deltaMs < 0f ? goodEarlyTargetContactTime : goodLateTargetContactTime;
                PerformBeatAction(judgedBeatTime, action, goodJumpMultiplier, targetTime);
            }
            else
                JumpWithoutBeatAction();
        }
        else if (absMs <= effectiveAssistMs)
        {
            totalTapsJudged++;
            streak = 0;
            points = timingOffsetScore;
            score += points;

            if (deltaMs < 0f)
            {
                earlyCount++;
                SetFeedback("Un poco antes", 0.5f);
                if (outcomeFeedback != null)
                    outcomeFeedback.ShowFailure(feedbackText != null ? feedbackText.rectTransform : null);
                result = "early";
                AccessibleHaptics.Error();
                PerformBeatAction(judgedBeatTime, action, earlyAssistJumpMultiplier, assistEarlyTargetContactTime);
            }
            else
            {
                lateCount++;
                SetFeedback("Un poco después", 0.5f);
                if (outcomeFeedback != null)
                    outcomeFeedback.ShowFailure(feedbackText != null ? feedbackText.rectTransform : null);
                result = "late";
                AccessibleHaptics.Error();
                PerformBeatAction(judgedBeatTime, action, lateAssistJumpMultiplier, assistLateTargetContactTime);
            }
        }
        else
        {
            JumpWithoutBeatAction();
            totalTapsJudged++;
            missCount++;
            streak = 0;
            if (absMs >= (float)beatController.IntervalSec * 400f)
            {
                int previousScore = score;
                score = Mathf.Max(0, score - veryFarPenalty);
                points = score - previousScore;
                veryFarCount++;
            }
            else farNoPointsCount++;
            SetFeedback(deltaMs < 0f ? "Espera el pulso" : "Prueba el siguiente pulso", 0.6f);
            if (outcomeFeedback != null)
                outcomeFeedback.ShowFailure(feedbackText != null ? feedbackText.rectTransform : null);
            result = absMs >= (float)beatController.IntervalSec * 400f ? "very_far" : "miss";
            AccessibleHaptics.Error();
            if (sfxSource && tapWrong) sfxSource.PlayOneShot(tapWrong);
            stability = Mathf.Max(0f, stability - lossOnMiss);
        }

        RecordTapDetail(tapTime, judgedBeatTime, deltaMs, intervalMs, result, true, points);
        bestStreak = Mathf.Max(bestStreak, streak);
        RecordRecentBeat(absMs <= effectiveGoodMs);
        int difficultyStage = streak >= 24 ? 3 : streak >= 16 ? 2 : streak >= 8 ? 1 : 0;
        obstacleSpawner?.SetDifficulty(difficultyStage);
        highestDifficultyStage = Mathf.Max(highestDifficultyStage, difficultyStage);
        UpdateUI();
    }

    private void RecordTapDetail(double tapTime, double judgedBeatTime, float offsetMs, float intervalMs, string result, bool hasTap, int points = 0)
    {
        BeatSyncedObstacleSpawner.ObstacleAction action = obstacleSpawner != null
            ? obstacleSpawner.GetActionForBeat(judgedBeatTime) : BeatSyncedObstacleSpawner.ObstacleAction.Tap;
        tapDetails.Add(new TempoTapTapMetric
        {
            beatNumber = result == "calibration" ? calibrationTaps.Count : totalTapsJudged,
            timeSeconds = Mathf.Max(0f, (float)(tapTime - sessionStartDspTime)),
            offsetMs = offsetMs,
            intervalMs = intervalMs,
            bpm = beatController != null ? beatController.bpm : baseBpm,
            result = result,
            action = action.ToString().ToLowerInvariant(),
            hasTap = hasTap,
            points = points,
        });
        if (tapDetails.Count >= TapChunkSize && running && SessionManager.Instance != null &&
            SessionManager.Instance.HasActiveSession && GameReportManager.Instance != null &&
            PlayerProfileManager.Instance != null && PlayerProfileManager.Instance.HasActiveProfile)
            ReportAttempt(false, "checkpoint", true);
    }

    private void PerformBeatAction(double beatTime, BeatSyncedObstacleSpawner.ObstacleAction action,
        float multiplier, float contactTime)
    {
        if (runner == null)
            return;

        bool performed = false;
        if (action == BeatSyncedObstacleSpawner.ObstacleAction.Duck)
            performed = runner.Duck(obstacleSpawner != null ? obstacleSpawner.GetDuckDuration() : 1.1f);
        else if (action == BeatSyncedObstacleSpawner.ObstacleAction.Jump)
            performed = runner.Jump(GetAdaptiveMultiplier(multiplier, contactTime, action));
        else
            JumpWithoutBeatAction();

        if (performed)
            obstacleSpawner?.MarkActionSuccessForBeat(beatTime, action);
    }

    private void JumpWithoutBeatAction()
    {
        if (runner != null)
            runner.Jump(obstacleSpawner != null ? obstacleSpawner.GetFreeJumpMultiplier() : 1f);
    }

    public void RegisterCollision(float penalty)
    {
        if (!running || pausedByApplication || isPaused)
            return;

        streak = 0;
        collisionCount++;
        stability = Mathf.Max(0f, stability - Mathf.Max(0f, penalty));
        AccessibleHaptics.Error();
        obstacleSpawner?.SetDifficulty(0);
        pendingBpm = Mathf.Clamp(beatController != null ? beatController.bpm - 5f : baseBpm,
            minimumAdaptiveBpm, maximumAdaptiveBpm);
        SetFeedback("Un tropiezo no termina el juego. Vamos más despacio", 1.2f);
        if (outcomeFeedback != null)
            outcomeFeedback.ShowFailure(feedbackText != null ? feedbackText.rectTransform : null);
        UpdateUI();
    }

    void SetFeedback(string msg, float seconds)
    {
        if (!feedbackText) return;
        feedbackText.text = msg;
        if (feedbackClearRoutine != null)
        {
            StopCoroutine(feedbackClearRoutine);
            feedbackClearRoutine = null;
        }

        if (seconds <= 0f)
            return;

        feedbackClearRoutine = StartCoroutine(ClearFeedbackRoutine(seconds));
    }

    IEnumerator ClearFeedbackRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (feedbackText) feedbackText.text = "";
        feedbackClearRoutine = null;
    }
    public void StartGameFromButton()
    {
        if (isPaused) return;
        if (Time.unscaledTime - lastStartPressTime < 0.5f)
            return;
        lastStartPressTime = Time.unscaledTime;

        if (running)
            EndSession(false, "restart");

        StartSession();
        if (!running)
            return;

        if (runner != null)
        {
            runner.SetGameStarted(true);
            JumpWithoutBeatAction();
        }
        if (startPanel != null)
            startPanel.SetActive(false);
    }

    private void ConfigurePauseMenu()
    {
        TopHudPanelView hud = FindFirstObjectByType<TopHudPanelView>();
        pauseTrigger = hud != null ? hud.MenuButton : null;
        if (pauseTrigger != null)
        {
            // RemoveAllListeners does not remove persistent Inspector callbacks.
            // The HUD button previously had ReturnToMenu serialized in this scene.
            pauseTrigger.onClick = new Button.ButtonClickedEvent();
            pauseTrigger.onClick.AddListener(PauseGame);
        }

        if (pausePanel == null)
            return;

        pausePanelView = pausePanel.GetComponent<PausePanelView>();
        if (pausePanelView != null)
            pausePanelView.InitializeHidden();
        else
            pausePanel.SetActive(false);

        Transform box = pausePanel.transform.Find("PauseBox");
        if (box == null) return;
        BindPauseAction(box, "ResumeButton", ResumeGame);
        BindPauseAction(box, "CloseButton", ResumeGame);
        BindPauseAction(box, "RestartButton", RestartScene);
        BindPauseAction(box, "MenuButton", ReturnToMenu);
    }

    private static void BindPauseAction(Transform box, string name, UnityEngine.Events.UnityAction action)
    {
        Button button = box.Find(name)?.GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void PauseGame()
    {
        if (isPaused || pausePanel == null) return;
        isPaused = true;
        if (beatController != null) beatController.PauseBeats();
        if (songSource != null) songSource.Pause();
        pausePanel.transform.SetAsLastSibling();
        if (pausePanelView != null) pausePanelView.Show();
        else pausePanel.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        if (pausePanelView != null) pausePanelView.Hide();
        else pausePanel.SetActive(false);
        AudioListener.pause = false;
        Time.timeScale = 1f;
        double elapsed = beatController != null ? beatController.ResumeBeats() : 0d;
        if (songSource != null) songSource.UnPause();
        if (elapsed <= 0d) return;
        firstDemoBeatDspTime += elapsed;
        calibrationStartDspTime += elapsed;
        firstJudgedBeatDspTime += elapsed;
        if (levelEndDspTime > 0d) levelEndDspTime += elapsed;
        sessionStartDspTime += elapsed;
        if (!double.IsNaN(lastJudgedBeatTime)) lastJudgedBeatTime += elapsed;
        if (!double.IsNaN(lastMissedBeatTime)) lastMissedBeatTime += elapsed;
        if (!double.IsNaN(lastTapDspTime)) lastTapDspTime += elapsed;
        for (int i = 0; i < calibrationTaps.Count; i++) calibrationTaps[i] += elapsed;
        obstacleSpawner?.ShiftSchedule(elapsed);
    }

    private void RestartScene()
    {
        EndSession(false, "restart");
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneTransitionController.ReloadCurrentScene();
    }

    void ResetSessionStats()
    {
        score = 0;
        streak = 0;
        totalTapsJudged = 0;
        physicalTapsJudged = 0;
        bestStreak = 0;
        perfectCount = 0;
        goodCount = 0;
        earlyCount = 0;
        lateCount = 0;
        missCount = 0;
        farNoPointsCount = 0;
        veryFarCount = 0;
        collisionCount = 0;
        sumOffsetMs = 0.0;
        sumAbsoluteOffsetMs = 0.0;
        lastTapDspTime = double.NaN;
        intervalCount = 0;
        sumIntervalMs = 0.0;
        sumSquaredIntervalMs = 0.0;
        recentBeats = 0;
        recentAccurateBeats = 0;
        highestDifficultyStage = 0;
        pendingBpm = 0f;
        calibrationTaps.Clear();
        tapDetails.Clear();
        reportChunkIndex = 0;
    }

    void ReportAttempt(bool completed, string exitReason, bool checkpoint = false)
    {
        if ((totalTapsJudged == 0 && collisionCount == 0 && tapDetails.Count == 0) ||
            (checkpoint && tapDetails.Count < TapChunkSize))
            return;

        if (GameReportManager.Instance == null || SessionManager.Instance == null || !SessionManager.Instance.HasActiveSession)
            return;

        int hits = perfectCount + goodCount;
        int savedTapCount = tapDetails.Count;
        double meanIntervalMs = intervalCount > 0 ? sumIntervalMs / intervalCount : 0.0;
        AttemptReportData report = new AttemptReportData
        {
            miniGame = "tempo_tap",
            level = highestDifficultyStage + 1,
            difficulty = "adaptativa",
            bpm = beatController != null ? Mathf.RoundToInt(beatController.bpm) : 0,
            errors = earlyCount + lateCount + missCount + collisionCount,
            correctAnswers = hits,
            levelRepetitions = 0,
            completed = completed,
            timeSeconds = Mathf.Max(1, Mathf.RoundToInt(Time.time - sessionStartedAt)),
            scoreFinal = score,
            wasTutorial = false,
            exitReason = exitReason,
            tempoTap = new TempoTapMetricsData
            {
                chunkIndex = reportChunkIndex,
                judgedTaps = totalTapsJudged,
                perfectTaps = perfectCount,
                goodTaps = goodCount,
                earlyTaps = earlyCount,
                lateTaps = lateCount,
                missedTaps = missCount,
                farNoPointsTaps = farNoPointsCount,
                veryFarTaps = veryFarCount,
                bestStreak = bestStreak,
                songId = currentKit != null ? currentKit.id : string.Empty,
                gameplaySeconds = Mathf.Clamp((float)(levelDurationSeconds -
                    (levelEndDspTime > 0d
                        ? Math.Max(0d, levelEndDspTime - AudioSettings.dspTime)
                        : remainingLevelSeconds)), 0f, levelDurationSeconds),
                collisions = collisionCount,
                hitRatePercent = totalTapsJudged > 0 ? 100f * hits / totalTapsJudged : 0f,
                meanOffsetMs = physicalTapsJudged > 0 ? (float)(sumOffsetMs / physicalTapsJudged) : 0f,
                meanAbsoluteOffsetMs = physicalTapsJudged > 0 ? (float)(sumAbsoluteOffsetMs / physicalTapsJudged) : 0f,
                intervalCount = intervalCount,
                intervalStdDevMs = intervalCount > 0
                    ? (float)Math.Sqrt(Math.Max(0.0, sumSquaredIntervalMs / intervalCount - meanIntervalMs * meanIntervalMs))
                    : 0f,
                startingBpm = startingBpm,
                endingBpm = beatController != null ? beatController.bpm : startingBpm,
                taps = new List<TempoTapTapMetric>(tapDetails),
            },
        };

        GameReportManager.Instance.ReportAttempt(report, () =>
        {
            if (!checkpoint) return;
            tapDetails.RemoveRange(0, Mathf.Min(savedTapCount, tapDetails.Count));
            reportChunkIndex++;
        }, error => Debug.LogWarning("TempoTap report error: " + error));
    }

    float GetAdaptiveMultiplier(float defaultMultiplier, float targetContactTime, BeatSyncedObstacleSpawner.ObstacleAction action)
    {
        if (obstacleSpawner == null)
            return defaultMultiplier;

        return obstacleSpawner.GetAdaptiveJumpMultiplier(defaultMultiplier, targetContactTime, action);
    }
}
