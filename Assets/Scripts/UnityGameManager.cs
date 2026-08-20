using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UnityGameManager : MonoBehaviour
{
    public enum BlockColor { Red, Green, Blue, Yellow, Neutral }
    public enum Direction { Up, Down, Left, Right }

    [System.Serializable]
    public struct BlockData
    {
        public int row;
        public int col;
        public BlockColor color;
        public Direction direction;
        public string textLabel;
    }

    [System.Serializable]
    public class LevelConfig
    {
        public int id;
        public string name;
        public int boardSize = 3;
        public int matchTarget = 3;
        public List<BlockData> blocks;
    }

    public class UnityArrowBlock : MonoBehaviour
    {
        public string id;
        public int row;
        public int col;
        public int originalRow;
        public int originalCol;
        public BlockColor color;
        public Direction direction;
        public string textLabel;

        public bool isFlying = false;
        public Vector3 flyDirVec;
        public float flySpeed = 16f;
        public int boardSize = 3;
        public float boardSpacing = 1.5f;
        public float boardCenterX = 0f;
        public float boardCenterY = 0.5f;
        public float boardScale = 1f;

        private Vector3 startPos;
        private bool isShaking = false;
        private float shakeDuration = 0.18f;
        private float shakeTimer = 0f;
        private float shakeMagnitude = 0.06f;
        private bool tapAnimating = false;
        private float tapTimer = 0f;
        private const float TapDuration = 0.12f;
        private float baseVisualScale = 1f;
        private float idlePulseOffset;

        void Start()
        {
            startPos = transform.position;
            idlePulseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (isFlying)
            {
                transform.Translate(flyDirVec * flySpeed * Time.deltaTime, Space.World);
            }
            else if (isShaking)
            {
                shakeTimer += Time.deltaTime;
                if (shakeTimer >= shakeDuration)
                {
                    isShaking = false;
                    transform.position = startPos;
                }
                else
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * shakeMagnitude;
                    transform.position = startPos + offset;
                }
            }
            else if (tapAnimating)
            {
                tapTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(tapTimer / TapDuration);
                float bump = Mathf.Sin(progress * Mathf.PI) * 0.045f;
                transform.localScale = Vector3.one * (baseVisualScale * (1f + bump));
                if (progress >= 1f)
                {
                    tapAnimating = false;
                    transform.localScale = Vector3.one * baseVisualScale;
                }
            }
            else
            {
                float pulse = 1f + Mathf.Sin((Time.time * 2.2f) + idlePulseOffset) * 0.012f;
                transform.localScale = Vector3.one * (baseVisualScale * pulse);
            }
        }

        public void StartShake()
        {
            if (isFlying) return;
            // Use the position currently shown on screen. Tray and parking blocks
            // no longer jump back to their original board cell after a failed tap.
            startPos = transform.position;
            transform.position = startPos;
            shakeTimer = 0f;
            tapAnimating = false;
            isShaking = true;
        }

        public void PlayTapFeedback()
        {
            if (isFlying || isShaking) return;
            tapTimer = 0f;
            tapAnimating = true;
        }

        public void SetBaseVisualScale(float scale)
        {
            baseVisualScale = scale;
            transform.localScale = Vector3.one * scale;
        }

        public void ResetPosition()
        {
            isFlying = false;
            isShaking = false;
            tapAnimating = false;
            SetBaseVisualScale(boardScale);
            transform.position = GetGridPosition();
            startPos = transform.position;
            var col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = true;
        }

        private Vector3 GetGridPosition()
        {
            return new Vector3(boardCenterX + (col - (boardSize - 1) * 0.5f) * boardSpacing, boardCenterY + ((boardSize - 1) * 0.5f - row) * boardSpacing, 0f);
        }
    }

    [Header("Game Configuration")]
    public List<LevelConfig> levels = new List<LevelConfig>();
    public int currentLevelIndex = 0;
    public int redCleared = 0;
    public int greenCleared = 0;
    public int blueCleared = 0;
    public int yellowCleared = 0;
    public int heartsRemaining = 3;

    [Header("UI Component Hooks")]
    public TextMeshProUGUI boardText;
    public TextMeshProUGUI redProgressText;
    public TextMeshProUGUI greenProgressText;
    public TextMeshProUGUI blueProgressText;
    public Image blueProgressDot;
    public TextMeshProUGUI yellowProgressText;
    public Image yellowProgressDot;
    public Image[] heartImages;
    
    public Button btnUndo;
    public Button btnExtraSlot;
    public Button btnKickback;
    public Button btnHammer;
    public Button btnSwap;

    public TextMeshProUGUI limitUndoText;
    public TextMeshProUGUI limitSlotText;
    public TextMeshProUGUI limitReturnText;
    public TextMeshProUGUI limitHammerText;
    public TextMeshProUGUI limitSwapText;

    public Image imgParkingSlot;
    public Image[] imgTraySlots;
    public Button btnShuffle;
    public GameObject settingsPanel;
    public GameObject startMenuPanel;

    [Header("Assets Generated")]
    public Sprite spriteBlockRed;
    public Sprite spriteBlockGreen;
    public Sprite spriteBlockBlue;
    public Sprite spriteBlockYellow;
    public Sprite spriteBlockNeutral;
    public Sprite spriteArrow;
    public Sprite spriteBoardTray;
    public Sprite spriteColorSlotsTray;
    public Sprite spriteColorSlotsTray4;
    public Sprite spriteColorSlotsTray5;
    public Sprite spriteParkSlot;
    public Sprite spriteExtraSlotBooster;
    public Sprite spriteUndoBooster;
    public Sprite spritePauseButton;
    public TMP_FontAsset gameFont;
    
    // Internal States
    private UnityArrowBlock[,] grid = new UnityArrowBlock[3, 3];
    private List<UnityArrowBlock> trayBlocks = new List<UnityArrowBlock>();
    private UnityArrowBlock parkedBlock = null;
    private GameObject boardTrayObject = null;
    private GameObject colorTrayObject = null;
    private GameObject parkingSlotObject = null;
    private GameObject extraSlotBoosterObject = null;
    private GameObject undoBoosterObject = null;
    private GameObject pauseButtonObject = null;
    private GameObject debugNextBoardButtonObject = null;
    private Sprite debugNextBoardButtonSprite = null;
    private ColorSortHudLayout layout = null;
    private int maxTraySlots = 3;
    private int boardSize = 3;
    private int matchTarget = 3;
    private int currentBlueMatchTarget = 0;
    private int currentYellowMatchTarget = 0;
    private bool blueGoalActive = false;
    private bool yellowGoalActive = false;
    private bool levelEnded = false;
    private bool isAnimatingMove = false;
    private bool isAnimatingClear = false;
    private const int InitialGeneratedLevelCount = 60;
    private const string LevelDatabaseResourceName = "ColorSortLevelDatabase";
    private const int StartingHearts = 3;
    private const int FirstBlueBoardId = 17;
    private const int ThreeBlueBoardId = 26;
    private const int FourBlueBoardId = 36;
    private const int FirstYellowBoardId = 46;
    private const int ThreeYellowBoardId = 49;
    private const int FourYellowBoardId = 50;
    private const int EarlyBlueMatchTarget = 2;
    private const int MiddleBlueMatchTarget = 3;
    private const int LateBlueMatchTarget = 4;
    private const int EarlyYellowMatchTarget = 2;
    private const int MiddleYellowMatchTarget = 3;
    private const int LateYellowMatchTarget = 4;
    private const float ValidMoveAnimationDuration = 0.2f;
    private const float MatchClearAnimationDuration = 0.28f;
    private const float NeutralClearAnimationDuration = 0.14f;
    private const float LevelIntroDuration = 0.26f;
    private const float LevelIntroStagger = 0.025f;
    private const float BoosterAnimationDuration = 0.24f;
    private const float BoardClearPulseDuration = 0.34f;

    // Power-up Limit trackers
    private Dictionary<string, bool> powerupsUsed = new Dictionary<string, bool>()
    {
        { "undo", false },
        { "extraslot", false },
        { "kickback", false },
        { "hammer", false },
        { "swap", false }
    };

    // Mode Flags
    private bool hammerActive = false;
    private bool swapActive = false;
    private int? selectedSwapIndex = null;
    private bool kickbackActive = false;

    // Undo Snapshots Stack
    private struct GameStateSnapshot
    {
        public BlockData[] gridBlocks;
        public BlockData[] trayBlocks;
        public BlockData? parkedBlock;
        public int redCleared;
        public int greenCleared;
        public int blueCleared;
        public int yellowCleared;
        public int heartsRemaining;
        public int boardSize;
        public int matchTarget;
        public int blueMatchTarget;
        public int yellowMatchTarget;
        public bool blueGoalActive;
        public bool yellowGoalActive;
        public bool undoUsed;
        public bool extraslotUsed;
        public bool kickbackUsed;
        public bool hammerUsed;
        public bool swapUsed;
        public int maxTraySlots;
    }
    private Stack<GameStateSnapshot> undoStack = new Stack<GameStateSnapshot>();

    void Start()
    {
        DefineLevels();
        LoadLevel(currentLevelIndex);
        ShowStartMenu();
    }

    void Update()
    {
        bool pressed = false;
        Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressed = true;
            screenPos = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pressed = true;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            pressed = true;
            screenPos = Input.mousePosition;
        }
#endif

        if (pressed)
        {
            if (settingsPanel != null && settingsPanel.activeSelf) return;
            if (startMenuPanel != null && startMenuPanel.activeSelf) return;
            if (isAnimatingMove || isAnimatingClear) return;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPos);
            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);
            if (hit.collider != null)
            {
                UnityArrowBlock block = hit.collider.GetComponent<UnityArrowBlock>();
                if (debugNextBoardButtonObject != null && hit.collider.gameObject == debugNextBoardButtonObject)
                {
                    ClickDebugNextBoard();
                }
                else if (levelEnded)
                {
                    return;
                }
                else if (extraSlotBoosterObject != null && hit.collider.gameObject == extraSlotBoosterObject)
                {
                    ClickExtraSlot();
                }
                else if (undoBoosterObject != null && hit.collider.gameObject == undoBoosterObject)
                {
                    ClickUndo();
                }
                else if (pauseButtonObject != null && hit.collider.gameObject == pauseButtonObject)
                {
                    ToggleSettingsPanel();
                }
                else if (block != null && !block.isFlying)
                {
                    int trayIndex = trayBlocks.IndexOf(block);
                    if (trayIndex >= 0)
                    {
                        ClickTrayBlock(trayIndex);
                    }
                    else if (parkedBlock == block)
                    {
                        ClickParkingSlot();
                    }
                    else
                    {
                        OnBlockTapped(block);
                    }
                }
            }
        }
    }

    private void DefineLevels()
    {
        ColorSortLevelDatabase database = Resources.Load<ColorSortLevelDatabase>(LevelDatabaseResourceName);
        if (database != null && database.levels != null && database.levels.Count > 0)
        {
            levels = CloneLevelList(database.levels);
            Debug.Log($"Loaded {levels.Count} fixed Color Sort levels from Resources/{LevelDatabaseResourceName}.");
        }
        else
        {
            Debug.LogWarning($"Missing Resources/{LevelDatabaseResourceName}; generating levels at runtime. Use Color Sort > Regenerate Fixed Level Database to make Play Mode fast.");
            levels = ColorSortLevelBuilder.BuildLevels(InitialGeneratedLevelCount);
        }

        ValidateLevelColorCounts();
    }

    private LevelConfig BuildStableSolvableLevel(int boardIndex)
    {
        List<LevelConfig> generatedLevels = ColorSortLevelBuilder.BuildLevels(boardIndex + 1);
        return generatedLevels[boardIndex];
    }

    private void AddDesignedLevel(int id, int size, string[] colorRows, string[] directionRows)
    {
        levels.Add(CreateDesignedLevel(id, size, colorRows, directionRows));
    }

    private LevelConfig CreateDesignedLevel(int id, int size, string[] colorRows, string[] directionRows)
    {
        var level = new LevelConfig
        {
            id = id,
            name = "Board " + id,
            boardSize = size,
            matchTarget = size == 3 ? 3 : 4,
            blocks = new List<BlockData>()
        };

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                BlockColor color = ColorFromCode(colorRows[r][c]);
                level.blocks.Add(new BlockData
                {
                    row = r,
                    col = c,
                    color = color,
                    direction = DirectionFromCode(directionRows[r][c]),
                    textLabel = LabelForColor(color)
                });
            }
        }

        return level;
    }

    private BlockColor ColorFromCode(char code)
    {
        if (code == 'R') return BlockColor.Red;
        if (code == 'G') return BlockColor.Green;
        if (code == 'B') return BlockColor.Blue;
        if (code == 'Y') return BlockColor.Yellow;
        return BlockColor.Neutral;
    }

    private Direction DirectionFromCode(char code)
    {
        if (code == 'D') return Direction.Down;
        if (code == 'L') return Direction.Left;
        if (code == 'R') return Direction.Right;
        return Direction.Up;
    }

    private string LabelForColor(BlockColor color)
    {
        if (color == BlockColor.Red) return "RED";
        if (color == BlockColor.Green) return "GREEN";
        if (color == BlockColor.Blue) return "BLUE";
        if (color == BlockColor.Yellow) return "YELLOW";
        return "BLOCK";
    }

    private void ValidateLevelColorCounts()
    {
        foreach (var level in levels)
        {
            int red = 0;
            int green = 0;
            int blue = 0;
            int yellow = 0;
            int neutral = 0;

            foreach (var block in level.blocks)
            {
                if (block.color == BlockColor.Red) red++;
                else if (block.color == BlockColor.Green) green++;
                else if (block.color == BlockColor.Blue) blue++;
                else if (block.color == BlockColor.Yellow) yellow++;
                else if (block.color == BlockColor.Neutral) neutral++;
            }

            int expectedRed = level.matchTarget;
            int expectedGreen = expectedRed;
            int expectedBlue = GetExpectedBlueCount(level.id);
            int expectedYellow = GetExpectedYellowCount(level.id);
            int expectedNeutral = level.boardSize * level.boardSize - expectedRed - expectedGreen - expectedBlue - expectedYellow;

            bool valid = red == expectedRed &&
                         green == expectedGreen &&
                         blue == expectedBlue &&
                         yellow == expectedYellow &&
                         neutral == expectedNeutral;

            if (!valid)
            {
                Debug.LogWarning($"{level.name} has unusual counts: red={red}, green={green}, blue={blue}, yellow={yellow}, neutral={neutral}.");
            }
        }
    }

    private int GetExpectedBlueCount(int boardId)
    {
        if (boardId >= FourBlueBoardId) return LateBlueMatchTarget;
        if (boardId >= ThreeBlueBoardId) return MiddleBlueMatchTarget;
        if (boardId >= FirstBlueBoardId) return EarlyBlueMatchTarget;
        return 0;
    }

    private int GetExpectedYellowCount(int boardId)
    {
        if (boardId >= FourYellowBoardId) return LateYellowMatchTarget;
        if (boardId >= ThreeYellowBoardId) return MiddleYellowMatchTarget;
        if (boardId >= FirstYellowBoardId) return EarlyYellowMatchTarget;
        return 0;
    }

    public void LoadLevel(int levelIdx)
    {
        if (levels.Count == 0) return;

        StopAllCoroutines();
        isAnimatingMove = false;
        isAnimatingClear = false;
        currentLevelIndex = Mathf.Max(0, levelIdx);
        LevelConfig level = BuildEndlessLevel(currentLevelIndex);
        boardSize = level.boardSize;
        matchTarget = level.matchTarget;
        blueGoalActive = ContainsColor(level.blocks, BlockColor.Blue);
        currentBlueMatchTarget = blueGoalActive ? CountColor(level.blocks, BlockColor.Blue) : 0;
        yellowGoalActive = ContainsColor(level.blocks, BlockColor.Yellow);
        currentYellowMatchTarget = yellowGoalActive ? CountColor(level.blocks, BlockColor.Yellow) : 0;
        List<BlockData> displayBlocks = new List<BlockData>(level.blocks);
        redCleared = 0;
        greenCleared = 0;
        blueCleared = 0;
        yellowCleared = 0;
        heartsRemaining = StartingHearts;
        maxTraySlots = matchTarget;
        levelEnded = false;

        // Reset powers
        powerupsUsed["undo"] = false;
        powerupsUsed["extraslot"] = false;
        powerupsUsed["kickback"] = false;
        powerupsUsed["hammer"] = false;
        powerupsUsed["swap"] = false;

        DeactivateActivePowerups();
        undoStack.Clear();

        // Clear existing block GameObjects
        foreach (var block in FindObjectsByType<UnityArrowBlock>(FindObjectsSortMode.None))
        {
            Destroy(block.gameObject);
        }
        trayBlocks.Clear();
        parkedBlock = null;

        // Build grid Array
        grid = new UnityArrowBlock[boardSize, boardSize];
        var spawnedBlocks = new List<UnityArrowBlock>();
        EnsureBoardTray();
        EnsureColorTray();
        EnsureParkingSlot();
        EnsureExtraSlotBooster();
        EnsureUndoBooster();
        EnsurePauseButton();
        EnsureDebugNextBoardButton();

        // Spawn blocks programmatically
        foreach (var blockData in displayBlocks)
        {
            GameObject blockObj = new GameObject("Block_" + blockData.color + "_" + blockData.row + "_" + blockData.col);
            blockObj.transform.position = GetBoardPosition(blockData.row, blockData.col);
            blockObj.transform.localScale = Vector3.one * GetBoardBlockScale();
            
            // Add SpriteRenderer
            SpriteRenderer sr = blockObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetBlockSprite(blockData.color);
            sr.sortingOrder = 5;

            // BoxCollider2D for touch clicks
            BoxCollider2D col = blockObj.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * GetBoardColliderSize();

            // Setup custom component
            UnityArrowBlock ab = blockObj.AddComponent<UnityArrowBlock>();
            ab.id = System.Guid.NewGuid().ToString();
            ab.row = blockData.row;
            ab.col = blockData.col;
            ab.originalRow = blockData.row;
            ab.originalCol = blockData.col;
            ab.color = blockData.color;
            ab.direction = blockData.direction;
            ab.textLabel = blockData.textLabel;
            ab.boardSize = boardSize;
            ab.boardSpacing = GetBoardSpacing();
            ab.boardCenterX = GetBoardCenterX();
            ab.boardCenterY = GetBoardCenterY();
            ab.boardScale = GetBoardBlockScale();
            ab.SetBaseVisualScale(GetBoardBlockScale());

            AddBlockArtwork(blockObj, blockData.direction, blockData.color, blockData.textLabel);

            grid[blockData.row, blockData.col] = ab;
            spawnedBlocks.Add(ab);
        }

        // Re-scale tray visual slots in UI
        UpdateTrayCapacityUI();
        UpdateUI();
        StartCoroutine(PlayLevelIntroAnimation(spawnedBlocks));
    }

    private System.Collections.IEnumerator PlayLevelIntroAnimation(List<UnityArrowBlock> blocks)
    {
        if (blocks == null || blocks.Count == 0) yield break;

        isAnimatingMove = true;
        foreach (UnityArrowBlock block in blocks)
        {
            if (block == null) continue;
            block.isFlying = true;
            block.transform.localScale = Vector3.one * (GetBoardBlockScale() * 0.88f);
        }

        float totalDuration = LevelIntroDuration + Mathf.Max(0, blocks.Count - 1) * LevelIntroStagger;
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < blocks.Count; i++)
            {
                UnityArrowBlock block = blocks[i];
                if (block == null) continue;

                float blockProgress = Mathf.Clamp01((elapsed - i * LevelIntroStagger) / LevelIntroDuration);
                float eased = Mathf.SmoothStep(0f, 1f, blockProgress);
                float scale = Mathf.Lerp(0.88f, 1f, eased);
                block.transform.localScale = Vector3.one * (GetBoardBlockScale() * scale);
            }

            yield return null;
        }

        foreach (UnityArrowBlock block in blocks)
        {
            if (block == null) continue;
            block.isFlying = false;
            block.SetBaseVisualScale(GetBoardBlockScale());
        }

        isAnimatingMove = false;
    }

    private bool ContainsColor(List<BlockData> blocks, BlockColor color)
    {
        foreach (BlockData block in blocks)
        {
            if (block.color == color) return true;
        }

        return false;
    }

    private int CountColor(List<BlockData> blocks, BlockColor color)
    {
        int count = 0;
        foreach (BlockData block in blocks)
        {
            if (block.color == color) count++;
        }

        return count;
    }

    private Sprite GetBlockSprite(BlockColor color)
    {
        if (color == BlockColor.Red) return spriteBlockRed;
        if (color == BlockColor.Green) return spriteBlockGreen;
        if (color == BlockColor.Blue) return spriteBlockBlue != null ? spriteBlockBlue : spriteBlockGreen;
        if (color == BlockColor.Yellow) return spriteBlockYellow != null ? spriteBlockYellow : spriteBlockGreen;
        return spriteBlockNeutral;
    }

    private Vector3 GetBoardPosition(int row, int col)
    {
        float spacing = GetBoardSpacing();
        return new Vector3(GetBoardCenterX() + (col - (boardSize - 1) * 0.5f) * spacing, GetBoardCenterY() + ((boardSize - 1) * 0.5f - row) * spacing, 0f);
    }

    private ColorSortHudLayout GetLayout()
    {
        if (layout == null)
        {
            layout = ColorSortHudLayout.LoadOrDefault();
        }

        return layout;
    }

    private float GetBoardSpacing()
    {
        ColorSortHudLayout hud = GetLayout();
        return boardSize >= 4 ? hud.boardSpacing4 : hud.boardSpacing3;
    }

    private float GetBoardBlockScale()
    {
        ColorSortHudLayout hud = GetLayout();
        return boardSize >= 4 ? hud.boardBlockScale4 : hud.boardBlockScale3;
    }

    private float GetBoardColliderSize()
    {
        ColorSortHudLayout hud = GetLayout();
        return boardSize >= 4 ? hud.boardColliderSize4 : hud.boardColliderSize3;
    }

    private float GetBoardCenterY()
    {
        return GetLayout().boardCenterY;
    }

    private float GetBoardCenterX()
    {
        return GetLayout().boardCenterX;
    }

    private LevelConfig BuildEndlessLevel(int boardIndex)
    {
        EnsureGeneratedLevelsThrough(boardIndex);
        return CloneLevel(levels[boardIndex]);
    }

    private void EnsureGeneratedLevelsThrough(int boardIndex)
    {
        if (boardIndex < levels.Count) return;

        int targetCount = Mathf.Max(boardIndex + 1, levels.Count + 32);
        levels = ColorSortLevelBuilder.BuildLevels(targetCount);
        ValidateLevelColorCounts();
    }

    private LevelConfig CloneLevel(LevelConfig source)
    {
        return new LevelConfig
        {
            id = source.id,
            name = source.name,
            boardSize = source.boardSize,
            matchTarget = source.matchTarget,
            blocks = new List<BlockData>(source.blocks)
        };
    }

    private List<LevelConfig> CloneLevelList(List<LevelConfig> sourceLevels)
    {
        var clonedLevels = new List<LevelConfig>(sourceLevels.Count);
        foreach (LevelConfig level in sourceLevels)
        {
            clonedLevels.Add(CloneLevel(level));
        }

        return clonedLevels;
    }

    private LevelConfig BuildDeterministicAdvancedLevel(int boardIndex)
    {
        return BuildStableSolvableLevel(boardIndex);
    }

    private void EnsureBoardTray()
    {
        if (spriteBoardTray == null) return;

        if (boardTrayObject == null)
        {
            boardTrayObject = new GameObject("BoardTray_3x3");
            SpriteRenderer trayRenderer = boardTrayObject.AddComponent<SpriteRenderer>();
            trayRenderer.sortingOrder = 2;
        }

        SpriteRenderer sr = boardTrayObject.GetComponent<SpriteRenderer>();
        sr.sprite = spriteBoardTray;

        ColorSortHudLayout hud = GetLayout();
        boardTrayObject.transform.position = new Vector3(hud.boardTrayPosition.x, hud.boardTrayPosition.y, 0.08f);
        Vector2 spriteSize = sr.sprite.bounds.size;
        float targetSize = boardSize >= 4 ? hud.boardTraySize4 : hud.boardTraySize3;
        float scale = targetSize / Mathf.Max(spriteSize.x, spriteSize.y);
        boardTrayObject.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void EnsureColorTray()
    {
        Sprite activeTraySprite = GetActiveColorTraySprite();
        if (activeTraySprite == null) return;

        if (colorTrayObject == null)
        {
            colorTrayObject = new GameObject("ColorTray_3Slots");
            SpriteRenderer trayRenderer = colorTrayObject.AddComponent<SpriteRenderer>();
            trayRenderer.sortingOrder = 3;
        }

        SpriteRenderer sr = colorTrayObject.GetComponent<SpriteRenderer>();
        sr.sprite = activeTraySprite;

        ColorSortHudLayout hud = GetLayout();
        Vector2 trayPosition = GetColorTrayPosition();
        colorTrayObject.transform.position = new Vector3(trayPosition.x, trayPosition.y, 0.06f);
        Vector2 spriteSize = sr.sprite.bounds.size;
        float targetWidth = GetColorTrayWidth();
        float scale = targetWidth / spriteSize.x;
        colorTrayObject.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private Sprite GetActiveColorTraySprite()
    {
        if (maxTraySlots >= 5 && spriteColorSlotsTray5 != null) return spriteColorSlotsTray5;
        if (maxTraySlots >= 4 && spriteColorSlotsTray4 != null) return spriteColorSlotsTray4;
        return spriteColorSlotsTray;
    }

    private void EnsureParkingSlot()
    {
        if (spriteParkSlot == null) return;

        if (parkingSlotObject == null)
        {
            parkingSlotObject = new GameObject("ParkingSlot");
            SpriteRenderer slotRenderer = parkingSlotObject.AddComponent<SpriteRenderer>();
            slotRenderer.sortingOrder = 3;
        }

        SpriteRenderer sr = parkingSlotObject.GetComponent<SpriteRenderer>();
        sr.sprite = spriteParkSlot;
        parkingSlotObject.transform.position = GetParkingSlotPosition();

        Vector2 spriteSize = sr.sprite.bounds.size;
        ColorSortHudLayout hud = GetLayout();
        float targetSize = maxTraySlots >= 4 ? hud.parkingSize4 : hud.parkingSize3;
        float scale = targetSize / Mathf.Max(spriteSize.x, spriteSize.y);
        parkingSlotObject.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void EnsureExtraSlotBooster()
    {
        if (spriteExtraSlotBooster == null) return;

        if (extraSlotBoosterObject == null)
        {
            extraSlotBoosterObject = new GameObject("ExtraSlotBooster");
            SpriteRenderer boosterRenderer = extraSlotBoosterObject.AddComponent<SpriteRenderer>();
            boosterRenderer.sortingOrder = 20;
            CircleCollider2D collider = extraSlotBoosterObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
        }

        SpriteRenderer sr = extraSlotBoosterObject.GetComponent<SpriteRenderer>();
        sr.sprite = spriteExtraSlotBooster;
        sr.color = powerupsUsed["extraslot"] || maxTraySlots >= 5 ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        extraSlotBoosterObject.transform.position = GetExtraSlotBoosterPosition();
        extraSlotBoosterObject.transform.localScale = GetExtraSlotBoosterScale();
        extraSlotBoosterObject.SetActive(!levelEnded);
    }

    private void EnsureUndoBooster()
    {
        if (spriteUndoBooster == null) return;

        if (undoBoosterObject == null)
        {
            undoBoosterObject = new GameObject("UndoBooster");
            SpriteRenderer boosterRenderer = undoBoosterObject.AddComponent<SpriteRenderer>();
            boosterRenderer.sortingOrder = 20;
            CircleCollider2D collider = undoBoosterObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
        }

        SpriteRenderer sr = undoBoosterObject.GetComponent<SpriteRenderer>();
        sr.sprite = spriteUndoBooster;
        sr.color = powerupsUsed["undo"] || undoStack.Count == 0 ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        undoBoosterObject.transform.position = GetUndoBoosterPosition();
        undoBoosterObject.transform.localScale = GetUndoBoosterScale();
        undoBoosterObject.SetActive(!levelEnded);
    }

    private void EnsurePauseButton()
    {
        if (spritePauseButton == null) return;

        if (pauseButtonObject == null)
        {
            pauseButtonObject = new GameObject("PauseButton");
            SpriteRenderer pauseRenderer = pauseButtonObject.AddComponent<SpriteRenderer>();
            pauseRenderer.sortingOrder = 20;
            CircleCollider2D collider = pauseButtonObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
        }

        SpriteRenderer sr = pauseButtonObject.GetComponent<SpriteRenderer>();
        sr.sprite = spritePauseButton;
        sr.color = Color.white;
        pauseButtonObject.transform.position = GetPauseButtonPosition();
        pauseButtonObject.transform.localScale = GetPauseButtonScale();
        pauseButtonObject.SetActive(!levelEnded);
    }

    private void EnsureDebugNextBoardButton()
    {
        ColorSortHudLayout hud = GetLayout();
        if (!hud.showDebugNextBoardButton)
        {
            if (debugNextBoardButtonObject != null) debugNextBoardButtonObject.SetActive(false);
            return;
        }

        if (debugNextBoardButtonObject == null)
        {
            debugNextBoardButtonObject = new GameObject("DebugNextBoardButton");
            BoxCollider2D collider = debugNextBoardButtonObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            GameObject backObj = new GameObject("DebugNextBoardButtonBack");
            backObj.transform.SetParent(debugNextBoardButtonObject.transform);
            backObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            SpriteRenderer backRenderer = backObj.AddComponent<SpriteRenderer>();
            backRenderer.sortingOrder = 35;

            GameObject labelObj = new GameObject("DebugNextBoardButtonLabel");
            labelObj.transform.SetParent(debugNextBoardButtonObject.transform);
            labelObj.transform.localPosition = new Vector3(0f, -0.03f, -0.03f);
            TextMeshPro label = labelObj.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.sortingOrder = 36;
        }

        debugNextBoardButtonObject.transform.position = GetDebugNextBoardButtonPosition();

        BoxCollider2D box = debugNextBoardButtonObject.GetComponent<BoxCollider2D>();
        if (box != null) box.size = GetLayout().debugNextBoardButtonSize;

        Transform back = debugNextBoardButtonObject.transform.Find("DebugNextBoardButtonBack");
        if (back != null)
        {
            SpriteRenderer sr = back.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = GetDebugNextBoardButtonSprite();
                Vector2 spriteSize = sr.sprite.bounds.size;
                Vector2 targetSize = GetLayout().debugNextBoardButtonSize;
                back.localScale = new Vector3(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y, 1f);
            }
        }

        Transform labelTransform = debugNextBoardButtonObject.transform.Find("DebugNextBoardButtonLabel");
        if (labelTransform != null)
        {
            TextMeshPro label = labelTransform.GetComponent<TextMeshPro>();
            if (label != null)
            {
                string text = string.IsNullOrWhiteSpace(hud.debugNextBoardButtonText) ? "NEXT" : hud.debugNextBoardButtonText;
                label.text = text;
                label.fontSize = Mathf.Max(0.65f, hud.debugNextBoardButtonSize.y * 2.05f);
                ApplyGameFont(label, hud.hudFont);
            }
        }

        debugNextBoardButtonObject.SetActive(true);
    }

    private Sprite GetDebugNextBoardButtonSprite()
    {
        if (debugNextBoardButtonSprite != null) return debugNextBoardButtonSprite;

        const int width = 120;
        const int height = 60;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color fill = new Color(0.04f, 0.63f, 0.98f, 0.92f);
        Color border = new Color(0.02f, 0.12f, 0.48f, 0.96f);
        Color highlight = new Color(0.36f, 0.88f, 1f, 0.95f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool edge = x < 5 || x >= width - 5 || y < 5 || y >= height - 5;
                bool topHighlight = !edge && y > height - 14;
                texture.SetPixel(x, y, edge ? border : topHighlight ? highlight : fill);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        debugNextBoardButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        return debugNextBoardButtonSprite;
    }

    private Vector3 GetParkingSlotPosition()
    {
        Vector2 parkingPosition = maxTraySlots >= 4 ? GetLayout().parkingPosition4 : GetLayout().parkingPosition3;
        return new Vector3(parkingPosition.x, parkingPosition.y, 0.04f);
    }

    private Vector3 GetExtraSlotBoosterPosition()
    {
        Vector2 boosterPosition = GetLayout().extraSlotBoosterPosition;
        return new Vector3(boosterPosition.x, boosterPosition.y, 0f);
    }

    private Vector3 GetExtraSlotBoosterScale()
    {
        Vector2 boosterSize = GetLayout().extraSlotBoosterSize;
        return new Vector3(boosterSize.x, boosterSize.y, 1f);
    }

    private Vector3 GetUndoBoosterPosition()
    {
        Vector2 boosterPosition = GetLayout().undoBoosterPosition;
        return new Vector3(boosterPosition.x, boosterPosition.y, 0f);
    }

    private Vector3 GetUndoBoosterScale()
    {
        Vector2 boosterSize = GetLayout().undoBoosterSize;
        return new Vector3(boosterSize.x, boosterSize.y, 1f);
    }

    private Vector3 GetPauseButtonPosition()
    {
        Vector2 buttonPosition = GetLayout().pauseButtonPosition;
        return new Vector3(buttonPosition.x, buttonPosition.y, 0f);
    }

    private Vector3 GetPauseButtonScale()
    {
        Vector2 buttonSize = GetLayout().pauseButtonSize;
        return new Vector3(buttonSize.x, buttonSize.y, 1f);
    }

    private Vector3 GetDebugNextBoardButtonPosition()
    {
        Vector2 buttonPosition = GetLayout().debugNextBoardButtonPosition;
        return new Vector3(buttonPosition.x, buttonPosition.y, 0f);
    }

    private Vector2 GetColorTrayPosition()
    {
        ColorSortHudLayout hud = GetLayout();
        if (maxTraySlots >= 5) return hud.colorTrayPosition5;
        return maxTraySlots >= 4 ? hud.colorTrayPosition4 : hud.colorTrayPosition3;
    }

    private float GetColorTrayWidth()
    {
        ColorSortHudLayout hud = GetLayout();
        if (maxTraySlots >= 5) return hud.colorTrayWidth5;
        return maxTraySlots >= 4 ? hud.colorTrayWidth4 : hud.colorTrayWidth3;
    }

    private void UpdateTrayCapacityUI()
    {
        for (int i = 0; i < imgTraySlots.Length; i++)
        {
            imgTraySlots[i].gameObject.SetActive(i < maxTraySlots);
        }
    }

    private void AddBlockArtwork(GameObject blockObj, Direction direction, BlockColor color, string label)
    {
        GameObject arrowObj = new GameObject("ArrowImage");
        arrowObj.transform.SetParent(blockObj.transform);
        arrowObj.transform.localPosition = new Vector3(0f, 0.13f, -0.02f);
        arrowObj.transform.localScale = new Vector3(0.52f, 0.46f, 1f);
        SetBlockArrowDirection(blockObj, direction);

        SpriteRenderer arrowRenderer = arrowObj.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = spriteArrow;
        arrowRenderer.sortingOrder = 6;
        arrowRenderer.color = color == BlockColor.Neutral ? new Color(1f, 1f, 1f, 0.45f) : Color.white;

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(blockObj.transform);
        textObj.transform.localPosition = new Vector3(0f, -0.37f, -0.03f);

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 2.3f;
        tmp.fontStyle = FontStyles.Bold;
        ApplyGameFont(tmp, GetLayout().hudFont);
        tmp.characterSpacing = 7f;
        tmp.color = color == BlockColor.Neutral ? new Color(1f, 1f, 1f, 0.32f) : new Color(1f, 1f, 1f, 0.7f);
        tmp.sortingOrder = 7;
    }

    private void ApplyGameFont(TextMeshPro text, TMP_FontAsset font)
    {
        if (text == null) return;

        TMP_FontAsset usableFont = GetUsableFont(font);
        if (usableFont != null)
        {
            text.font = usableFont;
        }
    }

    private TMP_FontAsset GetUsableFont(TMP_FontAsset font)
    {
        if (font != null && font.material != null) return font;
        if (gameFont != null && gameFont.material != null) return gameFont;
        return TMP_Settings.defaultFontAsset;
    }

    private float GetArrowRotation(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return 90f;
            case Direction.Down: return -90f;
            case Direction.Left: return 180f;
            case Direction.Right: return 0f;
        }
        return 0f;
    }

    private void SetBlockArrowDirection(GameObject blockObj, Direction direction)
    {
        Transform arrow = blockObj.transform.Find("ArrowImage");
        if (arrow != null)
        {
            arrow.localRotation = Quaternion.Euler(0f, 0f, GetArrowRotation(direction));
        }
    }

    private bool IsExitPathClear(int row, int col, Direction dir)
    {
        if (dir == Direction.Up)
        {
            for (int r = row - 1; r >= 0; r--)
            {
                if (grid[r, col] != null) return false;
            }
        }
        else if (dir == Direction.Down)
        {
            for (int r = row + 1; r < boardSize; r++)
            {
                if (grid[r, col] != null) return false;
            }
        }
        else if (dir == Direction.Left)
        {
            for (int c = col - 1; c >= 0; c--)
            {
                if (grid[row, c] != null) return false;
            }
        }
        else if (dir == Direction.Right)
        {
            for (int c = col + 1; c < boardSize; c++)
            {
                if (grid[row, c] != null) return false;
            }
        }
        return true;
    }

    private void OnBlockTapped(UnityArrowBlock block)
    {
        if (levelEnded || isAnimatingMove || isAnimatingClear) return;

        if (block.color != BlockColor.Neutral && trayBlocks.Count >= maxTraySlots)
        {
            Debug.Log("Tray is full. Use park, return, hammer, swap, or +Slot before removing another color block.");
            block.StartShake();
            RegisterWrongMove();
            return;
        }

        // 1. Check path blocking
        if (!IsExitPathClear(block.row, block.col, block.direction))
        {
            block.StartShake();
            RegisterWrongMove();
            return;
        }

        // Save Undo snapshot
        SaveSnapshot();

        // Remove from logical grid map immediately
        grid[block.row, block.col] = null;

        Collider2D blockCollider = block.GetComponent<Collider2D>();
        if (blockCollider != null) blockCollider.enabled = false;

        if (block.color == BlockColor.Neutral)
        {
            isAnimatingMove = true;
            block.isFlying = true;
            StartCoroutine(PlayNeutralClearAnimation(block));
            return;
        }

        // Move the color block directly from the board into its tray slot.
        isAnimatingMove = true;
        block.isFlying = true;
        SetBlockVisible(block, true);

        StartCoroutine(ProcessExitFlight(block));
    }

    private System.Collections.IEnumerator ProcessExitFlight(UnityArrowBlock block)
    {
        if (block == null)
        {
            isAnimatingMove = false;
            yield break;
        }

        Vector3 startPosition = block.transform.position;
        Vector3 trayPosition = new Vector3(GetTraySlotX(trayBlocks.Count), GetColorTrayPosition().y, 0f);
        float startScale = GetBoardBlockScale();
        float trayScale = GetTrayBlockScale();
        float elapsed = 0f;

        while (elapsed < ValidMoveAnimationDuration && block != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / ValidMoveAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            block.transform.position = Vector3.Lerp(startPosition, trayPosition, eased);
            block.transform.localScale = Vector3.one * Mathf.Lerp(startScale, trayScale, eased);
            yield return null;
        }

        if (block == null)
        {
            isAnimatingMove = false;
            yield break;
        }

        AddBlockToTray(block);
        isAnimatingMove = false;
        UpdateUI();
    }

    private System.Collections.IEnumerator PlayNeutralClearAnimation(UnityArrowBlock block)
    {
        if (block == null)
        {
            isAnimatingMove = false;
            yield break;
        }

        Vector3 startScale = block.transform.localScale;
        float elapsed = 0f;
        while (elapsed < NeutralClearAnimationDuration && block != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / NeutralClearAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            block.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            SetBlockAlpha(block, 1f - eased);
            yield return null;
        }

        if (block != null) Destroy(block.gameObject);
        isAnimatingMove = false;
        CheckGameEndConditions();
        UpdateUI();
    }

    private void AddBlockToTray(UnityArrowBlock block)
    {
        if (trayBlocks.Count >= maxTraySlots)
        {
            Debug.Log("Tray is full. Block was not added.");
            return;
        }

        block.isFlying = false;
        trayBlocks.Add(block);
        SetBlockVisible(block, true);
        block.SetBaseVisualScale(GetTrayBlockScale());
        Collider2D col = block.GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        // Reposition block to slot location
        RepositionTrayBlocksVisual();

        // Check for 3-match matches
        CheckMatchesInTray();
    }

    private void SetBlockVisible(UnityArrowBlock block, bool visible)
    {
        if (block == null) return;

        foreach (SpriteRenderer renderer in block.GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.enabled = visible;
        }

        foreach (TextMeshPro text in block.GetComponentsInChildren<TextMeshPro>())
        {
            text.enabled = visible;
        }
    }

    private void SetBlockAlpha(UnityArrowBlock block, float alpha)
    {
        if (block == null) return;

        foreach (SpriteRenderer renderer in block.GetComponentsInChildren<SpriteRenderer>())
        {
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        foreach (TextMeshPro text in block.GetComponentsInChildren<TextMeshPro>())
        {
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }
    }

    private void CheckMatchesInTray()
    {
        if (isAnimatingClear) return;

        List<int> redIdx = new List<int>();
        List<int> greenIdx = new List<int>();
        List<int> yellowIdx = new List<int>();
        List<int> blueMatchIdx = FindAdjacentBlueMatchIndices();

        for (int i = 0; i < trayBlocks.Count; i++)
        {
            if (trayBlocks[i].color == BlockColor.Red) redIdx.Add(i);
            else if (trayBlocks[i].color == BlockColor.Green) greenIdx.Add(i);
            else if (trayBlocks[i].color == BlockColor.Yellow) yellowIdx.Add(i);
        }

        if (redIdx.Count >= matchTarget)
        {
            ClearMatchedIndices(redIdx, BlockColor.Red);
        }
        else if (greenIdx.Count >= matchTarget)
        {
            ClearMatchedIndices(greenIdx, BlockColor.Green);
        }
        else if (blueGoalActive && blueMatchIdx.Count >= GetCurrentBlueMatchTarget())
        {
            ClearMatchedIndices(blueMatchIdx, BlockColor.Blue);
        }
        else if (yellowGoalActive && yellowIdx.Count >= GetCurrentYellowMatchTarget())
        {
            ClearMatchedIndices(yellowIdx, BlockColor.Yellow);
        }
        else
        {
            CheckGameEndConditions();
        }
    }

    private List<int> FindAdjacentBlueMatchIndices()
    {
        List<int> result = new List<int>();
        if (!blueGoalActive) return result;
        int target = GetCurrentBlueMatchTarget();
        if (target <= 0) return result;

        for (int i = 0; i <= trayBlocks.Count - target; i++)
        {
            bool matches = true;
            for (int offset = 0; offset < target; offset++)
            {
                if (trayBlocks[i + offset].color != BlockColor.Blue)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                for (int offset = 0; offset < target; offset++)
                {
                    result.Add(i + offset);
                }

                return result;
            }
        }

        return result;
    }

    private void ClearMatchedIndices(List<int> indices, BlockColor col)
    {
        // Remove from list back to front to avoid shifting indices issues
        indices.Sort((a, b) => b.CompareTo(a));
        int target = GetMatchTargetForColor(col);
        var matchedBlocks = new List<UnityArrowBlock>();

        foreach (int idx in indices.GetRange(0, target))
        {
            UnityArrowBlock block = trayBlocks[idx];
            matchedBlocks.Add(block);
            trayBlocks.RemoveAt(idx);
            Collider2D collider = block.GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
        }

        if (col == BlockColor.Red) redCleared = Mathf.Min(matchTarget, redCleared + target);
        else if (col == BlockColor.Green) greenCleared = Mathf.Min(matchTarget, greenCleared + target);
        else if (col == BlockColor.Blue) blueCleared = Mathf.Min(GetCurrentBlueMatchTarget(), blueCleared + target);
        else if (col == BlockColor.Yellow) yellowCleared = Mathf.Min(GetCurrentYellowMatchTarget(), yellowCleared + target);

        isAnimatingClear = true;
        RepositionTrayBlocksVisual();
        UpdateUI();

        CheckGameEndConditions();
        StartCoroutine(PlayMatchClearAnimation(matchedBlocks));
    }

    private System.Collections.IEnumerator PlayMatchClearAnimation(List<UnityArrowBlock> matchedBlocks)
    {
        float elapsed = 0f;
        var startingScales = new List<Vector3>(matchedBlocks.Count);
        foreach (UnityArrowBlock block in matchedBlocks)
        {
            startingScales.Add(block == null ? Vector3.zero : block.transform.localScale);
            if (block != null) block.isFlying = true;
        }

        while (elapsed < MatchClearAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / MatchClearAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float pop = progress < 0.22f
                ? Mathf.Lerp(1f, 1.1f, progress / 0.22f)
                : Mathf.Lerp(1.1f, 0f, (progress - 0.22f) / 0.78f);

            for (int i = 0; i < matchedBlocks.Count; i++)
            {
                UnityArrowBlock block = matchedBlocks[i];
                if (block == null) continue;

                block.transform.localScale = startingScales[i] * pop;
                SetBlockAlpha(block, 1f - eased);
            }

            yield return null;
        }

        foreach (UnityArrowBlock block in matchedBlocks)
        {
            if (block != null) Destroy(block.gameObject);
        }

        isAnimatingClear = false;
        if (!levelEnded) CheckMatchesInTray();
    }

    private int GetMatchTargetForColor(BlockColor color)
    {
        if (color == BlockColor.Blue) return GetCurrentBlueMatchTarget();
        if (color == BlockColor.Yellow) return GetCurrentYellowMatchTarget();
        return matchTarget;
    }

    private int GetCurrentBlueMatchTarget()
    {
        return Mathf.Max(0, currentBlueMatchTarget);
    }

    private int GetCurrentYellowMatchTarget()
    {
        return Mathf.Max(0, currentYellowMatchTarget);
    }

    private void RepositionTrayBlocksVisual()
    {
        for (int i = 0; i < trayBlocks.Count; i++)
        {
            float slotX = GetTraySlotX(i);

            float trayScale = GetTrayBlockScale();
            Vector2 trayPosition = GetColorTrayPosition();
            trayBlocks[i].SetBaseVisualScale(trayScale);
            trayBlocks[i].transform.position = new Vector3(slotX, trayPosition.y, 0f);
            SetBlockArrowDirection(trayBlocks[i].gameObject, Direction.Up);
            Collider2D col = trayBlocks[i].GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
    }

    private float GetTraySlotX(int index)
    {
        ColorSortHudLayout hud = GetLayout();
        if (maxTraySlots >= 5)
        {
            float[] slotCenters5 =
            {
                hud.colorTraySlotX5FirstFour.x,
                hud.colorTraySlotX5FirstFour.y,
                hud.colorTraySlotX5FirstFour.z,
                hud.colorTraySlotX5FirstFour.w,
                hud.colorTraySlotX5Last
            };
            return hud.colorTrayPosition5.x + slotCenters5[Mathf.Clamp(index, 0, slotCenters5.Length - 1)];
        }

        if (maxTraySlots >= 4)
        {
            float[] slotCenters = { hud.colorTraySlotX4.x, hud.colorTraySlotX4.y, hud.colorTraySlotX4.z, hud.colorTraySlotX4.w };
            return hud.colorTrayPosition4.x + slotCenters[Mathf.Clamp(index, 0, slotCenters.Length - 1)];
        }

        float[] threeSlotCenters = { hud.colorTraySlotX3.x, hud.colorTraySlotX3.y, hud.colorTraySlotX3.z };
        return hud.colorTrayPosition3.x + threeSlotCenters[Mathf.Clamp(index, 0, threeSlotCenters.Length - 1)];
    }

    private float GetTrayBlockScale()
    {
        ColorSortHudLayout hud = GetLayout();
        if (maxTraySlots >= 5) return hud.trayBlockScale5;
        return maxTraySlots >= 4 ? hud.trayBlockScale4 : hud.trayBlockScale3;
    }

    private float GetParkingBlockScale()
    {
        ColorSortHudLayout hud = GetLayout();
        return maxTraySlots >= 4 ? hud.parkingBlockScale4 : hud.parkingBlockScale3;
    }

    private void RepositionParkedBlockVisual()
    {
        if (parkedBlock == null) return;

        Vector3 parkPosition = GetParkingSlotPosition();
        parkedBlock.transform.position = new Vector3(parkPosition.x, parkPosition.y, 0f);
        parkedBlock.SetBaseVisualScale(GetParkingBlockScale());
        SetBlockArrowDirection(parkedBlock.gameObject, Direction.Up);

        Collider2D col = parkedBlock.GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }

    public void ApplyLayoutFromEditor()
    {
        layout = ColorSortHudLayout.LoadOrDefault();
        gameFont = GetUsableFont(layout.hudFont);

        EnsureBoardTray();
        EnsureColorTray();
        EnsureParkingSlot();
        EnsureExtraSlotBooster();
        EnsureUndoBooster();
        EnsurePauseButton();
        EnsureDebugNextBoardButton();
        ApplyFontToAllWorldText();

        for (int r = 0; r < boardSize; r++)
        {
            for (int c = 0; c < boardSize; c++)
            {
                UnityArrowBlock block = grid[r, c];
                if (block == null) continue;

                block.boardSpacing = GetBoardSpacing();
                block.boardCenterX = GetBoardCenterX();
                block.boardCenterY = GetBoardCenterY();
                block.boardScale = GetBoardBlockScale();
                block.transform.position = GetBoardPosition(r, c);
                block.SetBaseVisualScale(GetBoardBlockScale());

                Collider2D col = block.GetComponent<Collider2D>();
                if (col != null)
                {
                    if (col is BoxCollider2D box)
                    {
                        box.size = Vector2.one * GetBoardColliderSize();
                    }
                }
            }
        }

        RepositionTrayBlocksVisual();

        if (parkedBlock != null)
        {
            RepositionParkedBlockVisual();
        }

        UpdateUI();
    }

    private void ApplyFontToAllWorldText()
    {
        TMP_FontAsset usableFont = GetUsableFont(layout != null ? layout.hudFont : null);
        if (usableFont == null) return;

        TextMeshPro[] texts = FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None);
        foreach (TextMeshPro text in texts)
        {
            text.font = usableFont;
        }
    }

    private void UpdateUI()
    {
        UpdateBoardCounterUI();
        redProgressText.text = redCleared + "/" + matchTarget;
        greenProgressText.text = greenCleared + "/" + matchTarget;
        if (blueProgressText != null)
        {
            blueProgressText.text = blueCleared + "/" + GetCurrentBlueMatchTarget();
            blueProgressText.gameObject.SetActive(blueGoalActive);
        }

        if (blueProgressDot != null)
        {
            blueProgressDot.gameObject.SetActive(blueGoalActive);
        }

        if (yellowProgressText != null)
        {
            yellowProgressText.text = yellowCleared + "/" + GetCurrentYellowMatchTarget();
            yellowProgressText.gameObject.SetActive(yellowGoalActive);
        }

        if (yellowProgressDot != null)
        {
            yellowProgressDot.gameObject.SetActive(yellowGoalActive);
        }

        UpdateHeartUI();

        // Update Limits UI buttons
        limitUndoText.text = powerupsUsed["undo"] ? "0x" : "1x";
        limitSlotText.text = powerupsUsed["extraslot"] || maxTraySlots >= 5 ? "0x" : "1x";
        limitReturnText.text = powerupsUsed["kickback"] ? "0x" : "1x";
        limitHammerText.text = powerupsUsed["hammer"] ? "0x" : "1x";
        limitSwapText.text = powerupsUsed["swap"] ? "0x" : "1x";

        // Style highlights on buttons if active
        btnHammer.image.color = hammerActive ? Color.magenta : Color.white;
        btnSwap.image.color = swapActive ? Color.magenta : Color.white;
        btnKickback.image.color = kickbackActive ? Color.magenta : Color.white;

        // Position Park block visually
        if (parkedBlock != null)
        {
            RepositionParkedBlockVisual();
        }

        EnsureColorTray();
        EnsureParkingSlot();
        EnsureExtraSlotBooster();
        EnsureUndoBooster();
        EnsurePauseButton();
        EnsureDebugNextBoardButton();
    }

    private void UpdateBoardCounterUI()
    {
        if (boardText == null) return;

        string value = (currentLevelIndex + 1).ToString();
        boardText.text = value;

        ColorSortHudLayout hud = GetLayout();
        if (value.Length >= 3)
        {
            boardText.fontSize = hud.boardNumberThreeDigitFontSize;
        }
        else if (value.Length >= 2)
        {
            boardText.fontSize = hud.boardNumberTwoDigitFontSize;
        }
        else
        {
            boardText.fontSize = hud.boardNumberFontSize;
        }
    }

    private void UpdateHeartUI()
    {
        if (heartImages == null) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;

            bool full = i < heartsRemaining;
            heartImages[i].color = full ? new Color(1f, 0.12f, 0.2f, 1f) : new Color(0.05f, 0.14f, 0.28f, 0.45f);
        }
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    // Power-up: Undo
    public void ClickUndo()
    {
        if (powerupsUsed["undo"] || undoStack.Count == 0) return;

        powerupsUsed["undo"] = true;
        GameStateSnapshot snapshot = undoStack.Pop();

        RestoreSnapshot(snapshot);
        EnsureUndoBooster();
        UpdateUI();
    }

    public void ClickRetryFromStart()
    {
        LoadLevel(0);
    }

    public void ClickStartFromMenu()
    {
        if (startMenuPanel != null)
        {
            startMenuPanel.SetActive(false);
        }
    }

    public void ClickQuitToMenu()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        ShowStartMenu();
    }

    public void ShowStartMenu()
    {
        if (startMenuPanel != null)
        {
            startMenuPanel.SetActive(true);
        }
    }

    public void ClickRetryCurrentBoard()
    {
        LoadLevel(currentLevelIndex);
    }

    public void ClickDebugNextBoard()
    {
        JumpToBoardNumber(currentLevelIndex + 2);
    }

    public void JumpToBoardNumber(int boardNumber)
    {
        int targetIndex = Mathf.Max(0, boardNumber - 1);

        StopAllCoroutines();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (startMenuPanel != null) startMenuPanel.SetActive(false);

        LoadLevel(targetIndex);
    }

    // Power-up: Extra Slot
    public void ClickExtraSlot()
    {
        if (powerupsUsed["extraslot"]) return;
        if (maxTraySlots >= 5) return;

        // Extra slot is a booster state change, not a block move, so Undo should not roll it back.
        undoStack.Clear();
        powerupsUsed["extraslot"] = true;
        maxTraySlots = Mathf.Min(5, matchTarget + 1);

        UpdateTrayCapacityUI();
        EnsureColorTray();
        EnsureParkingSlot();
        EnsureExtraSlotBooster();
        EnsureUndoBooster();
        RepositionTrayBlocksVisual();
        UpdateUI();
        StartCoroutine(PlayBoosterAnimation(extraSlotBoosterObject));
    }

    // Power-up: Kickback (Toggle mode)
    public void ClickKickback()
    {
        if (powerupsUsed["kickback"]) return;

        DeactivateActivePowerups();
        kickbackActive = true;
        UpdateUI();
    }

    // Power-up: Hammer (Toggle mode)
    public void ClickHammer()
    {
        if (powerupsUsed["hammer"]) return;

        DeactivateActivePowerups();
        hammerActive = true;
        UpdateUI();
    }

    // Power-up: Swap (Toggle mode)
    public void ClickSwap()
    {
        if (powerupsUsed["swap"]) return;

        DeactivateActivePowerups();
        swapActive = true;
        UpdateUI();
    }

    // Tray Block Interaction Handler (Triggered via UI or screen raycasting click checks)
    public void ClickTrayBlock(int index)
    {
        if (isAnimatingMove || isAnimatingClear) return;
        if (index < 0 || index >= trayBlocks.Count) return;

        trayBlocks[index].PlayTapFeedback();

        // 1. Hammer power-up active
        if (hammerActive)
        {
            SaveSnapshot();
            powerupsUsed["hammer"] = true;
            
            BlockColor colorToSmash = trayBlocks[index].color;

            for (int i = trayBlocks.Count - 1; i >= 0; i--)
            {
                if (trayBlocks[i].color == colorToSmash)
                {
                    UnityArrowBlock ab = trayBlocks[i];
                    trayBlocks.RemoveAt(i);
                    Destroy(ab.gameObject);
                }
            }

            for (int r = 0; r < boardSize; r++)
            {
                for (int c = 0; c < boardSize; c++)
                {
                    if (grid[r, c] != null && grid[r, c].color == colorToSmash)
                    {
                        Destroy(grid[r, c].gameObject);
                        grid[r, c] = null;
                    }
                }
            }

            if (colorToSmash == BlockColor.Red) redCleared = matchTarget;
            if (colorToSmash == BlockColor.Green) greenCleared = matchTarget;
            if (colorToSmash == BlockColor.Blue) blueCleared = GetCurrentBlueMatchTarget();
            if (colorToSmash == BlockColor.Yellow) yellowCleared = GetCurrentYellowMatchTarget();

            DeactivateActivePowerups();
            RepositionTrayBlocksVisual();
            UpdateUI();
            CheckGameEndConditions();
            return;
        }

        // 2. Return (Kickback) power-up active
        if (kickbackActive)
        {
            UnityArrowBlock block = trayBlocks[index];
            int r = block.originalRow;
            int c = block.originalCol;

            if (grid[r, c] != null)
            {
                Debug.LogWarning("Original grid cell is occupied, cannot kickback.");
                block.StartShake();
                RegisterWrongMove();
                return;
            }

            SaveSnapshot();
            powerupsUsed["kickback"] = true;

            trayBlocks.RemoveAt(index);
            grid[r, c] = block;
            block.ResetPosition();
            SetBlockArrowDirection(block.gameObject, block.direction);

            DeactivateActivePowerups();
            RepositionTrayBlocksVisual();
            UpdateUI();
            CheckGameEndConditions();
            return;
        }

        // 3. Swap power-up active
        if (swapActive)
        {
            if (selectedSwapIndex == null)
            {
                selectedSwapIndex = index;
                // visually lift selected block slightly
                trayBlocks[index].transform.position += Vector3.up * 0.3f;
            }
            else
            {
                int firstIdx = selectedSwapIndex.Value;
                if (firstIdx != index)
                {
                    SaveSnapshot();
                    powerupsUsed["swap"] = true;

                    // Swap
                    UnityArrowBlock temp = trayBlocks[firstIdx];
                    trayBlocks[firstIdx] = trayBlocks[index];
                    trayBlocks[index] = temp;
                }
                DeactivateActivePowerups();
                RepositionTrayBlocksVisual();
                UpdateUI();
                CheckMatchesInTray();
            }
            return;
        }

        // 4. Default: Tactical Parking Spot
        if (parkedBlock == null)
        {
            SaveSnapshot();
            parkedBlock = trayBlocks[index];
            trayBlocks.RemoveAt(index);
            RepositionTrayBlocksVisual();
            RepositionParkedBlockVisual();
            UpdateUI();
            return;
        }

        SaveSnapshot();
        UnityArrowBlock trayBlock = trayBlocks[index];
        trayBlocks[index] = parkedBlock;
        parkedBlock = trayBlock;

        RepositionTrayBlocksVisual();
        RepositionParkedBlockVisual();
        UpdateUI();
        CheckMatchesInTray();
    }

    private bool IsTrayFull()
    {
        return trayBlocks.Count >= maxTraySlots;
    }

    private bool CanParkingBlockCompleteMatch()
    {
        if (parkedBlock == null || parkedBlock.color == BlockColor.Neutral) return false;

        if (parkedBlock.color == BlockColor.Blue)
        {
            if (!IsTrayFull())
            {
                return WouldAppendingBlueCompleteMatch();
            }

            return FindBlueParkingSwapIndex() >= 0;
        }

        int sameColorCount = 0;
        foreach (UnityArrowBlock block in trayBlocks)
        {
            if (block != null && block.color == parkedBlock.color)
            {
                sameColorCount++;
            }
        }

        int target = GetMatchTargetForColor(parkedBlock.color);
        return target > 0 && sameColorCount >= target - 1;
    }

    private int FindTraySwapIndexForParkingMatch()
    {
        if (!CanParkingBlockCompleteMatch()) return -1;

        if (parkedBlock != null && parkedBlock.color == BlockColor.Blue)
        {
            return FindBlueParkingSwapIndex();
        }

        for (int i = 0; i < trayBlocks.Count; i++)
        {
            if (trayBlocks[i] != null && trayBlocks[i].color != parkedBlock.color)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindBlueParkingSwapIndex()
    {
        if (parkedBlock == null || parkedBlock.color != BlockColor.Blue) return -1;
        int target = GetCurrentBlueMatchTarget();
        if (target <= 0) return -1;

        for (int i = 0; i < trayBlocks.Count; i++)
        {
            if (trayBlocks[i] == null || trayBlocks[i].color == BlockColor.Blue) continue;

            int adjacentBlueCount = 1;
            for (int left = i - 1; left >= 0 && trayBlocks[left].color == BlockColor.Blue; left--)
            {
                adjacentBlueCount++;
            }

            for (int right = i + 1; right < trayBlocks.Count && trayBlocks[right].color == BlockColor.Blue; right++)
            {
                adjacentBlueCount++;
            }

            if (adjacentBlueCount >= target) return i;
        }

        return -1;
    }

    private bool WouldAppendingBlueCompleteMatch()
    {
        int target = GetCurrentBlueMatchTarget();
        if (target <= 1) return false;

        int consecutiveBlueAtEnd = 0;
        for (int i = trayBlocks.Count - 1; i >= 0; i--)
        {
            if (trayBlocks[i] == null || trayBlocks[i].color != BlockColor.Blue) break;
            consecutiveBlueAtEnd++;
        }

        return consecutiveBlueAtEnd + 1 >= target;
    }

    private bool CompleteParkingMatchIfPossible()
    {
        if (!CanParkingBlockCompleteMatch()) return false;

        if (!IsTrayFull())
        {
            trayBlocks.Add(parkedBlock);
            parkedBlock = null;

            RepositionTrayBlocksVisual();
            UpdateUI();
            CheckMatchesInTray();
            return true;
        }

        int swapIndex = FindTraySwapIndexForParkingMatch();
        if (swapIndex < 0) return false;

        UnityArrowBlock trayBlock = trayBlocks[swapIndex];
        trayBlocks[swapIndex] = parkedBlock;
        parkedBlock = trayBlock;

        RepositionTrayBlocksVisual();
        RepositionParkedBlockVisual();
        UpdateUI();
        CheckMatchesInTray();
        return true;
    }

    // Click Parking spot to move block back to Tray
    public void ClickParkingSlot()
    {
        if (isAnimatingMove || isAnimatingClear) return;
        if (parkedBlock == null) return;

        parkedBlock.PlayTapFeedback();
        if (IsTrayFull())
        {
            int swapIndex = FindTraySwapIndexForParkingMatch();
            if (swapIndex >= 0)
            {
                SaveSnapshot();
                UnityArrowBlock trayBlock = trayBlocks[swapIndex];
                trayBlocks[swapIndex] = parkedBlock;
                parkedBlock = trayBlock;

                RepositionTrayBlocksVisual();
                RepositionParkedBlockVisual();
                UpdateUI();
                CheckMatchesInTray();
            }
            else
            {
                parkedBlock.StartShake();
                RegisterWrongMove();
            }
            return;
        }

        SaveSnapshot();
        trayBlocks.Add(parkedBlock);
        parkedBlock = null;

        RepositionTrayBlocksVisual();
        UpdateUI();
        CheckMatchesInTray();
    }

    private void RegisterWrongMove()
    {
        if (levelEnded) return;

        heartsRemaining = Mathf.Max(0, heartsRemaining - 1);
        UpdateUI();

        if (heartsRemaining <= 0)
        {
            levelEnded = true;
            Debug.Log("DEFEAT! No hearts left.");
            UpdateUI();
        }
    }

    private void DeactivateActivePowerups()
    {
        hammerActive = false;
        swapActive = false;
        selectedSwapIndex = null;
        kickbackActive = false;
    }

    // Snapshot Saving for Reverts
    private void SaveSnapshot()
    {
        var snapshot = new GameStateSnapshot();
        
        // save board grid
        var gridList = new List<BlockData>();
        for (int r = 0; r < boardSize; r++)
        {
            for (int c = 0; c < boardSize; c++)
            {
                if (grid[r, c] != null)
                {
                    gridList.Add(new BlockData
                    {
                        row = grid[r, c].row,
                        col = grid[r, c].col,
                        color = grid[r, c].color,
                        direction = grid[r, c].direction,
                        textLabel = grid[r, c].textLabel
                    });
                }
            }
        }
        snapshot.gridBlocks = gridList.ToArray();

        // save tray slots
        var trayList = new List<BlockData>();
        foreach (var b in trayBlocks)
        {
            trayList.Add(new BlockData
            {
                row = b.originalRow,
                col = b.originalCol,
                color = b.color,
                direction = b.direction,
                textLabel = b.textLabel
            });
        }
        snapshot.trayBlocks = trayList.ToArray();

        // save park spot
        if (parkedBlock != null)
        {
            snapshot.parkedBlock = new BlockData
            {
                row = parkedBlock.originalRow,
                col = parkedBlock.originalCol,
                color = parkedBlock.color,
                direction = parkedBlock.direction,
                textLabel = parkedBlock.textLabel
            };
        }
        else
        {
            snapshot.parkedBlock = null;
        }

        snapshot.redCleared = redCleared;
        snapshot.greenCleared = greenCleared;
        snapshot.blueCleared = blueCleared;
        snapshot.yellowCleared = yellowCleared;
        snapshot.heartsRemaining = heartsRemaining;
        snapshot.maxTraySlots = maxTraySlots;
        snapshot.boardSize = boardSize;
        snapshot.matchTarget = matchTarget;
        snapshot.blueMatchTarget = currentBlueMatchTarget;
        snapshot.yellowMatchTarget = currentYellowMatchTarget;
        snapshot.blueGoalActive = blueGoalActive;
        snapshot.yellowGoalActive = yellowGoalActive;

        snapshot.undoUsed = powerupsUsed["undo"];
        snapshot.extraslotUsed = powerupsUsed["extraslot"];
        snapshot.kickbackUsed = powerupsUsed["kickback"];
        snapshot.hammerUsed = powerupsUsed["hammer"];
        snapshot.swapUsed = powerupsUsed["swap"];

        undoStack.Push(snapshot);
    }

    private void RestoreSnapshot(GameStateSnapshot snapshot)
    {
        // Clear existing block gameobjects
        foreach (var block in FindObjectsByType<UnityArrowBlock>(FindObjectsSortMode.None))
        {
            Destroy(block.gameObject);
        }
        trayBlocks.Clear();
        parkedBlock = null;

        boardSize = snapshot.boardSize;
        matchTarget = snapshot.matchTarget;
        grid = new UnityArrowBlock[boardSize, boardSize];
        maxTraySlots = snapshot.maxTraySlots;

        // Restore grid
        foreach (var data in snapshot.gridBlocks)
        {
            UnityArrowBlock ab = SpawnBlockGameObject(data);
            grid[data.row, data.col] = ab;
        }

        // Restore tray
        foreach (var data in snapshot.trayBlocks)
        {
            UnityArrowBlock ab = SpawnBlockGameObject(data);
            trayBlocks.Add(ab);
        }

        // Restore park
        if (snapshot.parkedBlock.HasValue)
        {
            parkedBlock = SpawnBlockGameObject(snapshot.parkedBlock.Value);
        }

        redCleared = snapshot.redCleared;
        greenCleared = snapshot.greenCleared;
        blueCleared = snapshot.blueCleared;
        yellowCleared = snapshot.yellowCleared;
        heartsRemaining = snapshot.heartsRemaining;
        currentBlueMatchTarget = snapshot.blueMatchTarget;
        currentYellowMatchTarget = snapshot.yellowMatchTarget;
        blueGoalActive = snapshot.blueGoalActive;
        yellowGoalActive = snapshot.yellowGoalActive;

        powerupsUsed["undo"] = snapshot.undoUsed;
        powerupsUsed["extraslot"] = snapshot.extraslotUsed;
        powerupsUsed["kickback"] = snapshot.kickbackUsed;
        powerupsUsed["hammer"] = snapshot.hammerUsed;
        powerupsUsed["swap"] = snapshot.swapUsed;

        DeactivateActivePowerups();
        UpdateTrayCapacityUI();
        RepositionTrayBlocksVisual();
    }

    private UnityArrowBlock SpawnBlockGameObject(BlockData data)
    {
        GameObject obj = new GameObject("RestoredBlock");
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetBlockSprite(data.color);
        sr.sortingOrder = 5;

        BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
        col.size = Vector2.one * GetBoardColliderSize();

        UnityArrowBlock ab = obj.AddComponent<UnityArrowBlock>();
        ab.id = System.Guid.NewGuid().ToString();
        ab.row = data.row;
        ab.col = data.col;
        ab.originalRow = data.row;
        ab.originalCol = data.col;
        ab.color = data.color;
        ab.direction = data.direction;
        ab.textLabel = data.textLabel;
        ab.boardSize = boardSize;
        ab.boardSpacing = GetBoardSpacing();
        ab.boardCenterX = GetBoardCenterX();
        ab.boardCenterY = GetBoardCenterY();
        ab.boardScale = GetBoardBlockScale();
        ab.SetBaseVisualScale(GetBoardBlockScale());

        obj.transform.position = GetBoardPosition(data.row, data.col);
        obj.transform.localScale = Vector3.one * GetBoardBlockScale();

        AddBlockArtwork(obj, data.direction, data.color, data.textLabel);

        return ab;
    }

    private void TriggerAutomaticRevert()
    {
        if (undoStack.Count > 0)
        {
            GameStateSnapshot snapshot = undoStack.Pop();
            RestoreSnapshot(snapshot);
            UpdateUI();
        }
    }

    private void CheckGameEndConditions()
    {
        if (levelEnded) return;

        bool clearedRed = redCleared >= matchTarget;
        bool clearedGreen = greenCleared >= matchTarget;
        bool clearedBlue = !blueGoalActive || blueCleared >= GetCurrentBlueMatchTarget();
        bool clearedYellow = !yellowGoalActive || yellowCleared >= GetCurrentYellowMatchTarget();

        if (clearedRed && clearedGreen && clearedBlue && clearedYellow)
        {
            // LEVEL CLEARED
            levelEnded = true;
            Debug.Log("VICTORY! All sets cleared successfully!");
            StartCoroutine(PlayBoardClearPulse());
            // Auto transition to next level after delay
            StartCoroutine(LoadNextLevelAfterDelay());
        }
    }

    private System.Collections.IEnumerator PlayBoosterAnimation(GameObject booster)
    {
        if (booster == null) yield break;

        Vector3 startScale = booster.transform.localScale;
        float elapsed = 0f;
        while (elapsed < BoosterAnimationDuration && booster != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / BoosterAnimationDuration);
            float bump = Mathf.Sin(progress * Mathf.PI) * 0.2f;
            booster.transform.localScale = startScale * (1f + bump);
            yield return null;
        }

        if (booster != null) booster.transform.localScale = startScale;
    }

    private System.Collections.IEnumerator PlayBoardClearPulse()
    {
        if (boardTrayObject == null) yield break;

        Vector3 startScale = boardTrayObject.transform.localScale;
        float elapsed = 0f;
        while (elapsed < BoardClearPulseDuration && boardTrayObject != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / BoardClearPulseDuration);
            float pulse = Mathf.Sin(progress * Mathf.PI) * 0.045f;
            boardTrayObject.transform.localScale = startScale * (1f + pulse);
            yield return null;
        }

        if (boardTrayObject != null) boardTrayObject.transform.localScale = startScale;
    }

    private System.Collections.IEnumerator LoadNextLevelAfterDelay()
    {
        yield return new WaitForSeconds(2.0f);
        int nextLevel = currentLevelIndex + 1;
        LoadLevel(nextLevel);
    }
}
