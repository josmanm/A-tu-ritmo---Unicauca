using UnityEngine;

public class RunnerController2D : MonoBehaviour
{
    [Header("Jump")]
    public float jumpForce = 10f;
    [Range(0.75f, 1.25f)] public float risingGravityMultiplier = 0.9f;
    [Range(0.75f, 1.5f)] public float fallingGravityMultiplier = 1.05f;
    public float obstacleClearanceMargin = 0.12f;
    [Range(0f, 0.4f)] public float preferredClearPhase = 0.2f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Hit Response")]
    public float invulnerabilitySeconds = 1.2f;
    public float blinkInterval = 0.08f;

    [Header("Tempo Tap art (optional)")]
    public Sprite idleSprite;
    public Sprite runSprite;
    public Sprite jumpSprite;
    public Sprite slideSprite;

    [Header("Tempo Tap animation frames (optional)")]
    public Sprite[] runFrames;
    public Sprite[] jumpFrames;
    public Sprite[] slideFrames;
    [Min(1f)] public float runFramesPerSecond = 10f;
    [Min(0.01f)] public float poseScale = 1f;
    public Vector2 poseOffset = new Vector2(0f, -0.81f);

    Rigidbody2D rb;
    bool isGrounded;
    Animator anim;
    SpriteRenderer spriteRenderer;
    SpriteRenderer duckRenderer;
    SpriteRenderer poseRenderer;
    Collider2D bodyCollider;
    CapsuleCollider2D capsuleCollider;
    Vector2 standingColliderSize;
    Vector2 standingColliderOffset;
    float duckUntil;
    float baseGravityScale;
    float invulnerabilityTimer;
    float blinkTimer;
    bool blinkVisible = true;
    float jumpStartedAt;
    float jumpDuration;
    float landingStartedAt = -1f;
    float duckStartedAt;
    bool wasAirborne;

    bool gameStarted = false;

    public bool IsGrounded => isGrounded;
    public bool GameStarted => gameStarted;
    public bool IsInvulnerable => invulnerabilityTimer > 0f;
    public bool IsDucking => duckUntil > Time.time;

    void Start()
    {
        SetGameStarted(false);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && (idleSprite != null || runSprite != null || HasFrames(runFrames)))
        {
            Transform existingPose = transform.Find("RunnerPose");
            GameObject visual = existingPose != null ? existingPose.gameObject : new GameObject("RunnerPose");
            if (existingPose == null)
                visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(poseOffset.x, poseOffset.y, 0f);
            visual.transform.localScale = Vector3.one * poseScale;
            poseRenderer = visual.GetComponent<SpriteRenderer>();
            if (poseRenderer == null)
                poseRenderer = visual.AddComponent<SpriteRenderer>();
            poseRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            poseRenderer.sortingOrder = spriteRenderer.sortingOrder;
            poseRenderer.sprite = idleSprite != null ? idleSprite : HasFrames(runFrames) ? runFrames[0] : runSprite;
            spriteRenderer.enabled = false;
        }
        else if (spriteRenderer != null)
        {
            GameObject visual = new GameObject("DuckVisual");
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(1f, 0.52f, 1f);
            visual.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            duckRenderer = visual.AddComponent<SpriteRenderer>();
            duckRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            duckRenderer.sortingOrder = spriteRenderer.sortingOrder;
            visual.SetActive(false);
        }
        bodyCollider = GetComponent<Collider2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        if (capsuleCollider != null)
        {
            standingColliderSize = capsuleCollider.size;
            standingColliderOffset = capsuleCollider.offset;
        }

        if (rb != null)
            baseGravityScale = rb.gravityScale;
    }

    void Update()
    {
        UpdateGroundedState();
        UpdateGravityScale();
        UpdateInvulnerabilityVisual();
        if (duckUntil > 0f && Time.time >= duckUntil)
            RestoreStandingCollider();

        if (gameStarted && wasAirborne && isGrounded)
            landingStartedAt = Time.time;
        wasAirborne = gameStarted && !isGrounded;

        if (poseRenderer != null)
            poseRenderer.sprite = GetCurrentPose();

        if (!gameStarted) return;

        // Detectar si est� tocando el suelo
        if (anim != null)
            anim.SetBool("IsJumping", !isGrounded);
    }

    public bool Jump(float multiplier = 1f)
    {
        // The first tap can arrive before Update has refreshed the ground probe.
        if (gameStarted && !isGrounded)
            UpdateGroundedState();
        if (!gameStarted || !isGrounded || IsDucking || rb == null) return false;

        float jumpVelocity = jumpForce * multiplier;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
        isGrounded = false;
        wasAirborne = true;
        jumpStartedAt = Time.time;
        float upwardGravity = Mathf.Max(0.01f, baseGravityScale * Mathf.Abs(Physics2D.gravity.y) * risingGravityMultiplier);
        float downwardGravity = Mathf.Max(0.01f, baseGravityScale * Mathf.Abs(Physics2D.gravity.y) * fallingGravityMultiplier);
        jumpDuration = jumpVelocity / upwardGravity + jumpVelocity / Mathf.Sqrt(upwardGravity * downwardGravity);
        landingStartedAt = -1f;
        if (poseRenderer != null)
            poseRenderer.sprite = HasFrames(jumpFrames) ? jumpFrames[0] : jumpSprite;
        return true;
    }

    public bool Duck(float seconds = 0.9f)
    {
        if (!gameStarted || !isGrounded || capsuleCollider == null)
            return false;

        if (!IsDucking)
            duckStartedAt = Time.time;
        duckUntil = Mathf.Max(duckUntil, Time.time + seconds);
        capsuleCollider.size = new Vector2(standingColliderSize.x, standingColliderSize.y * 0.52f);
        capsuleCollider.offset = standingColliderOffset - Vector2.up * (standingColliderSize.y * 0.24f);
        if (duckRenderer != null)
        {
            duckRenderer.sprite = spriteRenderer.sprite;
            duckRenderer.color = spriteRenderer.color;
            duckRenderer.gameObject.SetActive(true);
            spriteRenderer.enabled = false;
        }
        return true;
    }

    private void RestoreStandingCollider()
    {
        duckUntil = 0f;
        if (capsuleCollider != null)
        {
            capsuleCollider.size = standingColliderSize;
            capsuleCollider.offset = standingColliderOffset;
        }
        if (duckRenderer != null)
        {
            duckRenderer.gameObject.SetActive(false);
            spriteRenderer.enabled = true;
        }
    }

    public void TriggerHitInvulnerability()
    {
        invulnerabilityTimer = invulnerabilitySeconds;
        blinkTimer = 0f;
        blinkVisible = false;
        ApplyBlinkState();
    }

    public bool TryGetJumpClearanceWindow(float obstacleTopY, float jumpMultiplier,
        out float clearOnAscent, out float clearOnDescent)
    {
        float worldGravity = Mathf.Abs(Physics2D.gravity.y);
        float upwardsGravity = Mathf.Max(0.01f, baseGravityScale * worldGravity * risingGravityMultiplier);
        float downwardsGravity = Mathf.Max(0.01f, baseGravityScale * worldGravity * fallingGravityMultiplier);
        float initialVelocity = jumpForce * jumpMultiplier;

        float runnerBottomY = GetRunnerBottomY();
        float requiredHeight = obstacleTopY + obstacleClearanceMargin - runnerBottomY;

        float apexTime = initialVelocity / upwardsGravity;
        float apexHeight = (initialVelocity * apexTime) - (0.5f * upwardsGravity * apexTime * apexTime);

        if (requiredHeight <= 0f)
        {
            clearOnAscent = 0f;
            clearOnDescent = apexTime + initialVelocity / Mathf.Sqrt(upwardsGravity * downwardsGravity);
            return true;
        }

        if (apexHeight <= requiredHeight)
        {
            clearOnAscent = clearOnDescent = apexTime;
            return false;
        }

        clearOnAscent = (initialVelocity - Mathf.Sqrt(Mathf.Max(0f,
            initialVelocity * initialVelocity - 2f * upwardsGravity * requiredHeight))) / upwardsGravity;
        clearOnDescent = apexTime + Mathf.Sqrt(2f * (apexHeight - requiredHeight) / downwardsGravity);
        return true;
    }

    public float GetRecommendedObstacleContactDelay(float obstacleTopY, float jumpMultiplier = 1f)
    {
        TryGetJumpClearanceWindow(obstacleTopY, jumpMultiplier, out float ascent, out float descent);
        return Mathf.Clamp(Mathf.Lerp(ascent, descent, preferredClearPhase), 0.16f, 0.52f);
    }

    // Para ver el circulito en la escena
    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
    public void SetGameStarted(bool started)
    {
        gameStarted = started;

        if (rb != null)
        {
            rb.simulated = started;
            rb.gravityScale = baseGravityScale;
            if (!started)
                rb.linearVelocity = Vector2.zero;
        }

        if (anim != null)
            anim.enabled = started;

        if (!started)
        {
            RestoreStandingCollider();
            isGrounded = false;
            wasAirborne = false;
            landingStartedAt = -1f;
            invulnerabilityTimer = 0f;
            blinkVisible = true;
            ApplyBlinkState();
        }
    }

    void UpdateGroundedState()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        // The foot probe can still overlap the ground for a frame after an impulse.
        // Rising takes precedence so a valid beat does not display the running pose.
        isGrounded = (rb == null || rb.linearVelocity.y <= 0.05f) &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    void UpdateGravityScale()
    {
        if (rb == null)
            return;

        if (!gameStarted)
        {
            rb.gravityScale = baseGravityScale;
            return;
        }

        if (isGrounded)
        {
            rb.gravityScale = baseGravityScale;
            return;
        }

        float gravityMultiplier = rb.linearVelocity.y >= 0f
            ? risingGravityMultiplier
            : fallingGravityMultiplier;

        rb.gravityScale = baseGravityScale * gravityMultiplier;
    }

    void UpdateInvulnerabilityVisual()
    {
        if (invulnerabilityTimer <= 0f)
        {
            if (!blinkVisible)
            {
                blinkVisible = true;
                ApplyBlinkState();
            }

            return;
        }

        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);
        blinkTimer -= Time.deltaTime;

        if (blinkTimer > 0f)
            return;

        blinkTimer = blinkInterval;
        blinkVisible = !blinkVisible;
        ApplyBlinkState();
    }

    void ApplyBlinkState()
    {
        if (spriteRenderer == null)
            return;

        Color color = spriteRenderer.color;
        color.a = blinkVisible ? 1f : 0.35f;
        spriteRenderer.color = color;
        if (duckRenderer != null) duckRenderer.color = color;
        if (poseRenderer != null) poseRenderer.color = color;
    }

    static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;

    Sprite GetCurrentPose()
    {
        if (!gameStarted)
            return idleSprite != null ? idleSprite : HasFrames(runFrames) ? runFrames[0] : runSprite;

        if (IsDucking)
        {
            if (HasFrames(slideFrames))
            {
                float elapsed = Time.time - duckStartedAt;
                float remaining = duckUntil - Time.time;
                int middle = slideFrames.Length / 2;
                int frame = elapsed < 0.18f ? Mathf.Min(middle, Mathf.FloorToInt(elapsed / 0.045f))
                    : remaining < 0.18f ? Mathf.Clamp(slideFrames.Length - 1 - Mathf.FloorToInt(remaining / 0.045f), middle, slideFrames.Length - 1)
                    : middle;
                return slideFrames[Mathf.Clamp(frame, 0, slideFrames.Length - 1)];
            }
            return slideSprite;
        }

        if (gameStarted && !isGrounded)
        {
            if (HasFrames(jumpFrames))
            {
                float progress = Mathf.Clamp01((Time.time - jumpStartedAt) / Mathf.Max(0.01f, jumpDuration));
                int frame = Mathf.FloorToInt(progress * jumpFrames.Length);
                return jumpFrames[Mathf.Clamp(frame, 0, jumpFrames.Length - 1)];
            }
            return jumpSprite;
        }

        if (landingStartedAt >= 0f && HasFrames(jumpFrames))
        {
            float elapsed = Time.time - landingStartedAt;
            if (elapsed < 0.16f)
                return jumpFrames[elapsed < 0.08f ? Mathf.Max(0, jumpFrames.Length - 2) : jumpFrames.Length - 1];
            landingStartedAt = -1f;
        }

        if (HasFrames(runFrames))
            return runFrames[Mathf.FloorToInt(Time.time * runFramesPerSecond) % runFrames.Length];
        return runSprite;
    }

    float GetRunnerBottomY()
    {
        if (bodyCollider != null)
            return bodyCollider.bounds.min.y;

        return transform.position.y - 0.75f;
    }
}
