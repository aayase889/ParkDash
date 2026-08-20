#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Level50MysteryBoxEditorVerifier
{
    private const string CorrectTextureResourcePath =
        "CarPrototype/Mechanics/MysteryBox";
    private const string PreviewPath =
        "/Users/ayseakbal/.codex/visualizations/2026/08/13/019ffae9-834c-7852-9779-1c653f0b5d45/level50-box-unity-preview.png";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Car Prototype/Verify Global Mystery Box on Level 50")]
    public static void RunBatch()
    {
        GameObject root = null;
        RenderTexture target = null;
        Texture2D screenshot = null;
        try
        {
            Texture2D correctTexture = Resources.Load<Texture2D>(CorrectTextureResourcePath);
            if (correctTexture == null)
                throw new InvalidOperationException(
                    $"Correct Level 50 texture is missing at Resources/{CorrectTextureResourcePath}.");

            root = new GameObject("Level 50 Mystery Box Verification");
            Camera camera = CreateCamera(root.transform);
            CreateLighting(root.transform);
            CreateFloor(root.transform);

            CarPuzzlePiece referenceCar = CreatePiece(
                root.transform,
                new Vector3(-1.6f, 0.35f, -1.1f),
                false,
                false);
            CarPuzzlePiece level50Box = CreatePiece(
                root.transform,
                new Vector3(0f, 0.35f, -1.1f),
                true,
                true);
            CarPuzzlePiece regularBox = CreatePiece(
                root.transform,
                new Vector3(1.6f, 0.35f, -1.1f),
                true,
                false);

            VerifyIsolation(level50Box, regularBox);
            VerifyArtworkPresentation(level50Box);

            // Keep the saved preview unambiguous: the isolation checks above
            // still exercise the regular car and 2D box, but the image itself
            // shows only the Level 50 replacement at the exact camera angle.
            referenceCar.gameObject.SetActive(false);
            regularBox.gameObject.SetActive(false);

            target = new RenderTexture(1000, 700, 24, RenderTextureFormat.ARGB32);
            target.Create();
            if (!target.IsCreated())
                throw new InvalidOperationException("Could not create the Level 50 box RenderTexture.");
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            screenshot = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            screenshot.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, screenshot.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;

            Debug.Log(
                "GLOBAL LEVEL 50 MYSTERY BOX VERIFICATION PASSED: "
                + $"resource={CorrectTextureResourcePath}, preview={PreviewPath}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        if (Application.isBatchMode) EditorApplication.Exit(0);
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

    private static CarPuzzlePiece CreatePiece(
        Transform parent,
        Vector3 position,
        bool revealBox,
        bool useLevel50Visual)
    {
        GameObject pieceObject = new GameObject(
            useLevel50Visual ? "Level 50 Box Piece" : revealBox ? "Regular Box Piece" : "Car Piece");
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();

        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod("Configure", PrivateInstance);
        Type colorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        if (configure == null || colorType == null || directionType == null)
            throw new MissingMethodException("CarPuzzlePiece.Configure could not be found.");

        configure.Invoke(piece, new object[]
        {
            0,
            0,
            Enum.Parse(colorType, "Blue"),
            Enum.Parse(directionType, "Down"),
            position,
            0.53f,
            0.53f,
            1,
            revealBox,
            false
        });

        if (useLevel50Visual)
        {
            MethodInfo useLevel50 = typeof(CarPuzzlePiece).GetMethod(
                "UseLevel50MysteryBoxVisual",
                PrivateInstance);
            if (useLevel50 == null)
                throw new MissingMethodException("Level 50 box visual API is missing.");
            useLevel50.Invoke(piece, null);
        }

        return piece;
    }

    private static void VerifyIsolation(CarPuzzlePiece level50Box, CarPuzzlePiece regularBox)
    {
        Transform level50Artwork = FindDescendant(level50Box.transform, "Approved Mystery Box Artwork");
        Transform regularArtwork = FindDescendant(regularBox.transform, "Approved Mystery Box Artwork");
        if (level50Box.transform.Find("Mystery Box Visual") == null || level50Artwork == null)
            throw new InvalidOperationException("Level 50 is not using the shared mystery-box artwork.");
        if (regularBox.transform.Find("Mystery Box Visual") == null || regularArtwork == null)
            throw new InvalidOperationException("A regular level is not using the shared mystery-box artwork.");
        if (level50Box.transform.Find("Level 50 3D Mystery Box Visual") != null
            || regularBox.transform.Find("Level 50 3D Mystery Box Visual") != null)
            throw new InvalidOperationException("A legacy Level 50 3D box visual is still active.");
        if (Vector3.Distance(level50Artwork.localScale, regularArtwork.localScale) > 0.001f)
            throw new InvalidOperationException("Level 50 and regular boxes do not share the same scale.");
    }

    private static void VerifyArtworkPresentation(CarPuzzlePiece level50Box)
    {
        Transform visual = level50Box.transform.Find("Mystery Box Visual");
        if (visual == null) throw new InvalidOperationException("Level 50 visual root is missing.");
        Transform artwork = FindDescendant(visual, "Approved Mystery Box Artwork");
        if (artwork == null)
            throw new InvalidOperationException("The corrected global box artwork is missing.");

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("The Level 50 box contains no renderers.");

        Texture2D expectedTexture = Resources.Load<Texture2D>(CorrectTextureResourcePath);

        if (Mathf.Abs(artwork.localScale.y - 3.08f) > 0.02f)
            throw new InvalidOperationException(
                $"Global mystery-box scale is wrong: {artwork.localScale.y:0.000}.");

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer.GetComponent<Collider>() != null)
                throw new InvalidOperationException("An imported FBX collider was left on the visual.");
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || !material.shader.isSupported)
                    throw new InvalidOperationException("The global box has a missing or unsupported material.");
                if (material.mainTexture != expectedTexture)
                    throw new InvalidOperationException(
                        "Level 50 is not using the corrected global box texture.");
            }
        }
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;
        return null;
    }
}
#endif
