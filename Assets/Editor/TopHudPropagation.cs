using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TopHudPropagation
{
    private const string PrefabPath = "Assets/Prefabs/TopHudPanel.prefab";

    [MenuItem("Tools/A Tu Ritmo/Actualizar HUD superior en los juegos")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de actualizar el HUD.");

        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Button button = prefab.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name == "MenuButton");
            if (button == null || button.GetComponent<Image>()?.sprite == null)
                throw new InvalidOperationException("El botón de menú del prefab no tiene imagen.");
            // This component belongs to the earlier kit integration; it paints
            // the previous blue button over the user's new sprite in Play Mode.
            CartoonButtonSkin oldSkin = button.GetComponent<CartoonButtonSkin>();
            if (oldSkin != null) UnityEngine.Object.DestroyImmediate(oldSkin);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        foreach (string path in new[]
        {
            "Assets/Scenes/SimonScene.unity",
            "Assets/Scenes/BasicrhythmScene.unity",
            "Assets/Scenes/TempoTapScene.unity",
            "Assets/Scenes/BandaMusical.unity"
        })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (wasOpen && scene.isDirty)
            {
                Debug.LogWarning("HUD: escena con cambios pendientes; se omite " + path);
                continue;
            }
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (TopHudPanelView hud in root.GetComponentsInChildren<TopHudPanelView>(true))
                {
                    if (AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(hud.gameObject)) != PrefabPath)
                        continue;
                    Image score = FindImage(hud.transform, "ScorePanel");
                    Image level = FindImage(hud.transform, "LevelImage");
                    Image menu = hud.MenuButton != null ? hud.MenuButton.GetComponent<Image>() : null;
                    // Revert only obsolete image overrides, never score/level
                    // strings, life count, scene-specific position or onClick.
                    RevertIfHidden(hud.gameObject, score);
                    RevertIfBlank(hud.gameObject, level);
                    RevertIfBlank(hud.gameObject, menu);
                    if (hud.MenuButton != null && hud.MenuButton.GetComponent<CartoonButtonSkin>() != null &&
                        PrefabUtility.IsAddedComponentOverride(hud.MenuButton.GetComponent<CartoonButtonSkin>()))
                        UnityEngine.Object.DestroyImmediate(hud.MenuButton.GetComponent<CartoonButtonSkin>());
                    hud.GetComponent<TopHudPanelStyle>()?.Apply();
                    Debug.Log("HUD actualizado: " + path);
                }
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
    }

    [MenuItem("Tools/A Tu Ritmo/Actualizar HUD superior en los juegos", true)]
    private static bool Validate() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static Image FindImage(Transform root, string name) => root
        .GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == name);

    private static void RevertIfHidden(GameObject instance, Image image)
    {
        if (image == null) return;
        Image original = PrefabUtility.GetCorrespondingObjectFromSource(image);
        if (original == null || original.sprite == null || original.color.a <= 0f || image.color.a > 0f)
            return;
        RemoveOverride(instance, original, "m_Color.a");
        if (image.color.a <= 0f)
        {
            image.color = original.color;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        }
    }

    private static void RevertIfBlank(GameObject instance, Image image)
    {
        if (image == null || image.sprite != null) return;
        Image original = PrefabUtility.GetCorrespondingObjectFromSource(image);
        if (original == null || original.sprite == null) return;
        RemoveOverride(instance, original, "m_Sprite");
        if (image.sprite == null)
        {
            image.sprite = original.sprite;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        }
    }

    private static void RemoveOverride(GameObject instance, UnityEngine.Object source, string property)
    {
        var modifications = PrefabUtility.GetPropertyModifications(instance);
        if (modifications == null) return;
        var keep = modifications.Where(m => m == null || m.target != source || m.propertyPath != property).ToArray();
        if (keep.Length == modifications.Length) return;
        PrefabUtility.SetPropertyModifications(instance, keep);
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}
