using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ChatterboxVoiceCatalogBuilder
{
    private const string AudioFolder = "Assets/Audio/";
    private const string CatalogPath = "Assets/Resources/ChatterboxVoiceCatalog.asset";
    private static readonly string[] Prefixes = { "banda_", "basico_", "menu_", "perfil_", "simon_", "tempo_", "voz_" };

    [MenuItem("Tools/Simondice/Actualizar voces Chatterbox")]
    public static void RefreshCatalog()
    {
        List<ChatterboxVoiceCatalog.Entry> entries = new List<ChatterboxVoiceCatalog.Entry>();
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder.TrimEnd('/') });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(AudioFolder, StringComparison.Ordinal) ||
                !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                continue;

            string name = Path.GetFileNameWithoutExtension(path);
            // The Banda music loops also start with "banda_"; only its voice folder is narration.
            if (name.StartsWith("banda_", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/Audio/audios_banda_voz/", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("Assets/Audio/audios_tutoriales/banda_tutorial.wav", StringComparison.OrdinalIgnoreCase))
                continue;
            bool isVoice = false;
            foreach (string prefix in Prefixes)
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { isVoice = true; break; }
            if (!isVoice)
                continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                entries.Add(new ChatterboxVoiceCatalog.Entry { id = name, clip = clip });
        }

        entries.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        ChatterboxVoiceCatalog catalog = AssetDatabase.LoadAssetAtPath<ChatterboxVoiceCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ChatterboxVoiceCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Voces Chatterbox disponibles: {entries.Count}");
    }

    [InitializeOnLoadMethod]
    private static void RefreshAfterDomainReload()
    {
        EditorApplication.delayCall += RefreshCatalog;
    }
}

public class ChatterboxVoicePostprocessor : AssetPostprocessor
{
    private static bool queued;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (queued)
            return;

        foreach (string path in imported)
        {
            if (!path.StartsWith("Assets/Audio/", StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                continue;

            queued = true;
            EditorApplication.delayCall += () => { queued = false; ChatterboxVoiceCatalogBuilder.RefreshCatalog(); };
            break;
        }
    }
}
