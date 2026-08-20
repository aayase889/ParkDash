#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the production Park Dash victory interstitial at portrait resolution,
/// captures its measured beats, and verifies that the existing Win UI appears
/// only after the complete four-second logo/firework sequence.
/// </summary>
[InitializeOnLoad]
public static class ParkDashVictoryCelebrationEditorVerifier
{
    private const string ActiveKey = "CarPrototype.ParkDashVictoryCapture.Active";
    private const string ExitAfterKey = "CarPrototype.ParkDashVictoryCapture.ExitAfter";
    private const string OutputDirectory =
        "/Users/ayseakbal/.codex/visualizations/2026/08/13/"
        + "019ffae9-834c-7852-9779-1c653f0b5d45/parkdash-victory-frames";
    private static readonly float[] CaptureTimes =
    {
        0.00f, 0.10f, 0.20f, 0.30f, 0.40f, 0.55f, 0.70f, 0.82f,
        1.00f, 1.20f, 1.40f, 1.66f, 1.90f, 2.20f, 2.50f, 2.90f,
        3.30f, 3.70f, 3.95f, 4.08f
    };

    private static GameObject verificationRoot;
    private static CarPrototypeHud hud;
    private static Camera captureCamera;
    private static RenderTexture captureTarget;
    private static double startedAt;
    private static double warmupUntil;
    private static int nextCaptureIndex;
    private static bool running;
    private static bool sequenceStarted;

    static ParkDashVictoryCelebrationEditorVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (EditorPrefs.GetBool(ActiveKey, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += BeginCapture;
    }

    [MenuItem("Tools/Car Prototype/Verify Park Dash Victory Celebration")]
    public static void RunBatch()
    {
        EditorPrefs.SetBool(ActiveKey, true);
        EditorPrefs.SetBool(ExitAfterKey, Application.isBatchMode);
        if (EditorApplication.isPlaying)
            BeginCapture();
        else
            EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode
            && EditorPrefs.GetBool(ActiveKey, false))
        {
            EditorApplication.delayCall += BeginCapture;
        }
        else if (state == PlayModeStateChange.EnteredEditMode
            && EditorPrefs.GetBool(ExitAfterKey, false))
        {
            EditorPrefs.DeleteKey(ExitAfterKey);
            EditorApplication.Exit(0);
        }
    }

    private static void BeginCapture()
    {
        if (running || !EditorApplication.isPlaying) return;
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            DisableLoadedSceneRoots();

            verificationRoot = new GameObject("Park Dash Victory Verification");
            captureCamera = CreateCaptureCamera(verificationRoot.transform);

            GameObject hudObject = new GameObject("Victory HUD Under Test");
            hudObject.transform.SetParent(verificationRoot.transform, false);
            CarPrototype3D game = hudObject.AddComponent<CarPrototype3D>();
            game.enabled = false;
            hud = hudObject.AddComponent<CarPrototypeHud>();
            hud.Initialize(game);

            Canvas[] canvases = verificationRoot.GetComponentsInChildren<Canvas>(true);
            for (int index = 0; index < canvases.Length; index++)
            {
                canvases[index].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[index].worldCamera = captureCamera;
                canvases[index].planeDistance = 1f;
            }

            captureTarget = new RenderTexture(562, 1218, 24, RenderTextureFormat.ARGB32);
            captureTarget.Create();
            if (!captureTarget.IsCreated())
                throw new InvalidOperationException("Could not create the portrait victory capture target.");
            captureCamera.targetTexture = captureTarget;

            running = true;
            sequenceStarted = false;
            warmupUntil = Time.realtimeSinceStartupAsDouble + 0.80;
            nextCaptureIndex = 0;
            EditorApplication.update -= CaptureTick;
            EditorApplication.update += CaptureTick;
        }
        catch (Exception exception)
        {
            FailCapture(exception);
        }
    }

    private static IEnumerator RunCompleteVictorySequence()
    {
        yield return hud.PlayVictoryCelebration();
        hud.ShowVictory(false);
    }

    private static void CaptureTick()
    {
        if (!running || !EditorApplication.isPlaying) return;
        try
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!sequenceStarted)
            {
                if (now < warmupUntil) return;
                startedAt = now;
                hud.StartCoroutine(RunCompleteVictorySequence());
                sequenceStarted = true;
                CaptureFrame(0);
                nextCaptureIndex = 1;
                return;
            }

            double elapsed = now - startedAt;
            if (nextCaptureIndex < CaptureTimes.Length
                && elapsed >= CaptureTimes[nextCaptureIndex])
            {
                CaptureFrame(nextCaptureIndex);
                nextCaptureIndex++;
            }

            if (nextCaptureIndex >= CaptureTimes.Length && elapsed >= 4.18f)
            {
                VerifyFinalHandoff(elapsed);
                Debug.Log(
                    "PARK DASH VICTORY CELEBRATION VERIFICATION PASSED: "
                    + $"duration={elapsed:0.000}s, frames={CaptureTimes.Length}, "
                    + $"output={OutputDirectory}");
                FinishCapture();
            }
        }
        catch (Exception exception)
        {
            FailCapture(exception);
        }
    }

    private static void CaptureFrame(int index)
    {
        Canvas.ForceUpdateCanvases();
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
            $"victory-{index:00}-{CaptureTimes[index]:0.00}s.png");
        File.WriteAllBytes(path, screenshot.EncodeToPNG());
        UnityEngine.Object.Destroy(screenshot);
        RenderTexture.active = previous;

        VerifyBeat(index);
    }

    private static void VerifyBeat(int index)
    {
        float time = CaptureTimes[index];
        Transform celebration = FindNamedTransform(
            verificationRoot.transform,
            "Park Dash Victory Celebration");
        Transform win = FindNamedTransform(verificationRoot.transform, "Victory Overlay");
        if (time < 4f && (celebration == null || !celebration.gameObject.activeInHierarchy))
            throw new InvalidOperationException("The celebration ended before its four-second handoff.");
        if (time < 4f && win != null && win.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The Win UI appeared over the logo/firework animation.");

        if (time >= 0.20f && time < 0.30f)
        {
            RawImage badge = FindNamedComponent<RawImage>(
                verificationRoot.transform,
                "Badge Artwork");
            RawImage parkP = FindNamedComponent<RawImage>(
                verificationRoot.transform,
                "P Artwork");
            if (badge == null || badge.color.a < 0.9f || parkP == null || parkP.color.a < 0.5f)
                throw new InvalidOperationException("The badge and first PARK letter did not construct first.");
        }
        if (time >= 0.82f && time < 0.90f)
        {
            Transform dash = FindNamedTransform(verificationRoot.transform, "DASH Layer");
            if (dash == null || dash.localScale.x < 1.30f)
                throw new InvalidOperationException("DASH is missing its reference-sized forward slam.");
        }
        if (time >= 1.20f && time < 1.30f)
        {
            RawImage finalLogo = FindNamedComponent<RawImage>(
                verificationRoot.transform,
                "Approved Final Logo Artwork");
            if (finalLogo == null || finalLogo.color.a < 0.90f)
                throw new InvalidOperationException("The animation did not settle onto the untouched final logo.");
            RectTransform assembly = FindNamedTransform(
                verificationRoot.transform,
                "Park Dash Logo Assembly") as RectTransform;
            if (assembly == null || Mathf.Abs(assembly.sizeDelta.x - 990f) > 0.5f)
                throw new InvalidOperationException("The enlarged Park Dash popup scale was not applied.");
        }
        if (time >= 1.90f && time < 3.40f)
        {
            ParkDashVictoryEffectsGraphic fireworks = FindNamedComponent<ParkDashVictoryEffectsGraphic>(
                verificationRoot.transform,
                "Victory Fireworks Behind Logo");
            if (fireworks == null || !fireworks.IsPlaying)
                throw new InvalidOperationException("The firework layer stopped during the celebration window.");
            Mesh mesh = fireworks.canvasRenderer.GetMesh();
            int vertexCount = mesh != null ? mesh.vertexCount : 0;
            if (vertexCount < 40)
                throw new InvalidOperationException("The active firework layer did not render enough streak geometry.");
        }
    }

    private static void VerifyFinalHandoff(double elapsed)
    {
        Transform celebration = FindNamedTransform(
            verificationRoot.transform,
            "Park Dash Victory Celebration");
        Transform win = FindNamedTransform(verificationRoot.transform, "Victory Overlay");
        if (celebration == null || celebration.gameObject.activeSelf)
            throw new InvalidOperationException("The celebration did not stop before the Win UI handoff.");
        if (win == null || !win.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The existing Win UI did not appear after the celebration.");
        if (elapsed < 4f || elapsed > 4.35f)
            throw new InvalidOperationException("The celebration-to-Win handoff is outside the four-second window.");
        if (Mathf.Abs(Time.timeScale) > 0.001f)
            throw new InvalidOperationException("Gameplay was not kept paused across the victory handoff.");
    }

    private static Camera CreateCaptureCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Portrait Victory Capture Camera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    private static void DisableLoadedSceneRoots()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded) continue;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                roots[rootIndex].SetActive(false);
        }
    }

    private static T FindNamedComponent<T>(Transform root, string objectName)
        where T : Component
    {
        Transform transform = FindNamedTransform(root, objectName);
        return transform != null ? transform.GetComponent<T>() : null;
    }

    private static Transform FindNamedTransform(Transform root, string objectName)
    {
        if (root == null) return null;
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
            if (descendants[index].name == objectName) return descendants[index];
        return null;
    }

    private static void FinishCapture()
    {
        running = false;
        sequenceStarted = false;
        EditorApplication.update -= CaptureTick;
        Time.timeScale = 1f;
        if (captureCamera != null) captureCamera.targetTexture = null;
        if (captureTarget != null)
        {
            captureTarget.Release();
            UnityEngine.Object.Destroy(captureTarget);
        }
        if (verificationRoot != null) UnityEngine.Object.Destroy(verificationRoot);
        captureTarget = null;
        captureCamera = null;
        hud = null;
        verificationRoot = null;
        EditorPrefs.DeleteKey(ActiveKey);
        EditorApplication.ExitPlaymode();
    }

    private static void FailCapture(Exception exception)
    {
        Debug.LogException(exception);
        running = false;
        sequenceStarted = false;
        EditorApplication.update -= CaptureTick;
        Time.timeScale = 1f;
        EditorPrefs.DeleteKey(ActiveKey);
        EditorPrefs.DeleteKey(ExitAfterKey);
        if (Application.isBatchMode) EditorApplication.Exit(2);
        else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}
#endif
