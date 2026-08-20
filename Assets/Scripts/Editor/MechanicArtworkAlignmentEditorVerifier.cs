#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a regular car, garage, and mystery box at the exact same board
/// scale and camera pitch. This prevents mechanic sprites from regressing to
/// a small, sideways, or double-foreshortened presentation.
/// </summary>
public static class MechanicArtworkAlignmentEditorVerifier
{
    private const string PreviewPath = "/private/tmp/mechanic_art_alignment_unity.png";
    // Derived from CarPrototypeHudLayout.asset:
    // camera=(0,15.4,-8.7), lookAt=(0,0,-1.1).
    private const float ExpectedCameraElevationDegrees = 63.733362f;
    private const float ExpectedTopToFrontProjectionRatio = 2.026f;

    [MenuItem("Tools/Car Prototype/Verify Mechanic Artwork Alignment")]
    public static void RunBatch()
    {
        GameObject root = null;
        RenderTexture target = null;
        Texture2D screenshot = null;
        try
        {
            root = new GameObject("Mechanic Artwork Alignment Verification");
            Camera camera = CreateCamera(root.transform);
            CreateLighting(root.transform);
            CreateFloor(root.transform);
            VerifyMeasuredCameraPitch(camera);

            CreateCar(root.transform, new Vector3(-1.85f, 0.35f, -1.1f), false);
            CreateGarage(root.transform, new Vector3(0f, 0.39f, -1.1f));
            CreateCar(root.transform, new Vector3(1.85f, 0.35f, -1.1f), true);

            VerifyMechanicTransforms(root.transform, camera);

            target = new RenderTexture(1000, 620, 24, RenderTextureFormat.ARGB32);
            target.Create();
            if (!target.IsCreated())
                throw new InvalidOperationException("Could not create the mechanic alignment RenderTexture.");
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            screenshot = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(PreviewPath, screenshot.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;

            Debug.Log(
                "MECHANIC ARTWORK ALIGNMENT VERIFICATION PASSED: "
                + $"uniformSize={GetApprovedDisplaySize():0.00}, preview={PreviewPath}");
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
        GameObject cameraObject = new GameObject("Prototype Camera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.3f;
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

    private static void CreateGarage(Transform parent, Vector3 position)
    {
        GameObject garageObject = new GameObject("Garage Preview");
        garageObject.transform.SetParent(parent, false);
        garageObject.transform.position = position;
        GaragePuzzleVisual visual = garageObject.AddComponent<GaragePuzzleVisual>();
        MethodInfo configure = typeof(GaragePuzzleVisual).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo materialMethod = typeof(CarPrototype3D).GetMethod(
            "GetApprovedMechanicMaterial",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (configure == null || materialMethod == null)
            throw new MissingMethodException("Garage artwork configuration API is missing.");
        Material open = (Material)materialMethod.Invoke(
            null, new object[] { "CarPrototype/Mechanics/GarageOpen" });
        Material closed = (Material)materialMethod.Invoke(
            null, new object[] { "CarPrototype/Mechanics/GarageClosed" });
        configure.Invoke(visual, new object[] { open, closed, 0.53f });
    }

    private static void CreateCar(Transform parent, Vector3 position, bool revealBox)
    {
        GameObject pieceObject = new GameObject(revealBox ? "Mystery Box Preview" : "Car Preview");
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();
        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Type colorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        if (configure == null || colorType == null || directionType == null)
            throw new MissingMethodException("Car artwork configuration API is missing.");
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
            -1,
            -1,
            false,
            false
        });
    }

    private static void VerifyMechanicTransforms(Transform root, Camera camera)
    {
        string[] names = { "Approved Open Garage", "Approved Mystery Box Artwork" };
        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            Transform artwork = FindDeepChild(root, names[nameIndex]);
            if (artwork == null)
                throw new InvalidOperationException($"Missing {names[nameIndex]} in alignment preview.");
            if (Mathf.Abs(artwork.localScale.x - artwork.localScale.y) > 0.0001f)
                throw new InvalidOperationException($"{names[nameIndex]} is stretched non-uniformly.");
            if (Mathf.Abs(artwork.localScale.x - GetApprovedDisplaySize()) > 0.0001f)
                throw new InvalidOperationException($"{names[nameIndex]} does not use the approved car-sized scale.");
            if (Vector3.Dot(artwork.forward, camera.transform.forward) < 0.999f)
                throw new InvalidOperationException($"{names[nameIndex]} does not face the game camera.");
        }
    }

    private static void VerifyMeasuredCameraPitch(Camera camera)
    {
        Vector3 forward = camera.transform.forward.normalized;
        float horizontal = new Vector2(forward.x, forward.z).magnitude;
        float elevation = Mathf.Atan2(Mathf.Abs(forward.y), horizontal) * Mathf.Rad2Deg;
        if (Mathf.Abs(elevation - ExpectedCameraElevationDegrees) > 0.001f)
            throw new InvalidOperationException(
                $"Mechanic preview camera elevation drifted to {elevation:0.000000} degrees.");

        // Under this orthographic camera, one unit of ground depth projects by
        // sin(elevation), while one unit of front-wall height projects by
        // cos(elevation). Their ratio is the measurable sprite-angle target.
        float projectionRatio = Mathf.Sin(elevation * Mathf.Deg2Rad)
            / Mathf.Cos(elevation * Mathf.Deg2Rad);
        if (Mathf.Abs(projectionRatio - ExpectedTopToFrontProjectionRatio) > 0.001f)
            throw new InvalidOperationException(
                $"Mechanic top/front projection ratio drifted to {projectionRatio:0.000}.");
    }

    private static float GetApprovedDisplaySize()
    {
        FieldInfo field = typeof(CarPrototype3D).GetField(
            "MechanicArtworkDisplaySize",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            throw new MissingFieldException(typeof(CarPrototype3D).FullName, "MechanicArtworkDisplaySize");
        return (float)field.GetRawConstantValue();
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < children.Length; index++)
            if (children[index].name == objectName) return children[index];
        return null;
    }
}
#endif
