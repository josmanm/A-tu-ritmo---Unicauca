using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;
    [SerializeField] [Range(0f, 1f)] private float menuVolume = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float gameplayVolume = 0.18f;
    [SerializeField] private float volumeTransitionDuration = 0.35f;
    [SerializeField] private bool playOnAwake = true;

    private static BackgroundMusicPlayer instance;
    private AudioSource audioSource;
    private Coroutine volumeRoutine;
    private bool gameHasMusicFocus;

    // Silences only the persistent background track; effects, narration and
    // the band's own scheduled song remain subject to their normal settings.
    public static void SetBandaMusicFocus(bool focused)
    {
        SetGameMusicFocus(focused);
    }

    public static void SetGameMusicFocus(bool focused)
    {
        if (instance == null) return;
        instance.gameHasMusicFocus = focused;
        instance.RefreshSceneVolume();
    }

    private void Awake()
    {
        GameSettings.EnsureInitialized();

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (transform.parent != null)
            transform.SetParent(null, true);

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        if (musicClip == null)
            musicClip = audioSource.clip;

        if (musicClip != null)
            audioSource.clip = musicClip;

        audioSource.volume = GetVolumeForScene(SceneManager.GetActiveScene().name);

        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameSettings.SettingsChanged += HandleSettingsChanged;

        if (playOnAwake)
            Play();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            GameSettings.SettingsChanged -= HandleSettingsChanged;
        }
    }

    private void OnValidate()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.volume = menuVolume * GameSettings.MusicVolume;
    }

    public void Play()
    {
        if (audioSource == null || audioSource.isPlaying)
            return;

        audioSource.Play();
    }

    public void Stop()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    public void SetVolume(float newVolume)
    {
        menuVolume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
            audioSource.volume = menuVolume * GameSettings.MusicVolume;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (audioSource == null)
            return;

        if (mode == LoadSceneMode.Single)
            gameHasMusicFocus = scene.name == "BandaMusical";

        if (!audioSource.isPlaying)
        {
            if (audioSource.clip == null && musicClip != null)
                audioSource.clip = musicClip;

            Play();
        }

        float targetVolume = GetVolumeForScene(scene.name);
        if (volumeRoutine != null)
            StopCoroutine(volumeRoutine);

        volumeRoutine = StartCoroutine(AnimateVolume(targetVolume));
    }

    private IEnumerator AnimateVolume(float targetVolume)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, volumeTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, k);
            yield return null;
        }

        audioSource.volume = targetVolume;
        volumeRoutine = null;
    }

    private float GetVolumeForScene(string sceneName)
    {
        if (gameHasMusicFocus || sceneName == "BandaMusical")
            return 0f;
        float baseVolume = sceneName == "MainScene" ? menuVolume : gameplayVolume;
        return baseVolume * GameSettings.MusicVolume;
    }

    private void HandleSettingsChanged()
    {
        RefreshSceneVolume();
    }

    private void RefreshSceneVolume()
    {
        if (audioSource == null)
            return;

        float targetVolume = GetVolumeForScene(SceneManager.GetActiveScene().name);
        if (volumeRoutine != null)
            StopCoroutine(volumeRoutine);

        volumeRoutine = StartCoroutine(AnimateVolume(targetVolume));
    }
}
