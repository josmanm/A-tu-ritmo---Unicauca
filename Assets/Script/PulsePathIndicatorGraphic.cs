using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class PulsePathIndicatorGraphic : MaskableGraphic
{
    [SerializeField] [Range(0f, 1f)] private float progress;
    [SerializeField] private Color markerColor = new(1f, 0.83f, 0.54f, 1f);
    [SerializeField] [Range(0.05f, 0.4f)] private float flashDuration = 0.16f;

    private float flashTimer;
    private bool pointerVisible;

    public void SetPointerVisible(bool visible)
    {
        if (pointerVisible == visible) return;
        pointerVisible = visible;
        SetVerticesDirty();
    }

    public void SetProgress(float value)
    {
        float next = Mathf.Clamp01(value);
        if (Mathf.Approximately(progress, next)) return;
        progress = next;
        SetVerticesDirty();
    }

    public void TriggerFlash()
    {
        flashTimer = flashDuration;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        if (flashTimer <= 0f) return;
        flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.39f;
        float thickness = Mathf.Max(8f, radius * 0.13f);

        // Una vuelta = un pulso. La estrella arriba es el lugar donde se toca.
        DrawArc(vh, center, radius, thickness, 1f, new Color(0.09f, 0.24f, 0.34f, 0.95f));
        if (pointerVisible)
            DrawArc(vh, center, radius, thickness, progress, new Color(0.35f, 0.91f, 0.74f, 1f));

        Vector2 target = center + Vector2.up * radius;
        DrawDot(vh, target, thickness * 1.4f, new Color(0.09f, 0.24f, 0.34f), 24);
        DrawStar(vh, target, thickness * 1.13f, flashTimer > 0f ? Color.white : markerColor);

        if (!pointerVisible) return;
        float angle = (90f - progress * 360f) * Mathf.Deg2Rad;
        Vector2 pointer = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        DrawDot(vh, pointer, thickness * 0.9f, new Color(0.09f, 0.24f, 0.34f), 24);
        DrawDot(vh, pointer, thickness * 0.58f, flashTimer > 0f ? Color.white : markerColor, 24);
    }

    private static void DrawArc(VertexHelper vh, Vector2 center, float radius, float thickness, float fraction, Color color)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(96f * Mathf.Clamp01(fraction)));
        for (int i = 0; i < steps; i++)
        {
            float start = (90f - 360f * (float)i / 96f) * Mathf.Deg2Rad;
            float end = (90f - 360f * Mathf.Min(fraction, (float)(i + 1) / 96f)) * Mathf.Deg2Rad;
            Vector2 direction0 = new(Mathf.Cos(start), Mathf.Sin(start));
            Vector2 direction1 = new(Mathf.Cos(end), Mathf.Sin(end));
            int index = vh.currentVertCount;
            vh.AddVert(center + direction0 * (radius + thickness * 0.5f), color, Vector2.zero);
            vh.AddVert(center + direction1 * (radius + thickness * 0.5f), color, Vector2.zero);
            vh.AddVert(center + direction1 * (radius - thickness * 0.5f), color, Vector2.zero);
            vh.AddVert(center + direction0 * (radius - thickness * 0.5f), color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }

    private static void DrawStar(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        int index = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= 10; i++)
        {
            float angle = (90f + i * 36f) * Mathf.Deg2Rad;
            float vertexRadius = i % 2 == 0 ? radius : radius * 0.48f;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * vertexRadius, color, Vector2.zero);
            if (i > 0) vh.AddTriangle(index, index + i, index + i + 1);
        }
    }

    private static void DrawDot(VertexHelper vh, Vector2 center, float radius, Color color, int steps)
    {
        int index = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= steps; i++)
        {
            float angle = i * Mathf.PI * 2f / steps;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            if (i > 0) vh.AddTriangle(index, index + i, index + i + 1);
        }
    }

}
