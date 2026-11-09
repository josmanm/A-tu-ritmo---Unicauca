using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class BandaMusicalSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/BandaMusical.unity";
    private const string Art = "Assets/Sprites/BandaMusical/";
    private const string TransparentArt = Art + "Transparent/";
    private const string HudPath = "Assets/Prefabs/TopHudPanel.prefab";
    private const string SongsPath = "Assets/Audio/Audios Banda Musical/";
    [MenuItem("Tools/A Tu Ritmo/Vincular animación monkey al mono")]
    public static void ConfigureMonkeyAnimationInExistingScene()
    {
        ConfigureMusiciansInExistingScene(true, false);
    }

    [MenuItem("Tools/A Tu Ritmo/Vincular animaciones mono y gato")]
    public static void ConfigureMonkeyAndCatAnimationsInExistingScene()
    {
        ConfigureMusiciansInExistingScene(true, true);
    }

    private static void ConfigureMusiciansInExistingScene(bool monkey, bool cat)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de editar las animaciones.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity para configurar los músicos.");
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>()).FirstOrDefault(item => item != null);
        if (game == null) throw new InvalidOperationException("No se encontró la banda musical.");
        if (monkey) ConfigureMusicianAnimation(game.monkey, "Monkey", "monkey");
        if (cat) ConfigureMusicianAnimation(game.cat, "Cat", "cat");
        EditorSceneManager.MarkSceneDirty(scene);
        // Save after reviewing other pending edits in the scene.
    }

    private static void ConfigureMusicianAnimation(MusicianController musician, string spriteName, string clipName)
    {
        if (musician == null || musician.portrait == null)
            throw new InvalidOperationException("No se encontró " + spriteName + " en el Canvas.");
        string clipPath = Art + "Animaciones/" + clipName + ".anim";
        string uiClipPath = Art + "Animaciones/" + clipName + "-ui.anim";
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        Sprite[] frames = Frames(TransparentArt + spriteName + ".png");
        if (source == null || frames.Length != 16)
            throw new InvalidOperationException("Falta " + clipPath + " o alguno de sus 16 cuadros transparentes.");

        // Preserve the user's SpriteRenderer clip. The in-game monkey is a UI
        // Image, so adapt its sprite curve without changing the authored asset.
        EditorCurveBinding[] sourceBindings = AnimationUtility.GetObjectReferenceCurveBindings(source);
        EditorCurveBinding sourceBinding = sourceBindings.FirstOrDefault(binding =>
            binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite");
        if (sourceBinding.type == null)
            throw new InvalidOperationException(clipName + ".anim no anima SpriteRenderer.m_Sprite.");
        ObjectReferenceKeyframe[] originalKeys = AnimationUtility.GetObjectReferenceCurve(source, sourceBinding);
        if (originalKeys == null || originalKeys.Length != 16)
            throw new InvalidOperationException(clipName + ".anim necesita 16 cuadros para vincularse al UI.");

        AnimationClip uiClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(uiClipPath);
        if (uiClip == null)
        {
            uiClip = new AnimationClip { name = clipName + "-ui" };
            AssetDatabase.CreateAsset(uiClip, uiClipPath);
        }
        Undo.RecordObject(uiClip, "Adaptar " + clipName + ".anim al Canvas");
        uiClip.frameRate = source.frameRate;
        var keys = new ObjectReferenceKeyframe[originalKeys.Length];
        for (int i = 0; i < originalKeys.Length; i++)
        {
            Sprite originalSprite = originalKeys[i].value as Sprite;
            Sprite transparent = originalSprite == null ? null :
                frames.FirstOrDefault(frame => frame.name == originalSprite.name);
            if (transparent == null)
                throw new InvalidOperationException("No hay cuadro transparente para " + originalKeys[i].value);
            keys[i] = new ObjectReferenceKeyframe { time = originalKeys[i].time, value = transparent };
        }
        AnimationUtility.SetObjectReferenceCurve(uiClip,
            EditorCurveBinding.PPtrCurve("", typeof(UnityEngine.UI.Image), "m_Sprite"), keys);
        var clipData = new SerializedObject(uiClip);
        SerializedProperty loopTime = clipData.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopTime != null)
        {
            loopTime.boolValue = true;
            clipData.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(uiClip);
        AssetDatabase.SaveAssets();

        // AnimationClip.SampleAnimation needs an Animator on the target object
        // to evaluate UI object-reference curves; no controller is needed.
        Animator animator = musician.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
            throw new InvalidOperationException(spriteName + " ya tiene un AnimatorController; no se sobrescribirá.");
        if (animator == null)
            animator = Undo.AddComponent<Animator>(musician.gameObject);

        Undo.RecordObject(musician, "Sincronizar animación de " + spriteName);
        Undo.RecordObject(musician.portrait, "Figura base de " + spriteName);
        musician.beatAnimation = uiClip;
        musician.portrait.sprite = frames[0];
        EditorUtility.SetDirty(musician);
        EditorUtility.SetDirty(musician.portrait);
    }

    [MenuItem("Tools/A Tu Ritmo/Configurar tres retos de la banda")]
    public static void ConfigureSoloBreakInExistingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de editar la banda.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity para configurar el momento solo.");
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>()).FirstOrDefault(item => item != null);
        if (game == null || game.levels == null || game.levels.Length < 3)
            throw new InvalidOperationException("Se necesitan los tres niveles de la banda.");
        Undo.RecordObject(game, "Tres retos por nivel de la banda");
        for (int i = 0; i < 3; i++)
        {
            game.levels[i].levelDurationSeconds = 60f;
            game.levels[i].challengeDurationSeconds = 5f;
            game.levels[i].firstChallengeMusicFraction = 0.6f;
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        // Do not save other pending scene changes without reviewing them.
    }

    [MenuItem("Tools/A Tu Ritmo/Configurar loops de los tres niveles de la banda")]
    public static void ConfigureLevelLoopsInExistingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de editar la banda.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity para configurar los loops.");
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>()).FirstOrDefault(item => item != null);
        if (game == null || game.levels == null || game.levels.Length < 3)
            throw new InvalidOperationException("Se necesitan los tres niveles de la banda.");
        AudioClip[] guided = new AudioClip[3];
        AudioClip[] backing = new AudioClip[3];
        int[] tempos = { 60, 80, 100 };
        for (int i = 0; i < tempos.Length; i++)
        {
            guided[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath + $"banda_{tempos[i]}bpm_guiada.wav");
            backing[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath + $"banda_{tempos[i]}bpm_fondo.wav");
            if (guided[i] == null || backing[i] == null)
                throw new InvalidOperationException("Falta una pareja de WAV del nivel " + (i + 1));
        }
        Undo.RecordObject(game, "Asignar loops de la banda");
        AssignLevelLoops(game, guided, backing, tempos);
        game.animalHit = AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath + "golpe_animal.wav");
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        // Deliberately do not save: the user may have other unsaved scene edits.
        // Save BandaMusical.unity in the Editor after reviewing the new references.
    }

    private static void AssignLevelLoops(BandaMusicalGameManager game,
        AudioClip[] guided, AudioClip[] backing, int[] tempos)
    {
        for (int i = 0; i < tempos.Length; i++)
        {
            game.levels[i].bpm = tempos[i];
            game.levels[i].guidedLoop = guided[i];
            game.levels[i].backgroundLoop = backing[i];
            game.levels[i].beatsPerLoop = 16;
            game.levels[i].levelDurationSeconds = 60f;
            game.levels[i].challengeDurationSeconds = 5f;
            game.levels[i].firstChallengeMusicFraction = 0.6f;
        }
    }

    [MenuItem("Tools/A Tu Ritmo/Configurar canciones de La banda musical")]
    public static void ConfigureSongsInExistingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de editar las canciones.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity para configurarla.");
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>()).FirstOrDefault(item => item != null);
        if (game == null) throw new InvalidOperationException("No se encontró el controlador de la banda.");
        if (game.songs != null && game.songs.Length > 0)
            throw new InvalidOperationException("Ya hay canciones configuradas; no se sobrescribirán tus ajustes.");
        Undo.RecordObject(game, "Configurar canciones de la banda");
        AssignKnownSongs(game);
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AssignKnownSongs(BandaMusicalGameManager game)
    {
        AudioClip pulse80 = AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath +
            "lesiakower-80-bpm-drum-loop-457509.mp3");
        AudioClip mix120 = AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath +
            "freesound_community-game-loops-001-simple-mix-1-long-loop-120-bpm-59010.mp3");
        if (pulse80 == null || mix120 == null)
            throw new InvalidOperationException("Falta alguna de las pistas con BPM identificado.");
        game.songs = new[]
        {
            new BandaSongConfiguration
            {
                id = "pulso_80", clip = pulse80, bpm = 80f,
                firstBeatOffsetSeconds = 0.03f, calibratedByListening = false
            },
            new BandaSongConfiguration
            {
                id = "mix_120", clip = mix120, bpm = 120f,
                firstBeatOffsetSeconds = 0.05f, calibratedByListening = false
            }
        };
        game.selectedSongIndex = 0;
    }

    [MenuItem("Tools/A Tu Ritmo/Ordenar músicos y guiar pulsos")]
    public static void ArrangeAndCueExistingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Detén Play Mode antes de editar la escena.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity para actualizarla.");
        if (scene.isDirty)
            throw new InvalidOperationException("Guarda los cambios pendientes de BandaMusical antes de actualizarla.");
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>()).FirstOrDefault(item => item != null);
        if (game == null || game.monkey == null || game.cat == null || game.bear == null || game.drum == null)
            throw new InvalidOperationException("Faltan referencias de los músicos o del tambor.");
        PositionMusicians(game);
        ConfigureDrumCue(game.drum);
        EditorUtility.SetDirty(game.drum);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void PositionMusicians(BandaMusicalGameManager game)
    {
        MusicianController[] musicians = { game.monkey, game.cat, game.bear };
        float[] xPositions = { 0f, -294f, 294f };
        for (int i = 0; i < musicians.Length; i++)
        {
            RectTransform rect = musicians[i].transform as RectTransform;
            if (rect == null) throw new InvalidOperationException("El músico no tiene RectTransform.");
            Undo.RecordObject(rect, "Ordenar la banda musical");
            rect.anchoredPosition = new Vector2(xPositions[i], 64f);
        }
    }

    private static void ConfigureDrumCue(DrumInputController drum)
    {
        UnityEngine.UI.Outline outline = drum.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null) outline = Undo.AddComponent<UnityEngine.UI.Outline>(drum.gameObject);
        Undo.RecordObjects(new UnityEngine.Object[] { drum, outline }, "Indicar pulso del tambor");
        outline.effectDistance = new Vector2(7f, -7f);
        outline.effectColor = new Color(1f, 0.78f, 0.16f, 0.85f);
        outline.useGraphicAlpha = true;
        outline.enabled = false;
        drum.beatOutline = outline;
    }

    [MenuItem("Tools/A Tu Ritmo/Agregar HUD a La banda musical")]
    public static void AddHudToExistingScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity para agregar el HUD.");
        BandaMusicalGameManager game = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<BandaMusicalGameManager>()).FirstOrDefault(item => item != null);
        Canvas canvas = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<Canvas>()).FirstOrDefault(item => item != null);
        if (game == null || canvas == null)
            throw new InvalidOperationException("Faltan el controlador o el Canvas de la banda.");
        AddHud(scene, canvas, game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AddHud(Scene scene, Canvas canvas, BandaMusicalGameManager game)
    {
        TopHudPanelView hud = canvas.GetComponentInChildren<TopHudPanelView>(true);
        if (hud == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (prefab == null) throw new InvalidOperationException("Falta el HUD: " + HudPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(canvas.transform, false);
            hud = instance.GetComponent<TopHudPanelView>();
        }
        if (hud == null) throw new InvalidOperationException("El prefab no tiene TopHudPanelView.");
        hud.ResolveMissingReferences();
        Transform lives = hud.transform.Find("LifeIcons");
        if (lives != null) lives.gameObject.SetActive(false); // No lives or penalties in this game.
        if (hud.TimeBarRoot != null) hud.TimeBarRoot.SetActive(false);
        if (game.backButton != null) game.backButton.gameObject.SetActive(false);
        if (game.message != null)
        {
            RectTransform instructions = game.message.transform as RectTransform;
            if (instructions != null) instructions.anchoredPosition = new Vector2(0, 230);
        }
        game.topHud = hud;
        EditorUtility.SetDirty(game);
        hud.transform.SetAsLastSibling();
    }

    [MenuItem("Tools/A Tu Ritmo/Corregir transparencia de La banda musical")]
    public static void UseTransparentMusicians()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Abre BandaMusical.unity antes de actualizar sus músicos.");
        if (scene.isDirty)
            throw new InvalidOperationException("Hay cambios sin guardar en BandaMusical; guárdalos antes de actualizar los sprites.");

        foreach (string name in new[] { "Monkey", "Cat", "Bear" })
        {
            string objectName = name == "Monkey" ? "Mono" : name == "Cat" ? "Gato" : "Oso";
            MusicianController musician = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MusicianController>(true))
                .FirstOrDefault(item => item.name == objectName);
            Sprite[] frames = Frames(TransparentArt + name + ".png");
            if (musician == null || musician.portrait == null || frames.Length != 16)
                throw new InvalidOperationException("No se encontraron los 16 cuadros transparentes de " + name);

            Undo.RecordObjects(new UnityEngine.Object[] { musician, musician.portrait }, "Quitar fondo gris de " + objectName);
            musician.beatFrames = frames;
            musician.portrait.sprite = frames[0];
            EditorUtility.SetDirty(musician);
            EditorUtility.SetDirty(musician.portrait);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("BandaMusical: spritesheets originales conservados; los músicos de la escena usan copias con alfa.");
    }

    [MenuItem("Tools/A Tu Ritmo/Construir La banda musical")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("La escena ya existe. No se sobrescribirá: " + ScenePath);

        Sprite backgroundSprite = RequireSprite("Assets/Sprites/background_basic rhythm.png");
        Sprite monkeySprite = Frames(TransparentArt + "Monkey.png")[0];
        Sprite catSprite = Frames(TransparentArt + "Cat.png")[0];
        Sprite bearSprite = Frames(TransparentArt + "Bear.png")[0];
        Sprite drumIdle = FindSprite(Art + "tambores.png", "tambores_0");
        Sprite drumHit = FindSprite(Art + "tambores.png", "tambores_10");
        Sprite handSprite = FindSprite(Art + "set de assets.png", "assets_14");
        Sprite starSprite = FindSprite(Art + "set de assets.png", "set de assets_6");
        Sprite earSprite = FindSprite(Art + "set de assets.png", "set de assets_5");
        AudioClip song = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/Audios Banda Musical/lesiakower-80-bpm-drum-loop-457509.mp3");
        if (song == null) throw new InvalidOperationException("No se encuentra el loop de 80 BPM.");

        Scene previouslyActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<LandscapePhoneCanvas>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        UnityEngine.UI.Image background = Image(canvasGo.transform, "Background", backgroundSprite,
            Vector2.zero, new Vector2(1280, 800));
        background.raycastTarget = false;
        background.gameObject.AddComponent<GameCanvasBackgroundCover>();

        var managerGo = new GameObject("BandaMusicalGameManager");
        var manager = managerGo.AddComponent<BandaMusicalGameManager>();
        var rhythm = managerGo.AddComponent<RhythmManager>();
        var recorder = managerGo.AddComponent<BandaSessionRecorder>();
        manager.rhythm = rhythm;
        manager.recorder = recorder;
        rhythm.music = song;
        // The music is scheduled from the same DSP origin as the expected beats.
        // The 80 BPM loop's first transient begins about 30 ms into the decoded file.
        // Keep this editable; perceptual alignment must still be checked by ear.
        rhythm.beatOffsetSeconds = 0.03f;
        rhythm.musicSource = Audio(managerGo, "Music");
        manager.voiceSource = Audio(managerGo, "Voice");
        manager.positiveSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/success.mp3");
        AssignKnownSongs(manager);
        manager.animalHit = AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath + "golpe_animal.wav");
        int[] levelTempos = { 60, 80, 100 };
        AudioClip[] guidedLoops = levelTempos.Select(tempo =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath + $"banda_{tempo}bpm_guiada.wav")).ToArray();
        AudioClip[] backingLoops = levelTempos.Select(tempo =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(SongsPath + $"banda_{tempo}bpm_fondo.wav")).ToArray();
        if (guidedLoops.All(clip => clip != null) && backingLoops.All(clip => clip != null))
            AssignLevelLoops(manager, guidedLoops, backingLoops, levelTempos);

        manager.message = Text(canvasGo.transform, "Instructions", "¡Toca el tambor!",
            new Vector2(0, 294), new Vector2(820, 85), 39);
        manager.monkey = Musician(canvasGo.transform, "Mono", monkeySprite,
            Frames(TransparentArt + "Monkey.png"), new Vector2(0, 64));
        manager.cat = Musician(canvasGo.transform, "Gato", catSprite,
            Frames(TransparentArt + "Cat.png"), new Vector2(-294, 64));
        manager.bear = Musician(canvasGo.transform, "Oso", bearSprite,
            Frames(TransparentArt + "Bear.png"), new Vector2(294, 64));

        var drumImage = Image(canvasGo.transform, "TamborInteractivo", drumIdle,
            new Vector2(0, -230), new Vector2(420, 310));
        drumImage.preserveAspect = true;
        drumImage.raycastTarget = true;
        var drum = drumImage.gameObject.AddComponent<DrumInputController>();
        drum.game = manager;
        drum.drumImage = drumImage;
        drum.idleSprite = drumIdle;
        drum.hitSprite = drumHit;
        drum.source = Audio(managerGo, "DrumSound");
        ConfigureDrumCue(drum);
        manager.drum = drum;

        manager.hand = Image(canvasGo.transform, "ManoDeAyuda", handSprite,
            new Vector2(228, -155), new Vector2(112, 105));
        manager.hand.raycastTarget = false;
        manager.hand.preserveAspect = true;
        manager.star = Image(canvasGo.transform, "Estrella", starSprite,
            new Vector2(385, 220), new Vector2(130, 110));
        manager.star.raycastTarget = false;
        manager.star.preserveAspect = true;
        manager.ear = Image(canvasGo.transform, "Escuchar", earSprite,
            new Vector2(-400, -210), new Vector2(135, 110));
        manager.ear.raycastTarget = false;
        manager.ear.preserveAspect = true;

        manager.backButton = Button(canvasGo.transform, "Volver", "VOLVER",
            new Vector2(96, -50), new Vector2(170, 70));
        // Use normalized anchors for corner buttons instead of moving with the drum.
        RectTransform backRect = (RectTransform)manager.backButton.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0, 1);
        backRect.anchoredPosition = new Vector2(110, -54);
        manager.replayButton = Button(canvasGo.transform, "OtraVez", "OTRA VEZ",
            new Vector2(0, -80), new Vector2(220, 74));

        AddHud(scene, canvas, manager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = EditorBuildSettings.scenes.ToList();
        if (!buildScenes.Any(s => s.path == ScenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        AssetDatabase.SaveAssets();
        SceneManager.SetActiveScene(previouslyActive);
        Debug.Log("La banda musical: escena y Build Settings creados sin tocar la escena abierta.");
    }

    private static AudioSource Audio(GameObject owner, string name)
    {
        var go = new GameObject(name, typeof(AudioSource));
        go.transform.SetParent(owner.transform);
        AudioSource source = go.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        return source;
    }

    private static UnityEngine.UI.Image Image(Transform parent, string name, Sprite sprite,
        Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = Color.white;
        return image;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string content,
        Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(32, 46, 83, 255);
        text.raycastTarget = false;
        return text;
    }

    private static UnityEngine.UI.Button Button(Transform parent, string name, string label,
        Vector2 position, Vector2 size)
    {
        var image = Image(parent, name, null, position, size);
        image.color = new Color32(255, 232, 158, 255);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        Text(image.transform, "Label", label, Vector2.zero, size - new Vector2(16, 14), 29);
        return button;
    }

    private static MusicianController Musician(Transform parent, string name, Sprite sprite,
        Sprite[] frames, Vector2 position)
    {
        var image = Image(parent, name, sprite, position, new Vector2(245, 245));
        image.preserveAspect = true;
        image.raycastTarget = false;
        var musician = image.gameObject.AddComponent<MusicianController>();
        musician.portrait = image;
        musician.beatFrames = frames;
        return musician;
    }

    private static Sprite RequireSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) sprite = Frames(path).FirstOrDefault();
        if (sprite == null) throw new InvalidOperationException("Falta sprite: " + path);
        return sprite;
    }

    private static Sprite FindSprite(string path, string name)
    {
        Sprite sprite = Frames(path).FirstOrDefault(s => s.name == name);
        if (sprite == null) throw new InvalidOperationException("Falta sub-sprite " + name + " en " + path);
        return sprite;
    }

    private static Sprite[] Frames(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => FrameNumber(s.name)).ToArray();
    }

    private static int FrameNumber(string name)
    {
        int underscore = name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int n) ? n : 0;
    }

}
