using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// A guide for authoring BandaSongConfiguration, not an automatic beat map.
// Strong offbeats and half/double-time patterns need listening verification.
public static class BandaSongPulseAnalyzer
{
    public struct Suggestion
    {
        public float bpm;
        public float phaseSeconds;
        public float confidence;
    }

    [MenuItem("Tools/A Tu Ritmo/Analizar pulso de audio seleccionado")]
    private static void AnalyzeSelectedAudio()
    {
        AudioClip clip = Selection.activeObject as AudioClip;
        if (clip == null)
        {
            EditorUtility.DisplayDialog("La banda musical", "Selecciona un AudioClip en el proyecto.", "Aceptar");
            return;
        }
        try
        {
            Suggestion[] suggestions = Analyze(clip);
            string[] lines = suggestions.Select(s =>
                $"{s.bpm:0.#} BPM; fase sugerida {s.phaseSeconds:0.000} s; correlación {s.confidence:0.00}").ToArray();
            string message = clip.name + "\n" + string.Join("\n", lines) +
                "\n\nSon hipótesis, NO pulsos confirmados. La fase puede corresponder a un contratiempo. " +
                "Ajusta BPM y primer pulso en las canciones de BandaMusicalGameManager y compruébalos escuchando y tocando en Play Mode.";
            Debug.Log("Banda / análisis provisional:\n" + message);
            EditorUtility.DisplayDialog("Pulso sugerido: " + clip.name, message, "Aceptar");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Banda: no se pudo analizar " + clip.name + ": " + ex.Message);
        }
    }

    public static Suggestion[] Analyze(AudioClip clip)
    {
        if (clip == null || clip.frequency <= 0 || clip.channels <= 0)
            throw new ArgumentException("El audio no tiene muestras disponibles.");
        if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
            throw new InvalidOperationException("No se pudo cargar el audio para analizarlo.");

        const float hopSeconds = 0.01f;
        int hopFrames = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * hopSeconds));
        int totalFrames = Mathf.Min(clip.samples, clip.frequency * 30);
        int windows = totalFrames / hopFrames;
        if (windows < 150) throw new ArgumentException("Se necesitan al menos 1,5 segundos de audio.");

        float[] rms = new float[windows];
        const int windowsPerChunk = 100;
        for (int startWindow = 0; startWindow < windows; startWindow += windowsPerChunk)
        {
            int count = Math.Min(windowsPerChunk, windows - startWindow);
            float[] pcm = new float[count * hopFrames * clip.channels];
            if (!clip.GetData(pcm, startWindow * hopFrames))
                throw new InvalidOperationException("Unity no pudo leer las muestras PCM de esta pista.");
            int samplesPerWindow = hopFrames * clip.channels;
            for (int w = 0; w < count; w++)
            {
                double energy = 0;
                int offset = w * samplesPerWindow;
                for (int j = 0; j < samplesPerWindow; j++)
                    energy += pcm[offset + j] * pcm[offset + j];
                rms[startWindow + w] = (float)Math.Sqrt(energy / samplesPerWindow);
            }
        }

        float[] onset = new float[windows];
        for (int i = 3; i < windows; i++)
            onset[i] = Mathf.Max(0f, rms[i] - (rms[i - 1] + rms[i - 2] + rms[i - 3]) / 3f);

        var candidates = new List<Suggestion>();
        for (float bpm = 60f; bpm <= 160f; bpm += 0.5f)
        {
            int lag = Mathf.RoundToInt((60f / bpm) / hopSeconds);
            double match = 0d, energyA = 0d, energyB = 0d;
            for (int i = 3; i < windows - lag; i++)
            {
                match += onset[i] * onset[i + lag];
                energyA += onset[i] * onset[i];
                energyB += onset[i + lag] * onset[i + lag];
            }
            float similarity = energyA > 0 && energyB > 0
                ? (float)(match / Math.Sqrt(energyA * energyB)) : 0f;
            candidates.Add(new Suggestion { bpm = bpm, confidence = similarity });
        }

        var selected = new List<Suggestion>();
        foreach (Suggestion candidate in candidates.OrderByDescending(s => s.confidence))
        {
            if (selected.Any(s => Mathf.Abs(s.bpm - candidate.bpm) < 4f)) continue;
            Suggestion suggestion = candidate;
            float interval = 60f / candidate.bpm;
            float bestScore = -1f;
            for (float phase = 0f; phase < interval; phase += hopSeconds)
            {
                float score = 0f;
                for (float t = phase; t < Mathf.Min(clip.length, 24f); t += interval)
                {
                    int i = Mathf.RoundToInt(t / hopSeconds);
                    if (i < 1 || i >= windows - 1) break;
                    score += Mathf.Max(onset[i - 1], onset[i], onset[i + 1]);
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    suggestion.phaseSeconds = phase;
                }
            }
            selected.Add(suggestion);
            if (selected.Count == 3) break;
        }
        return selected.ToArray();
    }
}
