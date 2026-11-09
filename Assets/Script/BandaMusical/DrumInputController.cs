using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class DrumInputController : MonoBehaviour, IPointerDownHandler
{
    public BandaMusicalGameManager game;
    public AudioSource source;
    public AudioClip hitClip;
    [Header("Volumen del tambor al tocar")]
    [Range(0.5f, 15f)] public float hitVolumeBoost = 1.8225f;
    public UnityEngine.UI.Image drumImage;
    public Sprite idleSprite;
    public Sprite hitSprite;
    [Header("Señal visual del pulso (mismo reloj DSP que evalúa los toques)")]
    [Min(0.1f)] public float anticipationSeconds = 0.25f;
    [Min(0.1f)] public float flashSeconds = 0.25f;
    public UnityEngine.UI.Outline beatOutline;
    private float hitUntil;
    private double firstBeatDsp;
    private double beatInterval;
    private int beatCount;
    private bool showingBeatGuide;
    private bool beatCueVisible = true;
    private AudioClip generatedHitClip;

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        if (hitClip == null)
        {
            generatedHitClip = CreateSoftDrum();
            hitClip = generatedHitClip;
        }
        if (drumImage == null) drumImage = GetComponent<UnityEngine.UI.Image>();
        if (beatOutline == null) beatOutline = GetComponent<UnityEngine.UI.Outline>();
        if (beatOutline == null) beatOutline = gameObject.AddComponent<UnityEngine.UI.Outline>();
        beatOutline.effectDistance = new Vector2(7f, -7f);
        beatOutline.useGraphicAlpha = true;
        beatOutline.enabled = false;
    }

    private void OnDestroy()
    {
        if (generatedHitClip != null) Destroy(generatedHitClip);
    }

    private static AudioClip CreateSoftDrum()
    {
        const int sampleRate = 22050;
        float[] samples = new float[sampleRate / 5];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / sampleRate;
            samples[i] = Mathf.Sin(2f * Mathf.PI * (115f * t - 160f * t * t)) *
                         Mathf.Exp(-24f * t) * 0.65f;
        }
        AudioClip clip = AudioClip.Create("BandaSoftDrum", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void BeginBeatGuide(double firstBeat, double interval, int count)
    {
        firstBeatDsp = firstBeat;
        beatInterval = interval;
        beatCount = count;
        showingBeatGuide = interval > 0 && count > 0;
        beatCueVisible = true;
    }

    // Hides the rhythmic glow without removing the playable drum or its clock.
    public void SetBeatCueVisible(bool visible)
    {
        beatCueVisible = visible;
        if (visible) return;
        if (beatOutline != null) beatOutline.enabled = false;
        if (drumImage != null) drumImage.color = Color.white;
        transform.localScale = Vector3.one;
    }

    public void EndBeatGuide()
    {
        showingBeatGuide = false;
        beatCueVisible = true;
        if (drumImage != null) drumImage.color = Color.white;
        if (beatOutline != null) beatOutline.enabled = false;
        transform.localScale = Vector3.one;
    }

    // Public for a focused timing test: anticipation never changes scoring.
    public float CueStrength(double dspTime)
    {
        if (!showingBeatGuide || !beatCueVisible) return 0f;
        int nearest = Mathf.Clamp((int)Math.Round((dspTime - firstBeatDsp) / beatInterval), 0, beatCount - 1);
        double offset = dspTime - firstBeatDsp - nearest * beatInterval;
        return offset < 0d
            ? Mathf.Clamp01(1f + (float)(offset / Mathf.Max(0.1f, anticipationSeconds))) * 0.4f
            : Mathf.Clamp01(1f - (float)(offset / Mathf.Max(0.1f, flashSeconds)));
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data.pointerId < 0 && data.button != PointerEventData.InputButton.Left) return;
        Strike();
    }

    public void Strike()
    {
        if (game != null && game.IsPaused) return;
        // El sonido del golpe no depende de que el ritmo esté aceptando respuestas.
        if (source != null && hitClip != null)
        {
            // No audio ducking on this source: challenge silences only music.
            source.mute = false;
            source.PlayOneShot(hitClip, hitVolumeBoost * GameSettings.EffectsVolume);
        }
        hitUntil = Time.unscaledTime + 0.13f;
        if (game != null) game.OnDrumTap(AudioSettings.dspTime);
    }

    private void Update()
    {
        if (game != null && game.IsPaused) return;
        bool pressed = Time.unscaledTime < hitUntil;
        float cue = CueStrength(AudioSettings.dspTime);
        transform.localScale = Vector3.one * (pressed ? 0.94f : 1f + 0.12f * cue);
        if (drumImage != null && idleSprite != null)
        {
            drumImage.sprite = pressed && hitSprite != null ? hitSprite : idleSprite;
            drumImage.color = Color.Lerp(Color.white, new Color(1f, 0.87f, 0.43f), cue);
        }
        if (beatOutline != null)
        {
            beatOutline.enabled = cue > 0.02f;
            beatOutline.effectColor = new Color(1f, 0.78f, 0.16f, cue * 0.85f);
        }
#if ENABLE_INPUT_SYSTEM && (UNITY_EDITOR || UNITY_STANDALONE)
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            Strike();
#endif
    }
}
