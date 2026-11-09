using System;
using UnityEngine;

[Serializable]
public class MusicKit
{
    public string id;
    [Tooltip("WAV con pulso para enseñar el inicio de la canción.")]
    public AudioClip withPulse;
    [Tooltip("WAV sin clic para la parte jugable, sincronizado con el anterior.")]
    public AudioClip withoutClick;
    [Tooltip("BeatMap del kit: valida BPM, duración y tiempos de los pulsos.")]
    public TextAsset beatMap;
    [Min(1)] public int originalBpm = 80;
    [Min(1)] public int beatCount = 64;

    [Serializable]
    private class BeatMapData
    {
        public int bpm, tempo_bpm, beat_count, total_beats;
        public float duration_seconds, durationSeconds, first_beat_offset_seconds, firstBeatOffsetSeconds;
        public float[] beatTimesSeconds;
        public BeatData[] beats;
    }

    [Serializable]
    private class BeatData { public int index; public float time_seconds; }

    public bool IsValid()
    {
        if (withPulse == null || withoutClick == null || originalBpm <= 0 || beatCount <= 0)
            return false;
        double duration = beatCount * 60d / originalBpm;
        if (Math.Abs(withPulse.length - duration) > 0.02d ||
            Math.Abs(withoutClick.length - duration) > 0.02d)
            return false;
        if (beatMap == null) return false;

        BeatMapData map;
        try { map = JsonUtility.FromJson<BeatMapData>(beatMap.text); }
        catch (Exception) { return false; }
        if (map == null) return false;
        int bpm = map.bpm > 0 ? map.bpm : map.tempo_bpm;
        int beats = map.beat_count > 0 ? map.beat_count :
            map.total_beats > 0 ? map.total_beats :
            map.beatTimesSeconds != null ? map.beatTimesSeconds.Length : 0;
        float length = map.duration_seconds > 0 ? map.duration_seconds : map.durationSeconds;
        if (bpm != originalBpm || beats != beatCount || Math.Abs(length - duration) > 0.02d ||
            Math.Abs(map.first_beat_offset_seconds) > 0.001f ||
            Math.Abs(map.firstBeatOffsetSeconds) > 0.001f) return false;
        float interval = 60f / originalBpm;
        if (map.beatTimesSeconds != null && map.beatTimesSeconds.Length > 0)
            for (int i = 0; i < beatCount; i++)
                if (Math.Abs(map.beatTimesSeconds[i] - i * interval) > 0.01f) return false;
        if (map.beats != null && map.beats.Length > 0)
        {
            if (map.beats.Length != beatCount) return false;
            for (int i = 0; i < beatCount; i++)
                if (map.beats[i].index != i || Math.Abs(map.beats[i].time_seconds - i * interval) > 0.01f)
                    return false;
        }
        return true;
    }
}
