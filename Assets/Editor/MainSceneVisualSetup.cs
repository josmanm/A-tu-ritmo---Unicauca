using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainSceneVisualSetup
{
    [MenuItem("Tools/Main Menu/Apply Panel Transitions")]
    public static void ApplyMainMenuPanelTransitions()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "MainScene")
        {
            EditorUtility.DisplayDialog("MainScene", "Abre la escena MainScene antes de ejecutar este setup.", "OK");
            return;
        }

        int buttonsUpdated = AddButtonPulseComponents();
        int panelsUpdated = EnsurePanelTransitions();
        int labelsStyled = StyleMenuButtonLabels();
        int layoutAdjusted = ApplyMainMenuLayout();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorUtility.DisplayDialog(
            "MainScene",
            $"Setup aplicado.\n\nBotones actualizados: {buttonsUpdated}\nPaneles con UIPanelTransition agregados: {panelsUpdated}\nLabels estilizados: {labelsStyled}\nBloques ajustados: {layoutAdjusted}",
            "OK");
    }

    private static int AddButtonPulseComponents()
    {
        int updated = 0;
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].GetComponent<UIButtonPulse>() != null)
                continue;

            Undo.AddComponent<UIButtonPulse>(buttons[i].gameObject);
            updated++;
        }

        return updated;
    }

    private static int EnsurePanelTransitions()
    {
        string[] panelNames = { "MainOptionsPanel", "GamesPanel", "SettingsPanel" };
        int updated = 0;

        for (int i = 0; i < panelNames.Length; i++)
        {
            GameObject panel = GameObject.Find(panelNames[i]);
            if (panel == null)
                continue;

            if (panel.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(panel);

            UIPanelTransition transition = panel.GetComponent<UIPanelTransition>();
            if (transition == null)
            {
                transition = Undo.AddComponent<UIPanelTransition>(panel);
                updated++;
            }

            ConfigurePanelTransition(panel.name, transition);
        }

        return updated;
    }

    private static void ConfigurePanelTransition(string panelName, UIPanelTransition transition)
    {
        if (transition == null)
            return;

        SerializedObject serializedTransition = new SerializedObject(transition);
        SetFloat(serializedTransition, "duration", 0.22f);
        SetBool(serializedTransition, "useScale", true);
        SetFloat(serializedTransition, "hiddenScale", 0.96f);
        SetBool(serializedTransition, "useSlide", true);

        switch (panelName)
        {
            case "MainOptionsPanel":
                SetVector2(serializedTransition, "hiddenOffset", new UnityEngine.Vector2(0f, -24f));
                break;
            case "GamesPanel":
                SetVector2(serializedTransition, "hiddenOffset", new UnityEngine.Vector2(120f, 0f));
                break;
            case "SettingsPanel":
                SetVector2(serializedTransition, "hiddenOffset", new UnityEngine.Vector2(0f, -80f));
                break;
            default:
                SetVector2(serializedTransition, "hiddenOffset", new UnityEngine.Vector2(0f, -32f));
                break;
        }

        serializedTransition.ApplyModifiedPropertiesWithoutUndo();
        transition.CaptureShownState();
    }

    private static int StyleMenuButtonLabels()
    {
        int styled = 0;
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;

            Undo.RecordObject(label, "Style menu label");
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 52f;
            label.characterSpacing = 4f;
            label.wordSpacing = 2f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = new Vector4(16f, 8f, 16f, 8f);
            label.fontWeight = FontWeight.Black;
            ApplyPrimaryLabelAccent(buttons[i], label);
            styled++;
        }

        return styled;
    }

    private static void ApplyPrimaryLabelAccent(Button button, TMP_Text label)
    {
        if (button == null || label == null)
            return;

        string plainText = StripRichText(label.text);
        if (string.IsNullOrWhiteSpace(plainText))
            return;

        label.enableVertexGradient = false;

        if (button.name == "Exit")
        {
            label.color = Color.white;
            label.text = plainText;
            return;
        }

        if (button.name == "Play" || button.name == "Settings")
        {
            label.color = Color.white;
            label.text = FormatTwoLetterColorGroups(plainText);
        }
    }

    private static string FormatTwoLetterColorGroups(string text)
    {
        Color[] palette =
        {
            new Color(1f, 0.45f, 0.72f),
            new Color(1f, 0.74f, 0.32f),
            new Color(0.46f, 0.86f, 0.99f),
            new Color(0.56f, 0.94f, 0.62f),
        };

        System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length * 20);
        int paletteIndex = 0;

        for (int i = 0; i < text.Length; i += 2)
        {
            int length = Mathf.Min(2, text.Length - i);
            string group = text.Substring(i, length);
            string colorHex = ColorUtility.ToHtmlStringRGB(palette[paletteIndex % palette.Length]);
            builder.Append("<color=#");
            builder.Append(colorHex);
            builder.Append('>');
            builder.Append(group);
            builder.Append("</color>");
            paletteIndex++;
        }

        return builder.ToString();
    }

    private static string StripRichText(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : Regex.Replace(text, "<.*?>", string.Empty);
    }

    private static int ApplyMainMenuLayout()
    {
        MenuManager menuManager = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        if (menuManager == null)
            return 0;

        SerializedObject serializedManager = new SerializedObject(menuManager);
        int adjusted = 0;

        adjusted += ConfigureLogo(serializedManager.FindProperty("logoRect"));
        adjusted += ConfigurePrimaryButton(serializedManager.FindProperty("playButtonRect"), new Vector2(0f, -24f), new Vector2(400f, 100f));
        adjusted += ConfigurePrimaryButton(serializedManager.FindProperty("settingsButtonRect"), new Vector2(0f, -160f), new Vector2(300f, 76f));
        adjusted += ConfigurePrimaryButton(serializedManager.FindProperty("exitButtonRect"), new Vector2(0f, -286f), new Vector2(360f, 82f));
        adjusted += ConfigureBackButton(serializedManager.FindProperty("backButton"));

        return adjusted;
    }

    private static int ConfigureLogo(SerializedProperty rectProperty)
    {
        RectTransform rect = rectProperty != null ? rectProperty.objectReferenceValue as RectTransform : null;
        if (rect == null)
            return 0;

        Undo.RecordObject(rect, "Adjust Logo layout");
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -150f);
        rect.sizeDelta = new Vector2(700f, 200f);
        return 1;
    }

    private static int ConfigurePrimaryButton(SerializedProperty rectProperty, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = rectProperty != null ? rectProperty.objectReferenceValue as RectTransform : null;
        if (rect == null)
            return 0;

        GameObject buttonObject = rect.gameObject;

        int adjusted = 0;
        Undo.RecordObject(rect, $"Adjust {buttonObject.name} layout");
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        adjusted++;

        Image image = buttonObject.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, $"Adjust {buttonObject.name} color");
            image.color = Color.white;
            adjusted++;
        }

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Undo.RecordObject(label, $"Adjust {buttonObject.name} label");
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 3f;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 52f;
            adjusted++;
        }

        return adjusted;
    }

    private static int ConfigureBackButton(SerializedProperty buttonProperty)
    {
        Button backButton = buttonProperty != null ? buttonProperty.objectReferenceValue as Button : null;
        if (backButton == null)
            return 0;

        int adjusted = 0;
        RectTransform rect = backButton.GetComponent<RectTransform>();
        if (rect != null)
        {
            Undo.RecordObject(rect, "Adjust BackButton layout");
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(110f, -84f);
            rect.sizeDelta = new Vector2(180f, 60f);
            adjusted++;
        }

        Image image = backButton.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, "Adjust BackButton color");
            image.color = new Color(0.92f, 0.92f, 0.96f, 0.18f);
            adjusted++;
        }

        TMP_Text label = backButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Undo.RecordObject(label, "Adjust BackButton label");
            label.color = Color.white;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 34f;
            adjusted++;
        }

        backButton.gameObject.SetActive(false);

        return adjusted;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetVector2(SerializedObject serializedObject, string propertyName, UnityEngine.Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector2Value = value;
    }
}
