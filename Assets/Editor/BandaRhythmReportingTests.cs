#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BandaRhythmReportingTests
{
    private GameObject host;
    private RhythmManager rhythm;
    private BandaSessionRecorder recorder;
    private double start;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Banda reporting tests");
        rhythm = host.AddComponent<RhythmManager>();
        recorder = host.AddComponent<BandaSessionRecorder>();
        rhythm.bpm = 60f;
        rhythm.toleranceSeconds = 0.25f;
        recorder.Begin(false);
        recorder.BeginLevel(1, "test", 60, 4);
        rhythm.StartSequence(4, false);
        start = rhythm.FirstBeatDspTime;
        recorder.BeginEvaluatedSection(rhythm);
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

    private bool Tap(double seconds, out string result)
    {
        return recorder.RecordTap(start + seconds, rhythm, 1, out _, out result);
    }

    private BandaLevelScoreData Finish(double seconds = 3.25, bool completed = true)
    {
        recorder.EndEvaluatedSection(start + seconds, false, completed);
        if (completed) recorder.CompleteLevel();
        return recorder.Metrics.levels[0];
    }

    [Test]
    public void Silence_MissesAllAndOffsetIsUnavailable()
    {
        var level = Finish();
        Assert.AreEqual(4, level.expectedBeats);
        Assert.AreEqual(4, level.missed);
        Assert.AreEqual(0, level.hitRatePercent);
        Assert.IsFalse(level.hasMeanAbsoluteOffsetMs);
    }

    [Test]
    public void Perfect_OneHitPerBeat()
    {
        for (int i = 0; i < 4; i++) Assert.IsTrue(Tap(i, out _));
        var level = Finish();
        Assert.AreEqual(4, level.onBeat);
        Assert.AreEqual(100f, level.hitRatePercent);
        Assert.AreEqual(0f, level.meanAbsoluteOffsetMs, 0.001f);
        Assert.AreEqual(0, level.missed);
    }

    [Test]
    public void Duplicates_DoNotIncreaseHitsOrHitRate()
    {
        Assert.IsTrue(Tap(0, out _));
        Assert.IsFalse(Tap(0.05, out string result));
        Assert.AreEqual("extra", result);
        var level = Finish();
        Assert.AreEqual(2, level.registeredTaps);
        Assert.AreEqual(1, level.onBeat);
        Assert.AreEqual(1, level.extra);
        Assert.AreEqual(25f, level.hitRatePercent);
        Assert.AreEqual(0f, level.meanAbsoluteOffsetMs, 0.001f);
    }

    [Test]
    public void OutOfWindowTaps_AreEarlyOrLate_NotHits()
    {
        Tap(-0.3, out string early);
        Tap(0.3, out string late);
        Assert.AreEqual("early", early);
        Assert.AreEqual("late", late);
        var level = Finish();
        Assert.AreEqual(1, level.early);
        Assert.AreEqual(1, level.late);
        Assert.AreEqual(4, level.missed);
        Assert.AreEqual(2, level.registeredTaps);
    }

    [Test]
    public void InterruptedLevel_OnlyPresentedBeatsAreMissed()
    {
        Tap(0, out _);
        var level = Finish(1.1, false);
        Assert.IsFalse(level.completed);
        Assert.AreEqual(4, level.plannedBeats);
        Assert.AreEqual(2, level.expectedBeats);
        Assert.AreEqual(1, level.missed);
        Assert.AreEqual(50f, level.hitRatePercent);
        Assert.AreEqual(1.1f, level.durationSeconds, 0.001f);
    }

    [Test]
    public void BeforeFirstBeat_RateIsUnavailable_AndLegacyLevelsDoNotGainMetrics()
    {
        var level = Finish(-0.1, false);
        Assert.IsFalse(level.hasHitRatePercent);
        var old = new BandaMetricsData();
        old.levels.Add(new BandaLevelScoreData { level = 1, perfect = 2 });
        var data = ReportUtility.ToDictionary(new AttemptReportData { bandaMusical = old });
        var band = (Dictionary<string, object>)data["bandaMusical"];
        var levels = (List<Dictionary<string, object>>)band["levels"];
        Assert.IsFalse(levels[0].ContainsKey("expectedBeats"));
    }

    [Test]
    public void NewLevel_SerializesMetricsForFirebase_AndUnavailableValuesAreOmitted()
    {
        var level = Finish();
        var data = ReportUtility.ToDictionary(new AttemptReportData { bandaMusical = recorder.Metrics });
        var band = (Dictionary<string, object>)data["bandaMusical"];
        var levels = (List<Dictionary<string, object>>)band["levels"];
        Assert.AreEqual(level.expectedBeats, levels[0]["expectedBeats"]);
        Assert.AreEqual(0f, levels[0]["hitRatePercent"]);
        Assert.IsFalse(levels[0].ContainsKey("meanAbsoluteOffsetMs"));
    }
}
#endif
