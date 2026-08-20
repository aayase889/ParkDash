#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Captures and validates the production Win UI reveal in portrait.</summary>
[InitializeOnLoad]
public static class ParkDashVictoryUiEditorVerifier
{
    private const string ActiveKey = "CarPrototype.NewVictoryUiCapture.Active";
    private const string ExitAfterKey = "CarPrototype.NewVictoryUiCapture.ExitAfter";
    private const string OutputDirectory =
        "/Users/ayseakbal/.codex/visualizations/2026/08/13/"
        + "019ffae9-834c-7852-9779-1c653f0b5d45/new-victory-ui-frames";
    private static readonly float[] CaptureTimes =
    {
        0f, 0.10f, 0.20f, 0.30f, 0.40f, 0.50f, 0.60f, 0.70f, 0.82f, 1.10f
    };

    private static GameObject verificationRoot;
    private static CarPrototypeHud hud;
    private static Camera captureCamera;
    private static RenderTexture captureTarget;
    private static double warmupUntil;
    private static double startedAt;
    private static int nextCaptureIndex;
    private static bool running;
    private static bool revealStarted;
    private static int buttonVerificationPhase;
    private static double buttonPhaseStartedAt;
    private static bool finalStateVerified;

    static ParkDashVictoryUiEditorVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (EditorPrefs.GetBool(ActiveKey, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += BeginCapture;
    }

    [MenuItem("Tools/Car Prototype/Verify New Victory UI")]
    public static void RunBatch()
    {
        EditorPrefs.SetBool(ActiveKey, true);
        EditorPrefs.SetBool(ExitAfterKey, Application.isBatchMode);
        if (EditorApplication.isPlaying) BeginCapture();
        else EditorApplication.EnterPlaymode();
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
            verificationRoot = new GameObject("New Victory UI Verification");
            captureCamera = CreateCaptureCamera(verificationRoot.transform);

            GameObject hudObject = new GameObject("Victory HUD Under Test");
            hudObject.transform.SetParent(verificationRoot.transform, false);
            CarPrototype3D game = hudObject.AddComponent<CarPrototype3D>();
            game.enabled = false;
            SetRepresentativeObjectives(game);
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
            captureCamera.targetTexture = captureTarget;
            running = true;
            revealStarted = false;
            buttonVerificationPhase = 0;
            finalStateVerified = false;
            nextCaptureIndex = 0;
            warmupUntil = Time.realtimeSinceStartupAsDouble + 0.8;
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
        if (!running || !EditorApplication.isPlaying) return;
        try
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!revealStarted)
            {
                if (now < warmupUntil) return;
                startedAt = now;
                hud.ShowVictory(false);
                revealStarted = true;
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
            if (nextCaptureIndex >= CaptureTimes.Length && elapsed >= 1.18)
            {
                if (!finalStateVerified)
                {
                    VerifyFinalState();
                    finalStateVerified = true;
                }
                if (VerifyButtonAnimationsAndNavigation(now))
                {
                    Debug.Log(
                        "NEW VICTORY UI VERIFICATION PASSED: "
                        + $"duration={elapsed:0.000}s, frames={CaptureTimes.Length}, "
                        + "layout=2x3, controls=animated, close=main-menu, "
                        + $"output={OutputDirectory}");
                    FinishCapture();
                }
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
        Texture2D screenshot = new Texture2D(562, 1218, TextureFormat.RGBA32, false);
        screenshot.ReadPixels(new Rect(0f, 0f, 562f, 1218f), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(
            Path.Combine(OutputDirectory, $"victory-ui-{index:00}-{CaptureTimes[index]:0.00}s.png"),
            screenshot.EncodeToPNG());
        UnityEngine.Object.Destroy(screenshot);
        RenderTexture.active = previous;
        VerifyBeat(CaptureTimes[index]);
    }

    private static void VerifyBeat(float time)
    {
        Transform root = FindNamedTransform(verificationRoot.transform, "Victory Overlay");
        if (root == null || !root.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The new Win UI did not appear after the celebration.");
        if (FindNamedTransform(root, "Win Retry Button") != null
            || FindNamedTransform(root, "Win Home Button") != null
            || FindNamedTransform(root, "Win Next Level Button") != null)
            throw new InvalidOperationException("Legacy Win UI buttons are still present.");
        if (time >= 0.20f && time < 0.30f)
        {
            RawImage first = FindNamedComponent<RawImage>(root, "PERFECT PerfectP Artwork");
            RawImage final = FindNamedComponent<RawImage>(root, "Approved Settled PERFECT Word Artwork");
            if (first == null || first.color.a < 0.8f || final == null || final.color.a > 0.1f)
                throw new InvalidOperationException("PERFECT did not begin with its first oversized letter.");
        }
        if (time >= 0.70f && time < 0.80f)
        {
            RawImage last = FindNamedComponent<RawImage>(root, "PERFECT PerfectBang Artwork");
            if (last == null || last.color.a < 0.8f)
                throw new InvalidOperationException("The exclamation point did not land last.");
        }
    }

    private static void VerifyFinalState()
    {
        Transform root = FindNamedTransform(verificationRoot.transform, "Victory Overlay");
        RawImage panel = FindNamedComponent<RawImage>(root, "Approved New Victory Panel Artwork");
        RawImage final = FindNamedComponent<RawImage>(root, "Approved Settled PERFECT Word Artwork");
        TextMeshProUGUIProxy.VerifyLevelText(root);
        if (panel == null || panel.texture == null || final == null || final.color.a < 0.99f)
            throw new InvalidOperationException("The new approved art did not settle correctly.");
        if (FindNamedTransform(root, "Result Red Objective") == null)
            throw new InvalidOperationException("Objective completion icons are missing from the panel.");
        VerifySixObjectiveGrid(root);
        if (FindNamedComponent<RawImage>(root, "Approved Win Continue Artwork") == null
            || FindNamedComponent<RawImage>(root, "Approved Win Close Artwork") == null)
            throw new InvalidOperationException("The clean, independently animated button artwork is missing.");
        VerifyApprovedControlPositions(root);
        if (FindNamedTransform(root, "Victory Objective Sparkles") != null)
            throw new InvalidOperationException("The removed objective sparkles are still present.");
        VerifyButtonHasAnimation(root, "Win Continue Button");
        VerifyButtonHasAnimation(root, "Win Close Button");
        if (Mathf.Abs(Time.timeScale) > 0.001f)
            throw new InvalidOperationException("Gameplay was not paused behind the Win UI.");
    }

    private static void VerifyApprovedControlPositions(Transform root)
    {
        RectTransform continueArt = FindNamedTransform(root, "Approved Win Continue Artwork") as RectTransform;
        RectTransform closeArt = FindNamedTransform(root, "Approved Win Close Artwork") as RectTransform;
        if (continueArt == null || closeArt == null)
            throw new InvalidOperationException("Win control artwork is missing.");
        if (Vector2.Distance(continueArt.anchoredPosition, new Vector2(10f, -355f)) > 0.5f
            || Vector2.Distance(continueArt.sizeDelta, new Vector2(503f, 204f)) > 0.5f)
            throw new InvalidOperationException("Continue is not in its approved source-art position.");
        if (Vector2.Distance(closeArt.anchoredPosition, new Vector2(352f, 414f)) > 0.5f
            || Vector2.Distance(closeArt.sizeDelta, new Vector2(128f, 128f)) > 0.5f)
            throw new InvalidOperationException("X is not in its approved source-art position.");
    }

    private static void VerifySixObjectiveGrid(Transform root)
    {
        string[] names =
        {
            "Result Red Objective", "Result Green Objective", "Result Blue Objective",
            "Result Purple Objective", "Result Yellow Objective", "Result Pink Objective"
        };
        Vector2[] positions = new Vector2[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            RectTransform row = FindNamedTransform(root, names[index]) as RectTransform;
            if (row == null || !row.gameObject.activeInHierarchy)
                throw new InvalidOperationException($"Six-objective grid is missing {names[index]}.");
            positions[index] = row.anchoredPosition;
        }
        for (int index = 0; index < positions.Length; index++)
        {
            float expectedX = index % 2 == 0 ? -155f : 155f;
            float expectedY = 76f - index / 2 * 122f;
            if (Mathf.Abs(positions[index].x - expectedX) > 0.5f
                || Mathf.Abs(positions[index].y - expectedY) > 0.5f)
                throw new InvalidOperationException("Objectives are not arranged in the required 2-column x 3-row grid.");
            if (positions[index].y > 100f || positions[index].y < -180f)
                throw new InvalidOperationException("An objective can touch PERFECT or Continue.");
        }
    }

    private static void VerifyButtonHasAnimation(Transform root, string name)
    {
        Transform button = FindNamedTransform(root, name);
        if (button == null || button.GetComponent<SimpleButtonPressAnimation>() == null)
            throw new InvalidOperationException($"{name} is missing its press animation.");
    }

    private static bool VerifyButtonAnimationsAndNavigation(double now)
    {
        if (buttonVerificationPhase >= 5)
        {
            if (now - buttonPhaseStartedAt < 0.06) return false;
            Transform menuAfterClick = FindNamedTransform(verificationRoot.transform, "StartMenuPanel");
            if (menuAfterClick == null || !menuAfterClick.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The Win UI X button did not return to the main menu.");
            return true;
        }

        Transform root = FindNamedTransform(verificationRoot.transform, "Victory Overlay");
        Transform continueButton = FindNamedTransform(root, "Win Continue Button");
        Transform continueArt = FindNamedTransform(root, "Approved Win Continue Artwork");
        Transform closeButton = FindNamedTransform(root, "Win Close Button");
        Transform closeArt = FindNamedTransform(root, "Approved Win Close Artwork");
        PointerEventData pointer = new PointerEventData(EventSystem.current);
        switch (buttonVerificationPhase)
        {
            case 0:
                ExecuteEvents.Execute(continueButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                buttonVerificationPhase = 1;
                buttonPhaseStartedAt = now;
                return false;
            case 1:
                if (now - buttonPhaseStartedAt < 0.09) return false;
                if (continueArt.localScale.x > 0.965f)
                    throw new InvalidOperationException("Continue artwork did not visibly compress on press.");
                ExecuteEvents.Execute(continueButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                buttonVerificationPhase = 2;
                buttonPhaseStartedAt = now;
                return false;
            case 2:
                if (now - buttonPhaseStartedAt < 0.09) return false;
                if (Mathf.Abs(continueArt.localScale.x - 1f) > 0.01f)
                    throw new InvalidOperationException("Continue artwork did not rebound after release.");
                ExecuteEvents.Execute(closeButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                buttonVerificationPhase = 3;
                buttonPhaseStartedAt = now;
                return false;
            case 3:
                if (now - buttonPhaseStartedAt < 0.09) return false;
                if (closeArt.localScale.x > 0.965f)
                    throw new InvalidOperationException("Close artwork did not visibly compress on press.");
                ExecuteEvents.Execute(closeButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                buttonVerificationPhase = 4;
                buttonPhaseStartedAt = now;
                return false;
            case 4:
                if (now - buttonPhaseStartedAt < 0.09) return false;
                if (Mathf.Abs(closeArt.localScale.x - 1f) > 0.01f)
                    throw new InvalidOperationException("Close artwork did not rebound after release.");
                closeButton.GetComponent<Button>().onClick.Invoke();
                buttonVerificationPhase = 5;
                buttonPhaseStartedAt = now;
                return false;
            default:
                return false;
        }
    }

    private static class TextMeshProUGUIProxy
    {
        public static void VerifyLevelText(Transform root)
        {
            TMPro.TextMeshProUGUI text = FindNamedComponent<TMPro.TextMeshProUGUI>(
                root,
                "Victory Level Number");
            if (text == null || text.text != "Level 1")
                throw new InvalidOperationException("The live level number is missing or incorrect.");
        }
    }

    private static Camera CreateCaptureCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("New Victory UI Capture Camera");
        cameraObject.transform.SetParent(parent, false);
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
            for (int index = 0; index < roots.Length; index++) roots[index].SetActive(false);
        }
    }

    private static void SetRepresentativeObjectives(CarPrototype3D game)
    {
        SetPrivateInt(game, "activeRedTarget", 3);
        SetPrivateInt(game, "activeGreenTarget", 3);
        SetPrivateInt(game, "activeBlueTarget", 3);
        SetPrivateInt(game, "activePurpleTarget", 3);
        SetPrivateInt(game, "activeYellowTarget", 3);
        SetPrivateInt(game, "activePinkTarget", 3);
    }

    private static void SetPrivateInt(object target, string fieldName, int value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException($"Missing verifier field {fieldName}.");
        field.SetValue(target, value);
    }

    private static Transform FindNamedTransform(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
            if (transforms[index].name == name) return transforms[index];
        return null;
    }

    private static T FindNamedComponent<T>(Transform root, string name) where T : Component
    {
        Transform target = FindNamedTransform(root, name);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static void FinishCapture()
    {
        running = false;
        EditorPrefs.DeleteKey(ActiveKey);
        EditorApplication.update -= CaptureTick;
        if (captureTarget != null)
        {
            captureTarget.Release();
            UnityEngine.Object.Destroy(captureTarget);
        }
        if (verificationRoot != null) UnityEngine.Object.Destroy(verificationRoot);
        EditorApplication.ExitPlaymode();
    }

    private static void FailCapture(Exception exception)
    {
        Debug.LogException(exception);
        running = false;
        EditorPrefs.DeleteKey(ActiveKey);
        EditorApplication.update -= CaptureTick;
        if (Application.isBatchMode) EditorApplication.Exit(1);
        else EditorApplication.ExitPlaymode();
    }
}
#endif
