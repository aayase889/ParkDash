using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CarPrototypeHudLayoutEditor : EditorWindow
{
    private const string LayoutPath = "Assets/Resources/CarPrototypeHudLayout.asset";
    private const string ScenePath = "Assets/Scenes/CarPrototype3D.unity";

    private CarPrototypeHudLayout layout;
    private SerializedObject serializedLayout;
    private Vector2 scroll;
    private bool autoApply = true;
    private string lastStatus;
    private MessageType lastStatusType = MessageType.Info;

    [MenuItem("Color Sort/3D Car HUD Layout Editor")]
    [MenuItem("Car Prototype/3D HUD Layout Editor")]
    [MenuItem("Window/Color Sort/HUD Layout Editor")]
    [MenuItem("Window/Color Sort/3D Car HUD Layout Editor")]
    public static void Open()
    {
        CarPrototypeHudLayoutEditor window = GetWindow<CarPrototypeHudLayoutEditor>("3D Car Editor");
        window.minSize = new Vector2(430f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadLayout();
        Undo.undoRedoPerformed += HandleUndoRedo;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (layout == null || serializedLayout == null)
        {
            EditorGUILayout.HelpBox("The 3D HUD layout asset could not be loaded.", MessageType.Error);
            if (GUILayout.Button("Create Or Reload 3D HUD Layout", GUILayout.Height(30f))) LoadLayout();
            return;
        }

        serializedLayout.UpdateIfRequiredOrScript();
        EditorGUI.BeginChangeCheck();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("3D Camera", "sceneCameraPosition", "sceneCameraLookAt", "sceneCameraOrthographicSize");
        DrawSection("3D Board Pieces", "sceneBoardFirstRowZ", "scenePieceScale3x3", "scenePieceScale4x4", "scenePieceScale5x5", "sceneLimousineOffBoardScale");
        DrawSection("3D Asphalt Environment", "sceneAsphaltGroundPosition", "sceneAsphaltGroundSize", "sceneRoadCenterZ", "sceneRoadDepth", "sceneBackgroundAsphaltColor", "scenePlayfieldAsphaltColor", "sceneRoadBorderColor");
        DrawSection("3D Parking Markings", "sceneMatchTrayPosition", "sceneMatchTraySlotSpacing", "sceneMatchTrayBaySize", "sceneSideParkingPosition", "sceneSideParkingSize", "sceneParkingLineWidth", "sceneSideParkingHatchSpacing");
        DrawSection("Compact Top HUD", "levelPillPosition", "levelPillSize", "levelTextPosition", "levelTextSize", "levelTextFontSize");
        DrawSection("Main Menu", "mainMenuPlayPosition", "mainMenuPlaySize", "mainMenuPlayText", "mainMenuPlayFontSize", "mainMenuShopTabPosition", "mainMenuHomeTabPosition", "mainMenuLockedTabPosition", "mainMenuTabSize", "mainMenuTallTabSize", "mainMenuSelectedTabOffset", "mainMenuSelectedLabelOffset", "mainMenuSelectedLabelSize", "mainMenuSelectedLabelFontSize");
        DrawSection("Car Objectives",
            "redObjectiveCarPosition", "greenObjectiveCarPosition", "blueObjectiveCarPosition", "yellowObjectiveCarPosition",
            "objectiveCarSize",
            "redObjectiveStatusPosition", "greenObjectiveStatusPosition", "blueObjectiveStatusPosition", "yellowObjectiveStatusPosition",
            "objectiveStatusSize", "objectiveCheckSize", "objectiveStatusFontSize");
        DrawSection("Lives",
            "heartsPosition", "heartSize", "heartSpacing", "heartLossFallDistance", "heartLossDuration");
        DrawSection("Bottom Controls", "extraSlotPosition", "undoPosition", "boosterSize", "pausePosition", "pauseSize");
        DrawSection("Level Preview Navigation", "showLevelPreviewButtons", "previousLevelPosition", "nextLevelPosition", "levelPreviewButtonSize");
        DrawSection("Settings", "settingsPanelPosition", "settingsPanelSize", "settingsTitlePosition", "hapticsPosition", "soundsPosition", "musicPosition", "resumePosition", "quitPosition", "morePosition", "settingsClosePosition");
        DrawSection("More Page", "morePanelPosition", "morePanelSize", "moreTitlePosition", "termsPosition", "privacyPosition", "moreBackPosition", "moreClosePosition");
        DrawSection("Leave Confirmation", "leavePanelSize", "leaveTitlePosition", "leaveDescriptionPosition", "leaveCancelPosition", "leaveConfirmPosition");
        DrawSection("Font", "fontOverride");
        EditorGUILayout.EndScrollView();

        bool changed = EditorGUI.EndChangeCheck();
        if (changed)
        {
            serializedLayout.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            lastStatus = "Game layout changed. Save Layout writes it to disk.";
            lastStatusType = MessageType.Info;

            if (autoApply && EditorApplication.isPlaying)
                ApplyToRunningGame(layout, false);
        }

        DrawActions();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("3D Car Game Editor", EditorStyles.largeLabel);
        EditorGUILayout.HelpBox(
            "Edit the 3D camera, board pieces, asphalt environment, parking markings, and HUD while the car scene is in Play Mode. Auto Apply updates the Game view immediately; Save Layout keeps the values after Play Mode ends.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Layout Asset", layout, typeof(CarPrototypeHudLayout), false);

            if (GUILayout.Button("Ping", GUILayout.Width(48f)) && layout != null)
                EditorGUIUtility.PingObject(layout);
        }

        autoApply = EditorGUILayout.ToggleLeft("Auto Apply While Playing", autoApply);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Saved edits will be used next time the 3D scene starts. Enter Play Mode for a live preview.", MessageType.Warning);
        }
        else if (FindRunningHud() == null)
        {
            EditorGUILayout.HelpBox("Play Mode is running, but the 3D car HUD is not in the active scene. Open CarPrototype3D and start Play Mode.", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("Connected to the running 3D car game. Scene and HUD live editing are ready.", MessageType.Info);
        }

        if (!string.IsNullOrEmpty(lastStatus))
            EditorGUILayout.HelpBox(lastStatus, lastStatusType);
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Layout", GUILayout.Height(30f))) SaveLayout();

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || FindRunningHud() == null))
            {
                if (GUILayout.Button("Apply Now", GUILayout.Height(30f)))
                    ApplyToRunningGame(layout, true);
            }

            if (GUILayout.Button("Reset Defaults", GUILayout.Height(30f))) ResetDefaults();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open 3D Scene")) OpenPrototypeScene();

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || FindRunningHud() == null))
            {
                if (GUILayout.Button("Previous Board")) RunHudAction(hud => hud.EditorLoadPreviousBoard(), "Loaded the previous board.");
                if (GUILayout.Button("Next Board")) RunHudAction(hud => hud.EditorLoadNextBoard(), "Loaded the next board.");
            }
        }

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || FindRunningHud() == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Main Menu")) RunHudAction(hud => hud.EditorShowMainMenuPreview(), "Showing main-menu preview.");
            if (GUILayout.Button("Gameplay")) RunHudAction(hud => hud.EditorShowGameplayPreview(), "Showing gameplay HUD.");
            if (GUILayout.Button("Settings")) RunHudAction(hud => hud.EditorShowSettingsPreview(), "Showing Settings preview.");
            if (GUILayout.Button("More")) RunHudAction(hud => hud.EditorShowMorePreview(), "Showing More-page preview.");
            if (GUILayout.Button("Leave")) RunHudAction(hud => hud.EditorShowLeavePreview(), "Showing leave-confirmation preview.");
            if (GUILayout.Button("Loss")) RunHudAction(hud => hud.EditorShowDefeatPreview(), "Showing loss-popup preview.");
            if (GUILayout.Button("Win")) RunHudAction(hud => hud.EditorShowVictoryPreview(), "Showing win-popup preview.");
            if (GUILayout.Button("Traffic Jam")) RunHudAction(hud => hud.EditorShowTrafficJamPreview(), "Showing traffic-jam rescue preview.");
        }
    }

    private void DrawSection(string title, params string[] propertyNames)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        for (int index = 0; index < propertyNames.Length; index++)
        {
            SerializedProperty property = serializedLayout.FindProperty(propertyNames[index]);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Missing layout property: {propertyNames[index]}", MessageType.Error);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }
        EditorGUI.indentLevel--;
    }

    private void LoadLayout()
    {
        layout = AssetDatabase.LoadAssetAtPath<CarPrototypeHudLayout>(LayoutPath);
        if (layout == null)
        {
            layout = CreateInstance<CarPrototypeHudLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        serializedLayout = new SerializedObject(layout);
        lastStatus = null;
    }

    private void SaveLayout()
    {
        if (layout == null) return;
        serializedLayout?.ApplyModifiedProperties();
        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssetIfDirty(layout);
        lastStatus = "3D scene and HUD layout saved to Assets/Resources/CarPrototypeHudLayout.asset.";
        lastStatusType = MessageType.Info;
        ApplyToRunningGame(layout, false);
    }

    private void ResetDefaults()
    {
        if (layout == null) return;

        if (!EditorUtility.DisplayDialog("Reset 3D HUD Layout", "Reset every 3D HUD layout value to its default? You can undo this operation before saving.", "Reset", "Cancel"))
            return;

        CarPrototypeHudLayout defaults = CreateInstance<CarPrototypeHudLayout>();
        Undo.RecordObject(layout, "Reset 3D HUD Layout");
        EditorUtility.CopySerialized(defaults, layout);
        DestroyImmediate(defaults);
        EditorUtility.SetDirty(layout);
        serializedLayout.Update();
        lastStatus = "Default values restored. Use Undo to reverse this, or Save Layout to keep it.";
        lastStatusType = MessageType.Warning;
        ApplyToRunningGame(layout, false);
    }

    private void OpenPrototypeScene()
    {
        if (EditorApplication.isPlaying)
        {
            lastStatus = "Stop Play Mode before opening a different scene.";
            lastStatusType = MessageType.Warning;
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        lastStatus = "Opened CarPrototype3D. Press Play to use live editing.";
        lastStatusType = MessageType.Info;
    }

    private void HandleUndoRedo()
    {
        if (layout == null) LoadLayout();
        serializedLayout?.Update();
        ApplyToRunningGame(layout, false);
        Repaint();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += () =>
            {
                ApplyToRunningGame(layout, false);
                Repaint();
            };
        }
        else
        {
            Repaint();
        }
    }

    private void RunHudAction(System.Action<CarPrototypeHud> action, string successMessage)
    {
        CarPrototypeHud hud = FindRunningHud();
        if (hud == null)
        {
            lastStatus = "The running 3D car HUD was not found.";
            lastStatusType = MessageType.Error;
            return;
        }

        action(hud);
        lastStatus = successMessage;
        lastStatusType = MessageType.Info;
        SceneView.RepaintAll();
    }

    private static CarPrototypeHud FindRunningHud()
    {
        return Object.FindFirstObjectByType<CarPrototypeHud>(FindObjectsInactive.Include);
    }

    private void ApplyToRunningGame(CarPrototypeHudLayout currentLayout, bool reportMissingHud)
    {
        if (!EditorApplication.isPlaying || currentLayout == null) return;

        CarPrototypeHud hud = FindRunningHud();
        if (hud == null)
        {
            if (reportMissingHud)
            {
                lastStatus = "Could not apply: the active scene does not contain a running 3D car HUD.";
                lastStatusType = MessageType.Error;
            }
            return;
        }

        if (!hud.ApplyLayoutFromEditor(currentLayout))
        {
            lastStatus = "The HUD exists but has not finished building. Wait one frame and apply again.";
            lastStatusType = MessageType.Warning;
            return;
        }

        lastStatus = "Applied the edited 3D scene and HUD layout to the running game.";
        lastStatusType = MessageType.Info;
        SceneView.RepaintAll();
    }
}
