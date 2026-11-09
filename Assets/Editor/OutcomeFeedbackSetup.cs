using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OutcomeFeedbackSetup
{
    private const string Icons = "Assets/Sprites/CartoonUI_IconPack_Vol1/Icons/";
    private const string Frames = "Assets/Sprites/FeedbackJuiceFrames/";

    [MenuItem("Tools/UI/Apply Outcome Feedback")]
    public static void Apply()
    {
        Scene original = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || original.isDirty || !original.IsValid())
            throw new System.InvalidOperationException("Save open scenes and leave Play Mode before updating feedback.");

        string originalPath = original.path;
        try
        {
            Configure("Assets/Scenes/SimonScene.unity", typeof(SimonGameManager));
            Configure("Assets/Scenes/BasicrhythmScene.unity", typeof(BasicRhythmGameManager));
            Configure("Assets/Scenes/TempoTapScene.unity", typeof(TempoTapGameManager));
        }
        finally
        {
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        }
        Debug.Log("Outcome icons and CrispJuice feedback configured in Simon, Basic Rhythm and Tempo Tap.");
    }

    private static void Configure(string scenePath, System.Type managerType)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        MonoBehaviour manager = Object.FindFirstObjectByType(managerType, FindObjectsInactive.Include) as MonoBehaviour;
        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (manager == null || canvas == null)
            throw new System.InvalidOperationException("Missing manager or Canvas in " + scenePath);

        OutcomeFeedbackView view = canvas.GetComponent<OutcomeFeedbackView>();
        if (view == null)
            view = canvas.gameObject.AddComponent<OutcomeFeedbackView>();

        Sprite check = Load(Icons + "Check.png");
        Sprite cross = Load(Icons + "Cross.png");
        SerializedObject viewData = new SerializedObject(view);
        viewData.FindProperty("successIcon").objectReferenceValue = check;
        viewData.FindProperty("errorIcon").objectReferenceValue = cross;
        if (manager is SimonGameManager)
        {
            viewData.FindProperty("showErrorBurst").boolValue = false;
            viewData.FindProperty("burstSize").floatValue = 320f;
            viewData.FindProperty("iconSize").floatValue = 110f;
            viewData.FindProperty("offset").vector2Value = Vector2.zero;
        }
        else if (manager is TempoTapGameManager)
        {
            viewData.FindProperty("showSuccessBurst").boolValue = false;
            viewData.FindProperty("showErrorBurst").boolValue = false;
            viewData.FindProperty("iconSize").floatValue = 54f;
            viewData.FindProperty("burstSize").floatValue = 74f;
            viewData.FindProperty("offset").vector2Value = new Vector2(-295f, 0f);
        }
        SetFrames(viewData.FindProperty("successFrames"), "success", 12);
        SetFrames(viewData.FindProperty("errorFrames"), "error", 8);
        viewData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject managerData = new SerializedObject(manager);
        managerData.FindProperty("outcomeFeedback").objectReferenceValue = view;
        if (manager is SimonGameManager || manager is BasicRhythmGameManager)
        {
            managerData.FindProperty("successStatusSprite").objectReferenceValue = check;
            managerData.FindProperty(manager is SimonGameManager ? "errorStatusSprite" : "failStatusSprite")
                .objectReferenceValue = cross;
        }
        managerData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetFrames(SerializedProperty array, string name, int count)
    {
        array.arraySize = count;
        for (int i = 0; i < count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = Load(Frames + name + "_" + i.ToString("00") + ".png");
    }

    private static Sprite Load(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new System.InvalidOperationException("Missing feedback sprite: " + path);
        return sprite;
    }
}
