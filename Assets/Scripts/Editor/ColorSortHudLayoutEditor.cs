using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ColorSortHudLayoutEditor : EditorWindow
{
    private const string LayoutAssetPath = "Assets/Resources/ColorSortHudLayout.asset";
    private ColorSortHudLayout layout;
    private Vector2 scroll;

    [MenuItem("Window/Color Sort/2D HUD Layout Editor")]
    public static void Open()
    {
        GetWindow<ColorSortHudLayoutEditor>("2D HUD Layout");
    }

    private void OnEnable()
    {
        layout = LoadOrCreateLayout();
    }

    private void OnGUI()
    {
        if (IsThreeDimensionalCarScene())
        {
            EditorGUILayout.HelpBox(
                "This is the 2D HUD editor and it edits ColorSortHudLayout. The active scene is the 3D car game, which uses CarPrototypeHudLayout instead.",
                MessageType.Error);
            EditorGUILayout.HelpBox(
                "Open the 3D editor below. Its Layout Asset field must say CarPrototypeHudLayout before you change any values.",
                MessageType.Info);

            if (GUILayout.Button("Open Correct 3D HUD Editor", GUILayout.Height(42f)))
            {
                CarPrototypeHudLayoutEditor.Open();
                Close();
            }

            return;
        }

        layout = (ColorSortHudLayout)EditorGUILayout.ObjectField("Layout Asset", layout, typeof(ColorSortHudLayout), false);

        if (layout == null)
        {
            if (GUILayout.Button("Create Layout Asset"))
            {
                layout = LoadOrCreateLayout();
            }
            return;
        }

        EditorGUILayout.HelpBox("Use these values to nudge the top deck UI. Press Apply To Running Game while in Play Mode to preview without restarting.", MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        Undo.RecordObject(layout, "Edit Color Sort HUD Layout");

        DrawRectGroup("Header Panel", ref layout.headerPosition, ref layout.headerSize);
        DrawRectGroup("Upper Deck Art", ref layout.deckPosition, ref layout.deckSize);

        layout.hudFont = (TMP_FontAsset)EditorGUILayout.ObjectField("All Game Text Font", layout.hudFont, typeof(TMP_FontAsset), false);
        if (layout.hudFont != null && layout.hudFont.material == null)
        {
            EditorGUILayout.HelpBox("This font asset is missing its TMP material/atlas, so it will be ignored. Create the font through Window > TextMeshPro > Font Asset Creator.", MessageType.Warning);
        }

        layout.boardLabelText = EditorGUILayout.TextField("Board Label Text", layout.boardLabelText);
        DrawRectGroup("Board Label", ref layout.boardLabelPosition, ref layout.boardLabelSize);
        layout.boardLabelFontSize = EditorGUILayout.FloatField("Board Label Font", layout.boardLabelFontSize);
        DrawRectGroup("Board Number", ref layout.boardNumberPosition, ref layout.boardNumberSize);
        layout.boardNumberFontSize = EditorGUILayout.FloatField("Board Number Font", layout.boardNumberFontSize);
        layout.boardNumberTwoDigitFontSize = EditorGUILayout.FloatField("Board Number 2 Digit Font", layout.boardNumberTwoDigitFontSize);
        layout.boardNumberThreeDigitFontSize = EditorGUILayout.FloatField("Board Number 3 Digit Font", layout.boardNumberThreeDigitFontSize);

        DrawRectGroup("Red Dot", ref layout.redDotPosition, ref layout.redDotSize);
        DrawRectGroup("Red Text", ref layout.redTextPosition, ref layout.redTextSize);
        layout.redTextFontSize = EditorGUILayout.FloatField("Red Text Font", layout.redTextFontSize);

        DrawRectGroup("Green Dot", ref layout.greenDotPosition, ref layout.greenDotSize);
        DrawRectGroup("Green Text", ref layout.greenTextPosition, ref layout.greenTextSize);
        layout.greenTextFontSize = EditorGUILayout.FloatField("Green Text Font", layout.greenTextFontSize);

        DrawRectGroup("Blue Dot", ref layout.blueDotPosition, ref layout.blueDotSize);
        DrawRectGroup("Blue Text", ref layout.blueTextPosition, ref layout.blueTextSize);
        layout.blueTextFontSize = EditorGUILayout.FloatField("Blue Text Font", layout.blueTextFontSize);

        DrawRectGroup("Yellow Dot", ref layout.yellowDotPosition, ref layout.yellowDotSize);
        DrawRectGroup("Yellow Text", ref layout.yellowTextPosition, ref layout.yellowTextSize);
        layout.yellowTextFontSize = EditorGUILayout.FloatField("Yellow Text Font", layout.yellowTextFontSize);

        DrawRectGroup("Hearts", ref layout.heartPosition, ref layout.heartSize);
        layout.heartSpacing = EditorGUILayout.FloatField("Heart Spacing", layout.heartSpacing);

        DrawRectGroup("Retry Button", ref layout.retryPosition, ref layout.retrySize);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Main Menu", EditorStyles.boldLabel);
        layout.mainMenuPlayText = EditorGUILayout.TextField("Play Button Text", layout.mainMenuPlayText);
        DrawRectGroup("Play Button", ref layout.mainMenuPlayPosition, ref layout.mainMenuPlaySize);
        layout.mainMenuPlayFontSize = EditorGUILayout.FloatField("Play Button Font", layout.mainMenuPlayFontSize);
        layout.mainMenuShopTabPosition = EditorGUILayout.Vector2Field("Shop Tab Position", layout.mainMenuShopTabPosition);
        layout.mainMenuHomeTabPosition = EditorGUILayout.Vector2Field("Home Tab Position", layout.mainMenuHomeTabPosition);
        layout.mainMenuLockedTabPosition = EditorGUILayout.Vector2Field("Locked Tab Position", layout.mainMenuLockedTabPosition);
        layout.mainMenuTabSize = EditorGUILayout.Vector2Field("Normal Tab Size", layout.mainMenuTabSize);
        layout.mainMenuTallTabSize = EditorGUILayout.Vector2Field("Selected Tall Tab Size", layout.mainMenuTallTabSize);
        layout.mainMenuSelectedTabOffset = EditorGUILayout.Vector2Field("Selected Tab Offset", layout.mainMenuSelectedTabOffset);
        DrawRectGroup("Selected Tab Label", ref layout.mainMenuSelectedLabelOffset, ref layout.mainMenuSelectedLabelSize);
        layout.mainMenuSelectedLabelFontSize = EditorGUILayout.FloatField("Selected Tab Label Font", layout.mainMenuSelectedLabelFontSize);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Gameplay Area", EditorStyles.boldLabel);
        DrawRectGroup("Main Board Tray", ref layout.boardTrayPosition, ref layout.boardTraySize3, ref layout.boardTraySize4);
        layout.boardCenterX = EditorGUILayout.FloatField("Board Center X", layout.boardCenterX);
        layout.boardCenterY = EditorGUILayout.FloatField("Board Center Y", layout.boardCenterY);
        DrawFloatPair("Board Spacing", ref layout.boardSpacing3, ref layout.boardSpacing4);
        DrawFloatPair("Board Block Scale", ref layout.boardBlockScale3, ref layout.boardBlockScale4);
        DrawFloatPair("Board Collider Size", ref layout.boardColliderSize3, ref layout.boardColliderSize4);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Bottom Tray", EditorStyles.boldLabel);
        layout.colorTrayPosition3 = EditorGUILayout.Vector2Field("3 Slot Tray Position", layout.colorTrayPosition3);
        layout.colorTrayPosition4 = EditorGUILayout.Vector2Field("4 Slot Tray Position", layout.colorTrayPosition4);
        layout.colorTrayPosition5 = EditorGUILayout.Vector2Field("5 Slot Tray Position", layout.colorTrayPosition5);
        DrawFloatTriple("Tray Width", ref layout.colorTrayWidth3, ref layout.colorTrayWidth4, ref layout.colorTrayWidth5);
        layout.colorTraySlotX3 = EditorGUILayout.Vector3Field("3 Slot Block X Centers", layout.colorTraySlotX3);
        layout.colorTraySlotX4 = EditorGUILayout.Vector4Field("4 Slot Block X Centers", layout.colorTraySlotX4);
        layout.colorTraySlotX5FirstFour = EditorGUILayout.Vector4Field("5 Slot Block X Centers A-D", layout.colorTraySlotX5FirstFour);
        layout.colorTraySlotX5Last = EditorGUILayout.FloatField("5 Slot Block X Center E", layout.colorTraySlotX5Last);
        DrawFloatTriple("Tray Block Scale", ref layout.trayBlockScale3, ref layout.trayBlockScale4, ref layout.trayBlockScale5);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Parking Slot", EditorStyles.boldLabel);
        layout.parkingPosition3 = EditorGUILayout.Vector2Field("3 Slot Mode Position", layout.parkingPosition3);
        layout.parkingPosition4 = EditorGUILayout.Vector2Field("4 Slot Mode Position", layout.parkingPosition4);
        DrawFloatPair("Parking Art Size", ref layout.parkingSize3, ref layout.parkingSize4);
        DrawFloatPair("Parking Block Scale", ref layout.parkingBlockScale3, ref layout.parkingBlockScale4);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Boosters", EditorStyles.boldLabel);
        layout.extraSlotBoosterPosition = EditorGUILayout.Vector2Field("Extra Slot Booster World Position", layout.extraSlotBoosterPosition);
        layout.extraSlotBoosterSize = EditorGUILayout.Vector2Field("Extra Slot Booster World Size", layout.extraSlotBoosterSize);
        layout.undoBoosterPosition = EditorGUILayout.Vector2Field("Undo Booster World Position", layout.undoBoosterPosition);
        layout.undoBoosterSize = EditorGUILayout.Vector2Field("Undo Booster World Size", layout.undoBoosterSize);
        layout.pauseButtonPosition = EditorGUILayout.Vector2Field("Pause Button World Position", layout.pauseButtonPosition);
        layout.pauseButtonSize = EditorGUILayout.Vector2Field("Pause Button World Size", layout.pauseButtonSize);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Level Debug", EditorStyles.boldLabel);
        layout.showDebugNextBoardButton = EditorGUILayout.Toggle("Show In-Game Next Button", layout.showDebugNextBoardButton);
        layout.debugNextBoardButtonText = EditorGUILayout.TextField("Next Button Text", layout.debugNextBoardButtonText);
        layout.debugNextBoardButtonPosition = EditorGUILayout.Vector2Field("Next Button World Position", layout.debugNextBoardButtonPosition);
        layout.debugNextBoardButtonSize = EditorGUILayout.Vector2Field("Next Button World Size", layout.debugNextBoardButtonSize);
        layout.editorPreviewBoardNumber = Mathf.Max(1, EditorGUILayout.IntField("Jump To Board Number", layout.editorPreviewBoardNumber));

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Settings Screen", EditorStyles.boldLabel);
        layout.settingsDimAlpha = EditorGUILayout.Slider("Background Darkness", layout.settingsDimAlpha, 0f, 0.95f);
        DrawRectGroup("Settings Tray", ref layout.settingsPanelPosition, ref layout.settingsPanelSize);
        layout.settingsTitleText = EditorGUILayout.TextField("Title Text", layout.settingsTitleText);
        DrawRectGroup("Title", ref layout.settingsTitlePosition, ref layout.settingsTitleSize);
        layout.settingsTitleFontSize = EditorGUILayout.FloatField("Title Font", layout.settingsTitleFontSize);

        DrawSettingsRow(
            "Haptics",
            ref layout.settingsHapticsIconPosition,
            ref layout.settingsHapticsIconSize,
            ref layout.settingsHapticsText,
            ref layout.settingsHapticsTextPosition,
            ref layout.settingsHapticsTextSize,
            ref layout.settingsHapticsFontSize,
            ref layout.settingsHapticsTogglePosition);

        DrawSettingsRow(
            "Sounds",
            ref layout.settingsSoundsIconPosition,
            ref layout.settingsSoundsIconSize,
            ref layout.settingsSoundsText,
            ref layout.settingsSoundsTextPosition,
            ref layout.settingsSoundsTextSize,
            ref layout.settingsSoundsFontSize,
            ref layout.settingsSoundsTogglePosition);

        DrawSettingsRow(
            "Music",
            ref layout.settingsMusicIconPosition,
            ref layout.settingsMusicIconSize,
            ref layout.settingsMusicText,
            ref layout.settingsMusicTextPosition,
            ref layout.settingsMusicTextSize,
            ref layout.settingsMusicFontSize,
            ref layout.settingsMusicTogglePosition);

        layout.settingsToggleSize = EditorGUILayout.Vector2Field("Switch Size", layout.settingsToggleSize);
        layout.settingsSoundsTogglePosition = EditorGUILayout.Vector2Field("Sounds Switch Position", layout.settingsSoundsTogglePosition);
        layout.settingsMusicTogglePosition = EditorGUILayout.Vector2Field("Music Switch Position", layout.settingsMusicTogglePosition);
        layout.settingsToggleKnobSize = EditorGUILayout.Vector2Field("Switch Knob Size", layout.settingsToggleKnobSize);
        layout.settingsToggleKnobOffset = EditorGUILayout.FloatField("Switch Knob Offset", layout.settingsToggleKnobOffset);
        layout.settingsToggleTextSize = EditorGUILayout.Vector2Field("Switch Text Size", layout.settingsToggleTextSize);
        layout.settingsToggleFontSize = EditorGUILayout.FloatField("Switch Text Font", layout.settingsToggleFontSize);

        layout.settingsResumeText = EditorGUILayout.TextField("Resume Text", layout.settingsResumeText);
        DrawRectGroup("Resume Button", ref layout.settingsResumePosition, ref layout.settingsResumeSize);
        layout.settingsResumeFontSize = EditorGUILayout.FloatField("Resume Font", layout.settingsResumeFontSize);
        layout.settingsQuitText = EditorGUILayout.TextField("Quit Text", layout.settingsQuitText);
        DrawRectGroup("Quit Button", ref layout.settingsQuitPosition, ref layout.settingsQuitSize);
        layout.settingsQuitFontSize = EditorGUILayout.FloatField("Quit Font", layout.settingsQuitFontSize);
        layout.settingsMoreText = EditorGUILayout.TextField("More Text", layout.settingsMoreText);
        DrawRectGroup("More Button", ref layout.settingsMorePosition, ref layout.settingsMoreSize);
        layout.settingsMoreFontSize = EditorGUILayout.FloatField("More Font", layout.settingsMoreFontSize);
        DrawRectGroup("Close Button", ref layout.settingsClosePosition, ref layout.settingsCloseSize);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Settings More Page", EditorStyles.boldLabel);
        DrawRectGroup("More Page Tray", ref layout.morePanelPosition, ref layout.morePanelSize);
        layout.moreTitleText = EditorGUILayout.TextField("More Title Text", layout.moreTitleText);
        DrawRectGroup("More Title", ref layout.moreTitlePosition, ref layout.moreTitleSize);
        layout.moreTitleFontSize = EditorGUILayout.FloatField("More Title Font", layout.moreTitleFontSize);
        layout.termsButtonText = EditorGUILayout.TextField("Terms Text", layout.termsButtonText);
        DrawRectGroup("Terms Button", ref layout.termsButtonPosition, ref layout.termsButtonSize);
        layout.termsButtonFontSize = EditorGUILayout.FloatField("Terms Font", layout.termsButtonFontSize);
        layout.privacyButtonText = EditorGUILayout.TextField("Privacy Text", layout.privacyButtonText);
        DrawRectGroup("Privacy Button", ref layout.privacyButtonPosition, ref layout.privacyButtonSize);
        layout.privacyButtonFontSize = EditorGUILayout.FloatField("Privacy Font", layout.privacyButtonFontSize);
        layout.moreBackButtonText = EditorGUILayout.TextField("Back Text", layout.moreBackButtonText);
        DrawRectGroup("Back Button", ref layout.moreBackButtonPosition, ref layout.moreBackButtonSize);
        layout.moreBackButtonFontSize = EditorGUILayout.FloatField("Back Font", layout.moreBackButtonFontSize);
        DrawRectGroup("More Close Button", ref layout.moreClosePosition, ref layout.moreCloseSize);
        layout.termsUrl = EditorGUILayout.TextField("Terms URL", layout.termsUrl);
        layout.privacyUrl = EditorGUILayout.TextField("Privacy URL", layout.privacyUrl);

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(layout);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save"))
            {
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (GUILayout.Button("Apply To Running Game"))
            {
                ApplyToRunningGame(layout);
            }

            if (GUILayout.Button("Reset Defaults"))
            {
                ResetDefaults(layout);
                ApplyToRunningGame(layout);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview 3x3 Board"))
            {
                LoadPreviewBoard(0);
            }

            if (GUILayout.Button("Preview 4x4 Board"))
            {
                LoadPreviewBoard(5);
            }

            if (GUILayout.Button("Preview 5 Slot Tray"))
            {
                LoadPreviewBoostedFiveSlotTray();
            }

            if (GUILayout.Button("Open Settings Preview"))
            {
                OpenSettingsPreview(layout);
            }

            if (GUILayout.Button("Open Main Menu Preview"))
            {
                OpenMainMenuPreview(layout);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Next Board Now"))
            {
                SkipToNextBoard();
            }

            if (GUILayout.Button("Jump To Board Now"))
            {
                LoadPreviewBoard(Mathf.Max(0, layout.editorPreviewBoardNumber - 1));
            }
        }
    }

    private static bool IsThreeDimensionalCarScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == "Assets/Scenes/CarPrototype3D.unity") return true;

        return UnityEngine.Object.FindFirstObjectByType<CarPrototype3D>(FindObjectsInactive.Include) != null;
    }

    private static void DrawRectGroup(string label, ref Vector2 position, ref Vector2 size)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        position = EditorGUILayout.Vector2Field("Position", position);
        size = EditorGUILayout.Vector2Field("Size", size);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4f);
    }

    private static void DrawRectGroup(string label, ref Vector2 position, ref float size3, ref float size4)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        position = EditorGUILayout.Vector2Field("Position", position);
        DrawFloatPair("Size", ref size3, ref size4);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4f);
    }

    private static void DrawFloatPair(string label, ref float value3, ref float value4)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            value3 = EditorGUILayout.FloatField("3x3", value3);
            value4 = EditorGUILayout.FloatField("4x4", value4);
        }
    }

    private static void DrawFloatTriple(string label, ref float value3, ref float value4, ref float value5)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            value3 = EditorGUILayout.FloatField("3", value3);
            value4 = EditorGUILayout.FloatField("4", value4);
            value5 = EditorGUILayout.FloatField("5", value5);
        }
    }

    private static void DrawSettingsRow(
        string label,
        ref Vector2 iconPosition,
        ref Vector2 iconSize,
        ref string text,
        ref Vector2 textPosition,
        ref Vector2 textSize,
        ref float fontSize,
        ref Vector2 togglePosition)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        iconPosition = EditorGUILayout.Vector2Field("Icon Position", iconPosition);
        iconSize = EditorGUILayout.Vector2Field("Icon Size", iconSize);
        text = EditorGUILayout.TextField("Text", text);
        textPosition = EditorGUILayout.Vector2Field("Text Position", textPosition);
        textSize = EditorGUILayout.Vector2Field("Text Size", textSize);
        fontSize = EditorGUILayout.FloatField("Text Font", fontSize);
        togglePosition = EditorGUILayout.Vector2Field("Switch Position", togglePosition);
        EditorGUI.indentLevel--;
    }

    private static ColorSortHudLayout LoadOrCreateLayout()
    {
        ColorSortHudLayout loaded = AssetDatabase.LoadAssetAtPath<ColorSortHudLayout>(LayoutAssetPath);
        if (loaded != null) return loaded;

        ColorSortHudLayout created = CreateInstance<ColorSortHudLayout>();
        AssetDatabase.CreateAsset(created, LayoutAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return created;
    }

    private static void ApplyToRunningGame(ColorSortHudLayout hud)
    {
        if (hud == null) return;

        ApplyRect("HeaderPanel", hud.headerPosition, hud.headerSize);
        ApplyRect("UpperDeckArt", hud.deckPosition, hud.deckSize);
        ApplyRect("BoardLabel", hud.boardLabelPosition, hud.boardLabelSize);
        ApplyText("BoardLabel", hud.boardLabelFontSize, hud.hudFont);
        ApplyTextValue("BoardLabel", hud.boardLabelText);
        ApplyRect("BoardCounter", hud.boardNumberPosition, hud.boardNumberSize);
        ApplyText("BoardCounter", hud.boardNumberFontSize, hud.hudFont);

        ApplyRect("RedDot", hud.redDotPosition, hud.redDotSize);
        ApplyRect("RedProgress", hud.redTextPosition, hud.redTextSize);
        ApplyText("RedProgress", hud.redTextFontSize, hud.hudFont);

        ApplyRect("GreenDot", hud.greenDotPosition, hud.greenDotSize);
        ApplyRect("GreenProgress", hud.greenTextPosition, hud.greenTextSize);
        ApplyText("GreenProgress", hud.greenTextFontSize, hud.hudFont);

        ApplyRect("BlueDot", hud.blueDotPosition, hud.blueDotSize);
        ApplyRect("BlueProgress", hud.blueTextPosition, hud.blueTextSize);
        ApplyText("BlueProgress", hud.blueTextFontSize, hud.hudFont);

        ApplyRect("YellowDot", hud.yellowDotPosition, hud.yellowDotSize);
        ApplyRect("YellowProgress", hud.yellowTextPosition, hud.yellowTextSize);
        ApplyText("YellowProgress", hud.yellowTextFontSize, hud.hudFont);
        for (int i = 0; i < 3; i++)
        {
            ApplyRect("Heart_" + i, hud.heartPosition + new Vector2(i * hud.heartSpacing, 0f), hud.heartSize);
        }

        ApplyRect("RetryButton", hud.retryPosition, hud.retrySize);

        ApplyRect("MainMenuPlayButton", hud.mainMenuPlayPosition, hud.mainMenuPlaySize);
        ApplyRect("MainMenuPlayButtonText", Vector2.zero, hud.mainMenuPlaySize);
        ApplyText("MainMenuPlayButtonText", hud.mainMenuPlayFontSize, hud.hudFont);
        ApplyTextValue("MainMenuPlayButtonText", hud.mainMenuPlayText);
        ApplyMenuTabRect("MainMenuShopTab", "MainMenuShopTabLabel", hud.mainMenuShopTabPosition, hud.mainMenuTabSize, hud.mainMenuTallTabSize, hud.mainMenuSelectedTabOffset);
        ApplyMenuTabRect("MainMenuHomeTab", "MainMenuHomeTabLabel", hud.mainMenuHomeTabPosition, hud.mainMenuTabSize, hud.mainMenuTallTabSize, hud.mainMenuSelectedTabOffset);
        ApplyMenuTabRect("MainMenuLockedTab", "MainMenuLockedTabLabel", hud.mainMenuLockedTabPosition, hud.mainMenuTabSize, hud.mainMenuTallTabSize, hud.mainMenuSelectedTabOffset);
        ApplyRect("MainMenuShopTabLabel", hud.mainMenuSelectedLabelOffset, hud.mainMenuSelectedLabelSize);
        ApplyRect("MainMenuHomeTabLabel", hud.mainMenuSelectedLabelOffset, hud.mainMenuSelectedLabelSize);
        ApplyRect("MainMenuLockedTabLabel", hud.mainMenuSelectedLabelOffset, hud.mainMenuSelectedLabelSize);
        ApplyText("MainMenuShopTabLabel", hud.mainMenuSelectedLabelFontSize, hud.hudFont);
        ApplyText("MainMenuHomeTabLabel", hud.mainMenuSelectedLabelFontSize, hud.hudFont);
        ApplyText("MainMenuLockedTabLabel", hud.mainMenuSelectedLabelFontSize, hud.hudFont);

        ApplyOverlayDim("SettingsOverlay", hud.settingsDimAlpha);
        ApplyRect("SettingsPanel", hud.settingsPanelPosition, hud.settingsPanelSize);
        ApplyRect("SettingsTitle", hud.settingsTitlePosition, hud.settingsTitleSize);
        ApplyText("SettingsTitle", hud.settingsTitleFontSize, hud.hudFont);
        ApplyTextValue("SettingsTitle", hud.settingsTitleText);

        ApplyRect("HapticsIcon", hud.settingsHapticsIconPosition, hud.settingsHapticsIconSize);
        ApplyRect("HapticsLabel", hud.settingsHapticsTextPosition, hud.settingsHapticsTextSize);
        ApplyText("HapticsLabel", hud.settingsHapticsFontSize, hud.hudFont);
        ApplyTextValue("HapticsLabel", hud.settingsHapticsText);
        ApplyRect("HapticsToggle", hud.settingsHapticsTogglePosition, hud.settingsToggleSize);
        ApplyRect("HapticsToggleKnob", new Vector2(-hud.settingsToggleKnobOffset, 0f), hud.settingsToggleKnobSize);
        ApplyRect("HapticsToggleHitArea", Vector2.zero, hud.settingsToggleSize);
        ApplyRect("HapticsToggleText", Vector2.zero, hud.settingsToggleTextSize);
        ApplyText("HapticsToggleText", hud.settingsToggleFontSize, hud.hudFont);

        ApplyRect("SoundsIcon", hud.settingsSoundsIconPosition, hud.settingsSoundsIconSize);
        ApplyRect("SoundsLabel", hud.settingsSoundsTextPosition, hud.settingsSoundsTextSize);
        ApplyText("SoundsLabel", hud.settingsSoundsFontSize, hud.hudFont);
        ApplyTextValue("SoundsLabel", hud.settingsSoundsText);
        ApplyRect("SoundsToggle", hud.settingsSoundsTogglePosition, hud.settingsToggleSize);
        ApplyRect("SoundsToggleKnob", new Vector2(-hud.settingsToggleKnobOffset, 0f), hud.settingsToggleKnobSize);
        ApplyRect("SoundsToggleHitArea", Vector2.zero, hud.settingsToggleSize);
        ApplyRect("SoundsToggleText", Vector2.zero, hud.settingsToggleTextSize);
        ApplyText("SoundsToggleText", hud.settingsToggleFontSize, hud.hudFont);

        ApplyRect("MusicIcon", hud.settingsMusicIconPosition, hud.settingsMusicIconSize);
        ApplyRect("MusicLabel", hud.settingsMusicTextPosition, hud.settingsMusicTextSize);
        ApplyText("MusicLabel", hud.settingsMusicFontSize, hud.hudFont);
        ApplyTextValue("MusicLabel", hud.settingsMusicText);
        ApplyRect("MusicToggle", hud.settingsMusicTogglePosition, hud.settingsToggleSize);
        ApplyRect("MusicToggleKnob", new Vector2(-hud.settingsToggleKnobOffset, 0f), hud.settingsToggleKnobSize);
        ApplyRect("MusicToggleHitArea", Vector2.zero, hud.settingsToggleSize);
        ApplyRect("MusicToggleText", Vector2.zero, hud.settingsToggleTextSize);
        ApplyText("MusicToggleText", hud.settingsToggleFontSize, hud.hudFont);

        ApplyRect("ResumeButton", hud.settingsResumePosition, hud.settingsResumeSize);
        ApplyRect("ResumeButtonText", Vector2.zero, hud.settingsResumeSize);
        ApplyText("ResumeButtonText", hud.settingsResumeFontSize, hud.hudFont);
        ApplyTextValue("ResumeButtonText", hud.settingsResumeText);
        ApplyRect("QuitButton", hud.settingsQuitPosition, hud.settingsQuitSize);
        ApplyRect("QuitButtonText", Vector2.zero, hud.settingsQuitSize);
        ApplyText("QuitButtonText", hud.settingsQuitFontSize, hud.hudFont);
        ApplyTextValue("QuitButtonText", hud.settingsQuitText);
        ApplyRect("MoreButton", hud.settingsMorePosition, hud.settingsMoreSize);
        ApplyRect("MoreButtonText", Vector2.zero, hud.settingsMoreSize);
        ApplyText("MoreButtonText", hud.settingsMoreFontSize, hud.hudFont);
        ApplyTextValue("MoreButtonText", hud.settingsMoreText);
        ApplyRect("CloseButton", hud.settingsClosePosition, hud.settingsCloseSize);

        ApplyRect("SettingsMorePanel", hud.morePanelPosition, hud.morePanelSize);
        ApplyRect("MoreTitle", hud.moreTitlePosition, hud.moreTitleSize);
        ApplyText("MoreTitle", hud.moreTitleFontSize, hud.hudFont);
        ApplyTextValue("MoreTitle", hud.moreTitleText);
        ApplyRect("TermsButton", hud.termsButtonPosition, hud.termsButtonSize);
        ApplyRect("TermsButtonText", Vector2.zero, hud.termsButtonSize);
        ApplyText("TermsButtonText", hud.termsButtonFontSize, hud.hudFont);
        ApplyTextValue("TermsButtonText", hud.termsButtonText);
        ApplyRect("PrivacyButton", hud.privacyButtonPosition, hud.privacyButtonSize);
        ApplyRect("PrivacyButtonText", Vector2.zero, hud.privacyButtonSize);
        ApplyText("PrivacyButtonText", hud.privacyButtonFontSize, hud.hudFont);
        ApplyTextValue("PrivacyButtonText", hud.privacyButtonText);
        ApplyRect("MoreBackButton", hud.moreBackButtonPosition, hud.moreBackButtonSize);
        ApplyRect("MoreBackButtonText", Vector2.zero, hud.moreBackButtonSize);
        ApplyText("MoreBackButtonText", hud.moreBackButtonFontSize, hud.hudFont);
        ApplyTextValue("MoreBackButtonText", hud.moreBackButtonText);
        ApplyRect("MoreCloseButton", hud.moreClosePosition, hud.moreCloseSize);

        ApplyFontToAllLiveText(hud.hudFont);

        UnityGameManager gameManager = UnityEngine.Object.FindFirstObjectByType<UnityGameManager>();
        if (gameManager != null)
        {
            gameManager.ApplyLayoutFromEditor();
        }
    }

    private static void LoadPreviewBoard(int boardIndex)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("Enter Play Mode before switching preview boards.");
            return;
        }

        UnityGameManager gameManager = UnityEngine.Object.FindFirstObjectByType<UnityGameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("Color Sort GameManager was not found in the running scene.");
            return;
        }

        gameManager.LoadLevel(boardIndex);
        gameManager.ApplyLayoutFromEditor();
    }

    private static void LoadPreviewBoostedFiveSlotTray()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("Enter Play Mode before previewing the 5 slot tray.");
            return;
        }

        UnityGameManager gameManager = UnityEngine.Object.FindFirstObjectByType<UnityGameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("Color Sort GameManager was not found in the running scene.");
            return;
        }

        gameManager.LoadLevel(5);
        gameManager.ClickExtraSlot();
        gameManager.ApplyLayoutFromEditor();
    }

    private static void SkipToNextBoard()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("Enter Play Mode before skipping to the next board.");
            return;
        }

        UnityGameManager gameManager = UnityEngine.Object.FindFirstObjectByType<UnityGameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("Color Sort GameManager was not found in the running scene.");
            return;
        }

        gameManager.LoadLevel(gameManager.currentLevelIndex + 1);
        gameManager.ApplyLayoutFromEditor();
    }

    private static void OpenSettingsPreview(ColorSortHudLayout hud)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("Enter Play Mode before opening the settings preview.");
            return;
        }

        ApplyToRunningGame(hud);

        GameObject overlay = FindSceneObjectIncludingInactive("SettingsOverlay");
        if (overlay == null)
        {
            Debug.LogWarning("SettingsOverlay was not found in the running scene.");
            return;
        }

        overlay.SetActive(true);
    }

    private static void OpenMainMenuPreview(ColorSortHudLayout hud)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("Enter Play Mode before opening the main menu preview.");
            return;
        }

        ApplyToRunningGame(hud);

        GameObject menu = FindSceneObjectIncludingInactive("StartMenuPanel");
        if (menu == null)
        {
            Debug.LogWarning("StartMenuPanel was not found in the running scene.");
            return;
        }

        menu.SetActive(true);
    }

    private static void ApplyRect(string objectName, Vector2 position, Vector2 size)
    {
        GameObject obj = FindSceneObjectIncludingInactive(objectName);
        if (obj == null) return;

        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void ApplyMenuTabRect(string tabName, string labelName, Vector2 position, Vector2 normalSize, Vector2 selectedSize, Vector2 selectedOffset)
    {
        GameObject label = FindSceneObjectIncludingInactive(labelName);
        bool selected = label != null && label.activeSelf;
        ApplyRect(tabName, position + (selected ? selectedOffset : Vector2.zero), selected ? selectedSize : normalSize);
    }

    private static void ApplyText(string objectName, float fontSize, TMP_FontAsset font)
    {
        GameObject obj = FindSceneObjectIncludingInactive(objectName);
        if (obj == null) return;

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        if (text == null) return;

        TMP_FontAsset usableFont = GetUsableFont(font);
        if (usableFont != null)
        {
            text.font = usableFont;
        }
        text.fontSize = fontSize;
    }

    private static void ApplyOverlayDim(string objectName, float alpha)
    {
        GameObject obj = FindSceneObjectIncludingInactive(objectName);
        if (obj == null) return;

        UnityEngine.UI.Image image = obj.GetComponent<UnityEngine.UI.Image>();
        if (image == null) return;

        image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private static TMP_FontAsset GetUsableFont(TMP_FontAsset font)
    {
        if (font == null) return TMP_Settings.defaultFontAsset;
        if (font.material != null) return font;

        Debug.LogWarning("The selected font asset is missing its material/atlas, so it was not applied. Create it with Unity's TextMeshPro Font Asset Creator instead.");
        return TMP_Settings.defaultFontAsset;
    }

    private static void ApplyFontToAllLiveText(TMP_FontAsset font)
    {
        TMP_FontAsset usableFont = GetUsableFont(font);
        if (usableFont == null) return;

        TextMeshProUGUI[] uiTexts = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshProUGUI text in uiTexts)
        {
            text.font = usableFont;
        }

        TextMeshPro[] worldTexts = UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshPro text in worldTexts)
        {
            text.font = usableFont;
        }
    }

    private static void ApplyTextValue(string objectName, string value)
    {
        GameObject obj = FindSceneObjectIncludingInactive(objectName);
        if (obj == null) return;

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        if (text == null) return;

        text.text = value;
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null) return activeObject;

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform.name != objectName) continue;
            if (EditorUtility.IsPersistent(transform.gameObject)) continue;
            return transform.gameObject;
        }

        return null;
    }

    private static void ResetDefaults(ColorSortHudLayout hud)
    {
        Undo.RecordObject(hud, "Reset Color Sort HUD Layout");

        ColorSortHudLayout defaults = CreateInstance<ColorSortHudLayout>();
        hud.headerPosition = defaults.headerPosition;
        hud.headerSize = defaults.headerSize;
        hud.deckPosition = defaults.deckPosition;
        hud.deckSize = defaults.deckSize;
        hud.hudFont = defaults.hudFont;
        hud.boardLabelText = defaults.boardLabelText;
        hud.boardLabelPosition = defaults.boardLabelPosition;
        hud.boardLabelSize = defaults.boardLabelSize;
        hud.boardLabelFontSize = defaults.boardLabelFontSize;
        hud.boardNumberPosition = defaults.boardNumberPosition;
        hud.boardNumberSize = defaults.boardNumberSize;
        hud.boardNumberFontSize = defaults.boardNumberFontSize;
        hud.boardNumberTwoDigitFontSize = defaults.boardNumberTwoDigitFontSize;
        hud.boardNumberThreeDigitFontSize = defaults.boardNumberThreeDigitFontSize;
        hud.redDotPosition = defaults.redDotPosition;
        hud.redDotSize = defaults.redDotSize;
        hud.redTextPosition = defaults.redTextPosition;
        hud.redTextSize = defaults.redTextSize;
        hud.redTextFontSize = defaults.redTextFontSize;
        hud.greenDotPosition = defaults.greenDotPosition;
        hud.greenDotSize = defaults.greenDotSize;
        hud.greenTextPosition = defaults.greenTextPosition;
        hud.greenTextSize = defaults.greenTextSize;
        hud.greenTextFontSize = defaults.greenTextFontSize;
        hud.blueDotPosition = defaults.blueDotPosition;
        hud.blueDotSize = defaults.blueDotSize;
        hud.blueTextPosition = defaults.blueTextPosition;
        hud.blueTextSize = defaults.blueTextSize;
        hud.blueTextFontSize = defaults.blueTextFontSize;
        hud.yellowDotPosition = defaults.yellowDotPosition;
        hud.yellowDotSize = defaults.yellowDotSize;
        hud.yellowTextPosition = defaults.yellowTextPosition;
        hud.yellowTextSize = defaults.yellowTextSize;
        hud.yellowTextFontSize = defaults.yellowTextFontSize;
        hud.heartPosition = defaults.heartPosition;
        hud.heartSize = defaults.heartSize;
        hud.heartSpacing = defaults.heartSpacing;
        hud.retryPosition = defaults.retryPosition;
        hud.retrySize = defaults.retrySize;
        hud.mainMenuPlayPosition = defaults.mainMenuPlayPosition;
        hud.mainMenuPlaySize = defaults.mainMenuPlaySize;
        hud.mainMenuPlayText = defaults.mainMenuPlayText;
        hud.mainMenuPlayFontSize = defaults.mainMenuPlayFontSize;
        hud.mainMenuShopTabPosition = defaults.mainMenuShopTabPosition;
        hud.mainMenuHomeTabPosition = defaults.mainMenuHomeTabPosition;
        hud.mainMenuLockedTabPosition = defaults.mainMenuLockedTabPosition;
        hud.mainMenuTabSize = defaults.mainMenuTabSize;
        hud.mainMenuTallTabSize = defaults.mainMenuTallTabSize;
        hud.mainMenuSelectedTabOffset = defaults.mainMenuSelectedTabOffset;
        hud.mainMenuSelectedLabelOffset = defaults.mainMenuSelectedLabelOffset;
        hud.mainMenuSelectedLabelSize = defaults.mainMenuSelectedLabelSize;
        hud.mainMenuSelectedLabelFontSize = defaults.mainMenuSelectedLabelFontSize;
        hud.boardTrayPosition = defaults.boardTrayPosition;
        hud.boardTraySize3 = defaults.boardTraySize3;
        hud.boardTraySize4 = defaults.boardTraySize4;
        hud.boardCenterX = defaults.boardCenterX;
        hud.boardCenterY = defaults.boardCenterY;
        hud.boardSpacing3 = defaults.boardSpacing3;
        hud.boardSpacing4 = defaults.boardSpacing4;
        hud.boardBlockScale3 = defaults.boardBlockScale3;
        hud.boardBlockScale4 = defaults.boardBlockScale4;
        hud.boardColliderSize3 = defaults.boardColliderSize3;
        hud.boardColliderSize4 = defaults.boardColliderSize4;
        hud.colorTrayPosition3 = defaults.colorTrayPosition3;
        hud.colorTrayPosition4 = defaults.colorTrayPosition4;
        hud.colorTrayPosition5 = defaults.colorTrayPosition5;
        hud.colorTrayWidth3 = defaults.colorTrayWidth3;
        hud.colorTrayWidth4 = defaults.colorTrayWidth4;
        hud.colorTrayWidth5 = defaults.colorTrayWidth5;
        hud.colorTraySlotX3 = defaults.colorTraySlotX3;
        hud.colorTraySlotX4 = defaults.colorTraySlotX4;
        hud.colorTraySlotX5FirstFour = defaults.colorTraySlotX5FirstFour;
        hud.colorTraySlotX5Last = defaults.colorTraySlotX5Last;
        hud.trayBlockScale3 = defaults.trayBlockScale3;
        hud.trayBlockScale4 = defaults.trayBlockScale4;
        hud.trayBlockScale5 = defaults.trayBlockScale5;
        hud.parkingPosition3 = defaults.parkingPosition3;
        hud.parkingPosition4 = defaults.parkingPosition4;
        hud.parkingSize3 = defaults.parkingSize3;
        hud.parkingSize4 = defaults.parkingSize4;
        hud.parkingBlockScale3 = defaults.parkingBlockScale3;
        hud.parkingBlockScale4 = defaults.parkingBlockScale4;
        hud.extraSlotBoosterPosition = defaults.extraSlotBoosterPosition;
        hud.extraSlotBoosterSize = defaults.extraSlotBoosterSize;
        hud.undoBoosterPosition = defaults.undoBoosterPosition;
        hud.undoBoosterSize = defaults.undoBoosterSize;
        hud.pauseButtonPosition = defaults.pauseButtonPosition;
        hud.pauseButtonSize = defaults.pauseButtonSize;
        hud.showDebugNextBoardButton = defaults.showDebugNextBoardButton;
        hud.debugNextBoardButtonPosition = defaults.debugNextBoardButtonPosition;
        hud.debugNextBoardButtonSize = defaults.debugNextBoardButtonSize;
        hud.debugNextBoardButtonText = defaults.debugNextBoardButtonText;
        hud.editorPreviewBoardNumber = defaults.editorPreviewBoardNumber;
        hud.settingsDimAlpha = defaults.settingsDimAlpha;
        hud.settingsPanelPosition = defaults.settingsPanelPosition;
        hud.settingsPanelSize = defaults.settingsPanelSize;
        hud.settingsTitleText = defaults.settingsTitleText;
        hud.settingsTitlePosition = defaults.settingsTitlePosition;
        hud.settingsTitleSize = defaults.settingsTitleSize;
        hud.settingsTitleFontSize = defaults.settingsTitleFontSize;
        hud.settingsHapticsIconPosition = defaults.settingsHapticsIconPosition;
        hud.settingsHapticsIconSize = defaults.settingsHapticsIconSize;
        hud.settingsHapticsText = defaults.settingsHapticsText;
        hud.settingsHapticsTextPosition = defaults.settingsHapticsTextPosition;
        hud.settingsHapticsTextSize = defaults.settingsHapticsTextSize;
        hud.settingsHapticsFontSize = defaults.settingsHapticsFontSize;
        hud.settingsHapticsTogglePosition = defaults.settingsHapticsTogglePosition;
        hud.settingsSoundsIconPosition = defaults.settingsSoundsIconPosition;
        hud.settingsSoundsIconSize = defaults.settingsSoundsIconSize;
        hud.settingsSoundsText = defaults.settingsSoundsText;
        hud.settingsSoundsTextPosition = defaults.settingsSoundsTextPosition;
        hud.settingsSoundsTextSize = defaults.settingsSoundsTextSize;
        hud.settingsSoundsFontSize = defaults.settingsSoundsFontSize;
        hud.settingsSoundsTogglePosition = defaults.settingsSoundsTogglePosition;
        hud.settingsMusicIconPosition = defaults.settingsMusicIconPosition;
        hud.settingsMusicIconSize = defaults.settingsMusicIconSize;
        hud.settingsMusicText = defaults.settingsMusicText;
        hud.settingsMusicTextPosition = defaults.settingsMusicTextPosition;
        hud.settingsMusicTextSize = defaults.settingsMusicTextSize;
        hud.settingsMusicFontSize = defaults.settingsMusicFontSize;
        hud.settingsMusicTogglePosition = defaults.settingsMusicTogglePosition;
        hud.settingsToggleSize = defaults.settingsToggleSize;
        hud.settingsToggleKnobSize = defaults.settingsToggleKnobSize;
        hud.settingsToggleKnobOffset = defaults.settingsToggleKnobOffset;
        hud.settingsToggleTextSize = defaults.settingsToggleTextSize;
        hud.settingsToggleFontSize = defaults.settingsToggleFontSize;
        hud.settingsResumePosition = defaults.settingsResumePosition;
        hud.settingsResumeSize = defaults.settingsResumeSize;
        hud.settingsResumeText = defaults.settingsResumeText;
        hud.settingsResumeFontSize = defaults.settingsResumeFontSize;
        hud.settingsQuitPosition = defaults.settingsQuitPosition;
        hud.settingsQuitSize = defaults.settingsQuitSize;
        hud.settingsQuitText = defaults.settingsQuitText;
        hud.settingsQuitFontSize = defaults.settingsQuitFontSize;
        hud.settingsMorePosition = defaults.settingsMorePosition;
        hud.settingsMoreSize = defaults.settingsMoreSize;
        hud.settingsMoreText = defaults.settingsMoreText;
        hud.settingsMoreFontSize = defaults.settingsMoreFontSize;
        hud.settingsClosePosition = defaults.settingsClosePosition;
        hud.settingsCloseSize = defaults.settingsCloseSize;
        hud.morePanelPosition = defaults.morePanelPosition;
        hud.morePanelSize = defaults.morePanelSize;
        hud.moreTitleText = defaults.moreTitleText;
        hud.moreTitlePosition = defaults.moreTitlePosition;
        hud.moreTitleSize = defaults.moreTitleSize;
        hud.moreTitleFontSize = defaults.moreTitleFontSize;
        hud.termsButtonText = defaults.termsButtonText;
        hud.termsButtonPosition = defaults.termsButtonPosition;
        hud.termsButtonSize = defaults.termsButtonSize;
        hud.termsButtonFontSize = defaults.termsButtonFontSize;
        hud.privacyButtonText = defaults.privacyButtonText;
        hud.privacyButtonPosition = defaults.privacyButtonPosition;
        hud.privacyButtonSize = defaults.privacyButtonSize;
        hud.privacyButtonFontSize = defaults.privacyButtonFontSize;
        hud.moreBackButtonText = defaults.moreBackButtonText;
        hud.moreBackButtonPosition = defaults.moreBackButtonPosition;
        hud.moreBackButtonSize = defaults.moreBackButtonSize;
        hud.moreBackButtonFontSize = defaults.moreBackButtonFontSize;
        hud.moreClosePosition = defaults.moreClosePosition;
        hud.moreCloseSize = defaults.moreCloseSize;
        hud.termsUrl = defaults.termsUrl;
        hud.privacyUrl = defaults.privacyUrl;

        DestroyImmediate(defaults);
        EditorUtility.SetDirty(hud);
    }
}
