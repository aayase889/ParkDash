using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled, camera-facing effects for the matching tray. Every ParticleSystem is
/// created once and reused so a match never allocates or destroys renderers.
/// </summary>
public sealed class CarMatchVfx : MonoBehaviour
{
    private const string AdditiveShaderResource = "CarPrototype/PremiumVfxAdditive";
    private const string AlphaShaderResource = "CarPrototype/PremiumVfxAlpha";

    private Camera sourceCamera;
    private Transform effectRoot;
    private ParticleSystem whiteSmoke;
    private ParticleSystem darkSmoke;
    private ParticleSystem aura;
    private ParticleSystem flare;
    private ParticleSystem ring;
    private ParticleSystem fragments;
    private Material smokeMaterial;
    private Material auraMaterial;
    private Material flareMaterial;
    private Material ringMaterial;
    private Material fragmentMaterial;
    private Texture2D softTexture;
    private Texture2D starTexture;
    private Texture2D ringTexture;
    private Texture2D shardTexture;
    private int smokeEmissionIndex;
    private bool initialized;

    public void Initialize(Camera camera, Transform parent)
    {
        sourceCamera = camera;
        if (initialized) return;

        GameObject rootObject = new GameObject("Premium Match VFX Pool");
        effectRoot = rootObject.transform;
        effectRoot.SetParent(parent != null ? parent : transform, false);

        softTexture = CreateSoftCircleTexture(64);
        starTexture = CreateStarTexture(96);
        ringTexture = CreateRingTexture(96);
        shardTexture = CreateShardTexture(48);

        Shader alphaShader = Resources.Load<Shader>(AlphaShaderResource);
        if (alphaShader == null) alphaShader = Shader.Find("CarPrototype/PremiumVfxAlpha");
        Shader additiveShader = Resources.Load<Shader>(AdditiveShaderResource);
        if (additiveShader == null) additiveShader = Shader.Find("CarPrototype/PremiumVfxAdditive");

        smokeMaterial = CreateEffectMaterial("Arrival Smoke Material", alphaShader, softTexture, 1f);
        auraMaterial = CreateEffectMaterial("Merge Aura Material", additiveShader, softTexture, 2.15f);
        flareMaterial = CreateEffectMaterial("Merge Flare Material", additiveShader, starTexture, 3.4f);
        ringMaterial = CreateEffectMaterial("Merge Ring Material", additiveShader, ringTexture, 1.05f);
        fragmentMaterial = CreateEffectMaterial("Merge Fragment Material", additiveShader, shardTexture, 4.15f);

        whiteSmoke = CreateParticleSystem("White Arrival Puffs", smokeMaterial, 80, 10);
        darkSmoke = CreateParticleSystem("Charcoal Exhaust Flecks", smokeMaterial, 80, 9);
        aura = CreateParticleSystem("Merge Soft Aura", auraMaterial, 8, 30);
        flare = CreateParticleSystem("Merge Core Flare", flareMaterial, 12, 34);
        ring = CreateParticleSystem("Merge Expanding Ring", ringMaterial, 8, 32);
        fragments = CreateParticleSystem("Merge Energy Fragments", fragmentMaterial, 160, 36);

        ConfigureSmokeSystem(whiteSmoke, false);
        ConfigureSmokeSystem(darkSmoke, true);
        ConfigureAuraSystem(aura);
        ConfigureFlareSystem(flare);
        ConfigureRingSystem(ring);
        ConfigureFragmentSystem(fragments);
        initialized = true;
    }

    public void EmitArrivalSmoke(Vector3 carPosition, Vector3 travelDirection, float carScale)
    {
        if (!initialized || whiteSmoke == null) return;

        Vector3 direction = travelDirection.sqrMagnitude > 0.0001f
            ? travelDirection.normalized
            : Vector3.forward;
        float scale = Mathf.Clamp(carScale, 0.42f, 1.15f);
        Vector3 cameraRight = sourceCamera != null ? sourceCamera.transform.right : Vector3.right;
        Vector3 basePosition = carPosition - direction * (0.34f * scale);

        // Two offset puffs make the short final approach read as a soft smoke
        // trail instead of a single pale dot. The random separation is kept
        // perpendicular to the route so the trail still follows the car.
        for (int index = 0; index < 2; index++)
        {
            Vector3 puffPosition = basePosition
                - direction * Random.Range(0f, 0.15f) * scale
                + cameraRight * Random.Range(-0.12f, 0.12f) * scale;
            ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
            {
                position = MoveTowardCamera(puffPosition, 0.012f),
                velocity = -direction * Random.Range(0.08f, 0.20f)
                    + cameraRight * Random.Range(-0.08f, 0.08f),
                startColor = new Color(1f, 1f, 1f, Random.Range(0.88f, 1f)),
                startLifetime = Random.Range(0.31f, 0.41f),
                startSize = Random.Range(0.48f, 0.66f) * scale,
                rotation = Random.Range(0f, Mathf.PI * 2f)
            };
            whiteSmoke.Emit(puff, 1);
        }

        smokeEmissionIndex++;
        if ((smokeEmissionIndex & 1) != 0 || darkSmoke == null) return;

        int fleckCount = smokeEmissionIndex % 4 == 0 ? 2 : 1;
        for (int index = 0; index < fleckCount; index++)
        {
            ParticleSystem.EmitParams fleck = new ParticleSystem.EmitParams
            {
                position = MoveTowardCamera(
                    basePosition - direction * Random.Range(0.06f, 0.20f)
                        + cameraRight * Random.Range(-0.09f, 0.09f) * scale,
                    0.010f),
                velocity = -direction * Random.Range(0.16f, 0.29f)
                    + cameraRight * Random.Range(-0.08f, 0.08f),
                startColor = new Color(0.13f, 0.12f, 0.17f, Random.Range(0.34f, 0.52f)),
                startLifetime = Random.Range(0.29f, 0.40f),
                startSize = Random.Range(0.12f, 0.22f) * scale,
                rotation = Random.Range(0f, Mathf.PI * 2f)
            };
            darkSmoke.Emit(fleck, 1);
        }
    }

    public void PlayMergeCore(Vector3 center, Color matchColor, float diameter)
    {
        if (!initialized) return;
        Vector3 position = MoveTowardCamera(center, 0.055f);
        Color auraColor = Color.Lerp(matchColor, Color.white, 0.18f);
        EmitOne(aura, position, new Color(auraColor.r, auraColor.g, auraColor.b, 0.82f), diameter * 1.06f, 0.34f);
        EmitOne(flare, MoveTowardCamera(center, 0.066f), Color.white, diameter * 0.78f, 0.34f);
    }

    public void PlayMergeOrbit(Vector3 center, Color matchColor, float diameter, int matchedCarCount)
    {
        if (!initialized) return;

        Vector3 position = MoveTowardCamera(center, 0.070f);
        Color ringColor = Color.Lerp(matchColor, Color.white, 0.32f);
        EmitOne(ring, position, new Color(ringColor.r, ringColor.g, ringColor.b, 0.28f), diameter * 1.12f, 0.38f);

        Vector3 screenRight = sourceCamera != null ? sourceCamera.transform.right : Vector3.right;
        Vector3 screenUp = sourceCamera != null ? sourceCamera.transform.up : Vector3.up;
        int fragmentCount = Mathf.Clamp(47 + matchedCarCount * 3, 50, 64);
        for (int index = 0; index < fragmentCount; index++)
        {
            float baseAngle = index * Mathf.PI * 2f / fragmentCount;
            float angle = baseAngle + Random.Range(-0.075f, 0.075f);
            Vector3 radial = screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle);
            Vector3 tangent = -screenRight * Mathf.Sin(angle) + screenUp * Mathf.Cos(angle);
            float speed = diameter * Random.Range(0.98f, 1.48f);
            Color particleColor = Color.Lerp(matchColor, Color.white, Random.Range(0.22f, 0.72f));

            ParticleSystem.EmitParams fragment = new ParticleSystem.EmitParams
            {
                position = position + radial * diameter * Random.Range(0.035f, 0.09f),
                velocity = radial * speed + tangent * Random.Range(-0.13f, 0.13f) * diameter,
                startColor = new Color(particleColor.r, particleColor.g, particleColor.b, Random.Range(0.76f, 1f)),
                startLifetime = Random.Range(0.58f, 0.82f),
                startSize = diameter * Random.Range(0.045f, 0.095f),
                rotation = angle + Mathf.PI * 0.5f
            };
            fragments.Emit(fragment, 1);
        }
    }

    public void PlayMergePeak(Vector3 center, Color matchColor, float diameter)
    {
        if (!initialized) return;
        Vector3 position = MoveTowardCamera(center, 0.082f);
        Color hotColor = Color.Lerp(matchColor, Color.white, 0.78f);
        EmitOne(flare, position, hotColor, diameter * 0.92f, 0.18f);

        // The reference has a handful of faster hot fragments at the instant
        // the car vanishes, separate from the slower circular particle shell.
        Vector3 screenRight = sourceCamera != null ? sourceCamera.transform.right : Vector3.right;
        Vector3 screenUp = sourceCamera != null ? sourceCamera.transform.up : Vector3.up;
        for (int index = 0; index < 10; index++)
        {
            float angle = index * Mathf.PI * 2f / 10f + Random.Range(-0.16f, 0.16f);
            Vector3 radial = screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle);
            ParticleSystem.EmitParams fragment = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = radial * diameter * Random.Range(1.35f, 1.82f),
                startColor = Color.white,
                startLifetime = Random.Range(0.25f, 0.38f),
                startSize = diameter * Random.Range(0.025f, 0.050f),
                rotation = angle + Mathf.PI * 0.5f
            };
            fragments.Emit(fragment, 1);
        }
    }

    /// <summary>
    /// Compact burst measured from the supplied mystery-box reference. It uses
    /// the same pooled systems as the approved merge effect so the reveal has
    /// the same crisp additive rendering and creates no runtime VFX objects.
    /// </summary>
    public void PlayMysteryBoxBurst(Vector3 center, float diameter)
    {
        if (!initialized) return;

        float effectDiameter = Mathf.Clamp(diameter, 1.25f, 2.15f);
        Vector3 position = MoveTowardCamera(center, 0.075f);
        EmitOne(
            aura,
            position,
            new Color(1f, 0.28f, 0.055f, 0.84f),
            effectDiameter * 0.92f,
            0.24f);
        EmitOne(
            flare,
            MoveTowardCamera(center, 0.086f),
            new Color(1f, 0.94f, 0.72f, 1f),
            effectDiameter * 0.70f,
            0.19f);

        Vector3 screenRight = sourceCamera != null ? sourceCamera.transform.right : Vector3.right;
        Vector3 screenUp = sourceCamera != null ? sourceCamera.transform.up : Vector3.up;
        // The reference exposes sixteen short, bright streaks around the
        // fourteen wood fragments. Keep the count and phase deterministic so
        // every reveal reads the same way at any frame rate.
        const int fragmentCount = 16;
        for (int index = 0; index < fragmentCount; index++)
        {
            float angle = (index * 137.50776f + 11f) * Mathf.Deg2Rad;
            Vector3 radial = screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle);
            Vector3 tangent = -screenRight * Mathf.Sin(angle) + screenUp * Mathf.Cos(angle);
            float speedVariation = 0.72f + (index % 5) * 0.065f;
            Color particleColor;
            if (index % 6 == 0)
                particleColor = new Color(1f, 0.34f, 0.72f, 1f);
            else if (index % 3 == 0)
                particleColor = new Color(1f, 0.56f, 0.10f, 1f);
            else
                particleColor = new Color(1f, 0.96f, 0.80f, 1f);

            ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams
            {
                position = position + radial * effectDiameter * (0.025f + (index % 4) * 0.009f),
                velocity = radial * effectDiameter * speedVariation
                    + tangent * effectDiameter * (((index % 5) - 2) * 0.018f),
                startColor = particleColor,
                startLifetime = 0.28f + (index % 5) * 0.030f,
                startSize = effectDiameter * (0.034f + (index % 4) * 0.009f),
                rotation = angle + Mathf.PI * 0.5f
            };
            fragments.Emit(spark, 1);
        }
    }

    private Vector3 MoveTowardCamera(Vector3 position, float distance)
    {
        return sourceCamera != null
            ? position - sourceCamera.transform.forward * distance
            : position;
    }

    private static void EmitOne(
        ParticleSystem system,
        Vector3 position,
        Color color,
        float size,
        float lifetime)
    {
        if (system == null) return;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = Vector3.zero,
            startColor = color,
            startLifetime = lifetime,
            startSize = size,
            rotation = 0f
        };
        system.Emit(emit, 1);
    }

    private ParticleSystem CreateParticleSystem(string objectName, Material material, int maximumParticles, int sortingOrder)
    {
        GameObject systemObject = new GameObject(objectName);
        systemObject.transform.SetParent(effectRoot, false);
        ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.startSize = 1f;
        main.maxParticles = maximumParticles;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static void ConfigureSmokeSystem(ParticleSystem system, bool dark)
    {
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, dark ? 0.72f : 0.76f),
            new Keyframe(0.55f, dark ? 1.05f : 1.16f),
            new Keyframe(1f, dark ? 1.18f : 1.32f)));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(dark ? new Color(0.45f, 0.43f, 0.50f) : new Color(0.94f, 0.95f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.10f),
                new GradientAlphaKey(dark ? 0.34f : 0.70f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = dark ? 0.075f : 0.045f;
        noise.frequency = 0.62f;
        noise.scrollSpeed = 0.15f;
    }

    private static void ConfigureAuraSystem(ParticleSystem system)
    {
        ConfigureSizeAndAlpha(
            system,
            new AnimationCurve(
                new Keyframe(0f, 0.34f),
                new Keyframe(0.24f, 0.94f),
                new Keyframe(0.66f, 1.02f),
                new Keyframe(1f, 1.14f)),
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.88f, 0.10f),
                new GradientAlphaKey(0.48f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
    }

    private static void ConfigureFlareSystem(ParticleSystem system)
    {
        ConfigureSizeAndAlpha(
            system,
            new AnimationCurve(
                new Keyframe(0f, 0.24f),
                new Keyframe(0.32f, 1f),
                new Keyframe(0.68f, 0.82f),
                new Keyframe(1f, 0.56f)),
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.72f, 0.64f),
                new GradientAlphaKey(0f, 1f)
            });
    }

    private static void ConfigureRingSystem(ParticleSystem system)
    {
        ConfigureSizeAndAlpha(
            system,
            new AnimationCurve(
                new Keyframe(0f, 0.22f),
                new Keyframe(0.25f, 0.64f),
                new Keyframe(0.62f, 0.94f),
                new Keyframe(1f, 1.08f)),
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.78f, 0.10f),
                new GradientAlphaKey(0.42f, 0.58f),
                new GradientAlphaKey(0f, 1f)
            });
    }

    private static void ConfigureFragmentSystem(ParticleSystem system)
    {
        ConfigureSizeAndAlpha(
            system,
            new AnimationCurve(
                new Keyframe(0f, 0.62f),
                new Keyframe(0.14f, 1f),
                new Keyframe(0.68f, 0.70f),
                new Keyframe(1f, 0.20f)),
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.06f),
                new GradientAlphaKey(0.82f, 0.54f),
                new GradientAlphaKey(0f, 1f)
            });

        ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1.90f;
        limit.dampen = 0.34f;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.08f;
        noise.frequency = 0.70f;
        noise.scrollSpeed = 0.22f;
    }

    private static void ConfigureSizeAndAlpha(
        ParticleSystem system,
        AnimationCurve sizeCurve,
        GradientAlphaKey[] alphaKeys)
    {
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            alphaKeys);
        color.color = gradient;
    }

    private static Material CreateEffectMaterial(
        string materialName,
        Shader shader,
        Texture2D texture,
        float intensity)
    {
        if (shader == null) return null;
        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = texture
        };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", intensity);
        return material;
    }

    private static Texture2D CreateSoftCircleTexture(int size)
    {
        return CreateTexture(size, "Premium VFX Soft Circle", (x, y) =>
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
            return new Color(1f, 1f, 1f, alpha);
        });
    }

    private static Texture2D CreateRingTexture(int size)
    {
        return CreateTexture(size, "Premium VFX Ring", (x, y) =>
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            float ring = Mathf.Exp(-Mathf.Pow((distance - 0.72f) / 0.075f, 2f));
            float innerGlow = Mathf.Exp(-distance * distance * 7.5f) * 0.12f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(ring * 0.72f + innerGlow));
        });
    }

    private static Texture2D CreateStarTexture(int size)
    {
        return CreateTexture(size, "Premium VFX Star", (x, y) =>
        {
            float radius = Mathf.Sqrt(x * x + y * y);
            float core = Mathf.Exp(-radius * radius * 28f);
            float horizontal = Mathf.Exp(-Mathf.Abs(y) * 42f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x)), 1.6f);
            float vertical = Mathf.Exp(-Mathf.Abs(x) * 42f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 1.6f);
            float diagonalA = Mathf.Exp(-Mathf.Abs(x - y) * 38f) * Mathf.Pow(Mathf.Clamp01(1f - radius), 2.2f);
            float diagonalB = Mathf.Exp(-Mathf.Abs(x + y) * 38f) * Mathf.Pow(Mathf.Clamp01(1f - radius), 2.2f);
            float alpha = Mathf.Clamp01(core + (horizontal + vertical) * 0.76f + (diagonalA + diagonalB) * 0.34f);
            return new Color(1f, 1f, 1f, alpha);
        });
    }

    private static Texture2D CreateShardTexture(int size)
    {
        return CreateTexture(size, "Premium VFX Shard", (x, y) =>
        {
            float roundedX = Mathf.Abs(x) / 0.28f;
            float roundedY = Mathf.Abs(y) / 0.86f;
            float distance = Mathf.Pow(roundedX, 4f) + Mathf.Pow(roundedY, 4f);
            float alpha = Mathf.Clamp01((1.12f - distance) * 4.2f);
            float core = Mathf.Clamp01((0.78f - distance) * 5f);
            return new Color(1f, 1f, 1f, Mathf.Max(alpha * 0.58f, core));
        });
    }

    private static Texture2D CreateTexture(
        int size,
        string textureName,
        System.Func<float, float, Color> pixel)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = textureName,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] colors = new Color[size * size];
        for (int row = 0; row < size; row++)
        {
            float y = ((row + 0.5f) / size - 0.5f) * 2f;
            for (int column = 0; column < size; column++)
            {
                float x = ((column + 0.5f) / size - 0.5f) * 2f;
                colors[row * size + column] = pixel(x, y);
            }
        }
        texture.SetPixels(colors);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        DestroyGeneratedObject(smokeMaterial);
        DestroyGeneratedObject(auraMaterial);
        DestroyGeneratedObject(flareMaterial);
        DestroyGeneratedObject(ringMaterial);
        DestroyGeneratedObject(fragmentMaterial);
        DestroyGeneratedObject(softTexture);
        DestroyGeneratedObject(starTexture);
        DestroyGeneratedObject(ringTexture);
        DestroyGeneratedObject(shardTexture);
    }

    private static void DestroyGeneratedObject(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
