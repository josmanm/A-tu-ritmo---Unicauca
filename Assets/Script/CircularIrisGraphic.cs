using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CircularIrisGraphic : MaskableGraphic
{
    [SerializeField] [Range(0f, 2f)] private float innerRadiusNormalized = 1.2f;
    [SerializeField] [Range(12, 128)] private int segments = 64;

    public float InnerRadiusNormalized
    {
        get => innerRadiusNormalized;
        set
        {
            float clamped = Mathf.Clamp(value, 0f, 2f);
            if (Mathf.Approximately(innerRadiusNormalized, clamped))
                return;

            innerRadiusNormalized = clamped;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float maxRadius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.75f;
        float innerRadius = maxRadius * innerRadiusNormalized;
        float outerRadius = maxRadius * 2.5f;
        Vector2 center = rect.center;
        Color32 color32 = color;
        int safeSegments = Mathf.Max(12, segments);

        for (int i = 0; i < safeSegments; i++)
        {
            float t0 = (float)i / safeSegments * Mathf.PI * 2f;
            float t1 = (float)(i + 1) / safeSegments * Mathf.PI * 2f;

            Vector2 inner0 = center + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * innerRadius;
            Vector2 inner1 = center + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * innerRadius;
            Vector2 outer0 = center + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * outerRadius;
            Vector2 outer1 = center + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * outerRadius;

            int startIndex = vh.currentVertCount;
            vh.AddVert(outer0, color32, Vector2.zero);
            vh.AddVert(outer1, color32, Vector2.zero);
            vh.AddVert(inner1, color32, Vector2.zero);
            vh.AddVert(inner0, color32, Vector2.zero);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }
}
