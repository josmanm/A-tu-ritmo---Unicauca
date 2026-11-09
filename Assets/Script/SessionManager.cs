using System;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }
    public SessionReportData CurrentSession { get; private set; }
    public bool HasActiveSession => CurrentSession != null && !string.IsNullOrEmpty(CurrentSession.sessionId);

    private DateTime sessionStartUtc;
    private PlayerProfileData profileBeforePause;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        EndCurrentSession(null, null);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && HasActiveSession)
        {
            profileBeforePause = new PlayerProfileData
            {
                playerId = CurrentSession.playerId,
                name = CurrentSession.playerName,
                avatar = CurrentSession.avatar,
            };
            EndCurrentSession(null, error => Debug.LogWarning("No se pudo cerrar la sesion: " + error));
        }
        else if (!paused && profileBeforePause != null)
        {
            PlayerProfileData profile = profileBeforePause;
            profileBeforePause = null;
            if (!HasActiveSession && PlayerProfileManager.Instance != null &&
                PlayerProfileManager.Instance.ActiveProfile?.playerId == profile.playerId)
                StartSessionForPlayer(profile, null, error => Debug.LogWarning("No se pudo reanudar la sesion: " + error));
        }
    }

    public void StartSessionForPlayer(PlayerProfileData profile, Action<SessionReportData> onSuccess, Action<string> onError)
    {
        if (profile == null || string.IsNullOrEmpty(profile.playerId))
        {
            onError?.Invoke("No hay perfil activo.");
            return;
        }

        if (HasActiveSession)
        {
            if (CurrentSession.playerId == profile.playerId)
            {
                onSuccess?.Invoke(CurrentSession);
                return;
            }

            bool closed = false;
            EndCurrentSession(() => closed = true, onError);
            if (!closed)
                return;
        }

        if (ReportOutbox.Instance == null)
        {
            onError?.Invoke("El almacenamiento local no esta disponible.");
            return;
        }

        sessionStartUtc = DateTime.UtcNow;
        SessionReportData sessionToStart = new SessionReportData
        {
            sessionId = ReportUtility.GenerateId("session"),
            playerId = profile.playerId,
            playerName = profile.name,
            avatar = profile.avatar,
            startedAt = ReportUtility.UtcNowString(),
            endedAt = string.Empty,
            totalTimeSeconds = 0,
        };

        ReportOutbox.Instance.QueueSession(sessionToStart, () =>
        {
            CurrentSession = sessionToStart;
            onSuccess?.Invoke(sessionToStart);
        }, onError);
    }

    public void EndCurrentSession(Action onSuccess, Action<string> onError)
    {
        if (!HasActiveSession)
        {
            onSuccess?.Invoke();
            return;
        }

        if (ReportOutbox.Instance == null)
        {
            onError?.Invoke("El almacenamiento local no esta disponible.");
            return;
        }

        SessionReportData sessionToClose = new SessionReportData
        {
            sessionId = CurrentSession.sessionId,
            playerId = CurrentSession.playerId,
            playerName = CurrentSession.playerName,
            avatar = CurrentSession.avatar,
            startedAt = CurrentSession.startedAt,
            endedAt = ReportUtility.UtcNowString(),
            totalTimeSeconds = Mathf.Max(0, Mathf.RoundToInt((float)(DateTime.UtcNow - sessionStartUtc).TotalSeconds)),
        };

        ReportOutbox.Instance.QueueSession(sessionToClose, () =>
        {
            CurrentSession = null;
            onSuccess?.Invoke();
        }, onError);
    }
}
