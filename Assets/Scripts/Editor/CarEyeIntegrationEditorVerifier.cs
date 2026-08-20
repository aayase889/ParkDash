using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only verification and inspection render for the integrated
/// windshield eyes. It does not enter Play Mode.
/// </summary>
public static class CarEyeIntegrationEditorVerifier
{
    private const string PreviewPath = "/tmp/cargame3_integrated_windshield_eyes.png";

    public static void RunBatch()
    {
        GameObject root = null;
        RenderTexture renderTexture = null;
        Texture2D screenshot = null;
        try
        {
            root = new GameObject("Integrated Windshield Eye Verification");
            CarPuzzlePiece redToward = CreatePiece(
                root.transform,
                "Red Toward Preview Car",
                "Red",
                "Down",
                new Vector3(-1.15f, 0f, -0.90f));
            CarPuzzlePiece redAway = CreatePiece(
                root.transform,
                "Red Away Preview Car",
                "Red",
                "Up",
                new Vector3(-1.15f, 0f, 0.90f));
            CarPuzzlePiece greenLeft = CreatePiece(
                root.transform,
                "Green Left Preview Car",
                "Green",
                "Left",
                new Vector3(1.15f, 0f, 0.90f));
            CarPuzzlePiece greenRight = CreatePiece(
                root.transform,
                "Green Right Preview Car",
                "Green",
                "Right",
                new Vector3(1.15f, 0f, -0.90f));

            VerifyIntegratedRig(redToward);
            VerifyIntegratedRig(redAway);
            VerifyIntegratedRig(greenLeft);
            VerifyIntegratedRig(greenRight);

            GameObject lightObject = new GameObject("Preview Directional Light");
            lightObject.transform.SetParent(root.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            GameObject fillObject = new GameObject("Preview Fill Light");
            fillObject.transform.SetParent(root.transform, false);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.transform.rotation = Quaternion.Euler(70f, 150f, 0f);

            GameObject cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.23f, 0.28f);
            camera.transform.position = new Vector3(0f, 9f, -5.1f);
            camera.transform.LookAt(new Vector3(0f, 0f, 0f));

            renderTexture = new RenderTexture(1200, 700, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(PreviewPath, screenshot.EncodeToPNG());
            RenderTexture.active = previous;

            Debug.Log($"[Integrated Windshield Eye Verification] PASS: windshield surfaces replace floating whites, pupils retain animation, preview={PreviewPath}");
        }
        finally
        {
            if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CarPuzzlePiece CreatePiece(
        Transform parent,
        string objectName,
        string colorName,
        string directionName,
        Vector3 position)
    {
        GameObject pieceObject = new GameObject(objectName);
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();
        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (configure == null)
            throw new InvalidOperationException("CarPuzzlePiece.Configure could not be found.");

        Type pieceColorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        if (pieceColorType == null || directionType == null)
            throw new InvalidOperationException("The car prototype color or direction enum could not be found.");

        configure.Invoke(piece, new object[]
        {
            0,
            0,
            Enum.Parse(pieceColorType, colorName),
            Enum.Parse(directionType, directionName),
            position,
            1f,
            1f,
            1,
            false,
            -1,
            -1,
            false,
            false
        });
        return piece;
    }

    private static void VerifyIntegratedRig(CarPuzzlePiece piece)
    {
        CarEyeController controller = piece.GetComponent<CarEyeController>();
        Require(controller != null, $"{piece.name} has no eye controller.");

        Transform surface = FindDeepChild(piece.transform, "Windshield Eye Surface");
        Transform leftPupil = FindDeepChild(piece.transform, "Left Pupil");
        Transform rightPupil = FindDeepChild(piece.transform, "Right Pupil");
        Require(surface != null, $"{piece.name} is missing its integrated windshield surface.");
        Require(leftPupil != null && rightPupil != null, $"{piece.name} is missing animated pupils.");
        Require(FindDeepChild(piece.transform, "Left Eye White") == null, $"{piece.name} still contains a floating left eye white.");
        Require(FindDeepChild(piece.transform, "Right Eye White") == null, $"{piece.name} still contains a floating right eye white.");

        float pupilHeight = Mathf.Max(leftPupil.localPosition.y, rightPupil.localPosition.y);
        Require(pupilHeight <= 0.065f, $"{piece.name} pupils are raised too far above the windshield.");
        float windshieldTilt = surface.parent.localEulerAngles.x;
        Require(
            windshieldTilt >= 60f && windshieldTilt <= 80f,
            $"{piece.name} eye surface is not aligned to its measured windshield plane.");
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < children.Length; index++)
            if (children[index].name == objectName)
                return children[index];
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
