using UnityEngine;
using System.Collections.Generic;

public class BeatSyncedObstacleSpawner : MonoBehaviour
{
    public enum ObstacleAction { Tap, Jump, Duck }

    private struct ScheduledObstacle
    {
        public ObstacleMoverBeat mover;
        public double beatTime;
        public ObstacleAction action;
    }

    public BeatController beat;

    [Header("Prefabs/Refs")]
    public GameObject obstaclePrefab;
    public Sprite logSprite;
    public Sprite birdSprite;
    public Sprite[] birdFrames;
    [Min(1f)] public float birdFramesPerSecond = 12f;
    [Min(0.01f)] public float birdVisualScale = 5f;
    [Min(0.01f)] public float logVisualScale = 1f;
    [Tooltip("Tamaño visible del tronco en unidades de mundo; cero conserva la escala anterior.")]
    public Vector2 logVisualSize = Vector2.zero;
    public Collider2D groundCollider;
    public Transform runner;          // Runner transform (posición x fija)
    public Transform destroyPoint;    // Punto a la izquierda

    [Header("Spawn points")]
    public float spawnX = 12f;
    public float obstacleY = -2.5f;

    [Header("Beat design")]
    public int beatsAhead = 4;        // cuántos beats antes spawnear
    public int beatSpacing = 2;       // 2 = cada 2 beats aparece uno
    [Min(0f)] public float actionVisibilityDistance = 5.5f;

    [Header("Jump timing")]
    public float obstacleArrivalDelay = 0.22f;
    public float contactPadding = 0.1f;
    public float minimumTravelTime = 0.55f;
    [Range(0f, 1f)] public float adaptiveJumpStrength = 0.8f;
    public float minimumJumpMultiplier = 0.88f;
    public float maximumJumpMultiplier = 1.22f;
    public float earlyContactBias = 0.08f;
    [Min(0f)] public float jumpContactMargin = 0.04f;
    [Min(0f)] public float jumpLandingMargin = 0.04f;

    [Header("Jump heights by obstacle")]
    public float logJumpMultiplier = 0.68f;
    [Min(0.1f)] public float duckContactDelay = 0.3f;

    [Header("Fallback obstacle")]
    public Vector2 fallbackObstacleSize = new(0.85f, 1.8f);
    public Color fallbackObstacleColor = new(0.18f, 0.18f, 0.18f, 1f);

    public bool gameStarted = false;

    int beatCount;
    static Sprite fallbackSprite;
    Collider2D runnerCollider;
    float standingRunnerHeight;
    RunnerController2D runnerController;
    RunnerCollision runnerCollision;
    readonly List<ObstacleMoverBeat> activeObstacles = new();
    readonly List<ScheduledObstacle> scheduledObstacles = new();
    int obstacleCount;
    int difficultyStage;
    float lastObstacleSpeed;


    void OnEnable()
    {
        if (beat != null) beat.OnBeat += HandleBeat;

        if (runner != null && runnerCollider == null)
            runnerCollider = runner.GetComponent<Collider2D>();

        if (runnerCollider != null && standingRunnerHeight <= 0f)
            standingRunnerHeight = runnerCollider.bounds.size.y;

        if (runner != null && runnerController == null)
            runnerController = runner.GetComponent<RunnerController2D>();

        if (runner != null && runnerCollision == null)
            runnerCollision = runner.GetComponent<RunnerCollision>();
    }

    void OnDisable()
    {
        if (beat != null) beat.OnBeat -= HandleBeat;
    }

    void Update()
    {
        if (!gameStarted || runnerCollision == null || runnerCollision.game == null)
            return;

        // A mistimed jump can clear the collider physically, but only a valid
        // beat may mark the obstacle as avoided. Wait for late assisted taps.
        double now = AudioSettings.dspTime;
        double grace = runnerCollision.game.CurrentAssistMs * 0.001 + 0.04;
        foreach (ScheduledObstacle scheduled in scheduledObstacles)
        {
            ObstacleMoverBeat mover = scheduled.mover;
            if (mover == null || mover.avoidedOnBeat || mover.penaltyApplied ||
                now < scheduled.beatTime + grace || mover.transform.position.x > mover.runnerX)
                continue;

            runnerCollision.RegisterMissedObstacle(mover);
        }
    }

    void HandleBeat(double beatDspTime)
    {
        if (!gameStarted) return;

        beatCount++;

        // Leave enough time for the previous jump or slide to finish.
        int spacing = difficultyStage >= 1 ? 2 : Mathf.Max(2, beatSpacing);
        if ((beatCount - 1) % spacing != 0) return;

        double targetBeatTime = beatDspTime + beat.IntervalSec * beatsAhead;
        SpawnForTargetBeat(targetBeatTime);
    }

    void SpawnForTargetBeat(double targetBeatTime)
    {
        if (runner == null || beat == null) return;

        float runnerX = runner.position.x;
        double now = AudioSettings.dspTime;
        double desiredContactTime = targetBeatTime + obstacleArrivalDelay;

        if (desiredContactTime - now < minimumTravelTime)
        {
            targetBeatTime += beat.IntervalSec;
            desiredContactTime = targetBeatTime + obstacleArrivalDelay;
        }

        ObstacleAction action = obstacleCount > 0 && obstacleCount % 3 == 2 ? ObstacleAction.Duck : ObstacleAction.Jump;
        bool wideGround = action == ObstacleAction.Jump && obstacleCount % 3 == 1;
        obstacleCount++;

        // La altura del ave no debe depender de si el jugador salta o se agacha al generarla.
        float runnerTop = groundCollider != null && standingRunnerHeight > 0f
            ? groundCollider.bounds.max.y + standingRunnerHeight
            : runnerCollider != null ? runnerCollider.bounds.max.y : runner.position.y + 0.8f;
        // The log's painted base sits about 0.3 units below its sprite pivot.
        // Place it against the actual ground collider, not the old square's centre.
        float groundTop = groundCollider != null ? groundCollider.bounds.max.y : obstacleY;
        float logY = logSprite != null ? groundTop + 0.3f : obstacleY;
        var pos = new Vector3(spawnX, action == ObstacleAction.Duck ? runnerTop - 0.2f : logY, 0f);
        GameObject obs = action == ObstacleAction.Duck || obstaclePrefab == null
            ? CreateFallbackObstacle(pos) : Instantiate(obstaclePrefab, pos, Quaternion.identity);

        if (action == ObstacleAction.Duck)
        {
            obs.name = "BirdSlideObstacle";
            BoxCollider2D collider = obs.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(1.05f, 0.52f);
            collider.offset = Vector2.zero;
            collider.isTrigger = true;
            obs.transform.localScale = Vector3.one;
            SpriteRenderer birdRenderer = obs.GetComponentInChildren<SpriteRenderer>();
            if ((birdFrames != null && birdFrames.Length > 0) || birdSprite != null)
            {
                birdRenderer.sprite = birdFrames != null && birdFrames.Length > 0 ? birdFrames[0] : birdSprite;
                birdRenderer.color = Color.white;
                birdRenderer.flipX = true;
                birdRenderer.transform.localScale = Vector3.one *
                    (birdFrames != null && birdFrames.Length > 0 ? birdVisualScale : 1f);
                // Narrower than the wings to allow a forgiving slide.
                birdRenderer.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }
            else
            {
                birdRenderer.transform.localScale = new Vector3(1.05f, 0.52f, 1f);
                birdRenderer.color = new Color(0.5f, 0.34f, 0.84f);
            }
        }
        else if (wideGround)
        {
            obs.name = "WideJumpObstacle";
            BoxCollider2D collider = obs.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 0.68f);
            collider.isTrigger = true;
            if (logSprite == null)
                obs.GetComponentInChildren<SpriteRenderer>().color = new Color(0.92f, 0.43f, 0.29f);
        }

        SpriteRenderer obstacleRenderer = obs.GetComponentInChildren<SpriteRenderer>();
        if (obstacleRenderer != null)
        {
            obstacleRenderer.sortingOrder = 9;
            if (action == ObstacleAction.Jump && logSprite != null)
            {
                obstacleRenderer.sprite = logSprite;
                obstacleRenderer.color = Color.white;
                // Keep the prefab's root collider independent from the artwork scale.
                Vector3 visualScale = logVisualSize.x > 0f && logVisualSize.y > 0f
                    ? new Vector3(logVisualSize.x / logSprite.bounds.size.x,
                        logVisualSize.y / logSprite.bounds.size.y, 1f)
                    : Vector3.one * logVisualScale;
                if (obstacleRenderer.transform == obs.transform && visualScale != Vector3.one)
                {
                    var visual = new GameObject("RockVisual");
                    visual.transform.SetParent(obs.transform, false);
                    visual.transform.localScale = visualScale;
                    var rockRenderer = visual.AddComponent<SpriteRenderer>();
                    rockRenderer.sprite = logSprite;
                    rockRenderer.sortingOrder = obstacleRenderer.sortingOrder;
                    obstacleRenderer.enabled = false;
                }
                else if (obstacleRenderer.transform != obs.transform)
                {
                    obstacleRenderer.transform.localScale = visualScale;
                    obstacleRenderer.transform.localPosition = Vector3.zero;
                }
                BoxCollider2D logCollider = obs.GetComponent<BoxCollider2D>();
                logCollider.size = new Vector2(wideGround ? 0.9f : 0.78f, 0.6f);
                logCollider.offset = Vector2.zero;
                logCollider.isTrigger = true;
            }
            else if (action == ObstacleAction.Jump && !wideGround)
                obstacleRenderer.color = new Color(0.12f, 0.25f, 0.44f);
        }

        var mover = obs.GetComponent<ObstacleMoverBeat>();
        if (mover == null) mover = obs.AddComponent<ObstacleMoverBeat>();
        if (action == ObstacleAction.Duck && birdFrames != null && birdFrames.Length > 0)
            mover.ConfigureAnimation(obs.GetComponentInChildren<SpriteRenderer>(), birdFrames, birdFramesPerSecond);

        float obstacleHalfWidth = GetHalfWidth(obs.GetComponent<Collider2D>());
        float obstacleTopY = GetTopY(obs.GetComponent<Collider2D>(), obstacleY);
        float runnerHalfWidth = GetHalfWidth(runnerCollider);
        float obstacleVisualHalfWidth = obstacleHalfWidth;
        if (action == ObstacleAction.Jump)
        {
            foreach (SpriteRenderer visual in obs.GetComponentsInChildren<SpriteRenderer>())
                if (visual.enabled && visual.sprite != null)
                    obstacleVisualHalfWidth = Mathf.Max(obstacleVisualHalfWidth, visual.bounds.extents.x);
        }
        float physicsBasedDelay = duckContactDelay;
        float latestClearTime = 0f;
        if (action == ObstacleAction.Jump && runnerController != null &&
            runnerController.TryGetJumpClearanceWindow(obstacleTopY, GetNominalJumpMultiplier(),
                out float firstClearTime, out latestClearTime))
        {
            physicsBasedDelay = firstClearTime + jumpContactMargin + Mathf.Max(0f, obstacleArrivalDelay);
        }
        else if (action == ObstacleAction.Jump)
        {
            physicsBasedDelay = runnerController != null
                ? runnerController.GetRecommendedObstacleContactDelay(obstacleTopY, GetNominalJumpMultiplier()) : 0.38f;
        }

        // Contact follows the beat: the player must first have time to clear the obstacle.
        desiredContactTime = targetBeatTime + physicsBasedDelay;

        if (desiredContactTime - now < minimumTravelTime)
            desiredContactTime = now + minimumTravelTime;
        float actualContactDelay = (float)(desiredContactTime - targetBeatTime);

        // Match the actual collider edges rather than the obstacle centre; the old negative
        // padding caused physical contact before the indicated beat.
        float contactTargetX = runnerX + obstacleHalfWidth + runnerHalfWidth;
        float distance = spawnX - contactTargetX;
        if (distance <= 0.5f) distance = 5f;

        double timeToTarget = desiredContactTime - now;
        if (timeToTarget < minimumTravelTime)
            timeToTarget = minimumTravelTime;

        float speed = (float)(distance / timeToTarget);
        if (action == ObstacleAction.Jump && latestClearTime > actualContactDelay + jumpLandingMargin)
        {
            // Clear the painted rock as well as both physical colliders before landing.
            float crossingDistance = 2f * (runnerHalfWidth + obstacleVisualHalfWidth);
            float safeOverlapTime = latestClearTime - actualContactDelay - jumpLandingMargin;
            speed = Mathf.Max(speed, crossingDistance / safeOverlapTime);
        }

        // At slow BPM the safe speed places the obstacle farther off-screen at spawn.
        // Keep the beat/contact time unchanged rather than moving a rock faster into the player.
        obs.transform.position = new Vector3(contactTargetX + speed * (float)timeToTarget,
            obs.transform.position.y, obs.transform.position.z);

        mover.speed = speed;
        mover.runnerX = contactTargetX;
        mover.destroyPoint = destroyPoint;
        mover.targetBeatDspTime = targetBeatTime;
        lastObstacleSpeed = speed;
        activeObstacles.Add(mover);
        scheduledObstacles.Add(new ScheduledObstacle { mover = mover, beatTime = targetBeatTime, action = action });
    }

    public ObstacleAction GetActionForBeat(double beatTime)
    {
        for (int i = scheduledObstacles.Count - 1; i >= 0; i--)
        {
            ScheduledObstacle obstacle = scheduledObstacles[i];
            if (obstacle.mover == null)
            {
                scheduledObstacles.RemoveAt(i);
                continue;
            }

            if (System.Math.Abs(obstacle.beatTime - beatTime) < 0.03)
            {
                // A scheduled beat must not trigger an action for an off-screen
                // obstacle (for instance while a new tempo is being applied).
                if (runner == null)
                    return ObstacleAction.Tap;

                float distance = obstacle.mover.transform.position.x - runner.position.x;
                return distance >= -0.5f && distance <= actionVisibilityDistance
                    ? obstacle.action : ObstacleAction.Tap;
            }
        }

        return ObstacleAction.Tap;
    }

    public void MarkActionSuccessForBeat(double beatTime, ObstacleAction action)
    {
        for (int i = scheduledObstacles.Count - 1; i >= 0; i--)
        {
            ScheduledObstacle scheduled = scheduledObstacles[i];
            if (scheduled.action == action && scheduled.mover != null &&
                System.Math.Abs(scheduled.beatTime - beatTime) < 0.03)
            {
                scheduled.mover.avoidedOnBeat = true;
                return;
            }
        }
    }

    public void SetDifficulty(int stage)
    {
        difficultyStage = Mathf.Clamp(stage, 0, 3);
    }

    public float GetVisualScrollSpeed()
    {
        float nearestContact = float.PositiveInfinity;
        float speed = 0f;
        foreach (ObstacleMoverBeat obstacle in activeObstacles)
        {
            if (obstacle == null || obstacle.TimeToRunnerContact < 0f ||
                obstacle.TimeToRunnerContact >= nearestContact)
                continue;

            nearestContact = obstacle.TimeToRunnerContact;
            speed = obstacle.speed;
        }

        if (speed > 0f)
            return speed;

        // Keep the scenery moving between obstacles at approximately the same pace.
        float interval = beat != null && beat.IntervalSec > 0.01
            ? (float)beat.IntervalSec : 1.5f;
        float runnerX = runner != null ? runner.position.x : -4.5f;
        float estimatedSpeed = (spawnX - runnerX - 1f) / (Mathf.Max(1, beatsAhead) * interval + 0.4f);
        return Mathf.Max(Mathf.Clamp(estimatedSpeed, 2f, 12f), lastObstacleSpeed * 0.85f);
    }

    public void ShiftSchedule(double seconds)
    {
        if (seconds <= 0d) return;
        for (int i = 0; i < scheduledObstacles.Count; i++)
        {
            ScheduledObstacle item = scheduledObstacles[i];
            item.beatTime += seconds;
            if (item.mover != null)
                item.mover.targetBeatDspTime += seconds;
            scheduledObstacles[i] = item;
        }
    }

    // Preserve each obstacle's target beat when the metronome changes speed.
    public void RescheduleTempo(double anchorBeatTime, double oldInterval, double newInterval)
    {
        if (oldInterval <= 0.0 || newInterval <= 0.0)
            return;

        double now = AudioSettings.dspTime;
        for (int i = 0; i < scheduledObstacles.Count; i++)
        {
            ScheduledObstacle obstacle = scheduledObstacles[i];
            if (obstacle.mover == null || obstacle.beatTime <= anchorBeatTime + 0.001)
                continue;

            int beatsRemaining = Mathf.Max(1, Mathf.RoundToInt((float)((obstacle.beatTime - anchorBeatTime) / oldInterval)));
            double newBeatTime = anchorBeatTime + beatsRemaining * newInterval;
            double originalContactTime = now + obstacle.mover.TimeToRunnerContact;
            double revisedContactTime = originalContactTime + (newBeatTime - obstacle.beatTime);
            float distance = obstacle.mover.transform.position.x - obstacle.mover.runnerX;
            if (distance > 0f)
                obstacle.mover.speed = distance / Mathf.Max(minimumTravelTime, (float)(revisedContactTime - now));

            obstacle.beatTime = newBeatTime;
            obstacle.mover.targetBeatDspTime = newBeatTime;
            scheduledObstacles[i] = obstacle;
        }
    }

    public void StartSpawner()
    {
        gameStarted = true;
        beatCount = 0;
        obstacleCount = 0;
        difficultyStage = 0;
        lastObstacleSpeed = 0f;
    }

    public void StopSpawner()
    {
        gameStarted = false;
        foreach (ObstacleMoverBeat obstacle in activeObstacles)
        {
            if (obstacle != null)
                Destroy(obstacle.gameObject);
        }
        activeObstacles.Clear();
        scheduledObstacles.Clear();
        lastObstacleSpeed = 0f;
    }

    public float GetAdaptiveJumpMultiplier(float defaultMultiplier, float desiredTimeToContact, ObstacleAction action)
    {
        float nominal = GetNominalJumpMultiplier();
        ObstacleMoverBeat obstacle = GetNearestActiveObstacle(action);
        if (obstacle == null)
            return ClampJumpMultiplier(nominal * defaultMultiplier);

        float actualTimeToContact = obstacle.TimeToRunnerContact;
        if (float.IsInfinity(actualTimeToContact) || actualTimeToContact <= 0.02f)
            return ClampJumpMultiplier(nominal * defaultMultiplier);

        float ratio = desiredTimeToContact / actualTimeToContact;
        float correction = (ratio - 1f) * adaptiveJumpStrength;
        float result = Mathf.Clamp(defaultMultiplier + correction, minimumJumpMultiplier, maximumJumpMultiplier);
        return ClampJumpMultiplier(nominal * result);
    }

    public float GetDuckDuration()
    {
        ObstacleMoverBeat bird = GetNearestActiveObstacle(ObstacleAction.Duck);
        if (bird == null || bird.speed <= 0f)
            return 1.1f;

        float crossingTime = (2f * GetHalfWidth(runnerCollider) + 2f * GetHalfWidth(bird.GetComponent<Collider2D>())) / bird.speed;
        return Mathf.Clamp(bird.TimeToRunnerContact + crossingTime + 0.12f, 0.9f, 2.2f);
    }

    float GetNominalJumpMultiplier()
    {
        return ClampJumpMultiplier(logJumpMultiplier);
    }

    public float GetFreeJumpMultiplier()
    {
        return GetNominalJumpMultiplier();
    }

    float ClampJumpMultiplier(float multiplier)
    {
        return Mathf.Clamp(multiplier, 0.60f, 0.78f);
    }

    GameObject CreateFallbackObstacle(Vector3 position)
    {
        var obstacle = new GameObject("TempoObstacle");
        obstacle.tag = "Obstacle";
        obstacle.transform.position = position;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(obstacle.transform, false);
        visual.transform.localScale = new Vector3(fallbackObstacleSize.x, fallbackObstacleSize.y, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = GetFallbackSprite();
        renderer.color = fallbackObstacleColor;
        renderer.sortingOrder = 9;

        var collider = obstacle.AddComponent<BoxCollider2D>();
        collider.size = fallbackObstacleSize;

        return obstacle;
    }

    static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        fallbackSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            1f);
        return fallbackSprite;
    }

    float GetHalfWidth(Collider2D col)
    {
        if (col == null)
            return 0.5f;

        return col.bounds.extents.x;
    }

    float GetTopY(Collider2D col, float fallbackY)
    {
        if (col == null)
            return fallbackY + 0.5f;

        return col.bounds.max.y;
    }

    ObstacleMoverBeat GetNearestActiveObstacle(ObstacleAction action)
    {
        float bestTime = float.PositiveInfinity;
        ObstacleMoverBeat best = null;

        for (int i = scheduledObstacles.Count - 1; i >= 0; i--)
        {
            ScheduledObstacle scheduled = scheduledObstacles[i];
            if (scheduled.action != action)
                continue;

            ObstacleMoverBeat obstacle = scheduled.mover;
            if (obstacle == null)
            {
                scheduledObstacles.RemoveAt(i);
                continue;
            }

            float timeToContact = obstacle.TimeToRunnerContact;
            if (timeToContact < 0f)
            {
                continue;
            }

            if (timeToContact < bestTime)
            {
                bestTime = timeToContact;
                best = obstacle;
            }
        }

        return best;
    }
}
