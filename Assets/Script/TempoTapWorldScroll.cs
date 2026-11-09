using System;
using UnityEngine;

public class TempoTapWorldScroll : MonoBehaviour
{
    [Serializable]
    public class ParallaxStrip
    {
        public Transform root;
        [Min(0.01f)] public float width = 20.48f;
        [Range(0f, 1f)] public float speedRatio = 0.1f;
    }

    public BeatSyncedObstacleSpawner obstacleSpawner;
    public RunnerController2D runner;
    public Transform groundArt;
    [Min(0.01f)] public float groundTileWidth = 1.28f;
    public ParallaxStrip[] layers;
    [Min(0f)] public float speedResponsiveness = 8f;

    Vector3 groundOrigin;
    Vector3[] layerOrigins;
    double distance;
    float currentSpeed;
    bool wasMoving;
    SpriteRenderer backgroundRenderer;
    Camera backgroundCamera;
    float lastCameraAspect = -1f;

    void Start()
    {
        GameObject background = GameObject.Find("TempoTapBackground");
        if (background != null)
            backgroundRenderer = background.GetComponent<SpriteRenderer>();
        backgroundCamera = Camera.main;
        ResizeBackground();
    }

    void LateUpdate()
    {
        ResizeBackground();
    }

    private void ResizeBackground()
    {
        // The Simulator can switch from tablet to phone without reloading the scene.
        if (backgroundRenderer == null || backgroundRenderer.sprite == null ||
            backgroundCamera == null || !backgroundCamera.orthographic ||
            Mathf.Approximately(lastCameraAspect, backgroundCamera.aspect))
            return;

        lastCameraAspect = backgroundCamera.aspect;
        float visibleHeight = 2f * backgroundCamera.orthographicSize;
        float visibleWidth = visibleHeight * lastCameraAspect;
        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        float scale = Mathf.Max(visibleWidth / spriteSize.x, visibleHeight / spriteSize.y);
        Transform background = backgroundRenderer.transform;
        background.localScale = new Vector3(scale, scale, background.localScale.z);
    }

    void Awake()
    {
        if (groundArt != null)
            groundOrigin = groundArt.localPosition;

        layerOrigins = new Vector3[layers != null ? layers.Length : 0];
        for (int i = 0; i < layerOrigins.Length; i++)
            if (layers[i] != null && layers[i].root != null)
                layerOrigins[i] = layers[i].root.localPosition;
    }

    void Update()
    {
        // The runner starts before the warm-up completes; scenery should start with it.
        if (obstacleSpawner == null || runner == null || !runner.GameStarted)
        {
            wasMoving = false;
            return;
        }

        float targetSpeed = obstacleSpawner.GetVisualScrollSpeed();
        currentSpeed = wasMoving
            ? Mathf.Lerp(currentSpeed, targetSpeed, 1f - Mathf.Exp(-speedResponsiveness * Time.deltaTime))
            : targetSpeed;
        wasMoving = true;
        distance += currentSpeed * Time.deltaTime;

        // Move only the artwork: the ground collider and the runner stay in place.
        if (groundArt != null)
            groundArt.localPosition = groundOrigin + Vector3.left * (float)(distance % groundTileWidth);

        for (int i = 0; i < layerOrigins.Length; i++)
        {
            ParallaxStrip layer = layers[i];
            if (layer == null || layer.root == null || layer.width <= 0f)
                continue;

            float offset = (float)((distance * layer.speedRatio) % layer.width);
            layer.root.localPosition = layerOrigins[i] + Vector3.left * offset;
        }
    }
}
