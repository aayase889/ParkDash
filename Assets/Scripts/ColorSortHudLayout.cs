using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "ColorSortHudLayout", menuName = "Color Sort/HUD Layout")]
public class ColorSortHudLayout : ScriptableObject
{
    public Vector2 headerPosition = new Vector2(0f, -238f);
    public Vector2 headerSize = new Vector2(0f, 440f);

    public Vector2 deckPosition = Vector2.zero;
    public Vector2 deckSize = new Vector2(1240f, 488f);

    public TMP_FontAsset hudFont;

    public string boardLabelText = "BOARD";
    public Vector2 boardLabelPosition = new Vector2(-368f, 38f);
    public Vector2 boardLabelSize = new Vector2(260f, 70f);
    public float boardLabelFontSize = 56f;
    public Vector2 boardNumberPosition = new Vector2(-368f, -42f);
    public Vector2 boardNumberSize = new Vector2(210f, 190f);
    public float boardNumberFontSize = 142f;
    public float boardNumberTwoDigitFontSize = 104f;
    public float boardNumberThreeDigitFontSize = 82f;

    public Vector2 redDotPosition = new Vector2(84f, 47f);
    public Vector2 redDotSize = new Vector2(48f, 48f);
    public Vector2 redTextPosition = new Vector2(215f, 47f);
    public Vector2 redTextSize = new Vector2(150f, 64f);
    public float redTextFontSize = 54f;

    public Vector2 greenDotPosition = new Vector2(84f, -36f);
    public Vector2 greenDotSize = new Vector2(48f, 48f);
    public Vector2 greenTextPosition = new Vector2(215f, -36f);
    public Vector2 greenTextSize = new Vector2(150f, 64f);
    public float greenTextFontSize = 54f;

    public Vector2 blueDotPosition = new Vector2(84f, -112f);
    public Vector2 blueDotSize = new Vector2(42f, 42f);
    public Vector2 blueTextPosition = new Vector2(215f, -112f);
    public Vector2 blueTextSize = new Vector2(150f, 56f);
    public float blueTextFontSize = 44f;

    public Vector2 yellowDotPosition = new Vector2(84f, -178f);
    public Vector2 yellowDotSize = new Vector2(42f, 42f);
    public Vector2 yellowTextPosition = new Vector2(215f, -178f);
    public Vector2 yellowTextSize = new Vector2(150f, 56f);
    public float yellowTextFontSize = 44f;

    public Vector2 heartPosition = new Vector2(345f, -122f);
    public Vector2 heartSize = new Vector2(44f, 44f);
    public float heartSpacing = 52f;

    public Vector2 retryPosition = new Vector2(420f, 5f);
    public Vector2 retrySize = new Vector2(150f, 150f);

    [Header("Main Menu")]
    public Vector2 mainMenuPlayPosition = new Vector2(0f, -95f);
    public Vector2 mainMenuPlaySize = new Vector2(640f, 190f);
    public string mainMenuPlayText = "PLAY";
    public float mainMenuPlayFontSize = 74f;
    public Vector2 mainMenuShopTabPosition = new Vector2(-390f, -1005f);
    public Vector2 mainMenuHomeTabPosition = new Vector2(0f, -1005f);
    public Vector2 mainMenuLockedTabPosition = new Vector2(390f, -1005f);
    public Vector2 mainMenuTabSize = new Vector2(430f, 300f);
    public Vector2 mainMenuTallTabSize = new Vector2(470f, 430f);
    public Vector2 mainMenuSelectedTabOffset = new Vector2(0f, 65f);
    public Vector2 mainMenuSelectedLabelOffset = new Vector2(0f, -145f);
    public Vector2 mainMenuSelectedLabelSize = new Vector2(260f, 70f);
    public float mainMenuSelectedLabelFontSize = 42f;

    [Header("Main Board")]
    public Vector2 boardTrayPosition = new Vector2(0f, 0.5f);
    public float boardTraySize3 = 5.35f;
    public float boardTraySize4 = 5.75f;
    public float boardCenterX = 0f;
    public float boardCenterY = 0.5f;
    public float boardSpacing3 = 1.34f;
    public float boardSpacing4 = 1.18f;
    public float boardBlockScale3 = 1.34f;
    public float boardBlockScale4 = 1.02f;
    public float boardColliderSize3 = 1.36f;
    public float boardColliderSize4 = 1.02f;

    [Header("Bottom Tray")]
    public Vector2 colorTrayPosition3 = new Vector2(0f, -2.82f);
    public Vector2 colorTrayPosition4 = new Vector2(0f, -3.18f);
    public Vector2 colorTrayPosition5 = new Vector2(0f, -3.18f);
    public float colorTrayWidth3 = 4.65f;
    public float colorTrayWidth4 = 5.85f;
    public float colorTrayWidth5 = 6.75f;
    public Vector3 colorTraySlotX3 = new Vector3(-1.31f, 0f, 1.31f);
    public Vector4 colorTraySlotX4 = new Vector4(-1.90f, -0.63f, 0.63f, 1.90f);
    public Vector4 colorTraySlotX5FirstFour = new Vector4(-2.55f, -1.28f, 0f, 1.28f);
    public float colorTraySlotX5Last = 2.55f;
    public float trayBlockScale3 = 1.08f;
    public float trayBlockScale4 = 0.96f;
    public float trayBlockScale5 = 0.88f;

    [Header("Boosters")]
    public Vector2 extraSlotBoosterPosition = new Vector2(-2f, -5.05f);
    public Vector2 extraSlotBoosterSize = new Vector2(0.9f, 0.9f);
    public Vector2 undoBoosterPosition = new Vector2(-0.95f, -5.05f);
    public Vector2 undoBoosterSize = new Vector2(0.9f, 0.9f);
    public Vector2 pauseButtonPosition = new Vector2(2.45f, -5.05f);
    public Vector2 pauseButtonSize = new Vector2(0.9f, 0.9f);

    [Header("Level Debug")]
    public bool showDebugNextBoardButton = true;
    public Vector2 debugNextBoardButtonPosition = new Vector2(2.3f, -4.15f);
    public Vector2 debugNextBoardButtonSize = new Vector2(0.95f, 0.48f);
    public string debugNextBoardButtonText = "NEXT";
    public int editorPreviewBoardNumber = 1;

    [Header("Parking Slot")]
    public Vector2 parkingPosition3 = new Vector2(-2.8f, -3.2f);
    public Vector2 parkingPosition4 = new Vector2(-2.8f, -3.58f);
    public float parkingSize3 = 1.08f;
    public float parkingSize4 = 1.05f;
    public float parkingBlockScale3 = 1.08f;
    public float parkingBlockScale4 = 0.96f;

    [Header("Settings Screen")]
    public float settingsDimAlpha = 0.68f;
    public Vector2 settingsPanelPosition = new Vector2(0f, 20f);
    public Vector2 settingsPanelSize = new Vector2(700f, 960f);

    public string settingsTitleText = "SETTINGS";
    public Vector2 settingsTitlePosition = new Vector2(0f, 340f);
    public Vector2 settingsTitleSize = new Vector2(560f, 90f);
    public float settingsTitleFontSize = 64f;

    public Vector2 settingsHapticsIconPosition = new Vector2(-190f, 195f);
    public Vector2 settingsHapticsIconSize = new Vector2(80f, 80f);
    public string settingsHapticsText = "Haptics:";
    public Vector2 settingsHapticsTextPosition = new Vector2(30f, 195f);
    public Vector2 settingsHapticsTextSize = new Vector2(300f, 76f);
    public float settingsHapticsFontSize = 42f;
    public Vector2 settingsHapticsTogglePosition = new Vector2(210f, 195f);

    public Vector2 settingsSoundsIconPosition = new Vector2(-190f, 75f);
    public Vector2 settingsSoundsIconSize = new Vector2(80f, 80f);
    public string settingsSoundsText = "Sounds:";
    public Vector2 settingsSoundsTextPosition = new Vector2(30f, 75f);
    public Vector2 settingsSoundsTextSize = new Vector2(300f, 76f);
    public float settingsSoundsFontSize = 42f;
    public Vector2 settingsSoundsTogglePosition = new Vector2(210f, 75f);

    public Vector2 settingsMusicIconPosition = new Vector2(-190f, -45f);
    public Vector2 settingsMusicIconSize = new Vector2(80f, 80f);
    public string settingsMusicText = "Music:";
    public Vector2 settingsMusicTextPosition = new Vector2(30f, -45f);
    public Vector2 settingsMusicTextSize = new Vector2(300f, 76f);
    public float settingsMusicFontSize = 42f;
    public Vector2 settingsMusicTogglePosition = new Vector2(210f, -45f);

    public Vector2 settingsToggleSize = new Vector2(170f, 78f);
    public Vector2 settingsToggleKnobSize = new Vector2(66f, 66f);
    public float settingsToggleKnobOffset = 43f;
    public Vector2 settingsToggleTextSize = new Vector2(140f, 60f);
    public float settingsToggleFontSize = 30f;

    public Vector2 settingsResumePosition = new Vector2(0f, -230f);
    public Vector2 settingsResumeSize = new Vector2(360f, 96f);
    public string settingsResumeText = "Resume";
    public float settingsResumeFontSize = 42f;

    public Vector2 settingsQuitPosition = new Vector2(0f, -355f);
    public Vector2 settingsQuitSize = new Vector2(360f, 96f);
    public string settingsQuitText = "Quit";
    public float settingsQuitFontSize = 42f;

    public Vector2 settingsMorePosition = new Vector2(0f, -455f);
    public Vector2 settingsMoreSize = new Vector2(260f, 70f);
    public string settingsMoreText = "More";
    public float settingsMoreFontSize = 34f;

    public Vector2 settingsClosePosition = new Vector2(315f, 390f);
    public Vector2 settingsCloseSize = new Vector2(92f, 92f);

    [Header("Settings More Page")]
    public Vector2 morePanelPosition = new Vector2(0f, 20f);
    public Vector2 morePanelSize = new Vector2(700f, 760f);
    public string moreTitleText = "MORE";
    public Vector2 moreTitlePosition = new Vector2(0f, 260f);
    public Vector2 moreTitleSize = new Vector2(560f, 80f);
    public float moreTitleFontSize = 58f;
    public string termsButtonText = "Terms";
    public Vector2 termsButtonPosition = new Vector2(0f, 80f);
    public Vector2 termsButtonSize = new Vector2(420f, 86f);
    public float termsButtonFontSize = 38f;
    public string privacyButtonText = "Privacy";
    public Vector2 privacyButtonPosition = new Vector2(0f, -40f);
    public Vector2 privacyButtonSize = new Vector2(420f, 86f);
    public float privacyButtonFontSize = 38f;
    public string moreBackButtonText = "Back";
    public Vector2 moreBackButtonPosition = new Vector2(0f, -220f);
    public Vector2 moreBackButtonSize = new Vector2(320f, 80f);
    public float moreBackButtonFontSize = 36f;
    public Vector2 moreClosePosition = new Vector2(275f, 315f);
    public Vector2 moreCloseSize = new Vector2(92f, 92f);
    public string termsUrl = "";
    public string privacyUrl = "";

    public static ColorSortHudLayout LoadOrDefault()
    {
        ColorSortHudLayout layout = Resources.Load<ColorSortHudLayout>("ColorSortHudLayout");
        if (layout != null) return layout;

        return CreateInstance<ColorSortHudLayout>();
    }
}
