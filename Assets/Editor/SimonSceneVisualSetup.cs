using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SimonSceneVisualSetup
{
    [MenuItem("Tools/Simon/Apply Visual Polish Setup")]
    public static void ApplyVisualPolishSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "SimonScene")
        {
            EditorUtility.DisplayDialog("SimonScene", "Abre la escena SimonScene antes de ejecutar este setup.", "OK");
            return;
        }

        int buttonsUpdated = AddButtonPulseComponents();
        bool pausePanelUpdated = EnsurePausePanelCanvasGroup();
        bool pauseTransitionUpdated = EnsurePausePanelTransition();
        bool pauseViewUpdated = EnsurePausePanelView();
        int textsStyled = StyleSimonTexts();
        int layoutAdjusted = ApplySimonLayout();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorUtility.DisplayDialog(
            "SimonScene",
            $"Setup aplicado.\n\nBotones actualizados: {buttonsUpdated}\nCanvasGroup en PausePanel: {(pausePanelUpdated ? "agregado" : "ya existia o no se encontro")}\nUIPanelTransition en PausePanel: {(pauseTransitionUpdated ? "agregado" : "ya existia o no se encontro")}\nPausePanelView en PausePanel: {(pauseViewUpdated ? "agregado" : "ya existia o no se encontro")}\nTextos estilizados: {textsStyled}\nBloques ajustados: {layoutAdjusted}",
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
        SimonGameManager gameManager = Object.FindFirstObjectByType<SimonGameManager>(FindObjectsInactive.Include);
        if (gameManager == null)
            return false;

        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty pausePanelProperty = serializedManager.FindProperty("pausePanel");
        if (pausePanelProperty == null || pausePanelProperty.objectReferenceValue == null)
            return false;

        GameObject pausePanel = pausePanelProperty.objectReferenceValue as GameObject;
        if (pausePanel == null)
            return false;

        if (pausePanel.GetComponent<CanvasGroup>() != null)
            return false;

        Undo.AddComponent<CanvasGroup>(pausePanel);
        return true;
    }

    private static bool EnsurePausePanelTransition()
    {
        SimonGameManager gameManager = Object.FindFirstObjectByType<SimonGameManager>(FindObjectsInactive.Include);
        if (gameManager == null)
            return false;

        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty pausePanelProperty = serializedManager.FindProperty("pausePanel");
        if (pausePanelProperty == null || pausePanelProperty.objectReferenceValue == null)
            return false;

        GameObject pausePanel = pausePanelProperty.objectReferenceValue as GameObject;
        if (pausePanel == null)
            return false;

        if (pausePanel.GetComponent<UIPanelTransition>() != null)
            return false;

        Undo.AddComponent<UIPanelTransition>(pausePanel);
        return true;
    }

    private static bool EnsurePausePanelView()
    {
        SimonGameManager gameManager = Object.FindFirstObjectByType<SimonGameManager>(FindObjectsInactive.Include);
        if (gameManager == null)
            return false;

        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty pausePanelProperty = serializedManager.FindProperty("pausePanel");
        if (pausePanelProperty == null || pausePanelProperty.objectReferenceValue == null)
            return false;

        GameObject pausePanel = pausePanelProperty.objectReferenceValue as GameObject;
        if (pausePanel == null)
            return false;

        if (pausePanel.GetComponent<PausePanelView>() != null)
            return false;

        Undo.AddComponent<PausePanelView>(pausePanel);
        return true;
    }

    private static int StyleSimonTexts()
    {
        int styled = 0;
        SimonGameManager gameManager = Object.FindFirstObjectByType<SimonGameManager>(FindObjectsInactive.Include);
        if (gameManager == null)
            return 0;

        SerializedObject serializedManager = new SerializedObject(gameManager);
        styled += StyleTextProperty(serializedManager.FindProperty("statusText"), 42f, 88f, FontStyles.Bold, 0f);
        styled += StyleTextProperty(serializedManager.FindProperty("scoreText"), 36f, 64f, FontStyles.Bold, 2f);
        styled += StyleTextProperty(serializedManager.FindProperty("recordText"), 24f, 38f, FontStyles.Bold, 1f);
        return styled;
    }

    private static int ApplySimonLayout()
    {
        int adjusted = 0;
        adjusted += ConfigurePanel("HeaderPanel", new Vector2(0f, -48f), new Vector2(0f, 120f));
        adjusted += ConfigurePanel("RightPanel", new Vector2(-210f, -44f), new Vector2(320f, 120f));
        adjusted += ConfigureLivesPanel();
        adjusted += ConfigureTimeBar();
        adjusted += ConfigurePauseBox();
        adjusted += ConfigureMenuButton();
        return adjusted;
    }

    private static int ConfigurePanel(string objectName, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject panel = GameObject.Find(objectName);
        if (panel == null)
            return 0;

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null)
            return 0;

        Undo.RecordObject(rect, $"Adjust {objectName}");
        rect.anchoredPosition = anchoredPosition;
        if (sizeDelta != Vector2.zero)
            rect.sizeDelta = sizeDelta;
        return 1;
    }

    private static int ConfigureTimeBar()
    {
        int adjusted = 0;
        GameObject bg = GameObject.Find("TimeBarBG");
        if (bg != null)
        {
            RectTransform rect = bg.GetComponent<RectTransform>();
            Image image = bg.GetComponent<Image>();
            if (rect != null)
            {
                Undo.RecordObject(rect, "Adjust TimeBarBG");
                rect.sizeDelta = new Vector2(240f, 26f);
                adjusted++;
            }

            if (image != null)
            {
                Undo.RecordObject(image, "Adjust TimeBarBG color");
                image.color = new Color(0.16f, 0.15f, 0.22f, 0.85f);
                adjusted++;
            }
        }

        GameObject fill = GameObject.Find("TimeBarFill");
        if (fill != null)
        {
            Image image = fill.GetComponent<Image>();
            if (image != null)
            {
                Undo.RecordObject(image, "Adjust TimeBarFill color");
                image.color = new Color(1f, 0.93f, 0.6f, 1f);
                adjusted++;
            }
        }

        return adjusted;
    }

    private static int ConfigureLivesPanel()
    {
        GameObject livesPanel = GameObject.Find("LivesPanel");
        if (livesPanel == null)
            return 0;

        RectTransform rect = livesPanel.GetComponent<RectTransform>();
        if (rect == null)
            return 0;

        Undo.RecordObject(rect, "Adjust LivesPanel");
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(260f, 110f);
        return 1;
    }

    private static int ConfigurePauseBox()
    {
        GameObject pauseBox = GameObject.Find("PauseBox");
        if (pauseBox == null)
            return 0;

        int adjusted = 0;
        RectTransform rect = pauseBox.GetComponent<RectTransform>();
        if (rect != null)
        {
            Undo.RecordObject(rect, "Adjust PauseBox");
            rect.sizeDelta = new Vector2(620f, 420f);
            adjusted++;
        }

        Image image = pauseBox.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, "Adjust PauseBox color");
            image.color = new Color(0.11f, 0.11f, 0.16f, 0.94f);
            adjusted++;
        }

        return adjusted;
    }

    private static int ConfigureMenuButton()
    {
        GameObject menuButton = GameObject.Find("MenuButton");
        if (menuButton == null)
            return 0;

        int adjusted = 0;
        RectTransform rect = menuButton.GetComponent<RectTransform>();
        if (rect != null)
        {
            Undo.RecordObject(rect, "Adjust Simon MenuButton");
            rect.sizeDelta = new Vector2(120f, 120f);
            adjusted++;
        }

        return adjusted;
    }

    private static int StyleTextProperty(SerializedProperty property, float minSize, float maxSize, FontStyles style, float spacing)
    {
        if (property == null || property.objectReferenceValue == null)
            return 0;

        TMP_Text text = property.objectReferenceValue as TMP_Text;
        if (text == null)
            return 0;

        Undo.RecordObject(text, "Style Simon text");
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.fontStyle = style;
        text.characterSpacing = spacing;
        text.textWrappingMode = TextWrappingModes.Normal;
        return 1;
    }
}
