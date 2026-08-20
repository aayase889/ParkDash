using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight, deterministic sparkle halo for the completed objective icons.
/// It uses one UI mesh and unscaled time so the paused Win UI stays animated.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VictoryUiSparklesGraphic : Graphic
{
    private bool playing;
    private float startedAt;

    public void Play()
    {
        startedAt = Time.unscaledTime;
        playing = true;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void Stop()
    {
        playing = false;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (playing) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!playing) return;

        float time = Time.unscaledTime - startedAt - 0.62f;
        if (time < 0f) return;
        float arrival = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.24f, time));
        const int sparkleCount = 22;
        for (int index = 0; index < sparkleCount; index++)
        {
            float phase = Mathf.Repeat(time * (0.58f + index % 4 * 0.08f)
                + Hash01(index * 71 + 13), 1f);
            float bell = Mathf.Sin(phase * Mathf.PI);
            bell *= bell;
            if (bell < 0.035f) continue;

            float angle = Hash01(index * 113 + 29) * Mathf.PI * 2f;
            float radius = Mathf.Lerp(128f, 305f, Hash01(index * 173 + 47));
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            position.y *= 0.54f;
            float size = Mathf.Lerp(9f, 22f, Hash01(index * 211 + 61)) * bell;
            Color color = index % 4 == 0
                ? new Color(1f, 0.72f, 0.10f, bell * arrival * 0.82f)
                : new Color(1f, 1f, 0.90f, bell * arrival * 0.86f);
            AddStar(vertexHelper, position, size, Mathf.Max(2.2f, size * 0.17f), color);
        }
    }

    private static void AddStar(
        VertexHelper vertexHelper,
        Vector2 center,
        float length,
        float width,
        Color color)
    {
        AddQuad(vertexHelper, center, new Vector2(length, width), 0f, color);
        Color vertical = color;
        vertical.a *= 0.82f;
        AddQuad(vertexHelper, center, new Vector2(length * 0.72f, width), 90f, vertical);
    }

    private static void AddQuad(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 size,
        float degrees,
        Color color)
    {
        float radians = degrees * Mathf.Deg2Rad;
        Vector2 right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * size.x * 0.5f;
        Vector2 up = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * size.y * 0.5f;
        int start = vertexHelper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center - right - up;
        vertexHelper.AddVert(vertex);
        vertex.position = center - right + up;
        vertexHelper.AddVert(vertex);
        vertex.position = center + right + up;
        vertexHelper.AddVert(vertex);
        vertex.position = center + right - up;
        vertexHelper.AddVert(vertex);
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start, start + 2, start + 3);
    }

    private static float Hash01(int value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);
    }
}
