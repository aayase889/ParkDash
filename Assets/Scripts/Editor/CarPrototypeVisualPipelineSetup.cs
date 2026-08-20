using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Keeps the 3D prototype's dedicated URP renderer configured with the visual
/// features required by the runtime-built scene.
/// </summary>
public static class CarPrototypeVisualPipelineSetup
{
    private const string RendererPath = "Assets/Settings/Renderer3D.asset";
    private const string PipelinePath = "Assets/Settings/UniversalRP.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleConfiguration()
    {
        EditorApplication.delayCall -= EnsureConfigured;
        EditorApplication.delayCall += EnsureConfigured;
    }

    [MenuItem("Color Sort/Configure 3D Visual Pipeline")]
    public static void EnsureConfigured()
    {
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (rendererData == null || pipeline == null)
        {
            Debug.LogWarning("Could not configure the 3D visual pipeline because its URP assets are missing.");
            return;
        }

        bool changed = ConfigureAmbientOcclusion(rendererData);
        changed |= ConfigurePipeline(pipeline);

        rendererData.SetDirty();
        EditorUtility.SetDirty(rendererData);
        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("Configured mobile 3D rendering: half-resolution SSAO and a compact soft-shadow pass are enabled.");
        }
    }

    private static bool ConfigureAmbientOcclusion(UniversalRendererData rendererData)
    {
        ScreenSpaceAmbientOcclusion feature = rendererData.rendererFeatures
            .OfType<ScreenSpaceAmbientOcclusion>()
            .FirstOrDefault();
        bool changed = false;

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            feature.name = "Car Prototype Soft Ambient Occlusion";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            SerializedObject rendererObject = new SerializedObject(rendererData);
            rendererObject.Update();
            SerializedProperty features = rendererObject.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = rendererObject.FindProperty("m_RendererFeatureMap");
            int index = features.arraySize;
            features.InsertArrayElementAtIndex(index);
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
            featureMap.InsertArrayElementAtIndex(index);
            featureMap.GetArrayElementAtIndex(index).longValue = localId;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (!feature.isActive)
        {
            feature.SetActive(true);
            changed = true;
        }

        SerializedObject featureObject = new SerializedObject(feature);
        featureObject.Update();
        SerializedProperty settings = featureObject.FindProperty("m_Settings");
        changed |= SetEnum(settings.FindPropertyRelative("AOMethod"), 0); // Blue noise
        changed |= SetBool(settings.FindPropertyRelative("Downsample"), true);
        changed |= SetBool(settings.FindPropertyRelative("AfterOpaque"), false);
        changed |= SetEnum(settings.FindPropertyRelative("Source"), 1); // Depth normals
        changed |= SetEnum(settings.FindPropertyRelative("NormalSamples"), 0); // Low
        changed |= SetFloat(settings.FindPropertyRelative("Intensity"), 1.05f);
        changed |= SetFloat(settings.FindPropertyRelative("DirectLightingStrength"), 0.2f);
        changed |= SetFloat(settings.FindPropertyRelative("Radius"), 0.08f);
        changed |= SetEnum(settings.FindPropertyRelative("Samples"), 0); // Low
        changed |= SetEnum(settings.FindPropertyRelative("BlurQuality"), 0); // Low
        changed |= SetFloat(settings.FindPropertyRelative("Falloff"), 80f);
        featureObject.ApplyModifiedPropertiesWithoutUndo();
        feature.Create();
        EditorUtility.SetDirty(feature);
        return changed;
    }

    private static bool ConfigurePipeline(UniversalRenderPipelineAsset pipeline)
    {
        SerializedObject pipelineObject = new SerializedObject(pipeline);
        pipelineObject.Update();
        bool changed = false;
        changed |= SetBool(pipelineObject.FindProperty("m_SoftShadowsSupported"), true);
        changed |= SetInt(pipelineObject.FindProperty("m_MainLightShadowmapResolution"), 1024);
        changed |= SetInt(pipelineObject.FindProperty("m_ShadowCascadeCount"), 1);
        changed |= SetFloat(pipelineObject.FindProperty("m_ShadowDistance"), 24f);
        changed |= SetInt(pipelineObject.FindProperty("m_SoftShadowQuality"), 1);
        changed |= SetEnum(pipelineObject.FindProperty("m_AdditionalLightsRenderingMode"), 0);
        changed |= SetInt(pipelineObject.FindProperty("m_AdditionalLightsPerObjectLimit"), 0);
        changed |= SetBool(pipelineObject.FindProperty("m_SupportsLightCookies"), false);
        changed |= SetBool(pipelineObject.FindProperty("m_SupportDataDrivenLensFlare"), false);
        changed |= SetBool(pipelineObject.FindProperty("m_SupportScreenSpaceLensFlare"), false);
        changed |= SetEnum(pipelineObject.FindProperty("m_PrefilteringModeScreenSpaceOcclusion"), 1);
        pipelineObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return changed;
    }

    private static bool SetBool(SerializedProperty property, bool value)
    {
        if (property == null || property.boolValue == value) return false;
        property.boolValue = value;
        return true;
    }

    private static bool SetInt(SerializedProperty property, int value)
    {
        if (property == null || property.intValue == value) return false;
        property.intValue = value;
        return true;
    }

    private static bool SetEnum(SerializedProperty property, int value)
    {
        if (property == null || property.enumValueIndex == value) return false;
        property.enumValueIndex = value;
        return true;
    }

    private static bool SetFloat(SerializedProperty property, float value)
    {
        if (property == null || Mathf.Approximately(property.floatValue, value)) return false;
        property.floatValue = value;
        return true;
    }
}
