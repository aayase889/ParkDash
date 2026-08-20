using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class ColorSortLauncher : MonoBehaviour
{
    private bool hapticsEnabled = true;
    private bool soundsEnabled = true;
    private bool musicEnabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeGame()
    {
        // The car prototype has its own 3D bootstrap and must never spawn the 2D UI.
        if (SceneManager.GetActiveScene().name == "CarPrototype3D") return;

        if (FindFirstObjectByType<ColorSortLauncher>() != null) return;

        // 1. Spawns launcher object
        GameObject launcherObj = new GameObject("_ColorSortLauncher");
        launcherObj.AddComponent<ColorSortLauncher>();
        DontDestroyOnLoad(launcherObj);
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        ForcePortraitOrientation();

        // 2. Configure Camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("MainCamera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.28f, 0.52f); // matches bg.png
        cam.orthographic = true;
        cam.orthographicSize = 6.0f;
        cam.transform.position = new Vector3(0f, -0.6f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.transform.localScale = Vector3.one;

        // 3. Spawns procedural sprites for block graphics
        Sprite redBlock = LoadUISprite("block_red", CreateRoundedRectSprite(128, 128, 26, new Color(0.94f, 0.27f, 0.27f)));
        Sprite greenBlock = LoadUISprite("block_green", CreateRoundedRectSprite(128, 128, 26, new Color(0.06f, 0.73f, 0.51f)));
        Sprite neutralBlock = LoadUISprite("block_grey", CreateRoundedRectSprite(128, 128, 26, new Color(0.11f, 0.11f, 0.18f)));
        Sprite blueBlock = LoadUISprite("block_blue", CreateRoundedRectSprite(128, 128, 26, new Color(0.06f, 0.72f, 0.86f)));
        Sprite yellowBlock = LoadUISprite("block_yellow", CreateRoundedRectSprite(128, 128, 26, new Color(0.95f, 0.82f, 0.16f)));
        Sprite arrowSprite = LoadArrowSprite();
        Sprite boardTraySprite = LoadBoardTraySprite();
        Sprite backgroundSprite = LoadBackgroundSprite();
        Sprite pointBoxSprite = LoadUISprite("point_box", CreateRoundedRectSprite(706, 298, 42, new Color(0.12f, 0.54f, 0.86f)));
        Sprite colorSlotsSprite = LoadUISprite("color_slots_box", CreateRoundedRectSprite(565, 163, 24, new Color(0.04f, 0.66f, 0.93f)));
        Sprite colorSlots4Sprite = LoadUISprite("color_slots_box_4", CreateRoundedRectSprite(836, 245, 30, new Color(0.04f, 0.66f, 0.93f)));
        Sprite colorSlots5Sprite = LoadUISprite("color_slots_box_5", CreateRoundedRectSprite(1086, 245, 30, new Color(0.04f, 0.66f, 0.93f)));
        Sprite boosterCircleSprite = CreateCircularSprite(128, new Color(0.04f, 0.66f, 0.93f));
        Sprite extraSlotBoosterSprite = LoadUISprite("booster_extra_slot", boosterCircleSprite);
        Sprite undoBoosterSprite = LoadUISprite("booster_undo", boosterCircleSprite);
        Sprite pauseButtonSprite = LoadUISprite("pause_button", boosterCircleSprite);

        // Progress indicators should show their assigned color clearly in the header.
        Sprite btnBg = CreateCircularSprite(96, Color.white);
        Sprite slotBg = CreateRoundedRectSprite(128, 128, 22, new Color(1f, 1f, 1f, 0.02f));
        Sprite parkBg = LoadUISprite("park", CreateRoundedRectSprite(128, 128, 22, new Color(0.04f, 0.66f, 0.93f)));
        Sprite parkButtonBg = CreateCircularSprite(128, new Color(0.96f, 0.62f, 0.04f, 0.03f));
        Sprite upperDeckSprite = LoadUISprite("upper_deck", CreateRoundedRectSprite(1125, 443, 40, new Color(0.08f, 0.48f, 0.92f)));
        Sprite retryButtonSprite = LoadUISprite("retry_button", CreateCircularSprite(160, new Color(1f, 0.68f, 0.12f)));
        Sprite heartSprite = CreateHeartSprite(96);
        ColorSortHudLayout hudLayout = ColorSortHudLayout.LoadOrDefault();
        CreateWorldBackground(backgroundSprite, cam);

        // 4. Setup Canvas UI
        GameObject canvasObj = new GameObject("UICanvas");
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one;
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2400);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 5. Build GameManager GameObject
        GameObject gmObj = new GameObject("GameManager");
        UnityGameManager gm = gmObj.AddComponent<UnityGameManager>();
        
        gm.spriteBlockRed = redBlock;
        gm.spriteBlockGreen = greenBlock;
        gm.spriteBlockNeutral = neutralBlock;
        gm.spriteBlockBlue = blueBlock;
        gm.spriteBlockYellow = yellowBlock;
        gm.spriteArrow = arrowSprite;
        gm.spriteBoardTray = boardTraySprite;
        gm.spriteColorSlotsTray = colorSlotsSprite;
        gm.spriteColorSlotsTray4 = colorSlots4Sprite;
        gm.spriteColorSlotsTray5 = colorSlots5Sprite;
        gm.spriteParkSlot = parkBg;
        gm.spriteExtraSlotBooster = extraSlotBoosterSprite;
        gm.spriteUndoBooster = undoBoosterSprite;
        gm.spritePauseButton = pauseButtonSprite;
        gm.gameFont = GetUsableHudFont(hudLayout.hudFont);

        // 6. Build UI Hierarchy
        // Create Safe Container
        GameObject container = new GameObject("UIContainer");
        container.transform.SetParent(canvasObj.transform, false);
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        RectTransform containerRt = container.AddComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0f, 0f);
        containerRt.anchorMax = new Vector2(1f, 1f);
        containerRt.sizeDelta = Vector2.zero;

        // Header deck
        GameObject header = new GameObject("HeaderPanel");
        header.transform.SetParent(container.transform, false);
        RectTransform headerRt = header.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.anchoredPosition = hudLayout.headerPosition;
        headerRt.sizeDelta = hudLayout.headerSize;

        GameObject upperDeck = new GameObject("UpperDeckArt");
        upperDeck.transform.SetParent(header.transform, false);
        RectTransform deckRt = upperDeck.AddComponent<RectTransform>();
        deckRt.anchorMin = new Vector2(0.5f, 0.5f);
        deckRt.anchorMax = new Vector2(0.5f, 0.5f);
        deckRt.anchoredPosition = hudLayout.deckPosition;
        deckRt.sizeDelta = hudLayout.deckSize;
        Image deckImg = upperDeck.AddComponent<Image>();
        deckImg.sprite = upperDeckSprite;
        deckImg.preserveAspect = true;
        deckImg.raycastTarget = false;

        GameObject boardLabelObj = new GameObject("BoardLabel");
        boardLabelObj.transform.SetParent(header.transform, false);
        RectTransform boardLabelRt = boardLabelObj.AddComponent<RectTransform>();
        boardLabelRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardLabelRt.anchoredPosition = hudLayout.boardLabelPosition;
        boardLabelRt.sizeDelta = hudLayout.boardLabelSize;
        TextMeshProUGUI boardLabelTxt = boardLabelObj.AddComponent<TextMeshProUGUI>();
        boardLabelTxt.alignment = TextAlignmentOptions.CenterGeoAligned;
        boardLabelTxt.text = hudLayout.boardLabelText;
        ApplyHudFont(boardLabelTxt, hudLayout.hudFont);
        boardLabelTxt.fontSize = hudLayout.boardLabelFontSize;
        boardLabelTxt.fontWeight = FontWeight.Heavy;
        boardLabelTxt.color = Color.white;

        GameObject boardObj = new GameObject("BoardCounter");
        boardObj.transform.SetParent(header.transform, false);
        RectTransform boardRt = boardObj.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = hudLayout.boardNumberPosition;
        boardRt.sizeDelta = hudLayout.boardNumberSize;
        TextMeshProUGUI boardTxt = boardObj.AddComponent<TextMeshProUGUI>();
        boardTxt.alignment = TextAlignmentOptions.CenterGeoAligned;
        boardTxt.text = "1";
        ApplyHudFont(boardTxt, hudLayout.hudFont);
        boardTxt.fontSize = hudLayout.boardNumberFontSize;
        boardTxt.fontWeight = FontWeight.Heavy;
        boardTxt.color = Color.white;
        gm.boardText = boardTxt;

        GameObject redDot = new GameObject("RedDot");
        redDot.transform.SetParent(header.transform, false);
        RectTransform redDotRt = redDot.AddComponent<RectTransform>();
        redDotRt.anchorMin = new Vector2(0.5f, 0.5f);
        redDotRt.anchorMax = new Vector2(0.5f, 0.5f);
        redDotRt.anchoredPosition = hudLayout.redDotPosition;
        redDotRt.sizeDelta = hudLayout.redDotSize;
        Image rd = redDot.AddComponent<Image>();
        rd.sprite = btnBg;
        rd.color = new Color(0.94f, 0.04f, 0.12f);
        rd.raycastTarget = false;

        GameObject redTxtObj = new GameObject("RedProgress");
        redTxtObj.transform.SetParent(header.transform, false);
        RectTransform redTxtRt = redTxtObj.AddComponent<RectTransform>();
        redTxtRt.anchorMin = new Vector2(0.5f, 0.5f);
        redTxtRt.anchorMax = new Vector2(0.5f, 0.5f);
        redTxtRt.anchoredPosition = hudLayout.redTextPosition;
        redTxtRt.sizeDelta = hudLayout.redTextSize;
        TextMeshProUGUI redVal = redTxtObj.AddComponent<TextMeshProUGUI>();
        redVal.alignment = TextAlignmentOptions.CenterGeoAligned;
        redVal.text = "0/3";
        ApplyHudFont(redVal, hudLayout.hudFont);
        redVal.fontSize = hudLayout.redTextFontSize;
        redVal.fontWeight = FontWeight.Heavy;
        redVal.color = Color.white;
        gm.redProgressText = redVal;

        GameObject greenDot = new GameObject("GreenDot");
        greenDot.transform.SetParent(header.transform, false);
        RectTransform greenDotRt = greenDot.AddComponent<RectTransform>();
        greenDotRt.anchorMin = new Vector2(0.5f, 0.5f);
        greenDotRt.anchorMax = new Vector2(0.5f, 0.5f);
        greenDotRt.anchoredPosition = hudLayout.greenDotPosition;
        greenDotRt.sizeDelta = hudLayout.greenDotSize;
        Image gd = greenDot.AddComponent<Image>();
        gd.sprite = btnBg;
        gd.color = new Color(0.08f, 0.83f, 0f);
        gd.raycastTarget = false;

        GameObject greenTxtObj = new GameObject("GreenProgress");
        greenTxtObj.transform.SetParent(header.transform, false);
        RectTransform greenTxtRt = greenTxtObj.AddComponent<RectTransform>();
        greenTxtRt.anchorMin = new Vector2(0.5f, 0.5f);
        greenTxtRt.anchorMax = new Vector2(0.5f, 0.5f);
        greenTxtRt.anchoredPosition = hudLayout.greenTextPosition;
        greenTxtRt.sizeDelta = hudLayout.greenTextSize;
        TextMeshProUGUI greenVal = greenTxtObj.AddComponent<TextMeshProUGUI>();
        greenVal.alignment = TextAlignmentOptions.CenterGeoAligned;
        greenVal.text = "0/3";
        ApplyHudFont(greenVal, hudLayout.hudFont);
        greenVal.fontSize = hudLayout.greenTextFontSize;
        greenVal.fontWeight = FontWeight.Heavy;
        greenVal.color = Color.white;
        gm.greenProgressText = greenVal;

        GameObject blueDot = new GameObject("BlueDot");
        blueDot.transform.SetParent(header.transform, false);
        RectTransform blueDotRt = blueDot.AddComponent<RectTransform>();
        blueDotRt.anchorMin = new Vector2(0.5f, 0.5f);
        blueDotRt.anchorMax = new Vector2(0.5f, 0.5f);
        blueDotRt.anchoredPosition = hudLayout.blueDotPosition;
        blueDotRt.sizeDelta = hudLayout.blueDotSize;
        Image bd = blueDot.AddComponent<Image>();
        bd.sprite = btnBg;
        bd.color = new Color(0.04f, 0.76f, 0.95f);
        bd.raycastTarget = false;
        gm.blueProgressDot = bd;

        GameObject blueTxtObj = new GameObject("BlueProgress");
        blueTxtObj.transform.SetParent(header.transform, false);
        RectTransform blueTxtRt = blueTxtObj.AddComponent<RectTransform>();
        blueTxtRt.anchorMin = new Vector2(0.5f, 0.5f);
        blueTxtRt.anchorMax = new Vector2(0.5f, 0.5f);
        blueTxtRt.anchoredPosition = hudLayout.blueTextPosition;
        blueTxtRt.sizeDelta = hudLayout.blueTextSize;
        TextMeshProUGUI blueVal = blueTxtObj.AddComponent<TextMeshProUGUI>();
        blueVal.alignment = TextAlignmentOptions.CenterGeoAligned;
        blueVal.text = "0/2";
        ApplyHudFont(blueVal, hudLayout.hudFont);
        blueVal.fontSize = hudLayout.blueTextFontSize;
        blueVal.fontWeight = FontWeight.Heavy;
        blueVal.color = Color.white;
        gm.blueProgressText = blueVal;

        GameObject yellowDot = new GameObject("YellowDot");
        yellowDot.transform.SetParent(header.transform, false);
        RectTransform yellowDotRt = yellowDot.AddComponent<RectTransform>();
        yellowDotRt.anchorMin = new Vector2(0.5f, 0.5f);
        yellowDotRt.anchorMax = new Vector2(0.5f, 0.5f);
        yellowDotRt.anchoredPosition = hudLayout.yellowDotPosition;
        yellowDotRt.sizeDelta = hudLayout.yellowDotSize;
        Image yd = yellowDot.AddComponent<Image>();
        yd.sprite = btnBg;
        yd.color = new Color(0.95f, 0.82f, 0.16f);
        yd.raycastTarget = false;
        gm.yellowProgressDot = yd;

        GameObject yellowTxtObj = new GameObject("YellowProgress");
        yellowTxtObj.transform.SetParent(header.transform, false);
        RectTransform yellowTxtRt = yellowTxtObj.AddComponent<RectTransform>();
        yellowTxtRt.anchorMin = new Vector2(0.5f, 0.5f);
        yellowTxtRt.anchorMax = new Vector2(0.5f, 0.5f);
        yellowTxtRt.anchoredPosition = hudLayout.yellowTextPosition;
        yellowTxtRt.sizeDelta = hudLayout.yellowTextSize;
        TextMeshProUGUI yellowVal = yellowTxtObj.AddComponent<TextMeshProUGUI>();
        yellowVal.alignment = TextAlignmentOptions.CenterGeoAligned;
        yellowVal.text = "0/2";
        ApplyHudFont(yellowVal, hudLayout.hudFont);
        yellowVal.fontSize = hudLayout.yellowTextFontSize;
        yellowVal.fontWeight = FontWeight.Heavy;
        yellowVal.color = Color.white;
        gm.yellowProgressText = yellowVal;

        Image[] heartImages = new Image[3];
        for (int i = 0; i < heartImages.Length; i++)
        {
            GameObject heartObj = new GameObject("Heart_" + i);
            heartObj.transform.SetParent(header.transform, false);
            RectTransform heartRt = heartObj.AddComponent<RectTransform>();
            heartRt.anchorMin = new Vector2(0.5f, 0.5f);
            heartRt.anchorMax = new Vector2(0.5f, 0.5f);
            heartRt.anchoredPosition = hudLayout.heartPosition + new Vector2(i * hudLayout.heartSpacing, 0f);
            heartRt.sizeDelta = hudLayout.heartSize;
            Image heartImg = heartObj.AddComponent<Image>();
            heartImg.sprite = heartSprite;
            heartImg.color = new Color(1f, 0.12f, 0.2f);
            heartImg.raycastTarget = false;
            heartImages[i] = heartImg;
        }
        gm.heartImages = heartImages;

        GameObject retryObj = new GameObject("RetryButton");
        retryObj.transform.SetParent(header.transform, false);
        RectTransform retryRt = retryObj.AddComponent<RectTransform>();
        retryRt.anchorMin = new Vector2(0.5f, 0.5f);
        retryRt.anchorMax = new Vector2(0.5f, 0.5f);
        retryRt.anchoredPosition = hudLayout.retryPosition;
        retryRt.sizeDelta = hudLayout.retrySize;
        Image retryImg = retryObj.AddComponent<Image>();
        retryImg.sprite = retryButtonSprite;
        retryImg.preserveAspect = true;
        Button retryBtn = retryObj.AddComponent<Button>();
        retryBtn.onClick.AddListener(gm.ClickRetryCurrentBoard);

        // Footer Section Layout
        GameObject footer = new GameObject("FooterPanel");
        footer.transform.SetParent(container.transform, false);
        RectTransform footerRt = footer.AddComponent<RectTransform>();
        footerRt.anchorMin = new Vector2(0f, 0f);
        footerRt.anchorMax = new Vector2(1f, 0f);
        footerRt.anchoredPosition = new Vector3(0f, 240f, 0f);
        footerRt.sizeDelta = new Vector2(-100f, 440f);

        // Power-ups buttons layout (5 items)
        GameObject pPanel = new GameObject("PowerupBar");
        pPanel.transform.SetParent(footer.transform, false);
        RectTransform ppRt = pPanel.AddComponent<RectTransform>();
        ppRt.anchorMin = new Vector2(0.5f, 0f);
        ppRt.anchorMax = new Vector2(0.5f, 0f);
        ppRt.anchoredPosition = new Vector3(0f, -145f, 0f);
        ppRt.sizeDelta = new Vector2(680f, 150f);

        string[] pNames = { "Undo", "+Slot", "Return", "Hammer", "Swap" };
        Button[] btns = new Button[5];
        TextMeshProUGUI[] limitTxts = new TextMeshProUGUI[5];

        for (int i = 0; i < 5; i++)
        {
            GameObject btnObj = new GameObject("Btn_" + pNames[i]);
            btnObj.transform.SetParent(pPanel.transform, false);
            RectTransform bRt = btnObj.AddComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.5f);
            bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector3(80f + i * 130f, 0f, 0f);
            bRt.sizeDelta = new Vector2(112f, 112f);
            if (i == 1)
            {
                bRt.anchoredPosition = hudLayout.extraSlotBoosterPosition;
                bRt.sizeDelta = hudLayout.extraSlotBoosterSize;
            }

            Image img = btnObj.AddComponent<Image>();
            img.sprite = i == 1 ? extraSlotBoosterSprite : boosterCircleSprite;
            img.preserveAspect = true;
            img.color = i <= 1 ? Color.clear : Color.white;
            img.raycastTarget = i > 1;

            Button b = btnObj.AddComponent<Button>();
            btns[i] = b;

            GameObject tObj = new GameObject("Txt");
            tObj.transform.SetParent(btnObj.transform, false);
            RectTransform tRt = tObj.AddComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(96f, 58f);
            TextMeshProUGUI tVal = tObj.AddComponent<TextMeshProUGUI>();
            tVal.alignment = TextAlignmentOptions.Center;
            tVal.text = pNames[i];
            tVal.fontSize = 20;
            tVal.fontWeight = FontWeight.Bold;
            tVal.color = i <= 1 ? Color.clear : Color.white;

            GameObject lObj = new GameObject("LimitTxt");
            lObj.transform.SetParent(btnObj.transform, false);
            RectTransform lRt = lObj.AddComponent<RectTransform>();
            lRt.anchoredPosition = new Vector3(0f, -34f, 0f);
            lRt.sizeDelta = new Vector2(80f, 32f);
            TextMeshProUGUI lVal = lObj.AddComponent<TextMeshProUGUI>();
            lVal.alignment = TextAlignmentOptions.Center;
            lVal.text = "1x";
            lVal.fontSize = 18;
            lVal.color = i <= 1 ? Color.clear : new Color(1f, 1f, 1f, 0.75f);
            limitTxts[i] = lVal;
        }

        // Link buttons to Game Manager listeners
        gm.btnUndo = btns[0];
        gm.btnExtraSlot = btns[1];
        gm.btnKickback = btns[2];
        gm.btnHammer = btns[3];
        gm.btnSwap = btns[4];

        gm.limitUndoText = limitTxts[0];
        gm.limitSlotText = limitTxts[1];
        gm.limitReturnText = limitTxts[2];
        gm.limitHammerText = limitTxts[3];
        gm.limitSwapText = limitTxts[4];

        btns[0].onClick.AddListener(gm.ClickUndo);
        btns[1].onClick.AddListener(gm.ClickExtraSlot);
        btns[2].onClick.AddListener(gm.ClickKickback);
        btns[3].onClick.AddListener(gm.ClickHammer);
        btns[4].onClick.AddListener(gm.ClickSwap);

        // Bottom controls: parking spot, tray slots, and reload button.
        GameObject bottomSection = new GameObject("TrayParkSection");
        bottomSection.transform.SetParent(footer.transform, false);
        RectTransform bsRt = bottomSection.AddComponent<RectTransform>();
        bsRt.anchorMin = new Vector2(0f, 0f);
        bsRt.anchorMax = new Vector2(1f, 0f);
        bsRt.anchoredPosition = new Vector3(0f, 80f, 0f);
        bsRt.sizeDelta = new Vector2(0f, 200f);

        // Left Side: PARK Spot
        GameObject park = new GameObject("ParkSpot");
        park.transform.SetParent(bottomSection.transform, false);
        RectTransform pkRt = park.AddComponent<RectTransform>();
        pkRt.anchorMin = new Vector2(0f, 0.5f);
        pkRt.anchorMax = new Vector2(0f, 0.5f);
        pkRt.anchoredPosition = new Vector3(120f, 0f, 0f);
        pkRt.sizeDelta = new Vector2(160f, 160f);

        Image pkImg = park.AddComponent<Image>();
        pkImg.sprite = parkButtonBg;
        pkImg.color = new Color(0.96f, 0.62f, 0.04f, 0.05f);
        gm.imgParkingSlot = pkImg;

        Button pkBtn = park.AddComponent<Button>();
        pkBtn.onClick.AddListener(gm.ClickParkingSlot);

        // Middle: TRAY slots (supports up to 4 slots dynamically)
        GameObject tray = new GameObject("TraySpot");
        tray.transform.SetParent(bottomSection.transform, false);
        RectTransform trRt = tray.AddComponent<RectTransform>();
        trRt.anchorMin = new Vector2(0.5f, 0.5f);
        trRt.anchorMax = new Vector2(0.5f, 0.5f);
        trRt.anchoredPosition = new Vector3(0f, 0f, 0f);
        trRt.sizeDelta = new Vector2(560f, 160f);
        Image trayArt = tray.AddComponent<Image>();
        trayArt.sprite = colorSlotsSprite;
        trayArt.preserveAspect = true;
        trayArt.color = Color.clear;
        trayArt.raycastTarget = false;

        GameObject trLabel = new GameObject("Label");
        trLabel.transform.SetParent(tray.transform, false);
        RectTransform trlRt = trLabel.AddComponent<RectTransform>();
        trlRt.anchoredPosition = new Vector3(0f, 100f, 0f);
        trlRt.sizeDelta = new Vector2(400f, 50f);
        TextMeshProUGUI trlT = trLabel.AddComponent<TextMeshProUGUI>();
        trlT.alignment = TextAlignmentOptions.Center;
        trlT.text = "TRAY";
        trlT.fontSize = 28;
        trlT.fontWeight = FontWeight.Bold;
        trlT.color = Color.clear;

        GameObject trLabelSub = new GameObject("Sub");
        trLabelSub.transform.SetParent(tray.transform, false);
        RectTransform trlsRt = trLabelSub.AddComponent<RectTransform>();
        trlsRt.anchoredPosition = new Vector3(0f, -100f, 0f);
        trlsRt.sizeDelta = new Vector2(400f, 50f);
        TextMeshProUGUI trlsT = trLabelSub.AddComponent<TextMeshProUGUI>();
        trlsT.alignment = TextAlignmentOptions.Center;
        trlsT.text = "tap to park";
        trlsT.fontSize = 24;
        trlsT.color = Color.clear;

        // UI representation for tray slots
        Image[] uiSlots = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = new GameObject("Slot_" + i);
            slotObj.transform.SetParent(tray.transform, false);
            RectTransform sRt = slotObj.AddComponent<Image>().rectTransform;
            Image slotImage = slotObj.GetComponent<Image>();
            slotImage.sprite = slotBg;
            slotImage.type = Image.Type.Sliced;
            slotImage.color = i < 3 ? Color.clear : new Color(0.04f, 0.66f, 0.93f, 0.6f);
            
            // Set size
            sRt.sizeDelta = new Vector2(132f, 120f);
            sRt.anchoredPosition = new Vector3(-180f + i * 180f, 0f, 0f);

            Button slotBtn = slotObj.AddComponent<Button>();
            int idx = i;
            slotBtn.onClick.AddListener(() => gm.ClickTrayBlock(idx));

            uiSlots[i] = slotObj.GetComponent<Image>();
        }
        gm.imgTraySlots = uiSlots;

        // Right Side: NEW (Shuffle) button
        GameObject reload = new GameObject("NewSpot");
        reload.transform.SetParent(bottomSection.transform, false);
        RectTransform rlRt = reload.AddComponent<RectTransform>();
        rlRt.anchorMin = new Vector2(1f, 0.5f);
        rlRt.anchorMax = new Vector2(1f, 0.5f);
        rlRt.anchoredPosition = new Vector3(-120f, 0f, 0f);
        rlRt.sizeDelta = new Vector2(160f, 160f);

        Image rlImg = reload.AddComponent<Image>();
        rlImg.sprite = slotBg;
        rlImg.type = Image.Type.Sliced;
        rlImg.color = new Color(1f, 1f, 1f, 0.05f);

        Button rlBtn = reload.AddComponent<Button>();
        rlBtn.onClick.AddListener(() => gm.LoadLevel(gm.currentLevelIndex));
        gm.btnShuffle = rlBtn;

        // Very Bottom Help text
        GameObject help = new GameObject("HelpText");
        help.transform.SetParent(footer.transform, false);
        RectTransform hpRt = help.AddComponent<RectTransform>();
        hpRt.anchorMin = new Vector2(0.5f, 0f);
        hpRt.anchorMax = new Vector2(0.5f, 0f);
        hpRt.anchoredPosition = new Vector3(0f, -140f, 0f);
        hpRt.sizeDelta = new Vector2(980f, 80f);
        TextMeshProUGUI hT = help.AddComponent<TextMeshProUGUI>();
        hT.alignment = TextAlignmentOptions.Center;
        hT.text = "Tap glowing blocks to remove. Fill tray with 3 same color to clear.\nEach power-up has 1 use per level.";
        hT.fontSize = 30;
        hT.color = new Color(0.3f, 0.35f, 0.45f);

        gm.settingsPanel = CreateSettingsPanel(container, hudLayout, gm);
        gm.startMenuPanel = CreateStartMenuPanel(container, hudLayout, gm);

        ApplyHudFontToAllText(canvasObj, hudLayout.hudFont);
    }

    private GameObject CreateStartMenuPanel(GameObject parent, ColorSortHudLayout hudLayout, UnityGameManager gm)
    {
        Sprite menuBackgroundSprite = LoadUISprite("bg", CreateSolidSprite(32, 32, new Color(0.02f, 0.28f, 0.52f)));
        Sprite playSprite = LoadUISprite("settings_resume_button", CreateRoundedRectSprite(640, 190, 36, new Color(0.32f, 0.88f, 0.08f)));
        Sprite shopNormalSprite = LoadUISprite("menu_shop_normal", CreateRoundedRectSprite(430, 300, 12, new Color(0.07f, 0.58f, 0.92f)));
        Sprite shopTallSprite = LoadUISprite("menu_shop_tall", shopNormalSprite);
        Sprite homeNormalSprite = LoadUISprite("menu_home_normal", CreateRoundedRectSprite(430, 300, 12, new Color(0.07f, 0.58f, 0.92f)));
        Sprite homeTallSprite = LoadUISprite("menu_home_tall", homeNormalSprite);
        Sprite lockedNormalSprite = LoadUISprite("menu_locked_normal", CreateRoundedRectSprite(430, 300, 12, new Color(0.07f, 0.58f, 0.92f)));
        Sprite lockedTallSprite = LoadUISprite("menu_locked_tall", lockedNormalSprite);

        GameObject menu = new GameObject("StartMenuPanel");
        menu.transform.SetParent(parent.transform, false);

        RectTransform menuRt = menu.AddComponent<RectTransform>();
        menuRt.anchorMin = Vector2.zero;
        menuRt.anchorMax = Vector2.one;
        menuRt.sizeDelta = Vector2.zero;

        Image background = menu.AddComponent<Image>();
        background.sprite = menuBackgroundSprite;
        background.type = Image.Type.Sliced;
        background.color = Color.white;

        GameObject homePage = new GameObject("MainMenuHomePage");
        homePage.transform.SetParent(menu.transform, false);
        RectTransform homePageRt = homePage.AddComponent<RectTransform>();
        homePageRt.anchorMin = Vector2.zero;
        homePageRt.anchorMax = Vector2.one;
        homePageRt.sizeDelta = Vector2.zero;

        CreateSettingsButton(homePage.transform, "MainMenuPlay", hudLayout.mainMenuPlayText, playSprite, hudLayout.mainMenuPlayPosition, hudLayout.mainMenuPlaySize, hudLayout.mainMenuPlayFontSize, gm.ClickStartFromMenu, hudLayout);

        GameObject shopPage = CreateEmptyMenuPage(menu.transform, "MainMenuShopPage");
        GameObject lockedPage = CreateEmptyMenuPage(menu.transform, "MainMenuLockedPage");

        GameObject tabsRoot = new GameObject("MainMenuTabs");
        tabsRoot.transform.SetParent(menu.transform, false);
        RectTransform tabsRootRt = tabsRoot.AddComponent<RectTransform>();
        tabsRootRt.anchorMin = Vector2.zero;
        tabsRootRt.anchorMax = Vector2.one;
        tabsRootRt.sizeDelta = Vector2.zero;

        Image shopImage = CreateMenuTab(tabsRoot.transform, "MainMenuShopTab", "Shop", shopNormalSprite, hudLayout.mainMenuShopTabPosition, hudLayout.mainMenuTabSize, hudLayout);
        Image homeImage = CreateMenuTab(tabsRoot.transform, "MainMenuHomeTab", "Home", homeNormalSprite, hudLayout.mainMenuHomeTabPosition, hudLayout.mainMenuTabSize, hudLayout);
        Image lockedImage = CreateMenuTab(tabsRoot.transform, "MainMenuLockedTab", "Locked", lockedNormalSprite, hudLayout.mainMenuLockedTabPosition, hudLayout.mainMenuTabSize, hudLayout);

        void SelectTab(string tab)
        {
            bool shopSelected = tab == "Shop";
            bool homeSelected = tab == "Home";
            bool lockedSelected = tab == "Locked";

            homePage.SetActive(homeSelected);
            shopPage.SetActive(shopSelected);
            lockedPage.SetActive(lockedSelected);

            ApplyMenuTabState(shopImage, shopSelected, shopNormalSprite, shopTallSprite, hudLayout.mainMenuShopTabPosition, hudLayout);
            ApplyMenuTabState(homeImage, homeSelected, homeNormalSprite, homeTallSprite, hudLayout.mainMenuHomeTabPosition, hudLayout);
            ApplyMenuTabState(lockedImage, lockedSelected, lockedNormalSprite, lockedTallSprite, hudLayout.mainMenuLockedTabPosition, hudLayout);
        }

        shopImage.GetComponent<Button>().onClick.AddListener(() => SelectTab("Shop"));
        homeImage.GetComponent<Button>().onClick.AddListener(() => SelectTab("Home"));
        lockedImage.GetComponent<Button>().onClick.AddListener(() => SelectTab("Locked"));
        SelectTab("Home");

        return menu;
    }

    private GameObject CreateEmptyMenuPage(Transform parent, string name)
    {
        GameObject page = new GameObject(name);
        page.transform.SetParent(parent, false);
        RectTransform rt = page.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return page;
    }

    private Image CreateMenuTab(Transform parent, string name, string label, Sprite sprite, Vector2 position, Vector2 size, ColorSortHudLayout hudLayout)
    {
        GameObject tab = new GameObject(name);
        tab.transform.SetParent(parent, false);
        RectTransform rt = tab.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image image = tab.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        Button button = tab.AddComponent<Button>();
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI text = CreatePanelText(tab.transform, name + "Label", label, hudLayout.mainMenuSelectedLabelOffset, hudLayout.mainMenuSelectedLabelSize, hudLayout.mainMenuSelectedLabelFontSize, hudLayout);
        text.fontWeight = FontWeight.Heavy;
        text.gameObject.SetActive(false);

        return image;
    }

    private void ApplyMenuTabState(Image tabImage, bool selected, Sprite normalSprite, Sprite tallSprite, Vector2 basePosition, ColorSortHudLayout hudLayout)
    {
        if (tabImage == null) return;

        tabImage.sprite = selected ? tallSprite : normalSprite;
        RectTransform rt = tabImage.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = basePosition + (selected ? hudLayout.mainMenuSelectedTabOffset : Vector2.zero);
            rt.sizeDelta = selected ? hudLayout.mainMenuTallTabSize : hudLayout.mainMenuTabSize;
        }

        if (selected)
        {
            tabImage.transform.SetAsLastSibling();
        }

        Transform label = tabImage.transform.Find(tabImage.name + "Label");
        if (label != null)
        {
            label.gameObject.SetActive(selected);
        }
    }

    private GameObject CreateSettingsPanel(GameObject parent, ColorSortHudLayout hudLayout, UnityGameManager gm)
    {
        Sprite settingsTraySprite = LoadUISprite("settings_tray", CreateRoundedRectSprite(620, 760, 48, new Color(0.05f, 0.63f, 0.93f)));
        Sprite soundIconSprite = LoadUISprite("settings_sound_icon", CreateSolidSprite(32, 32, Color.clear));
        Sprite vibrationIconSprite = LoadUISprite("settings_vibration_icon", CreateSolidSprite(32, 32, Color.clear));
        Sprite musicIconSprite = LoadUISprite("settings_music_icon", CreateSolidSprite(32, 32, Color.clear));
        Sprite closeSprite = LoadUISprite("settings_close_button", CreateCircularSprite(96, new Color(0.96f, 0.18f, 0.2f)));
        Sprite resumeSprite = LoadUISprite("settings_resume_button", CreateRoundedRectSprite(360, 90, 22, new Color(0.32f, 0.88f, 0.08f)));
        Sprite quitSprite = LoadUISprite("settings_quit_button", CreateRoundedRectSprite(360, 90, 22, new Color(0.9f, 0.12f, 0.13f)));
        Sprite moreButtonSprite = CreateRoundedRectSprite(360, 90, 22, new Color(0.08f, 0.53f, 0.93f));

        GameObject overlay = new GameObject("SettingsOverlay");
        overlay.transform.SetParent(parent.transform, false);
        overlay.SetActive(false);

        RectTransform overlayRt = overlay.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;

        Image dim = overlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, hudLayout.settingsDimAlpha);

        GameObject panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = hudLayout.settingsPanelPosition;
        panelRt.sizeDelta = hudLayout.settingsPanelSize;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.sprite = settingsTraySprite;
        panelImg.preserveAspect = true;

        TextMeshProUGUI title = CreatePanelText(panel.transform, "SettingsTitle", hudLayout.settingsTitleText, hudLayout.settingsTitlePosition, hudLayout.settingsTitleSize, hudLayout.settingsTitleFontSize, hudLayout);
        title.fontWeight = FontWeight.Heavy;

        CreateSettingsIcon(panel.transform, "HapticsIcon", vibrationIconSprite, hudLayout.settingsHapticsIconPosition, hudLayout.settingsHapticsIconSize);
        CreateSettingsIcon(panel.transform, "SoundsIcon", soundIconSprite, hudLayout.settingsSoundsIconPosition, hudLayout.settingsSoundsIconSize);
        CreateSettingsIcon(panel.transform, "MusicIcon", musicIconSprite, hudLayout.settingsMusicIconPosition, hudLayout.settingsMusicIconSize);

        CreatePanelText(panel.transform, "HapticsLabel", hudLayout.settingsHapticsText, hudLayout.settingsHapticsTextPosition, hudLayout.settingsHapticsTextSize, hudLayout.settingsHapticsFontSize, hudLayout).alignment = TextAlignmentOptions.Left;
        CreatePanelText(panel.transform, "SoundsLabel", hudLayout.settingsSoundsText, hudLayout.settingsSoundsTextPosition, hudLayout.settingsSoundsTextSize, hudLayout.settingsSoundsFontSize, hudLayout).alignment = TextAlignmentOptions.Left;
        CreatePanelText(panel.transform, "MusicLabel", hudLayout.settingsMusicText, hudLayout.settingsMusicTextPosition, hudLayout.settingsMusicTextSize, hudLayout.settingsMusicFontSize, hudLayout).alignment = TextAlignmentOptions.Left;

        CreateSettingsToggle(panel.transform, "HapticsToggle", hudLayout.settingsHapticsTogglePosition, () => hapticsEnabled, value => hapticsEnabled = value, hudLayout);
        CreateSettingsToggle(panel.transform, "SoundsToggle", hudLayout.settingsSoundsTogglePosition, () => soundsEnabled, value => soundsEnabled = value, hudLayout);
        CreateSettingsToggle(panel.transform, "MusicToggle", hudLayout.settingsMusicTogglePosition, () => musicEnabled, value => musicEnabled = value, hudLayout);

        CreateSettingsButton(panel.transform, "Resume", hudLayout.settingsResumeText, resumeSprite, hudLayout.settingsResumePosition, hudLayout.settingsResumeSize, hudLayout.settingsResumeFontSize, () => overlay.SetActive(false), hudLayout);
        GameObject quitConfirmation = CreateQuitConfirmationPanel(
            overlay.transform, settingsTraySprite, resumeSprite, quitSprite, hudLayout, gm, overlay);

        CreateSettingsButton(panel.transform, "Quit", hudLayout.settingsQuitText, quitSprite, hudLayout.settingsQuitPosition, hudLayout.settingsQuitSize, hudLayout.settingsQuitFontSize, () =>
        {
            quitConfirmation.SetActive(true);
        }, hudLayout);

        GameObject morePage = CreateSettingsMorePanel(overlay.transform, settingsTraySprite, moreButtonSprite, resumeSprite, closeSprite, hudLayout);
        CreateSettingsButton(panel.transform, "More", hudLayout.settingsMoreText, moreButtonSprite, hudLayout.settingsMorePosition, hudLayout.settingsMoreSize, hudLayout.settingsMoreFontSize, () =>
        {
            panel.SetActive(false);
            morePage.SetActive(true);
        }, hudLayout);

        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(panel.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0.5f);
        closeRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = hudLayout.settingsClosePosition;
        closeRt.sizeDelta = hudLayout.settingsCloseSize;
        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.sprite = closeSprite;
        closeImg.preserveAspect = true;
        Button closeButton = closeObj.AddComponent<Button>();
        closeButton.onClick.AddListener(() => overlay.SetActive(false));

        return overlay;
    }

    private GameObject CreateQuitConfirmationPanel(Transform parent, Sprite panelSprite, Sprite cancelSprite, Sprite confirmSprite,
        ColorSortHudLayout hudLayout, UnityGameManager gm, GameObject settingsOverlay)
    {
        GameObject modal = new GameObject("QuitConfirmation");
        modal.transform.SetParent(parent, false);
        RectTransform modalRt = modal.AddComponent<RectTransform>();
        modalRt.anchorMin = Vector2.zero;
        modalRt.anchorMax = Vector2.one;
        modalRt.sizeDelta = Vector2.zero;

        Image dim = modal.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.22f);

        GameObject panel = new GameObject("QuitConfirmationPanel");
        panel.transform.SetParent(modal.transform, false);
        RectTransform panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, 20f);
        panelRt.sizeDelta = new Vector2(650f, 470f);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.preserveAspect = true;

        TextMeshProUGUI title = CreatePanelText(panel.transform, "QuitTitle", "LEAVE GAME?",
            new Vector2(0f, 145f), new Vector2(560f, 70f), 52f, hudLayout);
        title.fontWeight = FontWeight.Heavy;

        TextMeshProUGUI message = CreatePanelText(panel.transform, "QuitMessage",
            "Do you want to leave?\nYour hearts will be lost.",
            new Vector2(0f, 45f), new Vector2(560f, 120f), 34f, hudLayout);
        message.alignment = TextAlignmentOptions.Center;

        CreateSettingsButton(panel.transform, "Stay", "STAY", cancelSprite,
            new Vector2(-145f, -125f), new Vector2(250f, 82f), 34f,
            () => modal.SetActive(false), hudLayout);

        CreateSettingsButton(panel.transform, "Leave", "LEAVE", confirmSprite,
            new Vector2(145f, -125f), new Vector2(250f, 82f), 34f,
            () =>
            {
                modal.SetActive(false);
                settingsOverlay.SetActive(false);
                gm.ClickQuitToMenu();
            }, hudLayout);

        modal.SetActive(false);
        return modal;
    }

    private GameObject CreateSettingsMorePanel(Transform parent, Sprite panelSprite, Sprite buttonSprite, Sprite termsPrivacyButtonSprite, Sprite closeSprite, ColorSortHudLayout hudLayout)
    {
        GameObject panel = new GameObject("SettingsMorePanel");
        panel.transform.SetParent(parent, false);
        panel.SetActive(false);
        RectTransform panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = hudLayout.morePanelPosition;
        panelRt.sizeDelta = hudLayout.morePanelSize;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.preserveAspect = true;

        TextMeshProUGUI title = CreatePanelText(panel.transform, "MoreTitle", hudLayout.moreTitleText, hudLayout.moreTitlePosition, hudLayout.moreTitleSize, hudLayout.moreTitleFontSize, hudLayout);
        title.fontWeight = FontWeight.Heavy;

        CreateSettingsButton(panel.transform, "Terms", hudLayout.termsButtonText, termsPrivacyButtonSprite, hudLayout.termsButtonPosition, hudLayout.termsButtonSize, hudLayout.termsButtonFontSize, () => OpenUrlIfPresent(hudLayout.termsUrl, "Terms URL is empty."), hudLayout);
        CreateSettingsButton(panel.transform, "Privacy", hudLayout.privacyButtonText, termsPrivacyButtonSprite, hudLayout.privacyButtonPosition, hudLayout.privacyButtonSize, hudLayout.privacyButtonFontSize, () => OpenUrlIfPresent(hudLayout.privacyUrl, "Privacy URL is empty."), hudLayout);
        CreateSettingsButton(panel.transform, "MoreBack", hudLayout.moreBackButtonText, buttonSprite, hudLayout.moreBackButtonPosition, hudLayout.moreBackButtonSize, hudLayout.moreBackButtonFontSize, () =>
        {
            panel.SetActive(false);
            Transform settingsPanel = parent.Find("SettingsPanel");
            if (settingsPanel != null)
            {
                settingsPanel.gameObject.SetActive(true);
            }
        }, hudLayout);

        GameObject closeObj = new GameObject("MoreCloseButton");
        closeObj.transform.SetParent(panel.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0.5f);
        closeRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = hudLayout.moreClosePosition;
        closeRt.sizeDelta = hudLayout.moreCloseSize;
        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.sprite = closeSprite;
        closeImg.preserveAspect = true;
        Button closeButton = closeObj.AddComponent<Button>();
        closeButton.onClick.AddListener(() =>
        {
            GameObject overlay = parent.gameObject;
            overlay.SetActive(false);
            panel.SetActive(false);
            Transform settingsPanel = parent.Find("SettingsPanel");
            if (settingsPanel != null)
            {
                settingsPanel.gameObject.SetActive(true);
            }
        });

        return panel;
    }

    private void OpenUrlIfPresent(string url, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.Log(emptyMessage);
            return;
        }

        Application.OpenURL(url);
    }

    private void CreateSettingsIcon(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject iconObj = new GameObject(name);
        iconObj.transform.SetParent(parent, false);
        RectTransform rt = iconObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private TextMeshProUGUI CreatePanelText(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, ColorSortHudLayout hudLayout)
    {
        return CreatePanelText(parent, text.Replace(":", ""), text, position, size, fontSize, hudLayout);
    }

    private TextMeshProUGUI CreatePanelText(Transform parent, string objectName, string text, Vector2 position, Vector2 size, float fontSize, ColorSortHudLayout hudLayout)
    {
        GameObject textObj = new GameObject(objectName);
        textObj.transform.SetParent(parent, false);
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.CenterGeoAligned;
        tmp.fontSize = fontSize;
        tmp.fontWeight = FontWeight.Bold;
        tmp.color = Color.white;
        ApplyHudFont(tmp, hudLayout.hudFont);
        return tmp;
    }

    private void CreateSettingsButton(Transform parent, string name, string label, Sprite sprite, Vector2 position, Vector2 size, float fontSize, UnityEngine.Events.UnityAction onClick, ColorSortHudLayout hudLayout)
    {
        GameObject buttonObj = new GameObject(name + "Button");
        buttonObj.transform.SetParent(parent, false);
        RectTransform rt = buttonObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image img = buttonObj.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;

        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreatePanelText(buttonObj.transform, name + "ButtonText", label, Vector2.zero, size, fontSize, hudLayout);
        text.fontWeight = FontWeight.Heavy;
    }

    private void CreateSettingsToggle(Transform parent, string name, Vector2 position, System.Func<bool> getValue, System.Action<bool> setValue, ColorSortHudLayout hudLayout)
    {
        GameObject toggleObj = new GameObject(name);
        toggleObj.transform.SetParent(parent, false);
        RectTransform rt = toggleObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = hudLayout.settingsToggleSize;

        Image bg = toggleObj.AddComponent<Image>();
        bg.type = Image.Type.Sliced;

        GameObject knobObj = new GameObject(name + "Knob");
        knobObj.transform.SetParent(toggleObj.transform, false);
        RectTransform knobRt = knobObj.AddComponent<RectTransform>();
        knobRt.anchorMin = new Vector2(0.5f, 0.5f);
        knobRt.anchorMax = new Vector2(0.5f, 0.5f);
        knobRt.sizeDelta = hudLayout.settingsToggleKnobSize;
        Image knob = knobObj.AddComponent<Image>();
        knob.sprite = CreateCircularSprite(96, Color.white);
        knob.preserveAspect = true;
        knob.raycastTarget = false;

        TextMeshProUGUI stateText = CreatePanelText(toggleObj.transform, name + "Text", "", Vector2.zero, hudLayout.settingsToggleTextSize, hudLayout.settingsToggleFontSize, hudLayout);
        stateText.raycastTarget = false;

        GameObject hitObj = new GameObject(name + "HitArea");
        hitObj.transform.SetParent(toggleObj.transform, false);
        RectTransform hitRt = hitObj.AddComponent<RectTransform>();
        hitRt.anchorMin = new Vector2(0.5f, 0.5f);
        hitRt.anchorMax = new Vector2(0.5f, 0.5f);
        hitRt.anchoredPosition = Vector2.zero;
        hitRt.sizeDelta = hudLayout.settingsToggleSize;
        Image hitImage = hitObj.AddComponent<Image>();
        hitImage.sprite = CreateSolidSprite(4, 4, Color.white);
        hitImage.color = Color.clear;
        hitImage.raycastTarget = true;
        Button button = hitObj.AddComponent<Button>();
        button.targetGraphic = hitImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            setValue(!getValue());
            UpdateToggleVisual(bg, knobRt, stateText, getValue(), hudLayout);
        });

        UpdateToggleVisual(bg, knobRt, stateText, getValue(), hudLayout);
    }

    private void UpdateToggleVisual(Image bg, RectTransform knobRt, TextMeshProUGUI stateText, bool isOn, ColorSortHudLayout hudLayout)
    {
        int width = Mathf.Max(8, Mathf.RoundToInt(hudLayout.settingsToggleSize.x));
        int height = Mathf.Max(8, Mathf.RoundToInt(hudLayout.settingsToggleSize.y));
        bg.sprite = CreateRoundedRectSprite(width, height, Mathf.RoundToInt(height * 0.5f), isOn ? new Color(0.16f, 0.78f, 0.18f) : new Color(0.55f, 0.55f, 0.55f));
        bg.type = Image.Type.Sliced;
        knobRt.anchoredPosition = new Vector2(isOn ? -hudLayout.settingsToggleKnobOffset : hudLayout.settingsToggleKnobOffset, 0f);
        stateText.text = isOn ? "ON" : "OFF";
        stateText.alignment = isOn ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        stateText.color = isOn ? new Color(0.02f, 0.42f, 0.08f) : new Color(0.38f, 0.38f, 0.38f);
    }

    void LateUpdate()
    {
        ForcePortraitOrientation();
        EnsureGeneratedSceneUpright();
    }

    private static void ForcePortraitOrientation()
    {
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;

        if (Screen.orientation != ScreenOrientation.Portrait)
        {
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }

    private static void EnsureGeneratedSceneUpright()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.rotation = Quaternion.identity;
            cam.transform.localScale = Vector3.one;
        }

        ResetTransformIfFound("UICanvas");
        ResetTransformIfFound("UIContainer");
    }

    private static void ResetTransformIfFound(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null) return;

        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObj.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObj.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void ApplyHudFont(TextMeshProUGUI text, TMP_FontAsset font)
    {
        if (text == null) return;

        TMP_FontAsset usableFont = GetUsableHudFont(font);
        if (usableFont != null)
        {
            text.font = usableFont;
        }
    }

    private static void ApplyHudFontToAllText(GameObject root, TMP_FontAsset font)
    {
        if (root == null) return;

        TMP_FontAsset usableFont = GetUsableHudFont(font);
        if (usableFont == null) return;

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            text.font = usableFont;
        }
    }

    private static TMP_FontAsset GetUsableHudFont(TMP_FontAsset font)
    {
        if (font != null && font.material != null) return font;
        return TMP_Settings.defaultFontAsset;
    }

    private Sprite LoadArrowSprite()
    {
        Texture2D arrowTexture = Resources.Load<Texture2D>("arrow");
        if (arrowTexture == null)
        {
            return CreateFallbackArrowSprite();
        }

        arrowTexture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(
            arrowTexture,
            new Rect(0, 0, arrowTexture.width, arrowTexture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(arrowTexture.width, arrowTexture.height)
        );
    }

    private Sprite LoadBackgroundSprite()
    {
        Texture2D bgTexture = Resources.Load<Texture2D>("bg");
        if (bgTexture == null)
        {
            return CreateSolidSprite(32, 32, new Color(0.02f, 0.28f, 0.52f));
        }

        bgTexture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(
            bgTexture,
            new Rect(0, 0, bgTexture.width, bgTexture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(bgTexture.width, bgTexture.height)
        );
    }

    private Sprite LoadUISprite(string resourceName, Sprite fallback)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourceName);
        if (texture == null)
        {
            return fallback;
        }

        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(texture.width, texture.height)
        );
    }

    private Sprite LoadBoardTraySprite()
    {
        Texture2D trayTexture = Resources.Load<Texture2D>("tray");
        if (trayTexture == null)
        {
            return CreateRoundedRectSprite(256, 256, 32, new Color(0.12f, 0.54f, 0.86f));
        }

        trayTexture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(
            trayTexture,
            new Rect(0, 0, trayTexture.width, trayTexture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(trayTexture.width, trayTexture.height)
        );
    }

    private void CreateWorldBackground(Sprite backgroundSprite, Camera cam)
    {
        if (backgroundSprite == null) return;

        GameObject bgObj = new GameObject("WorldBackground");
        SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = backgroundSprite;
        bgRenderer.sortingOrder = -20;

        bgObj.transform.position = new Vector3(0f, cam.transform.position.y, 0.2f);
        Vector2 spriteSize = bgRenderer.sprite.bounds.size;
        float worldHeight = cam.orthographicSize * 2f;
        float worldWidth = worldHeight * 9f / 16f;
        float scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y);
        bgObj.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private Sprite CreateSolidSprite(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateHeartSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float formula = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * Mathf.Pow(ny, 3f);
                pixels[y * size + x] = formula <= 0f ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateFallbackArrowSprite()
    {
        Texture2D tex = new Texture2D(96, 64, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[96 * 64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        for (int y = 22; y < 42; y++)
        {
            for (int x = 12; x < 58; x++)
            {
                pixels[y * 96 + x] = Color.white;
            }
        }

        for (int y = 8; y < 56; y++)
        {
            int halfHeight = Mathf.Abs(y - 32);
            int startX = 58;
            int endX = 86 - halfHeight;
            for (int x = startX; x < endX; x++)
            {
                pixels[y * 96 + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 96, 64), new Vector2(0.5f, 0.5f), 96f);
    }

    // Procedural Rounded Rectangle Texture Sprites
    private Sprite CreateRoundedRectSprite(int width, int height, int cornerRadius, Color color)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isInside = true;
                // Corner check Top-Left
                if (x < cornerRadius && y > height - 1 - cornerRadius)
                {
                    float dx = cornerRadius - x;
                    float dy = y - (height - 1 - cornerRadius);
                    if (dx * dx + dy * dy > cornerRadius * cornerRadius) isInside = false;
                }
                // Corner check Top-Right
                else if (x > width - 1 - cornerRadius && y > height - 1 - cornerRadius)
                {
                    float dx = x - (width - 1 - cornerRadius);
                    float dy = y - (height - 1 - cornerRadius);
                    if (dx * dx + dy * dy > cornerRadius * cornerRadius) isInside = false;
                }
                // Corner check Bottom-Left
                else if (x < cornerRadius && y < cornerRadius)
                {
                    float dx = cornerRadius - x;
                    float dy = cornerRadius - y;
                    if (dx * dx + dy * dy > cornerRadius * cornerRadius) isInside = false;
                }
                // Corner check Bottom-Right
                else if (x > width - 1 - cornerRadius && y < cornerRadius)
                {
                    float dx = x - (width - 1 - cornerRadius);
                    float dy = cornerRadius - y;
                    if (dx * dx + dy * dy > cornerRadius * cornerRadius) isInside = false;
                }

                pixels[y * width + x] = isInside ? color : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    // Procedural Circular Texture Sprites
    private Sprite CreateCircularSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float radius = size / 2.0f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius;
                float dy = y - radius;
                pixels[y * size + x] = (dx * dx + dy * dy <= radius * radius) ? color : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
