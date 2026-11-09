using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class BandaTapData
{
    public int beat;
    public float seconds;
    public float offsetMs;
    public string result;
    public string grade;
    public int points;
}

[Serializable]
public class BandaLevelScoreData
{
    public int level;
    public string songId;
    public int bpm;
    public int perfect;
    public int near;
    public int valid;
    public int far;
    public int veryFar;
    public int scoreEarned;
}

[Serializable]
public class BandaMetricsData
{
    public string songId;
    public List<BandaLevelScoreData> levels = new List<BandaLevelScoreData>();
    public int perfect;
    public int near;
    public int valid;
    public int far;
    public int veryFar;
    public bool assisted;
    public int expectedBeats;
    public int registeredTaps;
    public int onBeat;
    public int early;
    public int late;
    public int extra;
    public int missed;
    public int helpCount;
    public float meanAbsoluteOffsetMs;
    public List<BandaTapData> taps = new List<BandaTapData>();
}

[Serializable]
public class BandaLocalReport
{
    public string attemptId;
    public string playerId;
    public string sessionId;
    public string miniGame;
    public int level;
    public int bpm;
    public string playedAt;
    public bool completed;
    public int scoreFinal;
    public BandaMetricsData metrics;
}

[Serializable]
public class BandaLocalReports
{
    public List<BandaLocalReport> attempts = new List<BandaLocalReport>();
}

public class BandaSessionRecorder : MonoBehaviour
{
    private BandaMetricsData metrics = new BandaMetricsData();
    private readonly HashSet<int> scoredBeats = new HashSet<int>();
    private float absoluteOffsetSum;
    private bool saved;
    private string attemptId;
    private int score;
    private BandaLevelScoreData currentLevel;

    public BandaMetricsData Metrics => metrics;

    public void Begin(bool assisted)
    {
        metrics = new BandaMetricsData { assisted = assisted };
        scoredBeats.Clear();
        absoluteOffsetSum = 0f;
        saved = false;
        attemptId = ReportUtility.GenerateId("banda");
        score = 0;
        currentLevel = null;
    }

    public void SetScore(int value) => score = Math.Max(0, value);

    public void BeginLevel(int level, string songId, int bpm)
    {
        currentLevel = new BandaLevelScoreData { level = level, songId = songId, bpm = bpm };
        metrics.levels.Add(currentLevel);
        metrics.songId = songId; // Compatibilidad con los reportes anteriores.
    }

    public void RecordScoreForLastTap(int points, string grade)
    {
        if (metrics.taps.Count > 0)
        {
            BandaTapData tap = metrics.taps[metrics.taps.Count - 1];
            tap.points = points;
            tap.grade = grade;
        }
        if (currentLevel == null || grade == "extra") return;
        currentLevel.scoreEarned += points;
        switch (grade)
        {
            case "perfect": metrics.perfect++; currentLevel.perfect++; break;
            case "near": metrics.near++; currentLevel.near++; break;
            case "valid": metrics.valid++; currentLevel.valid++; break;
            case "far": metrics.far++; currentLevel.far++; break;
            case "very_far": metrics.veryFar++; currentLevel.veryFar++; break;
        }
    }

    public void AddSection(int beats, int hits, bool assisted)
    {
        metrics.expectedBeats += beats;
        metrics.missed += Math.Max(0, beats - hits);
        metrics.assisted |= assisted;
        scoredBeats.Clear();
    }

    public bool RecordTap(double dspTime, RhythmManager rhythm, int sectionIndex,
        out float offsetMs, out string result)
    {
        int index = rhythm.NearestBeat(dspTime, out float offset);
        offsetMs = offset;
        float tolerance = rhythm.toleranceSeconds * 1000f;
        bool inWindow = rhythm.Running && Mathf.Abs(offset) <= tolerance;
        bool unique = inWindow && scoredBeats.Add(index);
        result = !inWindow ? (offset < 0f ? "early" : "late") : unique ? "on_beat" : "extra";
        metrics.registeredTaps++;
        if (unique)
        {
            metrics.onBeat++;
            absoluteOffsetSum += Mathf.Abs(offset);
            metrics.meanAbsoluteOffsetMs = absoluteOffsetSum / metrics.onBeat;
        }
        else if (result == "early") metrics.early++;
        else if (result == "late") metrics.late++;
        else metrics.extra++;
        metrics.taps.Add(new BandaTapData
        {
            beat = sectionIndex * 100 + index,
            seconds = (float)(dspTime - rhythm.FirstBeatDspTime),
            offsetMs = offset,
            result = result
        });
        return unique;
    }

    public void Save(int level, int bpm, bool completed)
    {
        if (saved) return;
        saved = true;
        PlayerProfileData player = PlayerProfileManager.Instance != null ? PlayerProfileManager.Instance.ActiveProfile : null;
        SessionReportData session = SessionManager.Instance != null ? SessionManager.Instance.CurrentSession : null;
        BandaLocalReport local = new BandaLocalReport
        {
            attemptId = attemptId,
            playerId = player != null ? player.playerId : string.Empty,
            sessionId = session != null ? session.sessionId : string.Empty,
            miniGame = "banda_musical",
            level = level,
            bpm = bpm,
            playedAt = ReportUtility.UtcNowString(),
            completed = completed,
            scoreFinal = score,
            metrics = metrics
        };

        // Always retain a local copy, including offline sessions and sessions without a profile.
        string file = Path.Combine(Application.persistentDataPath, "banda-musical-sessions.json");
        try
        {
            BandaLocalReports history = File.Exists(file)
                ? JsonUtility.FromJson<BandaLocalReports>(File.ReadAllText(file)) : null;
            if (history == null) history = new BandaLocalReports();
            if (history.attempts == null) history.attempts = new List<BandaLocalReport>();
            history.attempts.Add(local);
            File.WriteAllText(file, JsonUtility.ToJson(history, true));
        }
        catch (Exception ex) { Debug.LogWarning("Banda: no se pudo guardar el reporte local: " + ex.Message); }

        if (GameReportManager.Instance != null && player != null && session != null)
        {
            GameReportManager.Instance.ReportAttempt(new AttemptReportData
            {
                attemptId = attemptId,
                miniGame = local.miniGame,
                level = level,
                difficulty = metrics.assisted ? "asistido" : "autonomo",
                bpm = bpm,
                errors = metrics.missed + metrics.extra,
                correctAnswers = metrics.onBeat,
                completed = completed,
                scoreFinal = score,
                wasTutorial = metrics.expectedBeats == 0,
                exitReason = completed ? "completed" : "return_to_menu",
                bandaMusical = metrics
            }, null, error => Debug.LogWarning("Banda: reporte pendiente: " + error));
        }
    }
}
