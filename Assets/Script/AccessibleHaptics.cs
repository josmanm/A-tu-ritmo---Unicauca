using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// One shared, low-intensity vocabulary for all three games.
public sealed class AccessibleHaptics : MonoBehaviour
{
    private static AccessibleHaptics instance;
    private static float lastCueTime = -10f;
    private static bool lastCueWasError;

    private Coroutine gamepadRoutine;
    private Coroutine iosRoutine;

    public static void Success() => Play(false);
    public static void Error() => Play(true);

    private static void Play(bool isError)
    {
        if (!Application.isPlaying || !GameSettings.HapticsEnabled)
            return;

        float now = Time.unscaledTime;
        // Never pile pulses on top of each other. An error may replace a success cue.
        if (now - lastCueTime < 0.12f && (!isError || lastCueWasError))
            return;
        lastCueTime = now;
        lastCueWasError = isError;

#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(isError);
#endif

#if UNITY_IOS && !UNITY_EDITOR
        EnsureInstance();
        instance.VibrateIos(isError);
#endif

#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            EnsureInstance();
            instance.VibrateGamepad(isError);
        }
#endif
    }

    public static void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                vibrator?.Call("cancel");
        }
        catch (AndroidJavaException) { }
#endif

        if (instance == null)
            return;

        if (instance.iosRoutine != null)
        {
            instance.StopCoroutine(instance.iosRoutine);
            instance.iosRoutine = null;
        }
        if (instance.gamepadRoutine != null)
        {
            instance.StopCoroutine(instance.gamepadRoutine);
            instance.gamepadRoutine = null;
        }
#if ENABLE_INPUT_SYSTEM
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
#endif
    }

    private static void EnsureInstance()
    {
        if (instance == null)
            new GameObject("AccessibleHaptics").AddComponent<AccessibleHaptics>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Stop();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;
        Stop();
        instance = null;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool androidWarningLogged;

    private static void VibrateAndroid(bool isError)
    {
        // Success: one gentle 55 ms pulse. Guidance/error: two separated soft pulses.
        long[] timing = isError ? new long[] { 0, 65, 85, 55 } : new long[] { 0, 55 };
        int[] amplitude = isError ? new[] { 0, 135, 0, 110 } : new[] { 0, 95 };
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    return;

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                    {
                        using (var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = vibrationEffect.CallStatic<AndroidJavaObject>("createWaveform", timing, amplitude, -1))
                            vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        vibrator.Call("vibrate", timing, -1);
                    }
                }
            }
        }
        catch (AndroidJavaException error)
        {
            if (!androidWarningLogged)
            {
                androidWarningLogged = true;
                Debug.LogWarning("No se pudo activar la vibración: " + error.Message);
            }
            // Also lets Unity declare VIBRATE in the generated Android manifest.
            try { Handheld.Vibrate(); } catch (AndroidJavaException) { }
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    private void VibrateIos(bool isError)
    {
        if (iosRoutine != null)
            StopCoroutine(iosRoutine);
        Handheld.Vibrate();
        if (isError)
            iosRoutine = StartCoroutine(SecondIosPulse());
    }

    private IEnumerator SecondIosPulse()
    {
        yield return new WaitForSecondsRealtime(0.18f);
        if (GameSettings.HapticsEnabled)
            Handheld.Vibrate();
        iosRoutine = null;
    }
#endif

#if ENABLE_INPUT_SYSTEM
    private void VibrateGamepad(bool isError)
    {
        if (gamepadRoutine != null)
            StopCoroutine(gamepadRoutine);
        gamepadRoutine = StartCoroutine(GamepadPulse(isError));
    }

    private IEnumerator GamepadPulse(bool isError)
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
            yield break;

        pad.SetMotorSpeeds(isError ? 0.18f : 0.1f, isError ? 0.15f : 0.08f);
        yield return new WaitForSecondsRealtime(isError ? 0.065f : 0.055f);
        pad.SetMotorSpeeds(0f, 0f);
        if (isError && GameSettings.HapticsEnabled)
        {
            yield return new WaitForSecondsRealtime(0.085f);
            pad.SetMotorSpeeds(0.12f, 0.10f);
            yield return new WaitForSecondsRealtime(0.055f);
            pad.SetMotorSpeeds(0f, 0f);
        }
        gamepadRoutine = null;
    }
#endif
}
