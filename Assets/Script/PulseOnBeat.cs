using UnityEngine;

public class PulseOnBeat : MonoBehaviour
{
    public BeatController beatController;
    public float scaleUp = 1.12f;
    public float speed = 10f;

    Vector3 baseScale;
    float target = 1f;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        if (beatController != null)
            beatController.OnBeat += HandleBeat;
    }

    void OnDisable()
    {
        if (beatController != null)
            beatController.OnBeat -= HandleBeat;
        target = 1f;
        transform.localScale = baseScale;
    }

    void HandleBeat(double beatTime)
    {
        target = scaleUp;
    }

    void Update()
    {
        target = Mathf.Lerp(target, 1f, Time.deltaTime * speed);
        transform.localScale = baseScale * target;
    }
}
