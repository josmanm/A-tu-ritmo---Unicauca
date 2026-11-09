using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BasicRhythmSceneVisualSetup
{
    [MenuItem("Tools/Basic Rhythm/Apply Visual Polish Setup")]
    public static void ApplyVisualPolishSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "BasicrhythmScene")
        {
            EditorUtility.DisplayDialog("BasicrhythmScene", "Abre la escena BasicrhythmScene antes de ejecutar este setup.", "OK");
            return;
        }

        int buttonsUpdated = AddButtonPulseComponents();
        bool pauseCanvasGroupUpdated = EnsurePausePanelCanvasGroup();
        bool pauseTransitionUpdated = EnsurePauseBoxTransition();
        bool pauseViewUpdated = EnsurePausePanelView();
        int layoutAdjusted = ApplyLayout();
        int textsStyled = StyleTexts();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorUtility.DisplayDialog(
            "BasicrhythmScene",
            $"Setup aplicado.\n\nBotones actualizados: {buttonsUpdated}\nCanvasGroup en PausePanel: {(pauseCanvasGroupUpdated ? "agregado" : "ya existia o no se encontro")}\nUIPanelTransition en PauseBox: {(pauseTransitionUpdated ? "agregado" : "ya existia o no se encontro")}\nPausePanelView en PausePanel: {(pauseViewUpdated ? "agregado" : "ya existia o no se encontro")}\nBloques ajustados: {layoutAdjusted}\nTextos estilizados: {textsStyled}",
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

    private static bool EnsurePausePanelCanvasGroup()
    {
        GameObject pausePanel = GameObject.Find("PausePanel");
        if (pausePanel == null)
            return false;

        if (pausePanel.GetComponent<CanvasGroup>() != null)
            return false;

        Undo.AddComponent<CanvasGroup>(pausePanel);
        return true;
    }

    private static bool EnsurePauseBoxTransition()
    {
        GameObject pauseBox = GameObject.Find("PauseBox");
        if (pauseBox == null)
            return false;

        if (pauseBox.GetComponent<UIPanelTransition>() != null)
            return false;

        Undo.AddComponent<UIPanelTransition>(pauseBox);
        return true;
    }

    private static bool EnsurePausePanelView()
    {
        GameObject pausePanel = GameObject.Find("PausePanel");
        if (pausePanel == null)
            return false;

        if (pausePanel.GetComponent<PausePanelView>() != null)
            return false;

        Undo.AddComponent<PausePanelView>(pausePanel);
        return true;
    }

    private static int ApplyLayout()
    {
        int adjusted = 0;
        adjusted += ConfigureRect("TopPanel", new Vector2(0f, -54f), new Vector2(0f, 122f));
        adjusted += ConfigureRect("ScorePanel", new Vector2(124f, -58f), new Vector2(230f, 92f));
        adjusted += ConfigureRect("LevelPanel", new Vector2(-124f, -58f), new Vector2(230f, 92f));
        adjusted += ConfigureRect("LivesPanel", new Vector2(0f, 0f), new Vector2(260f, 110f));
        adjusted += ConfigureRect("PauseBox", Vector2.zero, new Vector2(620f, 420f));
        adjusted += ConfigureRect("DrumButton", new Vector2(0f, -230f), new Vector2(260f, 180f));
        adjusted += ConfigureFeedbackPanel();
        adjusted += ConfigurePausePanelOverlay();
        return adjusted;
    }

    private static int ConfigureRect(string objectName, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
            return 0;

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
            return 0;

        Undo.RecordObject(rect, $"Adjust {objectName}");
        rect.anchoredPosition = anchoredPosition;
        if (sizeDelta != Vector2.zero)
            rect.sizeDelta = sizeDelta;
        return 1;
    }

    private static int ConfigureFeedbackPanel()
    {
        GameObject panel = GameObject.Find("FeedbackPanel");
        if (panel == null)
            return 0;

        int adjusted = 0;
        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect != null)
        {
            Undo.RecordObject(rect, "Adjust FeedbackPanel");
            rect.anchoredPosition = new Vector2(0f, 140f);
            rect.sizeDelta = new Vector2(760f, 140f);
            adjusted++;
        }

        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, "Adjust FeedbackPanel color");
            image.color = new Color(1f, 1f, 1f, 0.12f);
            adjusted++;
        }

        return adjusted;
    }

    private static int ConfigurePausePanelOverlay()
    {
        GameObject panel = GameObject.Find("PausePanel");
        if (panel == null)
            return 0;

        Image image = panel.GetComponent<Image>();
        if (image == null)
            return 0;

        Undo.RecordObject(image, "Adjust PausePanel overlay");
        image.color = new Color(1f, 1f, 1f, 0.24f);
        return 1;
    }

    private static int StyleTexts()
    {
        int styled = 0;
        styled += StyleTextIn("ScorePanel", 28f, 52f, FontStyles.Bold, 2f);
        styled += StyleTextIn("LevelPanel", 28f, 48f, FontStyles.Bold, 2f);
        styled += StyleTextIn("FeedbackPanel", 32f, 62f, FontStyles.Bold, 1f);
        styled += StylePauseTexts();
        return styled;
    }

    private static int StyleTextIn(string objectName, float minSize, float maxSize, FontStyles style, float spacing)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
            return 0;

        TMP_Text text = target.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return 0;

        Undo.RecordObject(text, $"Style {objectName} text");
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.fontStyle = style;
        text.characterSpacing = spacing;
        text.alignment = TextAlignmentOptions.Center;
        return 1;
    }

    private static int StylePauseTexts()
    {
        int styled = 0;
        GameObject pauseBox = GameObject.Find("PauseBox");
        if (pauseBox == null)
            return 0;

        TMP_Text[] texts = pauseBox.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Undo.RecordObject(texts[i], "Style PauseBox text");
            texts[i].enableAutoSizing = true;
            texts[i].fontSizeMin = 26f;
            texts[i].fontSizeMax = 54f;
            texts[i].fontStyle = FontStyles.Bold;
            texts[i].characterSpacing = 1.5f;
            texts[i].alignment = TextAlignmentOptions.Center;
            styled++;
        }

        return styled;
    }
}
