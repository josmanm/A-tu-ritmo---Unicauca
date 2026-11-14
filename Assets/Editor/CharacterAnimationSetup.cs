using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rebuildable sprite clips. The scene changes are limited to animation references;
// the existing beat clock, obstacle movement and colliders are not modified.
public static class CharacterAnimationSetup
{
    private const string Folder = "Assets/Animations/Characters";

    [MenuItem("Tools/A tu ritmo/Actualizar animaciones de personajes")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Animations", "Characters");

        BoySpeakFrames boy = AssetDatabase.LoadAssetAtPath<BoySpeakFrames>("Assets/Resources/BoySpeakFrames.asset");
        if (boy == null || boy.frames == null || boy.frames.Length == 0)
            throw new InvalidOperationException("Faltan los cuadros de BoySpeakFrames.");

        AnimationClip boyClip = Clip("BoySpeaking", boy.frames, typeof(UnityEngine.UI.Image));
        boy.speakingController = Controller("BoySpeaking", boyClip);
        EditorUtility.SetDirty(boy);

        Scene original = SceneManager.GetActiveScene();
        Scene band = EditorSceneManager.OpenScene("Assets/Scenes/BandaMusical.unity", OpenSceneMode.Additive);
        try
        {
            MusicianController bear = FindInScene<MusicianController>(band, "Oso");
            if (bear == null || bear.beatFrames == null || bear.beatFrames.Length == 0)
                throw new InvalidOperationException("Faltan los cuadros del oso en BandaMusical.");
            bear.beatAnimation = Clip("BearUI", bear.beatFrames, typeof(UnityEngine.UI.Image));
            if (bear.GetComponent<Animator>() == null) bear.gameObject.AddComponent<Animator>();
            EditorUtility.SetDirty(bear);
            EditorSceneManager.SaveScene(band);
        }
        finally
        {
            EditorSceneManager.CloseScene(band, true);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
        }

        Scene tempo = EditorSceneManager.OpenScene("Assets/Scenes/TempoTapScene.unity", OpenSceneMode.Additive);
        try
        {
            BeatSyncedObstacleSpawner spawner = FindInScene<BeatSyncedObstacleSpawner>(tempo);
            if (spawner == null || spawner.birdFrames == null || spawner.birdFrames.Length == 0)
                throw new InvalidOperationException("Faltan los cuadros del pájaro en TempoTapScene.");
            AnimationClip birdClip = Clip("BirdFlying", spawner.birdFrames, typeof(SpriteRenderer));
            spawner.birdAnimationController = Controller("BirdFlying", birdClip);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.SaveScene(tempo);
        }
        finally
        {
            EditorSceneManager.CloseScene(tempo, true);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Animaciones de chico, oso y pájaro actualizadas (12 cuadros/s). Los clips de mono y gato existentes se conservan.");
    }

    private static T FindInScene<T>(Scene scene, string name = null) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (name == null || component.name == name) return component;
        return null;
    }

    private static AnimationClip Clip(string name, Sprite[] frames, Type componentType)
    {
        string path = Folder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.frameRate = 12f;
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / clip.frameRate,
                value = frames[i]
            };
        var binding = new EditorCurveBinding { path = "", type = componentType, propertyName = "m_Sprite" };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.stopTime = frames.Length / clip.frameRate;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static RuntimeAnimatorController Controller(string name, AnimationClip clip)
    {
        string path = Folder + "/" + name + ".controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        if (machine.states.Length == 0) machine.AddState(name);
        machine.states[0].state.motion = clip;
        machine.defaultState = machine.states[0].state;
        EditorUtility.SetDirty(controller);
        return controller;
    }
}
