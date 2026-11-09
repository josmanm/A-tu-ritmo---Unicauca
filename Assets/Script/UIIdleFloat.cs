using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIIdleFloat : MonoBehaviour
{
    [SerializeField] private float floatAmount = 8f;
    [SerializeField] private float floatSpeed = 0.8f;
    [SerializeField] private float wobbleAngle = 1f;
    [SerializeField] private float wobbleSpeed = 0.7f;

    private RectTransform rect;
    private Vector2 basePosition;
    private Quaternion baseRotation;

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
        baseRotation = rect.localRotation;
    }

    private void LateUpdate()
    {
        float time = Time.unscaledTime;
        rect.anchoredPosition = basePosition + Vector2.up * (Mathf.Sin(time * floatSpeed) * floatAmount);
        rect.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(time * wobbleSpeed) * wobbleAngle);
    }

    private void OnDisable()
    {
        if (rect == null) return;
        rect.anchoredPosition = basePosition;
        rect.localRotation = baseRotation;
    }
}
