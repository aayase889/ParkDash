using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
internal static class CarPrototypeRenderSetup
{
    private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
    private const string RendererPath = "Assets/Settings/Renderer3D.asset";
    private const string RendererTemplatePath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset";

    static CarPrototypeRenderSetup()
    {
        EditorApplication.delayCall += EnsurePrototypeRenderer;
    }

    [MenuItem("Car Prototype/Ensure 3D Renderer")]
    public static void EnsurePrototypeRenderer()
    {
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null) return;

        UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            if (!AssetDatabase.CopyAsset(RendererTemplatePath, RendererPath))
            {
                Debug.LogError("Could not create the isolated 3D renderer for the car prototype.");
                return;
            }

            renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null) return;
            renderer.name = "Renderer3D";
            EditorUtility.SetDirty(renderer);
        }

        SerializedObject pipelineObject = new SerializedObject(pipeline);
        SerializedProperty renderers = pipelineObject.FindProperty("m_RendererDataList");
        bool alreadyAdded = false;
        for (int index = 0; index < renderers.arraySize; index++)
        {
            if (renderers.GetArrayElementAtIndex(index).objectReferenceValue == renderer)
            {
                alreadyAdded = true;
                break;
            }
        }

        if (!alreadyAdded)
        {
            int nextIndex = renderers.arraySize;
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(nextIndex).objectReferenceValue = renderer;
            pipelineObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Car Prototype/Open 3D Test Scene")]
    private static void OpenPrototypeScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CarPrototype3D.unity");
    }
}
