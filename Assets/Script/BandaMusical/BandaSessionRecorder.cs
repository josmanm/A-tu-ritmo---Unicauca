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
    // 0 denotes a historical record; new indicators must not be inferred from old scores.
    public int metricsVersion;
    public bool completed;
    public int plannedBeats;
    public int expectedBeats; // Presented beats, including evaluated retries.
    public int registeredTaps;
    public int onBeat;
    public int missed;
    public int extra;
    public int early;
    public int late;
    public float meanAbsoluteOffsetMs; // Accepted unique taps only.
    public bool hasMeanAbsoluteOffsetMs;
    public float durationSeconds; // DSP time in evaluated sections, excluding lead-ins.
    public float hitRatePercent;
    public bool hasHitRatePercent;
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
    private int sectionBeats;
    private int sectionHits;
    private int highestHitIndex;
    private double sectionStartDsp;
    private double sectionInterval;
    private float levelOffsetSum;

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
        sectionBeats = 0;
        levelOffsetSum = 0f;
    }

    public void SetScore(int value) => score = Math.Max(0, value);

    public void BeginLevel(int level, string songId, int bpm, int plannedBeats)
    {
        currentLevel = new BandaLevelScoreData
        {
            level = level, songId = songId, bpm = bpm, plannedBeats = plannedBeats,
            metricsVersion = 1
        };
        levelOffsetSum = 0f;
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

    public void BeginEvaluatedSection(RhythmManager rhythm)
    {
        scoredBeats.Clear();
        sectionBeats = rhythm.BeatCount;
        sectionStartDsp = rhythm.FirstBeatDspTime;
        sectionInterval = rhythm.Interval;
        sectionHits = 0;
        highestHitIndex = -1;
    }

    public void EndEvaluatedSection(double dspTime, bool assisted, bool finished)
    {
        if (sectionBeats == 0 || currentLevel == null) return;
        // A partial section counts only beats already presented (or accepted in their
        // early tolerance window); the rest remain planned, not missed.
        int presented = finished ? sectionBeats : Math.Max(0, Math.Min(sectionBeats,
            (int)Math.Floor((dspTime - sectionStartDsp) / sectionInterval) + 1));
        presented = Math.Max(presented, highestHitIndex + 1);
        int missed = Math.Max(0, presented - sectionHits);
        currentLevel.expectedBeats += presented;
        currentLevel.missed += missed;
        currentLevel.durationSeconds += (float)Math.Max(0d,
            Math.Min(sectionBeats * sectionInterval, dspTime - sectionStartDsp));
        currentLevel.hasHitRatePercent = currentLevel.expectedBeats > 0;
        if (currentLevel.hasHitRatePercent)
            currentLevel.hitRatePercent = 100f * currentLevel.onBeat / currentLevel.expectedBeats;
        metrics.expectedBeats += presented;
        metrics.missed += missed;
        metrics.assisted |= assisted;
        sectionBeats = 0;
        scoredBeats.Clear();
    }

    public void CompleteLevel()
    {
        if (currentLevel != null) currentLevel.completed = true;
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
        currentLevel.registeredTaps++;
        if (unique)
        {
            sectionHits++;
            highestHitIndex = Math.Max(highestHitIndex, index);
            metrics.onBeat++;
            currentLevel.onBeat++;
            absoluteOffsetSum += Mathf.Abs(offset);
            metrics.meanAbsoluteOffsetMs = absoluteOffsetSum / metrics.onBeat;
            levelOffsetSum += Mathf.Abs(offset);
            currentLevel.meanAbsoluteOffsetMs = levelOffsetSum / currentLevel.onBeat;
            currentLevel.hasMeanAbsoluteOffsetMs = true;
        }
        else if (result == "early") { metrics.early++; currentLevel.early++; }
        else if (result == "late") { metrics.late++; currentLevel.late++; }
        else { metrics.extra++; currentLevel.extra++; }
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
            if (!File.Exists(file))
                File.WriteAllText(file, JsonUtility.ToJson(new BandaLocalReports
                {
                    attempts = new List<BandaLocalReport> { local }
                }, true));
            else
            {
                string json = File.ReadAllText(file);
                BandaLocalReports history = JsonUtility.FromJson<BandaLocalReports>(json);
                int arrayEnd = json.LastIndexOf(']');
                if (history == null || history.attempts == null || arrayEnd < 0 ||
                    json.IndexOf("\"attempts\"", StringComparison.Ordinal) < 0)
                    throw new InvalidDataException("El historial local no tiene el formato esperado.");
                // Keep historical attempt JSON verbatim: JsonUtility would otherwise
                // rewrite absent legacy level fields as misleading zero values.
                string entry = JsonUtility.ToJson(local);
                File.WriteAllText(file, json.Insert(arrayEnd,
                    (history.attempts.Count > 0 ? "," : "") + entry));
            }
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
