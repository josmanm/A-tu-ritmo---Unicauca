using System;
using UnityEngine;

// A fixed-tempo song needs both tempo and the time of its first real beat.
// A filename or an automatic estimate alone is not a perceptual calibration.
[Serializable]
public class BandaSongConfiguration
{
    public string id;
    public AudioClip clip;
    [Tooltip("Known tempo in BPM. Analyze for a suggestion, then confirm by ear. Overrides level BPM while this clip plays.")]
    [Range(40f, 160f)] public float bpm = 80f;
    [Tooltip("Seconds from the start of the decoded clip to its first intended beat; may include a musical introduction.")]
    [Min(0f)] public float firstBeatOffsetSeconds;
    [Tooltip("Mark only after listening to the song against the drum's beat cue.")]
    public bool calibratedByListening;
}
