using UnityEngine;

public class ObstacleMoverBeat : MonoBehaviour
{
    public float speed = 6f;
    public float runnerX = 3f;
    public Transform destroyPoint;
    [System.NonSerialized] public double targetBeatDspTime;
    [System.NonSerialized] public bool avoidedOnBeat;
    [System.NonSerialized] public bool penaltyApplied;

    SpriteRenderer animationRenderer;
    Sprite[] animationFrames;
    float animationFramesPerSecond;
    float animationStartedAt;

    public void ConfigureAnimation(SpriteRenderer renderer, Sprite[] frames, float framesPerSecond)
    {
        animationRenderer = renderer;
        animationFrames = frames;
        animationFramesPerSecond = Mathf.Max(1f, framesPerSecond);
        animationStartedAt = Time.time;
        if (animationRenderer != null && animationFrames != null && animationFrames.Length > 0)
            animationRenderer.sprite = animationFrames[0];
    }

    public float TimeToRunnerContact
    {
        get
        {
            if (speed <= 0.001f)
                return float.PositiveInfinity;

            return (transform.position.x - runnerX) / speed;
        }
    }

    void Update()
    {
        if (animationRenderer != null && animationFrames != null && animationFrames.Length > 0)
        {
            int frame = Mathf.FloorToInt((Time.time - animationStartedAt) * animationFramesPerSecond) % animationFrames.Length;
            animationRenderer.sprite = animationFrames[frame];
        }

        transform.Translate(Vector3.left * speed * Time.deltaTime);

        if (destroyPoint != null && transform.position.x < destroyPoint.position.x)
            Destroy(gameObject);
    }
}
