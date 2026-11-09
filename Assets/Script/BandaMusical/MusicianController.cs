using System;
using UnityEngine;

public class MusicianController : MonoBehaviour
{
    public UnityEngine.UI.Image portrait;
    public Sprite[] beatFrames;
    [Tooltip("Clip opcional para el Image. Se muestrea con el reloj DSP: un ciclo cada dos pulsos.")]
    public AnimationClip beatAnimation;
    private double lastBeatDspTime;
    private double firstBeatDspTime;
    private double beatInterval;
    private int beatIndex;
    private bool animating;
    private Animator clipSampler;

    private void Awake()
    {
        clipSampler = GetComponent<Animator>();
    }

    public void OnBeat(int index, double dspTime, double interval)
    {
        if (!gameObject.activeInHierarchy || portrait == null) return;
        if (beatAnimation != null && clipSampler == null)
            clipSampler = GetComponent<Animator>();
        beatIndex = index;
        lastBeatDspTime = dspTime;
        firstBeatDspTime = dspTime - index * interval;
        beatInterval = interval;
        animating = beatAnimation != null && clipSampler != null ||
            beatFrames != null && beatFrames.Length > 0;
        UpdateFrame();
    }

    private void Update()
    {
        if (animating) UpdateFrame();
    }

    private void UpdateFrame()
    {
        if (beatAnimation != null && clipSampler != null && beatAnimation.length > 0f && beatInterval > 0d)
        {
            // Animator.speed alone can drift. Sample the authored UI animation
            // at the exact phase shared by music, cue and tap scoring.
            double twoBeatPhase = (AudioSettings.dspTime - firstBeatDspTime) / (2d * beatInterval);
            float clipTime = (float)((twoBeatPhase - Math.Floor(twoBeatPhase)) * beatAnimation.length);
            beatAnimation.SampleAnimation(gameObject, clipTime);
            return;
        }
        // A full 16-frame action spans two beats; all in-between poses are
        // displayed at ~10.7 FPS at 80 BPM instead of one frame per beat.
        if (beatFrames == null || beatFrames.Length == 0) return;
        int framesPerBeat = (beatFrames.Length + 1) / 2;
        double progress = beatInterval > 0d
            ? Math.Max(0d, (AudioSettings.dspTime - lastBeatDspTime) / beatInterval) : 0d;
        int withinBeat = Mathf.Clamp((int)(progress * framesPerBeat), 0, framesPerBeat - 1);
        int frame = (beatIndex % 2) * framesPerBeat + withinBeat;
        portrait.sprite = beatFrames[Mathf.Min(frame, beatFrames.Length - 1)];
        // Keep the musician at one fixed size. The sprite sheet already has
        // pose changes; scaling the entire portrait on every beat looked like
        // alternating frames had different dimensions.
    }

    public void FinishPhrase()
    {
        animating = false;
        if (beatAnimation != null && clipSampler != null)
            beatAnimation.SampleAnimation(gameObject, 0f);
        else if (portrait != null && beatFrames != null && beatFrames.Length > 0)
            portrait.sprite = beatFrames[0];
    }
}
