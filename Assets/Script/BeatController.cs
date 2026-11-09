using UnityEngine;
using System;

public class BeatController : MonoBehaviour
{
    [Header("Tempo")]
    [Range(30, 160)] public float bpm = 80f;
    public bool startOnPlay = true;

    [Header("Scheduling")]
    [Min(0.05f)] public float startDelaySeconds = 0.35f;
    [Min(0.01f)] public float scheduleAheadSeconds = 0.1f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip metronomeClick;

    public event Action<double> OnBeat;

    public double NextBeatDspTime => nextBeatTime;
    public double IntervalSec => interval;
    public double LastBeatDspTime { get; private set; }
    public bool IsRunning => running && !paused;

    double interval;
    double nextBeatTime;
    double scheduledBeatTime;
    bool running;
    bool paused;
    double pausedAtDspTime;
    bool hasProducedFirstBeat;
    bool nextBeatScheduled;

    void Start()
    {
        RecalculateInterval();

        if (startOnPlay)
            StartBeats();
    }

    public void StartBeats()
    {
        RecalculateInterval();
        running = true;
        paused = false;
        hasProducedFirstBeat = false;
        nextBeatScheduled = false;

        nextBeatTime = AudioSettings.dspTime + startDelaySeconds;
        scheduledBeatTime = nextBeatTime;
        LastBeatDspTime = nextBeatTime - interval;
    }

    public void StopBeats()
    {
        running = false;
        paused = false;
        nextBeatScheduled = false;
        if (audioSource != null)
            audioSource.Stop();
    }

    public void PauseBeats()
    {
        if (!running || paused) return;
        paused = true;
        pausedAtDspTime = AudioSettings.dspTime;
        nextBeatScheduled = false;
        if (audioSource != null) audioSource.Stop();
    }

    public double ResumeBeats()
    {
        if (!running || !paused) return 0d;
        double elapsed = Math.Max(0d, AudioSettings.dspTime - pausedAtDspTime);
        nextBeatTime += elapsed;
        scheduledBeatTime += elapsed;
        LastBeatDspTime += elapsed;
        paused = false;
        return elapsed;
    }

    // Call from an OnBeat subscriber so the next scheduled click uses the new interval.
    public void ChangeTempoOnBeat(float newBpm)
    {
        bpm = Mathf.Clamp(newBpm, 30f, 160f);
        RecalculateInterval();
    }

    void Update()
    {
        if (!running || paused) return;

        double dsp = AudioSettings.dspTime;

        if (!nextBeatScheduled && dsp + scheduleAheadSeconds >= nextBeatTime)
        {
            scheduledBeatTime = nextBeatTime;
            nextBeatScheduled = true;

            if (audioSource != null && metronomeClick != null)
            {
                audioSource.clip = metronomeClick;
                audioSource.PlayScheduled(scheduledBeatTime);
            }
        }

        if (!nextBeatScheduled || dsp < scheduledBeatTime)
        {
            return;
        }

        hasProducedFirstBeat = true;
        LastBeatDspTime = scheduledBeatTime;
        OnBeat?.Invoke(LastBeatDspTime);

        nextBeatTime = scheduledBeatTime + interval;
        nextBeatScheduled = false;
    }

    public float GetSignedDeltaToNearestBeatMs(double dspTime, out double nearestBeatTime)
    {
        if (!running || interval <= 0.0)
        {
            nearestBeatTime = nextBeatTime;
            return 0f;
        }

        if (!hasProducedFirstBeat)
        {
            nearestBeatTime = nextBeatTime;
            return (float)((dspTime - nearestBeatTime) * 1000.0);
        }

        double previousBeatTime = nextBeatTime - interval;
        double previousDelta = Math.Abs(dspTime - previousBeatTime);
        double nextDelta = Math.Abs(nextBeatTime - dspTime);

        nearestBeatTime = previousDelta <= nextDelta ? previousBeatTime : nextBeatTime;
        return (float)((dspTime - nearestBeatTime) * 1000.0);
    }

    void OnValidate()
    {
        RecalculateInterval();
    }

    void RecalculateInterval()
    {
        interval = 60.0 / bpm;
    }
}
