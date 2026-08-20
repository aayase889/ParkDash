#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the real global reveal coroutine in Play Mode and captures its key
/// beats. This is intentionally a visual verification tool, not a second copy
/// of the animation.
/// </summary>
[InitializeOnLoad]
public static class Level50MysteryBoxAnimationEditorVerifier
{
    private const string ActiveKey = "CarPrototype.Level50RevealCapture.Active";
    private const string ExitAfterKey = "CarPrototype.Level50RevealCapture.ExitAfter";
    private const string OutputDirectory =
        "/Users/ayseakbal/.codex/visualizations/2026/08/19/01a019ce-6496-7043-b7b4-a1f441a1e932/mystery-box-reveal";
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly float[] CaptureTimes = BuildCaptureTimes();

    private static GameObject verificationRoot;
    private static CarPuzzlePiece boxPiece;
    private static Camera captureCamera;
    private static RenderTexture captureTarget;
    private static int nextCaptureIndex;
    private static bool captureRunning;
    private static bool sawCarPopOvershoot;

    private static float[] BuildCaptureTimes()
    {
        // Capture every rendered frame at the game's 60-fps target. This
        // exposes one-frame holds or velocity discontinuities that a 30-fps
        // contact sheet can hide.
        var times = new float[38];
        for (int index = 0; index < times.Length; index++)
            times[index] = index / 60f;
        return times;
    }

    static Level50MysteryBoxAnimationEditorVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (EditorPrefs.GetBool(ActiveKey, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += BeginPlayModeCapture;
    }

    [MenuItem("Tools/Car Prototype/Verify Global Premium Reveal Animation")]
    public static void RunBatch()
    {
        EditorPrefs.SetBool(ActiveKey, true);
        EditorPrefs.SetBool(ExitAfterKey, Application.isBatchMode);
        if (EditorApplication.isPlaying)
            BeginPlayModeCapture();
        else
            EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode
            && EditorPrefs.GetBool(ActiveKey, false))
        {
            EditorApplication.delayCall += BeginPlayModeCapture;
        }
        else if (state == PlayModeStateChange.EnteredEditMode
            && EditorPrefs.GetBool(ExitAfterKey, false))
        {
            EditorPrefs.DeleteKey(ExitAfterKey);
            EditorApplication.Exit(0);
        }
    }

    private static void BeginPlayModeCapture()
    {
        if (captureRunning || !EditorApplication.isPlaying) return;
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            string[] previousFrames = Directory.GetFiles(OutputDirectory, "reveal-*.png");
            for (int frameIndex = 0; frameIndex < previousFrames.Length; frameIndex++)
                File.Delete(previousFrames[frameIndex]);
            Time.timeScale = 1f;
            Time.captureFramerate = 60;
            // Play Mode opens the project's normal startup scene. Isolate this
            // verifier from its board, cameras, UI, and managers before
            // constructing the one-piece capture stage.
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    roots[rootIndex].SetActive(false);
            }
            CarPrototype3D[] livePrototypes = UnityEngine.Object.FindObjectsByType<CarPrototype3D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int prototypeIndex = 0; prototypeIndex < livePrototypes.Length; prototypeIndex++)
                livePrototypes[prototypeIndex].enabled = false;
            verificationRoot = new GameObject("Global Premium Reveal Verification");
            captureCamera = CreateCamera(verificationRoot.transform);
            CreateLighting(verificationRoot.transform);
            CreateFloor(verificationRoot.transform);
            CarMatchVfx pooledVfx = verificationRoot.AddComponent<CarMatchVfx>();
            pooledVfx.Initialize(captureCamera, verificationRoot.transform);
            boxPiece = CreateGlobalBoxPiece(verificationRoot.transform, pooledVfx);
            sawCarPopOvershoot = false;

            captureTarget = new RenderTexture(1000, 700, 24, RenderTextureFormat.ARGB32);
            captureTarget.Create();
            if (!captureTarget.IsCreated())
                throw new InvalidOperationException("Could not create the reveal capture RenderTexture.");
            captureCamera.targetTexture = captureTarget;

            CaptureFrame(0);
            nextCaptureIndex = 1;
            MethodInfo reveal = typeof(CarPuzzlePiece).GetMethod("RevealFromBox", PrivateInstance);
            if (reveal == null)
                throw new MissingMethodException("CarPuzzlePiece.RevealFromBox could not be found.");
            boxPiece.StartCoroutine((IEnumerator)reveal.Invoke(boxPiece, null));
            // StartCoroutine executes through its first yield immediately.
            // Validate the impact objects here so PNG encoding or a busy
            // editor frame cannot move a fixed-time screenshot past the hit.
            VerifyImpactState();
            captureRunning = true;
            EditorApplication.update -= CaptureTick;
            EditorApplication.update += CaptureTick;
        }
        catch (Exception exception)
        {
            FailCapture(exception);
        }
    }

    private static void CaptureTick()
    {
        if (!captureRunning || !EditorApplication.isPlaying) return;
        try
        {
            TrackCarPopState();
            if (nextCaptureIndex < CaptureTimes.Length)
            {
                CaptureFrame(nextCaptureIndex);
                nextCaptureIndex++;
            }

            if (nextCaptureIndex >= CaptureTimes.Length)
            {
                VerifyFinishedState();
                Debug.Log(
                    "GLOBAL PREMIUM REVEAL VERIFICATION PASSED: "
                    + $"frames={CaptureTimes.Length}, output={OutputDirectory}");
                FinishCapture();
            }
        }
        catch (Exception exception)
        {
            FailCapture(exception);
        }
    }

    private static Camera CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Exact Car Camera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.25f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.20f, 0.29f, 1f);
        camera.transform.position = new Vector3(0f, 15.4f, -8.7f);
        camera.transform.rotation = Quaternion.LookRotation(
            new Vector3(0f, 0f, -1.1f) - camera.transform.position,
            Vector3.up);
        return camera;
    }

    private static void CreateLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("Preview Directional Light");
        lightObject.transform.SetParent(parent, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
    }

    private static void CreateFloor(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Preview Asphalt";
        floor.transform.SetParent(parent, false);
        floor.transform.position = new Vector3(0f, 0f, -1.1f);
        floor.transform.localScale = new Vector3(8f, 0.04f, 5f);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        floor.GetComponent<Renderer>().sharedMaterial = new Material(shader)
        {
            color = new Color(0.16f, 0.20f, 0.29f, 1f)
        };
    }

    private static CarPuzzlePiece CreateGlobalBoxPiece(Transform parent, CarMatchVfx pooledVfx)
    {
        GameObject pieceObject = new GameObject("Global Animated Box Piece");
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();

        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod("Configure", PrivateInstance);
        MethodInfo setRevealVfx = typeof(CarPuzzlePiece).GetMethod(
            "SetMysteryBoxRevealVfx",
            PrivateInstance);
        Type colorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        if (configure == null
            || setRevealVfx == null
            || colorType == null
            || directionType == null)
            throw new MissingMethodException("Global box construction API could not be found.");

        configure.Invoke(piece, new object[]
        {
            0,
            0,
            Enum.Parse(colorType, "Blue"),
            Enum.Parse(directionType, "Down"),
            new Vector3(0f, 0.35f, -1.1f),
            0.53f,
            0.53f,
            1,
            true,
            false
        });
        setRevealVfx.Invoke(piece, new object[] { pooledVfx });
        return piece;
    }

    private static void CaptureFrame(int frameIndex)
    {
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = 2.25f;
        captureCamera.transform.position = new Vector3(0f, 15.4f, -8.7f);
        captureCamera.transform.rotation = Quaternion.LookRotation(
            new Vector3(0f, 0f, -1.1f) - captureCamera.transform.position,
            Vector3.up);
        captureCamera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = captureTarget;
        Texture2D screenshot = new Texture2D(
            captureTarget.width,
            captureTarget.height,
            TextureFormat.RGBA32,
            false);
        screenshot.ReadPixels(
            new Rect(0f, 0f, captureTarget.width, captureTarget.height),
            0,
            0);
        screenshot.Apply();
        string path = Path.Combine(
            OutputDirectory,
            $"reveal-{frameIndex:00}-{CaptureTimes[frameIndex]:0.00}s.png");
        File.WriteAllBytes(path, screenshot.EncodeToPNG());
        UnityEngine.Object.Destroy(screenshot);
        RenderTexture.active = previous;
    }

    private static void VerifyImpactState()
    {
        Transform car = boxPiece != null ? boxPiece.transform.Find("Hidden Car Visual") : null;
        if (car == null || !car.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The car did not appear under the opening flash.");
        Vector3 localScreenUp = boxPiece.transform.InverseTransformVector(
            captureCamera.transform.up).normalized;
        if (car.localScale.x <= 0.80f || car.localScale.x >= 0.94f)
            throw new InvalidOperationException(
                "The revealed car did not begin with its subtle pre-pop shrink.");
        if (Vector3.Dot(car.localPosition, localScreenUp) >= -0.002f)
            throw new InvalidOperationException(
                "The revealed car did not begin tucked slightly inside the box.");

        ParticleSystem[] pooledSystems = verificationRoot.GetComponentsInChildren<ParticleSystem>(true);
        int visibleParticles = 0;
        for (int systemIndex = 0; systemIndex < pooledSystems.Length; systemIndex++)
            visibleParticles += pooledSystems[systemIndex].particleCount;
        if (visibleParticles < 14)
            throw new InvalidOperationException("The pooled flash and compact spark burst are missing.");

        Transform mergeRing = FindDescendant(
            verificationRoot.transform,
            "Merge Expanding Ring");
        ParticleSystem mergeRingParticles = mergeRing != null
            ? mergeRing.GetComponent<ParticleSystem>()
            : null;
        if (mergeRingParticles == null || mergeRingParticles.particleCount < 1)
            throw new InvalidOperationException(
                "The car-merge sparkly ring was not emitted with the box rupture.");

        Transform compactBurst = FindDescendant(
            verificationRoot.transform,
            "Premium Mystery Box Burst");
        Renderer[] compactRenderers = compactBurst != null
            ? compactBurst.GetComponentsInChildren<Renderer>(true)
            : null;
        if (compactBurst == null || compactRenderers == null || compactRenderers.Length < 4)
            throw new InvalidOperationException(
                "The warm-white impact bloom or rising question mark is missing.");

        Transform question = FindDescendant(
            verificationRoot.transform,
            "Rising Mystery Question Mark");
        MeshFilter questionMesh = question != null ? question.GetComponent<MeshFilter>() : null;
        if (question == null
            || !question.gameObject.activeInHierarchy
            || questionMesh == null
            || questionMesh.sharedMesh == null)
            throw new InvalidOperationException(
                "The separate rising mystery question mark is missing.");

        Renderer[] renderers = verificationRoot.GetComponentsInChildren<Renderer>(true);
        int fragmentCount = 0;
        int shadowCount = 0;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material material = renderers[rendererIndex].sharedMaterial;
            if (material == null) continue;
            if (material.name == "Premium Mystery Box Fragment Atlas") fragmentCount++;
            else if (material.name == "Premium Mystery Box Fragment Shadows") shadowCount++;
        }

        if (fragmentCount != 14 || shadowCount != 14)
            throw new InvalidOperationException(
                $"Expected 14 visible wood fragments and 14 shadows; found "
                + $"{fragmentCount} fragments and {shadowCount} shadows.");
    }

    private static void VerifyFinishedState()
    {
        if (!sawCarPopOvershoot)
            throw new InvalidOperationException(
                "The revealed car never rose and scaled through its pop overshoot.");

        Transform boxVisual = boxPiece.transform.Find("Mystery Box Visual");
        Transform carVisual = boxPiece.transform.Find("Hidden Car Visual");
        if (boxVisual == null || boxVisual.gameObject.activeSelf)
            throw new InvalidOperationException("The broken box did not cleanly disappear.");
        if (carVisual == null || !carVisual.gameObject.activeSelf)
            throw new InvalidOperationException("The revealed car is not active after the settle.");
        if (Vector3.Distance(carVisual.localPosition, Vector3.zero) > 0.001f
            || Vector3.Distance(carVisual.localScale, Vector3.one) > 0.001f)
            throw new InvalidOperationException("The revealed car did not finish at its exact authored transform.");
        Collider collider = boxPiece.GetComponent<Collider>();
        if (collider == null || !collider.enabled)
            throw new InvalidOperationException("The revealed car did not regain input after settling.");
    }

    private static void TrackCarPopState()
    {
        Transform car = boxPiece != null ? boxPiece.transform.Find("Hidden Car Visual") : null;
        if (car == null || !car.gameObject.activeInHierarchy || captureCamera == null) return;
        Vector3 localScreenUp = boxPiece.transform.InverseTransformVector(
            captureCamera.transform.up).normalized;
        if (car.localScale.x >= 1.08f
            && Vector3.Dot(car.localPosition, localScreenUp) >= 0.010f)
        {
            sawCarPopOvershoot = true;
        }
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
            if (descendants[index].name == objectName) return descendants[index];
        return null;
    }

    private static void FinishCapture()
    {
        captureRunning = false;
        EditorApplication.update -= CaptureTick;
        Time.captureFramerate = 0;
        if (captureCamera != null) captureCamera.targetTexture = null;
        if (captureTarget != null)
        {
            captureTarget.Release();
            UnityEngine.Object.Destroy(captureTarget);
        }
        if (verificationRoot != null) UnityEngine.Object.Destroy(verificationRoot);
        captureTarget = null;
        captureCamera = null;
        boxPiece = null;
        verificationRoot = null;
        sawCarPopOvershoot = false;
        EditorPrefs.DeleteKey(ActiveKey);
        EditorApplication.ExitPlaymode();
    }

    private static void FailCapture(Exception exception)
    {
        Debug.LogException(exception);
        captureRunning = false;
        EditorApplication.update -= CaptureTick;
        Time.captureFramerate = 0;
        EditorPrefs.DeleteKey(ActiveKey);
        EditorPrefs.DeleteKey(ExitAfterKey);
        if (Application.isBatchMode) EditorApplication.Exit(2);
        else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}
#endif
