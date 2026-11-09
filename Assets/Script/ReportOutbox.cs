using System;
using System.Collections.Generic;
using System.IO;
using Firebase.Extensions;
using UnityEngine;

public class ReportOutbox : MonoBehaviour
{
    [Serializable]
    private class PendingDocument
    {
        public string collection;
        public string documentId;
        public string payload;
    }

    [Serializable]
    private class PendingDocuments
    {
        public List<PendingDocument> documents = new List<PendingDocument>();
    }

    private const float RetrySeconds = 15f;
    private const string FileName = "pending-reports.json";

    public static ReportOutbox Instance { get; private set; }
    public int PendingCount => pending.documents.Count;

    private PendingDocuments pending = new PendingDocuments();
    private string filePath;
    private bool canPersist = true;
    private bool sending;
    private float nextRetryTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        filePath = Path.Combine(Application.persistentDataPath, FileName);
        LoadPending();
    }

    private void Start()
    {
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.Ready += HandleFirebaseReady;

        Flush();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.Ready -= HandleFirebaseReady;
        Instance = null;
    }

    private void HandleFirebaseReady()
    {
        nextRetryTime = 0f;
        Flush();
    }

    private void Update()
    {
        if (pending.documents.Count > 0 && Time.unscaledTime >= nextRetryTime)
            Flush();
    }

    public bool QueueSession(SessionReportData session, Action onSaved, Action<string> onError)
    {
        return Queue("sessions", session.sessionId, JsonUtility.ToJson(session), onSaved, onError);
    }

    public bool QueueProfile(PlayerProfileData profile, Action onSaved, Action<string> onError)
    {
        return Queue("players", profile.playerId, JsonUtility.ToJson(profile), onSaved, onError);
    }

    public bool HasPendingProfile(string playerId)
    {
        return pending.documents.Exists(item => item.collection == "players" && item.documentId == playerId);
    }

    public bool QueueAttempt(AttemptReportData attempt, Action onSaved, Action<string> onError)
    {
        return Queue("attempts", attempt.attemptId, JsonUtility.ToJson(attempt), onSaved, onError);
    }

    private bool Queue(string collection, string documentId, string payload, Action onSaved, Action<string> onError)
    {
        if (!canPersist || string.IsNullOrEmpty(documentId))
        {
            onError?.Invoke("No se puede guardar el reporte en este dispositivo.");
            return false;
        }

        PendingDocument existing = pending.documents.Find(item => item.collection == collection && item.documentId == documentId);
        string previousPayload = existing != null ? existing.payload : null;
        if (existing != null)
            existing.payload = payload;
        else
            pending.documents.Add(new PendingDocument { collection = collection, documentId = documentId, payload = payload });

        try
        {
            SavePending();
        }
        catch (Exception error)
        {
            if (existing != null)
                existing.payload = previousPayload;
            else
                pending.documents.RemoveAt(pending.documents.Count - 1);

            onError?.Invoke("No se pudo guardar el reporte localmente: " + error.Message);
            return false;
        }

        onSaved?.Invoke();
        Flush();
        return true;
    }

    private void Flush()
    {
        if (!canPersist || sending || pending.documents.Count == 0 || Time.unscaledTime < nextRetryTime)
            return;

        FirebaseManager firebase = FirebaseManager.Instance;
        if (firebase == null || !firebase.IsReady)
        {
            nextRetryTime = Time.unscaledTime + RetrySeconds;
            return;
        }

        PendingDocument current = pending.documents[0];
        Dictionary<string, object> data;
        try
        {
            if (current.collection == "players")
                data = ReportUtility.ToDictionary(JsonUtility.FromJson<PlayerProfileData>(current.payload));
            else if (current.collection == "sessions")
                data = ReportUtility.ToDictionary(JsonUtility.FromJson<SessionReportData>(current.payload));
            else if (current.collection == "attempts")
                data = ReportUtility.ToDictionary(JsonUtility.FromJson<AttemptReportData>(current.payload));
            else
                throw new InvalidDataException("Coleccion desconocida: " + current.collection);
        }
        catch (Exception error)
        {
            Debug.LogError("No se puede leer un reporte pendiente: " + error.Message);
            nextRetryTime = Time.unscaledTime + RetrySeconds;
            return;
        }

        sending = true;
        nextRetryTime = Time.unscaledTime + RetrySeconds;
        string sentPayload = current.payload;
        try
        {
            firebase.Db.Collection(current.collection).Document(current.documentId).SetAsync(data).ContinueWithOnMainThread(task =>
            {
                sending = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("Reporte pendiente de sincronizar: " +
                        (task.Exception != null ? task.Exception.GetBaseException().Message : "Firebase no disponible."));
                    return;
                }

                // A session may have been closed while its initial write was in progress.
                if (pending.documents.Count > 0 && ReferenceEquals(pending.documents[0], current) && current.payload == sentPayload)
                {
                    pending.documents.RemoveAt(0);
                    try
                    {
                        SavePending();
                    }
                    catch (Exception error)
                    {
                        pending.documents.Insert(0, current);
                        Debug.LogWarning("No se pudo actualizar la cola de reportes: " + error.Message);
                        return;
                    }
                }

                nextRetryTime = 0f;
                Flush();
            });
        }
        catch (Exception error)
        {
            sending = false;
            Debug.LogWarning("Reporte pendiente de sincronizar: " + error.Message);
        }
    }

    private void LoadPending()
    {
        string backupPath = filePath + ".tmp";
        try
        {
            if (File.Exists(filePath))
                pending = ReadPending(filePath);
            else if (File.Exists(backupPath))
                pending = ReadPending(backupPath);
        }
        catch (Exception error)
        {
            try
            {
                pending = ReadPending(backupPath);
            }
            catch
            {
                canPersist = false;
                Debug.LogError("No se pudo recuperar la cola local de reportes: " + error.Message);
            }
        }
    }

    private static PendingDocuments ReadPending(string path)
    {
        PendingDocuments loaded = JsonUtility.FromJson<PendingDocuments>(File.ReadAllText(path));
        if (loaded == null || loaded.documents == null)
            throw new InvalidDataException("La cola de reportes no tiene el formato esperado.");
        return loaded;
    }

    private void SavePending()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        string backupPath = filePath + ".tmp";
        File.WriteAllText(backupPath, JsonUtility.ToJson(pending));
        File.Copy(backupPath, filePath, true);
        File.Delete(backupPath);
    }
}
