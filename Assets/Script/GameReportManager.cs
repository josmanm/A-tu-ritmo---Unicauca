using System;
using UnityEngine;

public class GameReportManager : MonoBehaviour
{
    public static GameReportManager Instance { get; private set; }

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

    public void ReportAttempt(AttemptReportData data, Action onSuccess, Action<string> onError)
    {
        PlayerProfileManager profileManager = PlayerProfileManager.Instance;
        SessionManager sessionManager = SessionManager.Instance;

        if (profileManager == null || !profileManager.HasActiveProfile)
        {
            onError?.Invoke("No hay perfil activo.");
            return;
        }

        if (sessionManager == null || !sessionManager.HasActiveSession)
        {
            onError?.Invoke("No hay sesion activa.");
            return;
        }

        data.attemptId = string.IsNullOrEmpty(data.attemptId) ? ReportUtility.GenerateId("attempt") : data.attemptId;
        data.playerId = profileManager.ActiveProfile.playerId;
        data.playerName = profileManager.ActiveProfile.name;
        data.avatar = profileManager.ActiveProfile.avatar;
        data.sessionId = sessionManager.CurrentSession.sessionId;
        data.playedAt = string.IsNullOrEmpty(data.playedAt) ? ReportUtility.UtcNowString() : data.playedAt;

        if (ReportOutbox.Instance == null)
        {
            onError?.Invoke("El almacenamiento local no esta disponible.");
            return;
        }

        ReportOutbox.Instance.QueueAttempt(data, onSuccess, onError);
    }
}
