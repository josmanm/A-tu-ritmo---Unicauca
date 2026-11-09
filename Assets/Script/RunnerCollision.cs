using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RunnerCollision : MonoBehaviour
{
    public TempoTapGameManager game;
    public float stabilityPenalty = 0.2f;

    RunnerController2D runner;
    readonly HashSet<ObstacleMoverBeat> pendingCollisions = new HashSet<ObstacleMoverBeat>();

    void Awake()
    {
        runner = GetComponent<RunnerController2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleObstacle(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleObstacle(other);
    }

    void HandleObstacle(Collider2D other)
    {
        if (!IsObstacle(other))
            return;

        ObstacleMoverBeat mover = other.GetComponentInParent<ObstacleMoverBeat>();
        if (mover != null && (mover.avoidedOnBeat || mover.penaltyApplied || pendingCollisions.Contains(mover)))
            return;

        if (runner != null && runner.IsInvulnerable)
        {
            if (mover != null) mover.penaltyApplied = true;
            Destroy(other.gameObject);
            return;
        }

        // A valid late beat can follow the first physical overlap. Wait until the
        // acceptance window closes before treating an unprotected rock as a hit.
        double now = AudioSettings.dspTime;
        if (mover != null && mover.targetBeatDspTime > 0d && game != null)
        {
            double deadline = System.Math.Min(mover.targetBeatDspTime +
                    Mathf.Max(game.CurrentGoodMs, game.CurrentAssistMs) * 0.001 + 0.04,
                now + 0.4);
            if (now < deadline)
            {
                pendingCollisions.Add(mover);
                StartCoroutine(ResolveAfterBeatWindow(other, mover, deadline));
                return;
            }
        }

        RegisterHit(other);
    }

    IEnumerator ResolveAfterBeatWindow(Collider2D other, ObstacleMoverBeat mover, double deadline)
    {
        while (AudioSettings.dspTime < deadline && other != null)
            yield return null;

        pendingCollisions.Remove(mover);
        if (other == null || mover == null || mover.avoidedOnBeat || mover.penaltyApplied)
            yield break;

        if (runner != null && runner.IsInvulnerable)
        {
            mover.penaltyApplied = true;
            Destroy(other.gameObject);
            yield break;
        }

        RegisterHit(other);
    }

    void RegisterHit(Collider2D other)
    {
        ObstacleMoverBeat mover = other.GetComponentInParent<ObstacleMoverBeat>();
        if (mover != null)
        {
            if (mover.avoidedOnBeat || mover.penaltyApplied)
                return;
            mover.penaltyApplied = true;
        }

        if (game != null)
            game.RegisterCollision(stabilityPenalty);

        if (runner != null)
            runner.TriggerHitInvulnerability();

        Destroy(other.gameObject);
    }

    public void RegisterMissedObstacle(ObstacleMoverBeat mover)
    {
        if (mover == null || mover.avoidedOnBeat || mover.penaltyApplied)
            return;

        if (runner != null && runner.IsInvulnerable)
        {
            mover.penaltyApplied = true;
            Destroy(mover.gameObject);
            return;
        }

        Collider2D collider = mover.GetComponent<Collider2D>();
        if (collider != null)
            RegisterHit(collider);
    }

    bool IsObstacle(Collider2D other)
    {
        if (other == null)
            return false;

        return other.CompareTag("Obstacle") || other.GetComponentInParent<ObstacleMoverBeat>() != null;
    }
}
