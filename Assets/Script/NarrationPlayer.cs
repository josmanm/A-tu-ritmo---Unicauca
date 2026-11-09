using UnityEngine;

public class NarrationPlayer : MonoBehaviour
{
    private static NarrationPlayer instance;
    private AudioSource voiceSource;
    private ChatterboxVoiceCatalog catalog;
    private string lastVoiceId;
    private float lastVoiceAt = -100f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<ChatterboxVoiceCatalog>("ChatterboxVoiceCatalog");
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
        GameSettings.SettingsChanged += RefreshVolume;
        RefreshVolume();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        GameSettings.SettingsChanged -= RefreshVolume;
        instance = null;
    }

    public static AudioClip FindClip(string id)
    {
        ChatterboxVoiceCatalog source = instance != null && instance.catalog != null
            ? instance.catalog : Resources.Load<ChatterboxVoiceCatalog>("ChatterboxVoiceCatalog");
        return source != null ? source.Find(id) : null;
    }

    public static float Say(string id, bool interrupt = true)
    {
        if (!Application.isPlaying || GameSettings.EffectsVolume <= 0f)
            return 0f;

        if (instance == null)
            new GameObject("NarrationPlayer").AddComponent<NarrationPlayer>();

        if (instance.catalog == null)
            instance.catalog = Resources.Load<ChatterboxVoiceCatalog>("ChatterboxVoiceCatalog");

        AudioClip clip = instance.catalog != null ? instance.catalog.Find(id) : null;
        if (clip == null)
            return 0f;

        if (instance.lastVoiceId == id && Time.unscaledTime - instance.lastVoiceAt < 0.8f)
            return RemainingSeconds;

        if (instance.voiceSource.isPlaying)
        {
            if (!interrupt)
                return 0f;
            instance.voiceSource.Stop();
        }

        instance.voiceSource.clip = clip;
        instance.voiceSource.Play();
        instance.lastVoiceId = id;
        instance.lastVoiceAt = Time.unscaledTime;
        return clip.length;
    }

    public static void Stop()
    {
        if (instance != null && instance.voiceSource != null)
            instance.voiceSource.Stop();
    }

    public static bool IsSpeaking => instance != null && instance.voiceSource != null && instance.voiceSource.isPlaying;

    public static bool IsPlaying(AudioClip clip) => clip != null && IsSpeaking && instance.voiceSource.clip == clip;

    public static float RemainingSeconds => IsSpeaking && instance.voiceSource.clip != null
        ? Mathf.Max(0f, instance.voiceSource.clip.length - instance.voiceSource.time) : 0f;

    private void RefreshVolume()
    {
        if (voiceSource != null)
            voiceSource.volume = GameSettings.EffectsVolume * 0.85f;
    }
}
