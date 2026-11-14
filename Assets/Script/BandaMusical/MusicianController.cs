using System;
using UnityEngine;

public class MusicianController : MonoBehaviour
{
    public UnityEngine.UI.Image portrait;
    public Sprite[] beatFrames;
    [Tooltip("Clip del Image; se muestrea con el reloj DSP según beatsPerCycle.")]
    public AnimationClip beatAnimation;
    private double firstBeatDspTime;
    private double beatInterval;
    private bool animating;
    private Animator clipSampler;
    private int displayedFrame = -1;
    [Header("Animación visual (sin alterar el reloj de puntuación)")]
    [Min(1)] public int beatsPerCycle = 1;

    private void Awake()
    {
        clipSampler = GetComponent<Animator>();
    }

    public void OnBeat(int index, double dspTime, double interval)
    {
        if (!gameObject.activeInHierarchy || portrait == null) return;
        if (beatAnimation != null && clipSampler == null)
            clipSampler = GetComponent<Animator>();
        firstBeatDspTime = dspTime - index * interval;
        beatInterval = interval;
        animating = beatAnimation != null && clipSampler != null ||
            beatFrames != null && beatFrames.Length > 0;
        if (index == 0) displayedFrame = -1;
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
            // Sample only when the clip advances to another authored frame. The
            // DSP phase keeps it in step without rebuilding the UI every Update.
            double phase = (AudioSettings.dspTime - firstBeatDspTime) /
                (Mathf.Max(1, beatsPerCycle) * beatInterval);
            int count = Mathf.Max(1, Mathf.RoundToInt(beatAnimation.length * beatAnimation.frameRate));
            int frame = Mathf.Clamp((int)((phase - Math.Floor(phase)) * count), 0, count - 1);
            if (frame != displayedFrame)
            {
                beatAnimation.SampleAnimation(gameObject, frame / beatAnimation.frameRate);
                displayedFrame = frame;
            }
            return;
        }
        // Fallback for a musician without an authored UI clip.
        if (beatFrames == null || beatFrames.Length == 0) return;
        double progress = beatInterval > 0d
            ? Math.Max(0d, (AudioSettings.dspTime - firstBeatDspTime) /
                (Mathf.Max(1, beatsPerCycle) * beatInterval)) : 0d;
        int frameIndex = Mathf.Min((int)((progress - Math.Floor(progress)) * beatFrames.Length),
            beatFrames.Length - 1);
        if (frameIndex != displayedFrame)
        {
            portrait.sprite = beatFrames[frameIndex];
            displayedFrame = frameIndex;
        }
        // Keep the musician at one fixed size. The sprite sheet already has
        // pose changes; scaling the entire portrait on every beat looked like
        // alternating frames had different dimensions.
    }

    public void FinishPhrase()
    {
        animating = false;
        displayedFrame = -1;
        if (beatAnimation != null && clipSampler != null)
            beatAnimation.SampleAnimation(gameObject, 0f);
        else if (portrait != null && beatFrames != null && beatFrames.Length > 0)
            portrait.sprite = beatFrames[0];
    }
}
