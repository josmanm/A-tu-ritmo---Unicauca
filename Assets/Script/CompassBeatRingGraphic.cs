using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CompassBeatRingGraphic : MaskableGraphic
{
    [SerializeField] [Range(0.04f, 0.35f)] private float thickness = 0.2f;
    [SerializeField] [Range(24, 120)] private int segments = 72;
    [SerializeField] [Range(1f, 2f)] private float glowMultiplier = 1.35f;
    [SerializeField] [Range(0.01f, 0.12f)] private float outlineThickness = 0.04f;
    [SerializeField] private Color outlineColor = new(0f, 0f, 0f, 0.9f);
    [SerializeField] private Color faceColor = new(1f, 1f, 1f, 0.98f);

    private float glowAmount;

    public void SetColor(Color color)
    {
        this.color = color;
        SetVerticesDirty();
    }

    public void SetGlow(float normalizedGlow)
    {
        glowAmount = Mathf.Clamp01(normalizedGlow);
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerRadius = outerRadius * (1f - thickness);
        Vector2 center = rect.center;
        int safeSegments = Mathf.Max(24, segments);
        float innerOutlineInnerRadius = outerRadius * (1f - thickness - outlineThickness);
        float innerOutlineOuterRadius = innerRadius;
        float outerOutlineInnerRadius = outerRadius;
        float outerOutlineOuterRadius = outerRadius + (Mathf.Min(rect.width, rect.height) * outlineThickness);

        DrawDisc(vh, center, innerOutlineInnerRadius, faceColor, safeSegments);
        DrawRing(vh, center, innerOutlineInnerRadius, innerOutlineOuterRadius, outlineColor, safeSegments);
        DrawRing(vh, center, outerOutlineInnerRadius, outerOutlineOuterRadius, outlineColor, safeSegments);

        Color drawColor = Color.Lerp(color, Color.white, glowAmount * 0.45f);
        drawColor.r = Mathf.Clamp01(drawColor.r * Mathf.Lerp(1f, glowMultiplier, glowAmount));
        drawColor.g = Mathf.Clamp01(drawColor.g * Mathf.Lerp(1f, glowMultiplier, glowAmount));
        drawColor.b = Mathf.Clamp01(drawColor.b * Mathf.Lerp(1f, glowMultiplier, glowAmount));
        DrawRing(vh, center, innerRadius, outerRadius, drawColor, safeSegments);
    }

    private static void DrawDisc(VertexHelper vh, Vector2 center, float radius, Color color, int count)
    {
        int first = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
        }
    }

    private void DrawRing(VertexHelper vh, Vector2 center, float innerRadius, float outerRadius, Color color, int safeSegments)
    {
        Color32 c = color;
        for (int i = 0; i < safeSegments; i++)
        {
            float t0 = (float)i / safeSegments * Mathf.PI * 2f;
            float t1 = (float)(i + 1) / safeSegments * Mathf.PI * 2f;

            Vector2 outer0 = center + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * outerRadius;
            Vector2 outer1 = center + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * outerRadius;
            Vector2 inner0 = center + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * innerRadius;
            Vector2 inner1 = center + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * innerRadius;

            int start = vh.currentVertCount;
            vh.AddVert(outer0, c, Vector2.zero);
            vh.AddVert(outer1, c, Vector2.zero);
            vh.AddVert(inner1, c, Vector2.zero);
            vh.AddVert(inner0, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
