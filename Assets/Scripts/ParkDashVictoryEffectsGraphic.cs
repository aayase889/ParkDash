using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Batched premium victory effects for the Park Dash logo interstitial.
/// The firework simulation is deterministic, uses unscaled time, and renders
/// every rocket, shell, curved spark trail, ember, glint, and confetti chip in
/// two CanvasRenderer batches instead of spawning short-lived GameObjects.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ParkDashVictoryEffectsGraphic : Graphic
{
    private const int AtlasCellSize = 64;
    private const int AtlasCellCount = 3;

    private struct BurstSpec
    {
        public float time;
        public Vector2 center;
        public float radius;
        public int sparks;
        public int palette;
        public int style;
        public int seed;

        public BurstSpec(
            float time,
            Vector2 center,
            float radius,
            int sparks,
            int palette,
            int style,
            int seed)
        {
            this.time = time;
            this.center = center;
            this.radius = radius;
            this.sparks = sparks;
            this.palette = palette;
            this.style = style;
            this.seed = seed;
        }
    }

    // Shells deliberately vary in size, height, cadence, and firework family.
    // 0 = chrysanthemum, 1 = peony, 2 = long-drooping willow.
    private static readonly BurstSpec[] Bursts =
    {
        new BurstSpec(1.72f, new Vector2(350f, 650f), 475f, 82, 1, 0, 17),
        new BurstSpec(1.91f, new Vector2(-380f, 500f), 455f, 78, 0, 1, 31),
        new BurstSpec(2.10f, new Vector2(15f, 870f), 500f, 92, 3, 0, 47),
        new BurstSpec(2.34f, new Vector2(-125f, 80f), 390f, 70, 2, 1, 59),
        new BurstSpec(2.58f, new Vector2(430f, 345f), 450f, 80, 1, 2, 71),
        new BurstSpec(2.82f, new Vector2(-420f, 780f), 485f, 86, 2, 2, 89),
        new BurstSpec(3.05f, new Vector2(95f, 690f), 520f, 96, 0, 2, 101),
        new BurstSpec(3.28f, new Vector2(405f, -85f), 390f, 72, 3, 1, 127)
    };

    // One coherent color family per shell reads more like a real firework than
    // alternating every spoke through a rainbow palette.
    private static readonly Color[] ShellPrimary =
    {
        new Color(1f, 0.58f, 0.055f, 1f),
        new Color(0.18f, 0.53f, 1f, 1f),
        new Color(0.72f, 0.34f, 1f, 1f),
        new Color(1f, 0.91f, 0.56f, 1f)
    };

    private static readonly Color[] ShellSecondary =
    {
        new Color(1f, 0.90f, 0.48f, 1f),
        new Color(0.63f, 0.88f, 1f, 1f),
        new Color(1f, 0.56f, 0.86f, 1f),
        new Color(1f, 1f, 1f, 1f)
    };

    private static readonly Color[] ReferenceSparkPalette =
    {
        new Color(1f, 0.68f, 0.12f, 1f),
        new Color(0.17f, 0.38f, 1f, 1f),
        new Color(0.70f, 0.52f, 1f, 1f),
        new Color(1f, 0.87f, 0.70f, 1f),
        new Color(0.47f, 0.78f, 1f, 1f)
    };

    private static Texture2D effectAtlas;
    private bool foregroundLayer;
    private bool playing;
    private float startedAt;
    private float totalDuration = 4f;

    public bool IsPlaying => playing;

    public override Texture mainTexture
    {
        get
        {
            EnsureEffectAtlas();
            return effectAtlas != null ? effectAtlas : Texture2D.whiteTexture;
        }
    }

    public void Initialize(bool foreground)
    {
        foregroundLayer = foreground;
        raycastTarget = false;
        color = Color.white;
        EnsureEffectAtlas();
        SetVerticesDirty();
        SetMaterialDirty();
    }

    public void Play(float duration)
    {
        totalDuration = Mathf.Max(0.1f, duration);
        startedAt = Time.unscaledTime;
        playing = true;
        SetVerticesDirty();
    }

    public void Stop()
    {
        playing = false;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!playing) return;
        if (Time.unscaledTime - startedAt >= totalDuration)
        {
            Stop();
            return;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!playing) return;

        float elapsed = Mathf.Clamp(Time.unscaledTime - startedAt, 0f, totalDuration);
        float endingFade = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(3.58f, totalDuration, elapsed));
        if (endingFade <= 0.001f) return;

        if (foregroundLayer)
        {
            DrawLogoGlint(vertexHelper, elapsed, endingFade);
            DrawConfetti(vertexHelper, elapsed, endingFade);
            DrawTwinkles(vertexHelper, elapsed, endingFade);
        }
        else
        {
            DrawRockets(vertexHelper, elapsed, endingFade);
            DrawBursts(vertexHelper, elapsed, endingFade);
        }
    }

    private static void DrawRockets(VertexHelper vertexHelper, float elapsed, float endingFade)
    {
        for (int burstIndex = 0; burstIndex < Bursts.Length; burstIndex++)
        {
            BurstSpec burst = Bursts[burstIndex];
            float launchDuration = 0.40f + 0.025f * (burstIndex % 3);
            float launchStart = burst.time - launchDuration;
            if (elapsed < launchStart || elapsed >= burst.time) continue;

            float progress = Mathf.InverseLerp(launchStart, burst.time, elapsed);
            float headAlpha = Mathf.SmoothStep(0f, 1f, progress) * endingFade;
            Vector2 current = EvaluateRocketPosition(burst, burstIndex, progress);
            Color shellColor = ShellPrimary[burst.palette];

            // A broken, soft exhaust train produces a much more natural launch
            // than one continuous ruler-straight line.
            Vector2 previous = current;
            for (int sample = 1; sample <= 10; sample++)
            {
                float sampleProgress = Mathf.Max(0f, progress - sample * 0.031f);
                Vector2 samplePosition = EvaluateRocketPosition(
                    burst,
                    burstIndex,
                    sampleProgress);
                float trailFade = (1f - sample / 11f) * headAlpha;
                Color glow = Color.Lerp(shellColor, Color.white, 0.28f);
                glow.a = trailFade * 0.22f;
                AddSoftStreakBetween(
                    vertexHelper,
                    samplePosition,
                    previous,
                    Mathf.Lerp(14f, 6f, sample / 10f),
                    glow);

                Color hotCore = Color.Lerp(shellColor, Color.white, 0.78f);
                hotCore.a = trailFade * 0.82f;
                AddSoftStreakBetween(
                    vertexHelper,
                    samplePosition,
                    previous,
                    Mathf.Lerp(4.8f, 1.8f, sample / 10f),
                    hotCore);

                if (sample % 2 == 0)
                {
                    Color smoke = new Color(0.55f, 0.63f, 0.82f, trailFade * 0.055f);
                    float puff = 21f + sample * 2.1f;
                    AddSoftDisc(vertexHelper, samplePosition, new Vector2(puff, puff), smoke);
                }
                previous = samplePosition;
            }

            // Tiny shedding embers around the comet tail.
            for (int ember = 0; ember < 5; ember++)
            {
                float seed = Hash01(burst.seed + ember * 43);
                float emberProgress = Mathf.Max(0f, progress - 0.045f - ember * 0.037f);
                Vector2 position = EvaluateRocketPosition(burst, burstIndex, emberProgress);
                position.x += Mathf.Lerp(-18f, 18f, seed);
                position.y -= 12f + 15f * ember;
                float emberAlpha = headAlpha * (1f - ember / 5f) * 0.72f;
                Color emberColor = ShellSecondary[burst.palette];
                emberColor.a = emberAlpha;
                AddSoftDisc(vertexHelper, position, new Vector2(8f, 8f), emberColor);
            }

            AddSoftDisc(
                vertexHelper,
                current,
                new Vector2(48f, 48f),
                new Color(shellColor.r, shellColor.g, shellColor.b, headAlpha * 0.32f));
            AddSoftDisc(
                vertexHelper,
                current,
                new Vector2(15f, 15f),
                new Color(1f, 0.98f, 0.86f, headAlpha));
            AddSoftStar(
                vertexHelper,
                current,
                31f,
                7f,
                new Color(1f, 1f, 1f, headAlpha * 0.95f));
        }
    }

    private static Vector2 EvaluateRocketPosition(BurstSpec burst, int burstIndex, float progress)
    {
        progress = Mathf.Clamp01(progress);
        Vector2 launch = new Vector2(
            burst.center.x * 0.34f + (burstIndex % 2 == 0 ? -105f : 105f),
            -1235f);
        float eased = EaseInCubic(progress);
        Vector2 position = Vector2.LerpUnclamped(launch, burst.center, eased);
        float curveDirection = burstIndex % 2 == 0 ? 1f : -1f;
        position.x += Mathf.Sin(progress * Mathf.PI) * (42f + burstIndex % 3 * 9f) * curveDirection;
        position.x += Mathf.Sin(progress * Mathf.PI * 3f + burst.seed) * 4.5f * progress;
        return position;
    }

    private static void DrawBursts(VertexHelper vertexHelper, float elapsed, float endingFade)
    {
        for (int burstIndex = 0; burstIndex < Bursts.Length; burstIndex++)
        {
            BurstSpec burst = Bursts[burstIndex];
            float age = elapsed - burst.time;
            float shellLife = burst.style == 2 ? 1.50f : 1.30f;
            if (age < 0f || age > shellLife) continue;

            DrawExplosionFlash(vertexHelper, burst, age, endingFade);

            for (int spark = 0; spark < burst.sparks; spark++)
            {
                float lifeSeed = Hash01(burst.seed * 19 + spark * 67);
                float sparkLife = shellLife * Mathf.Lerp(0.70f, 1f, lifeSeed);
                if (age > sparkLife) continue;

                float fade = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(sparkLife * 0.48f, sparkLife, age));
                float ignition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.055f, age));
                float flicker = 0.82f + 0.18f * Mathf.Sin(age * 39f + spark * 2.73f);
                float alpha = fade * ignition * flicker * endingFade;
                if (alpha <= 0.002f) continue;

                int colorIndex = (
                    Mathf.FloorToInt(Hash01(burst.seed * 23 + spark * 101) * 5f)
                    + burst.palette) % ReferenceSparkPalette.Length;
                Color sparkColor = ReferenceSparkPalette[colorIndex];
                Vector2 head = EvaluateSparkPosition(burst, spark, age);

                // Reference-style needle comets: a pointed colored shard with a
                // white-hot internal filament and a restrained bloom halo.
                float trailSeed = Hash01(burst.seed * 37 + spark * 173);
                float trailDuration = Mathf.Lerp(0.050f, 0.145f, trailSeed)
                    * (burst.style == 2 ? 1.22f : 1f);
                Vector2 tail = EvaluateSparkPosition(
                    burst,
                    spark,
                    Mathf.Max(0f, age - trailDuration));
                AddCometShard(vertexHelper, tail, head, sparkColor, alpha, burst.style == 2);

                // Small detached embers break up the mathematical shell edge.
                if (spark % 3 == 0 && age > 0.18f)
                {
                    Vector2 ember = EvaluateSparkPosition(
                        burst,
                        spark,
                        Mathf.Max(0f, age - trailDuration * 1.55f));
                    Color emberColor = sparkColor;
                    emberColor.a = alpha * 0.48f;
                    AddSoftDisc(vertexHelper, ember, new Vector2(5.5f, 5.5f), emberColor);
                }

                if (spark % 7 == 0 && age > 0.34f)
                    DrawCrackleChildren(vertexHelper, burst, spark, age, endingFade);
            }
        }
    }

    private static Vector2 EvaluateSparkPosition(BurstSpec burst, int spark, float age)
    {
        float angleSeed = Hash01(burst.seed * 13 + spark * 83);
        float speedSeed = Hash01(burst.seed * 29 + spark * 131);
        float baseAngle = spark / (float)burst.sparks * Mathf.PI * 2f;
        float angleJitter = Mathf.Lerp(-0.19f, 0.19f, angleSeed);
        float angle = baseAngle + angleJitter + burst.seed * 0.017f;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        float styleSpeed = burst.style == 2 ? 1.42f : 1.58f;
        float initialSpeed = burst.radius * styleSpeed * Mathf.Lerp(0.61f, 1.32f, speedSeed);
        float drag = burst.style == 2 ? 0.82f : burst.style == 1 ? 1.42f : 1.12f;
        float distance = initialSpeed * (1f - Mathf.Exp(-drag * age)) / drag;
        float gravity = burst.style == 2 ? 310f : 225f;
        Vector2 position = burst.center + direction * distance;
        position.y -= gravity * age * age * 0.5f;

        // A tiny lateral drift prevents mathematically perfect spokes while
        // remaining deterministic across devices and captures.
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float drift = (Hash01(burst.seed * 41 + spark * 197) - 0.5f) * 24f;
        position += tangent * drift * age * age;
        return position;
    }

    private static void AddCometShard(
        VertexHelper vertexHelper,
        Vector2 tail,
        Vector2 head,
        Color sparkColor,
        float alpha,
        bool willow)
    {
        Vector2 delta = head - tail;
        float length = delta.magnitude;
        if (length < 1f) return;

        float bloomWidth = willow ? 13f : 11f;
        Color bloom = sparkColor;
        bloom.a = alpha * 0.18f;
        AddSoftStreakBetween(vertexHelper, tail, head, bloomWidth, bloom);

        Vector2 brightPoint = Vector2.Lerp(tail, head, 0.72f);
        float widest = willow ? 6.4f : 5.4f;
        Color transparentColor = sparkColor;
        transparentColor.a = 0f;
        Color saturatedColor = sparkColor;
        saturatedColor.a = alpha * 0.92f;
        Color tipColor = Color.Lerp(sparkColor, Color.white, 0.72f);
        tipColor.a = alpha * 0.76f;
        AddGradientTaperedStreakBetween(
            vertexHelper,
            tail,
            brightPoint,
            0.35f,
            widest,
            transparentColor,
            saturatedColor);
        AddGradientTaperedStreakBetween(
            vertexHelper,
            brightPoint,
            head,
            widest,
            0.45f,
            saturatedColor,
            tipColor);

        Vector2 coreStart = Vector2.Lerp(tail, head, 0.38f);
        Color coreStartColor = new Color(1f, 1f, 1f, 0f);
        Color coreEndColor = new Color(1f, 1f, 1f, alpha * 0.82f);
        AddGradientTaperedStreakBetween(
            vertexHelper,
            coreStart,
            head,
            0.25f,
            1.25f,
            coreStartColor,
            coreEndColor);
    }

    private static void DrawCrackleChildren(
        VertexHelper vertexHelper,
        BurstSpec burst,
        int parentSpark,
        float age,
        float endingFade)
    {
        const float splitTime = 0.36f;
        float childAge = age - splitTime;
        if (childAge < 0f || childAge > 0.46f) return;

        Vector2 splitCenter = EvaluateSparkPosition(burst, parentSpark, splitTime);
        float childFade = (1f - Mathf.SmoothStep(0f, 1f, childAge / 0.46f)) * endingFade;
        for (int child = 0; child < 3; child++)
        {
            float seed = Hash01(burst.seed * 53 + parentSpark * 211 + child * 17);
            float angle = seed * Mathf.PI * 2f + child * 2.094f;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                * Mathf.Lerp(62f, 112f, seed);
            Vector2 position = splitCenter
                + velocity * childAge
                + Vector2.down * (120f * childAge * childAge);
            Vector2 tail = position - velocity.normalized * Mathf.Lerp(18f, 8f, childAge / 0.46f);
            Color color = ShellSecondary[burst.palette];
            color.a = childFade * 0.72f;
            AddSoftStreakBetween(vertexHelper, tail, position, 3.4f, color);
            color.a = childFade;
            AddSoftDisc(vertexHelper, position, new Vector2(5f, 5f), color);
        }
    }

    private static void DrawExplosionFlash(
        VertexHelper vertexHelper,
        BurstSpec burst,
        float age,
        float endingFade)
    {
        if (age > 0.22f) return;
        float progress = age / 0.22f;
        float fade = (1f - progress) * endingFade;
        Color shell = ShellPrimary[burst.palette];

        Color outerGlow = shell;
        outerGlow.a = fade * 0.24f;
        float outerSize = Mathf.Lerp(175f, 52f, EaseOutCubic(progress));
        AddSoftDisc(
            vertexHelper,
            burst.center,
            new Vector2(outerSize, outerSize),
            outerGlow);

        Color innerGlow = Color.Lerp(shell, Color.white, 0.78f);
        innerGlow.a = fade * 0.86f;
        float innerSize = Mathf.Lerp(70f, 13f, EaseOutCubic(progress));
        AddSoftDisc(
            vertexHelper,
            burst.center,
            new Vector2(innerSize, innerSize),
            innerGlow);
        AddSoftStar(
            vertexHelper,
            burst.center,
            Mathf.Lerp(110f, 24f, progress),
            Mathf.Lerp(14f, 4f, progress),
            new Color(1f, 1f, 0.94f, fade));
    }

    private static void DrawShockRing(
        VertexHelper vertexHelper,
        BurstSpec burst,
        float age,
        float endingFade)
    {
        const float life = 0.30f;
        if (age < 0.025f || age > life) return;
        float progress = Mathf.InverseLerp(0.025f, life, age);
        float radius = Mathf.Lerp(12f, burst.radius * 0.27f, EaseOutCubic(progress));
        float alpha = (1f - progress) * endingFade * 0.18f;
        Color color = ShellSecondary[burst.palette];
        color.a = alpha;
        const int segmentCount = 24;
        Vector2 previous = burst.center + Vector2.right * radius;
        for (int segment = 1; segment <= segmentCount; segment++)
        {
            float angle = segment / (float)segmentCount * Mathf.PI * 2f;
            Vector2 current = burst.center
                + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            AddSoftStreakBetween(vertexHelper, previous, current, 5f, color);
            previous = current;
        }
    }

    private static void DrawLogoGlint(VertexHelper vertexHelper, float elapsed, float endingFade)
    {
        const float start = 1.24f;
        const float duration = 0.40f;
        float progress = Mathf.InverseLerp(start, start + duration, elapsed);
        if (elapsed < start || elapsed > start + duration) return;

        float bell = Mathf.Sin(progress * Mathf.PI);
        Vector2 position = Vector2.Lerp(new Vector2(125f, 275f), new Vector2(328f, 190f), progress);
        AddSoftDisc(
            vertexHelper,
            position,
            Vector2.one * Mathf.Lerp(32f, 96f, bell),
            new Color(1f, 0.88f, 0.35f, bell * endingFade * 0.28f));
        AddSoftStar(
            vertexHelper,
            position,
            Mathf.Lerp(20f, 84f, bell),
            Mathf.Lerp(6f, 16f, bell),
            new Color(1f, 1f, 0.86f, bell * endingFade));
        AddSoftStar(
            vertexHelper,
            position,
            Mathf.Lerp(12f, 42f, bell),
            Mathf.Lerp(4f, 10f, bell),
            new Color(1f, 1f, 1f, bell * endingFade));
    }

    private static void DrawConfetti(VertexHelper vertexHelper, float elapsed, float endingFade)
    {
        const int pieceCount = 72;
        const float start = 1.34f;
        for (int index = 0; index < pieceCount; index++)
        {
            float delay = Hash01(index * 137 + 19) * 0.31f;
            float age = elapsed - start - delay;
            float life = Mathf.Lerp(1.72f, 2.34f, Hash01(index * 193 + 41));
            if (age < 0f || age > life) continue;

            float xSeed = Hash01(index * 251 + 73);
            float ySeed = Hash01(index * 277 + 97);
            float driftSeed = Hash01(index * 307 + 113);
            float originX = Mathf.Lerp(-500f, 500f, xSeed);
            Vector2 velocity = new Vector2(
                Mathf.Lerp(-205f, 205f, driftSeed),
                Mathf.Lerp(535f, 825f, ySeed));
            Vector2 position = new Vector2(
                    originX,
                    Mathf.Lerp(-1160f, -1000f, Hash01(index * 331 + 131)))
                + velocity * age
                + Vector2.down * (Mathf.Lerp(245f, 335f, xSeed) * age * age);
            float fade = (1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.72f, 1f, age / life))) * endingFade;
            Color chipColor = index % 3 == 0
                ? new Color(1f, 0.58f, 0.04f, fade)
                : index % 3 == 1
                    ? new Color(0.16f, 0.25f, 1f, fade)
                    : new Color(1f, 0.96f, 0.72f, fade * 0.88f);
            Vector2 size = index % 4 == 0
                ? new Vector2(19f, 8f)
                : new Vector2(11f, 11f);
            AddSolidQuad(
                vertexHelper,
                position,
                size,
                index * 47f + age * Mathf.Lerp(155f, 390f, ySeed),
                chipColor);
        }
    }

    private static void DrawTwinkles(VertexHelper vertexHelper, float elapsed, float endingFade)
    {
        const int sparkleCount = 48;
        for (int index = 0; index < sparkleCount; index++)
        {
            float start = 1.35f + Mathf.Repeat(index * 0.327f, 1f) * 2.20f;
            float life = 0.28f + 0.045f * (index % 4);
            float age = elapsed - start;
            if (age < 0f || age > life) continue;

            float progress = age / life;
            float bell = Mathf.Sin(progress * Mathf.PI) * endingFade;
            Vector2 position = new Vector2(
                Mathf.Lerp(-510f, 510f, Mathf.Repeat(index * 0.7548777f, 1f)),
                Mathf.Lerp(-780f, 970f, Mathf.Repeat(index * 0.5698403f, 1f)));
            float size = 11f + 3.5f * (index % 5);
            AddSoftDisc(
                vertexHelper,
                position,
                Vector2.one * size * 1.8f,
                new Color(0.62f, 0.78f, 1f, bell * 0.18f));
            AddSoftStar(
                vertexHelper,
                position,
                size,
                Mathf.Max(3.5f, size * 0.18f),
                new Color(1f, 1f, 1f, bell));
        }
    }

    private static void AddSoftStar(
        VertexHelper vertexHelper,
        Vector2 center,
        float length,
        float width,
        Color color)
    {
        AddSoftStreak(vertexHelper, center, new Vector2(length, width), 0f, color);
        Color vertical = color;
        vertical.a *= 0.86f;
        AddSoftStreak(
            vertexHelper,
            center,
            new Vector2(length * 0.72f, width),
            90f,
            vertical);
    }

    private static void AddSoftStreakBetween(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f) return;
        AddSoftStreak(
            vertexHelper,
            (start + end) * 0.5f,
            new Vector2(length + width, width),
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
            color);
    }

    private static void AddGradientTaperedStreakBetween(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float startWidth,
        float endWidth,
        Color startColor,
        Color endColor)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f || (startColor.a <= 0.001f && endColor.a <= 0.001f))
            return;

        Vector2 direction = delta / length;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        int startIndex = vertexHelper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;

        vertex.color = startColor;
        vertex.position = start - normal * (startWidth * 0.5f);
        vertex.uv0 = new Vector2(0.84f, 0.50f);
        vertexHelper.AddVert(vertex);
        vertex.position = start + normal * (startWidth * 0.5f);
        vertexHelper.AddVert(vertex);

        vertex.color = endColor;
        vertex.position = end + normal * (endWidth * 0.5f);
        vertexHelper.AddVert(vertex);
        vertex.position = end - normal * (endWidth * 0.5f);
        vertexHelper.AddVert(vertex);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void AddSoftDisc(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 size,
        Color color)
    {
        AddAtlasQuad(vertexHelper, center, size, 0f, color, 0);
    }

    private static void AddSoftStreak(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 size,
        float angleDegrees,
        Color color)
    {
        AddAtlasQuad(vertexHelper, center, size, angleDegrees, color, 1);
    }

    private static void AddSolidQuad(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 size,
        float angleDegrees,
        Color color)
    {
        AddAtlasQuad(vertexHelper, center, size, angleDegrees, color, 2);
    }

    private static void AddAtlasQuad(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 size,
        float angleDegrees,
        Color color,
        int atlasCell)
    {
        if (size.x <= 0f || size.y <= 0f || color.a <= 0.001f) return;

        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (size.x * 0.5f);
        Vector2 up = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * (size.y * 0.5f);
        float inset = 0.5f / (AtlasCellSize * AtlasCellCount);
        float uvMinX = atlasCell / (float)AtlasCellCount + inset;
        float uvMaxX = (atlasCell + 1f) / AtlasCellCount - inset;
        float uvMinY = 0.5f / AtlasCellSize;
        float uvMaxY = 1f - uvMinY;
        int startIndex = vertexHelper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = center - right - up;
        vertex.uv0 = new Vector2(uvMinX, uvMinY);
        vertexHelper.AddVert(vertex);
        vertex.position = center - right + up;
        vertex.uv0 = new Vector2(uvMinX, uvMaxY);
        vertexHelper.AddVert(vertex);
        vertex.position = center + right + up;
        vertex.uv0 = new Vector2(uvMaxX, uvMaxY);
        vertexHelper.AddVert(vertex);
        vertex.position = center + right - up;
        vertex.uv0 = new Vector2(uvMaxX, uvMinY);
        vertexHelper.AddVert(vertex);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void EnsureEffectAtlas()
    {
        if (effectAtlas != null) return;

        int width = AtlasCellSize * AtlasCellCount;
        effectAtlas = new Texture2D(width, AtlasCellSize, TextureFormat.RGBA32, false, true)
        {
            name = "Park Dash Victory Particle Atlas",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * AtlasCellSize];
        for (int y = 0; y < AtlasCellSize; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cell = x / AtlasCellSize;
                float localX = (x % AtlasCellSize + 0.5f) / AtlasCellSize;
                float localY = (y + 0.5f) / AtlasCellSize;
                float alpha;
                if (cell == 0)
                {
                    float dx = localX * 2f - 1f;
                    float dy = localY * 2f - 1f;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    alpha = radial * radial * (3f - 2f * radial);
                }
                else if (cell == 1)
                {
                    float cross = Mathf.Clamp01(1f - Mathf.Abs(localY * 2f - 1f));
                    cross = cross * cross * (3f - 2f * cross);
                    float left = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.14f, localX));
                    float right = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.78f, 1f, localX));
                    alpha = cross * left * right;
                }
                else
                {
                    // Slight edge antialiasing keeps spinning confetti crisp.
                    float edge = Mathf.Min(
                        Mathf.Min(localX, 1f - localX),
                        Mathf.Min(localY, 1f - localY));
                    alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.045f, edge));
                }

                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        effectAtlas.SetPixels32(pixels);
        effectAtlas.Apply(false, true);
    }

    private static float Hash01(int value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value;
    }
}
