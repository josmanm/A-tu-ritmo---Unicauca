using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class BandaLevelConfiguration
{
    public string label = "Nivel 1";
    [Tooltip("Tempo for this level's WAV loops; used as a fallback without clips.")]
    [Range(40, 160)] public int bpm = 80;
    [Min(4)] public int beats = 4;
    [Header("Audio del nivel (primera vuelta guiada, luego fondo en loop)")]
    public AudioClip guidedLoop;
    public AudioClip backgroundLoop;
    [Min(1)] public int beatsPerLoop = 16;
    [Header("Retos a los 15, 30 y 45 segundos")]
    [Min(1f)] public float levelDurationSeconds = 60f;
    [Min(0f)] public float challengeDurationSeconds = 5f;
    [Range(0f, 1f)] public float firstChallengeMusicFraction = 0.6f;
    [Range(0.08f, 0.4f)] public float toleranceSeconds = 0.25f;
    public bool visualHelp = true;
    [Range(0.25f, 1f)] public float successFraction = 0.5f;
}

public class BandaMusicalGameManager : MonoBehaviour
{
    public RhythmManager rhythm;
    public BandaSessionRecorder recorder;
    public DrumInputController drum;
    public MusicianController monkey;
    public MusicianController cat;
    public MusicianController bear;
    public TMP_Text message;
    public UnityEngine.UI.Image hand;
    public UnityEngine.UI.Image star;
    public UnityEngine.UI.Image ear;
    public UnityEngine.UI.Button replayButton;
    public UnityEngine.UI.Button backButton;
    public TopHudPanelView topHud;
    [Header("Pausa")]
    public GameObject pausePanel;
    public AudioSource voiceSource;
    [Header("Optional narration: assign recordings of these exact phrases")]
    public AudioClip discoverVoice; // ¡Hola! ¡Toca el tambor!
    public AudioClip listenVoice;   // ¡Mira cómo lo hago!
    public AudioClip togetherVoice; // ¡Ahora toca conmigo!
    public AudioClip helpVoice;     // ¡Escuchemos otra vez!
    public AudioClip positiveSound;
    [Tooltip("Golpe breve al incorporarse un músico nuevo, entre secciones (sin tapar el loop).")]
    public AudioClip animalHit;
    [Header("Canciones: BPM e inicio del primer pulso por pista")]
    public BandaSongConfiguration[] songs = Array.Empty<BandaSongConfiguration>();
    [Tooltip("Index in Songs. 0 uses the 80 BPM drum loop; 1 uses the 120 BPM mix (provisional alignment).")]
    [Min(0)] public int selectedSongIndex;
    [Header("Niveles: sus WAV tienen prioridad sobre la canción del tutorial")]
    public BandaLevelConfiguration[] levels =
    {
        new BandaLevelConfiguration { label = "Nivel 1", bpm = 80, beats = 4, visualHelp = true },
        new BandaLevelConfiguration { label = "Nivel 2", bpm = 80, beats = 8, visualHelp = true },
        new BandaLevelConfiguration { label = "Nivel 3", bpm = 80, beats = 8, visualHelp = false }
    };
    [Header("Kits de canciones (sin repetir entre los tres niveles)")]
    public MusicKit[] musicKits;
    public bool allowHelp = true;
    [Header("Puntuación del tambor durante los niveles")]
    [Min(0)] public int perfectPoints = 15;
    [Min(0)] public int nearPoints = 8;
    [Min(0)] public int validPoints = 3;
    [Min(0)] public int farPenalty = 1;
    [Min(0)] public int veryFarPenalty = 3;

    private bool discovering;
    private bool tappedDiscovery;
    private bool accepting;
    private bool assisted;
    private bool completed;
    private int currentLevel;
    private int sectionIndex;
    private int sectionHits;
    private Coroutine activity;
    private bool playingMainLevels;
    private int score;
    private BandaSongConfiguration selectedSong;
    private int[] kitOrder;
    private PausePanelView pauseView;
    private bool isPaused;
    private ChatterboxVoiceCatalog voiceCatalog;
    private bool allLevelsSucceeded;
    public bool IsPaused => isPaused;
    [SerializeField] private UnityEngine.UI.Button startButton;

    private static readonly string[] LevelIntroductions =
    {
        "banda_nivel_1_intro", "banda_nivel_2_intro", "banda_nivel_3_intro"
    };

    private static readonly string[] LevelSuccesses =
    {
        "banda_nivel_1_logro", "banda_nivel_2_logro", "banda_nivel_3_logro"
    };

    // Played only at quiet instructional intervals, never on individual drum hits.
    private static readonly string[,] Encouragements =
    {
        { "banda_nivel_1_progreso", "banda_nivel_1_animo", "banda_logro_muy_bien", "banda_logro_asi_se_hace" },
        { "banda_nivel_2_progreso", "banda_nivel_2_con_gato", "banda_logro_excelente", "banda_logro_buen_ritmo" },
        { "banda_logro_seguir_pulso", "banda_logro_perfecto", "banda_logro_bien_tocado", "banda_nivel_3_felicitacion" }
    };

    private void Start()
    {
        if (rhythm == null || recorder == null || drum == null || monkey == null || cat == null || bear == null)
        {
            Debug.LogError("Banda Musical: faltan referencias en la escena.");
            return;
        }
        BackgroundMusicPlayer.SetBandaMusicFocus(true);
        rhythm.Beat += OnBeat;
        UnityEngine.UI.Button menu = topHud != null && topHud.MenuButton != null
            ? topHud.MenuButton : backButton;
        if (pausePanel != null)
        {
            pauseView = pausePanel.GetComponent<PausePanelView>();
            if (pauseView != null) pauseView.InitializeHidden();
            else pausePanel.SetActive(false);
            Transform box = pausePanel.transform.Find("PauseBox");
            if (box != null)
            {
                BindPauseButton(box, "ResumeButton", ResumeGame);
                BindPauseButton(box, "CloseButton", ResumeGame);
                BindPauseButton(box, "RestartButton", RestartFromPause);
                BindPauseButton(box, "MenuButton", ReturnToMenu);
            }
        }
        if (menu != null)
        {
            menu.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            menu.onClick.AddListener(pausePanel != null ? (UnityEngine.Events.UnityAction)PauseGame : ReturnToMenu);
        }
        if (menu != backButton && backButton != null) backButton.gameObject.SetActive(false);
        if (replayButton != null) replayButton.onClick.AddListener(Restart);
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGameFromButton);
            startButton.gameObject.SetActive(true);
            if (replayButton != null) replayButton.gameObject.SetActive(false);
            cat.gameObject.SetActive(false);
            bear.gameObject.SetActive(false);
            monkey.gameObject.SetActive(true);
            SetHand(false);
            if (message != null) message.text = "Pulsa JUGAR para comenzar";
            UpdateHud("Tutorial");
            Canvas tutorialCanvas = startButton.GetComponentInParent<Canvas>();
            GameTutorialOverlay.Show(tutorialCanvas, "banda_tutorial",
                new[] { monkey.transform, drum.transform, startButton.transform },
                new[] { "Escucha al mono y mira el ritmo.",
                    "El tambor brilla con cada pulso. Tócalo siguiendo el brillo.",
                    "Los demás músicos se unirán. ¡Pulsa JUGAR!" },
                demoGlow: drum.transform, playButton: startButton);
        }
        else
            Restart();
    }

    public void StartGameFromButton()
    {
        if (startButton != null && !startButton.gameObject.activeSelf)
            return;
        Restart();
    }

    public void Restart()
    {
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (isPaused) ClosePause();
        if (activity != null) StopCoroutine(activity);
        if (rhythm != null) rhythm.StopSequence();
        if (drum != null) drum.EndBeatGuide();
        // Record an interrupted activity before starting a different one.
        if (recorder != null && sectionIndex > 0 && !completed)
            recorder.Save(Mathf.Max(1, currentLevel + 1), Mathf.RoundToInt(rhythm.bpm), false);
        currentLevel = 0;
        sectionIndex = 0;
        sectionHits = 0;
        assisted = false;
        completed = false;
        playingMainLevels = false;
        score = 0;
        allLevelsSucceeded = true;
        if (voiceSource != null) voiceSource.Stop();
        UpdateHud("Tutorial");
        discovering = false;
        accepting = false;
        recorder.Begin(false);
        selectedSong = songs != null && selectedSongIndex >= 0 && selectedSongIndex < songs.Length
            ? songs[selectedSongIndex] : null;
        kitOrder = null;
        if (musicKits != null)
        {
            var valid = new System.Collections.Generic.List<int>();
            for (int i = 0; i < musicKits.Length; i++)
            {
                if (musicKits[i] != null && musicKits[i].IsValid()) valid.Add(i);
                else Debug.LogWarning("Banda: kit musical incompleto o BeatMap inválido en posición " + i);
            }
            for (int i = valid.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = valid[i]; valid[i] = valid[j]; valid[j] = temp;
            }
            if (valid.Count >= levels.Length) kitOrder = valid.ToArray();
            else Debug.LogWarning("Banda: se necesitan tres kits válidos para los tres niveles.");
        }
        if (selectedSong != null && selectedSong.clip != null && selectedSong.bpm > 0f)
        {
            rhythm.music = selectedSong.clip;
            rhythm.bpm = selectedSong.bpm;
            rhythm.beatOffsetSeconds = selectedSong.firstBeatOffsetSeconds;
            recorder.Metrics.songId = selectedSong.id;
        }
        cat.gameObject.SetActive(false);
        bear.gameObject.SetActive(false);
        monkey.gameObject.SetActive(true);
        if (star != null) star.gameObject.SetActive(false);
        if (ear != null) ear.gameObject.SetActive(false);
        if (replayButton != null) replayButton.gameObject.SetActive(false);
        activity = StartCoroutine(RunActivity());
    }

    public void OnDrumTap(double dspTime)
    {
        if (isPaused) return;
        if (discovering)
        {
            tappedDiscovery = true;
            return;
        }
        if (!accepting || !rhythm.Running) return;
        bool unique = recorder.RecordTap(dspTime, rhythm, sectionIndex,
            out float offsetMs, out string result);
        if (unique) sectionHits++;
        if (!playingMainLevels) return; // Tutorial sin puntos ni penalizaciones.

        int delta = 0;
        string grade = "extra";
        float absoluteMs = Mathf.Abs(offsetMs);
        float beatMs = (float)rhythm.Interval * 1000f;
        if (unique)
        {
            if (absoluteMs <= Mathf.Min(80f, beatMs * 0.15f))
            { grade = "perfect"; delta = perfectPoints; }
            else if (absoluteMs <= Mathf.Min(170f, beatMs * 0.3f))
            { grade = "near"; delta = nearPoints; }
            else { grade = "valid"; delta = validPoints; }
        }
        else if (result != "extra")
        {
            bool veryFar = absoluteMs >= beatMs * 0.4f;
            grade = veryFar ? "very_far" : "far";
            delta = -(veryFar ? veryFarPenalty : farPenalty);
        }
        int newScore = Mathf.Max(0, score + delta);
        recorder.RecordScoreForLastTap(newScore - score, grade);
        score = newScore;
        recorder.SetScore(score);
        UpdateHud("Nivel " + (currentLevel + 1));
    }

    private void OnBeat(int beat, double dsp)
    {
        monkey.OnBeat(beat, dsp, rhythm.Interval);
        if (cat.gameObject.activeSelf) cat.OnBeat(beat, dsp, rhythm.Interval);
        if (bear.gameObject.activeSelf) bear.OnBeat(beat, dsp, rhythm.Interval);
        // Only the teaching demonstration uses a separate drum sound. The song
        // and its expected beats share one DSP start and do not layer a metronome.
        if (!rhythm.PlayingSong && drum.source != null && drum.hitClip != null)
            drum.source.PlayOneShot(drum.hitClip, 0.55f * GameSettings.EffectsVolume);
    }

    private void Say(string words, AudioClip voice)
    {
        if (message != null) message.text = words;
        if (voice != null && voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.clip = voice;
            voiceSource.volume = GameSettings.EffectsVolume;
            voiceSource.Play();
        }
    }

    private IEnumerator RunActivity()
    {
        discovering = true;
        tappedDiscovery = false;
        SetHand(true);
        Say("¡Hola! ¡Toca el tambor!", discoverVoice);
        yield return new WaitUntil(() => tappedDiscovery);
        discovering = false;
        yield return new WaitForSeconds(0.6f);

        yield return NarrateBeforeBeat("Cada brillo marca un pulso. Escucha y observa.", "banda_que_es_pulso");
        yield return NarrateBeforeBeat("Primero toca el mono. Fíjate en su ritmo.", "banda_observa_mono");
        yield return NarrateBeforeBeat("Uno, dos, tres, cuatro.", "banda_cuenta_cuatro");
        yield return NarrateBeforeBeat("Mira: el tambor brilla con cada pulso", "", listenVoice);
        yield return PlaySection(4, false, false, true);
        if (togetherVoice != null)
            yield return NarrateBeforeBeat("¡Toca cuando el tambor brille!", "", togetherVoice);
        else
            Say("¡Toca cuando el tambor brille!", null);
        yield return PlaySection(4, true, true, true);
        if (sectionHits >= 2)
            yield return NarrateBeforeBeat("¡Lo estás haciendo genial!", "banda_logro_genial");

        yield return NarrateBeforeBeat("Ahora toca tú. Sigue el brillo del tambor.", "banda_ahora_tu");
        int independentTries = 0;
        do
        {
            if (independentTries > 0)
                yield return NarrateBeforeBeat("Escucha el pulso e inténtalo de nuevo.", "banda_reintento");
            else
                Say("Ahora tú: toca cuando brille", null);
            yield return PlaySection(4, true, true, false);
            if (sectionHits >= 2) break;
            independentTries++;
            assisted = true;
            if (allowHelp && independentTries < 2) yield return ShowHelp();
        } while (independentTries < 2);

        if (sectionHits >= 2)
        {
            yield return NarrateBeforeBeat("¡Eso es! Estás siguiendo el ritmo.", "banda_acierto");
            yield return NarrateBeforeBeat("¡Muy bien! Vamos a tocar una canción.", "banda_empieza_cancion");
        }
        else
            yield return NarrateBeforeBeat("Si te equivocas, no pasa nada. Sigue escuchando.", "banda_error_amable");

        for (int level = 0; level < levels.Length; level++)
        {
            currentLevel = level;
            UpdateHud("Nivel " + (level + 1));
            BandaLevelConfiguration config = levels[level];
            if (level == 2)
                yield return NarrateBeforeBeat("Escucha al mono, al gato y al oso.", "banda_escucha_animales");
            yield return NarrateBeforeBeat(config.label + ": toca cuando brille", LevelIntroductions[Mathf.Min(level, LevelIntroductions.Length - 1)]);
            MusicKit kit = kitOrder != null && level < kitOrder.Length ? musicKits[kitOrder[level]] : null;
            recorder.BeginLevel(level + 1, kit != null ? kit.id :
                selectedSong != null ? selectedSong.id : string.Empty, config.bpm);
            bool hasLevelLoops = config.guidedLoop != null && config.backgroundLoop != null;
            // Only change tempo when the corresponding music changes too.
            rhythm.bpm = kit != null || hasLevelLoops ? config.bpm :
                selectedSong != null && selectedSong.clip != null ? selectedSong.bpm : config.bpm;
            rhythm.beatOffsetSeconds = kit != null || hasLevelLoops ? 0f :
                selectedSong != null ? selectedSong.firstBeatOffsetSeconds : 0f;
            rhythm.music = hasLevelLoops ? config.guidedLoop :
                selectedSong != null ? selectedSong.clip : rhythm.music;
            rhythm.toleranceSeconds = config.toleranceSeconds;
            // Round to a complete beat: at 60/80/100 BPM, 60 s is exact.
            int expectedBeats = kit != null || hasLevelLoops
                ? Mathf.RoundToInt(config.levelDurationSeconds * config.bpm / 60f) : config.beats;
            int tries = 0;
            do
            {
                Say(config.label + ": toca cuando brille", null);
                playingMainLevels = true;
                if (kit != null || hasLevelLoops)
                    yield return PlayLoopedLevel(config, expectedBeats, config.visualHelp || assisted, kit);
                else
                    yield return PlaySection(expectedBeats, true, true, config.visualHelp || assisted);
                playingMainLevels = false;
                recorder.AddSection(expectedBeats, sectionHits, assisted);
                // A looped level is one full minute, even if the player needs
                // more practice; do not silently repeat the entire minute.
                if (kit != null || hasLevelLoops || sectionHits >= Mathf.CeilToInt(expectedBeats * config.successFraction)) break;
                tries++;
                assisted = true;
                if (allowHelp && tries < 2)
                {
                    yield return ShowHelp();
                    yield return NarrateBeforeBeat("Escucha y vuelve a intentarlo.", "banda_reintento");
                    yield return PlaySection(4, false, false, true);
                }
            } while (tries < 2);

            bool accompanied = sectionHits >= Mathf.CeilToInt(expectedBeats * config.successFraction);
            if (!accompanied) allLevelsSucceeded = false;
            if (star != null) star.gameObject.SetActive(accompanied);
            if (accompanied && positiveSound != null && drum.source != null)
                drum.source.PlayOneShot(positiveSound, GameSettings.EffectsVolume);
            if (accompanied)
                yield return NarrateBeforeBeat(level == 2 ? "¡La banda toca junta!" : "¡Muy bien!", LevelSuccesses[Mathf.Min(level, LevelSuccesses.Length - 1)]);
            else
                yield return NarrateBeforeBeat("¡Seguimos juntos!", "banda_seguimos_juntos");
            if (level == 0) cat.gameObject.SetActive(true);
            if (level == 1) bear.gameObject.SetActive(true);
            if (level < 2 && animalHit != null && drum.source != null)
                drum.source.PlayOneShot(animalHit, 0.35f * GameSettings.EffectsVolume);
            if (level == 0)
                yield return NarrateBeforeBeat("¡El gato se une a la banda!", "banda_llega_gato");
            if (level == 1)
                yield return NarrateBeforeBeat("¡El oso se une a la banda!", "banda_llega_oso");
            yield return new WaitForSeconds(0.3f);
            if (star != null) star.gameObject.SetActive(false);
        }

        completed = true;
        playingMainLevels = false;
        SetHand(false);
        if (star != null) star.gameObject.SetActive(true);
        if (replayButton != null) replayButton.gameObject.SetActive(true);
        recorder.Save(levels.Length, Mathf.RoundToInt(rhythm.bpm), true);
        if (allLevelsSucceeded)
            yield return NarrateBeforeBeat("¡Lo lograste! La banda ya toca junta.", "banda_fin_exito");
        yield return NarrateBeforeBeat("¡Bravo! ¡Lo conseguimos juntos!", "banda_final_bravo");
        Narrate("¿Quieres tocar otra vez? Pulsa Otra vez.", "banda_otra_vez");
        activity = null;
    }

    private IEnumerator PlaySection(int beats, bool withInput, bool song, bool showHand)
    {
        sectionHits = 0;
        sectionIndex++;
        accepting = withInput;
        SetHand(showHand);
        rhythm.StartSequence(beats, song);
        drum.BeginBeatGuide(rhythm.FirstBeatDspTime, rhythm.Interval, beats);
        double end = rhythm.FirstBeatDspTime + (beats - 1) * rhythm.Interval + rhythm.toleranceSeconds;
        while (AudioSettings.dspTime < end) yield return null;
        accepting = false;
        rhythm.StopSequence();
        drum.EndBeatGuide();
        monkey.FinishPhrase();
        cat.FinishPhrase();
        bear.FinishPhrase();
        SetHand(false);
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator PlayLoopedLevel(BandaLevelConfiguration config, int beats, bool showHand, MusicKit kit)
    {
        sectionHits = 0;
        sectionIndex++;
        accepting = true;
        SetHand(showHand);
        bool scheduled = kit != null
            ? rhythm.StartKitSequence(kit, beats, Mathf.Min(16, kit.beatCount / 2),
                config.challengeDurationSeconds, config.firstChallengeMusicFraction)
            : rhythm.StartLoopedSequence(config.guidedLoop, config.backgroundLoop,
                config.beatsPerLoop, beats, config.challengeDurationSeconds,
                config.firstChallengeMusicFraction);
        if (!scheduled)
        {
            // Invalid/missing loops should not make the level impossible to play.
            sectionIndex--;
            yield return PlaySection(beats, true, true, showHand);
            yield break;
        }
        drum.BeginBeatGuide(rhythm.FirstBeatDspTime, rhythm.Interval, beats);
        double lastWindowEnd = rhythm.FirstBeatDspTime + (beats - 1) * rhythm.Interval + rhythm.toleranceSeconds;
        double audioEnd = rhythm.FirstBeatDspTime + beats * rhythm.Interval;
        int previousChallenge = 0;
        int nextAnnouncement = 1;
        int nextEncouragement = 0;
        int lastEncouragedHits = 0;
        float[] encouragementTimes = { 7f, 24f, 39f, 54f };
        while (AudioSettings.dspTime < lastWindowEnd)
        {
            double now = AudioSettings.dspTime;
            double elapsed = now - rhythm.FirstBeatDspTime;
            // Speak BEFORE the five-second challenge, leaving its actual beats unobstructed.
            if (nextAnnouncement <= 3 && nextAnnouncement * 15d < config.levelDurationSeconds)
            {
                string cueId = nextAnnouncement == 1 ? "banda_reto_musica_baja" :
                    nextAnnouncement == 2 ? "banda_reto_sin_musica" :
                    currentLevel == 2 ? "banda_nivel_3_sin_luz" : "banda_reto_sin_luz";
                AudioClip cue = Voice(cueId);
                double lead = (cue != null ? cue.length : 1.5f) + 0.2d;
                if (elapsed >= nextAnnouncement * 15d - lead)
                {
                    Narrate(nextAnnouncement == 1 ? "¡Tu momento! Sigue el ritmo." :
                        nextAnnouncement == 2 ? "La música descansa. Mantén el pulso." :
                        "Ahora sin luz. Recuerda el ritmo.", cueId);
                    nextAnnouncement++;
                }
            }
            if (nextEncouragement < encouragementTimes.Length &&
                elapsed >= encouragementTimes[nextEncouragement])
            {
                if (sectionHits > lastEncouragedHits &&
                    (voiceSource == null || !voiceSource.isPlaying))
                {
                    lastEncouragedHits = sectionHits;
                    int row = Mathf.Min(currentLevel, Encouragements.GetLength(0) - 1);
                    Narrate("¡Buen ritmo!", Encouragements[row, nextEncouragement]);
                }
                nextEncouragement++;
            }
            int challenge = rhythm.ChallengeIndexAt(now);
            if (challenge != previousChallenge)
            {
                if (challenge == 0)
                {
                    drum.SetBeatCueVisible(true);
                    SetHand(showHand);
                    Narrate(config.label + ": ¡la banda vuelve contigo!", "banda_vuelve_la_banda");
                }
                else
                {
                    SetHand(false);
                    drum.SetBeatCueVisible(challenge != 3);
                    // The announcement was played ahead of time; update only the subtitle.
                    Say(challenge == 1 ? "¡Tu momento! Toca cuando brille" :
                        challenge == 2 ? "¡Ahora tú! Toca cuando brille" :
                        "¡Ahora tú! Sigue el ritmo sin luz", null);
                }
                previousChallenge = challenge;
            }
            yield return null;
        }
        accepting = false;
        while (AudioSettings.dspTime < audioEnd) yield return null;
        rhythm.StopSequence();
        drum.EndBeatGuide();
        monkey.FinishPhrase();
        cat.FinishPhrase();
        bear.FinishPhrase();
        SetHand(false);
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator ShowHelp()
    {
        recorder.Metrics.helpCount++;
        if (ear != null) ear.gameObject.SetActive(true);
        if (helpVoice != null)
            yield return NarrateBeforeBeat("¡Escuchemos otra vez!", "", helpVoice);
        else
            yield return NarrateBeforeBeat("Si te equivocas, no pasa nada. Sigue escuchando.", "banda_error_amable");
        yield return new WaitForSeconds(0.2f);
        if (ear != null) ear.gameObject.SetActive(false);
    }

    private void SetHand(bool visible)
    {
        if (hand != null) hand.gameObject.SetActive(visible && allowHelp);
    }

    private void UpdateHud(string level)
    {
        if (topHud == null) return;
        if (topHud.ScoreText != null) topHud.ScoreText.text = "Puntaje " + score;
        if (topHud.LevelText != null) topHud.LevelText.text = level;
    }

    private void Update()
    {
        if (isPaused) return;
        if (hand != null && hand.gameObject.activeSelf)
        {
            float press = discovering
                ? 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 0.75f))
                : Mathf.Clamp01(drum.CueStrength(AudioSettings.dspTime) * 2f);
            RectTransform handRect = hand.transform as RectTransform;
            if (handRect != null)
                handRect.anchoredPosition = Vector2.Lerp(new Vector2(228f, -155f),
                    new Vector2(126f, -222f), press);
            hand.transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 0.84f, press);
        }
    }

    public void ReturnToMenu()
    {
        if (isPaused) ClosePause();
        if (!completed && recorder != null)
            recorder.Save(Mathf.Max(1, currentLevel + 1), Mathf.RoundToInt(rhythm.bpm), false);
        if (rhythm != null) rhythm.StopSequence();
        if (drum != null) drum.EndBeatGuide();
        SceneTransitionController.LoadScene("MainScene");
    }

    private void OnDestroy()
    {
        if (isPaused) ClosePause();
        if (rhythm != null)
        {
            rhythm.Beat -= OnBeat;
            rhythm.StopSequence();
        }
        BackgroundMusicPlayer.SetBandaMusicFocus(false);
    }

    private AudioClip Voice(string id)
    {
        if (voiceCatalog == null)
            voiceCatalog = Resources.Load<ChatterboxVoiceCatalog>("ChatterboxVoiceCatalog");
        return voiceCatalog != null ? voiceCatalog.Find(id) : null;
    }

    private AudioClip Narrate(string caption, string id, AudioClip fallback = null)
    {
        AudioClip clip = Voice(id) ?? fallback;
        Say(caption, clip);
        return clip;
    }

    private IEnumerator NarrateBeforeBeat(string caption, string id, AudioClip fallback = null)
    {
        AudioClip clip = Narrate(caption, id, fallback);
        // The next voice or the first musical beat must not cut the instruction short.
        if (clip != null && voiceSource != null)
            yield return new WaitForSeconds(clip.length + 0.15f);
    }

    private static void BindPauseButton(Transform box, string name, UnityEngine.Events.UnityAction action)
    {
        UnityEngine.UI.Button button = box.Find(name)?.GetComponent<UnityEngine.UI.Button>();
        if (button == null) return;
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }

    private void PauseGame()
    {
        if (isPaused || pausePanel == null) return;
        isPaused = true;
        pausePanel.transform.SetAsLastSibling();
        if (pauseView != null) pauseView.Show();
        else pausePanel.SetActive(true);
        // Unity freezes AudioSettings.dspTime while AudioListener.pause is true.
        // Scheduled WAV loops, scoring, character frames and challenge windows
        // therefore resume at the same musical phase, without rescheduling.
        AudioListener.pause = true;
        Time.timeScale = 0f;
    }

    private void ResumeGame()
    {
        if (!isPaused) return;
        ClosePause();
        if (pauseView != null) pauseView.Hide();
        else pausePanel.SetActive(false);
    }

    private void RestartFromPause()
    {
        ClosePause();
        if (pauseView != null) pauseView.InitializeHidden();
        else pausePanel.SetActive(false);
        Restart();
    }

    private void ClosePause()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // Can be connected to a future song-selection UI or changed in the Inspector.
    public void SelectSong(int index)
    {
        if (songs == null || index < 0 || index >= songs.Length || songs[index] == null ||
            songs[index].clip == null || songs[index].bpm <= 0f)
            return;
        selectedSongIndex = index;
        Restart();
    }
}
