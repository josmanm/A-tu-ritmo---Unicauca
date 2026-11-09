using System;
using System.Collections.Generic;
using UnityEngine;

public class ChatterboxVoiceCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string id;
        public AudioClip clip;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();
    private Dictionary<string, AudioClip> clips;

    public int Count => entries.Length;

    public AudioClip Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (clips == null)
        {
            clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry entry in entries)
                if (entry != null && !string.IsNullOrEmpty(entry.id) && entry.clip != null)
                    clips[entry.id] = entry.clip;
        }

        return clips.TryGetValue(id, out AudioClip clip) ? clip : null;
    }

    public void SetEntries(Entry[] newEntries)
    {
        entries = newEntries ?? Array.Empty<Entry>();
        clips = null;
    }
}
