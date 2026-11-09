using System;
using UnityEngine;
using UnityEngine.UI;

public class SimonMetronomeController : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Image metronomeImage;
    [SerializeField] private RectTransform pendulum;
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField] private float swingAngle = 28f;
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color pulseColor = new Color(1f, 0.9f, 0.35f, 1f);

    [Header("Audio Opcional")]
    [SerializeField] private bool useMetronomeClick;
    [SerializeField] private AudioSource metronomeAudioSource;
    [SerializeField] private AudioClip metronomeClickClip;
    [SerializeField] [Range(0f, 1f)] private float metronomeClickVolume = 0.2f;

    private float bpm = 60f;
    private bool isRunning;
    private double firstBeatDspTime;
    private double nextBeatDspTime;
    private double pausedAtDspTime;
    private bool wasRunningBeforePause;
    private long lastVisualBeat = -1;
    private Vector3 baseScale = Vector3.one;
    private int currentFrameIndex = -1;
    private float baseClickVolume;
    private bool visualVisible = true;

    public double FirstBeatDspTime => firstBeatDspTime;
    public double BeatDurationSeconds => 60.0 / bpm;

    private void Awake()
    {
        if (pendulum != null)
            baseScale = pendulum.localScale;

        if (metronomeAudioSource != null)
        {
            metronomeAudioSource.playOnAwake = false;
            baseClickVolume = metronomeClickVolume;
        }

        GameSettings.SettingsChanged += ApplyGameSettings;
        ApplyGameSettings();

        SetIdleState();
    }

    private void Update()
    {
        if (!isRunning || bpm <= 0f)
            return;

        double now = AudioSettings.dspTime;
        double beatDuration = BeatDurationSeconds;
        if (now + 0.1 >= nextBeatDspTime)
        {
            if (nextBeatDspTime > now && useMetronomeClick && metronomeAudioSource != null && metronomeClickClip != null)
            {
                metronomeAudioSource.clip = metronomeClickClip;
                metronomeAudioSource.volume = metronomeClickVolume;
                metronomeAudioSource.PlayScheduled(nextBeatDspTime);
            }
            nextBeatDspTime += beatDuration;
            if (nextBeatDspTime <= now)
                nextBeatDspTime += Math.Ceiling((now - nextBeatDspTime) / beatDuration + 1.0) * beatDuration;
        }

        if (now < firstBeatDspTime)
            return;

        double elapsed = now - firstBeatDspTime;
        long currentBeat = (long)(elapsed / beatDuration);
        float normalizedTime = (float)((elapsed - currentBeat * beatDuration) / beatDuration);
        float swing = Mathf.Sin(normalizedTime * Mathf.PI * 2f - Mathf.PI * 0.5f);

        if (HasFrameAnimation())
        {
            UpdateFrameAnimation(normalizedTime);
        }
        else if (pendulum != null)
        {
            pendulum.localRotation = Quaternion.Euler(0f, 0f, swing * swingAngle);

            float pulseAmount = Mathf.Clamp01(1f - normalizedTime * 4f);
            pendulum.localScale = Vector3.Lerp(baseScale, baseScale * pulseScale, pulseAmount);
        }

        if (currentBeat != lastVisualBeat)
        {
            lastVisualBeat = currentBeat;
            if (visualVisible && metronomeImage != null)
                metronomeImage.color = pulseColor;
        }

        if (metronomeImage != null && normalizedTime > 0.12f)
            metronomeImage.color = idleColor;
    }

    private void OnDestroy()
    {
        GameSettings.SettingsChanged -= ApplyGameSettings;
    }

    public void SetBpm(float newBpm)
    {
        bpm = Mathf.Max(1f, newBpm);
    }

    public void StartMetronome(float newBpm)
    {
        SetBpm(newBpm);
        firstBeatDspTime = AudioSettings.dspTime + 0.2;
        nextBeatDspTime = firstBeatDspTime;
        lastVisualBeat = -1;
        isRunning = true;
        wasRunningBeforePause = false;
        currentFrameIndex = -1;
        if (metronomeAudioSource != null)
            metronomeAudioSource.Stop();
    }

    public void StartMetronomeFromTap(float newBpm, double tapDspTime)
    {
        SetBpm(newBpm);
        firstBeatDspTime = tapDspTime;
        nextBeatDspTime = firstBeatDspTime + BeatDurationSeconds;
        lastVisualBeat = -1;
        isRunning = true;
        wasRunningBeforePause = false;
        currentFrameIndex = -1;
        if (metronomeAudioSource != null)
        {
            metronomeAudioSource.Stop();
            if (useMetronomeClick && metronomeClickClip != null)
            {
                metronomeAudioSource.volume = metronomeClickVolume;
                metronomeAudioSource.PlayOneShot(metronomeClickClip);
            }
        }
    }

    public void SetVisualVisible(bool visible)
    {
        visualVisible = visible;

        if (metronomeImage != null)
            metronomeImage.gameObject.SetActive(visible);

        if (pendulum != null)
            pendulum.gameObject.SetActive(visible);
    }

    public void StopMetronome()
    {
        isRunning = false;
        wasRunningBeforePause = false;
        if (metronomeAudioSource != null)
            metronomeAudioSource.Stop();
        lastVisualBeat = -1;
        currentFrameIndex = -1;
        SetIdleState();
    }

    public void PauseMetronome()
    {
        wasRunningBeforePause = isRunning;
        pausedAtDspTime = AudioSettings.dspTime;
        isRunning = false;
        if (metronomeAudioSource != null)
            metronomeAudioSource.Stop();
    }

    public void ResumeMetronome()
    {
        if (bpm > 0f && wasRunningBeforePause)
        {
            double now = AudioSettings.dspTime;
            firstBeatDspTime += now - pausedAtDspTime;
            double elapsed = Math.Max(0.0, now - firstBeatDspTime);
            nextBeatDspTime = firstBeatDspTime + Math.Ceiling(elapsed / BeatDurationSeconds) * BeatDurationSeconds;
            if (nextBeatDspTime < now + 0.1)
                nextBeatDspTime += BeatDurationSeconds;
            isRunning = true;
            wasRunningBeforePause = false;
        }
    }

    private void SetIdleState()
    {
        if (!visualVisible)
            return;

        if (HasFrameAnimation())
        {
            SetFrame(0);
        }
        else if (pendulum != null)
        {
            pendulum.localRotation = Quaternion.identity;
            pendulum.localScale = baseScale;
        }

        if (metronomeImage != null)
            metronomeImage.color = idleColor;
    }

    private bool HasFrameAnimation()
    {
        return metronomeImage != null && animationFrames != null && animationFrames.Length > 0;
    }

    private void UpdateFrameAnimation(float normalizedTime)
    {
        if (!HasFrameAnimation())
            return;

        int frameCount = animationFrames.Length;
        int frameIndex = Mathf.Clamp(Mathf.FloorToInt(normalizedTime * frameCount), 0, frameCount - 1);
        SetFrame(frameIndex);
    }

    private void SetFrame(int frameIndex)
    {
        if (!HasFrameAnimation())
            return;

        frameIndex = Mathf.Clamp(frameIndex, 0, animationFrames.Length - 1);
        if (currentFrameIndex == frameIndex)
            return;

        currentFrameIndex = frameIndex;
        metronomeImage.sprite = animationFrames[frameIndex];
        metronomeImage.preserveAspect = true;
    }

    private void ApplyGameSettings()
    {
        metronomeClickVolume = baseClickVolume * GameSettings.EffectsVolume;
    }
}
