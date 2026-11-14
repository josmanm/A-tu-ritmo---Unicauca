using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerProfileData
{
    public string playerId;
    public string name;
    public string avatar;
    public string createdAt;
}

[Serializable]
public class SessionReportData
{
    public string sessionId;
    public string playerId;
    public string playerName;
    public string avatar;
    public string startedAt;
    public string endedAt;
    public int totalTimeSeconds;
}

[Serializable]
public class AttemptReportData
{
    public string attemptId;
    public string sessionId;
    public string playerId;
    public string playerName;
    public string avatar;
    public string miniGame;
    public int level;
    public string difficulty;
    public int bpm;
    public int errors;
    public int correctAnswers;
    public int levelRepetitions;
    public bool completed;
    public int timeSeconds;
    public int scoreFinal;
    public bool wasTutorial;
    public string exitReason;
    public string playedAt;
    public TempoTapMetricsData tempoTap;
    public SimonMetricsData simon;
    public BasicRhythmMetricsData basicRhythm;
    public BandaMetricsData bandaMusical;
}

[Serializable]
public class BasicRhythmFigureMetric
{
    public string figure;
    public int correctNotes;
    public int failedNotes;
    public int tutorialCorrectNotes;
    public int tutorialFailedNotes;
    public float accuracyPercent;
    public int judgedTaps;
    public int earlyTaps;
    public int lateTaps;
    public float meanAbsoluteOffsetMs;
    public float meanSignedOffsetMs;
}

[Serializable]
public class BasicRhythmMetricsData
{
    public int chunkIndex;
    public List<BasicRhythmFigureMetric> figures = new List<BasicRhythmFigureMetric>();
    public List<BasicRhythmTapMetric> taps = new List<BasicRhythmTapMetric>();
}

[Serializable]
public class BasicRhythmTapMetric
{
    public string figure;
    public int noteIndex;
    public int tapIndex;
    public float timeSeconds;
    public float offsetMs;
    public string result;
    public bool tutorial;
}

[Serializable]
public class SimonTapMetric
{
    public int round;
    public int beat;
    public int expectedColor;
    public int pressedColor;
    public float offsetMs;
    public bool correctColor;
    public bool onBeat;
    public bool anchorsClock;
}

[Serializable]
public class SimonRoundMetric
{
    public int round;
    public int memorySpan;
    public int colorCount;
    public int bpm;
    public int expectedTaps;
    public int onBeatTaps;
    public int silences;
    public int measures;
    public bool firstTapStartsClock;
    public bool success;
}

[Serializable]
public class SimonMetricsData
{
    public string mode;
    public int guidedMistakes;
    public int practiceReplays;
    public int assessedRounds;
    public int successfulRounds;
    public int bestUnaidedSpan;
    public int assessmentTaps;
    public int onBeatTaps;
    public float meanAbsoluteOffsetMs;
    public List<SimonTapMetric> taps = new List<SimonTapMetric>();
    public List<SimonRoundMetric> rounds = new List<SimonRoundMetric>();
    public List<string> assessmentFailures = new List<string>();
}

[Serializable]
public class TempoTapMetricsData
{
    public int chunkIndex;
    public string songId;
    public float gameplaySeconds;
    public int judgedTaps;
    public int perfectTaps;
    public int goodTaps;
    public int earlyTaps;
    public int lateTaps;
    public int missedTaps;
    public int farNoPointsTaps;
    public int veryFarTaps;
    public int bestStreak;
    public int collisions;
    public float hitRatePercent;
    public float meanOffsetMs;
    public float meanAbsoluteOffsetMs;
    public int intervalCount;
    public float intervalStdDevMs;
    public float startingBpm;
    public float endingBpm;
    public List<TempoTapTapMetric> taps = new List<TempoTapTapMetric>();
}

[Serializable]
public class TempoTapTapMetric
{
    public int beatNumber;
    public float timeSeconds;
    public float offsetMs;
    public float intervalMs;
    public float bpm;
    public string result;
    public string action;
    public bool hasTap;
    public int points;
}

public static class ReportUtility
{
    public static string UtcNowString()
    {
        return DateTime.UtcNow.ToString("o");
    }

    public static string GenerateId(string prefix)
    {
        return prefix + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N");
    }

    public static Dictionary<string, object> ToDictionary(PlayerProfileData data)
    {
        return new Dictionary<string, object>
        {
            { "playerId", data.playerId },
            { "name", data.name },
            { "avatar", data.avatar },
            { "createdAt", data.createdAt },
        };
    }

    public static Dictionary<string, object> ToDictionary(SessionReportData data)
    {
        return new Dictionary<string, object>
        {
            { "sessionId", data.sessionId },
            { "playerId", data.playerId },
            { "playerName", data.playerName },
            { "avatar", data.avatar },
            { "startedAt", data.startedAt },
            { "endedAt", data.endedAt },
            { "totalTimeSeconds", data.totalTimeSeconds },
        };
    }

    public static Dictionary<string, object> ToDictionary(AttemptReportData data)
    {
        var result = new Dictionary<string, object>
        {
            { "attemptId", data.attemptId },
            { "sessionId", data.sessionId },
            { "playerId", data.playerId },
            { "playerName", data.playerName },
            { "avatar", data.avatar },
            { "miniGame", data.miniGame },
            { "level", data.level },
            { "difficulty", data.difficulty },
            { "bpm", data.bpm },
            { "errors", data.errors },
            { "correctAnswers", data.correctAnswers },
            { "levelRepetitions", data.levelRepetitions },
            { "completed", data.completed },
            { "timeSeconds", data.timeSeconds },
            { "scoreFinal", data.scoreFinal },
            { "wasTutorial", data.wasTutorial },
            { "exitReason", data.exitReason },
            { "playedAt", data.playedAt },
        };

        if (data.bandaMusical != null)
        {
            BandaMetricsData band = data.bandaMusical;
            var taps = new List<Dictionary<string, object>>();
            foreach (BandaTapData tap in band.taps ?? new List<BandaTapData>())
                taps.Add(new Dictionary<string, object>
                {
                    { "beat", tap.beat }, { "seconds", tap.seconds },
                    { "offsetMs", tap.offsetMs }, { "result", tap.result },
                    { "grade", tap.grade }, { "points", tap.points }
                });
            var levels = new List<Dictionary<string, object>>();
            foreach (BandaLevelScoreData level in band.levels ?? new List<BandaLevelScoreData>())
            {
                var levelData = new Dictionary<string, object>
                {
                    { "level", level.level }, { "songId", level.songId }, { "bpm", level.bpm },
                    { "perfect", level.perfect }, { "near", level.near },
                    { "valid", level.valid }, { "far", level.far },
                    { "veryFar", level.veryFar }, { "scoreEarned", level.scoreEarned }
                };
                // Old queued attempts contain scores but not these measurements.
                if (level.metricsVersion > 0)
                {
                    levelData["metricsVersion"] = level.metricsVersion;
                    levelData["completed"] = level.completed;
                    levelData["plannedBeats"] = level.plannedBeats;
                    levelData["expectedBeats"] = level.expectedBeats;
                    levelData["registeredTaps"] = level.registeredTaps;
                    levelData["onBeat"] = level.onBeat;
                    levelData["missed"] = level.missed;
                    levelData["extra"] = level.extra;
                    levelData["early"] = level.early;
                    levelData["late"] = level.late;
                    levelData["durationSeconds"] = level.durationSeconds;
                    if (level.hasMeanAbsoluteOffsetMs)
                        levelData["meanAbsoluteOffsetMs"] = level.meanAbsoluteOffsetMs;
                    if (level.hasHitRatePercent)
                        levelData["hitRatePercent"] = level.hitRatePercent;
                }
                levels.Add(levelData);
            }
            result["bandaMusical"] = new Dictionary<string, object>
            {
                { "songId", band.songId },
                { "levels", levels },
                { "perfect", band.perfect }, { "near", band.near },
                { "valid", band.valid }, { "far", band.far },
                { "veryFar", band.veryFar },
                { "assisted", band.assisted }, { "expectedBeats", band.expectedBeats },
                { "registeredTaps", band.registeredTaps }, { "onBeat", band.onBeat },
                { "early", band.early }, { "late", band.late }, { "extra", band.extra },
                { "missed", band.missed }, { "helpCount", band.helpCount },
                { "meanAbsoluteOffsetMs", band.meanAbsoluteOffsetMs }, { "taps", taps }
            };
        }

        if (data.tempoTap != null)
        {
            List<Dictionary<string, object>> taps = new List<Dictionary<string, object>>();
            foreach (TempoTapTapMetric tap in data.tempoTap.taps ?? new List<TempoTapTapMetric>())
                taps.Add(new Dictionary<string, object>
                {
                    { "beatNumber", tap.beatNumber },
                    { "timeSeconds", tap.timeSeconds },
                    { "offsetMs", tap.offsetMs },
                    { "intervalMs", tap.intervalMs },
                    { "bpm", tap.bpm },
                    { "result", tap.result },
                    { "action", tap.action },
                    { "hasTap", tap.hasTap },
                    { "points", tap.points },
                });

            result["tempoTap"] = new Dictionary<string, object>
            {
                { "songId", data.tempoTap.songId },
                { "gameplaySeconds", data.tempoTap.gameplaySeconds },
                { "judgedTaps", data.tempoTap.judgedTaps },
                { "perfectTaps", data.tempoTap.perfectTaps },
                { "goodTaps", data.tempoTap.goodTaps },
                { "earlyTaps", data.tempoTap.earlyTaps },
                { "lateTaps", data.tempoTap.lateTaps },
                { "missedTaps", data.tempoTap.missedTaps },
                { "farNoPointsTaps", data.tempoTap.farNoPointsTaps },
                { "veryFarTaps", data.tempoTap.veryFarTaps },
                { "bestStreak", data.tempoTap.bestStreak },
                { "collisions", data.tempoTap.collisions },
                { "hitRatePercent", data.tempoTap.hitRatePercent },
                { "meanOffsetMs", data.tempoTap.meanOffsetMs },
                { "meanAbsoluteOffsetMs", data.tempoTap.meanAbsoluteOffsetMs },
                { "intervalCount", data.tempoTap.intervalCount },
                { "intervalStdDevMs", data.tempoTap.intervalStdDevMs },
                { "startingBpm", data.tempoTap.startingBpm },
                { "endingBpm", data.tempoTap.endingBpm },
                { "chunkIndex", data.tempoTap.chunkIndex },
                { "taps", taps },
            };
        }

        if (data.simon != null)
        {
            List<Dictionary<string, object>> taps = new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> rounds = new List<Dictionary<string, object>>();
            foreach (SimonTapMetric tap in data.simon.taps)
            {
                taps.Add(new Dictionary<string, object>
                {
                    { "round", tap.round },
                    { "beat", tap.beat },
                    { "expectedColor", tap.expectedColor },
                    { "pressedColor", tap.pressedColor },
                    { "offsetMs", tap.offsetMs },
                    { "correctColor", tap.correctColor },
                    { "onBeat", tap.onBeat },
                    { "anchorsClock", tap.anchorsClock },
                });
            }

            foreach (SimonRoundMetric round in data.simon.rounds)
            {
                rounds.Add(new Dictionary<string, object>
                {
                    { "round", round.round },
                    { "memorySpan", round.memorySpan },
                    { "colorCount", round.colorCount },
                    { "bpm", round.bpm },
                    { "expectedTaps", round.expectedTaps },
                    { "onBeatTaps", round.onBeatTaps },
                    { "silences", round.silences },
                    { "measures", round.measures },
                    { "firstTapStartsClock", round.firstTapStartsClock },
                    { "success", round.success },
                });
            }

            result["simon"] = new Dictionary<string, object>
            {
                { "mode", data.simon.mode },
                { "guidedMistakes", data.simon.guidedMistakes },
                { "practiceReplays", data.simon.practiceReplays },
                { "assessedRounds", data.simon.assessedRounds },
                { "successfulRounds", data.simon.successfulRounds },
                { "bestUnaidedSpan", data.simon.bestUnaidedSpan },
                { "assessmentTaps", data.simon.assessmentTaps },
                { "onBeatTaps", data.simon.onBeatTaps },
                { "meanAbsoluteOffsetMs", data.simon.meanAbsoluteOffsetMs },
                { "taps", taps },
                { "rounds", rounds },
                { "assessmentFailures", data.simon.assessmentFailures },
            };
        }

        if (data.basicRhythm != null)
        {
            List<Dictionary<string, object>> figures = new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> taps = new List<Dictionary<string, object>>();
            foreach (BasicRhythmFigureMetric figure in data.basicRhythm.figures)
            {
                figures.Add(new Dictionary<string, object>
                {
                    { "figure", figure.figure },
                    { "correctNotes", figure.correctNotes },
                    { "failedNotes", figure.failedNotes },
                    { "tutorialCorrectNotes", figure.tutorialCorrectNotes },
                    { "tutorialFailedNotes", figure.tutorialFailedNotes },
                    { "accuracyPercent", figure.accuracyPercent },
                    { "judgedTaps", figure.judgedTaps },
                    { "earlyTaps", figure.earlyTaps },
                    { "lateTaps", figure.lateTaps },
                    { "meanAbsoluteOffsetMs", figure.meanAbsoluteOffsetMs },
                    { "meanSignedOffsetMs", figure.meanSignedOffsetMs },
                });
            }
            foreach (BasicRhythmTapMetric tap in data.basicRhythm.taps ?? new List<BasicRhythmTapMetric>())
                taps.Add(new Dictionary<string, object>
                {
                    { "figure", tap.figure },
                    { "noteIndex", tap.noteIndex },
                    { "tapIndex", tap.tapIndex },
                    { "timeSeconds", tap.timeSeconds },
                    { "offsetMs", tap.offsetMs },
                    { "result", tap.result },
                    { "tutorial", tap.tutorial },
                });
            result["basicRhythm"] = new Dictionary<string, object>
            {
                { "chunkIndex", data.basicRhythm.chunkIndex },
                { "figures", figures },
                { "taps", taps },
            };
        }

        return result;
    }
}
