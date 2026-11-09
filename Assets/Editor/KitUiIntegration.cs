using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run from the Editor (not Play Mode). Changes only presentation: no Button events
// or gameplay components are replaced. Already-open dirty scenes are never saved.
public static class KitUiIntegration
{
    private const string Kit = "Assets/Sprites/Kit_UI_A_Tu_Ritmo_Unity/";

    [MenuItem("Tools/A Tu Ritmo/Aplicar kit UI a menús")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de aplicar el kit UI.");

        EnsureCloseSprites();
        StyleCardPrefab();
        StyleHudPrefab();
        StyleGamePausePrefab();
        foreach (string scenePath in new[]
        {
            "Assets/Scenes/MainScene.unity",
            "Assets/Scenes/SimonScene.unity",
            "Assets/Scenes/BasicrhythmScene.unity",
            "Assets/Scenes/TempoTapScene.unity",
            "Assets/Scenes/BandaMusical.unity"
        })
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (wasOpen && scene.isDirty)
            {
                Debug.LogWarning("Kit UI: escena con cambios pendientes, no se toca: " + scenePath);
                continue;
            }
            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                StyleScene(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Kit UI aplicado: " + scenePath);
            }
            finally
            {
                if (!wasOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/A Tu Ritmo/Aplicar kit UI a menús", true)]
    private static bool ValidateApply() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("Tools/A Tu Ritmo/Ajustar menús de pausa como referencia")]
    public static void ApplyPauseReference()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de editar los menús de pausa.");
        EnsureCloseSprites();
        StyleGamePausePrefab();
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
                Debug.LogWarning("Pausa: escena con cambios sin guardar; se omite " + path);
                continue;
            }
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (path.EndsWith("BandaMusical.unity", StringComparison.Ordinal))
                    EnsureBandPause(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                        if (node != null && node.name == "PauseBox" && !IsSharedPauseInstance(node)) StylePause(node);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Pausa ajustada: " + path);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void EnsureBandPause(Scene scene)
    {
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>())
            .FirstOrDefault(component => component != null);
        if (game == null || game.topHud == null)
            throw new InvalidOperationException("No se encontró el HUD de La banda musical.");
        if (game.pausePanel != null) return;
        Canvas canvas = game.topHud.GetComponentInParent<Canvas>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GamePausePanel.prefab");
        if (canvas == null || prefab == null)
            throw new InvalidOperationException("Falta Canvas o GamePausePanel en La banda musical.");
        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();
        panel.SetActive(false);
        game.pausePanel = panel;
        EditorUtility.SetDirty(game);
    }

    [MenuItem("Tools/A Tu Ritmo/Ajustar menús de pausa como referencia", true)]
    private static bool ValidatePause() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static void EnsureCloseSprites()
    {
        const string folder = "Assets/Sprites/Kit_UI_A_Tu_Ritmo_Unity/Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Kit.TrimEnd('/'), "Generated");
        CreateCloseSprite(folder + "/cerrar_rojo.png", false);
        CreateCloseSprite(folder + "/cerrar_rojo_presionado.png", true);
    }

    private static void CreateCloseSprite(string path, bool pressed)
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) return;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x - 63.5f) / 62f;
            float ny = (y - 63.5f) / 62f;
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            Color c = Color.clear;
            if (r < 0.96f)
            {
                if (r > 0.89f) c = new Color(0.19f, 0.06f, 0.17f);
                else if (r > 0.79f) c = new Color(0.63f, 0.03f, 0.11f);
                else
                {
                    float glow = Mathf.Clamp01((ny + 0.6f) * 0.42f);
                    c = Color.Lerp(new Color(0.8f, 0.025f, 0.1f),
                        new Color(1f, 0.37f, 0.38f), glow);
                    if (nx < -0.2f && ny > 0.42f && r < 0.78f)
                        c = Color.Lerp(c, Color.white, 0.48f);
                }
                if (pressed) c *= new Color(0.78f, 0.74f, 0.76f);
                c.a = Mathf.Clamp01((0.96f - r) * 100f);
            }
            texture.SetPixel(x, y, c);
        }
        texture.Apply();
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Sprite Sprite(string folder, string name)
    {
        string path = Kit + folder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Falta sprite del kit: " + path);
        return sprite;
    }

    private static void StyleCardPrefab()
    {
        const string path = "Assets/Prefabs/CardProfile.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SetImage(root.GetComponent<Image>(), Sprite("Paneles", "tarjeta_perfil_vacia"));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StyleHudPrefab()
    {
        const string path = "Assets/Prefabs/TopHudPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // The HUD prefab is authored by the user: don't reskin its images.
            // An older CartoonButtonSkin would replace the menu artwork on Awake.
            Button menu = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "MenuButton");
            if (menu != null && menu.GetComponent<CartoonButtonSkin>() != null)
                UnityEngine.Object.DestroyImmediate(menu.GetComponent<CartoonButtonSkin>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StyleGamePausePrefab()
    {
        const string path = "Assets/Prefabs/UI/GamePausePanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform box = Find(root.transform, "PauseBox");
            if (box == null) throw new InvalidOperationException("GamePausePanel no tiene PauseBox");
            StylePause(box);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static bool IsSharedPauseInstance(Transform node) =>
        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject) ==
        "Assets/Prefabs/UI/GamePausePanel.prefab";

    private static void StyleScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node == null) continue;
                if (node.name == "PerfilPanel")
                    StyleProfiles(node);
                else if (node.name == "PauseBox" && !IsSharedPauseInstance(node))
                    StylePause(node);
                else if (node.name == "StartPanel")
                    StyleStart(node);
            }
        }
    }

    private static void StyleProfiles(Transform panel)
    {
        SetImage(panel.GetComponent<Image>(), Sprite("Paneles", "panel_crema_vacio"));
        ProfileMenuController controller = panel.GetComponent<ProfileMenuController>();
        if (controller == null) throw new InvalidOperationException("PerfilPanel sin ProfileMenuController");
        SerializedObject serialized = new SerializedObject(controller);
        Assign(serialized, "buttonNormal", "Botones", "btn_morado_normal");
        Assign(serialized, "buttonHover", "Botones", "btn_morado_normal");
        Assign(serialized, "buttonPressed", "Botones", "btn_morado_presionado");
        Assign(serialized, "buttonDisabled", "Botones", "btn_gris_normal");
        Assign(serialized, "confirmNormal", "Botones", "btn_verde_normal");
        Assign(serialized, "confirmHover", "Botones", "btn_verde_normal");
        Assign(serialized, "confirmPressed", "Botones", "btn_verde_presionado");
        Assign(serialized, "confirmDisabled", "Botones", "btn_gris_normal");
        Assign(serialized, "cancelNormal", "Botones", "btn_rojo_normal");
        Assign(serialized, "cancelHover", "Botones", "btn_rojo_normal");
        Assign(serialized, "cancelPressed", "Botones", "btn_rojo_presionado");
        Assign(serialized, "cancelDisabled", "Botones", "btn_gris_normal");
        Assign(serialized, "refreshNormal", "Botones", "btn_amarillo_normal");
        Assign(serialized, "refreshPressed", "Botones", "btn_amarillo_presionado");
        Assign(serialized, "createPanelSprite", "Paneles", "panel_crema_vacio");
        serialized.ApplyModifiedPropertiesWithoutUndo();

        StyleButton(FindButton(panel, "ContinueButton"), "verde", "icono_play");
        StyleButton(FindButton(panel, "NewPerfilButton"), "morado", "icono_plus");
        StyleButton(FindButton(panel, "Update"), "amarillo", "icono_retry");
        StyleButton(FindButton(panel, "ExitProfileButton"), "rojo", "icono_cerrar");
        Transform popup = Find(panel, "CreateProfilePopup");
        if (popup == null) return;
        SetImage(popup.GetComponent<Image>(), Sprite("Paneles", "panel_crema_vacio"));
        StyleButton(FindButton(popup, "GuardarButton"), "verde", "icono_check");
        StyleButton(FindButton(popup, "CancelarButton"), "rojo", "icono_volver");
        foreach (Button avatar in popup.GetComponentsInChildren<Button>(true).Where(b => b.name == "AvatarButton"))
            StyleButton(avatar, "azul");
    }

    private static void Assign(SerializedObject owner, string field, string folder, string name)
    {
        SerializedProperty property = owner.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Falta campo " + field);
        property.objectReferenceValue = Sprite(folder, name);
    }

    private static void StylePause(Transform box)
    {
        SetImage(box.GetComponent<Image>(), Sprite("Paneles", "panel_pausa_vacio"));
        RectTransform panel = box as RectTransform;
        if (panel != null) panel.sizeDelta = new Vector2(440f, 500f);
        StylePauseTitle(Find(box, "PauseTitle"));
        StylePauseAction(FindButton(box, "ResumeButton") ?? FindButton(box, "ResumenButton"),
            "verde", "CONTINUAR", 54f, false);
        StylePauseAction(FindButton(box, "RestartButton"),
            "amarillo", "REINICIAR", -36f, false);
        StylePauseAction(FindButton(box, "MenuButton"),
            "rojo", "SALIR AL MENÚ", -126f, true);
        Button close = FindButton(box, "CloseButton");
        if (close != null)
        {
            RemoveKitIcon(close);
            RectTransform rect = close.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(182f, 190f);
            rect.sizeDelta = new Vector2(78f, 78f);
            CartoonButtonSkin skin = close.GetComponent<CartoonButtonSkin>();
            if (skin == null) skin = close.gameObject.AddComponent<CartoonButtonSkin>();
            skin.Configure(Sprite("Generated", "cerrar_rojo"), Sprite("Generated", "cerrar_rojo"),
                Sprite("Generated", "cerrar_rojo_presionado"), Sprite("Generated", "cerrar_rojo"));
            TMP_Text cross = close.GetComponentInChildren<TMP_Text>(true);
            if (cross != null)
            {
                cross.gameObject.SetActive(true);
                cross.text = "X";
                cross.fontStyle = FontStyles.Bold;
                cross.fontSize = 53f;
                cross.enableAutoSizing = false;
                cross.color = Color.white;
                cross.alignment = TextAlignmentOptions.Center;
                cross.raycastTarget = false;
                SetOutline(cross, new Color32(67, 18, 41, 255), 0.23f);
            }
            close.transform.SetAsLastSibling();
        }
    }

    private static void StylePauseTitle(Transform node)
    {
        if (node == null) return;
        RectTransform rect = node as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 151f);
        rect.sizeDelta = new Vector2(355f, 76f);
        TMP_Text title = node.GetComponent<TMP_Text>();
        if (title == null) return;
        title.text = "EN PAUSA";
        title.fontSize = 48f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        SetOutline(title, new Color32(18, 28, 68, 255), 0.2f);
    }

    private static void StylePauseAction(Button button, string color, string caption, float y, bool lightText)
    {
        if (button == null) return;
        RemoveKitIcon(button);
        StyleButton(button, color);
        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(320f, 78f);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        text.text = caption;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = lightText ? 36f : 39f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 27f;
        text.fontSizeMax = lightText ? 36f : 39f;
        text.color = lightText ? Color.white : new Color32(10, 39, 96, 255);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        SetOutline(text, lightText ? new Color32(82, 14, 38, 255) : Color.white, lightText ? 0.18f : 0.13f);
    }

    private static void RemoveKitIcon(Button button)
    {
        Transform icon = button.transform.Find("KitIcon");
        if (icon != null) UnityEngine.Object.DestroyImmediate(icon.gameObject);
    }

    private static void SetOutline(TMP_Text text, Color color, float width)
    {
        if (text.font == null || text.font.material == null) return;
        const string folder = "Assets/Materials/KitUI";
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Materials", "KitUI");
        Color32 c = color;
        string path = folder + "/" + text.font.name.Replace(' ', '_') + "_pause_" +
            c.r + "_" + c.g + "_" + c.b + "_" + Mathf.RoundToInt(width * 100f) + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(text.font.material);
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetColor("_OutlineColor", color);
            material.SetFloat("_OutlineWidth", width);
            material.EnableKeyword("OUTLINE_ON");
            AssetDatabase.CreateAsset(material, path);
        }
        text.fontSharedMaterial = material;
    }

    private static void StyleStart(Transform start)
    {
        StyleButton(FindButton(start, "StartButton"), "verde", "icono_play");
        StyleButton(FindButton(start, "BackButton"), "azul", "icono_volver");
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
    }

    private static void StyleButton(Button button, string color, string icon = null)
    {
        if (button == null) return;
        Sprite normal = Sprite("Botones", "btn_" + color + "_normal");
        Sprite pressed = Sprite("Botones", "btn_" + color + "_presionado");
        CartoonButtonSkin skin = button.GetComponent<CartoonButtonSkin>();
        if (skin == null) skin = button.gameObject.AddComponent<CartoonButtonSkin>();
        skin.Configure(normal, normal, pressed, Sprite("Botones", "btn_gris_normal"), normal);
        if (icon == null) return;
        Transform existing = button.transform.Find("KitIcon");
        Image image;
        if (existing == null)
        {
            GameObject child = new GameObject("KitIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(button.transform, false);
            image = child.GetComponent<Image>();
        }
        else image = existing.GetComponent<Image>();
        if (image == null) return;
        image.sprite = Sprite("Iconos", icon);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.08f, 0.5f);
        rect.sizeDelta = new Vector2(32f, 32f);
        rect.anchoredPosition = Vector2.zero;
        image.transform.SetAsLastSibling();
    }

    private static Button FindButton(Transform root, string name)
    {
        Transform found = Find(root, name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
