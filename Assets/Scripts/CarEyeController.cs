using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lightweight windshield-eye rig shared by every colored puzzle car. The
/// full windshield becomes a warm ivory character face, while dimensional
/// brown-and-amber eyes animate above it. Everything lives in car-local space so it follows
/// every car orientation.
/// </summary>
public sealed class CarEyeController : MonoBehaviour
{
    private const string AmberEyeTexturePath = "CarPrototype/amber_eye_gradient";
    private static Material eyeWhiteMaterial;
    private static Material eyeOutlineMaterial;
    private static Material pupilMaterial;
    private static Material highlightMaterial;
    private static Mesh eyeDiscMesh;
    private static Mesh windshieldPanelMesh;

    private Transform eyeRoot;
    private Transform leftPupil;
    private Transform rightPupil;
    private Transform[] blinkParts;
    private Vector3[] blinkBaseScales;
    private Vector3 leftPupilBasePosition;
    private Vector3 rightPupilBasePosition;
    private Vector2 gazeOffset;
    private Vector2 gazeTarget;
    private Vector2 gazeVelocity;
    private Vector2 windshieldSize;
    private Vector2 gazeLimits;
    private float nextGazeTime;
    private bool initialized;
    private bool canMove = true;

    public void Initialize(
        Vector3 localWindshieldPosition,
        Vector2 localWindshieldSize,
        float localWindshieldTiltDegrees,
        Vector2 windshieldSurfaceScale,
        bool useModelWindshieldFrame)
    {
        if (initialized) return;
        initialized = true;
        windshieldSize = new Vector2(
            Mathf.Max(0.2f, localWindshieldSize.x),
            Mathf.Max(0.12f, localWindshieldSize.y));

        GameObject rootObject = new GameObject("Integrated Windshield Eye Rig");
        eyeRoot = rootObject.transform;
        eyeRoot.SetParent(transform, false);
        eyeRoot.localPosition = localWindshieldPosition;
        eyeRoot.localRotation = Quaternion.Euler(localWindshieldTiltDegrees, 0f, 0f);
        eyeRoot.localScale = Vector3.one;

        if (!useModelWindshieldFrame)
        {
            CreateWindshieldPanel(
                "Windshield Eye Frame",
                Vector3.zero,
                windshieldSize,
                GetEyeOutlineMaterial(),
                eyeRoot);
        }
        Vector2 eyeSurfaceSize = useModelWindshieldFrame
            ? Vector2.Scale(windshieldSize, windshieldSurfaceScale)
            : windshieldSize * 0.89f;
        // The imported glass is mildly convex. Its highest measured points
        // sit 0.031 units above the corner plane, so 0.040 clears the original
        // glass everywhere without changing the measured X/Z border.
        float windshieldSurfaceDepth = useModelWindshieldFrame ? 0.040f : 0.018f;
        CreateWindshieldPanel(
            "Windshield Eye Surface",
            new Vector3(0f, windshieldSurfaceDepth, 0f),
            eyeSurfaceSize,
            GetEyeWhiteMaterial(),
            eyeRoot);

        float pupilOffset = windshieldSize.x * 0.19f;
        Vector3 pupilScale = new Vector3(
            windshieldSize.x * 0.28f,
            0.034f,
            windshieldSize.y * 0.82f);
        float pupilDepth = windshieldSurfaceDepth + 0.025f;
        leftPupilBasePosition = new Vector3(-pupilOffset, pupilDepth, 0f);
        rightPupilBasePosition = new Vector3(pupilOffset, pupilDepth, 0f);
        leftPupil = CreateEyeDisc("Left Pupil", leftPupilBasePosition, pupilScale, GetPupilMaterial(), eyeRoot);
        rightPupil = CreateEyeDisc("Right Pupil", rightPupilBasePosition, pupilScale, GetPupilMaterial(), eyeRoot);
        CreateEyeDisc(
            "Left Pupil Highlight",
            new Vector3(0.19f, 0.78f, -0.23f),
            new Vector3(0.22f, 0.30f, 0.22f),
            GetHighlightMaterial(),
            leftPupil);
        CreateEyeDisc(
            "Right Pupil Highlight",
            new Vector3(0.19f, 0.78f, -0.23f),
            new Vector3(0.22f, 0.30f, 0.22f),
            GetHighlightMaterial(),
            rightPupil);

        // The windshield remains fixed during a blink. Squashing only the
        // pupils makes the character blink without turning the glass back into
        // two separate floating eyeballs.
        blinkParts = new[] { leftPupil, rightPupil };
        blinkBaseScales = new Vector3[blinkParts.Length];
        for (int index = 0; index < blinkParts.Length; index++)
            blinkBaseScales[index] = blinkParts[index].localScale;

        gazeLimits = new Vector2(
            Mathf.Max(0f, eyeSurfaceSize.x * 0.49f - pupilOffset - pupilScale.x * 0.5f - 0.006f),
            Mathf.Max(0f, eyeSurfaceSize.y * 0.49f - pupilScale.z * 0.5f - 0.006f));
        ChooseNewGaze();
        gazeOffset = gazeTarget;
        ApplyPupilPositions();
        StartCoroutine(BlinkLoop());
    }

    private void Update()
    {
        if (!initialized || leftPupil == null || rightPupil == null) return;

        if (!canMove) return;

        if (Time.time >= nextGazeTime)
            ChooseNewGaze();

        gazeOffset = Vector2.SmoothDamp(gazeOffset, gazeTarget, ref gazeVelocity, 0.18f, 0.5f, Time.deltaTime);
        ApplyPupilPositions();
    }

    private void ApplyPupilPositions()
    {
        Vector3 offset = new Vector3(gazeOffset.x, 0f, gazeOffset.y);
        leftPupil.localPosition = leftPupilBasePosition + offset;
        rightPupil.localPosition = rightPupilBasePosition + offset;
    }

    private void ChooseNewGaze()
    {
        gazeTarget = new Vector2(
            Random.Range(-gazeLimits.x, gazeLimits.x),
            Random.Range(-gazeLimits.y, gazeLimits.y));
        nextGazeTime = Time.time + Random.Range(0.85f, 2.25f);
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(2.2f, 5.0f));
            if (!canMove) continue;
            yield return BlinkOnce();

            if (canMove && Random.value < 0.16f)
            {
                yield return new WaitForSeconds(0.10f);
                yield return BlinkOnce();
            }
        }
    }

    private IEnumerator BlinkOnce()
    {
        if (!canMove) yield break;
        const float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float openness = 1f - Mathf.Sin(progress * Mathf.PI) * 0.94f;
            SetBlinkOpenness(openness);
            yield return null;
        }

        SetBlinkOpenness(1f);
    }

    /// <summary>
    /// Board logic calls this whenever traffic changes. A blocked car keeps its
    /// eyes closed; an available car opens its eyes and resumes natural blinks.
    /// </summary>
    public void SetCanMove(bool value)
    {
        if (!initialized || canMove == value) return;
        canMove = value;
        SetBlinkOpenness(canMove ? 1f : 0f);
    }

    private void SetBlinkOpenness(float openness)
    {
        if (blinkParts == null) return;
        for (int index = 0; index < blinkParts.Length; index++)
        {
            if (blinkParts[index] == null) continue;
            Vector3 scale = blinkBaseScales[index];
            scale.z *= Mathf.Max(0.06f, openness);
            blinkParts[index].localScale = scale;
        }
    }

    private static Transform CreateEyeDisc(string objectName, Vector3 localPosition, Vector3 localScale, Material material, Transform parent)
    {
        GameObject disc = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        disc.transform.SetParent(parent, false);
        disc.transform.localPosition = localPosition;
        disc.transform.localRotation = Quaternion.identity;
        disc.transform.localScale = localScale;
        disc.GetComponent<MeshFilter>().sharedMesh = GetEyeDiscMesh();
        ConfigureEyeRenderer(disc.GetComponent<MeshRenderer>(), material);
        return disc.transform;
    }

    private static Transform CreateWindshieldPanel(
        string objectName,
        Vector3 localPosition,
        Vector2 localSize,
        Material material,
        Transform parent)
    {
        GameObject panel = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = localPosition;
        // The shared silhouette is authored with its wider edge toward local
        // -Z. On the imported car windshield local +Z points toward the hood,
        // so turn only the panel within its own plane. This covers the lower
        // side glass without rotating or mirroring the animated eyes.
        panel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        panel.transform.localScale = new Vector3(localSize.x, 1f, localSize.y);

        panel.GetComponent<MeshFilter>().sharedMesh = GetWindshieldPanelMesh();
        ConfigureEyeRenderer(panel.GetComponent<MeshRenderer>(), material);
        return panel.transform;
    }

    private static Mesh GetEyeDiscMesh()
    {
        if (eyeDiscMesh != null) return eyeDiscMesh;

        const int segments = 16;
        Vector3[] vertices = new Vector3[segments + 1];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int index = 0; index < segments; index++)
        {
            float angle = index * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[index + 1] = new Vector3(x, 0f, z);
            normals[index + 1] = Vector3.up;
            uv[index + 1] = new Vector2(x + 0.5f, z + 0.5f);

            int next = (index + 1) % segments;
            int triangle = index * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = next + 1;
            triangles[triangle + 2] = index + 1;
        }

        eyeDiscMesh = new Mesh
        {
            name = "Shared Mobile Eye Disc",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            normals = normals,
            uv = uv,
            triangles = triangles
        };
        eyeDiscMesh.RecalculateBounds();
        eyeDiscMesh.UploadMeshData(true);
        return eyeDiscMesh;
    }

    private static Mesh GetWindshieldPanelMesh()
    {
        if (windshieldPanelMesh != null) return windshieldPanelMesh;

        // Exact normalized front-glass trapezoid measured from the imported
        // regular-car and limousine meshes. The roof edge is 76% of the hood
        // edge; the caller supplies the measured glass width and slope.
        Vector3[] vertices =
        {
            new Vector3(-0.38f, 0f,  0.50f),
            new Vector3( 0.38f, 0f,  0.50f),
            new Vector3( 0.50f, 0f, -0.50f),
            new Vector3(-0.50f, 0f, -0.50f)
        };
        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3
        };
        Vector2[] uv = new Vector2[vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
            uv[index] = new Vector2(vertices[index].x + 0.5f, vertices[index].z + 0.5f);

        windshieldPanelMesh = new Mesh
        {
            name = "Shared Mobile Windshield Eye Surface",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            triangles = triangles,
            uv = uv
        };
        windshieldPanelMesh.RecalculateNormals();
        windshieldPanelMesh.RecalculateBounds();
        windshieldPanelMesh.UploadMeshData(true);
        return windshieldPanelMesh;
    }

    private static void ConfigureEyeRenderer(Renderer renderer, Material material)
    {
        if (renderer == null) return;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        if (material != null) material.enableInstancing = true;
    }

    private static Material GetEyeWhiteMaterial()
    {
        if (eyeWhiteMaterial == null)
        {
            eyeWhiteMaterial = CarPrototype3D.CreateMaterial(Color.white);
            eyeWhiteMaterial.name = "Runtime Car White Windshield Face";
            if (eyeWhiteMaterial.HasProperty("_Metallic")) eyeWhiteMaterial.SetFloat("_Metallic", 0f);
            if (eyeWhiteMaterial.HasProperty("_Smoothness")) eyeWhiteMaterial.SetFloat("_Smoothness", 0.28f);
        }
        return eyeWhiteMaterial;
    }

    private static Material GetEyeOutlineMaterial()
    {
        if (eyeOutlineMaterial == null)
        {
            eyeOutlineMaterial = CarPrototype3D.CreateMaterial(new Color(0.055f, 0.07f, 0.09f));
            eyeOutlineMaterial.name = "Runtime Car Eye Outline";
            SetLowSmoothness(eyeOutlineMaterial);
        }
        return eyeOutlineMaterial;
    }

    private static Material GetPupilMaterial()
    {
        if (pupilMaterial == null)
        {
            Texture2D amberEyeTexture = Resources.Load<Texture2D>(AmberEyeTexturePath);
            pupilMaterial = CarPrototype3D.CreateMaterial(Color.white);
            pupilMaterial.name = "Runtime Car Brown Amber Eye";
            if (amberEyeTexture != null)
            {
                if (pupilMaterial.HasProperty("_BaseMap")) pupilMaterial.SetTexture("_BaseMap", amberEyeTexture);
                if (pupilMaterial.HasProperty("_MainTex")) pupilMaterial.SetTexture("_MainTex", amberEyeTexture);
            }
            if (pupilMaterial.HasProperty("_BaseColor")) pupilMaterial.SetColor("_BaseColor", Color.white);
            if (pupilMaterial.HasProperty("_Metallic")) pupilMaterial.SetFloat("_Metallic", 0f);
            if (pupilMaterial.HasProperty("_Smoothness")) pupilMaterial.SetFloat("_Smoothness", 0.70f);
        }
        return pupilMaterial;
    }

    private static Material GetHighlightMaterial()
    {
        if (highlightMaterial == null)
        {
            highlightMaterial = CarPrototype3D.CreateMaterial(Color.white);
            highlightMaterial.name = "Runtime Car Pupil Highlight";
            if (highlightMaterial.HasProperty("_Smoothness")) highlightMaterial.SetFloat("_Smoothness", 0.72f);
        }
        return highlightMaterial;
    }

    private static void SetLowSmoothness(Material material)
    {
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
    }
}
