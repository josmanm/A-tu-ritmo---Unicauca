using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TopHudPanelView : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text levelText;

    [Header("Vidas")]
    [SerializeField] private Image[] lifeIcons = Array.Empty<Image>();

    [Header("Menu")]
    [SerializeField] private Button menuButton;

    [Header("Barra de tiempo")]
    [SerializeField] private GameObject timeBarRoot;
    [SerializeField] private Image timeBarFill;

    public TMP_Text ScoreText => scoreText;
    public TMP_Text LevelText => levelText;
    public Image[] LifeIcons => lifeIcons ?? Array.Empty<Image>();
    public Button MenuButton => menuButton;
    public GameObject TimeBarRoot => timeBarRoot;
    public Image TimeBarFill => timeBarFill;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            ResolveMissingReferences();
    }

    [ContextMenu("Resolve Missing References")]
    public void ResolveMissingReferences()
    {
        if (scoreText == null)
        {
            Transform scoreTextTransform = FindChild(transform, "ScoreText");
            Transform scorePanel = FindChild(transform, "ScorePanel");
            if (scoreTextTransform != null)
                scoreText = scoreTextTransform.GetComponent<TMP_Text>() ?? scoreTextTransform.GetComponentInChildren<TMP_Text>(true);
            else if (scorePanel != null)
                scoreText = scorePanel.GetComponentInChildren<TMP_Text>(true);
        }

        if (levelText == null)
        {
            Transform level = FindChild(transform, "LevelText");
            if (level != null)
                levelText = level.GetComponent<TMP_Text>();
        }

        if (menuButton == null)
            menuButton = FindButton(transform, "MenuButton");

        if (lifeIcons == null || lifeIcons.Length < 3 || lifeIcons.Any(life => life == null))
        {
            Transform livesPanel = FindChild(transform, "LifeIcons") ?? FindChild(transform, "LivesPanel");
            lifeIcons = FindLifeImages(livesPanel != null ? livesPanel : transform);
        }

        Transform timeBarPanel = FindTimeBarPanel(transform);
        if (timeBarRoot == null && timeBarPanel != null)
            timeBarRoot = timeBarPanel.gameObject;

        if (timeBarFill == null && timeBarPanel != null)
            timeBarFill = FindImage(timeBarPanel, "Fill") ?? FindImage(timeBarPanel, "TimeBarFill");
    }

    private static Transform FindTimeBarPanel(Transform root)
    {
        return FindChild(root, "TimeBar")
            ?? FindChild(root, "TimeBarRoot")
            ?? FindChild(root, "Timebar")
            ?? FindChild(root, "TimeBarBG");
    }

    private static Button FindButton(Transform root, string objectName)
    {
        Transform match = FindChild(root, objectName);
        return match != null ? match.GetComponent<Button>() ?? match.GetComponentInChildren<Button>(true) : null;
    }

    private static Image FindImage(Transform root, string objectName)
    {
        Transform match = FindChild(root, objectName);
        return match != null ? match.GetComponent<Image>() ?? match.GetComponentInChildren<Image>(true) : null;
    }

    private static Image[] FindLifeImages(Transform livesPanel)
    {
        if (livesPanel == null)
            return Array.Empty<Image>();

        Image[] namedLives =
        {
            FindImage(livesPanel, "Life1"),
            FindImage(livesPanel, "Life2"),
            FindImage(livesPanel, "Life3"),
        };

        if (namedLives.Any(life => life != null))
            return namedLives.Where(life => life != null).ToArray();

        return livesPanel
            .GetComponentsInChildren<Image>(true)
            .Where(image => image != null && image.name.StartsWith("Life", StringComparison.OrdinalIgnoreCase))
            .OrderBy(image => image.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }
}
