using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class MonitorWaveformGraphic : MaskableGraphic
{
    private const int SegmentCount = 96;
    private const float LineThickness = 2.5f;

    private float heartRate = 60f;
    private float phase;

    public void SetHeartRate(int beatsPerMinute)
    {
        heartRate = Mathf.Clamp(beatsPerMinute, 30f, 220f);
    }

    private void Update()
    {
        phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * heartRate / 60f, 1f);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect bounds = rectTransform.rect;
        if (bounds.width <= 0f || bounds.height <= 0f)
        {
            return;
        }

        Vector2 previous = GetPoint(0, bounds);
        for (int index = 1; index <= SegmentCount; index++)
        {
            Vector2 current = GetPoint(index, bounds);
            AddLineSegment(vertexHelper, previous, current);
            previous = current;
        }
    }

    private Vector2 GetPoint(int index, Rect bounds)
    {
        float normalizedX = index / (float)SegmentCount;
        float pulsePosition = Mathf.Repeat(normalizedX * 2.4f + phase, 1f);
        float signal = Mathf.Sin((normalizedX * 16f + phase) * Mathf.PI * 2f) * 0.025f;

        if (pulsePosition >= 0.30f && pulsePosition < 0.36f)
        {
            signal += Mathf.Lerp(0f, 0.15f, Mathf.InverseLerp(0.30f, 0.36f, pulsePosition));
        }
        else if (pulsePosition < 0.40f && pulsePosition >= 0.36f)
        {
            signal += Mathf.Lerp(0.15f, -0.22f, Mathf.InverseLerp(0.36f, 0.40f, pulsePosition));
        }
        else if (pulsePosition < 0.445f && pulsePosition >= 0.40f)
        {
            signal += Mathf.Lerp(-0.22f, 0.95f, Mathf.InverseLerp(0.40f, 0.445f, pulsePosition));
        }
        else if (pulsePosition < 0.49f && pulsePosition >= 0.445f)
        {
            signal += Mathf.Lerp(0.95f, -0.30f, Mathf.InverseLerp(0.445f, 0.49f, pulsePosition));
        }
        else if (pulsePosition < 0.56f && pulsePosition >= 0.49f)
        {
            signal += Mathf.Lerp(-0.30f, 0f, Mathf.InverseLerp(0.49f, 0.56f, pulsePosition));
        }
        else if (pulsePosition < 0.74f && pulsePosition >= 0.62f)
        {
            signal += Mathf.Sin(Mathf.InverseLerp(0.62f, 0.74f, pulsePosition) * Mathf.PI) * 0.18f;
        }

        return new Vector2(
            Mathf.Lerp(bounds.xMin, bounds.xMax, normalizedX),
            bounds.center.y + signal * bounds.height * 0.42f);
    }

    private void AddLineSegment(VertexHelper vertexHelper, Vector2 start, Vector2 end)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * LineThickness * 0.5f;
        int vertexIndex = vertexHelper.currentVertCount;

        vertexHelper.AddVert(start - normal, color, Vector2.zero);
        vertexHelper.AddVert(start + normal, color, Vector2.zero);
        vertexHelper.AddVert(end + normal, color, Vector2.zero);
        vertexHelper.AddVert(end - normal, color, Vector2.zero);
        vertexHelper.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
        vertexHelper.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
    }
}
