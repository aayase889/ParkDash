using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "CarPrototypeHudLayout", menuName = "Color Sort/3D Car HUD Layout")]
public sealed class CarPrototypeHudLayout : ScriptableObject
{
    [Header("3D Scene - Camera")]
    [Tooltip("World position of the gameplay camera.")]
    public Vector3 sceneCameraPosition = new Vector3(0f, 15.4f, -8.7f);
    [Tooltip("World point the gameplay camera looks toward.")]
    public Vector3 sceneCameraLookAt = new Vector3(0f, 0f, -1.1f);
    [Min(1f)]
    public float sceneCameraOrthographicSize = 9.25f;

    [Header("3D Scene - Board Pieces")]
    [Tooltip("Z position of the first car row. Lower values move the car board downward on screen.")]
    public float sceneBoardFirstRowZ = 2.3f;
    [Range(0.3f, 1.2f)]
    // Match the standard lower tray scale (1.0 off-board scale * 0.68)
    // so cars keep the same visual size when moving out of a 3x3 board.
    public float scenePieceScale3x3 = 0.68f;
    [Range(0.3f, 1.2f)]
    public float scenePieceScale4x4 = 0.66f;
    [Range(0.3f, 1.2f)]
    public float scenePieceScale5x5 = 0.53f;
    [Tooltip("Base scale used by two-cell limousines after they leave the board, before the tray or side-parking pose multiplier is applied.")]
    [Range(0.25f, 0.9f)]
    public float sceneLimousineOffBoardScale = 0.48f;

    [Header("3D Scene - Asphalt")]
    public Vector3 sceneAsphaltGroundPosition = new Vector3(0f, -0.58f, -3.5f);
    public Vector3 sceneAsphaltGroundSize = new Vector3(16f, 0.36f, 30f);
    public float sceneRoadCenterZ = -2.2f;
    [Min(1f)]
    public float sceneRoadDepth = 17.6f;
    public Color sceneBackgroundAsphaltColor = Color.white;
    public Color scenePlayfieldAsphaltColor = Color.white;
    public Color sceneRoadBorderColor = new Color(0.47f, 0.48f, 0.49f);

    [Header("3D Scene - Parking Markings")]
    [Tooltip("World X/Z center of the straight-in bottom matching lot.")]
    public Vector2 sceneMatchTrayPosition = new Vector2(0f, -5.65f);
    [Tooltip("Distance between adjacent matching-tray car centers.")]
    [Min(0.5f)]
    public float sceneMatchTraySlotSpacing = 1.3928572f;
    [Tooltip("Demo 2 reference width and depth for each straight matching-lot parking space. Later boards scale both dimensions with their smaller cars.")]
    public Vector2 sceneMatchTrayBaySize = new Vector2(1.3928572f, 2.30f);
    [Tooltip("World X/Z center of the horizontal side-parking bay.")]
    public Vector2 sceneSideParkingPosition = new Vector2(0f, -9.50f);
    [Tooltip("Width and depth of the side-parking bay. Its width accommodates a full limousine.")]
    public Vector2 sceneSideParkingSize = new Vector2(3.05f, 1.30f);
    [Tooltip("Thickness of all white parking lines.")]
    [Range(0.025f, 0.18f)]
    public float sceneParkingLineWidth = 0.085f;
    [Tooltip("Legacy hatch-line spacing retained for saved-layout compatibility.")]
    [Range(0.12f, 0.65f)]
    public float sceneSideParkingHatchSpacing = 0.24f;

    [Header("Compact Top HUD")]
    public Vector2 levelPillPosition = new Vector2(0f, 820f);
    public Vector2 levelPillSize = new Vector2(450f, 170f);
    public Vector2 levelTextPosition = new Vector2(0f, 820f);
    public Vector2 levelTextSize = new Vector2(360f, 90f);
    public float levelTextFontSize = 55f;

    [Header("Main Menu")]
    public Vector2 mainMenuPlayPosition = new Vector2(0f, -95f);
    public Vector2 mainMenuPlaySize = new Vector2(640f, 190f);
    public string mainMenuPlayText = "PLAY";
    public float mainMenuPlayFontSize = 74f;
    public Vector2 mainMenuDemoPosition = new Vector2(0f, -660f);
    public Vector2 mainMenuDemoSize = new Vector2(500f, 110f);
    public string mainMenuDemoText = "DEMO";
    public float mainMenuDemoFontSize = 42f;
    public Vector2 mainMenuShopTabPosition = new Vector2(-390f, -1005f);
    public Vector2 mainMenuHomeTabPosition = new Vector2(0f, -1005f);
    public Vector2 mainMenuLockedTabPosition = new Vector2(390f, -1005f);
    public Vector2 mainMenuTabSize = new Vector2(430f, 300f);
    public Vector2 mainMenuTallTabSize = new Vector2(470f, 430f);
    public Vector2 mainMenuSelectedTabOffset = new Vector2(0f, 65f);
    public Vector2 mainMenuSelectedLabelOffset = new Vector2(0f, -145f);
    public Vector2 mainMenuSelectedLabelSize = new Vector2(260f, 70f);
    public float mainMenuSelectedLabelFontSize = 42f;

    [Header("Car Objectives")]
    public Vector2 redObjectiveCarPosition = new Vector2(-365f, 855f);
    public Vector2 greenObjectiveCarPosition = new Vector2(-365f, 777f);
    public Vector2 blueObjectiveCarPosition = new Vector2(-365f, 699f);
    public Vector2 yellowObjectiveCarPosition = new Vector2(-365f, 621f);
    public Vector2 objectiveCarSize = new Vector2(88f, 74f);
    public Vector2 redObjectiveStatusPosition = new Vector2(-270f, 855f);
    public Vector2 greenObjectiveStatusPosition = new Vector2(-270f, 777f);
    public Vector2 blueObjectiveStatusPosition = new Vector2(-270f, 699f);
    public Vector2 yellowObjectiveStatusPosition = new Vector2(-270f, 621f);
    public Vector2 objectiveStatusSize = new Vector2(86f, 64f);
    public Vector2 objectiveCheckSize = new Vector2(58f, 58f);
    public float objectiveStatusFontSize = 46f;

    [Header("Lives")]
    [Tooltip("Center of the three-heart row, directly below the level badge.")]
    public Vector2 heartsPosition = new Vector2(0f, 680f);
    public Vector2 heartSize = new Vector2(82f, 82f);
    [Min(1f)]
    public float heartSpacing = 92f;
    [Min(1f)]
    public float heartLossFallDistance = 160f;
    [Min(0.1f)]
    public float heartLossDuration = 0.58f;

    [Header("Bottom Controls")]
    public Vector2 extraSlotPosition = new Vector2(-80f, -765f);
    public Vector2 undoPosition = new Vector2(80f, -765f);
    public Vector2 boosterSize = new Vector2(126f, 126f);
    public Vector2 pausePosition = new Vector2(390f, 820f);
    public Vector2 pauseSize = new Vector2(126f, 126f);

    [Header("Level Preview Navigation")]
    [Tooltip("Developer preview controls. Turn this off before a player build.")]
    // Temporary in-game controls for checking the imported blue-car model on
    // every board. Turn this off before making a player build.
    public bool showLevelPreviewButtons = true;
    public Vector2 previousLevelPosition = new Vector2(365f, -555f);
    public Vector2 nextLevelPosition = new Vector2(365f, -655f);
    public Vector2 levelPreviewButtonSize = new Vector2(150f, 64f);

    [Header("Settings")]
    [Tooltip("The supplied settings artwork uses the full 1125 x 2436 source canvas. Its transparent margins keep the visible panel centered in the phone safe area.")]
    public Vector2 settingsPanelPosition = Vector2.zero;
    public Vector2 settingsPanelSize = new Vector2(956f, 2071f);
    public Vector2 settingsTitlePosition = new Vector2(0f, 550f);
    public Vector2 hapticsPosition = new Vector2(264f, 218f);
    public Vector2 soundsPosition = new Vector2(-241f, 218f);
    public Vector2 musicPosition = new Vector2(11f, 218f);
    public Vector2 resumePosition = new Vector2(-183f, -71f);
    public Vector2 quitPosition = new Vector2(215f, -71f);
    public Vector2 morePosition = new Vector2(11f, -241f);
    [Tooltip("Transparent hit target aligned to the close button baked into the supplied settings artwork.")]
    public Vector2 settingsClosePosition = new Vector2(424f, 578f);

    [Header("More Page")]
    public Vector2 morePanelPosition = Vector2.zero;
    public Vector2 morePanelSize = new Vector2(956f, 2071f);
    public Vector2 moreTitlePosition = new Vector2(0f, 400f);
    public Vector2 termsPosition = new Vector2(11f, 305f);
    public Vector2 privacyPosition = new Vector2(11f, 44f);
    public Vector2 moreBackPosition = new Vector2(4f, -175f);
    public Vector2 moreClosePosition = new Vector2(424f, 578f);

    [Header("Leave Confirmation")]
    public Vector2 leavePanelSize = new Vector2(960f, 1020f);
    public Vector2 leaveTitlePosition = new Vector2(0f, 125f);
    public Vector2 leaveDescriptionPosition = new Vector2(0f, 30f);
    public Vector2 leaveCancelPosition = new Vector2(-170f, -150f);
    public Vector2 leaveConfirmPosition = new Vector2(170f, -150f);

    [Header("Loss Popup")]
    public Vector2 defeatPanelSize = new Vector2(700f, 600f);
    public Vector2 defeatTitlePosition = new Vector2(0f, 110f);
    public Vector2 defeatDescriptionPosition = new Vector2(0f, 10f);
    public Vector2 restartPosition = new Vector2(0f, -150f);

    [Header("Font")]
    public TMP_FontAsset fontOverride;

    public static CarPrototypeHudLayout LoadOrDefault()
    {
        CarPrototypeHudLayout layout = Resources.Load<CarPrototypeHudLayout>("CarPrototypeHudLayout");
        return layout != null ? layout : CreateInstance<CarPrototypeHudLayout>();
    }
}
