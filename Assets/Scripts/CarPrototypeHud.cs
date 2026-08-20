using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Standalone phone HUD for the isolated 3D car prototype. It deliberately does
/// not use the 2D game's launcher, so the two game modes remain independent.
/// </summary>
public sealed class CarPrototypeHud : MonoBehaviour
{
    private const int TutorialHighlightLayer = 31;
    private const float VictoryCelebrationDuration = 4f;
    private const float DifficultyBadgeTextYOffset = 4f;
    private const string NormalLevelBadgeResource = "level_badge_road_sign";
    private const string HardLevelBadgeResource = "level_badge_hard";
    private const string SuperHardLevelBadgeResource = "level_badge_super_hard";
    private const string MainMenuHardLevelButtonResource = "main_menu_level_hard";
    private const string MainMenuSuperHardLevelButtonResource = "main_menu_level_super_hard";
    private const float MainMenuDifficultyLevelTextYOffset = -40f;
    private static readonly Vector2 MainMenuDifficultyButtonSize = new Vector2(500f, 210f);
    private static readonly Vector2 MainMenuDifficultyTextSize = new Vector2(440f, 130f);
    private const float StartupLoadingDuration = 1.15f;
    private const float StartupLoadingFadeDuration = 0.18f;
    // Temporarily hide the colour/car objective indicator shown in the top-left.
    private const bool ShowTopLeftCarIndicator = false;
    // Lilita One is naturally wide. A slight negative tracking keeps labels
    // compact and playful, matching the close-set reference treatment.
    private const float GameTextCharacterSpacing = -1.25f;
    private static readonly Color GameTextOutlineColor = new Color(0.015f, 0.06f, 0.025f, 1f);
    private const float GameTextOutlineWidth = 0.16f;

    private enum MainMenuTab { Shop, Home, Locked }

    private sealed class OutcomeObjectiveRow
    {
        public GameObject root;
        public RawImage car;
        public TextMeshProUGUI counter;
        public RawImage check;
    }

    private sealed class VictoryLogoLayer
    {
        public RectTransform rect;
        public RawImage image;
        public Vector2 settledPosition;
        public float startTime;
        public float overshoot;
        public float entryRotation;
    }

    private sealed class PerfectLetterLayer
    {
        public RectTransform rect;
        public RawImage image;
        public Vector2 settledPosition;
        public float startTime;
        public float entryRotation;
        public float overshoot;
    }

    private static Sprite runtimeWhiteSprite;
    private static Material settingsDisabledMaterial;
    private static Material moreGreenPressMaterial;
    private static Material moreRedPressMaterial;

    private CarPrototype3D game;
    private CarPrototypeHudLayout layout;
    private TMP_FontAsset font;
    private Image levelPillImage;
    private Sprite normalLevelPillSprite;
    private Sprite hardLevelPillSprite;
    private Sprite superHardLevelPillSprite;
    private TextMeshProUGUI boardText;
    private Image mainMenuPlayButtonImage;
    private Sprite mainMenuPlayNormalSprite;
    private Sprite mainMenuPlayHardSprite;
    private Sprite mainMenuPlaySuperHardSprite;
    private TextMeshProUGUI mainMenuPlayText;
    private GameObject developmentControlsRoot;
    private Button developmentControlsToggleButton;
    private TextMeshProUGUI developmentControlsToggleText;
    private bool developmentControlsVisible;
    private Button demoAutoPlayButton;
    private TextMeshProUGUI demoAutoPlayText;
    private TextMeshProUGUI redText;
    private TextMeshProUGUI greenText;
    private TextMeshProUGUI blueText;
    private TextMeshProUGUI purpleText;
    private TextMeshProUGUI yellowText;
    private TextMeshProUGUI pinkText;
    private TextMeshProUGUI experimentalRuleText;
    private TextMeshProUGUI challengeLabButtonText;
    private Button challengeLabButton;
    private TextMeshProUGUI directionArrowButtonText;
    private Button directionArrowButton;
    private TextMeshProUGUI levelEditButtonText;
    private Button levelEditButton;
    private TextMeshProUGUI solvabilityCheckButtonText;
    private Button solvabilityCheckButton;
    private TextMeshProUGUI levelSaveButtonText;
    private Button levelSaveButton;
    private TextMeshProUGUI levelSwapButtonText;
    private Button levelSwapButton;
    private TextMeshProUGUI levelCarButtonText;
    private Button levelCarButton;
    private readonly Button[] levelCarColorButtons = new Button[7];
    private readonly TextMeshProUGUI[] levelCarColorButtonTexts = new TextMeshProUGUI[7];
    private readonly Button[] levelGarageCarSlotButtons =
        new Button[CarPrototype3D.MaximumEditableGarageQueueSlots];
    private readonly TextMeshProUGUI[] levelGarageCarSlotButtonTexts =
        new TextMeshProUGUI[CarPrototype3D.MaximumEditableGarageQueueSlots];
    private Button levelGarageCarAddButton;
    private TextMeshProUGUI levelGarageCarAddButtonText;
    private Button levelGarageCarDeleteButton;
    private TextMeshProUGUI levelGarageCarDeleteButtonText;
    private TextMeshProUGUI levelGarageButtonText;
    private Button levelGarageButton;
    private TextMeshProUGUI levelBoxButtonText;
    private Button levelBoxButton;
    private TextMeshProUGUI solvabilityResultText;
    private RawImage redObjectiveCar;
    private RawImage greenObjectiveCar;
    private RawImage blueObjectiveCar;
    private RawImage purpleObjectiveCar;
    private RawImage yellowObjectiveCar;
    private RawImage pinkObjectiveCar;
    private RawImage redObjectiveCheck;
    private RawImage greenObjectiveCheck;
    private RawImage blueObjectiveCheck;
    private RawImage purpleObjectiveCheck;
    private RawImage yellowObjectiveCheck;
    private RawImage pinkObjectiveCheck;
    private RectTransform heartsRoot;
    private readonly RawImage[] heartImages = new RawImage[3];
    private Texture2D fullHeartTexture;
    private Texture2D staleHeartTexture;
    private Texture2D brokenHeartTexture;
    private int displayedHearts = -1;
    private Coroutine defeatRevealCoroutine;
    private Transform hudRoot;
    private GameObject settingsRoot;
    private GameObject moreRoot;
    private GameObject leaveRoot;
    private GameObject defeatRoot;
    private GameObject victoryRoot;
    private GameObject victoryCelebrationRoot;
    private Image victoryCelebrationDim;
    private RectTransform victoryLogoRoot;
    private VictoryLogoLayer victoryBadgeLayer;
    private readonly VictoryLogoLayer[] victoryParkLayers = new VictoryLogoLayer[4];
    private VictoryLogoLayer victoryDashLayer;
    private VictoryLogoLayer victoryFinalLayer;
    private ParkDashVictoryEffectsGraphic victoryFireworksBack;
    private ParkDashVictoryEffectsGraphic victoryFireworksFront;
    private GameObject trafficJamRoot;
    private TextMeshProUGUI trafficJamSubtitleText;
    private TextMeshProUGUI trafficJamReturnNoteText;
    private TextMeshProUGUI trafficJamActionText;
    private Button trafficJamActionButton;
    private GameObject trafficJamShowcaseRoot;
    private RenderTexture trafficJamRenderTexture;
    private readonly OutcomeObjectiveRow[] victoryObjectiveRows = new OutcomeObjectiveRow[6];
    private RectTransform victoryPanelRoot;
    private TextMeshProUGUI victoryLevelText;
    private readonly PerfectLetterLayer[] victoryPerfectLetters = new PerfectLetterLayer[8];
    private RawImage victoryPerfectWord;
    private RectTransform victoryPerfectWordRect;
    private RectTransform victoryContinueArtwork;
    private RectTransform victoryCloseArtwork;
    private Coroutine victoryUiRevealCoroutine;
    private readonly RawImage[] defeatHeartImages = new RawImage[CarPrototypeHeartBank.MaximumHearts];
    private TextMeshProUGUI defeatHeartCountdownText;
    private Button lossRetryButton;
    private Coroutine defeatHeartBankCoroutine;
    private bool retryHeartConsumedForCurrentDefeat;
    private GameObject mainMenuRoot;
    private GameObject mainMenuHomePage;
    private GameObject mainMenuShopPage;
    private GameObject mainMenuLockedPage;
    private GameObject mainMenuHeartHudRoot;
    private TextMeshProUGUI mainMenuHeartCountText;
    private TextMeshProUGUI mainMenuHeartTimerText;
    private Coroutine mainMenuHeartHudCoroutine;
    private Image mainMenuShopTab;
    private Image mainMenuHomeTab;
    private Image mainMenuLockedTab;
    private Sprite menuShopNormalSprite;
    private Sprite menuShopTallSprite;
    private Sprite menuHomeNormalSprite;
    private Sprite menuHomeTallSprite;
    private Sprite menuLockedNormalSprite;
    private Sprite menuLockedTallSprite;
    private MainMenuTab selectedMainMenuTab = MainMenuTab.Home;
    private GameObject startupLoadingRoot;
    private CanvasGroup startupLoadingCanvasGroup;
    private RectTransform startupLoadingLogoRect;
    private TextMeshProUGUI startupLoadingText;
    private Coroutine startupLoadingCoroutine;
    private bool hapticsOn = true;
    private bool soundOn = true;
    private bool musicOn = true;
    private RectTransform tutorialRoot;
    private Image tutorialSpotlightDim;
    private RawImage tutorialFocusedCar;
    private Camera tutorialFocusCamera;
    private RenderTexture tutorialFocusTexture;
    private Transform tutorialFocusTarget;
    private readonly Dictionary<Transform, int> tutorialFocusOriginalLayers =
        new Dictionary<Transform, int>();
    private RectTransform tutorialHandRoot;
    private RectTransform tutorialMessagePanel;
    private TextMeshProUGUI tutorialTitleText;
    private TextMeshProUGUI tutorialMessageText;

    public void Initialize(CarPrototype3D prototype)
    {
        game = prototype;
        CarPrototypeFeedback.EnsureExists();
        hapticsOn = CarPrototypeFeedback.HapticsEnabled;
        soundOn = CarPrototypeFeedback.SoundEnabled;
        musicOn = CarPrototypeFeedback.MusicEnabled;
        layout = CarPrototypeHudLayout.LoadOrDefault();
        font = layout.fontOverride != null ? layout.fontOverride : TMP_Settings.defaultFontAsset;
        BuildCanvas();
    }

    public void Refresh(
        int boardNumber,
        int redCleared,
        int greenCleared,
        int blueCleared,
        int purpleCleared,
        int yellowCleared,
        int pinkCleared,
        int redGoal,
        int greenGoal,
        int blueGoal,
        int purpleGoal,
        int yellowGoal,
        int pinkGoal,
        int hearts,
        int trayCapacity,
        bool extraSlotUsed)
    {
        if (boardText == null) return;

        string levelMode = game.IsDemoMode ? "DEMO" : game.LevelDifficultyLabel;
        ApplyLevelBadgePresentation(levelMode);
        string displayedLevelLabel = levelMode == "DEMO" ? "DEMO" : "LEVEL";
        boardText.text = $"{displayedLevelLabel} <size=112%>{boardNumber}</size>";
        boardText.fontSize = layout.levelTextFontSize;
        bool hasRedGoal = redGoal > 0;
        bool hasGreenGoal = greenGoal > 0;
        bool hasBlueGoal = blueGoal > 0;
        bool hasPurpleGoal = purpleGoal > 0;
        bool hasYellowGoal = yellowGoal > 0;
        bool hasPinkGoal = pinkGoal > 0;
        RefreshObjective(redObjectiveCar, redText, redObjectiveCheck, hasRedGoal,
            hasRedGoal && redCleared >= redGoal, game.RedObjectiveRemaining);
        RefreshObjective(greenObjectiveCar, greenText, greenObjectiveCheck, hasGreenGoal,
            hasGreenGoal && greenCleared >= greenGoal, game.GreenObjectiveRemaining);
        RefreshObjective(blueObjectiveCar, blueText, blueObjectiveCheck, hasBlueGoal,
            hasBlueGoal && blueCleared >= blueGoal, game.BlueObjectiveRemaining);
        RefreshObjective(purpleObjectiveCar, purpleText, purpleObjectiveCheck, hasPurpleGoal,
            hasPurpleGoal && purpleCleared >= purpleGoal, game.PurpleObjectiveRemaining);
        RefreshObjective(yellowObjectiveCar, yellowText, yellowObjectiveCheck, hasYellowGoal,
            hasYellowGoal && yellowCleared >= yellowGoal, game.YellowObjectiveRemaining);
        RefreshObjective(pinkObjectiveCar, pinkText, pinkObjectiveCheck, hasPinkGoal,
            hasPinkGoal && pinkCleared >= pinkGoal, game.PinkObjectiveRemaining);
        ArrangeVisibleObjectives(new[] { hasRedGoal, hasGreenGoal, hasBlueGoal, hasPurpleGoal, hasYellowGoal, hasPinkGoal });
        UpdateHeartsPosition();
        if (heartsRoot != null)
            heartsRoot.gameObject.SetActive(game.HeartsEnabled);
        if (game.HeartsEnabled)
            RefreshHearts(hearts);
        if (experimentalRuleText != null)
        {
            string ruleStatus = game.ExperimentalRuleStatus;
            experimentalRuleText.gameObject.SetActive(!string.IsNullOrEmpty(ruleStatus));
            experimentalRuleText.text = ruleStatus;
        }
        bool showChallengeLab = layout.showLevelPreviewButtons && game.ChallengeLabAvailable;
        if (challengeLabButton != null)
            challengeLabButton.gameObject.SetActive(showChallengeLab);
        if (challengeLabButtonText != null)
        {
            challengeLabButtonText.gameObject.SetActive(showChallengeLab);
            challengeLabButtonText.text = game.IsChallengeLabActive ? "GAME" : "LAB";
        }
        RefreshDirectionArrowToggle();
        RefreshLevelEditControls();
        RefreshDevelopmentControlsVisibility();
    }

    private void Update()
    {
        UpdateTutorialOverlay();
    }

    private void OnDestroy()
    {
        if (trafficJamRenderTexture != null)
        {
            trafficJamRenderTexture.Release();
            Destroy(trafficJamRenderTexture);
            trafficJamRenderTexture = null;
        }
        if (trafficJamShowcaseRoot != null)
            Destroy(trafficJamShowcaseRoot);
        RestoreTutorialFocusLayers();
        if (tutorialFocusCamera != null)
            Destroy(tutorialFocusCamera.gameObject);
        ReleaseTutorialFocusTexture();
    }

    private static void RefreshObjective(
        RawImage car,
        TextMeshProUGUI counter,
        RawImage check,
        bool isVisible,
        bool isComplete,
        int remaining)
    {
        if (car != null) car.gameObject.SetActive(ShowTopLeftCarIndicator && isVisible);
        if (counter != null)
        {
            counter.gameObject.SetActive(ShowTopLeftCarIndicator && isVisible && !isComplete);
            if (isVisible && !isComplete)
                counter.text = Mathf.Max(0, remaining).ToString();
        }
        if (check != null) check.gameObject.SetActive(ShowTopLeftCarIndicator && isVisible && isComplete);
    }

    private void ArrangeVisibleObjectives(bool[] visible)
    {
        RawImage[] cars =
        {
            redObjectiveCar,
            greenObjectiveCar,
            blueObjectiveCar,
            purpleObjectiveCar,
            yellowObjectiveCar,
            pinkObjectiveCar
        };
        TextMeshProUGUI[] counters = { redText, greenText, blueText, purpleText, yellowText, pinkText };
        RawImage[] checks =
        {
            redObjectiveCheck,
            greenObjectiveCheck,
            blueObjectiveCheck,
            purpleObjectiveCheck,
            yellowObjectiveCheck,
            pinkObjectiveCheck
        };
        Vector2[] carPositions =
        {
            layout.redObjectiveCarPosition,
            layout.greenObjectiveCarPosition,
            layout.blueObjectiveCarPosition,
            layout.yellowObjectiveCarPosition,
            layout.yellowObjectiveCarPosition
                + (layout.yellowObjectiveCarPosition - layout.blueObjectiveCarPosition),
            layout.yellowObjectiveCarPosition
                + (layout.yellowObjectiveCarPosition - layout.blueObjectiveCarPosition) * 1.75f
        };
        Vector2[] statusPositions =
        {
            layout.redObjectiveStatusPosition,
            layout.greenObjectiveStatusPosition,
            layout.blueObjectiveStatusPosition,
            layout.yellowObjectiveStatusPosition,
            layout.yellowObjectiveStatusPosition
                + (layout.yellowObjectiveStatusPosition - layout.blueObjectiveStatusPosition),
            layout.yellowObjectiveStatusPosition
                + (layout.yellowObjectiveStatusPosition - layout.blueObjectiveStatusPosition) * 1.75f
        };

        int visibleIndex = 0;
        for (int index = 0; index < visible.Length; index++)
        {
            if (!visible[index]) continue;
            int slot = Mathf.Min(visibleIndex, carPositions.Length - 1);
            if (cars[index] != null)
                cars[index].rectTransform.anchoredPosition = carPositions[slot];
            if (counters[index] != null)
                counters[index].rectTransform.anchoredPosition = statusPositions[slot];
            if (checks[index] != null)
                checks[index].rectTransform.anchoredPosition = statusPositions[slot];
            visibleIndex++;
        }
    }

    private void UpdateHeartsPosition()
    {
        if (heartsRoot != null)
            heartsRoot.anchoredPosition = layout.heartsPosition;
    }

    public void ShowDefeat(bool waitForHeartAnimation = true)
    {
        if (defeatRoot == null) return;
        ResetTrafficJamUi();
        CarPrototypeFeedback.Defeat();
        StopMainMenuHeartHudUpdates();
        if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
        HidePauseOverlays();
        if (game.HeartsEnabled && !retryHeartConsumedForCurrentDefeat)
        {
            CarPrototypeHeartBank.TryConsumeHeart();
            retryHeartConsumedForCurrentDefeat = true;
        }
        if (game.HeartsEnabled)
            RefreshDefeatHeartBank();
        game.TogglePause(true);

        if (defeatRevealCoroutine != null)
            StopCoroutine(defeatRevealCoroutine);
        if (game.HeartsEnabled && waitForHeartAnimation)
        {
            defeatRevealCoroutine = StartCoroutine(RevealDefeatAfterHeartLoss());
            return;
        }

        ActivateDefeatOutcome();
    }

    public void ShowVictory(bool playFeedback = true)
    {
        if (victoryRoot == null) return;
        ResetTrafficJamUi();
        HideVictoryCelebration();
        if (playFeedback) CarPrototypeFeedback.Victory();
        StopMainMenuHeartHudUpdates();
        if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
        HidePauseOverlays();
        StopDefeatHeartBankUpdates();
        retryHeartConsumedForCurrentDefeat = false;
        if (defeatRoot != null) defeatRoot.SetActive(false);
        RefreshOutcomeObjectives(victoryObjectiveRows, true);
        RefreshVictoryPanelText();
        victoryRoot.SetActive(true);
        victoryRoot.transform.SetAsLastSibling();
        StartVictoryUiReveal();
        game.TogglePause(true);
    }

    /// <summary>
    /// Called by the 3D HUD Layout Editor while Play Mode is running. The HUD is
    /// built at runtime, so its RectTransforms need an explicit refresh after an
    /// asset value changes.
    /// </summary>
    public bool ApplyLayoutFromEditor(CarPrototypeHudLayout editedLayout = null)
    {
        if (editedLayout != null) layout = editedLayout;
        else layout = CarPrototypeHudLayout.LoadOrDefault();

        if (layout == null || hudRoot == null || settingsRoot == null || moreRoot == null || leaveRoot == null
            || defeatRoot == null || victoryRoot == null || mainMenuRoot == null || game == null)
            return false;

        if (!game.Apply3DSettingsFromEditor(layout))
            return false;

        font = layout.fontOverride != null ? layout.fontOverride : TMP_Settings.defaultFontAsset;

        ApplyRect(hudRoot, "Level Pill", layout.levelPillPosition, layout.levelPillSize);
        ApplyRect(hudRoot, "Level Text", layout.levelTextPosition, layout.levelTextSize);
        ApplyTextStyle(hudRoot, "Level Text", layout.levelTextFontSize);
        ApplyLevelBadgePresentation(game.IsDemoMode ? "DEMO" : game.LevelDifficultyLabel);
        ApplyObjectiveLayout("Red", layout.redObjectiveCarPosition, layout.redObjectiveStatusPosition);
        ApplyObjectiveLayout("Green", layout.greenObjectiveCarPosition, layout.greenObjectiveStatusPosition);
        ApplyObjectiveLayout("Blue", layout.blueObjectiveCarPosition, layout.blueObjectiveStatusPosition);
        ApplyObjectiveLayout("Purple", layout.yellowObjectiveCarPosition, layout.yellowObjectiveStatusPosition);
        ApplyObjectiveLayout("Yellow", layout.yellowObjectiveCarPosition, layout.yellowObjectiveStatusPosition);
        ApplyObjectiveLayout("Pink", layout.yellowObjectiveCarPosition, layout.yellowObjectiveStatusPosition);
        ApplyHeartLayout();

        ApplyRect(mainMenuRoot.transform, "MainMenuPlayButton", layout.mainMenuPlayPosition, layout.mainMenuPlaySize);
        ApplyRect(mainMenuRoot.transform, "MainMenuPlayButtonText", Vector2.zero, layout.mainMenuPlaySize);
        ApplyTextStyle(mainMenuRoot.transform, "MainMenuPlayButtonText", layout.mainMenuPlayFontSize);
        mainMenuPlayText = FindNamedComponent<TextMeshProUGUI>(mainMenuRoot.transform, "MainMenuPlayButtonText");
        RefreshMainMenuPlayLabel();
        ApplyRect(mainMenuRoot.transform, "MainMenuDemoButton", layout.mainMenuDemoPosition, layout.mainMenuDemoSize);
        ApplyRect(mainMenuRoot.transform, "MainMenuDemoButtonText", Vector2.zero, layout.mainMenuDemoSize);
        ApplyTextStyle(mainMenuRoot.transform, "MainMenuDemoButtonText", layout.mainMenuDemoFontSize);
        TextMeshProUGUI demoText = FindNamedComponent<TextMeshProUGUI>(mainMenuRoot.transform, "MainMenuDemoButtonText");
        if (demoText != null) demoText.text = layout.mainMenuDemoText;
        ApplyMainMenuTabState(mainMenuShopTab, selectedMainMenuTab == MainMenuTab.Shop, menuShopNormalSprite, menuShopTallSprite, layout.mainMenuShopTabPosition);
        ApplyMainMenuTabState(mainMenuHomeTab, selectedMainMenuTab == MainMenuTab.Home, menuHomeNormalSprite, menuHomeTallSprite, layout.mainMenuHomeTabPosition);
        ApplyMainMenuTabState(mainMenuLockedTab, selectedMainMenuTab == MainMenuTab.Locked, menuLockedNormalSprite, menuLockedTallSprite, layout.mainMenuLockedTabPosition);
        ApplyRect(mainMenuRoot.transform, "MainMenuShopTabLabel", layout.mainMenuSelectedLabelOffset, layout.mainMenuSelectedLabelSize);
        ApplyRect(mainMenuRoot.transform, "MainMenuHomeTabLabel", layout.mainMenuSelectedLabelOffset, layout.mainMenuSelectedLabelSize);
        ApplyRect(mainMenuRoot.transform, "MainMenuLockedTabLabel", layout.mainMenuSelectedLabelOffset, layout.mainMenuSelectedLabelSize);
        ApplyTextStyle(mainMenuRoot.transform, "MainMenuShopTabLabel", layout.mainMenuSelectedLabelFontSize);
        ApplyTextStyle(mainMenuRoot.transform, "MainMenuHomeTabLabel", layout.mainMenuSelectedLabelFontSize);
        ApplyTextStyle(mainMenuRoot.transform, "MainMenuLockedTabLabel", layout.mainMenuSelectedLabelFontSize);

        ApplyRect(hudRoot, "Pause Button", layout.pausePosition, layout.pauseSize);
        Vector2 developmentTogglePosition = layout.previousLevelPosition + new Vector2(0f, 200f);
        Vector2 developmentToggleSize = new Vector2(150f, 54f);
        ApplyRect(hudRoot, "Development Controls Toggle", developmentTogglePosition, developmentToggleSize);
        ApplyRect(hudRoot, "Development Controls Toggle Text", Vector2.zero,
            developmentToggleSize - new Vector2(10f, 6f));
        ApplyButtonAndLabel(hudRoot, "Previous Level", "Previous Level Text", layout.previousLevelPosition, layout.levelPreviewButtonSize);
        ApplyButtonAndLabel(hudRoot, "Next Level", "Next Level Text", layout.nextLevelPosition, layout.levelPreviewButtonSize);
        Vector2 directionArrowPosition = layout.nextLevelPosition + new Vector2(0f, -200f);
        ApplyButtonAndLabel(hudRoot, "Direction Arrows Toggle", "Direction Arrows Toggle Text",
            directionArrowPosition, layout.levelPreviewButtonSize);
        Vector2 levelEditPosition = directionArrowPosition + new Vector2(0f, -100f);
        ApplyButtonAndLabel(hudRoot, "Level Edit Mode", "Level Edit Mode Text",
            levelEditPosition, layout.levelPreviewButtonSize);
        ApplyButtonAndLabel(hudRoot, "Check Solvability", "Check Solvability Text",
            new Vector2(-120f, -1005f), new Vector2(340f, 76f));
        ApplyButtonAndLabel(hudRoot, "Save Edited Level", "Save Edited Level Text",
            new Vector2(240f, -1005f), new Vector2(250f, 76f));
        ApplyRect(hudRoot, "Solvability Result", new Vector2(0f, -920f), new Vector2(620f, 70f));
        SetNamedActive(hudRoot, "Previous Level", layout.showLevelPreviewButtons);
        SetNamedActive(hudRoot, "Previous Level Text", layout.showLevelPreviewButtons);
        SetNamedActive(hudRoot, "Next Level", layout.showLevelPreviewButtons);
        SetNamedActive(hudRoot, "Next Level Text", layout.showLevelPreviewButtons);
        SetNamedActive(hudRoot, "Direction Arrows Toggle", layout.showLevelPreviewButtons);
        SetNamedActive(hudRoot, "Direction Arrows Toggle Text", layout.showLevelPreviewButtons);
        RefreshLevelEditControls();
        RefreshDevelopmentControlsVisibility();

        ApplyRect(settingsRoot.transform, "Settings Tray", layout.settingsPanelPosition, layout.settingsPanelSize);
        ApplySettingsTileLayout("Haptics", layout.hapticsPosition);
        ApplySettingsTileLayout("Sounds", layout.soundsPosition);
        ApplySettingsTileLayout("Music", layout.musicPosition);
        ApplyButtonAndLabel(settingsRoot.transform, "Resume", "Resume Text", layout.resumePosition,
            ScaleSettingsArtworkSize(new Vector2(417f, 234f)));
        ApplyButtonAndLabel(settingsRoot.transform, "Quit", "Quit Text", layout.quitPosition,
            ScaleSettingsArtworkSize(new Vector2(417f, 234f)));
        ApplyRect(settingsRoot.transform, "More", layout.morePosition,
            ScaleSettingsArtworkSize(new Vector2(260f, 100f)));
        ApplyRect(settingsRoot.transform, "Close", layout.settingsClosePosition,
            ScaleSettingsArtworkSize(new Vector2(125f, 125f)));

        ApplyRect(moreRoot.transform, "More Tray", layout.morePanelPosition, layout.morePanelSize);
        ApplyRect(moreRoot.transform, "Terms", layout.termsPosition,
            ScaleMoreArtworkSize(new Vector2(548f, 234f)));
        ApplyRect(moreRoot.transform, "Privacy", layout.privacyPosition,
            ScaleMoreArtworkSize(new Vector2(548f, 234f)));
        ApplyRect(moreRoot.transform, "Return", layout.moreBackPosition,
            ScaleMoreArtworkSize(new Vector2(335f, 188f)));
        ApplyRect(moreRoot.transform, "Close", layout.moreClosePosition,
            ScaleMoreArtworkSize(new Vector2(125f, 125f)));

        ApplyRect(leaveRoot.transform, "Leave Tray", Vector2.zero, layout.leavePanelSize);
        ApplyRect(leaveRoot.transform, "Leave Title", layout.leaveTitlePosition, new Vector2(620f, 86f));
        ApplyRect(leaveRoot.transform, "Leave Description", layout.leaveDescriptionPosition, new Vector2(630f, 64f));
        ApplyButtonAndLabel(leaveRoot.transform, "Cancel Leave", "Cancel Leave Text", layout.leaveCancelPosition, new Vector2(280f, 90f));
        ApplyButtonAndLabel(leaveRoot.transform, "Confirm Leave", "Confirm Leave Text", layout.leaveConfirmPosition, new Vector2(280f, 90f));

        foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
            text.font = font;

        Refresh(
            game.BoardNumber,
            game.RedCleared,
            game.GreenCleared,
            game.BlueCleared,
            game.PurpleCleared,
            game.YellowCleared,
            game.PinkCleared,
            game.RedGoal,
            game.GreenGoal,
            game.BlueGoal,
            game.PurpleGoal,
            game.YellowGoal,
            game.PinkGoal,
            game.Hearts,
            game.TrayCapacity,
            game.ExtraSlotUsed);
        Canvas.ForceUpdateCanvases();
        return true;
    }

    private void ApplyObjectiveLayout(string colorName, Vector2 carPosition, Vector2 statusPosition)
    {
        ApplyRect(hudRoot, colorName + " Objective Car", carPosition, layout.objectiveCarSize);
        ApplyRect(hudRoot, colorName + " Objective Counter", statusPosition, layout.objectiveStatusSize);
        ApplyTextStyle(hudRoot, colorName + " Objective Counter", layout.objectiveStatusFontSize);
        ApplyRect(hudRoot, colorName + " Objective Check", statusPosition, layout.objectiveCheckSize);
    }

    private void ApplySettingsTileLayout(string label, Vector2 position)
    {
        ApplyRect(settingsRoot.transform, label + " Text", position + new Vector2(0f, 95f), new Vector2(204f, 48f));
        ApplyRect(settingsRoot.transform, label + " Toggle", position,
            ScaleSettingsArtworkSize(new Vector2(256f, 254f)));
    }

    public void EditorShowGameplayPreview()
    {
        if (!IsHudReady()) return;
        StopMainMenuHeartHudUpdates();
        mainMenuRoot.SetActive(false);
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        HideOutcomeRoots();
        game.TogglePause(false);
    }

    public void EditorShowMainMenuPreview()
    {
        ShowMainMenu();
    }

    public void EditorShowSettingsPreview()
    {
        if (!IsHudReady()) return;
        HideOutcomeRoots();
        OpenSettings();
    }

    public void EditorShowMorePreview()
    {
        if (!IsHudReady()) return;
        HideOutcomeRoots();
        settingsRoot.SetActive(false);
        leaveRoot.SetActive(false);
        moreRoot.SetActive(true);
        game.TogglePause(true);
    }

    public void EditorShowLeavePreview()
    {
        if (!IsHudReady()) return;
        HideOutcomeRoots();
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(true);
        game.TogglePause(true);
    }

    public void EditorShowDefeatPreview()
    {
        if (!IsHudReady()) return;
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        HideOutcomeRoots();
        ActivateDefeatOutcome();
        game.TogglePause(true);
    }

    public void EditorShowVictoryPreview()
    {
        if (!IsHudReady()) return;
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        HideOutcomeRoots();
        RefreshOutcomeObjectives(victoryObjectiveRows, true);
        RefreshVictoryPanelText();
        victoryRoot.SetActive(true);
        StartVictoryUiReveal();
        game.TogglePause(true);
    }

    public void EditorShowTrafficJamPreview()
    {
        if (!IsHudReady()) return;
        HideOutcomeRoots();
        ShowTrafficJam();
    }

    public void EditorLoadPreviousBoard()
    {
        if (game != null) game.LoadPreviousLevel();
    }

    public void EditorLoadNextBoard()
    {
        if (game != null) game.LoadNextLevel();
    }

    private bool IsHudReady()
    {
        return game != null && settingsRoot != null && moreRoot != null && leaveRoot != null
            && defeatRoot != null && victoryRoot != null && mainMenuRoot != null;
    }

    private void BuildCanvas()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("Car Prototype EventSystem", typeof(EventSystem));
            eventSystem.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        GameObject canvasObject = new GameObject("Car Prototype HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        hudRoot = canvasObject.transform;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.55f;

        BuildTopHud(canvasObject.transform);
        BuildBottomControls(canvasObject.transform);
        BuildTutorialOverlay(canvasObject.transform);
        BuildSettings(canvasObject.transform);
        BuildMorePage(canvasObject.transform);
        BuildLeaveConfirmation(canvasObject.transform);
        BuildMainMenuCanvas();
        BuildOutcomeCanvas();
    }

    private void BuildTopHud(Transform parent)
    {
        // Only the level number remains live text. Hard and Super Hard levels
        // swap the backing artwork, whose lower tab already carries the
        // difficulty label; mechanic counts never belong in this badge.
        normalLevelPillSprite = LoadSprite(NormalLevelBadgeResource);
        hardLevelPillSprite = LoadSprite(HardLevelBadgeResource);
        superHardLevelPillSprite = LoadSprite(SuperHardLevelBadgeResource);
        levelPillImage = CreateImage(parent, "Level Pill", normalLevelPillSprite, layout.levelPillPosition, layout.levelPillSize);
        levelPillImage.preserveAspect = true;

        boardText = CreateText(parent, "Level Text", "LEVEL <size=112%>1</size>", layout.levelTextPosition, layout.levelTextSize, layout.levelTextFontSize, TextAlignmentOptions.Center);
        boardText.fontStyle = FontStyles.Bold;
        boardText.outlineColor = new Color32(5, 28, 72, 255);
        boardText.outlineWidth = 0.18f;

        redObjectiveCar = CreateRawImage(parent, "Red Objective Car", LoadTexture("objective_car_red"),
            layout.redObjectiveCarPosition, layout.objectiveCarSize);
        greenObjectiveCar = CreateRawImage(parent, "Green Objective Car", LoadTexture("objective_car_green"),
            layout.greenObjectiveCarPosition, layout.objectiveCarSize);
        blueObjectiveCar = CreateRawImage(parent, "Blue Objective Car", LoadTexture("objective_car_blue"),
            layout.blueObjectiveCarPosition, layout.objectiveCarSize);
        purpleObjectiveCar = CreateRawImage(parent, "Purple Objective Car", LoadTexture("objective_car_purple"),
            layout.yellowObjectiveCarPosition, layout.objectiveCarSize);
        yellowObjectiveCar = CreateRawImage(parent, "Yellow Objective Car", LoadTexture("objective_car_yellow"),
            layout.yellowObjectiveCarPosition, layout.objectiveCarSize);
        pinkObjectiveCar = CreateRawImage(parent, "Pink Objective Car", LoadTexture("objective_car_purple"),
            layout.yellowObjectiveCarPosition, layout.objectiveCarSize);
        pinkObjectiveCar.color = new Color(1f, 0.42f, 0.78f);

        redText = CreateObjectiveCounter(parent, "Red Objective Counter", layout.redObjectiveStatusPosition);
        greenText = CreateObjectiveCounter(parent, "Green Objective Counter", layout.greenObjectiveStatusPosition);
        blueText = CreateObjectiveCounter(parent, "Blue Objective Counter", layout.blueObjectiveStatusPosition);
        purpleText = CreateObjectiveCounter(parent, "Purple Objective Counter", layout.yellowObjectiveStatusPosition);
        yellowText = CreateObjectiveCounter(parent, "Yellow Objective Counter", layout.yellowObjectiveStatusPosition);
        pinkText = CreateObjectiveCounter(parent, "Pink Objective Counter", layout.yellowObjectiveStatusPosition);

        Texture2D checkTexture = LoadTexture("objective_complete_check");
        redObjectiveCheck = CreateRawImage(parent, "Red Objective Check", checkTexture,
            layout.redObjectiveStatusPosition, layout.objectiveCheckSize);
        greenObjectiveCheck = CreateRawImage(parent, "Green Objective Check", checkTexture,
            layout.greenObjectiveStatusPosition, layout.objectiveCheckSize);
        blueObjectiveCheck = CreateRawImage(parent, "Blue Objective Check", checkTexture,
            layout.blueObjectiveStatusPosition, layout.objectiveCheckSize);
        purpleObjectiveCheck = CreateRawImage(parent, "Purple Objective Check", checkTexture,
            layout.yellowObjectiveStatusPosition, layout.objectiveCheckSize);
        yellowObjectiveCheck = CreateRawImage(parent, "Yellow Objective Check", checkTexture,
            layout.yellowObjectiveStatusPosition, layout.objectiveCheckSize);
        pinkObjectiveCheck = CreateRawImage(parent, "Pink Objective Check", checkTexture,
            layout.yellowObjectiveStatusPosition, layout.objectiveCheckSize);

        redObjectiveCheck.gameObject.SetActive(false);
        greenObjectiveCheck.gameObject.SetActive(false);
        blueObjectiveCheck.gameObject.SetActive(false);
        purpleObjectiveCheck.gameObject.SetActive(false);
        yellowObjectiveCheck.gameObject.SetActive(false);
        pinkObjectiveCheck.gameObject.SetActive(false);

        fullHeartTexture = LoadTexture("heart_full");
        staleHeartTexture = LoadTexture("heart_stale");
        brokenHeartTexture = LoadTexture("heart_broken_borderless");
        BuildHearts(parent);

        // Optional development-rule samples keep their compact strip outside
        // the permanent HUD layout.
        experimentalRuleText = CreateText(parent, "Experimental Rule Status", string.Empty,
            new Vector2(0f, 650f), new Vector2(650f, 92f), 24f, TextAlignmentOptions.Center);
        experimentalRuleText.fontStyle = FontStyles.Bold;
        experimentalRuleText.outlineColor = new Color32(5, 28, 72, 255);
        experimentalRuleText.outlineWidth = 0.16f;
        experimentalRuleText.textWrappingMode = TextWrappingModes.Normal;
        experimentalRuleText.gameObject.SetActive(false);
    }

    private void ApplyLevelBadgePresentation(string levelMode)
    {
        bool isHard = levelMode == "HARD";
        bool isSuperHard = levelMode == "SUPER HARD";

        if (levelPillImage != null)
        {
            Sprite difficultySprite = isSuperHard
                ? superHardLevelPillSprite
                : isHard ? hardLevelPillSprite : normalLevelPillSprite;
            levelPillImage.sprite = difficultySprite != null ? difficultySprite : normalLevelPillSprite;
            levelPillImage.preserveAspect = true;
        }

        if (boardText != null)
        {
            Vector2 textPosition = layout.levelTextPosition;
            if (isHard || isSuperHard)
                textPosition.y += DifficultyBadgeTextYOffset;
            boardText.rectTransform.anchoredPosition = textPosition;
            boardText.rectTransform.sizeDelta = layout.levelTextSize;
        }
    }

    private void BuildBottomControls(Transform parent)
    {
        Button pauseButton = CreateButton(parent, "Pause Button", LoadSprite("pause_button"), layout.pausePosition, layout.pauseSize, OpenSettings);
        AddScalePressAnimation(pauseButton);

        developmentControlsRoot = CreateFullScreenRoot(parent, "Development Controls");
        Transform developmentParent = developmentControlsRoot.transform;

        Button previousLevelButton = CreateButton(developmentParent, "Previous Level", LoadSprite("settings_resume_button"), layout.previousLevelPosition, layout.levelPreviewButtonSize, game.LoadPreviousLevel);
        AddScalePressAnimation(previousLevelButton);
        TextMeshProUGUI previousLevelText = CreateText(developmentParent, "Previous Level Text", "BACK", layout.previousLevelPosition, layout.levelPreviewButtonSize - new Vector2(16f, 8f), 25f, TextAlignmentOptions.Center);
        previousLevelButton.gameObject.SetActive(layout.showLevelPreviewButtons);
        previousLevelText.gameObject.SetActive(layout.showLevelPreviewButtons);

        Button nextLevelButton = CreateButton(developmentParent, "Next Level", LoadSprite("settings_resume_button"), layout.nextLevelPosition, layout.levelPreviewButtonSize, game.LoadNextLevel);
        AddScalePressAnimation(nextLevelButton);
        TextMeshProUGUI nextLevelText = CreateText(developmentParent, "Next Level Text", "NEXT", layout.nextLevelPosition, layout.levelPreviewButtonSize - new Vector2(16f, 8f), 25f, TextAlignmentOptions.Center);
        nextLevelButton.gameObject.SetActive(layout.showLevelPreviewButtons);
        nextLevelText.gameObject.SetActive(layout.showLevelPreviewButtons);

        Vector2 autoPlayPosition = layout.previousLevelPosition + new Vector2(0f, 100f);
        Vector2 autoPlaySize = new Vector2(180f, 64f);
        demoAutoPlayButton = CreateButton(developmentParent, "Demo Auto Play", LoadSprite("settings_resume_button"), autoPlayPosition, autoPlaySize, game.ToggleDemoAutoPlay);
        AddScalePressAnimation(demoAutoPlayButton);
        demoAutoPlayText = CreateText(demoAutoPlayButton.transform, "Demo Auto Play Text", "AUTO PLAY", Vector2.zero, autoPlaySize - new Vector2(12f, 8f), 22f, TextAlignmentOptions.Center);
        demoAutoPlayButton.gameObject.SetActive(false);

        // Optional post-campaign prototype switch. It remains hidden while the
        // refined 100-level catalog is the only enabled campaign content.
        Vector2 challengeLabPosition = layout.nextLevelPosition + new Vector2(0f, -100f);
        challengeLabButton = CreateButton(developmentParent, "Challenge Lab", LoadSprite("settings_resume_button"),
            challengeLabPosition, layout.levelPreviewButtonSize, game.ToggleChallengeLab);
        AddScalePressAnimation(challengeLabButton);
        challengeLabButtonText = CreateText(developmentParent, "Challenge Lab Text", "LAB",
            challengeLabPosition, layout.levelPreviewButtonSize - new Vector2(16f, 8f), 25f, TextAlignmentOptions.Center);
        challengeLabButton.gameObject.SetActive(layout.showLevelPreviewButtons && game.ChallengeLabAvailable);
        challengeLabButtonText.gameObject.SetActive(layout.showLevelPreviewButtons && game.ChallengeLabAvailable);

        // Temporary review control. It shares the developer-preview column so
        // every board arrow can be removed or restored with one tap.
        Vector2 directionArrowPosition = challengeLabPosition + new Vector2(0f, -100f);
        directionArrowButton = CreateButton(developmentParent, "Direction Arrows Toggle", LoadSprite("settings_resume_button"),
            directionArrowPosition, layout.levelPreviewButtonSize, game.ToggleDirectionArrows);
        AddScalePressAnimation(directionArrowButton);
        directionArrowButtonText = CreateText(developmentParent, "Direction Arrows Toggle Text", "ARROWS ON",
            directionArrowPosition, layout.levelPreviewButtonSize - new Vector2(10f, 8f), 20f, TextAlignmentOptions.Center);
        directionArrowButton.gameObject.SetActive(layout.showLevelPreviewButtons);
        directionArrowButtonText.gameObject.SetActive(layout.showLevelPreviewButtons);
        RefreshDirectionArrowToggle();

        Vector2 levelEditPosition = directionArrowPosition + new Vector2(0f, -100f);
        levelEditButton = CreateButton(developmentParent, "Level Edit Mode", LoadSprite("settings_resume_button"),
            levelEditPosition, layout.levelPreviewButtonSize, game.ToggleLevelEditMode);
        AddScalePressAnimation(levelEditButton);
        levelEditButtonText = CreateText(developmentParent, "Level Edit Mode Text", "EDIT MODE",
            levelEditPosition, layout.levelPreviewButtonSize - new Vector2(10f, 8f), 19f, TextAlignmentOptions.Center);

        Vector2 checkPosition = new Vector2(-120f, -1005f);
        Vector2 checkSize = new Vector2(340f, 76f);
        solvabilityCheckButton = CreateButton(developmentParent, "Check Solvability", LoadSprite("settings_resume_button"),
            checkPosition, checkSize, game.CheckEditedLevelSolvability);
        AddScalePressAnimation(solvabilityCheckButton);
        solvabilityCheckButtonText = CreateText(developmentParent, "Check Solvability Text", "CHECK SOLVABILITY",
            checkPosition, checkSize - new Vector2(18f, 8f), 27f, TextAlignmentOptions.Center);
        solvabilityCheckButtonText.fontStyle = FontStyles.Bold;

        Vector2 savePosition = new Vector2(240f, -1005f);
        Vector2 saveSize = new Vector2(250f, 76f);
        levelSaveButton = CreateButton(developmentParent, "Save Edited Level", LoadSprite("settings_resume_button"),
            savePosition, saveSize, game.SaveEditedLevelDirections);
        AddScalePressAnimation(levelSaveButton);
        levelSaveButtonText = CreateText(developmentParent, "Save Edited Level Text", "SAVE LEVEL",
            savePosition, saveSize - new Vector2(16f, 8f), 26f, TextAlignmentOptions.Center);
        levelSaveButtonText.fontStyle = FontStyles.Bold;

        Vector2 swapPosition = new Vector2(-255f, -840f);
        Vector2 swapSize = new Vector2(160f, 64f);
        levelSwapButton = CreateButton(developmentParent, "Swap Edited Cars", LoadSprite("settings_resume_button"),
            swapPosition, swapSize, game.ToggleLevelEditSwapMode);
        AddScalePressAnimation(levelSwapButton);
        levelSwapButtonText = CreateText(developmentParent, "Swap Edited Cars Text", "SWAP CARS",
            swapPosition, swapSize - new Vector2(12f, 8f), 18f, TextAlignmentOptions.Center);
        levelSwapButtonText.fontStyle = FontStyles.Bold;

        Vector2 carPosition = new Vector2(-85f, -840f);
        Vector2 carSize = new Vector2(160f, 64f);
        levelCarButton = CreateButton(developmentParent, "Add Delete Edited Cars", LoadSprite("settings_resume_button"),
            carPosition, carSize, game.ToggleLevelEditCarMode);
        AddScalePressAnimation(levelCarButton);
        levelCarButtonText = CreateText(developmentParent, "Add Delete Edited Cars Text", "CARS",
            carPosition, carSize - new Vector2(12f, 8f), 18f, TextAlignmentOptions.Center);
        levelCarButtonText.fontStyle = FontStyles.Bold;

        string[] carColorLabels = { "RED", "GREEN", "BLUE", "PURPLE", "YELLOW", "PINK", "POLICE" };
        int[] carColorValues = { 0, 1, 2, 3, 4, 6, 5 };
        for (int index = 0; index < levelCarColorButtons.Length; index++)
        {
            int colorValue = carColorValues[index];
            Vector2 palettePosition = new Vector2(-282f + index * 94f, -765f);
            levelCarColorButtons[index] = CreateButton(
                developmentParent,
                "Edited Car Color " + carColorLabels[index],
                LoadSprite("settings_resume_button"),
                palettePosition,
                new Vector2(86f, 52f),
                () => game.SelectLevelEditCarColor(colorValue));
            AddScalePressAnimation(levelCarColorButtons[index]);
            levelCarColorButtonTexts[index] = CreateText(
                developmentParent,
                "Edited Car Color " + carColorLabels[index] + " Text",
                carColorLabels[index],
                palettePosition,
                new Vector2(80f, 44f),
                index == 6 ? 13f : 15f,
                TextAlignmentOptions.Center);
            levelCarColorButtonTexts[index].fontStyle = FontStyles.Bold;
        }
        for (int index = 0; index < levelGarageCarSlotButtons.Length; index++)
        {
            int queueIndex = index;
            Vector2 slotPosition = new Vector2(-286f + index * 52f, -702f);
            levelGarageCarSlotButtons[index] = CreateButton(
                developmentParent,
                "Garage Queue Car " + (index + 1),
                LoadSprite("settings_resume_button"),
                slotPosition,
                new Vector2(48f, 44f),
                () => game.SelectLevelEditGarageCarSlot(queueIndex));
            AddScalePressAnimation(levelGarageCarSlotButtons[index]);
            levelGarageCarSlotButtonTexts[index] = CreateText(
                developmentParent,
                "Garage Queue Car " + (index + 1) + " Text",
                (index + 1).ToString(),
                slotPosition,
                new Vector2(44f, 38f),
                12f,
                TextAlignmentOptions.Center);
            levelGarageCarSlotButtonTexts[index].fontStyle = FontStyles.Bold;
        }
        levelGarageCarAddButton = CreateButton(
            developmentParent,
            "Add Garage Queue Car",
            LoadSprite("settings_resume_button"),
            new Vector2(-90f, -648f),
            new Vector2(160f, 44f),
            game.AddLevelEditGarageCar);
        AddScalePressAnimation(levelGarageCarAddButton);
        levelGarageCarAddButtonText = CreateText(
            developmentParent,
            "Add Garage Queue Car Text",
            "ADD CAR",
            new Vector2(-90f, -648f),
            new Vector2(150f, 38f),
            15f,
            TextAlignmentOptions.Center);
        levelGarageCarAddButtonText.fontStyle = FontStyles.Bold;

        levelGarageCarDeleteButton = CreateButton(
            developmentParent,
            "Delete Garage Queue Car",
            LoadSprite("settings_resume_button"),
            new Vector2(90f, -648f),
            new Vector2(160f, 44f),
            game.DeleteLevelEditGarageCar);
        AddScalePressAnimation(levelGarageCarDeleteButton);
        levelGarageCarDeleteButtonText = CreateText(
            developmentParent,
            "Delete Garage Queue Car Text",
            "DELETE CAR",
            new Vector2(90f, -648f),
            new Vector2(150f, 38f),
            15f,
            TextAlignmentOptions.Center);
        levelGarageCarDeleteButtonText.fontStyle = FontStyles.Bold;

        Vector2 garagePosition = new Vector2(85f, -840f);
        Vector2 garageSize = new Vector2(160f, 64f);
        levelGarageButton = CreateButton(developmentParent, "Move Edited Garage", LoadSprite("settings_resume_button"),
            garagePosition, garageSize, game.ToggleLevelEditGarageMode);
        AddScalePressAnimation(levelGarageButton);
        levelGarageButtonText = CreateText(developmentParent, "Move Edited Garage Text", "GARAGES",
            garagePosition, garageSize - new Vector2(12f, 8f), 18f, TextAlignmentOptions.Center);
        levelGarageButtonText.fontStyle = FontStyles.Bold;

        Vector2 boxPosition = new Vector2(255f, -840f);
        Vector2 boxSize = new Vector2(160f, 64f);
        levelBoxButton = CreateButton(developmentParent, "Move Edited Box", LoadSprite("settings_resume_button"),
            boxPosition, boxSize, game.ToggleLevelEditBoxMode);
        AddScalePressAnimation(levelBoxButton);
        levelBoxButtonText = CreateText(developmentParent, "Move Edited Box Text", "BOXES",
            boxPosition, boxSize - new Vector2(12f, 8f), 18f, TextAlignmentOptions.Center);
        levelBoxButtonText.fontStyle = FontStyles.Bold;

        solvabilityResultText = CreateText(developmentParent, "Solvability Result", string.Empty,
            new Vector2(0f, -920f), new Vector2(620f, 70f), 34f, TextAlignmentOptions.Center);
        solvabilityResultText.fontStyle = FontStyles.Bold;
        solvabilityResultText.outlineColor = new Color32(5, 28, 72, 255);
        solvabilityResultText.outlineWidth = 0.16f;

        Vector2 developmentTogglePosition = layout.previousLevelPosition + new Vector2(0f, 200f);
        Vector2 developmentToggleSize = new Vector2(150f, 54f);
        developmentControlsToggleButton = CreateButton(
            parent,
            "Development Controls Toggle",
            LoadSprite("settings_resume_button"),
            developmentTogglePosition,
            developmentToggleSize,
            ToggleDevelopmentControls);
        AddScalePressAnimation(developmentControlsToggleButton);
        developmentControlsToggleText = CreateText(
            developmentControlsToggleButton.transform,
            "Development Controls Toggle Text",
            "TOOLS",
            Vector2.zero,
            developmentToggleSize - new Vector2(10f, 6f),
            20f,
            TextAlignmentOptions.Center);
        developmentControlsToggleText.fontStyle = FontStyles.Bold;

        RefreshLevelEditControls();
        RefreshDevelopmentControlsVisibility();
    }

    private void ToggleDevelopmentControls()
    {
        developmentControlsVisible = !developmentControlsVisible;
        RefreshDevelopmentControlsVisibility();
    }

    private void RefreshDevelopmentControlsVisibility()
    {
        bool available = layout != null && layout.showLevelPreviewButtons;
        if (developmentControlsRoot != null)
            developmentControlsRoot.SetActive(available && developmentControlsVisible);
        if (developmentControlsToggleButton != null)
            developmentControlsToggleButton.gameObject.SetActive(available);
        if (developmentControlsToggleText != null)
            developmentControlsToggleText.text = developmentControlsVisible ? "HIDE" : "TOOLS";
    }

    private void RefreshDirectionArrowToggle()
    {
        if (directionArrowButton == null || directionArrowButtonText == null || game == null) return;
        bool enabled = game.DirectionArrowsEnabled;
        directionArrowButtonText.text = enabled ? "ARROWS ON" : "ARROWS OFF";
        Image image = directionArrowButton.GetComponent<Image>();
        if (image != null)
            image.color = enabled ? Color.white : new Color(0.55f, 0.58f, 0.62f, 1f);
    }

    private void RefreshLevelEditControls()
    {
        if (game == null) return;
        bool previewVisible = layout.showLevelPreviewButtons;
        bool editing = game.LevelEditMode;

        if (levelEditButton != null)
        {
            levelEditButton.gameObject.SetActive(previewVisible);
            Image image = levelEditButton.GetComponent<Image>();
            if (image != null)
                image.color = editing ? new Color(0.45f, 1f, 0.45f, 1f) : Color.white;
        }
        if (levelEditButtonText != null)
        {
            levelEditButtonText.gameObject.SetActive(previewVisible);
            levelEditButtonText.text = editing ? "EDIT ON" : "EDIT MODE";
        }
        if (solvabilityCheckButton != null)
            solvabilityCheckButton.gameObject.SetActive(previewVisible && editing);
        if (solvabilityCheckButtonText != null)
            solvabilityCheckButtonText.gameObject.SetActive(previewVisible && editing);
        if (levelSaveButton != null)
            levelSaveButton.gameObject.SetActive(previewVisible && editing);
        if (levelSaveButtonText != null)
            levelSaveButtonText.gameObject.SetActive(previewVisible && editing);
        if (levelSwapButton != null)
        {
            levelSwapButton.gameObject.SetActive(previewVisible && editing);
            Image image = levelSwapButton.GetComponent<Image>();
            if (image != null)
                image.color = game.LevelEditSwapMode ? new Color(0.45f, 1f, 0.45f, 1f) : Color.white;
        }
        if (levelSwapButtonText != null)
        {
            levelSwapButtonText.gameObject.SetActive(previewVisible && editing);
            levelSwapButtonText.text = game.LevelEditSwapMode ? "SWAP ON" : "SWAP CARS";
        }
        if (levelCarButton != null)
        {
            levelCarButton.gameObject.SetActive(previewVisible && editing);
            Image image = levelCarButton.GetComponent<Image>();
            if (image != null)
                image.color = game.LevelEditCarMode ? new Color(0.45f, 1f, 0.45f, 1f) : Color.white;
        }
        if (levelCarButtonText != null)
        {
            levelCarButtonText.gameObject.SetActive(previewVisible && editing);
            levelCarButtonText.text = game.LevelEditCarMode ? "CARS ON" : "CARS";
        }
        int[] paletteColors = { 0, 1, 2, 3, 4, 6, 5 };
        for (int index = 0; index < levelCarColorButtons.Length; index++)
        {
            bool visible = previewVisible && editing && game.LevelEditCarMode;
            Button button = levelCarColorButtons[index];
            if (button != null)
            {
                button.gameObject.SetActive(visible);
                Image image = button.GetComponent<Image>();
                if (image != null)
                    image.color = paletteColors[index] == game.LevelEditCarColor
                        ? new Color(0.45f, 1f, 0.45f, 1f)
                        : Color.white;
            }
            if (levelCarColorButtonTexts[index] != null)
                levelCarColorButtonTexts[index].gameObject.SetActive(visible);
        }
        int queueCount = game.LevelEditCarGarageQueueCount;
        for (int index = 0; index < levelGarageCarSlotButtons.Length; index++)
        {
            bool visible = previewVisible && editing && game.LevelEditCarMode
                && game.LevelEditCarGarageSelected && index < queueCount;
            Button button = levelGarageCarSlotButtons[index];
            if (button != null)
            {
                button.gameObject.SetActive(visible);
                Image image = button.GetComponent<Image>();
                if (image != null)
                    image.color = index == game.LevelEditCarGarageSlot
                        ? new Color(0.45f, 1f, 0.45f, 1f)
                        : Color.white;
            }
            TextMeshProUGUI label = levelGarageCarSlotButtonTexts[index];
            if (label != null)
            {
                label.gameObject.SetActive(visible);
                if (visible)
                    label.text = (index + 1) + GetGarageQueueColorShortName(
                        game.GetLevelEditGarageCarColor(index));
            }
        }
        bool garageQueueResizeVisible = previewVisible && editing && game.LevelEditCarMode
            && game.LevelEditCarGarageSelected && game.LevelEditSelectedGarageQueueResizable;
        if (levelGarageCarAddButton != null)
            levelGarageCarAddButton.gameObject.SetActive(garageQueueResizeVisible);
        if (levelGarageCarAddButtonText != null)
            levelGarageCarAddButtonText.gameObject.SetActive(garageQueueResizeVisible);
        if (levelGarageCarDeleteButton != null)
            levelGarageCarDeleteButton.gameObject.SetActive(garageQueueResizeVisible);
        if (levelGarageCarDeleteButtonText != null)
            levelGarageCarDeleteButtonText.gameObject.SetActive(garageQueueResizeVisible);
        if (levelGarageButton != null)
        {
            levelGarageButton.gameObject.SetActive(previewVisible && editing);
            Image image = levelGarageButton.GetComponent<Image>();
            if (image != null)
                image.color = game.LevelEditGarageMode ? new Color(0.45f, 1f, 0.45f, 1f) : Color.white;
        }
        if (levelGarageButtonText != null)
        {
            levelGarageButtonText.gameObject.SetActive(previewVisible && editing);
            levelGarageButtonText.text = game.LevelEditGarageMode ? "GARAGE ON" : "GARAGES";
        }
        if (levelBoxButton != null)
        {
            levelBoxButton.gameObject.SetActive(previewVisible && editing);
            Image image = levelBoxButton.GetComponent<Image>();
            if (image != null)
                image.color = game.LevelEditBoxMode ? new Color(0.45f, 1f, 0.45f, 1f) : Color.white;
        }
        if (levelBoxButtonText != null)
        {
            levelBoxButtonText.gameObject.SetActive(previewVisible && editing);
            levelBoxButtonText.text = game.LevelEditBoxMode ? "BOX ON" : "BOXES";
        }
        if (solvabilityResultText != null)
            solvabilityResultText.gameObject.SetActive(previewVisible && editing);
    }

    public void ClearLevelEditResult()
    {
        if (solvabilityResultText != null)
        {
            solvabilityResultText.text = BuildLevelEditInstruction();
            solvabilityResultText.color = Color.white;
        }
        RefreshLevelEditControls();
    }

    public void ShowLevelEditModeInstruction()
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = BuildLevelEditInstruction();
        solvabilityResultText.color = Color.white;
        RefreshLevelEditControls();
    }

    public void ShowLevelEditSwapSelection()
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = "SELECT SECOND CAR";
        solvabilityResultText.color = new Color(1f, 0.88f, 0.25f, 1f);
        RefreshLevelEditControls();
    }

    public void ShowLevelEditSwapComplete()
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = "CARS SWITCHED";
        solvabilityResultText.color = new Color(0.30f, 1f, 0.38f, 1f);
        RefreshLevelEditControls();
    }

    public void ShowLevelEditInvalidSwap()
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = "CARS DO NOT FIT THERE";
        solvabilityResultText.color = new Color(1f, 0.30f, 0.25f, 1f);
        RefreshLevelEditControls();
    }

    public void ShowLevelEditCarColorSelected(string colorName)
    {
        ShowLevelEditMessage(colorName + " CAR SELECTED", new Color(1f, 0.88f, 0.25f, 1f));
    }

    public void ShowLevelEditCarAdded(string colorName)
    {
        ShowLevelEditMessage(colorName + " CAR ADDED", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditCarDeleted()
    {
        ShowLevelEditMessage("CAR DELETED", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditInvalidCarCell()
    {
        ShowLevelEditMessage("SELECT AN EMPTY BOARD CELL", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageCarSelection()
    {
        ShowLevelEditMessage("SELECT COLOR  •  TAP GARAGE QUEUE NUMBER", new Color(1f, 0.88f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageCarChanged(int queueNumber, string colorName)
    {
        ShowLevelEditMessage(
            "GARAGE CAR " + queueNumber + ": " + colorName,
            new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditGarageCarEditFailed()
    {
        ShowLevelEditMessage("GARAGE CAR COULD NOT BE CHANGED", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageCarAdded(string colorName)
    {
        ShowLevelEditMessage(colorName + " ADDED TO GARAGE", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditGarageCarDeleted()
    {
        ShowLevelEditMessage("GARAGE CAR DELETED", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditGarageQueueLimit()
    {
        ShowLevelEditMessage("GARAGE QUEUE IS FULL", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageNeedsOneCar()
    {
        ShowLevelEditMessage("GARAGE MUST KEEP ONE CAR", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditCreatedGarageQueueFixed()
    {
        ShowLevelEditMessage("CREATED GARAGE KEEPS ITS 2 CARS", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageRequired()
    {
        ShowLevelEditMessage("SELECT A GARAGE FIRST", new Color(1f, 0.76f, 0.22f, 1f));
    }

    public void ShowLevelEditGarageSelection()
    {
        ShowLevelEditMessage("TAP CELL: MOVE  •  AGAIN: DELETE", new Color(1f, 0.88f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageAdded()
    {
        ShowLevelEditMessage("GARAGE ADDED (2 CARS)", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditInvalidGarageAdd()
    {
        ShowLevelEditMessage("NEEDS 2 VERTICAL CARS", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageDeleted()
    {
        ShowLevelEditMessage("GARAGE DELETED", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditInvalidGarageMove()
    {
        ShowLevelEditMessage("GARAGE NEEDS A CLEAN 2-CELL SPACE", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditGarageMoveComplete()
    {
        ShowLevelEditMessage("GARAGE MOVED (QUEUE SAFE)", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditBoxRequired()
    {
        ShowLevelEditMessage("SELECT AN EXISTING BOX FIRST", new Color(1f, 0.76f, 0.22f, 1f));
    }

    public void ShowLevelEditBoxSelection()
    {
        ShowLevelEditMessage("TAP CAR: MOVE  •  AGAIN: DELETE", new Color(1f, 0.88f, 0.25f, 1f));
    }

    public void ShowLevelEditBoxAdded()
    {
        ShowLevelEditMessage("NEW BOX ADDED", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditInvalidBoxAdd()
    {
        ShowLevelEditMessage("BOX NEEDS A SINGLE CAR", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditBoxDeleted()
    {
        ShowLevelEditMessage("BOX DELETED — CAR PRESERVED", new Color(0.30f, 1f, 0.38f, 1f));
    }

    public void ShowLevelEditInvalidBoxMove()
    {
        ShowLevelEditMessage("SELECT AN UNBOXED SINGLE CAR", new Color(1f, 0.30f, 0.25f, 1f));
    }

    public void ShowLevelEditBoxMoveComplete()
    {
        ShowLevelEditMessage("BOX MOVED (CARS SAFE)", new Color(0.30f, 1f, 0.38f, 1f));
    }

    private void ShowLevelEditMessage(string message, Color color)
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = message;
        solvabilityResultText.color = color;
        RefreshLevelEditControls();
    }

    private string BuildLevelEditInstruction()
    {
        if (game == null || !game.LevelEditMode) return string.Empty;
        if (game.LevelEditGarageMode)
            return game.LevelEditHasGarageSelection
                ? "TAP CELL: MOVE  •  AGAIN: DELETE"
                : "GARAGE: MOVE  •  CARS: ADD";
        if (game.LevelEditBoxMode)
            return game.LevelEditHasBoxSelection
                ? "TAP CAR: MOVE  •  AGAIN: DELETE"
                : "BOX: MOVE  •  CAR: ADD";
        if (game.LevelEditCarMode && game.LevelEditCarGarageSelected)
            return "SELECT COLOR  •  TAP GARAGE QUEUE NUMBER";
        if (game.LevelEditCarMode)
            return "EMPTY CELL: ADD " + game.LevelEditCarColorLabel + "  •  CAR: DELETE";
        return game.LevelEditSwapMode ? "SELECT FIRST CAR" : "TAP A CAR TO ROTATE";
    }

    private static string GetGarageQueueColorShortName(int color)
    {
        switch (color)
        {
            case 0: return "R";
            case 1: return "G";
            case 2: return "B";
            case 3: return "U";
            case 4: return "Y";
            case 5: return "C";
            case 6: return "P";
            default: return "?";
        }
    }

    public void ShowLevelEditSolvability(bool solvable)
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = solvable ? "SOLVABLE" : "NOT SOLVABLE";
        solvabilityResultText.color = solvable
            ? new Color(0.30f, 1f, 0.38f, 1f)
            : new Color(1f, 0.30f, 0.25f, 1f);
        RefreshLevelEditControls();
    }

    public void ShowLevelEditSaved()
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = "LEVEL SAVED";
        solvabilityResultText.color = new Color(0.30f, 1f, 0.38f, 1f);
        RefreshLevelEditControls();
    }

    public void ShowLevelEditSaveFailed()
    {
        if (solvabilityResultText == null) return;
        solvabilityResultText.text = "SAVE FAILED";
        solvabilityResultText.color = new Color(1f, 0.30f, 0.25f, 1f);
        RefreshLevelEditControls();
    }

    private void BuildMainMenu(Transform parent)
    {
        menuShopNormalSprite = LoadSprite("menu_shop_normal");
        menuShopTallSprite = LoadSprite("menu_shop_tall");
        menuHomeNormalSprite = LoadSprite("menu_home_normal");
        menuHomeTallSprite = LoadSprite("menu_home_tall");
        menuLockedNormalSprite = LoadSprite("menu_locked_normal");
        menuLockedTallSprite = LoadSprite("menu_locked_tall");

        mainMenuRoot = CreateFullScreenRoot(parent, "StartMenuPanel");
        Image background = mainMenuRoot.AddComponent<Image>();
        background.sprite = LoadSprite("bg");
        background.color = Color.white;

        mainMenuHomePage = CreateFullScreenRoot(mainMenuRoot.transform, "MainMenuHomePage");
        mainMenuPlayNormalSprite = LoadSprite("settings_resume_button");
        mainMenuPlayHardSprite = LoadSprite(MainMenuHardLevelButtonResource);
        mainMenuPlaySuperHardSprite = LoadSprite(MainMenuSuperHardLevelButtonResource);
        Button playButton = CreateButton(mainMenuHomePage.transform, "MainMenuPlayButton", mainMenuPlayNormalSprite, layout.mainMenuPlayPosition, layout.mainMenuPlaySize, StartFromMainMenu);
        mainMenuPlayButtonImage = playButton.GetComponent<Image>();
        playButton.gameObject.AddComponent<SimpleButtonPressAnimation>();
        mainMenuPlayText = CreateText(
            playButton.transform,
            "MainMenuPlayButtonText",
            FormatResumeLevelLabel(game.SavedCampaignBoardNumber),
            Vector2.zero,
            layout.mainMenuPlaySize,
            layout.mainMenuPlayFontSize,
            TextAlignmentOptions.Center);
        mainMenuPlayText.fontWeight = FontWeight.Heavy;
        RefreshMainMenuPlayLabel();

        Button demoButton = CreateButton(mainMenuHomePage.transform, "MainMenuDemoButton", LoadSprite("settings_resume_button"), layout.mainMenuDemoPosition, layout.mainMenuDemoSize, StartDemoFromMainMenu);
        demoButton.gameObject.AddComponent<SimpleButtonPressAnimation>();
        TextMeshProUGUI demoText = CreateText(demoButton.transform, "MainMenuDemoButtonText", layout.mainMenuDemoText, Vector2.zero, layout.mainMenuDemoSize, layout.mainMenuDemoFontSize, TextAlignmentOptions.Center);
        demoText.fontWeight = FontWeight.Heavy;

        mainMenuShopPage = CreateFullScreenRoot(mainMenuRoot.transform, "MainMenuShopPage");
        mainMenuLockedPage = CreateFullScreenRoot(mainMenuRoot.transform, "MainMenuLockedPage");

        GameObject tabsRoot = CreateFullScreenRoot(mainMenuRoot.transform, "MainMenuTabs");
        mainMenuShopTab = CreateMainMenuTab(tabsRoot.transform, "MainMenuShopTab", "Shop", menuShopNormalSprite, layout.mainMenuShopTabPosition, () => SelectMainMenuTab(MainMenuTab.Shop));
        mainMenuHomeTab = CreateMainMenuTab(tabsRoot.transform, "MainMenuHomeTab", "Home", menuHomeNormalSprite, layout.mainMenuHomeTabPosition, () => SelectMainMenuTab(MainMenuTab.Home));
        mainMenuLockedTab = CreateMainMenuTab(tabsRoot.transform, "MainMenuLockedTab", "Locked", menuLockedNormalSprite, layout.mainMenuLockedTabPosition, () => SelectMainMenuTab(MainMenuTab.Locked));

        BuildMainMenuHeartHud(mainMenuRoot.transform);
        if (mainMenuHeartHudRoot != null)
            mainMenuHeartHudRoot.SetActive(game.HeartsEnabled);
        SelectMainMenuTab(MainMenuTab.Home);
        mainMenuRoot.SetActive(false);
    }

    private void BuildMainMenuHeartHud(Transform parent)
    {
        mainMenuHeartHudRoot = new GameObject("Main Menu Heart HUD", typeof(RectTransform));
        mainMenuHeartHudRoot.transform.SetParent(parent, false);
        RectTransform rootRect = mainMenuHeartHudRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.anchoredPosition = new Vector2(-46f, -155f);
        rootRect.sizeDelta = new Vector2(480f, 146f);

        CreateRawImage(
            mainMenuHeartHudRoot.transform,
            "Heart HUD Approved Artwork",
            LoadTexture("heart_hud_bar"),
            Vector2.zero,
            rootRect.sizeDelta);

        mainMenuHeartCountText = CreateText(
            mainMenuHeartHudRoot.transform,
            "Heart HUD Count",
            "5",
            new Vector2(-148f, 8f),
            new Vector2(105f, 84f),
            52f,
            TextAlignmentOptions.Center);
        StyleHeartHudText(mainMenuHeartCountText);

        mainMenuHeartTimerText = CreateText(
            mainMenuHeartHudRoot.transform,
            "Heart HUD Timer",
            "MAX",
            new Vector2(16f, 5f),
            new Vector2(205f, 82f),
            48f,
            TextAlignmentOptions.Center);
        StyleHeartHudText(mainMenuHeartTimerText);

        CreateOutcomeButton(
            mainMenuHeartHudRoot.transform,
            "Heart HUD Plus Button",
            LoadTexture("heart_hud_plus"),
            new Vector2(169f, 5f),
            new Vector2(111f, 105f),
            OpenShopFromHeartHud);

        RefreshMainMenuHeartHud();
    }

    private static void StyleHeartHudText(TextMeshProUGUI text)
    {
        if (text == null) return;

        text.fontStyle = FontStyles.Bold;
        text.fontWeight = FontWeight.Heavy;
        text.outlineColor = new Color32(5, 28, 72, 255);
        text.outlineWidth = 0.19f;
    }

    private void BuildMainMenuCanvas()
    {
        GameObject canvasObject = new GameObject("3D Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 2400f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        BuildMainMenu(canvasObject.transform);
        BuildStartupLoadingScreen(canvasObject.transform);
    }

    private void BuildStartupLoadingScreen(Transform parent)
    {
        startupLoadingRoot = CreateFullScreenRoot(parent, "Startup Loading Screen");
        startupLoadingCanvasGroup = startupLoadingRoot.AddComponent<CanvasGroup>();

        Image background = startupLoadingRoot.AddComponent<Image>();
        background.sprite = LoadSprite("bg");
        background.color = Color.white;
        background.raycastTarget = true;

        Image shade = CreateFullScreenDim(
            startupLoadingRoot.transform,
            "Loading Background Shade",
            new Color(0.01f, 0.12f, 0.30f, 0.12f));
        shade.raycastTarget = true;

        RawImage logo = CreateRawImage(
            startupLoadingRoot.transform,
            "Park Dash Loading Logo",
            LoadTexture("CarPrototype/Victory/ParkDashLogo"),
            new Vector2(0f, 175f),
            new Vector2(760f, 333f));
        startupLoadingLogoRect = logo.rectTransform;

        startupLoadingText = CreateText(
            startupLoadingRoot.transform,
            "Startup Loading Text",
            "LOADING",
            new Vector2(0f, -230f),
            new Vector2(760f, 110f),
            54f,
            TextAlignmentOptions.Center);
        startupLoadingText.fontWeight = FontWeight.Heavy;

        startupLoadingRoot.SetActive(false);
    }

    private void BuildOutcomeCanvas()
    {
        GameObject canvasObject = new GameObject("3D Result Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1125f, 2436f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        BuildVictoryCelebration(canvasObject.transform);
        BuildDefeat(canvasObject.transform);
        BuildVictory(canvasObject.transform);
        BuildTrafficJam(canvasObject.transform);
    }

    private Image CreateMainMenuTab(Transform parent, string name, string label, Sprite sprite, Vector2 position, UnityEngine.Events.UnityAction click)
    {
        Button button = CreateButton(parent, name, sprite, position, layout.mainMenuTabSize, click);
        button.transition = Selectable.Transition.None;
        MainMenuTabScrubTarget scrubTarget = button.gameObject.AddComponent<MainMenuTabScrubTarget>();
        scrubTarget.Initialize(UpdateMainMenuTabFromPointer);
        Image image = button.GetComponent<Image>();
        image.preserveAspect = true;

        TextMeshProUGUI text = CreateText(button.transform, name + "Label", label, layout.mainMenuSelectedLabelOffset, layout.mainMenuSelectedLabelSize, layout.mainMenuSelectedLabelFontSize, TextAlignmentOptions.Center);
        text.fontWeight = FontWeight.Heavy;
        text.gameObject.SetActive(false);
        return image;
    }

    private void UpdateMainMenuTabFromPointer(PointerEventData eventData)
    {
        if (eventData == null || mainMenuShopTab == null) return;

        RectTransform tabsRect = mainMenuShopTab.rectTransform.parent as RectTransform;
        if (tabsRect == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tabsRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition))
            return;

        TrySelectMainMenuTabAtLocalPosition(localPosition);
    }

    private void TrySelectMainMenuTabAtLocalPosition(Vector2 localPosition)
    {
        float halfNormalHeight = layout.mainMenuTabSize.y * 0.5f;
        float halfSelectedHeight = layout.mainMenuTallTabSize.y * 0.5f;
        float lowestTabCenter = Mathf.Min(
            layout.mainMenuShopTabPosition.y,
            layout.mainMenuHomeTabPosition.y,
            layout.mainMenuLockedTabPosition.y);
        float highestTabCenter = Mathf.Max(
            layout.mainMenuShopTabPosition.y,
            layout.mainMenuHomeTabPosition.y,
            layout.mainMenuLockedTabPosition.y);
        const float verticalTolerance = 60f;
        float minimumY = lowestTabCenter - halfNormalHeight - verticalTolerance;
        float maximumY = highestTabCenter + layout.mainMenuSelectedTabOffset.y + halfSelectedHeight + verticalTolerance;
        if (localPosition.y < minimumY || localPosition.y > maximumY)
            return;

        float shopDistance = Mathf.Abs(localPosition.x - layout.mainMenuShopTabPosition.x);
        float homeDistance = Mathf.Abs(localPosition.x - layout.mainMenuHomeTabPosition.x);
        float lockedDistance = Mathf.Abs(localPosition.x - layout.mainMenuLockedTabPosition.x);

        MainMenuTab nearestTab = shopDistance <= homeDistance && shopDistance <= lockedDistance
            ? MainMenuTab.Shop
            : homeDistance <= lockedDistance
                ? MainMenuTab.Home
                : MainMenuTab.Locked;
        if (nearestTab != selectedMainMenuTab)
            SelectMainMenuTab(nearestTab);
    }

    private void SelectMainMenuTab(MainMenuTab tab)
    {
        selectedMainMenuTab = tab;
        bool shopSelected = tab == MainMenuTab.Shop;
        bool homeSelected = tab == MainMenuTab.Home;
        bool lockedSelected = tab == MainMenuTab.Locked;

        mainMenuShopPage.SetActive(shopSelected);
        mainMenuHomePage.SetActive(homeSelected);
        mainMenuLockedPage.SetActive(lockedSelected);

        ApplyMainMenuTabState(mainMenuShopTab, shopSelected, menuShopNormalSprite, menuShopTallSprite, layout.mainMenuShopTabPosition);
        ApplyMainMenuTabState(mainMenuHomeTab, homeSelected, menuHomeNormalSprite, menuHomeTallSprite, layout.mainMenuHomeTabPosition);
        ApplyMainMenuTabState(mainMenuLockedTab, lockedSelected, menuLockedNormalSprite, menuLockedTallSprite, layout.mainMenuLockedTabPosition);
    }

    private void OpenShopFromHeartHud()
    {
        SelectMainMenuTab(MainMenuTab.Shop);
    }

    private void RefreshMainMenuHeartHud()
    {
        int availableHearts = CarPrototypeHeartBank.AvailableHearts;
        if (mainMenuHeartCountText != null)
            mainMenuHeartCountText.text = availableHearts.ToString();

        if (mainMenuHeartTimerText == null) return;
        if (availableHearts >= CarPrototypeHeartBank.MaximumHearts)
        {
            mainMenuHeartTimerText.text = "MAX";
            return;
        }

        int remainingSeconds = Mathf.Clamp(
            Mathf.CeilToInt((float)CarPrototypeHeartBank.SecondsUntilNextHeart),
            0,
            CarPrototypeHeartBank.RecoveryMinutes * 60);
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        mainMenuHeartTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void StartMainMenuHeartHudUpdates()
    {
        StopMainMenuHeartHudUpdates();
        if (Application.isPlaying)
            mainMenuHeartHudCoroutine = StartCoroutine(UpdateMainMenuHeartHud());
    }

    private void StopMainMenuHeartHudUpdates()
    {
        if (mainMenuHeartHudCoroutine == null) return;

        StopCoroutine(mainMenuHeartHudCoroutine);
        mainMenuHeartHudCoroutine = null;
    }

    private IEnumerator UpdateMainMenuHeartHud()
    {
        var refreshDelay = new WaitForSecondsRealtime(0.25f);
        while (mainMenuRoot != null && mainMenuRoot.activeSelf)
        {
            RefreshMainMenuHeartHud();
            yield return refreshDelay;
        }

        mainMenuHeartHudCoroutine = null;
    }

    private void ApplyMainMenuTabState(Image tabImage, bool selected, Sprite normalSprite, Sprite tallSprite, Vector2 basePosition)
    {
        if (tabImage == null) return;

        tabImage.sprite = selected ? tallSprite : normalSprite;
        RectTransform rect = tabImage.rectTransform;
        rect.anchoredPosition = basePosition + (selected ? layout.mainMenuSelectedTabOffset : Vector2.zero);
        rect.sizeDelta = selected ? layout.mainMenuTallTabSize : layout.mainMenuTabSize;
        if (selected) tabImage.transform.SetAsLastSibling();

        Transform label = FindNamedTransform(tabImage.transform, tabImage.name + "Label");
        if (label != null) label.gameObject.SetActive(selected);
    }

    public void ShowMainMenu()
    {
        if (!IsHudReady()) return;

        CancelStartupLoading();
        CancelDefeatReveal();
        RefreshMainMenuPlayLabel();
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        HideOutcomeRoots();
        SelectMainMenuTab(MainMenuTab.Home);
        mainMenuRoot.SetActive(true);
        mainMenuRoot.transform.SetAsLastSibling();
        if (mainMenuHeartHudRoot != null)
            mainMenuHeartHudRoot.SetActive(game.HeartsEnabled);
        if (game.HeartsEnabled)
        {
            RefreshMainMenuHeartHud();
            StartMainMenuHeartHudUpdates();
        }
        game.TogglePause(true);
    }

    public void BeginStartupFlow()
    {
        if (!IsHudReady() || startupLoadingRoot == null) return;

        CancelStartupLoading();
        StopMainMenuHeartHudUpdates();
        mainMenuRoot.SetActive(false);
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        HideOutcomeRoots();

        startupLoadingCanvasGroup.alpha = 1f;
        startupLoadingLogoRect.localScale = Vector3.one;
        startupLoadingText.text = $"LOADING LEVEL {game.SavedCampaignBoardNumber}";
        startupLoadingRoot.SetActive(true);
        startupLoadingRoot.transform.SetAsLastSibling();
        game.TogglePause(true);
        startupLoadingCoroutine = StartCoroutine(RunStartupLoading());
    }

    private IEnumerator RunStartupLoading()
    {
        float elapsed = 0f;
        while (elapsed < StartupLoadingDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / StartupLoadingDuration);
            float pulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.035f;
            startupLoadingLogoRect.localScale = Vector3.one * pulse;

            int dotCount = Mathf.Clamp(Mathf.FloorToInt(elapsed / 0.24f) % 4, 0, 3);
            startupLoadingText.text = $"LOADING LEVEL {game.SavedCampaignBoardNumber}{new string('.', dotCount)}";
            yield return null;
        }

        game.StartRegularGame();
        yield return null;

        elapsed = 0f;
        while (elapsed < StartupLoadingFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            startupLoadingCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / StartupLoadingFadeDuration);
            yield return null;
        }

        startupLoadingRoot.SetActive(false);
        startupLoadingCanvasGroup.alpha = 1f;
        startupLoadingLogoRect.localScale = Vector3.one;
        startupLoadingCoroutine = null;
        game.TogglePause(false);
    }

    private void CancelStartupLoading()
    {
        if (startupLoadingCoroutine != null)
        {
            StopCoroutine(startupLoadingCoroutine);
            startupLoadingCoroutine = null;
        }

        if (startupLoadingRoot != null)
            startupLoadingRoot.SetActive(false);
        if (startupLoadingCanvasGroup != null)
            startupLoadingCanvasGroup.alpha = 1f;
        if (startupLoadingLogoRect != null)
            startupLoadingLogoRect.localScale = Vector3.one;
    }

    private void RefreshMainMenuPlayLabel()
    {
        if (mainMenuPlayText == null || game == null) return;

        string difficultyLabel = game.SavedCampaignDifficultyLabel;
        bool isHardLevel = difficultyLabel == "HARD";
        bool isSuperHardLevel = difficultyLabel == "SUPER HARD";
        bool usesDifficultyArtwork = isHardLevel || isSuperHardLevel;
        if (mainMenuPlayButtonImage == null && mainMenuRoot != null)
            mainMenuPlayButtonImage = FindNamedComponent<Image>(mainMenuRoot.transform, "MainMenuPlayButton");

        if (mainMenuPlayButtonImage != null)
        {
            Sprite targetSprite = isSuperHardLevel
                ? mainMenuPlaySuperHardSprite
                : isHardLevel
                    ? mainMenuPlayHardSprite
                    : mainMenuPlayNormalSprite;
            if (targetSprite != null) mainMenuPlayButtonImage.sprite = targetSprite;
            mainMenuPlayButtonImage.color = Color.white;
            mainMenuPlayButtonImage.preserveAspect = usesDifficultyArtwork;
            mainMenuPlayButtonImage.rectTransform.sizeDelta = usesDifficultyArtwork
                ? MainMenuDifficultyButtonSize
                : layout.mainMenuPlaySize;
        }

        mainMenuPlayText.text = FormatResumeLevelLabel(game.SavedCampaignBoardNumber);
        RectTransform textRect = mainMenuPlayText.rectTransform;
        textRect.anchoredPosition = usesDifficultyArtwork
            ? new Vector2(0f, MainMenuDifficultyLevelTextYOffset)
            : Vector2.zero;
        textRect.sizeDelta = usesDifficultyArtwork
            ? MainMenuDifficultyTextSize
            : layout.mainMenuPlaySize;
    }

    private static string FormatResumeLevelLabel(int savedBoardNumber)
    {
        return $"LEVEL {Mathf.Max(1, savedBoardNumber)}";
    }

    private void StartFromMainMenu()
    {
        if (mainMenuRoot == null) return;
        StopMainMenuHeartHudUpdates();
        game.StartRegularGame();
        mainMenuRoot.SetActive(false);
        game.TogglePause(false);
    }

    private void StartDemoFromMainMenu()
    {
        if (mainMenuRoot == null) return;
        StopMainMenuHeartHudUpdates();
        game.StartDemo();
        mainMenuRoot.SetActive(false);
        game.TogglePause(false);
    }

    public void SetDemoAutoPlayState(bool playing)
    {
        if (demoAutoPlayText != null) demoAutoPlayText.text = playing ? "STOP" : "AUTO PLAY";
    }

    public void ContinueDemoAutoPlay()
    {
        HideOutcomeRoots();
        game.LoadNextLevel();
        // Loading a board stops its active coroutines, including the current
        // demo playback. Start a fresh playback coroutine for the next Demo
        // board so one Auto Play tap runs the complete four-level showcase.
        game.ResumeDemoAutoPlayAfterLevelChange();
    }

    private void BuildSettings(Transform parent)
    {
        settingsRoot = CreateFullScreenRoot(parent, "Settings Overlay");
        CreateFullScreenDim(settingsRoot.transform, "Dim", new Color(0f, 0.03f, 0.1f, 0.75f));
        CreateRawImage(
            settingsRoot.transform,
            "Settings Tray",
            LoadTexture("SettingsUi/settings_page"),
            layout.settingsPanelPosition,
            layout.settingsPanelSize);
        CreateSettingsArtworkToggle(settingsRoot.transform, "Haptics Toggle", layout.hapticsPosition, () =>
        {
            hapticsOn = !hapticsOn;
            CarPrototypeFeedback.HapticsEnabled = hapticsOn;
            if (hapticsOn) CarPrototypeFeedback.ButtonTap();
        }, () => hapticsOn);
        CreateSettingsArtworkToggle(settingsRoot.transform, "Sounds Toggle", layout.soundsPosition, () =>
        {
            soundOn = !soundOn;
            CarPrototypeFeedback.SoundEnabled = soundOn;
            if (soundOn) CarPrototypeFeedback.ButtonTap();
        }, () => soundOn);
        CreateSettingsArtworkToggle(settingsRoot.transform, "Music Toggle", layout.musicPosition, () =>
        {
            musicOn = !musicOn;
            CarPrototypeFeedback.MusicEnabled = musicOn;
        }, () => musicOn);
        CreateSettingsArtworkButton(settingsRoot.transform, "Resume", layout.resumePosition,
            new Vector2(417f, 234f), CloseSettings);
        CreateSettingsArtworkButton(settingsRoot.transform, "Quit", layout.quitPosition,
            new Vector2(417f, 234f), LeaveCurrentBoard, true, true, true);
        CreateInvisibleSettingsButton(
            settingsRoot.transform,
            "More",
            layout.morePosition,
            ScaleSettingsArtworkSize(new Vector2(260f, 100f)),
            OpenMorePage);
        CreateSettingsArtworkButton(settingsRoot.transform, "Close", layout.settingsClosePosition,
            new Vector2(125f, 125f), CloseSettings, true, true, true);
        settingsRoot.SetActive(false);
    }

    private void BuildMorePage(Transform parent)
    {
        moreRoot = CreateFullScreenRoot(parent, "Settings More Overlay");
        CreateFullScreenDim(moreRoot.transform, "Dim", new Color(0f, 0.03f, 0.1f, 0.75f));
        CreateRawImage(
            moreRoot.transform,
            "More Tray",
            LoadTexture("SettingsUi/more_page"),
            layout.morePanelPosition,
            layout.morePanelSize);
        CreateMoreArtworkButton(moreRoot.transform, "Terms", layout.termsPosition,
            new Vector2(548f, 234f), false, null);
        CreateMoreArtworkButton(moreRoot.transform, "Privacy", layout.privacyPosition,
            new Vector2(548f, 234f), false, null);
        CreateMoreArtworkButton(moreRoot.transform, "Return", layout.moreBackPosition,
            new Vector2(335f, 188f), true, CloseMorePage);
        CreateMoreArtworkButton(moreRoot.transform, "Close", layout.moreClosePosition,
            new Vector2(125f, 125f), true, CloseSettings);
        moreRoot.SetActive(false);
    }

    private void BuildLeaveConfirmation(Transform parent)
    {
        leaveRoot = CreateFullScreenRoot(parent, "Leave Confirmation Overlay");
        CreateFullScreenDim(leaveRoot.transform, "Dim", new Color(0f, 0.03f, 0.1f, 0.8f));
        Image panel = CreateImage(leaveRoot.transform, "Leave Tray", LoadSprite("settings_tray"), Vector2.zero, layout.leavePanelSize);
        panel.preserveAspect = true;
        CreateText(leaveRoot.transform, "Leave Title", "LEAVE THIS BOARD?", layout.leaveTitlePosition, new Vector2(620f, 86f), 48f, TextAlignmentOptions.Center);
        CreateText(leaveRoot.transform, "Leave Description", "Your progress on this board will be lost.", layout.leaveDescriptionPosition, new Vector2(630f, 64f), 30f, TextAlignmentOptions.Center);
        CreateButton(leaveRoot.transform, "Cancel Leave", LoadSprite("settings_resume_button"), layout.leaveCancelPosition, new Vector2(280f, 90f), CancelLeaveConfirmation);
        CreateText(leaveRoot.transform, "Cancel Leave Text", "CANCEL", layout.leaveCancelPosition, new Vector2(240f, 66f), 30f, TextAlignmentOptions.Center);
        CreateButton(leaveRoot.transform, "Confirm Leave", LoadSprite("settings_quit_button"), layout.leaveConfirmPosition, new Vector2(280f, 90f), LeaveCurrentBoard);
        CreateText(leaveRoot.transform, "Confirm Leave Text", "LEAVE", layout.leaveConfirmPosition, new Vector2(240f, 66f), 30f, TextAlignmentOptions.Center);
        leaveRoot.SetActive(false);
    }

    private void BuildDefeat(Transform parent)
    {
        Texture2D artwork = LoadTexture("outcome_loss_ui");
        defeatRoot = BuildOutcomeRoot(parent, "Defeat Overlay", artwork);
        BuildDefeatHeartBank(defeatRoot.transform);
        lossRetryButton = CreateOutcomeButton(
            defeatRoot.transform,
            "Loss Retry Button",
            LoadTexture("outcome_loss_retry"),
            new Vector2(-124.5f, -202f),
            new Vector2(248f, 250f),
            RetryCurrentLevelFromOutcome);
        CreateOutcomeButton(
            defeatRoot.transform,
            "Loss Home Button",
            LoadTexture("outcome_loss_home"),
            new Vector2(135.5f, -202f),
            new Vector2(248f, 250f),
            ReturnHomeFromOutcome);
        defeatRoot.SetActive(false);
    }

    private void BuildTrafficJam(Transform parent)
    {
        trafficJamRoot = CreateFullScreenRoot(parent, "Traffic Jam Rescue Overlay");
        Image blocker = trafficJamRoot.AddComponent<Image>();
        blocker.sprite = GetRuntimeWhiteSprite();
        blocker.color = new Color(0.005f, 0.012f, 0.025f, 0.90f);

        Image panel = CreateImage(trafficJamRoot.transform, "Traffic Jam Panel", null,
            new Vector2(0f, 15f), new Vector2(990f, 1800f));
        panel.color = new Color(0.055f, 0.10f, 0.18f, 0.97f);

        TextMeshProUGUI title = CreateText(trafficJamRoot.transform, "Traffic Jam Title", "TRAFFIC JAM!",
            new Vector2(0f, 790f), new Vector2(900f, 180f), 86f, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, 0.96f, 0.88f);
        title.outlineColor = new Color32(190, 28, 24, 255);
        title.outlineWidth = 0.28f;

        trafficJamSubtitleText = CreateText(trafficJamRoot.transform, "Traffic Jam Subtitle",
            "The tray is blocked. The tow truck will return the fewest cars needed.",
            new Vector2(0f, 625f), new Vector2(820f, 130f), 38f, TextAlignmentOptions.Center);
        trafficJamSubtitleText.textWrappingMode = TextWrappingModes.Normal;

        BuildTrafficJamCarPreview(trafficJamRoot.transform);

        trafficJamReturnNoteText = CreateText(trafficJamRoot.transform, "Tow Return Note",
            "It tests 1 to 4 cars and stops at the smallest safe rescue.",
            new Vector2(0f, -385f), new Vector2(810f, 100f), 34f, TextAlignmentOptions.Center);
        trafficJamReturnNoteText.color = new Color(0.82f, 0.91f, 1f);

        trafficJamActionButton = CreateButton(trafficJamRoot.transform, "Call Tow Truck",
            LoadSprite("settings_resume_button"), new Vector2(0f, -610f), new Vector2(790f, 175f), BeginTowTruckFromPopup);
        AddScalePressAnimation(trafficJamActionButton);
        trafficJamActionText = CreateText(trafficJamRoot.transform, "Call Tow Truck Text", "RESTORE PUZZLE",
            new Vector2(0f, -610f), new Vector2(720f, 120f), 52f, TextAlignmentOptions.Center);
        trafficJamActionText.fontStyle = FontStyles.Bold;
        trafficJamActionText.outlineColor = new Color32(18, 72, 15, 255);
        trafficJamActionText.outlineWidth = 0.18f;

        Button retryButton = CreateButton(trafficJamRoot.transform, "Traffic Jam Retry",
            LoadSprite("settings_quit_button"), new Vector2(420f, 990f), new Vector2(130f, 130f), RetryFromTrafficJam);
        AddScalePressAnimation(retryButton);
        TextMeshProUGUI retryText = CreateText(trafficJamRoot.transform, "Traffic Jam Retry Text", "×",
            new Vector2(420f, 995f), new Vector2(100f, 100f), 78f, TextAlignmentOptions.Center);
        retryText.fontStyle = FontStyles.Bold;

        trafficJamRoot.SetActive(false);
    }

    private void BuildTrafficJamCarPreview(Transform parent)
    {
        const int showcaseLayer = 30;
        trafficJamRenderTexture = new RenderTexture(768, 520, 16, RenderTextureFormat.ARGB32)
        {
            name = "Traffic Jam 3D Cars",
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear
        };
        trafficJamRenderTexture.Create();

        RawImage preview = CreateRawImage(parent, "Traffic Jam 3D Car Preview", trafficJamRenderTexture,
            new Vector2(0f, 95f), new Vector2(930f, 650f));
        preview.color = Color.white;

        trafficJamShowcaseRoot = new GameObject("Traffic Jam 3D Showcase");
        trafficJamShowcaseRoot.transform.SetParent(transform, true);

        Vector3 center = new Vector3(0f, -80f, 0f);
        GameObject cameraObject = new GameObject("Traffic Jam Showcase Camera", typeof(Camera));
        cameraObject.transform.SetParent(trafficJamShowcaseRoot.transform, true);
        cameraObject.transform.position = center + new Vector3(0f, 7.6f, -6.8f);
        cameraObject.transform.rotation = Quaternion.LookRotation(center - cameraObject.transform.position, Vector3.up);
        Camera showcaseCamera = cameraObject.GetComponent<Camera>();
        showcaseCamera.orthographic = true;
        showcaseCamera.orthographicSize = 3.25f;
        showcaseCamera.clearFlags = CameraClearFlags.SolidColor;
        showcaseCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        showcaseCamera.cullingMask = 1 << showcaseLayer;
        showcaseCamera.targetTexture = trafficJamRenderTexture;
        showcaseCamera.allowHDR = false;

        CreateTrafficJamShowcaseCar("Green Rescue Car", CarPrototype3D.PieceColor.Green,
            center + new Vector3(-1.75f, 0f, 0.35f), -18f, showcaseLayer);
        CreateTrafficJamShowcaseCar("Red Rescue Car", CarPrototype3D.PieceColor.Red,
            center + new Vector3(0f, 0f, -0.15f), 4f, showcaseLayer);
        CreateTrafficJamShowcaseCar("Blue Rescue Car", CarPrototype3D.PieceColor.Blue,
            center + new Vector3(1.75f, 0f, 0.35f), 20f, showcaseLayer);
    }

    private void CreateTrafficJamShowcaseCar(
        string objectName,
        CarPrototype3D.PieceColor color,
        Vector3 position,
        float yawOffset,
        int showcaseLayer)
    {
        GameObject carObject = new GameObject(objectName);
        carObject.transform.SetParent(trafficJamShowcaseRoot.transform, true);
        CarPuzzlePiece piece = carObject.AddComponent<CarPuzzlePiece>();
        piece.Configure(0, 0, color, CarPrototype3D.ExitDirection.Up, position, 0.86f, 1f, 1);
        piece.SetDirectionArrowVisible(false);
        piece.SetEyesCanMove(true);
        carObject.transform.Rotate(0f, yawOffset, 0f, Space.World);
        SetLayerRecursively(carObject.transform, showcaseLayer);

        Collider[] colliders = carObject.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < colliders.Length; index++)
            colliders[index].enabled = false;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int index = 0; index < root.childCount; index++)
            SetLayerRecursively(root.GetChild(index), layer);
    }

    public void ShowTrafficJam()
    {
        if (trafficJamRoot == null) return;
        if (trafficJamSubtitleText != null)
            trafficJamSubtitleText.text = "The tray is blocked. The tow truck will return the fewest cars needed.";
        if (trafficJamReturnNoteText != null)
            trafficJamReturnNoteText.text = "It tests 1 to 4 cars and stops at the smallest safe rescue.";
        if (trafficJamActionText != null) trafficJamActionText.text = "RESTORE PUZZLE";
        if (trafficJamActionButton != null) trafficJamActionButton.interactable = true;
        HidePauseOverlays();
        if (defeatRoot != null) defeatRoot.SetActive(false);
        if (victoryRoot != null) victoryRoot.SetActive(false);
        trafficJamRoot.SetActive(true);
        trafficJamRoot.transform.SetAsLastSibling();
        game.TogglePause(true);
    }

    public void HideTrafficJamForRescue()
    {
        if (trafficJamRoot != null) trafficJamRoot.SetActive(false);
    }

    public void ShowNoSolvableRescue()
    {
        if (trafficJamSubtitleText != null)
            trafficJamSubtitleText.text = "Returning up to four cars cannot safely restore this puzzle.";
        if (trafficJamReturnNoteText != null)
            trafficJamReturnNoteText.text = "Tap × to restart and try a different route.";
        if (trafficJamActionText != null) trafficJamActionText.text = "RESTART REQUIRED";
        if (trafficJamActionButton != null) trafficJamActionButton.interactable = false;
    }

    public void ResetTrafficJamUi()
    {
        if (trafficJamRoot != null) trafficJamRoot.SetActive(false);
    }

    private void BeginTowTruckFromPopup()
    {
        game.BeginTowTruckRescue();
    }

    private void RetryFromTrafficJam()
    {
        ResetTrafficJamUi();
        game.RestartCurrentBoard();
        game.TogglePause(false);
    }

    private void BuildTutorialOverlay(Transform parent)
    {
        GameObject root = CreateFullScreenRoot(parent, "Tutorial Guidance");
        tutorialRoot = root.GetComponent<RectTransform>();
        tutorialRoot.SetAsLastSibling();

        // The dim is intentionally solid. The focused 3D car is rendered into
        // its own transparent layer above it, so no road can remain bright.
        Color dimColor = new Color(0.015f, 0.025f, 0.055f, 0.48f);
        tutorialSpotlightDim = CreateImage(
            tutorialRoot,
            "Tutorial Spotlight Dim",
            null,
            Vector2.zero,
            Vector2.zero);
        tutorialSpotlightDim.color = dimColor;
        tutorialSpotlightDim.raycastTarget = false;
        RectTransform spotlightRect = tutorialSpotlightDim.rectTransform;
        spotlightRect.anchorMin = Vector2.zero;
        spotlightRect.anchorMax = Vector2.one;
        spotlightRect.offsetMin = Vector2.zero;
        spotlightRect.offsetMax = Vector2.zero;

        tutorialFocusedCar = CreateRawImage(
            tutorialRoot,
            "Tutorial Focused Car",
            null,
            Vector2.zero,
            Vector2.zero);
        RectTransform focusedCarRect = tutorialFocusedCar.rectTransform;
        focusedCarRect.anchorMin = Vector2.zero;
        focusedCarRect.anchorMax = Vector2.one;
        focusedCarRect.offsetMin = Vector2.zero;
        focusedCarRect.offsetMax = Vector2.zero;
        tutorialFocusedCar.color = Color.white;
        tutorialFocusedCar.raycastTarget = false;
        tutorialFocusedCar.gameObject.SetActive(false);

        Image messagePanelImage = CreateImage(
            tutorialRoot,
            "Tutorial Message Panel",
            LoadSprite("settings_resume_button"),
            new Vector2(0f, 585f),
            new Vector2(820f, 170f));
        messagePanelImage.color = new Color(0.04f, 0.47f, 0.90f, 0.97f);
        messagePanelImage.raycastTarget = false;
        tutorialMessagePanel = messagePanelImage.rectTransform;

        tutorialTitleText = CreateText(
            tutorialMessagePanel,
            "Tutorial Title",
            "TUTORIAL",
            new Vector2(0f, 42f),
            new Vector2(735f, 54f),
            38f,
            TextAlignmentOptions.Center);
        tutorialTitleText.fontStyle = FontStyles.Bold;
        tutorialTitleText.outlineColor = new Color32(5, 28, 72, 255);
        tutorialTitleText.outlineWidth = 0.16f;

        tutorialMessageText = CreateText(
            tutorialMessagePanel,
            "Tutorial Message",
            string.Empty,
            new Vector2(0f, -28f),
            new Vector2(735f, 88f),
            27f,
            TextAlignmentOptions.Center);
        tutorialMessageText.fontStyle = FontStyles.Bold;
        tutorialMessageText.textWrappingMode = TextWrappingModes.Normal;
        tutorialMessageText.outlineColor = new Color32(5, 28, 72, 255);
        tutorialMessageText.outlineWidth = 0.12f;

        RawImage tutorialHand = CreateRawImage(
            tutorialRoot,
            "Tutorial Hand",
            LoadTexture("Tutorial/tutorial_hand"),
            Vector2.zero,
            new Vector2(300f, 200f));
        tutorialHand.raycastTarget = false;
        tutorialHandRoot = tutorialHand.rectTransform;

        tutorialRoot.gameObject.SetActive(false);
    }

    private void UpdateTutorialOverlay()
    {
        if (tutorialRoot == null || game == null) return;

        bool hasPresentation = game.TryGetTutorialPresentation(
            out Vector2 screenPosition,
            out string title,
            out string message,
            out bool showSpotlight,
            out bool isGameplayHint);
        tutorialRoot.gameObject.SetActive(hasPresentation);
        if (!hasPresentation)
        {
            SetTutorialFocusTarget(null);
            if (tutorialFocusCamera != null) tutorialFocusCamera.enabled = false;
            return;
        }

        tutorialTitleText.text = title;
        tutorialMessageText.text = message;
        tutorialMessagePanel.gameObject.SetActive(!isGameplayHint && !string.IsNullOrEmpty(message));
        tutorialHandRoot.gameObject.SetActive(showSpotlight && !isGameplayHint);

        tutorialSpotlightDim.gameObject.SetActive(showSpotlight);
        tutorialFocusedCar.gameObject.SetActive(showSpotlight);

        if (!showSpotlight)
        {
            SetTutorialFocusTarget(null);
            if (tutorialFocusCamera != null) tutorialFocusCamera.enabled = false;
            return;
        }

        UpdateTutorialFocusedCar(game.TutorialSourceCamera, game.TutorialFocusTransform);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tutorialRoot,
            screenPosition,
            null,
            out Vector2 focus);

        float pulse = (Mathf.Sin(Time.unscaledTime * 5.5f) + 1f) * 0.5f;
        tutorialSpotlightDim.color = isGameplayHint
            ? new Color(0.015f, 0.025f, 0.055f, Mathf.Lerp(0.52f, 0.60f, pulse))
            : new Color(0.015f, 0.025f, 0.055f, 0.48f);
        tutorialFocusedCar.color = isGameplayHint
            ? new Color(1f, 1f, 1f, Mathf.Lerp(0.86f, 1f, pulse))
            : Color.white;
        // Keep the fingertip on the focused car's lower-right area so it never
        // covers the direction arrow centered on the roof.
        if (!isGameplayHint)
        {
            tutorialHandRoot.anchoredPosition = focus + new Vector2(110f, -125f + pulse * 18f);
            tutorialHandRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.04f, pulse);
        }

        if (!isGameplayHint)
        {
            tutorialMessagePanel.SetAsLastSibling();
            tutorialHandRoot.SetAsLastSibling();
        }
    }

    private void UpdateTutorialFocusedCar(Camera sourceCamera, Transform focusTarget)
    {
        if (sourceCamera == null || focusTarget == null)
        {
            SetTutorialFocusTarget(null);
            tutorialFocusedCar.gameObject.SetActive(false);
            if (tutorialFocusCamera != null) tutorialFocusCamera.enabled = false;
            return;
        }

        SetTutorialFocusTarget(focusTarget);
        EnsureTutorialFocusTexture();
        EnsureTutorialFocusCamera(sourceCamera);

        tutorialFocusCamera.CopyFrom(sourceCamera);
        tutorialFocusCamera.transform.SetPositionAndRotation(
            sourceCamera.transform.position,
            sourceCamera.transform.rotation);
        tutorialFocusCamera.cullingMask = 1 << TutorialHighlightLayer;
        tutorialFocusCamera.clearFlags = CameraClearFlags.SolidColor;
        tutorialFocusCamera.backgroundColor = Color.clear;
        tutorialFocusCamera.targetTexture = tutorialFocusTexture;
        tutorialFocusCamera.allowHDR = false;
        tutorialFocusCamera.allowMSAA = false;
        tutorialFocusCamera.depth = sourceCamera.depth + 1f;
        tutorialFocusCamera.enabled = true;
        tutorialFocusedCar.texture = tutorialFocusTexture;
    }

    private void EnsureTutorialFocusCamera(Camera sourceCamera)
    {
        if (tutorialFocusCamera != null) return;

        GameObject cameraObject = new GameObject("Tutorial Focus Camera");
        cameraObject.transform.SetParent(transform, false);
        tutorialFocusCamera = cameraObject.AddComponent<Camera>();

        UniversalAdditionalCameraData cameraData =
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

        UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset;
        if (pipeline != null)
        {
            for (int index = 0; index < pipeline.rendererDataList.Length; index++)
            {
                if (pipeline.rendererDataList[index] != null
                    && pipeline.rendererDataList[index].name == "Renderer3D")
                {
                    cameraData.SetRenderer(index);
                    break;
                }
            }
        }

        tutorialFocusCamera.CopyFrom(sourceCamera);
    }

    private void EnsureTutorialFocusTexture()
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        if (tutorialFocusTexture != null
            && tutorialFocusTexture.width == width
            && tutorialFocusTexture.height == height)
            return;

        ReleaseTutorialFocusTexture();
        tutorialFocusTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Tutorial Focus Texture",
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        tutorialFocusTexture.Create();
        if (tutorialFocusedCar != null)
            tutorialFocusedCar.texture = tutorialFocusTexture;
    }

    private void ReleaseTutorialFocusTexture()
    {
        if (tutorialFocusTexture == null) return;
        if (tutorialFocusCamera != null && tutorialFocusCamera.targetTexture == tutorialFocusTexture)
            tutorialFocusCamera.targetTexture = null;
        tutorialFocusTexture.Release();
        Destroy(tutorialFocusTexture);
        tutorialFocusTexture = null;
    }

    private void SetTutorialFocusTarget(Transform target)
    {
        if (tutorialFocusTarget == target) return;

        RestoreTutorialFocusLayers();
        tutorialFocusTarget = target;
        if (tutorialFocusTarget == null) return;

        Transform[] hierarchy = tutorialFocusTarget.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < hierarchy.Length; index++)
        {
            Transform child = hierarchy[index];
            // The focused car and its movement arrow are one tutorial visual;
            // composite both above the dim so the direction is always legible.
            tutorialFocusOriginalLayers[child] = child.gameObject.layer;
            child.gameObject.layer = TutorialHighlightLayer;
        }
    }

    private void RestoreTutorialFocusLayers()
    {
        foreach (KeyValuePair<Transform, int> entry in tutorialFocusOriginalLayers)
        {
            if (entry.Key != null)
                entry.Key.gameObject.layer = entry.Value;
        }
        tutorialFocusOriginalLayers.Clear();
        tutorialFocusTarget = null;
    }

    private void BuildDefeatHeartBank(Transform parent)
    {
        GameObject hearts = new GameObject("Defeat Heart Bank", typeof(RectTransform));
        hearts.transform.SetParent(parent, false);
        RectTransform heartsRect = hearts.GetComponent<RectTransform>();
        heartsRect.anchorMin = heartsRect.anchorMax = new Vector2(0.5f, 0.5f);
        heartsRect.anchoredPosition = new Vector2(4f, 266f);
        heartsRect.sizeDelta = new Vector2(620f, 104f);

        const float heartSpacing = 104f;
        for (int index = 0; index < defeatHeartImages.Length; index++)
        {
            float x = (index - 2) * heartSpacing;
            defeatHeartImages[index] = CreateRawImage(
                hearts.transform,
                $"Defeat Heart {index + 1}",
                fullHeartTexture,
                new Vector2(x, 0f),
                new Vector2(92f, 92f));
        }

        defeatHeartCountdownText = CreateText(
            parent,
            "Defeat Heart Countdown",
            "10:00",
            new Vector2(4f, 116f),
            new Vector2(650f, 82f),
            42f,
            TextAlignmentOptions.Center);
        defeatHeartCountdownText.fontStyle = FontStyles.Bold;
        defeatHeartCountdownText.outlineColor = new Color32(5, 28, 72, 255);
        defeatHeartCountdownText.outlineWidth = 0.18f;
        hearts.SetActive(game.HeartsEnabled);
        defeatHeartCountdownText.gameObject.SetActive(game.HeartsEnabled);
    }

    private void BuildVictoryCelebration(Transform parent)
    {
        victoryCelebrationRoot = CreateFullScreenRoot(parent, "Park Dash Victory Celebration");
        victoryCelebrationDim = CreateFullScreenDim(
            victoryCelebrationRoot.transform,
            "Victory Celebration Dim",
            new Color(0.005f, 0.012f, 0.035f, 0.79f));

        victoryFireworksBack = CreateVictoryEffectsGraphic(
            victoryCelebrationRoot.transform,
            "Victory Fireworks Behind Logo",
            false);

        GameObject logoObject = new GameObject("Park Dash Logo Assembly", typeof(RectTransform));
        logoObject.transform.SetParent(victoryCelebrationRoot.transform, false);
        victoryLogoRoot = logoObject.GetComponent<RectTransform>();
        victoryLogoRoot.anchorMin = victoryLogoRoot.anchorMax = new Vector2(0.5f, 0.5f);
        victoryLogoRoot.anchoredPosition = new Vector2(0f, 150f);
        // A subtle 10% presentation lift keeps the approved artwork prominent
        // without changing any construction timing or firework choreography.
        victoryLogoRoot.sizeDelta = new Vector2(990f, 433.125f);

        victoryBadgeLayer = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "Badge",
            LoadTexture("CarPrototype/Victory/ParkDashBadge"),
            new Rect(0f, 0f, 1f, 1f),
            0.04f,
            1.13f,
            0f);
        victoryParkLayers[0] = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "P",
            LoadTexture("CarPrototype/Victory/ParkDashParkP"),
            new Rect(0.31184896f, 0.18601190f, 0.11621094f, 0.32514882f),
            0.14f,
            1.26f,
            -9f);
        victoryParkLayers[1] = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "A",
            LoadTexture("CarPrototype/Victory/ParkDashParkA"),
            new Rect(0.38899740f, 0.19717262f, 0.12369792f, 0.28125f),
            0.22f,
            1.54f,
            7f);
        victoryParkLayers[2] = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "R",
            LoadTexture("CarPrototype/Victory/ParkDashParkR"),
            new Rect(0.47753906f, 0.19568452f, 0.12076823f, 0.28125f),
            0.30f,
            1.30f,
            -5f);
        victoryParkLayers[3] = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "K",
            LoadTexture("CarPrototype/Victory/ParkDashParkK"),
            new Rect(0.56315106f, 0.18601190f, 0.11718750f, 0.31994048f),
            0.38f,
            1.28f,
            8f);
        victoryDashLayer = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "DASH",
            LoadTexture("CarPrototype/Victory/ParkDashDash"),
            new Rect(0.23600261f, 0.43898810f, 0.51432294f, 0.42336310f),
            0.56f,
            1.52f,
            2.5f);
        victoryFinalLayer = CreateVictoryLogoLayer(
            victoryLogoRoot,
            "Approved Final Logo",
            LoadTexture("CarPrototype/Victory/ParkDashLogo"),
            new Rect(0f, 0f, 1f, 1f),
            1.06f,
            1f,
            0f);

        victoryFireworksFront = CreateVictoryEffectsGraphic(
            victoryCelebrationRoot.transform,
            "Victory Confetti and Logo Glint",
            true);
        ResetVictoryCelebrationVisuals();
        victoryCelebrationRoot.SetActive(false);
    }

    private ParkDashVictoryEffectsGraphic CreateVictoryEffectsGraphic(
        Transform parent,
        string objectName,
        bool foreground)
    {
        GameObject effectObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(ParkDashVictoryEffectsGraphic));
        effectObject.transform.SetParent(parent, false);
        RectTransform rect = effectObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        ParkDashVictoryEffectsGraphic effect = effectObject.GetComponent<ParkDashVictoryEffectsGraphic>();
        effect.Initialize(foreground);
        return effect;
    }

    private VictoryLogoLayer CreateVictoryLogoLayer(
        RectTransform parent,
        string layerName,
        Texture2D texture,
        Rect normalizedSourceRect,
        float startTime,
        float overshoot,
        float entryRotation)
    {
        GameObject layerObject = new GameObject(layerName + " Layer", typeof(RectTransform));
        layerObject.transform.SetParent(parent, false);
        RectTransform layerRect = layerObject.GetComponent<RectTransform>();
        layerRect.anchorMin = layerRect.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 layerSize = new Vector2(
            normalizedSourceRect.width * parent.sizeDelta.x,
            normalizedSourceRect.height * parent.sizeDelta.y);
        Vector2 layerPosition = new Vector2(
            (normalizedSourceRect.x + normalizedSourceRect.width * 0.5f - 0.5f)
                * parent.sizeDelta.x,
            (0.5f - normalizedSourceRect.y - normalizedSourceRect.height * 0.5f)
                * parent.sizeDelta.y);
        layerRect.anchoredPosition = layerPosition;
        layerRect.sizeDelta = layerSize;

        RawImage image = CreateRawImage(
            layerRect,
            layerName + " Artwork",
            texture,
            Vector2.zero,
            layerSize);
        image.raycastTarget = false;
        return new VictoryLogoLayer
        {
            rect = layerRect,
            image = image,
            settledPosition = layerPosition,
            startTime = startTime,
            overshoot = overshoot,
            entryRotation = entryRotation
        };
    }

    public IEnumerator PlayVictoryCelebration()
    {
        if (victoryCelebrationRoot == null) yield break;

        ResetTrafficJamUi();
        StopMainMenuHeartHudUpdates();
        StopDefeatHeartBankUpdates();
        if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
        HidePauseOverlays();
        HideOutcomeRoots();
        retryHeartConsumedForCurrentDefeat = false;
        game.TogglePause(true);
        CarPrototypeFeedback.Victory();

        ResetVictoryCelebrationVisuals();
        victoryCelebrationRoot.SetActive(true);
        victoryCelebrationRoot.transform.SetAsLastSibling();
        if (victoryFireworksBack != null)
            victoryFireworksBack.Play(VictoryCelebrationDuration);
        if (victoryFireworksFront != null)
            victoryFireworksFront.Play(VictoryCelebrationDuration);

        float startedAt = Time.unscaledTime;
        float elapsed = 0f;
        while (elapsed < VictoryCelebrationDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            UpdateVictoryCelebrationVisuals(elapsed);
            yield return null;
        }

        UpdateVictoryCelebrationVisuals(VictoryCelebrationDuration);
        HideVictoryCelebration();
    }

    private void ResetVictoryCelebrationVisuals()
    {
        if (victoryLogoRoot != null)
            victoryLogoRoot.localScale = Vector3.one;
        ResetVictoryLogoLayer(victoryBadgeLayer);
        for (int index = 0; index < victoryParkLayers.Length; index++)
            ResetVictoryLogoLayer(victoryParkLayers[index]);
        ResetVictoryLogoLayer(victoryDashLayer);
        ResetVictoryLogoLayer(victoryFinalLayer);
    }

    private static void ResetVictoryLogoLayer(VictoryLogoLayer layer)
    {
        if (layer == null || layer.rect == null || layer.image == null) return;
        layer.rect.anchoredPosition = layer.settledPosition;
        layer.rect.localScale = Vector3.one * 0.01f;
        layer.rect.localRotation = Quaternion.Euler(0f, 0f, layer.entryRotation);
        SetVictoryLogoLayerAlpha(layer, 0f);
    }

    private void UpdateVictoryCelebrationVisuals(float elapsed)
    {
        UpdateVictoryPopLayer(victoryBadgeLayer, elapsed, 0.16f, 0.28f, 42f);
        for (int index = 0; index < victoryParkLayers.Length; index++)
            UpdateVictoryPopLayer(victoryParkLayers[index], elapsed, 0.10f, 0.27f, 76f);
        UpdateVictoryDashLayer(elapsed);

        float constructionFade = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(1.06f, 1.18f, elapsed));
        SetVictoryLogoLayerAlpha(
            victoryBadgeLayer,
            GetVictoryLayerCurrentAlpha(victoryBadgeLayer) * constructionFade);
        for (int index = 0; index < victoryParkLayers.Length; index++)
            SetVictoryLogoLayerAlpha(
                victoryParkLayers[index],
                GetVictoryLayerCurrentAlpha(victoryParkLayers[index]) * constructionFade);
        SetVictoryLogoLayerAlpha(
            victoryDashLayer,
            GetVictoryLayerCurrentAlpha(victoryDashLayer) * constructionFade);

        float finalAlpha = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(1.06f, 1.18f, elapsed));
        finalAlpha *= 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(3.70f, 3.98f, elapsed));
        SetVictoryLogoLayerAlpha(victoryFinalLayer, finalAlpha);
        if (victoryFinalLayer != null && victoryFinalLayer.rect != null)
        {
            victoryFinalLayer.rect.localScale = Vector3.one;
            victoryFinalLayer.rect.localRotation = Quaternion.identity;
        }

        if (victoryLogoRoot == null) return;
        float assemblyScale = 1f;
        if (elapsed >= 0.98f && elapsed < 1.10f)
        {
            float progress = VictoryEaseOutCubic(Mathf.InverseLerp(0.98f, 1.10f, elapsed));
            assemblyScale = Mathf.LerpUnclamped(0.92f, 1.10f, progress);
        }
        else if (elapsed < 1.22f && elapsed >= 1.10f)
        {
            float progress = VictoryEaseInOutSine(Mathf.InverseLerp(1.10f, 1.22f, elapsed));
            assemblyScale = Mathf.LerpUnclamped(1.10f, 0.96f, progress);
        }
        else if (elapsed < 1.35f && elapsed >= 1.22f)
        {
            float progress = VictoryEaseInOutSine(Mathf.InverseLerp(1.22f, 1.35f, elapsed));
            assemblyScale = Mathf.LerpUnclamped(0.96f, 1f, progress);
        }
        victoryLogoRoot.localScale = Vector3.one * assemblyScale;
    }

    private static void UpdateVictoryPopLayer(
        VictoryLogoLayer layer,
        float elapsed,
        float riseDuration,
        float settleDuration,
        float entryOffset)
    {
        if (layer == null || layer.rect == null || layer.image == null) return;
        float age = elapsed - layer.startTime;
        if (age < 0f)
        {
            SetVictoryLogoLayerAlpha(layer, 0f);
            return;
        }

        float scale;
        if (age < riseDuration)
        {
            float progress = VictoryEaseOutCubic(age / riseDuration);
            scale = Mathf.LerpUnclamped(0.08f, layer.overshoot, progress);
        }
        else if (age < riseDuration + settleDuration * 0.52f)
        {
            float progress = VictoryEaseInOutSine(
                (age - riseDuration) / (settleDuration * 0.52f));
            scale = Mathf.LerpUnclamped(layer.overshoot, 0.91f, progress);
        }
        else if (age < riseDuration + settleDuration)
        {
            float progress = VictoryEaseInOutSine(
                (age - riseDuration - settleDuration * 0.52f)
                / (settleDuration * 0.48f));
            scale = Mathf.LerpUnclamped(0.91f, 1f, progress);
        }
        else
        {
            scale = 1f;
        }

        float arrival = VictoryEaseOutCubic(Mathf.Clamp01(age / (riseDuration + 0.08f)));
        layer.rect.anchoredPosition = layer.settledPosition + Vector2.up * entryOffset * (1f - arrival);
        layer.rect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Lerp(layer.entryRotation, 0f, arrival));
        layer.rect.localScale = Vector3.one * scale;
        SetVictoryLogoLayerAlpha(layer, Mathf.Clamp01(age / 0.035f));
    }

    private void UpdateVictoryDashLayer(float elapsed)
    {
        VictoryLogoLayer layer = victoryDashLayer;
        if (layer == null || layer.rect == null || layer.image == null) return;
        float age = elapsed - layer.startTime;
        if (age < 0f)
        {
            SetVictoryLogoLayerAlpha(layer, 0f);
            return;
        }

        float scale;
        if (age < 0.12f)
        {
            scale = Mathf.Lerp(0.16f, 0.62f, VictoryEaseOutCubic(age / 0.12f));
        }
        else if (age < 0.28f)
        {
            scale = Mathf.LerpUnclamped(
                0.62f,
                layer.overshoot,
                VictoryEaseOutCubic((age - 0.12f) / 0.16f));
        }
        else if (age < 0.45f)
        {
            scale = Mathf.LerpUnclamped(
                layer.overshoot,
                0.88f,
                VictoryEaseInOutSine((age - 0.28f) / 0.17f));
        }
        else if (age < 0.58f)
        {
            scale = Mathf.LerpUnclamped(
                0.88f,
                1.08f,
                VictoryEaseInOutSine((age - 0.45f) / 0.13f));
        }
        else if (age < 0.71f)
        {
            scale = Mathf.LerpUnclamped(
                1.08f,
                1f,
                VictoryEaseInOutSine((age - 0.58f) / 0.13f));
        }
        else
        {
            scale = 1f;
        }

        float arrival = VictoryEaseOutCubic(Mathf.Clamp01(age / 0.28f));
        layer.rect.anchoredPosition = layer.settledPosition + Vector2.down * 54f * (1f - arrival);
        layer.rect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Lerp(layer.entryRotation, 0f, arrival));
        layer.rect.localScale = Vector3.one * scale;
        SetVictoryLogoLayerAlpha(layer, Mathf.Clamp01(age / 0.045f));
    }

    private static float GetVictoryLayerCurrentAlpha(VictoryLogoLayer layer)
    {
        return layer != null && layer.image != null ? layer.image.color.a : 0f;
    }

    private static void SetVictoryLogoLayerAlpha(VictoryLogoLayer layer, float alpha)
    {
        if (layer == null || layer.image == null) return;
        Color color = layer.image.color;
        color.a = Mathf.Clamp01(alpha);
        layer.image.color = color;
    }

    private void HideVictoryCelebration()
    {
        if (victoryFireworksBack != null) victoryFireworksBack.Stop();
        if (victoryFireworksFront != null) victoryFireworksFront.Stop();
        if (victoryCelebrationRoot != null) victoryCelebrationRoot.SetActive(false);
    }

    private void RefreshVictoryPanelText()
    {
        if (victoryLevelText != null && game != null)
            victoryLevelText.text = $"Level {Mathf.Max(1, game.BoardNumber)}";
    }

    private void StartVictoryUiReveal()
    {
        if (victoryUiRevealCoroutine != null)
            StopCoroutine(victoryUiRevealCoroutine);
        ResetVictoryUiRevealVisuals();
        if (Application.isPlaying)
            victoryUiRevealCoroutine = StartCoroutine(AnimateVictoryUiReveal());
        else
            SetVictoryUiRevealSettled();
    }

    private void ResetVictoryUiRevealVisuals()
    {
        for (int index = 0; index < victoryPerfectLetters.Length; index++)
        {
            PerfectLetterLayer layer = victoryPerfectLetters[index];
            if (layer == null || layer.rect == null || layer.image == null) continue;
            layer.rect.anchoredPosition = layer.settledPosition + new Vector2(-42f, 68f);
            layer.rect.localScale = Vector3.one * 0.01f;
            layer.rect.localRotation = Quaternion.Euler(0f, 0f, layer.entryRotation);
            SetRawImageAlpha(layer.image, 0f);
        }
        if (victoryPerfectWord != null)
        {
            victoryPerfectWordRect.localScale = Vector3.one;
            SetRawImageAlpha(victoryPerfectWord, 0f);
        }

        for (int index = 0; index < victoryObjectiveRows.Length; index++)
        {
            OutcomeObjectiveRow row = victoryObjectiveRows[index];
            if (row == null || row.root == null || !row.root.activeSelf) continue;
            row.root.transform.localScale = Vector3.one * 0.01f;
        }
    }

    private IEnumerator AnimateVictoryUiReveal()
    {
        float start = Time.unscaledTime;
        const float duration = 1.18f;
        while (Time.unscaledTime - start < duration)
        {
            UpdateVictoryUiReveal(Time.unscaledTime - start);
            yield return null;
        }
        SetVictoryUiRevealSettled();
        victoryUiRevealCoroutine = null;
    }

    private void UpdateVictoryUiReveal(float elapsed)
    {
        for (int index = 0; index < victoryPerfectLetters.Length; index++)
        {
            PerfectLetterLayer layer = victoryPerfectLetters[index];
            if (layer == null || layer.rect == null || layer.image == null) continue;
            float age = elapsed - layer.startTime;
            if (age < 0f)
            {
                SetRawImageAlpha(layer.image, 0f);
                continue;
            }

            float scale;
            if (age < 0.10f)
            {
                scale = Mathf.LerpUnclamped(
                    0.10f,
                    layer.overshoot,
                    VictoryEaseOutCubic(age / 0.10f));
            }
            else if (age < 0.22f)
            {
                scale = Mathf.LerpUnclamped(
                    layer.overshoot,
                    0.84f,
                    VictoryEaseInOutSine((age - 0.10f) / 0.12f));
            }
            else if (age < 0.34f)
            {
                scale = Mathf.LerpUnclamped(
                    0.84f,
                    1.08f,
                    VictoryEaseInOutSine((age - 0.22f) / 0.12f));
            }
            else if (age < 0.46f)
            {
                scale = Mathf.LerpUnclamped(
                    1.08f,
                    1f,
                    VictoryEaseInOutSine((age - 0.34f) / 0.12f));
            }
            else
            {
                scale = 1f;
            }

            float arrival = VictoryEaseOutCubic(Mathf.Clamp01(age / 0.18f));
            layer.rect.anchoredPosition = layer.settledPosition
                + new Vector2(-42f, 68f) * (1f - arrival);
            layer.rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Lerp(layer.entryRotation, 0f, arrival));
            layer.rect.localScale = Vector3.one * scale;
            SetRawImageAlpha(layer.image, Mathf.Clamp01(age / 0.025f));
        }

        float wordAlpha = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(0.72f, 0.80f, elapsed));
        SetRawImageAlpha(victoryPerfectWord, wordAlpha);
        float lettersFade = 1f - wordAlpha;
        for (int index = 0; index < victoryPerfectLetters.Length; index++)
        {
            PerfectLetterLayer layer = victoryPerfectLetters[index];
            if (layer == null || layer.image == null) continue;
            SetRawImageAlpha(layer.image, layer.image.color.a * lettersFade);
        }

        int visibleObjectiveIndex = 0;
        for (int index = 0; index < victoryObjectiveRows.Length; index++)
        {
            OutcomeObjectiveRow row = victoryObjectiveRows[index];
            if (row == null || row.root == null || !row.root.activeSelf) continue;
            float age = elapsed - 0.67f - visibleObjectiveIndex * 0.075f;
            float objectiveScale = age <= 0f
                ? 0.01f
                : age < 0.14f
                    ? Mathf.LerpUnclamped(0.2f, 1.20f, VictoryEaseOutCubic(age / 0.14f))
                    : age < 0.27f
                        ? Mathf.LerpUnclamped(1.20f, 0.92f, VictoryEaseInOutSine((age - 0.14f) / 0.13f))
                        : age < 0.39f
                            ? Mathf.LerpUnclamped(0.92f, 1f, VictoryEaseInOutSine((age - 0.27f) / 0.12f))
                            : 1f;
            row.root.transform.localScale = Vector3.one * objectiveScale;
            visibleObjectiveIndex++;
        }
    }

    private void SetVictoryUiRevealSettled()
    {
        for (int index = 0; index < victoryPerfectLetters.Length; index++)
        {
            PerfectLetterLayer layer = victoryPerfectLetters[index];
            if (layer == null || layer.image == null) continue;
            SetRawImageAlpha(layer.image, 0f);
        }
        if (victoryPerfectWord != null)
        {
            victoryPerfectWordRect.localScale = Vector3.one;
            SetRawImageAlpha(victoryPerfectWord, 1f);
        }
        for (int index = 0; index < victoryObjectiveRows.Length; index++)
        {
            OutcomeObjectiveRow row = victoryObjectiveRows[index];
            if (row != null && row.root != null && row.root.activeSelf)
                row.root.transform.localScale = Vector3.one;
        }
    }

    private static void SetRawImageAlpha(RawImage image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static float VictoryEaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float VictoryEaseInOutSine(float value)
    {
        return -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(value)) - 1f) * 0.5f;
    }

    private void BuildVictory(Transform parent)
    {
        victoryRoot = CreateFullScreenRoot(parent, "Victory Overlay");
        Image inputBlocker = victoryRoot.AddComponent<Image>();
        inputBlocker.sprite = GetRuntimeWhiteSprite();
        inputBlocker.color = new Color(0f, 0.025f, 0.08f, 0.78f);

        RawImage panel = CreateRawImage(
            victoryRoot.transform,
            "Approved New Victory Panel Artwork",
            LoadTexture("CarPrototype/VictoryUI/VictoryPanel"),
            new Vector2(0f, 18f),
            new Vector2(1000f, 1189f));
        victoryPanelRoot = panel.rectTransform;
        victoryPanelRoot.localScale = Vector3.one * 1.18f;

        victoryLevelText = CreateText(
            victoryPanelRoot,
            "Victory Level Number",
            "Level 1",
            new Vector2(0f, 449f),
            new Vector2(690f, 118f),
            80f,
            TextAlignmentOptions.Center);
        victoryLevelText.fontStyle = FontStyles.Bold;
        victoryLevelText.fontWeight = FontWeight.Heavy;
        victoryLevelText.color = new Color32(255, 247, 244, 255);
        victoryLevelText.outlineColor = new Color32(18, 28, 91, 255);
        victoryLevelText.outlineWidth = 0.17f;
        victoryLevelText.characterSpacing = -1f;

        BuildVictoryPerfectLayers(victoryPanelRoot);
        BuildOutcomeObjectiveRows(victoryPanelRoot, victoryObjectiveRows);

        RawImage closeArtwork = CreateRawImage(
            victoryPanelRoot,
            "Approved Win Close Artwork",
            LoadTexture("CarPrototype/VictoryUI/CloseButton"),
            new Vector2(352f, 414f),
            new Vector2(128f, 128f));
        closeArtwork.raycastTarget = false;
        victoryCloseArtwork = closeArtwork.rectTransform;
        RawImage continueArtwork = CreateRawImage(
            victoryPanelRoot,
            "Approved Win Continue Artwork",
            LoadTexture("CarPrototype/VictoryUI/ContinueButton"),
            new Vector2(10f, -355f),
            new Vector2(503f, 204f));
        continueArtwork.raycastTarget = false;
        victoryContinueArtwork = continueArtwork.rectTransform;

        CreateInvisibleArtworkButton(
            victoryPanelRoot,
            "Win Close Button",
            new Vector2(352f, 414f),
            new Vector2(118f, 118f),
            victoryCloseArtwork,
            ReturnHomeFromOutcome);
        CreateInvisibleArtworkButton(
            victoryPanelRoot,
            "Win Continue Button",
            new Vector2(10f, -355f),
            new Vector2(485f, 184f),
            victoryContinueArtwork,
            ContinueToNextLevel);
        victoryRoot.SetActive(false);
    }

    private void BuildVictoryPerfectLayers(Transform parent)
    {
        string[] textureNames =
        {
            "PerfectP", "PerfectE", "PerfectR", "PerfectF",
            "PerfectE2", "PerfectC", "PerfectT", "PerfectBang"
        };
        // Source-art crop geometry, scaled to the approved 1000-wide panel.
        float[] sourceX = { 488f, 645f, 760f, 880f, 995f, 1110f, 1230f, 1335f };
        float[] sourceY = { 526f, 526f, 524f, 526f, 524f, 522f, 524f, 523f };
        float[] sourceWidth = { 157f, 115f, 120f, 115f, 115f, 120f, 105f, 105f };
        float[] sourceHeight = { 217f, 217f, 219f, 215f, 218f, 224f, 217f, 219f };
        const float sourceArtWidth = 1888f;
        const float sourceArtHeight = 2246f;
        const float panelWidth = 1000f;
        const float panelHeight = 1189f;

        for (int index = 0; index < victoryPerfectLetters.Length; index++)
        {
            Vector2 size = new Vector2(
                sourceWidth[index] / sourceArtWidth * panelWidth,
                sourceHeight[index] / sourceArtHeight * panelHeight);
            Vector2 position = new Vector2(
                (sourceX[index] + sourceWidth[index] * 0.5f) / sourceArtWidth * panelWidth
                    - panelWidth * 0.5f,
                panelHeight * 0.5f
                    - (sourceY[index] + sourceHeight[index] * 0.5f) / sourceArtHeight * panelHeight);
            RawImage image = CreateRawImage(
                parent,
                $"PERFECT {textureNames[index]} Artwork",
                LoadTexture("CarPrototype/VictoryUI/" + textureNames[index]),
                position,
                size);
            image.raycastTarget = false;
            victoryPerfectLetters[index] = new PerfectLetterLayer
            {
                rect = image.rectTransform,
                image = image,
                settledPosition = position,
                startTime = 0.15f + index * 0.035f,
                entryRotation = index % 2 == 0 ? -4.5f : 4f,
                overshoot = index == 7 ? 1.82f : 1.56f + index % 3 * 0.07f
            };
        }

        Vector2 wordSize = new Vector2(952f / sourceArtWidth * panelWidth, 224f / sourceArtHeight * panelHeight);
        Vector2 wordPosition = new Vector2(
            (488f + 952f * 0.5f) / sourceArtWidth * panelWidth - panelWidth * 0.5f,
            panelHeight * 0.5f - (522f + 224f * 0.5f) / sourceArtHeight * panelHeight);
        victoryPerfectWord = CreateRawImage(
            parent,
            "Approved Settled PERFECT Word Artwork",
            LoadTexture("CarPrototype/VictoryUI/PerfectWord"),
            wordPosition,
            wordSize);
        victoryPerfectWord.raycastTarget = false;
        victoryPerfectWordRect = victoryPerfectWord.rectTransform;
    }

    private Button CreateInvisibleArtworkButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Transform visualTarget,
        UnityEngine.Events.UnityAction click)
    {
        RawImage target = CreateRawImage(parent, name, Texture2D.whiteTexture, position, size);
        target.color = new Color(1f, 1f, 1f, 0.001f);
        target.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(CarPrototypeFeedback.ButtonTap);
        button.onClick.AddListener(click);
        SimpleButtonPressAnimation animation =
            target.gameObject.AddComponent<SimpleButtonPressAnimation>();
        animation.SetVisualTarget(visualTarget);
        return button;
    }

    private GameObject BuildOutcomeRoot(Transform parent, string name, Texture2D artwork)
    {
        GameObject root = CreateFullScreenRoot(parent, name);
        Image inputBlocker = root.AddComponent<Image>();
        inputBlocker.sprite = GetRuntimeWhiteSprite();
        inputBlocker.color = new Color(0f, 0.03f, 0.1f, 0.75f);

        Vector2 artworkSize = artwork != null
            ? new Vector2(artwork.width, artwork.height)
            : new Vector2(1125f, 2436f);
        CreateRawImage(root.transform, name + " Approved Artwork", artwork, Vector2.zero, artworkSize);
        return root;
    }

    private void BuildOutcomeObjectiveRows(Transform parent, OutcomeObjectiveRow[] rows)
    {
        string[] colorNames = { "Red", "Green", "Blue", "Purple", "Yellow", "Pink" };
        string[] textureNames =
        {
            "objective_car_red",
            "objective_car_green",
            "objective_car_blue",
            "objective_car_purple",
            "objective_car_yellow",
            "objective_car_purple"
        };
        Texture2D checkTexture = LoadTexture("objective_complete_check");

        for (int index = 0; index < rows.Length; index++)
        {
            GameObject rowRoot = new GameObject($"Result {colorNames[index]} Objective", typeof(RectTransform));
            rowRoot.transform.SetParent(parent, false);
            RectTransform rowRect = rowRoot.GetComponent<RectTransform>();
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(4f, -58f);
            rowRect.sizeDelta = new Vector2(620f, 112f);

            RawImage car = CreateRawImage(
                rowRoot.transform,
                $"Result {colorNames[index]} Car",
                LoadTexture(textureNames[index]),
                new Vector2(-82f, 0f),
                new Vector2(128f, 108f));
            if (index == 5) car.color = new Color(1f, 0.42f, 0.78f);
            TextMeshProUGUI counter = CreateText(
                rowRoot.transform,
                $"Result {colorNames[index]} Counter",
                "0",
                new Vector2(84f, 0f),
                new Vector2(100f, 88f),
                58f,
                TextAlignmentOptions.Center);
            counter.fontStyle = FontStyles.Bold;
            counter.outlineColor = new Color32(5, 28, 72, 255);
            counter.outlineWidth = 0.18f;
            RawImage check = CreateRawImage(
                rowRoot.transform,
                $"Result {colorNames[index]} Check",
                checkTexture,
                new Vector2(84f, 0f),
                new Vector2(82f, 82f));

            rows[index] = new OutcomeObjectiveRow
            {
                root = rowRoot,
                car = car,
                counter = counter,
                check = check
            };
        }
    }

    private Button CreateOutcomeButton(
        Transform parent,
        string name,
        Texture2D texture,
        Vector2 position,
        Vector2 size,
        UnityEngine.Events.UnityAction click)
    {
        RawImage image = CreateRawImage(parent, name, texture, position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(CarPrototypeFeedback.ButtonTap);
        button.onClick.AddListener(click);
        image.gameObject.AddComponent<SimpleButtonPressAnimation>();
        return button;
    }

    private void CreateSettingRow(Transform parent, string label, string icon, Vector2 position, System.Action toggle, System.Func<bool> getter)
    {
        CreateText(parent, label + " Text", label, position + new Vector2(0f, 95f), new Vector2(204f, 48f), 29f, TextAlignmentOptions.Center);
        Button toggleButton = CreateButton(parent, label + " Toggle", null, position, new Vector2(133f, 133f), null);
        Image iconImage = CreateImage(toggleButton.transform, label + " Toggle Icon", LoadSprite(icon), Vector2.zero, new Vector2(80f, 80f));
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;
        toggleButton.onClick.AddListener(() =>
        {
            toggle();
            UpdateToggle(toggleButton, getter());
        });
        UpdateToggle(toggleButton, getter());
    }

    private Button CreateInvisibleSettingsButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        UnityEngine.Events.UnityAction click)
    {
        Button button = CreateButton(parent, name, null, position, size, click);
        Image image = button.GetComponent<Image>();
        // Fully transparent UI graphics can be culled by CanvasRenderer and
        // then disappear from GraphicRaycaster. A nearly invisible alpha keeps
        // the artwork untouched while ensuring the button receives taps.
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;
        button.targetGraphic = image;
        return button;
    }

    private Button CreateSettingsArtworkToggle(
        Transform parent,
        string name,
        Vector2 position,
        UnityEngine.Events.UnityAction toggle,
        System.Func<bool> getter)
    {
        // The three supplied green controls are 256 x 254 source pixels in
        // the updated artwork. Sampling those exact pixels keeps the visible
        // PNG untouched while providing press and disabled-state feedback.
        Vector2 sourceSize = new Vector2(256f, 254f);
        Button button = CreateSettingsArtworkButton(parent, name, position, sourceSize, null, false, false);
        RawImage disabledOverlay = CreateSettingsArtworkSample(
            button.transform,
            name + " Disabled",
            Vector2.zero,
            sourceSize,
            position);
        disabledOverlay.raycastTarget = false;
        disabledOverlay.material = GetSettingsDisabledMaterial();

        System.Action refresh = () => disabledOverlay.gameObject.SetActive(!getter());
        button.onClick.AddListener(() =>
        {
            toggle();
            refresh();
        });
        refresh();
        return button;
    }

    private Button CreateSettingsArtworkButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 sourceSize,
        UnityEngine.Events.UnityAction click,
        bool useScaleAnimation = true,
        bool usePressTint = true,
        bool useRedPressMask = false)
    {
        Vector2 displaySize = ScaleSettingsArtworkSize(sourceSize);
        Button button = CreateButton(parent, name, null, position, displaySize, null);
        Image hitArea = button.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0.001f);
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;

        if (usePressTint)
        {
            RawImage pressTint = CreateSettingsArtworkSample(
                button.transform,
                name + " Press Tint",
                Vector2.zero,
                sourceSize,
                position);
            pressTint.raycastTarget = false;
            pressTint.material = GetMorePressMaterial(useRedPressMask);
            button.targetGraphic = pressTint;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = Color.clear;
            colors.selectedColor = Color.clear;
            colors.pressedColor = new Color(0.58f, 0.58f, 0.58f, 0.72f);
            colors.disabledColor = Color.clear;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
        }
        else
        {
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
        }

        if (click != null) button.onClick.AddListener(click);
        if (useScaleAnimation) AddScalePressAnimation(button);
        return button;
    }

    private RawImage CreateSettingsArtworkSample(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 sourceSize,
        Vector2 artworkPosition)
    {
        RawImage image = CreateRawImage(
            parent,
            name,
            LoadTexture("SettingsUi/settings_page"),
            position,
            ScaleSettingsArtworkSize(sourceSize));
        image.uvRect = GetSettingsArtworkUvRect(artworkPosition, sourceSize);
        return image;
    }

    private Vector2 ScaleSettingsArtworkSize(Vector2 sourceSize)
    {
        return new Vector2(
            sourceSize.x * layout.settingsPanelSize.x / 1125f,
            sourceSize.y * layout.settingsPanelSize.y / 2436f);
    }

    private Rect GetSettingsArtworkUvRect(Vector2 artworkPosition, Vector2 sourceSize)
    {
        const float sourceWidth = 1125f;
        const float sourceHeight = 2436f;
        float scaleX = Mathf.Max(0.0001f, layout.settingsPanelSize.x / sourceWidth);
        float scaleY = Mathf.Max(0.0001f, layout.settingsPanelSize.y / sourceHeight);
        Vector2 panelLocal = artworkPosition - layout.settingsPanelPosition;
        float centerX = sourceWidth * 0.5f + panelLocal.x / scaleX;
        float centerYFromTop = sourceHeight * 0.5f - panelLocal.y / scaleY;
        return new Rect(
            (centerX - sourceSize.x * 0.5f) / sourceWidth,
            (sourceHeight - centerYFromTop - sourceSize.y * 0.5f) / sourceHeight,
            sourceSize.x / sourceWidth,
            sourceSize.y / sourceHeight);
    }

    private static Material GetSettingsDisabledMaterial()
    {
        if (settingsDisabledMaterial != null) return settingsDisabledMaterial;

        Shader shader = Resources.Load<Shader>("SettingsUi/SettingsGreenDisabled");
        if (shader == null) shader = Shader.Find("UI/Colorarrows Settings Green Disabled");
        if (shader == null) return null;

        settingsDisabledMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return settingsDisabledMaterial;
    }

    private Button CreateMoreArtworkButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 sourceSize,
        bool useRedMask,
        UnityEngine.Events.UnityAction click)
    {
        Button button = CreateButton(parent, name, null, position, ScaleMoreArtworkSize(sourceSize), null);
        Image hitArea = button.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0.001f);
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;

        RawImage pressTint = CreateRawImage(
            button.transform,
            name + " Press Tint",
            LoadTexture("SettingsUi/more_page"),
            Vector2.zero,
            ScaleMoreArtworkSize(sourceSize));
        pressTint.uvRect = GetMoreArtworkUvRect(position, sourceSize);
        pressTint.raycastTarget = false;
        pressTint.material = GetMorePressMaterial(useRedMask);

        button.targetGraphic = pressTint;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = Color.clear;
        colors.selectedColor = Color.clear;
        colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 0.78f);
        colors.disabledColor = Color.clear;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        if (click != null) button.onClick.AddListener(click);
        return button;
    }

    private Vector2 ScaleMoreArtworkSize(Vector2 sourceSize)
    {
        return new Vector2(
            sourceSize.x * layout.morePanelSize.x / 1125f,
            sourceSize.y * layout.morePanelSize.y / 2436f);
    }

    private Rect GetMoreArtworkUvRect(Vector2 artworkPosition, Vector2 sourceSize)
    {
        const float sourceWidth = 1125f;
        const float sourceHeight = 2436f;
        float scaleX = Mathf.Max(0.0001f, layout.morePanelSize.x / sourceWidth);
        float scaleY = Mathf.Max(0.0001f, layout.morePanelSize.y / sourceHeight);
        Vector2 panelLocal = artworkPosition - layout.morePanelPosition;
        float centerX = sourceWidth * 0.5f + panelLocal.x / scaleX;
        float centerYFromTop = sourceHeight * 0.5f - panelLocal.y / scaleY;
        return new Rect(
            (centerX - sourceSize.x * 0.5f) / sourceWidth,
            (sourceHeight - centerYFromTop - sourceSize.y * 0.5f) / sourceHeight,
            sourceSize.x / sourceWidth,
            sourceSize.y / sourceHeight);
    }

    private static Material GetMorePressMaterial(bool useRedMask)
    {
        Material material = useRedMask ? moreRedPressMaterial : moreGreenPressMaterial;
        if (material != null) return material;

        Shader shader = Resources.Load<Shader>("SettingsUi/ArtworkPressMask");
        if (shader == null) shader = Shader.Find("UI/Colorarrows Artwork Press Mask");
        if (shader == null) return null;

        material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetFloat("_MaskChannel", useRedMask ? 1f : 0f);
        if (useRedMask)
            moreRedPressMaterial = material;
        else
            moreGreenPressMaterial = material;
        return material;
    }

    private static void UpdateToggle(Button button, bool enabled)
    {
        Image image = button.GetComponent<Image>();
        image.color = enabled ? new Color(0.28f, 0.83f, 0.17f) : new Color(0.42f, 0.46f, 0.52f);
        if (button.transform.Find(button.gameObject.name + " Icon") != null) return;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text == null)
        {
            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(button.transform, false);
            text = label.GetComponent<TextMeshProUGUI>();
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 26f;
            text.font = TMP_Settings.defaultFontAsset;
            text.raycastTarget = false;
        }
        text.text = enabled ? "ON" : "OFF";
    }

    private void OpenSettings()
    {
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        settingsRoot.SetActive(true);
        game.TogglePause(true);
    }

    private void CloseSettings()
    {
        settingsRoot.SetActive(false);
        moreRoot.SetActive(false);
        leaveRoot.SetActive(false);
        game.TogglePause(false);
    }

    private void OpenMorePage()
    {
        settingsRoot.SetActive(false);
        moreRoot.SetActive(true);
    }

    private void CloseMorePage()
    {
        moreRoot.SetActive(false);
        settingsRoot.SetActive(true);
    }

    private void OpenLeaveConfirmation()
    {
        settingsRoot.SetActive(false);
        leaveRoot.SetActive(true);
    }

    private void CancelLeaveConfirmation()
    {
        leaveRoot.SetActive(false);
        settingsRoot.SetActive(true);
    }

    private void LeaveCurrentBoard()
    {
        leaveRoot.SetActive(false);
        game.RestartCurrentBoard();
        ShowMainMenu();
    }

    private void RetryCurrentLevelFromOutcome()
    {
        if (game.HeartsEnabled
            && defeatRoot != null
            && defeatRoot.activeSelf
            && CarPrototypeHeartBank.AvailableHearts <= 0)
        {
            RefreshDefeatHeartBank();
            return;
        }

        CancelDefeatReveal();
        HideOutcomeRoots();
        game.RestartCurrentBoard();
        game.TogglePause(false);
    }

    private void ReturnHomeFromOutcome()
    {
        CancelDefeatReveal();
        HideOutcomeRoots();
        game.RestartCurrentBoard();
        ShowMainMenu();
    }

    private void ContinueToNextLevel()
    {
        CancelDefeatReveal();
        HideOutcomeRoots();
        game.TogglePause(false);
        game.LoadNextLevel();
    }

    private void CancelDefeatReveal()
    {
        if (defeatRevealCoroutine == null) return;

        StopCoroutine(defeatRevealCoroutine);
        defeatRevealCoroutine = null;
    }

    private void HideOutcomeRoots()
    {
        StopDefeatHeartBankUpdates();
        if (victoryUiRevealCoroutine != null)
        {
            StopCoroutine(victoryUiRevealCoroutine);
            victoryUiRevealCoroutine = null;
        }
        retryHeartConsumedForCurrentDefeat = false;
        HideVictoryCelebration();
        if (defeatRoot != null) defeatRoot.SetActive(false);
        if (victoryRoot != null) victoryRoot.SetActive(false);
        if (trafficJamRoot != null) trafficJamRoot.SetActive(false);
    }

    private void ActivateDefeatOutcome()
    {
        if (defeatRoot == null) return;

        if (game.HeartsEnabled)
            RefreshDefeatHeartBank();
        defeatRoot.SetActive(true);
        if (game.HeartsEnabled && Application.isPlaying)
            StartDefeatHeartBankUpdates();
    }

    private void RefreshDefeatHeartBank()
    {
        int availableHearts = CarPrototypeHeartBank.AvailableHearts;
        for (int index = 0; index < defeatHeartImages.Length; index++)
        {
            if (defeatHeartImages[index] != null)
                defeatHeartImages[index].texture = index < availableHearts ? fullHeartTexture : staleHeartTexture;
        }

        if (lossRetryButton != null)
            lossRetryButton.interactable = availableHearts > 0;

        if (defeatHeartCountdownText == null) return;
        if (availableHearts >= CarPrototypeHeartBank.MaximumHearts)
        {
            defeatHeartCountdownText.text = "MAX";
            return;
        }

        int remainingSeconds = Mathf.Clamp(
            Mathf.CeilToInt((float)CarPrototypeHeartBank.SecondsUntilNextHeart),
            0,
            CarPrototypeHeartBank.RecoveryMinutes * 60);
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        defeatHeartCountdownText.text = $"{minutes:00}:{seconds:00}";
    }

    private void StartDefeatHeartBankUpdates()
    {
        StopDefeatHeartBankUpdates();
        defeatHeartBankCoroutine = StartCoroutine(UpdateDefeatHeartBank());
    }

    private void StopDefeatHeartBankUpdates()
    {
        if (defeatHeartBankCoroutine == null) return;

        StopCoroutine(defeatHeartBankCoroutine);
        defeatHeartBankCoroutine = null;
    }

    private IEnumerator UpdateDefeatHeartBank()
    {
        var refreshDelay = new WaitForSecondsRealtime(0.25f);
        while (defeatRoot != null && defeatRoot.activeSelf)
        {
            RefreshDefeatHeartBank();
            yield return refreshDelay;
        }

        defeatHeartBankCoroutine = null;
    }

    private void HidePauseOverlays()
    {
        if (settingsRoot != null) settingsRoot.SetActive(false);
        if (moreRoot != null) moreRoot.SetActive(false);
        if (leaveRoot != null) leaveRoot.SetActive(false);
    }

    private void RefreshOutcomeObjectives(OutcomeObjectiveRow[] rows, bool showCompletionChecks)
    {
        if (rows == null || game == null) return;

        bool[] visible =
        {
            game.RedGoal > 0,
            game.GreenGoal > 0,
            game.BlueGoal > 0,
            game.PurpleGoal > 0,
            game.YellowGoal > 0,
            game.PinkGoal > 0
        };
        int[] remaining =
        {
            game.RedObjectiveRemaining,
            game.GreenObjectiveRemaining,
            game.BlueObjectiveRemaining,
            game.PurpleObjectiveRemaining,
            game.YellowObjectiveRemaining,
            game.PinkObjectiveRemaining
        };

        int visibleCount = 0;
        for (int index = 0; index < visible.Length; index++)
        {
            if (visible[index]) visibleCount++;
        }

        bool newVictoryPanel = rows == victoryObjectiveRows;
        float spacing;
        float topOffset;
        if (newVictoryPanel)
        {
            spacing = visibleCount <= 2 ? 142f : visibleCount == 3 ? 126f : 122f;
            topOffset = (visibleCount - 1) * spacing * 0.5f;
        }
        else
        {
            spacing = visibleCount <= 2 ? 126f : visibleCount == 3 ? 104f : 88f;
            topOffset = (visibleCount - 1) * spacing * 0.5f;
        }
        int visibleIndex = 0;
        for (int index = 0; index < rows.Length; index++)
        {
            OutcomeObjectiveRow row = rows[index];
            if (row == null || row.root == null) continue;

            row.root.SetActive(visible[index]);
            if (!visible[index]) continue;

            RectTransform rowRect = row.root.GetComponent<RectTransform>();
            rowRect.anchoredPosition = newVictoryPanel
                ? (visibleCount <= 3
                    ? new Vector2(4f, -42f + topOffset - visibleIndex * spacing)
                    : new Vector2(
                        (visibleIndex % 2 == 0 ? -155f : 155f),
                        76f - visibleIndex / 2 * spacing))
                : new Vector2(4f, 200f + topOffset - visibleIndex * spacing);

            if (newVictoryPanel)
            {
                bool useGrid = visibleCount > 3;
                rowRect.sizeDelta = useGrid
                    ? new Vector2(270f, 100f)
                    : new Vector2(620f, 112f);
                row.car.rectTransform.anchoredPosition = useGrid
                    ? new Vector2(-38f, 0f)
                    : new Vector2(-82f, 0f);
                row.car.rectTransform.sizeDelta = useGrid
                    ? new Vector2(105f, 90f)
                    : new Vector2(128f, 108f);
                row.check.rectTransform.anchoredPosition = useGrid
                    ? new Vector2(72f, 0f)
                    : new Vector2(84f, 0f);
                row.check.rectTransform.sizeDelta = useGrid
                    ? new Vector2(68f, 68f)
                    : new Vector2(82f, 82f);
            }
            row.counter.gameObject.SetActive(!showCompletionChecks);
            row.counter.text = Mathf.Max(0, remaining[index]).ToString();
            row.check.gameObject.SetActive(showCompletionChecks);
            visibleIndex++;
        }
    }

    private void BuildHearts(Transform parent)
    {
        GameObject root = new GameObject("Hearts", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        heartsRoot = root.GetComponent<RectTransform>();
        heartsRoot.anchorMin = heartsRoot.anchorMax = new Vector2(0.5f, 0.5f);

        for (int index = 0; index < heartImages.Length; index++)
        {
            float x = (index - 1) * layout.heartSpacing;
            heartImages[index] = CreateRawImage(
                heartsRoot,
                $"Heart {index + 1}",
                fullHeartTexture,
                new Vector2(x, 0f),
                layout.heartSize);
        }

        ApplyHeartLayout();
    }

    private void ApplyHeartLayout()
    {
        if (heartsRoot == null) return;

        heartsRoot.anchoredPosition = layout.heartsPosition;
        heartsRoot.sizeDelta = new Vector2(
            layout.heartSize.x + layout.heartSpacing * (heartImages.Length - 1),
            layout.heartSize.y);

        for (int index = 0; index < heartImages.Length; index++)
        {
            if (heartImages[index] == null) continue;
            heartImages[index].rectTransform.anchoredPosition = new Vector2(
                (index - 1) * layout.heartSpacing,
                0f);
            heartImages[index].rectTransform.sizeDelta = layout.heartSize;
        }
    }

    private void RefreshHearts(int heartCount)
    {
        int clampedCount = Mathf.Clamp(heartCount, 0, heartImages.Length);
        if (displayedHearts >= 0 && clampedCount == displayedHearts)
            return;

        if (displayedHearts < 0 || clampedCount > displayedHearts)
        {
            StopAllCoroutines();
            defeatRevealCoroutine = null;
            DestroyFallingHearts();
            for (int index = 0; index < heartImages.Length; index++)
            {
                if (heartImages[index] != null)
                    heartImages[index].texture = index < clampedCount ? fullHeartTexture : staleHeartTexture;
            }

            displayedHearts = clampedCount;
            return;
        }

        for (int index = displayedHearts - 1; index >= clampedCount; index--)
        {
            if (heartImages[index] != null)
                heartImages[index].texture = staleHeartTexture;
            PlayHeartLoss(index);
        }

        displayedHearts = clampedCount;
    }

    private void PlayHeartLoss(int heartIndex)
    {
        if (heartsRoot == null || brokenHeartTexture == null) return;

        float x = (heartIndex - 1) * layout.heartSpacing;
        RawImage fallingHeart = CreateRawImage(
            heartsRoot,
            $"Falling Broken Heart {heartIndex + 1}",
            brokenHeartTexture,
            new Vector2(x, 0f),
            layout.heartSize);
        fallingHeart.transform.SetAsLastSibling();
        StartCoroutine(AnimateFallingHeart(fallingHeart));
    }

    private IEnumerator AnimateFallingHeart(RawImage fallingHeart)
    {
        RectTransform rect = fallingHeart.rectTransform;
        Vector2 startPosition = rect.anchoredPosition;
        float duration = Mathf.Max(0.1f, layout.heartLossDuration);
        float elapsed = 0f;

        while (elapsed < duration && fallingHeart != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float fallProgress = progress * progress;
            rect.anchoredPosition = startPosition + Vector2.down * (layout.heartLossFallDistance * fallProgress);

            Color color = Color.white;
            color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, progress));
            fallingHeart.color = color;
            yield return null;
        }

        if (fallingHeart != null)
            Destroy(fallingHeart.gameObject);
    }

    private IEnumerator RevealDefeatAfterHeartLoss()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, layout.heartLossDuration));
        ActivateDefeatOutcome();
        defeatRevealCoroutine = null;
    }

    private void DestroyFallingHearts()
    {
        if (heartsRoot == null) return;

        for (int index = heartsRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = heartsRoot.GetChild(index);
            if (child.name.StartsWith("Falling Broken Heart"))
                Destroy(child.gameObject);
        }
    }

    private TextMeshProUGUI CreateObjectiveCounter(Transform parent, string name, Vector2 position)
    {
        TextMeshProUGUI counter = CreateText(parent, name, "3", position,
            layout.objectiveStatusSize, layout.objectiveStatusFontSize, TextAlignmentOptions.Center);
        counter.fontStyle = FontStyles.Bold;
        counter.outlineColor = new Color32(5, 28, 72, 255);
        counter.outlineWidth = 0.18f;
        return counter;
    }

    private RawImage CreateRawImage(Transform parent, string name, Texture texture, Vector2 position, Vector2 size)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        item.transform.SetParent(parent, false);
        RawImage image = item.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    private Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : GetRuntimeWhiteSprite();
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    private Image CreateFullScreenDim(Transform parent, string name, Color color)
    {
        Image image = CreateImage(parent, name, null, Vector2.zero, Vector2.zero);
        image.color = color;
        image.raycastTarget = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static GameObject CreateFullScreenRoot(Transform parent, string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return root;
    }

    private Button CreateButton(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        Image image = CreateImage(parent, name, sprite, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(CarPrototypeFeedback.ButtonTap);
        if (click != null) button.onClick.AddListener(click);
        return button;
    }

    private static void AddScalePressAnimation(Button button)
    {
        if (button != null && button.GetComponent<SimpleButtonPressAnimation>() == null)
            button.gameObject.AddComponent<SimpleButtonPressAnimation>();
    }

    private Button CreateSettingsArtworkCloseButton(
        Transform parent,
        Vector2 position,
        UnityEngine.Events.UnityAction click)
    {
        const float sourceWidth = 1125f;
        const float sourceHeight = 2436f;
        const float closeSize = 120f;
        // Source-space center of the red X baked into settings_panel_final.png.
        const float sourceCenterX = 927.5f;
        const float sourceCenterYFromTop = 543f;

        RawImage pressTint = CreateRawImage(
            parent,
            "Close Artwork Press Tint",
            LoadTexture("settings_panel_final"),
            position,
            new Vector2(closeSize, closeSize));
        pressTint.uvRect = new Rect(
            (sourceCenterX - closeSize * 0.5f) / sourceWidth,
            (sourceHeight - sourceCenterYFromTop - closeSize * 0.5f) / sourceHeight,
            closeSize / sourceWidth,
            closeSize / sourceHeight);
        // Invisible at rest, then a sampled dark overlay fades in while pressed.
        pressTint.color = Color.clear;

        Button button = CreateButton(parent, "Close", null, position, new Vector2(closeSize, closeSize), click);
        button.GetComponent<Image>().color = Color.clear;
        button.targetGraphic = pressTint;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = Color.clear;
        colors.selectedColor = Color.clear;
        colors.pressedColor = new Color(0.38f, 0.38f, 0.38f, 0.58f);
        colors.disabledColor = Color.clear;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        return button;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.characterSpacing = GameTextCharacterSpacing;
        text.wordSpacing = 0f;
        text.color = Color.white;
        // A dark, clean SDF edge keeps white labels readable over the vivid
        // menu artwork, matching the friendly outlined reference treatment.
        text.outlineColor = GameTextOutlineColor;
        text.outlineWidth = GameTextOutlineWidth;
        // Labels are visual only. Leaving their raycast target on would block the button beneath.
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private static Sprite LoadSprite(string resourceName)
    {
        return Resources.Load<Sprite>(resourceName);
    }

    private static Texture2D LoadTexture(string resourceName)
    {
        return Resources.Load<Texture2D>(resourceName);
    }

    private static Sprite GetRuntimeWhiteSprite()
    {
        if (runtimeWhiteSprite != null) return runtimeWhiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        runtimeWhiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        runtimeWhiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return runtimeWhiteSprite;
    }

    private static void ApplyButtonAndLabel(Transform root, string buttonName, string labelName, Vector2 position, Vector2 size)
    {
        ApplyRect(root, buttonName, position, size);
        ApplyRect(root, labelName, position, size);
    }

    private static void ApplyTextStyle(Transform root, string name, float fontSize)
    {
        TextMeshProUGUI text = FindNamedComponent<TextMeshProUGUI>(root, name);
        if (text != null) text.fontSize = fontSize;
    }

    private static void ApplyRect(Transform root, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = FindNamedComponent<RectTransform>(root, name);
        if (rect == null) return;

        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetNamedActive(Transform root, string name, bool isActive)
    {
        Transform child = FindNamedTransform(root, name);
        if (child != null) child.gameObject.SetActive(isActive);
    }

    private static Transform FindNamedTransform(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index].gameObject.name == name)
                return transforms[index];
        }

        return null;
    }

    private static T FindNamedComponent<T>(Transform root, string name) where T : Component
    {
        if (root == null) return null;

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].gameObject.name == name)
                return components[index];
        }

        return null;
    }
}
