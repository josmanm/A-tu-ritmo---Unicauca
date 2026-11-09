using System;
using UnityEngine;

// One DSP clock drives the song, character cues and tap evaluation.
public class RhythmManager : MonoBehaviour
{
    [Range(40f, 160f)] public float bpm = 80f;
    [Range(0.08f, 0.4f)] public float toleranceSeconds = 0.25f;
    [Min(0f)] public float beatOffsetSeconds;
    [Min(0.15f)] public float leadInSeconds = 0.45f;
    public AudioSource musicSource;
    public AudioClip music;
    public bool useMusic = true;
    [Header("Audibility (soft-limited to avoid clipping)")]
    [Range(1f, 3f)] public float musicPresence = 2.2f;

    public event Action<int, double> Beat;
    public double Interval => 60d / Math.Max(1f, bpm);
    public double FirstBeatDspTime { get; private set; }
    public bool Running { get; private set; }
    public bool PlayingSong { get; private set; }
    public int BeatCount { get; private set; }
    private int emitted;
    private AudioSource backgroundSource;
    private double levelStartDspTime;
    private float challengeDurationSeconds;
    private float firstChallengeMusicFraction;
    private const double ChallengeFadeSeconds = 0.35d;
    private AudioClip enhancedMusic;
    private AudioClip enhancedFrom;
    private float enhancedAtPresence;
    private int enhancedFrames;

    // BeatMap describes the WAV at its recorded BPM. Both sources are sped up
    // together and the DSP beat interval uses the same ratio (including loops).
    public bool StartKitSequence(MusicKit kit, int totalBeats, int guidedBeats,
        float challengeSeconds, float firstChallengeFraction)
    {
        StopSequence();
        if (!useMusic || musicSource == null || kit == null || !kit.IsValid() ||
            totalBeats <= 0 || bpm <= 0f) return false;
        if (backgroundSource == null)
        {
            backgroundSource = gameObject.AddComponent<AudioSource>();
            backgroundSource.playOnAwake = false;
        }
        float speed = bpm / kit.originalBpm;
        double start = AudioSettings.dspTime + Math.Max(0.15f, leadInSeconds);
        double end = start + totalBeats * Interval;
        int introBeats = Mathf.Min(guidedBeats, totalBeats);
        musicSource.clip = kit.withPulse;
        musicSource.pitch = speed;
        musicSource.loop = false;
        musicSource.volume = GameSettings.MusicVolume;
        backgroundSource.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
        backgroundSource.spatialBlend = musicSource.spatialBlend;
        backgroundSource.clip = kit.withoutClick;
        backgroundSource.pitch = speed;
        backgroundSource.loop = true;
        backgroundSource.volume = GameSettings.MusicVolume;
        // Start the unassisted version at the corresponding point of the phrase.
        // Its sample position wraps after beatCount, never after guidedBeats.
        backgroundSource.timeSamples = Mathf.Clamp(Mathf.RoundToInt(
            (introBeats % kit.beatCount) * 60f / kit.originalBpm * kit.withoutClick.frequency),
            0, kit.withoutClick.samples - 1);
        FirstBeatDspTime = start;
        levelStartDspTime = start;
        BeatCount = totalBeats;
        emitted = 0;
        beatOffsetSeconds = 0f;
        challengeDurationSeconds = Mathf.Clamp(challengeSeconds, 0f, 15f);
        firstChallengeMusicFraction = Mathf.Clamp01(firstChallengeFraction);
        Running = PlayingSong = true;
        musicSource.PlayScheduled(start);
        musicSource.SetScheduledEndTime(start + introBeats * Interval);
        if (introBeats < totalBeats)
        {
            backgroundSource.PlayScheduled(start + introBeats * Interval);
            backgroundSource.SetScheduledEndTime(end);
        }
        return true;
    }

    private AudioClip SongToPlay(double requiredSeconds)
    {
        if (music == null || musicPresence <= 1.01f) return music;
        int frames = Math.Min(music.samples,
            Math.Max(1, (int)Math.Ceiling(requiredSeconds * music.frequency)));
        if (enhancedMusic != null && enhancedFrom == music &&
            Mathf.Approximately(enhancedAtPresence, musicPresence) && enhancedFrames >= frames)
            return enhancedMusic;
        if (enhancedMusic != null) ReleaseEnhancedMusic();
        enhancedMusic = null;
        // The original loop peaks close to full scale. A soft limiter brings
        // quieter samples forward without clipping and preserves exact timing.
        music.LoadAudioData();
        // Only process the portion actually heard in this exercise. A 2-minute
        // song must not allocate two minutes of duplicate PCM on a tablet.
        float[] samples = new float[frames * music.channels];
        if (!music.GetData(samples, 0))
        {
            Debug.LogWarning("Banda: no se pudo preparar el loop; se usará el audio original.");
            return music;
        }
        double normalizer = Math.Tanh(musicPresence);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)(Math.Tanh(samples[i] * musicPresence) / normalizer);
        enhancedMusic = AudioClip.Create("BandaMusicEnhanced", frames, music.channels,
            music.frequency, false);
        enhancedMusic.SetData(samples, 0);
        enhancedFrom = music;
        enhancedAtPresence = musicPresence;
        enhancedFrames = frames;
        return enhancedMusic;
    }

    public void StartSequence(int beats, bool playMusic)
    {
        StopSequence();
        BeatCount = Math.Max(1, beats);
        PlayingSong = playMusic && useMusic && music != null && musicSource != null;
        double requiredSeconds = Math.Max(0d, beatOffsetSeconds) +
            (BeatCount - 1) * Interval + toleranceSeconds + 0.5d;
        if (PlayingSong && requiredSeconds >= music.length)
        {
            Debug.LogWarning("Banda: la pista es más corta que la sección; se usarán pulsos de demostración sin música para evitar deriva al repetir el archivo.");
            PlayingSong = false;
        }
        // Prepare the clip BEFORE setting the DSP origin: processing a long
        // song must never consume the scheduled lead-in time.
        AudioClip playback = PlayingSong ? SongToPlay(requiredSeconds) : null;
        double songStart = AudioSettings.dspTime + Math.Max(0.15f, leadInSeconds);
        FirstBeatDspTime = songStart + (PlayingSong ? beatOffsetSeconds : 0d);
        emitted = 0;
        Running = true;
        if (PlayingSong)
        {
            musicSource.clip = playback;
            musicSource.loop = false;
            musicSource.volume = GameSettings.MusicVolume;
            musicSource.PlayScheduled(songStart);
        }
    }

    // The WAVs are one 16-beat phrase each. Start with one guided phrase,
    // then loop the backing WAV until the exact end of the 60-second level.
    public bool StartLoopedSequence(AudioClip guided, AudioClip background, int beatsPerLoop, int totalBeats,
        float challengeSeconds = 5f, float firstChallengeFraction = 0.6f)
    {
        StopSequence();
        if (!useMusic || musicSource == null || guided == null || background == null ||
            beatsPerLoop <= 0 || totalBeats <= beatsPerLoop || bpm <= 0f)
            return false;

        double phraseSeconds = beatsPerLoop * Interval;
        if (Math.Abs(guided.length - phraseSeconds) > 0.015d ||
            Math.Abs(background.length - phraseSeconds) > 0.015d)
        {
            Debug.LogWarning("Banda: los WAV del nivel no duran exactamente " + beatsPerLoop +
                " pulsos a " + bpm + " BPM; no se programará un loop desincronizado.");
            return false;
        }

        if (backgroundSource == null)
        {
            backgroundSource = gameObject.AddComponent<AudioSource>();
            backgroundSource.playOnAwake = false;
        }
        // Match the source's routing, so both versions obey the same music volume.
        backgroundSource.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
        backgroundSource.spatialBlend = musicSource.spatialBlend;
        backgroundSource.volume = musicSource.volume = GameSettings.MusicVolume;
        musicSource.clip = guided;
        musicSource.loop = false;
        backgroundSource.clip = background;
        backgroundSource.loop = true;

        double songStart = AudioSettings.dspTime + Math.Max(0.15f, leadInSeconds);
        FirstBeatDspTime = songStart; // WAVs begin on the beat; no MP3 encoder offset.
        BeatCount = totalBeats;
        levelStartDspTime = songStart;
        challengeDurationSeconds = Mathf.Clamp(challengeSeconds, 0f, 15f);
        firstChallengeMusicFraction = Mathf.Clamp01(firstChallengeFraction);
        emitted = 0;
        Running = true;
        PlayingSong = true;
        musicSource.PlayScheduled(songStart);
        backgroundSource.PlayScheduled(songStart + phraseSeconds);
        backgroundSource.SetScheduledEndTime(songStart + totalBeats * Interval);
        return true;
    }

    public void StopSequence()
    {
        Running = false;
        PlayingSong = false;
        challengeDurationSeconds = 0f;
        levelStartDspTime = 0d;
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.pitch = 1f;
            musicSource.volume = GameSettings.MusicVolume;
        }
        if (backgroundSource != null)
        {
            backgroundSource.Stop();
            backgroundSource.pitch = 1f;
            backgroundSource.volume = GameSettings.MusicVolume;
        }
    }

    // The same DSP origin drives the three five-second challenges, the audio,
    // the visual cue and scoring. A challenge never stops the beat clock.
    public int ChallengeIndexAt(double dspTime)
    {
        if (!Running || !PlayingSong || challengeDurationSeconds <= 0f) return 0;
        double elapsed = dspTime - levelStartDspTime;
        for (int challenge = 1; challenge <= 3; challenge++)
        {
            double start = challenge * 15d;
            if (elapsed >= start && elapsed < start + challengeDurationSeconds)
                return challenge;
        }
        return 0;
    }

    // Gain is relative to GameSettings.MusicVolume: 0 mutes only this level's
    // music, never effects or the saved global setting.
    public float MusicGainAt(double dspTime)
    {
        int challenge = ChallengeIndexAt(dspTime);
        if (challenge == 0) return 1f;
        double elapsed = dspTime - levelStartDspTime;
        double withinWindow = elapsed - challenge * 15d;
        double fade = Math.Min(1d, Math.Min(withinWindow / ChallengeFadeSeconds,
            (challengeDurationSeconds - withinWindow) / ChallengeFadeSeconds));
        float target = challenge == 1 ? firstChallengeMusicFraction : 0f;
        return 1f - (1f - target) * (float)fade;
    }

    private void Update()
    {
        if (!Running) return;
        double now = AudioSettings.dspTime;
        if (backgroundSource != null && PlayingSong && challengeDurationSeconds > 0f)
        {
            float volume = GameSettings.MusicVolume * MusicGainAt(now);
            musicSource.volume = volume;
            backgroundSource.volume = volume;
        }
        while (emitted < BeatCount && now >= FirstBeatDspTime + emitted * Interval)
        {
            int beat = emitted++;
            Beat?.Invoke(beat, FirstBeatDspTime + beat * Interval);
        }
    }

    public int NearestBeat(double dspTime, out float signedOffsetMs)
    {
        int index = Mathf.Clamp((int)Math.Round((dspTime - FirstBeatDspTime) / Interval), 0, BeatCount - 1);
        signedOffsetMs = (float)((dspTime - (FirstBeatDspTime + index * Interval)) * 1000d);
        return index;
    }

    private void OnDestroy()
    {
        ReleaseEnhancedMusic();
    }

    private void ReleaseEnhancedMusic()
    {
        if (enhancedMusic == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(enhancedMusic);
        else Destroy(enhancedMusic);
#else
        Destroy(enhancedMusic);
#endif
        enhancedMusic = null;
    }
}
