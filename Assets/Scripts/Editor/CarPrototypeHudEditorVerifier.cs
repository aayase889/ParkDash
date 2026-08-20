using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lightweight editor-side integration check for the runtime-built 3D HUD.
/// It verifies the layout asset schema and proves that edited values reach the
/// matching RectTransforms without changing the saved asset.
/// </summary>
public static class CarPrototypeHudEditorVerifier
{
    private const string LayoutPath = "Assets/Resources/CarPrototypeHudLayout.asset";

    private static readonly string[] RequiredProperties =
    {
        "sceneCameraPosition", "sceneCameraLookAt", "sceneCameraOrthographicSize",
        "sceneBoardFirstRowZ", "scenePieceScale3x3", "scenePieceScale4x4", "scenePieceScale5x5", "sceneLimousineOffBoardScale",
        "sceneAsphaltGroundPosition", "sceneAsphaltGroundSize", "sceneRoadCenterZ", "sceneRoadDepth",
        "sceneBackgroundAsphaltColor", "scenePlayfieldAsphaltColor", "sceneRoadBorderColor",
        "sceneMatchTrayPosition", "sceneMatchTraySlotSpacing", "sceneMatchTrayBaySize",
        "sceneSideParkingPosition", "sceneSideParkingSize", "sceneParkingLineWidth", "sceneSideParkingHatchSpacing",
        "levelPillPosition", "levelPillSize", "levelTextPosition", "levelTextSize", "levelTextFontSize",
        "mainMenuPlayPosition", "mainMenuPlaySize", "mainMenuPlayText", "mainMenuPlayFontSize",
        "mainMenuShopTabPosition", "mainMenuHomeTabPosition", "mainMenuLockedTabPosition",
        "mainMenuTabSize", "mainMenuTallTabSize", "mainMenuSelectedTabOffset",
        "mainMenuSelectedLabelOffset", "mainMenuSelectedLabelSize", "mainMenuSelectedLabelFontSize",
        "redObjectiveCarPosition", "greenObjectiveCarPosition", "blueObjectiveCarPosition", "yellowObjectiveCarPosition",
        "objectiveCarSize",
        "redObjectiveStatusPosition", "greenObjectiveStatusPosition", "blueObjectiveStatusPosition", "yellowObjectiveStatusPosition",
        "objectiveStatusSize", "objectiveCheckSize", "objectiveStatusFontSize",
        "heartsPosition", "heartSize", "heartSpacing", "heartLossFallDistance", "heartLossDuration",
        "extraSlotPosition", "undoPosition", "boosterSize", "pausePosition", "pauseSize",
        "showLevelPreviewButtons", "previousLevelPosition", "nextLevelPosition", "levelPreviewButtonSize",
        "settingsPanelPosition", "settingsPanelSize", "settingsTitlePosition", "hapticsPosition",
        "soundsPosition", "musicPosition", "resumePosition", "quitPosition", "morePosition",
        "settingsClosePosition", "morePanelPosition", "morePanelSize", "moreTitlePosition",
        "termsPosition", "privacyPosition", "moreBackPosition", "moreClosePosition",
        "leavePanelSize", "leaveTitlePosition", "leaveDescriptionPosition", "leaveCancelPosition",
        "leaveConfirmPosition", "defeatPanelSize", "defeatTitlePosition", "defeatDescriptionPosition",
        "restartPosition", "fontOverride"
    };

    [MenuItem("Color Sort/Verify 3D HUD Editor")]
    public static void RunFromMenu()
    {
        try
        {
            RunVerification();
            EditorUtility.DisplayDialog("3D HUD Editor", "Verification passed. Layout fields, live application, and preview visibility are connected correctly.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("3D HUD Editor", "Verification failed. See the Console for the exact cause.", "OK");
        }
    }

    public static void RunBatch()
    {
        RunVerification();
    }

    private static void RunVerification()
    {
        CarPrototypeHudLayout savedLayout = AssetDatabase.LoadAssetAtPath<CarPrototypeHudLayout>(LayoutPath);
        Require(savedLayout != null, $"Missing layout asset at {LayoutPath}.");

        ColorSortLevelDatabase levelDatabase = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>("Assets/Resources/ColorSortLevelDatabase.asset");
        Require(levelDatabase != null && levelDatabase.levels != null && levelDatabase.levels.Count >= 60, "The fixed level database is unavailable.");
        VerifyLimousineProgression(levelDatabase);

        GameObject importedRedCar = Resources.Load<GameObject>("CarModels/RedCar/redcar");
        Texture2D importedRedCarColor = Resources.Load<Texture2D>("CarModels/RedCar/Material-color-eyes");
        Texture2D importedRedCarMetallic = Resources.Load<Texture2D>("CarModels/RedCar/Material-metallic");
        Require(importedRedCar != null, "The imported red-car FBX is missing from Resources.");
        Require(importedRedCar.GetComponentsInChildren<Renderer>(true).Length > 0, "The imported red-car FBX has no renderable mesh.");
        Require(importedRedCarColor != null, "The cleaned REDCARFINAL eye texture is missing from Resources.");
        Require(importedRedCarMetallic != null, "The REDCARFINAL metallic texture is missing from Resources.");

        GameObject importedGreenCar = Resources.Load<GameObject>("CarModels/GreenCar/greencar");
        Texture2D importedGreenCarColor = Resources.Load<Texture2D>("CarModels/GreenCar/Material-color");
        Texture2D importedGreenCarMetallic = Resources.Load<Texture2D>("CarModels/GreenCar/Material-metallic");
        Require(importedGreenCar != null, "The imported green-car FBX is missing from Resources.");
        Require(importedGreenCar.GetComponentsInChildren<Renderer>(true).Length > 0, "The imported green-car FBX has no renderable mesh.");
        Require(importedGreenCarColor != null, "The green-car color texture is missing from Resources.");
        Require(importedGreenCarMetallic != null, "The green-car metallic texture is missing from Resources.");
        Require(Resources.Load<Texture2D>("heart_full") != null, "The full-heart HUD texture is missing from Resources.");
        Require(Resources.Load<Texture2D>("heart_stale") != null, "The stale-heart HUD texture is missing from Resources.");
        Require(Resources.Load<Texture2D>("heart_broken_borderless") != null, "The borderless broken-heart HUD texture is missing from Resources.");
        Require(Resources.Load<Sprite>("level_badge_road_sign") != null, "The normal level badge artwork is missing from Resources.");
        Require(Resources.Load<Sprite>("level_badge_hard") != null, "The supplied Hard level badge artwork is missing from Resources.");
        Require(Resources.Load<Sprite>("level_badge_super_hard") != null, "The supplied Super Hard level badge artwork is missing from Resources.");
        Require(Resources.Load<Texture2D>("outcome_loss_ui") != null, "The approved loss-screen artwork is missing from Resources.");
        Require(Resources.Load<Texture2D>("CarPrototype/VictoryUI/VictoryPanel") != null,
            "The approved new win-screen panel artwork is missing from Resources.");
        Require(Resources.Load<Texture2D>("CarPrototype/VictoryUI/PerfectWord") != null,
            "The approved settled PERFECT artwork is missing from Resources.");
        Require(Resources.Load<Texture2D>("CarPrototype/VictoryUI/ContinueButton") != null,
            "The clean, independently animated Continue artwork is missing from Resources.");
        Require(Resources.Load<Texture2D>("CarPrototype/VictoryUI/CloseButton") != null,
            "The clean, independently animated close artwork is missing from Resources.");
        Require(Resources.Load<Texture2D>("outcome_loss_retry") != null, "The loss Retry button overlay is missing.");
        Require(Resources.Load<Texture2D>("outcome_loss_home") != null, "The loss Home button overlay is missing.");
        Require(Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashBadge") != null,
            "The clean Park Dash construction badge is missing.");
        Require(Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashParkP") != null
            && Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashParkA") != null
            && Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashParkR") != null
            && Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashParkK") != null,
            "One or more PARK construction-letter layers are missing.");
        Require(Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashDash") != null,
            "The DASH construction layer is missing.");
        Require(Resources.Load<Texture2D>("CarPrototype/Victory/ParkDashLogo") != null,
            "The approved final Park Dash logo is missing.");
        Require(Resources.Load<Texture2D>("heart_hud_bar") != null, "The approved main-menu heart HUD artwork is missing.");
        Require(Resources.Load<Texture2D>("heart_hud_plus") != null, "The approved heart HUD Plus overlay is missing.");
        Texture2D settingsPanelArtwork = Resources.Load<Texture2D>("settings_panel_final");
        Require(settingsPanelArtwork != null, "The approved settings-panel artwork is missing.");
        Require(settingsPanelArtwork.width == 1125 && settingsPanelArtwork.height == 2436,
            "The settings-panel artwork is not using its exact supplied 1125 x 2436 dimensions.");

        SerializedObject serializedLayout = new SerializedObject(savedLayout);
        for (int index = 0; index < RequiredProperties.Length; index++)
            Require(serializedLayout.FindProperty(RequiredProperties[index]) != null, $"Missing serialized layout property '{RequiredProperties[index]}'.");

        GameObject testRoot = null;
        CarPrototypeHudLayout testLayout = null;
        float previousTimeScale = Time.timeScale;
        try
        {
            testRoot = new GameObject("3D HUD Editor Verification");
            CarPrototype3D game = testRoot.AddComponent<CarPrototype3D>();
            CarPrototypeHud hud = testRoot.AddComponent<CarPrototypeHud>();
            hud.Initialize(game);

            Require(FindNamedTransform(testRoot.transform, "Park Dash Victory Celebration") != null,
                "The four-second victory interstitial was not built.");
            Require(FindNamedComponent<ParkDashVictoryEffectsGraphic>(
                    testRoot.transform,
                    "Victory Fireworks Behind Logo") != null,
                "The batched victory firework layer was not built.");

            testLayout = UnityEngine.Object.Instantiate(savedLayout);
            testLayout.levelPillPosition = new Vector2(17f, 819f);
            testLayout.levelPillSize = new Vector2(413f, 127f);
            testLayout.mainMenuPlayPosition = new Vector2(91f, -123f);
            testLayout.mainMenuPlaySize = new Vector2(533f, 147f);
            testLayout.sceneCameraPosition = new Vector3(0f, 15f, -9f);
            testLayout.sceneBoardFirstRowZ = 2.1f;
            testLayout.scenePieceScale3x3 = 0.79f;
            testLayout.sceneBackgroundAsphaltColor = new Color(0.17f, 0.22f, 0.31f);
            testLayout.showLevelPreviewButtons = false;

            Require(hud.ApplyLayoutFromEditor(testLayout), "The runtime HUD rejected the edited layout.");

            RectTransform levelPill = FindNamedComponent<RectTransform>(testRoot.transform, "Level Pill");
            Require(levelPill != null, "The compact Level Pill was not built.");
            Require(Approximately(levelPill.anchoredPosition, testLayout.levelPillPosition), "The edited Level Pill position did not reach the runtime RectTransform.");
            Require(Approximately(levelPill.sizeDelta, testLayout.levelPillSize), "The edited Level Pill size did not reach the runtime RectTransform.");
            Require(FindNamedTransform(testRoot.transform, "Retry Button") == null, "The removed gameplay Retry Button was unexpectedly built.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(testRoot.transform, "Pause Button") != null,
                "The gameplay Pause button has no press animation.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(testRoot.transform, "Extra Slot Booster") != null,
                "The Extra Slot gameplay button has no press animation.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(testRoot.transform, "Undo Booster") != null,
                "The Undo gameplay button has no press animation.");

            RectTransform hearts = FindNamedComponent<RectTransform>(testRoot.transform, "Hearts");
            RectTransform firstHeart = FindNamedComponent<RectTransform>(testRoot.transform, "Heart 1");
            RectTransform secondHeart = FindNamedComponent<RectTransform>(testRoot.transform, "Heart 2");
            RectTransform thirdHeart = FindNamedComponent<RectTransform>(testRoot.transform, "Heart 3");
            Require(hearts != null && firstHeart != null && secondHeart != null && thirdHeart != null, "The three-image heart HUD was not built.");
            Require(Approximately(hearts.anchoredPosition, testLayout.heartsPosition), "The heart row is not using the edited position.");
            Require(Approximately(firstHeart.sizeDelta, testLayout.heartSize), "The heart images are not using the edited size.");
            Require(Mathf.Abs(firstHeart.anchoredPosition.x + testLayout.heartSpacing) < 0.01f, "The first heart is not using the edited spacing.");
            Require(Mathf.Abs(secondHeart.anchoredPosition.x) < 0.01f, "The middle heart is not centered.");
            Require(Mathf.Abs(thirdHeart.anchoredPosition.x - testLayout.heartSpacing) < 0.01f, "The third heart is not using the edited spacing.");

            RectTransform playButton = FindNamedComponent<RectTransform>(testRoot.transform, "MainMenuPlayButton");
            Require(playButton != null, "The runtime main-menu Play button was not built.");
            Require(Approximately(playButton.anchoredPosition, testLayout.mainMenuPlayPosition), "The edited Play position did not reach the main menu.");
            Require(Approximately(playButton.sizeDelta, testLayout.mainMenuPlaySize), "The edited Play size did not reach the main menu.");

            Transform previousButton = FindNamedTransform(testRoot.transform, "Previous Level");
            Transform nextButton = FindNamedTransform(testRoot.transform, "Next Level");
            Require(previousButton != null && nextButton != null, "The level preview buttons were not built.");
            Require(!previousButton.gameObject.activeSelf && !nextButton.gameObject.activeSelf, "The level preview visibility toggle was not applied.");

            testLayout.showLevelPreviewButtons = true;
            Require(hud.ApplyLayoutFromEditor(testLayout), "The runtime HUD rejected the second edited layout.");
            Require(previousButton.gameObject.activeSelf && nextButton.gameObject.activeSelf, "The level preview buttons did not become visible again.");

            hud.EditorShowSettingsPreview();
            Require(IsNamedObjectActive(testRoot.transform, "Settings Overlay"), "The Settings preview control did not open its overlay.");
            Transform settingsOverlay = FindNamedTransform(testRoot.transform, "Settings Overlay");
            Image settingsDim = FindNamedComponent<Image>(settingsOverlay, "Dim");
            Require(settingsDim != null && settingsDim.color.a >= 0.7f && IsFullScreenStretch(settingsDim.rectTransform),
                "The Settings dim does not stretch across the complete screen.");
            RawImage settingsTray = FindNamedComponent<RawImage>(settingsOverlay, "Settings Tray");
            Require(settingsTray != null && settingsTray.texture == settingsPanelArtwork,
                "The Settings panel is not displaying the exact supplied artwork.");
            Require(Approximately(settingsTray.rectTransform.sizeDelta, testLayout.settingsPanelSize),
                "The Settings artwork is not using the saved full-canvas size.");
            Require(FindNamedTransform(testRoot.transform, "Title") == null,
                "A duplicate live Settings title is covering the title baked into the approved artwork.");

            Button settingsClose = FindNamedComponent<Button>(settingsOverlay, "Close");
            Require(settingsClose != null && settingsClose.targetGraphic != null,
                "The baked Settings close artwork has no live hit target.");
            Require(settingsClose.targetGraphic.color.a < 0.01f,
                "A second visible close graphic is covering the close button baked into the approved artwork.");
            Require(Approximately(settingsClose.GetComponent<RectTransform>().anchoredPosition, testLayout.settingsClosePosition),
                "The Settings close hit target is not aligned with the baked close button.");
            Require(settingsClose.transition == Selectable.Transition.ColorTint
                && settingsClose.colors.pressedColor.a > 0.5f
                && settingsClose.colors.fadeDuration > 0f,
                "The Settings close button has no visible pressed-state animation.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(settingsOverlay, "Resume") != null,
                "The Settings Resume button has no press animation.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(settingsOverlay, "More") != null,
                "The Settings More button has no press animation.");

            string[] settingsInteriorControls =
            {
                "Haptics Icon", "Haptics Text", "Haptics Toggle",
                "Sounds Icon", "Sounds Text", "Sounds Toggle",
                "Music Icon", "Music Text", "Music Toggle",
                "Resume", "Resume Text", "Quit", "Quit Text", "More", "More Text"
            };
            for (int index = 0; index < settingsInteriorControls.Length; index++)
            {
                Require(
                    IsRectInside(testRoot.transform, settingsInteriorControls[index], -315f, 345f, -630f, 560f),
                    $"Settings control '{settingsInteriorControls[index]}' overlaps the supplied panel frame.");
            }

            hud.EditorShowMorePreview();
            Require(IsNamedObjectActive(testRoot.transform, "Settings More Overlay"), "The More preview control did not open its overlay.");
            Transform moreOverlay = FindNamedTransform(testRoot.transform, "Settings More Overlay");
            Image moreDim = FindNamedComponent<Image>(moreOverlay, "Dim");
            Require(moreDim != null && moreDim.color.a >= 0.7f && IsFullScreenStretch(moreDim.rectTransform),
                "The More-page dim does not stretch across the complete screen.");
            RawImage moreTray = FindNamedComponent<RawImage>(moreOverlay, "More Tray");
            Require(moreTray != null && moreTray.texture == settingsPanelArtwork,
                "The More page is still displaying the old Settings tray instead of the approved artwork.");
            Require(Approximately(moreTray.rectTransform.sizeDelta, testLayout.morePanelSize),
                "The More page artwork is not using the saved full-canvas size.");

            Button moreClose = FindNamedComponent<Button>(moreOverlay, "Close");
            Require(moreClose != null && moreClose.targetGraphic != null && moreClose.targetGraphic.color.a < 0.01f,
                "A second visible close graphic is covering the baked close button on the More page.");
            Require(Approximately(moreClose.GetComponent<RectTransform>().anchoredPosition, testLayout.moreClosePosition),
                "The More page close hit target is not aligned with the baked close button.");
            Require(moreClose.transition == Selectable.Transition.ColorTint
                && moreClose.colors.pressedColor.a > 0.5f
                && moreClose.colors.fadeDuration > 0f,
                "The More-page close button has no visible pressed-state animation.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(moreOverlay, "Terms") != null
                && FindNamedComponent<SimpleButtonPressAnimation>(moreOverlay, "Privacy") != null
                && FindNamedComponent<SimpleButtonPressAnimation>(moreOverlay, "Back") != null,
                "One or more green More-page buttons have no press animation.");

            string[] moreInteriorControls =
            {
                "More Title", "Terms", "Terms Text", "Privacy", "Privacy Text", "Back", "Back Text"
            };
            for (int index = 0; index < moreInteriorControls.Length; index++)
            {
                Require(
                    IsRectInside(moreOverlay, moreInteriorControls[index], -315f, 345f, -630f, 560f),
                    $"More-page control '{moreInteriorControls[index]}' overlaps the supplied panel frame.");
            }

            hud.EditorShowLeavePreview();
            Require(IsNamedObjectActive(testRoot.transform, "Leave Confirmation Overlay"), "The Leave preview control did not open its overlay.");
            hud.EditorShowDefeatPreview();
            Require(IsNamedObjectActive(testRoot.transform, "Defeat Overlay"), "The Loss preview control did not open its overlay.");
            Transform defeatOverlay = FindNamedTransform(testRoot.transform, "Defeat Overlay");
            Image defeatDim = defeatOverlay != null ? defeatOverlay.GetComponent<Image>() : null;
            Require(defeatDim != null && defeatDim.color.a >= 0.7f
                && IsFullScreenStretch(defeatOverlay.GetComponent<RectTransform>()),
                "The Loss UI dim does not stretch across the complete screen.");
            Require(FindNamedTransform(defeatOverlay, "Defeat Heart Bank") != null, "The five-heart bank was not built in the Loss panel.");
            Require(FindNamedTransform(defeatOverlay, "Defeat Heart Countdown") != null, "The ten-minute heart countdown was not built in the Loss panel.");
            for (int heartIndex = 1; heartIndex <= CarPrototypeHeartBank.MaximumHearts; heartIndex++)
                Require(FindNamedTransform(defeatOverlay, $"Defeat Heart {heartIndex}") != null, $"Loss heart slot {heartIndex} was not built.");
            Require(FindNamedTransform(defeatOverlay, "Result Red Objective") == null, "The removed objective summary is still present in the Loss panel.");
            Require(FindNamedComponent<Button>(testRoot.transform, "Loss Retry Button") != null, "The Loss Retry button was not built.");
            Require(FindNamedComponent<Button>(testRoot.transform, "Loss Home Button") != null, "The Loss Home button was not built.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(testRoot.transform, "Loss Retry Button") != null, "The Loss buttons have no press animation.");
            hud.EditorShowVictoryPreview();
            Require(IsNamedObjectActive(testRoot.transform, "Victory Overlay"), "The Win preview control did not open its overlay.");
            Require(!IsNamedObjectActive(testRoot.transform, "Defeat Overlay"), "Opening the Win overlay did not close the Loss overlay.");
            Transform victoryOverlay = FindNamedTransform(testRoot.transform, "Victory Overlay");
            Image victoryDim = victoryOverlay != null ? victoryOverlay.GetComponent<Image>() : null;
            Require(victoryDim != null && victoryDim.color.a >= 0.7f
                && IsFullScreenStretch(victoryOverlay.GetComponent<RectTransform>()),
                "The Win UI dim does not stretch across the complete screen.");
            Require(FindNamedTransform(testRoot.transform, "Approved New Victory Panel Artwork") != null,
                "The approved new Win UI panel was not built.");
            Require(FindNamedComponent<Button>(testRoot.transform, "Win Close Button") != null,
                "The artwork-aligned Win close hit target was not built.");
            Require(FindNamedComponent<Button>(testRoot.transform, "Win Continue Button") != null,
                "The artwork-aligned Continue hit target was not built.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(testRoot.transform, "Win Continue Button") != null,
                "The new Continue button has no press animation.");
            Require(FindNamedComponent<SimpleButtonPressAnimation>(testRoot.transform, "Win Close Button") != null,
                "The new close button has no press animation.");
            Require(FindNamedTransform(testRoot.transform, "Approved Win Continue Artwork") != null
                && FindNamedTransform(testRoot.transform, "Approved Win Close Artwork") != null,
                "The clean animated Win control art was not built.");
            Require(FindNamedComponent<TextMeshProUGUI>(testRoot.transform, "Victory Level Number") != null,
                "The live victory level number was not built.");
            Require(FindNamedTransform(testRoot.transform, "Approved Settled PERFECT Word Artwork") != null,
                "The final PERFECT artwork was not built.");
            hud.EditorShowMainMenuPreview();
            Require(IsNamedObjectActive(testRoot.transform, "StartMenuPanel"), "The Main Menu preview control did not open the menu.");
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuHomePage"), "The main menu did not select the Home page.");
            Require(!IsNamedObjectActive(testRoot.transform, "Defeat Overlay"), "Opening the main menu did not close the Loss overlay.");
            Require(!IsNamedObjectActive(testRoot.transform, "Victory Overlay"), "Opening the main menu did not close the Win overlay.");
            Require(FindNamedTransform(testRoot.transform, "Main Menu Heart HUD") != null, "The approved heart HUD was not built on the main menu.");
            Require(FindNamedComponent<TextMeshProUGUI>(testRoot.transform, "Heart HUD Count") != null, "The live heart count was not built.");
            Require(FindNamedComponent<TextMeshProUGUI>(testRoot.transform, "Heart HUD Timer") != null, "The live heart timer was not built.");
            Button heartHudPlus = FindNamedComponent<Button>(testRoot.transform, "Heart HUD Plus Button");
            Require(heartHudPlus != null, "The Heart HUD Plus button was not built.");
            Require(heartHudPlus.GetComponent<SimpleButtonPressAnimation>() != null, "The Heart HUD Plus button has no press animation.");
            heartHudPlus.onClick.Invoke();
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuShopPage"), "The Heart HUD Plus button did not open the Shop page.");

            Button shopTab = FindNamedComponent<Button>(testRoot.transform, "MainMenuShopTab");
            Button homeTab = FindNamedComponent<Button>(testRoot.transform, "MainMenuHomeTab");
            Button lockedTab = FindNamedComponent<Button>(testRoot.transform, "MainMenuLockedTab");
            Button menuPlay = FindNamedComponent<Button>(testRoot.transform, "MainMenuPlayButton");
            Require(shopTab != null && homeTab != null && lockedTab != null && menuPlay != null, "One or more main-menu buttons were not built.");
            Require(menuPlay.GetComponent<SimpleButtonPressAnimation>() != null, "The main-menu Play button has no press animation.");

            MainMenuTabScrubTarget shopScrub = shopTab.GetComponent<MainMenuTabScrubTarget>();
            MainMenuTabScrubTarget homeScrub = homeTab.GetComponent<MainMenuTabScrubTarget>();
            MainMenuTabScrubTarget lockedScrub = lockedTab.GetComponent<MainMenuTabScrubTarget>();
            Require(shopScrub != null && homeScrub != null && lockedScrub != null,
                "One or more main-menu tabs cannot track a held finger.");
            PointerEventData dragProbe = new PointerEventData(EventSystem.current) { useDragThreshold = true };
            shopScrub.OnInitializePotentialDrag(dragProbe);
            Require(!dragProbe.useDragThreshold, "The main-menu tab scrub still waits for the default drag threshold.");

            MethodInfo scrubTabs = typeof(CarPrototypeHud).GetMethod(
                "TrySelectMainMenuTabAtLocalPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(scrubTabs != null, "The continuous main-menu tab selector is missing.");
            scrubTabs.Invoke(hud, new object[] { testLayout.mainMenuHomeTabPosition });
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuHomePage"), "Scrubbing over Home did not select the Home page.");
            scrubTabs.Invoke(hud, new object[] { testLayout.mainMenuLockedTabPosition });
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuLockedPage"), "Scrubbing over Locked did not select the Locked page.");
            scrubTabs.Invoke(hud, new object[] { new Vector2(testLayout.mainMenuShopTabPosition.x, 0f) });
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuLockedPage"), "Dragging outside the tab strip caused an accidental page switch.");

            shopTab.onClick.Invoke();
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuShopPage"), "The Shop tab did not select its page.");
            lockedTab.onClick.Invoke();
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuLockedPage"), "The Locked tab did not select its page.");
            homeTab.onClick.Invoke();
            Require(IsNamedObjectActive(testRoot.transform, "MainMenuHomePage"), "The Home tab did not return to its page.");
            menuPlay.onClick.Invoke();
            Require(!IsNamedObjectActive(testRoot.transform, "StartMenuPanel"), "The Play button did not close the main menu.");

            hud.EditorShowLeavePreview();
            Button confirmLeave = FindNamedComponent<Button>(testRoot.transform, "Confirm Leave");
            Require(confirmLeave != null, "The leave-confirmation button was not built.");
            confirmLeave.onClick.Invoke();
            Require(IsNamedObjectActive(testRoot.transform, "StartMenuPanel"), "Confirming Quit did not return to the main menu.");
            hud.EditorShowGameplayPreview();
            Require(!IsNamedObjectActive(testRoot.transform, "StartMenuPanel"), "The Gameplay preview control did not close the main menu.");

            Debug.Log("[3D HUD Editor Verification] PASS: 3D scene fields, direct live apply, main menu, recursive visibility, and overlay previews are functional.");
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            if (testLayout != null) UnityEngine.Object.DestroyImmediate(testLayout);
            if (testRoot != null) UnityEngine.Object.DestroyImmediate(testRoot);
        }
    }

    private static void VerifyLimousineProgression(ColorSortLevelDatabase database)
    {
        MethodInfo buildPieces = typeof(CarPrototype3D).GetMethod("BuildPrototypePieceSpecs", BindingFlags.NonPublic | BindingFlags.Static);
        Require(buildPieces != null, "The post-level-30 limousine builder is missing.");

        for (int levelIndex = 0; levelIndex < 60; levelIndex++)
        {
            UnityGameManager.LevelConfig level = database.levels[levelIndex];
            Array specs = buildPieces.Invoke(null, new object[] { level }) as Array;
            Require(specs != null, $"Level {level.id} did not produce prototype pieces.");

            bool[,] occupied = new bool[level.boardSize, level.boardSize];
            int limousineCount = 0;
            int occupiedCellCount = 0;
            int redCount = 0;
            int greenCount = 0;

            foreach (object spec in specs)
            {
                Type specType = spec.GetType();
                int leadingRow = (int)specType.GetField("row").GetValue(spec);
                int leadingCol = (int)specType.GetField("col").GetValue(spec);
                int color = Convert.ToInt32(specType.GetField("color").GetValue(spec));
                int direction = Convert.ToInt32(specType.GetField("direction").GetValue(spec));
                int cellLength = (int)specType.GetField("cellLength").GetValue(spec);
                if (cellLength > 1) limousineCount++;
                if (color == 0) redCount++;
                if (color == 1) greenCount++;

                int rowStep = direction == 0 ? -1 : direction == 1 ? 1 : 0;
                int colStep = direction == 2 ? -1 : direction == 3 ? 1 : 0;
                for (int offset = 0; offset < cellLength; offset++)
                {
                    int row = leadingRow - rowStep * offset;
                    int col = leadingCol - colStep * offset;
                    Require(row >= 0 && row < level.boardSize && col >= 0 && col < level.boardSize,
                        $"Level {level.id} contains a limousine footprint outside the board.");
                    Require(!occupied[row, col], $"Level {level.id} contains overlapping prototype pieces at {row},{col}.");
                    occupied[row, col] = true;
                    occupiedCellCount++;
                }
            }

            Require(occupiedCellCount == level.blocks.Count, $"Level {level.id} no longer covers every original board cell.");
            Require(redCount == level.matchTarget && greenCount == level.matchTarget,
                $"Level {level.id} changed a required red or green color target.");
            if (level.id >= 51)
            {
                Require(level.boardSize == 5, $"Level {level.id} should use the new 5x5 board.");
                Require(level.matchTarget == 5, $"Level {level.id} should use a five-car matching tray.");
            }
            if (level.id <= 30)
                Require(limousineCount == 0, $"Level {level.id} introduced a limousine before level 31.");
            else if (level.id <= 40)
                Require(limousineCount == 1, $"Level {level.id} should introduce exactly one limousine.");
            else
                Require(limousineCount == 2, $"Level {level.id} should contain two limousines.");
        }

        Debug.Log("[3D Level Verification] PASS: levels 1-30 have no limousines, levels 31-40 have one, levels 41-60 have two, and levels 51-60 are valid 5x5 boards.");
    }

    private static bool IsNamedObjectActive(Transform root, string name)
    {
        Transform target = FindNamedTransform(root, name);
        return target != null && target.gameObject.activeSelf;
    }

    private static T FindNamedComponent<T>(Transform root, string name) where T : Component
    {
        Transform target = FindNamedTransform(root, name);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindNamedTransform(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index].gameObject.name == name)
                return transforms[index];
        }

        return null;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
    }

    private static bool IsFullScreenStretch(RectTransform rect)
    {
        return rect != null
            && Approximately(rect.anchorMin, Vector2.zero)
            && Approximately(rect.anchorMax, Vector2.one)
            && Approximately(rect.offsetMin, Vector2.zero)
            && Approximately(rect.offsetMax, Vector2.zero);
    }

    private static bool IsRectInside(
        Transform root,
        string name,
        float minimumX,
        float maximumX,
        float minimumY,
        float maximumY)
    {
        RectTransform rect = FindNamedComponent<RectTransform>(root, name);
        if (rect == null) return false;

        Vector2 center = rect.anchoredPosition;
        Vector2 halfSize = rect.sizeDelta * 0.5f;
        return center.x - halfSize.x >= minimumX
            && center.x + halfSize.x <= maximumX
            && center.y - halfSize.y >= minimumY
            && center.y + halfSize.y <= maximumY;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
