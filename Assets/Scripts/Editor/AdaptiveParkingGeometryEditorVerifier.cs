using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that every matching-tray capacity uses equal bays and that 5x5
/// boards scale the full parking geometry by the same factor as their cars.
/// Demo 2 remains the reference footprint.
/// </summary>
public static class AdaptiveParkingGeometryEditorVerifier
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const int PreviewLayer = 31;
    private const float EqualBayPixels = 182f;
    private const int SidewalkPixels = 220;
    private const string Demo2PreviewPath = "/private/tmp/adaptive_parking_demo2.png";
    private const string Demo3PreviewPath = "/private/tmp/adaptive_parking_demo3.png";
    private const string Demo4PreviewPath = "/private/tmp/adaptive_parking_demo4.png";

    [MenuItem("Car Prototype/Verify Adaptive Parking Geometry")]
    public static void RunBatch()
    {
        GameObject root = null;
        try
        {
            CarPrototypeHudLayout layout = CarPrototypeHudLayout.LoadOrDefault();
            Require(layout != null, "The 3D HUD layout asset is missing.");

            root = new GameObject("Adaptive Parking Geometry Verification");
            CarPrototype3D prototype = root.AddComponent<CarPrototype3D>();

            VerifySidewalkTextures();
            float demo2Spacing = VerifyDemo(
                prototype,
                layout,
                "Demo 2",
                4,
                3,
                layout.scenePieceScale4x4);
            float demo3Spacing = VerifyDemo(
                prototype,
                layout,
                "Demo 3",
                5,
                5,
                layout.scenePieceScale5x5);
            float demo4Spacing = VerifyDemo(
                prototype,
                layout,
                "Demo 4",
                5,
                4,
                layout.scenePieceScale5x5);

            Require(
                Mathf.Abs(demo3Spacing - demo4Spacing) < 0.0001f,
                "Demo 3 and Demo 4 do not use the same bay size for their equal-size 5x5 cars.");
            Require(
                Mathf.Abs(
                    demo3Spacing / demo2Spacing
                    - layout.scenePieceScale5x5 / layout.scenePieceScale4x4) < 0.0001f,
                "The later-demo bay scale does not match the later-demo car scale.");

            RenderDemoPreview(prototype, layout, 4, 3, layout.scenePieceScale4x4, Demo2PreviewPath);
            RenderDemoPreview(prototype, layout, 5, 5, layout.scenePieceScale5x5, Demo3PreviewPath);
            RenderDemoPreview(prototype, layout, 5, 4, layout.scenePieceScale5x5, Demo4PreviewPath);
            VerifyPortraitGuideSafeArea(prototype, layout);

            Debug.Log(
                "[Adaptive Parking Geometry Verification] PASS: Demo 2 keeps its reference footprint; "
                + "Demo 3 and Demo 4 scale every bay with their 5x5 cars; all capacities use equal "
                + "182-pixel sidewalk openings and mathematically centered car positions. previews="
                + $"{Demo2PreviewPath},{Demo3PreviewPath},{Demo4PreviewPath}");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifySidewalkTextures()
    {
        for (int capacity = 2; capacity <= 5; capacity++)
        {
            Texture2D texture = Resources.Load<Texture2D>(
                $"Environment/ApprovedParkingSidewalk_{capacity}");
            Require(texture != null, $"The {capacity}-space sidewalk texture is missing.");
            int expectedWidth = SidewalkPixels + Mathf.RoundToInt(EqualBayPixels) * capacity;
            Require(
                texture.width == expectedWidth && texture.height == 543,
                $"The {capacity}-space sidewalk is {texture.width}x{texture.height}; expected "
                + $"{expectedWidth}x543 for {capacity} equal bays.");
        }
    }

    private static float VerifyDemo(
        CarPrototype3D prototype,
        CarPrototypeHudLayout layout,
        string label,
        int boardSize,
        int capacity,
        float carScale)
    {
        SetPrivateField(prototype, "activeBoardSize", boardSize);
        SetPrivateField(prototype, "trayCapacity", capacity);

        float geometryScale = InvokePrivate<float>(prototype, "GetParkingGeometryScale");
        float spacing = InvokePrivate<float>(
            prototype,
            "GetMatchTraySlotSpacing",
            new[] { typeof(int) },
            capacity);
        Vector2 baySize = InvokePrivate<Vector2>(
            prototype,
            "GetMatchTrayBaySize",
            new[] { typeof(int) },
            capacity);

        float expectedScale = Mathf.Clamp(
            carScale / Mathf.Max(0.3f, layout.scenePieceScale4x4),
            0.55f,
            1f);
        Require(
            Mathf.Abs(geometryScale - expectedScale) < 0.0001f,
            $"{label} parking scale {geometryScale:F4} does not match its car scale {expectedScale:F4}.");
        Require(
            Mathf.Abs(spacing - layout.sceneMatchTraySlotSpacing * expectedScale) < 0.0001f,
            $"{label} bay spacing is not proportional to its cars.");
        Require(
            Vector2.Distance(baySize, layout.sceneMatchTrayBaySize * expectedScale) < 0.0001f,
            $"{label} bay width/depth is not uniformly scaled.");

        float previousX = float.NaN;
        for (int index = 0; index < capacity; index++)
        {
            Vector3 position = InvokePrivate<Vector3>(
                prototype,
                "GetTraySlotPosition",
                new[] { typeof(int), typeof(int) },
                index,
                capacity);
            float expectedX = layout.sceneMatchTrayPosition.x
                + (index - (capacity - 1) * 0.5f) * spacing;
            Require(
                Mathf.Abs(position.x - expectedX) < 0.0001f,
                $"{label} space {index + 1} is not centered in its equal bay.");
            if (!float.IsNaN(previousX))
            {
                Require(
                    Mathf.Abs(position.x - previousX - spacing) < 0.0001f,
                    $"{label} has unequal neighboring parking spaces.");
            }
            previousX = position.x;
        }

        return spacing;
    }

    private static void VerifyPortraitGuideSafeArea(
        CarPrototype3D prototype,
        CarPrototypeHudLayout layout)
    {
        GameObject cameraObject = null;
        RenderTexture portraitTarget = null;
        try
        {
            cameraObject = new GameObject("Adaptive Parking Portrait Safe-Area Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = layout.sceneCameraOrthographicSize;
            portraitTarget = new RenderTexture(768, 1454, 16);
            camera.targetTexture = portraitTarget;
            SetPrivateField(prototype, "prototypeCamera", camera);

            VerifyGuideOffset(prototype, layout, 4, 3, camera, "Demo 2");
            VerifyGuideOffset(prototype, layout, 5, 5, camera, "Demo 3");
            VerifyGuideOffset(prototype, layout, 5, 4, camera, "Demo 4");
        }
        finally
        {
            SetPrivateField(prototype, "prototypeCamera", null);
            if (cameraObject != null)
                cameraObject.GetComponent<Camera>().targetTexture = null;
            if (portraitTarget != null)
            {
                portraitTarget.Release();
                UnityEngine.Object.DestroyImmediate(portraitTarget);
            }
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void VerifyGuideOffset(
        CarPrototype3D prototype,
        CarPrototypeHudLayout layout,
        int boardSize,
        int capacity,
        Camera camera,
        string label)
    {
        SetPrivateField(prototype, "activeBoardSize", boardSize);
        SetPrivateField(prototype, "trayCapacity", capacity);
        MethodInfo method = typeof(CarPrototype3D).GetMethod(
            "TryGetSideRoadGuideGeometry",
            PrivateInstance);
        Require(method != null, "Missing side-road guide geometry method.");
        object[] arguments = { layout, null };
        Require((bool)method.Invoke(prototype, arguments), $"{label} side-road guide geometry failed.");
        object geometry = arguments[1];
        FieldInfo offsetField = geometry.GetType().GetField("guideOffset", PrivateInstance);
        Require(offsetField != null, "Missing side-road guide offset field.");
        float guideOffset = (float)offsetField.GetValue(geometry);
        float safeHalfWidth = camera.orthographicSize * camera.aspect - 0.28f;
        Require(
            guideOffset <= safeHalfWidth + 0.0001f,
            $"{label} side-road guide is outside the portrait camera safe area.");
    }

    private static void RenderDemoPreview(
        CarPrototype3D prototype,
        CarPrototypeHudLayout layout,
        int boardSize,
        int capacity,
        float carScale,
        string outputPath)
    {
        GameObject carsRoot = null;
        GameObject floor = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        Material floorMaterial = null;
        Material sidewalkPreviewMaterial = null;
        RenderTexture renderTexture = null;
        Texture2D screenshot = null;
        Transform sidewalkRoot = null;
        Transform dividerRoot = null;
        try
        {
            SetPrivateField(prototype, "activeBoardSize", boardSize);
            SetPrivateField(prototype, "trayCapacity", capacity);
            InvokePrivate(prototype, "CreateMatchTray");
            sidewalkRoot = GetPrivateField<Transform>(prototype, "matchTrayRootTransform");
            dividerRoot = GetPrivateField<Transform>(prototype, "matchTrayDividerRootTransform");
            Renderer sidewalkRenderer = sidewalkRoot.GetComponentInChildren<Renderer>(true);
            Texture2D sidewalkTexture = Resources.Load<Texture2D>(
                $"Environment/ApprovedParkingSidewalk_{capacity}");
            Shader sidewalkShader = Shader.Find("Unlit/Transparent");
            if (sidewalkShader == null) sidewalkShader = Shader.Find("Sprites/Default");
            Require(sidewalkRenderer != null && sidewalkTexture != null && sidewalkShader != null,
                "The sidewalk preview renderer could not be configured.");
            sidewalkPreviewMaterial = new Material(sidewalkShader)
            {
                mainTexture = sidewalkTexture,
                color = Color.white,
                renderQueue = 3000,
                hideFlags = HideFlags.HideAndDontSave
            };
            sidewalkRenderer.sharedMaterial = sidewalkPreviewMaterial;
            sidewalkRenderer.sortingOrder = 0;

            carsRoot = new GameObject($"Demo {capacity} Parking Preview Cars");
            string[] colors = { "Red", "Purple", "Yellow", "Blue", "Green" };
            for (int index = 0; index < capacity; index++)
            {
                Vector3 slot = InvokePrivate<Vector3>(
                    prototype,
                    "GetTraySlotPosition",
                    new[] { typeof(int), typeof(int) },
                    index,
                    capacity);
                CarPuzzlePiece car = CreatePreviewCar(
                    carsRoot.transform,
                    colors[index],
                    slot,
                    carScale);
                InvokePrivate(
                    car,
                    "SetTrayPose",
                    new[] { typeof(Vector3), typeof(bool) },
                    slot,
                    false);
            }

            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Adaptive Parking Preview Asphalt";
            floor.transform.position = new Vector3(0f, -0.02f, layout.sceneMatchTrayPosition.y);
            floor.transform.localScale = new Vector3(1.25f, 1f, 0.72f);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            floorMaterial = new Material(shader)
            {
                color = new Color(0.14f, 0.18f, 0.24f, 1f),
                hideFlags = HideFlags.HideAndDontSave
            };
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            lightObject = new GameObject("Adaptive Parking Preview Light", typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.cullingMask = 1 << PreviewLayer;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            cameraObject = new GameObject("Adaptive Parking Preview Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.25f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            camera.transform.position = new Vector3(0f, 7.5f, -9.0f);
            camera.transform.LookAt(new Vector3(0f, 0.15f, layout.sceneMatchTrayPosition.y - 0.25f));

            SetLayerRecursively(sidewalkRoot, PreviewLayer);
            SetLayerRecursively(dividerRoot, PreviewLayer);
            SetLayerRecursively(carsRoot.transform, PreviewLayer);
            SetLayerRecursively(floor.transform, PreviewLayer);
            lightObject.layer = PreviewLayer;

            renderTexture = new RenderTexture(1200, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            screenshot = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RGBA32,
                false);
            screenshot.ReadPixels(
                new Rect(0f, 0f, renderTexture.width, renderTexture.height),
                0,
                0);
            screenshot.Apply();
            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
        }
        finally
        {
            if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
            if (sidewalkPreviewMaterial != null) UnityEngine.Object.DestroyImmediate(sidewalkPreviewMaterial);
            if (carsRoot != null) UnityEngine.Object.DestroyImmediate(carsRoot);
            if (sidewalkRoot != null) UnityEngine.Object.DestroyImmediate(sidewalkRoot.gameObject);
            if (dividerRoot != null) UnityEngine.Object.DestroyImmediate(dividerRoot.gameObject);
        }
    }

    private static CarPuzzlePiece CreatePreviewCar(
        Transform parent,
        string colorName,
        Vector3 position,
        float carScale)
    {
        GameObject carObject = new GameObject($"{colorName} Preview Car");
        carObject.transform.SetParent(parent, false);
        CarPuzzlePiece car = carObject.AddComponent<CarPuzzlePiece>();
        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod("Configure", PrivateInstance);
        Type colorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        Require(configure != null && colorType != null && directionType != null,
            "The preview car configuration API is missing.");
        configure.Invoke(car, new object[]
        {
            0,
            0,
            Enum.Parse(colorType, colorName),
            Enum.Parse(directionType, "Down"),
            position,
            carScale,
            carScale / 0.68f,
            1,
            false,
            false
        });
        return car;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int index = 0; index < root.childCount; index++)
            SetLayerRecursively(root.GetChild(index), layer);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Require(field != null, $"Missing private field {fieldName}.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Require(field != null, $"Missing private field {fieldName}.");
        return (T)field.GetValue(target);
    }

    private static void InvokePrivate(
        object target,
        string methodName,
        Type[] parameterTypes,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance,
            null,
            parameterTypes,
            null);
        Require(method != null, $"Missing private method {methodName}.");
        method.Invoke(target, arguments);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        InvokePrivate(target, methodName, Type.EmptyTypes);
    }

    private static T InvokePrivate<T>(object target, string methodName)
    {
        return InvokePrivate<T>(target, methodName, Type.EmptyTypes);
    }

    private static T InvokePrivate<T>(
        object target,
        string methodName,
        Type[] parameterTypes,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance,
            null,
            parameterTypes,
            null);
        Require(method != null, $"Missing private method {methodName}.");
        return (T)method.Invoke(target, arguments);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
