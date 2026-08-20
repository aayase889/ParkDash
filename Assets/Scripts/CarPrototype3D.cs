using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// An isolated 3D translation of the fixed Color Sort boards. Cars preserve the
/// exact positions, colors, and exit directions from the 2D level database;
/// neutral blocks are represented by the imported police-car block.
/// </summary>
public sealed class CarPrototype3D : MonoBehaviour
{
    private const int MaximumTraySlots = 5;
    private const int TutorialLevelCount = 20;
    // The legacy database still contains 60 boards; it remains available only
    // as source material for the separate post-campaign challenge samples.
    private const int LegacyFixedLevelCount = 60;
    private const int CampaignLevelCount = 100;
    private const int CampaignProgressVersion = 2;
    private const string CampaignProgressVersionKey = "CarPrototype.CampaignProgressVersion";
    private const string CampaignResumeBoardKey = "CarPrototype.CampaignResumeBoard";
    private const string CampaignHighestUnlockedBoardKey = "CarPrototype.CampaignHighestUnlockedBoard";
    private const string CampaignHighestCompletedBoardKey = "CarPrototype.CampaignHighestCompletedBoard";
    // Keep this as one reversible switch. The HUD and retry bank remain in the
    // project, but no hearts are shown or consumed while the switch is false.
    private static readonly bool EnableHeartSystem = false;
    private const float BoardRouteLaneX = 4.25f;
    private const float CarDriveSpeed = 18f;
    private const float OutsideCarDriveSpeed = 30f;
    private const float ParkingDriveSpeed = 18f;
    private const float RouteCornerRadius = 0.78f;
    // Corners remain visibly readable without introducing the large speed
    // pulse that made cars appear to brake at every sampled bend.
    private const float NaturalCurveSpeed = 16.5f;
    private const float MinimumNaturalCurveDuration = 0.070f;
    private const float MaximumNaturalCurveDuration = 0.160f;
    internal const float ContinuousRouteCornerSpeedRatio = 0.84f;
    internal const float ContinuousRouteFinalSpeedRatio = 0.68f;
    internal const float ContinuousRouteSlowdownFraction = 0.13f;
    private const float MatchAnticipationDuration = 0.045f;
    private const float AutomaticSafeMoveHintDelay = 30f;
    private const float BlockingCarHintDuration = 1.5f;
    private const float TapAssistRadiusPixels = 88f;
    internal const int MaximumEditableGarageQueueSlots = 12;
    private const float FullScreenAsphaltPadding = 0.5f;
    private const string PremiumAsphaltResourcePath = "Environment/PremiumAsphalt_MidGrey";
    private const string ApprovedParkingSidewalkResourcePrefix = "Environment/ApprovedParkingSidewalk_";
    private const string ApprovedMysteryBoxResourcePath = "CarPrototype/Mechanics/MysteryBox";
    private const string Level38MysteryBoxResourcePath =
        "CarPrototype/Mechanics/Level38Box/Level38MysteryBox";
    private const string ApprovedGarageOpenResourcePath = "CarPrototype/Mechanics/GarageOpen";
    private const string ApprovedGarageClosedResourcePath = "CarPrototype/Mechanics/GarageClosed";
    private const string ApprovedMechanicMaterialResourcePath =
        "CarPrototype/Mechanics/MechanicArtworkMaterial";
    // The approved sprites are tightly cropped and square. A uniform 2.2-unit
    // display size matches the regular car's one-cell visual footprint while
    // preserving the artwork's natural proportions (no width/height squeeze).
    internal const float MechanicArtworkDisplaySize = 2.2f;
    // Every generated sidewalk variant has equal 182-pixel bays. Runtime
    // world size is derived from the current board's car scale, with Demo 2
    // (the 4x4 board) as the approved visual reference.
    private const float ApprovedParkingBayPitchPixels = 182f;
    private const float ApprovedParkingBayCenterFromTopPixels = 193.5f;
    private const float ApprovedParkingDividerTopPixels = 21f;
    // The rebuilt sidewalk's horizontal inner curb starts at pixel 389. Stop
    // the road paint one pixel before it so the dividers never overlap the art.
    private const float ApprovedParkingSidewalkInnerEdgePixels = 388f;

    private static Texture2D asphaltTexture;
    private static Material backgroundAsphaltMaterial;
    private static Material playfieldAsphaltMaterial;
    private static Material parkingLineMaterial;
    private static readonly Dictionary<int, Material> approvedParkingSidewalkMaterials = new Dictionary<int, Material>();
    private static Material approvedMysteryBoxMaterial;
    private static Material level38MysteryBoxMaterial;
    private static Material approvedGarageOpenMaterial;
    private static Material approvedGarageClosedMaterial;
    private static Mesh sharedCubeMesh;
    private static Mesh sharedCylinderMesh;

    internal enum PieceColor { Red = 0, Green = 1, Blue = 2, Purple = 3, Yellow = 4, Trash = 5, Pink = 6 }
    internal enum ExitDirection { Up, Down, Left, Right }
    private enum LevelEditTool { RotateCars, SwapCars, EditCars, MoveGarages, MoveBoxes }
    private enum LevelDifficulty { Normal, Hard, SuperHard }
    private enum GameplayHintKind { None, SafeMove, BlockingCar }
    private static readonly PieceColor[] PlayableColors =
    {
        PieceColor.Red,
        PieceColor.Green,
        PieceColor.Blue,
        PieceColor.Purple,
        PieceColor.Yellow,
        PieceColor.Pink
    };

    [System.Serializable]
    private sealed class DirectionOverrideStore
    {
        public List<LevelDirectionOverride> levels = new List<LevelDirectionOverride>();
    }

    [System.Serializable]
    private sealed class LevelDirectionOverride
    {
        public string levelKey;
        public bool storesCarPlacements;
        public List<CarDirectionOverride> cars = new List<CarDirectionOverride>();
        public bool storesGaragePlacements;
        public List<GaragePlacementOverride> garages = new List<GaragePlacementOverride>();
        public bool storesBoxPlacements;
        public List<BoxPlacementOverride> boxes = new List<BoxPlacementOverride>();
    }

    [System.Serializable]
    private sealed class CarDirectionOverride
    {
        public bool storesPosition;
        public int pieceIndex;
        public int row;
        public int col;
        public int color;
        public int cellLength;
        public int direction;
    }

    [System.Serializable]
    private sealed class GaragePlacementOverride
    {
        public int id;
        public int row;
        public int col;
        public bool isEditorCreated;
        public bool storesQueueColors;
        public List<int> queueColors = new List<int>();
        public List<int> storedPieceIndexes = new List<int>();
    }

    [System.Serializable]
    private sealed class BoxPlacementOverride
    {
        public int pieceIndex;
        public bool storesVisualRotation;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW;
    }

    private struct PieceSpec
    {
        public int row;
        public int col;
        public PieceColor color;
        public ExitDirection direction;
        public int cellLength;
        public bool isRevealBox;
        public bool storesRevealVisualRotation;
        public Quaternion revealVisualRotation;

        public PieceSpec(int row, int col, PieceColor color, ExitDirection direction, int cellLength = 1)
        {
            this.row = row;
            this.col = col;
            this.color = color;
            this.direction = direction;
            this.cellLength = Mathf.Max(1, cellLength);
            isRevealBox = false;
            storesRevealVisualRotation = false;
            revealVisualRotation = Quaternion.identity;
        }
    }

    private sealed class GarageSpec
    {
        public readonly int id;
        public int row;
        public int col;
        public PieceColor[] carQueue;
        public readonly bool isEditorCreated;
        public readonly int[] storedPieceIndexes;

        public GarageSpec(
            int id,
            int row,
            int col,
            PieceColor[] carQueue,
            bool isEditorCreated = false,
            int[] storedPieceIndexes = null)
        {
            this.id = id;
            this.row = row;
            this.col = col;
            this.carQueue = carQueue ?? new PieceColor[0];
            this.isEditorCreated = isEditorCreated;
            this.storedPieceIndexes = storedPieceIndexes ?? new int[0];
        }
    }

    private struct LimousineCandidate
    {
        public int carIndex;
        public int neutralIndex;
        public int leadingRow;
        public int leadingCol;
        public ExitDirection direction;

        public LimousineCandidate(int carIndex, int neutralIndex, int leadingRow, int leadingCol, ExitDirection direction)
        {
            this.carIndex = carIndex;
            this.neutralIndex = neutralIndex;
            this.leadingRow = leadingRow;
            this.leadingCol = leadingCol;
            this.direction = direction;
        }
    }

    private sealed class CampaignTile
    {
        public readonly int firstRow;
        public readonly int firstCol;
        public readonly int secondRow;
        public readonly int secondCol;
        public readonly bool isLimousine;

        public CampaignTile(int row, int col)
        {
            firstRow = row;
            firstCol = col;
            secondRow = row;
            secondCol = col;
            isLimousine = false;
        }

        public CampaignTile(int firstRow, int firstCol, int secondRow, int secondCol)
        {
            this.firstRow = firstRow;
            this.firstCol = firstCol;
            this.secondRow = secondRow;
            this.secondCol = secondCol;
            isLimousine = true;
        }
    }

    private struct CampaignExitOption
    {
        public int tileIndex;
        public int leadingRow;
        public int leadingCol;
        public ExitDirection direction;

        public CampaignExitOption(int tileIndex, int leadingRow, int leadingCol, ExitDirection direction)
        {
            this.tileIndex = tileIndex;
            this.leadingRow = leadingRow;
            this.leadingCol = leadingCol;
            this.direction = direction;
        }
    }

    private sealed class PrototypeLevel
    {
        public readonly int boardNumber;
        public readonly int boardSize;
        public readonly int matchTarget;
        public readonly UnityGameManager.LevelConfig source;
        public readonly PieceSpec[] customSpecifications;
        public readonly GarageSpec[] garageSpecifications;
        public readonly LevelDifficulty difficulty;

        public PrototypeLevel(int boardNumber, UnityGameManager.LevelConfig source)
        {
            this.boardNumber = boardNumber;
            this.source = source;
            boardSize = source.boardSize;
            matchTarget = source.matchTarget;
            customSpecifications = null;
            garageSpecifications = new GarageSpec[0];
            difficulty = LevelDifficulty.Normal;
        }

        public PrototypeLevel(
            int boardNumber,
            int boardSize,
            int matchTarget,
            PieceSpec[] customSpecifications,
            GarageSpec[] garageSpecifications = null,
            LevelDifficulty difficulty = LevelDifficulty.Normal)
        {
            this.boardNumber = boardNumber;
            this.boardSize = boardSize;
            this.matchTarget = matchTarget;
            this.customSpecifications = customSpecifications;
            this.garageSpecifications = garageSpecifications ?? new GarageSpec[0];
            this.difficulty = difficulty;
            source = null;
        }
    }

    private sealed class ActiveGarage
    {
        public readonly GarageSpec specification;
        public GaragePuzzleVisual visual;
        public CarPuzzlePiece exposedPiece;
        public int nextQueueIndex;
        public Coroutine transitionCoroutine;

        public ActiveGarage(GarageSpec specification)
        {
            this.specification = specification;
        }
    }

    private struct ExperimentalLockRule
    {
        public int row;
        public int col;
        public PieceColor unlockAfterColor;

        public ExperimentalLockRule(int row, int col, PieceColor unlockAfterColor)
        {
            this.row = row;
            this.col = col;
            this.unlockAfterColor = unlockAfterColor;
        }
    }

    private sealed class ExperimentalRuleSet
    {
        public readonly string title;
        public readonly string planningHint;
        public readonly int parkingUseLimit;
        public readonly PieceColor[] requiredColorOrder;
        public readonly ExperimentalLockRule[] locks;

        public ExperimentalRuleSet(
            string title,
            string planningHint,
            int parkingUseLimit = -1,
            PieceColor[] requiredColorOrder = null,
            ExperimentalLockRule[] locks = null)
        {
            this.title = title;
            this.planningHint = planningHint;
            this.parkingUseLimit = parkingUseLimit;
            this.requiredColorOrder = requiredColorOrder ?? new PieceColor[0];
            this.locks = locks ?? new ExperimentalLockRule[0];
        }
    }

    private sealed class ActiveExperimentalLock
    {
        public readonly CarPuzzlePiece piece;
        public readonly PieceColor unlockAfterColor;

        public ActiveExperimentalLock(CarPuzzlePiece piece, PieceColor unlockAfterColor)
        {
            this.piece = piece;
            this.unlockAfterColor = unlockAfterColor;
        }
    }

    private struct RescueSearchState : System.IEquatable<RescueSearchState>
    {
        public ulong boardMask;
        public int trayCounts;
        public byte clearedColors;
        public int clearedSetCounts;
        public sbyte parkedColor;
        public byte parkingUses;

        public bool Equals(RescueSearchState other)
        {
            return boardMask == other.boardMask
                && trayCounts == other.trayCounts
                && clearedColors == other.clearedColors
                && clearedSetCounts == other.clearedSetCounts
                && parkedColor == other.parkedColor
                && parkingUses == other.parkingUses;
        }

        public override bool Equals(object obj)
        {
            return obj is RescueSearchState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = boardMask.GetHashCode();
                hash = hash * 397 ^ trayCounts;
                hash = hash * 397 ^ clearedColors;
                hash = hash * 397 ^ clearedSetCounts;
                hash = hash * 397 ^ parkedColor;
                hash = hash * 397 ^ parkingUses;
                return hash;
            }
        }
    }

    private struct RescueSearchNode
    {
        public RescueSearchState state;
        public CarPuzzlePiece firstActionPiece;

        public RescueSearchNode(
            RescueSearchState state,
            CarPuzzlePiece firstActionPiece)
        {
            this.state = state;
            this.firstActionPiece = firstActionPiece;
        }
    }

    private sealed class RescueSearchContext
    {
        public const int MaximumStates = 500000;
        public readonly CarPrototype3D game;
        public readonly List<CarPuzzlePiece> pieces;
        public readonly ulong[] cellOccupants;
        public readonly int[] unlockColorByPiece;
        public readonly ulong[] revealNeighborMaskByPiece;
        public readonly int activeColorMask;
        public readonly int parkingLimit;
        public int exploredStates;

        public RescueSearchContext(CarPrototype3D game, IList<CarPuzzlePiece> returnedCandidates)
        {
            this.game = game;
            pieces = new List<CarPuzzlePiece>(game.boardPieces.Count + returnedCandidates.Count);
            pieces.AddRange(game.boardPieces);
            for (int index = 0; index < returnedCandidates.Count; index++)
                if (!pieces.Contains(returnedCandidates[index])) pieces.Add(returnedCandidates[index]);

            cellOccupants = new ulong[game.activeBoardSize * game.activeBoardSize];
            unlockColorByPiece = new int[pieces.Count];
            revealNeighborMaskByPiece = new ulong[pieces.Count];
            for (int index = 0; index < unlockColorByPiece.Length; index++)
            {
                unlockColorByPiece[index] = -1;
            }

            for (int pieceIndex = 0; pieceIndex < pieces.Count; pieceIndex++)
            {
                CarPuzzlePiece piece = pieces[pieceIndex];
                ulong bit = 1UL << pieceIndex;
                for (int row = 0; row < game.activeBoardSize; row++)
                for (int col = 0; col < game.activeBoardSize; col++)
                    if (piece.OccupiesCell(row, col)) cellOccupants[row * game.activeBoardSize + col] |= bit;

                for (int lockIndex = 0; lockIndex < game.activeExperimentalLocks.Count; lockIndex++)
                {
                    ActiveExperimentalLock activeLock = game.activeExperimentalLocks[lockIndex];
                    if (activeLock.piece != piece) continue;
                    unlockColorByPiece[pieceIndex] = (int)activeLock.unlockAfterColor;
                    break;
                }
            }

            for (int pieceIndex = 0; pieceIndex < pieces.Count; pieceIndex++)
            {
                CarPuzzlePiece hiddenPiece = pieces[pieceIndex];
                if (!hiddenPiece.IsRevealCovered) continue;
                ulong neighborMask = 0UL;
                for (int neighborIndex = 0; neighborIndex < pieces.Count; neighborIndex++)
                {
                    if (neighborIndex == pieceIndex) continue;
                    CarPuzzlePiece neighborPiece = pieces[neighborIndex];
                    if (IsOrthogonallyAdjacent(hiddenPiece, neighborPiece))
                        neighborMask |= 1UL << neighborIndex;
                }
                revealNeighborMaskByPiece[pieceIndex] = neighborMask;
            }

            activeColorMask = 0;
            for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
            {
                int color = (int)PlayableColors[colorIndex];
                if (game.GetMatchTarget(PlayableColors[colorIndex]) > 0) activeColorMask |= 1 << color;
            }

            parkingLimit = game.activeExperimentalRules != null
                ? game.activeExperimentalRules.parkingUseLimit
                : -1;
        }
    }

    private readonly List<CarPuzzlePiece> boardPieces = new List<CarPuzzlePiece>();
    private readonly List<CarPuzzlePiece> trayPieces = new List<CarPuzzlePiece>();
    private readonly List<CarPuzzlePiece> allPieces = new List<CarPuzzlePiece>();
    private readonly List<ActiveGarage> activeGarages = new List<ActiveGarage>();
    private readonly Dictionary<int, CarPuzzlePiece> levelEditStoredPieces =
        new Dictionary<int, CarPuzzlePiece>();
    private readonly List<PrototypeLevel> levels = new List<PrototypeLevel>();
    private readonly List<PrototypeLevel> demoLevels = new List<PrototypeLevel>();
    private readonly List<Transform> boardGridCells = new List<Transform>();
    private readonly HashSet<CarPuzzlePiece> queuedBoardPieces = new HashSet<CarPuzzlePiece>();
    private readonly HashSet<CarPuzzlePiece> boardCarsCurrentlyDriving = new HashSet<CarPuzzlePiece>();
    // Input is locked per vehicle, never for the whole board. This allows a
    // newly exposed car or an unrelated side-parking car to be tapped while
    // another vehicle is still travelling, without letting two coroutines
    // control the same transform.
    private readonly HashSet<CarPuzzlePiece> carsCurrentlyMoving = new HashSet<CarPuzzlePiece>();
    // Board cars reserve only the short road segment they are physically
    // traversing. The old single-owner approach lock serialized every route,
    // so a second valid tap appeared to pause even when the cars were using
    // separate lanes. Segment claims preserve collision safety without
    // blocking unrelated movement.
    private readonly List<ActiveTrayTrafficClaim> activeTrayTrafficClaims = new List<ActiveTrayTrafficClaim>();
    private readonly Transform[] matchTrayDividerLines = new Transform[MaximumTraySlots - 1];
    private readonly List<Transform> sideParkingHatchLines = new List<Transform>();
    private readonly List<ActiveExperimentalLock> activeExperimentalLocks = new List<ActiveExperimentalLock>();

    private Camera prototypeCamera;
    private CarMatchVfx matchVfx;
    private CarPrototypeHudLayout sceneLayout;
    private GameObject levelRoot;
    private GameObject boardEnvironmentRoot;
    private Transform asphaltGroundTransform;
    private Transform roadBoardTransform;
    private Transform roadInsetTransform;
    private Transform matchTrayRootTransform;
    private Transform matchTraySidewalkTransform;
    private Renderer matchTraySidewalkRenderer;
    private Transform matchTrayDividerRootTransform;
    private Transform sideParkingRootTransform;
    private Transform sideParkingTopLine;
    private Transform sideParkingBottomLine;
    private Transform sideParkingLeftLine;
    private Transform sideParkingRightLine;
    private Transform sideRoadGuideRootTransform;
    private MeshFilter leftSideRoadGuideFilter;
    private MeshFilter rightSideRoadGuideFilter;
    private Renderer roadBoardRenderer;
    private CarPuzzlePiece parkedPiece;
    private int sideRoadMotionGroupsInProgress;
    private CarPuzzlePiece lastMovedPiece;
    private CarPrototypeHud hud;
    private int levelIndex;
    private int lastStandardLevelIndex;
    private int highestUnlockedLevelIndex;
    private int highestCompletedLevelIndex = -1;
    private int redCleared;
    private int greenCleared;
    private int blueCleared;
    private int purpleCleared;
    private int yellowCleared;
    private int pinkCleared;
    private int activeBoardSize = 3;
    private int activeMatchTarget = 3;
    private int activeRedTarget;
    private int activeGreenTarget;
    private int activeBlueTarget;
    private int activePurpleTarget;
    private int activeYellowTarget;
    private int activePinkTarget;
    private float activeBoardFirstRowZ = float.NaN;
    private readonly HashSet<PieceColor> activeAdjacentMatchColors = new HashSet<PieceColor>();
    private int hearts = 3;
    private int trayCapacity = 3;
    private int parkingUses;
    private bool extraSlotUsed;
    private bool isAnimating;
    private bool isClearingTrayMatch;
    private bool outcomeResolved;
    private bool isEvaluatingAvailableMoves;
    private int boardCarsInTransit;
    private int boxRevealAnimationsInProgress;
    private int garageTransitionsInProgress;
    private ExperimentalRuleSet activeExperimentalRules;
    private bool isDemoMode;
    private Coroutine demoAutoPlayCoroutine;
    private bool demoAutoPlayPending;
    private bool directionArrowsEnabled = true;
    private bool levelEditMode;
    private LevelEditTool levelEditTool;
    private CarPuzzlePiece levelEditSwapSelection;
    private PieceColor levelEditCarColor = PieceColor.Red;
    private ActiveGarage levelEditCarGarageSelection;
    private int levelEditCarGarageSlot = -1;
    private ActiveGarage levelEditGarageSelection;
    private CarPuzzlePiece levelEditBoxSelection;
    private Quaternion levelEditLastBoxVisualRotation = Quaternion.identity;
    private bool levelEditHasBoxVisualRotation;
    private DirectionOverrideStore directionOverrideStore = new DirectionOverrideStore();
    private bool towRescueUsed;
    private bool trafficJamPopupOpen;
    private int tutorialStep;
    private float tutorialMessageExpiresAt;
    private CarPuzzlePiece gameplayHintPiece;
    private GameplayHintKind gameplayHintKind;
    private float gameplayHintExpiresAt;
    private float nextAutomaticHintTime;

    public int BoardNumber
    {
        get
        {
            List<PrototypeLevel> activeLevels = GetActiveLevels();
            return activeLevels.Count == 0 ? 1 : activeLevels[Mathf.Clamp(levelIndex, 0, activeLevels.Count - 1)].boardNumber;
        }
    }
    public string LevelDifficultyLabel
    {
        get
        {
            List<PrototypeLevel> activeLevels = GetActiveLevels();
            if (activeLevels.Count == 0 || isDemoMode) return "LEVEL";
            LevelDifficulty difficulty = activeLevels[Mathf.Clamp(levelIndex, 0, activeLevels.Count - 1)].difficulty;
            if (difficulty == LevelDifficulty.Hard) return "HARD";
            if (difficulty == LevelDifficulty.SuperHard) return "SUPER HARD";
            return "LEVEL";
        }
    }
    public int GarageCount
    {
        get
        {
            List<PrototypeLevel> activeLevels = GetActiveLevels();
            if (activeLevels.Count == 0) return 0;
            return activeLevels[Mathf.Clamp(levelIndex, 0, activeLevels.Count - 1)]
                .garageSpecifications.Length;
        }
    }
    public int Hearts => hearts;
    public int RedCleared => redCleared;
    public int GreenCleared => greenCleared;
    public int BlueCleared => blueCleared;
    public int PurpleCleared => purpleCleared;
    public int YellowCleared => yellowCleared;
    public int PinkCleared => pinkCleared;
    public int MatchGoal => activeMatchTarget;
    public int RedGoal => activeRedTarget;
    public int GreenGoal => activeGreenTarget;
    public int BlueGoal => activeBlueTarget;
    public int PurpleGoal => activePurpleTarget;
    public int YellowGoal => activeYellowTarget;
    public int PinkGoal => activePinkTarget;
    public int RedObjectiveRemaining => GetObjectiveRemaining(PieceColor.Red);
    public int GreenObjectiveRemaining => GetObjectiveRemaining(PieceColor.Green);
    public int BlueObjectiveRemaining => GetObjectiveRemaining(PieceColor.Blue);
    public int PurpleObjectiveRemaining => GetObjectiveRemaining(PieceColor.Purple);
    public int YellowObjectiveRemaining => GetObjectiveRemaining(PieceColor.Yellow);
    public int PinkObjectiveRemaining => GetObjectiveRemaining(PieceColor.Pink);
    public int TrayCapacity => trayCapacity;
    public bool HeartsEnabled => EnableHeartSystem;
    public bool ExtraSlotUsed => extraSlotUsed;
    public bool CanUseExtraSlot => !outcomeResolved && activeExperimentalRules == null && !isAnimating && !extraSlotUsed && trayCapacity < MaximumTraySlots;
    public bool CanUseUndo => !outcomeResolved && !isAnimating && lastMovedPiece != null;
    public string ExperimentalRuleStatus => BuildExperimentalRuleStatus();
    public bool IsDemoMode => isDemoMode;
    public bool DirectionArrowsEnabled => directionArrowsEnabled;
    public bool LevelEditMode => levelEditMode;
    public bool LevelEditSwapMode => levelEditTool == LevelEditTool.SwapCars;
    public bool LevelEditCarMode => levelEditTool == LevelEditTool.EditCars;
    public bool LevelEditGarageMode => levelEditTool == LevelEditTool.MoveGarages;
    public bool LevelEditBoxMode => levelEditTool == LevelEditTool.MoveBoxes;
    public bool LevelEditHasGarageSelection => levelEditGarageSelection != null;
    public bool LevelEditHasBoxSelection => levelEditBoxSelection != null;
    public int LevelEditCarColor => (int)levelEditCarColor;
    public string LevelEditCarColorLabel => ColorName(levelEditCarColor);
    public bool LevelEditCarGarageSelected => levelEditCarGarageSelection != null;
    public int LevelEditCarGarageQueueCount => levelEditCarGarageSelection != null
        ? levelEditCarGarageSelection.specification.carQueue.Length
        : 0;
    public int LevelEditCarGarageSlot => levelEditCarGarageSlot;
    public bool LevelEditSelectedGarageQueueResizable => levelEditCarGarageSelection != null
        && !levelEditCarGarageSelection.specification.isEditorCreated;
    public bool IsChallengeLabActive => !isDemoMode && BoardNumber > CampaignLevelCount;
    public bool ChallengeLabAvailable => !isDemoMode && levels.Count > CampaignLevelCount;
    public int SavedCampaignBoardNumber => lastStandardLevelIndex + 1;
    public string SavedCampaignDifficultyLabel
    {
        get
        {
            int availableCampaignLevels = Mathf.Min(CampaignLevelCount, levels.Count);
            if (availableCampaignLevels <= 0) return "LEVEL";

            LevelDifficulty difficulty = levels[
                Mathf.Clamp(lastStandardLevelIndex, 0, availableCampaignLevels - 1)].difficulty;
            if (difficulty == LevelDifficulty.Hard) return "HARD";
            if (difficulty == LevelDifficulty.SuperHard) return "SUPER HARD";
            return "LEVEL";
        }
    }
    public int HighestUnlockedCampaignBoardNumber => highestUnlockedLevelIndex + 1;
    public int HighestCompletedCampaignBoardNumber => highestCompletedLevelIndex + 1;
    internal Camera TutorialSourceCamera => prototypeCamera;
    internal Transform TutorialFocusTransform
    {
        get
        {
            CarPuzzlePiece focusPiece = GetTutorialFocusPiece();
            if (focusPiece == null) focusPiece = gameplayHintPiece;
            return focusPiece != null ? focusPiece.transform : null;
        }
    }

    public bool TryGetTutorialPresentation(
        out Vector2 screenPosition,
        out string title,
        out string message,
        out bool showSpotlight,
        out bool isGameplayHint)
    {
        screenPosition = Vector2.zero;
        title = string.Empty;
        message = string.Empty;
        showSpotlight = false;
        isGameplayHint = false;

        if (levelEditMode || isDemoMode || prototypeCamera == null)
            return false;

        UpdateTutorialStep();
        bool tutorialBoard = BoardNumber >= 1 && BoardNumber <= TutorialLevelCount;
        CarPuzzlePiece focusPiece = tutorialBoard ? GetTutorialFocusPiece() : null;
        if (tutorialBoard) GetTutorialCopy(out title, out message);
        if (focusPiece == null && gameplayHintPiece != null)
        {
            focusPiece = gameplayHintPiece;
            // Automatic and blocker hints are intentionally visual-only. The
            // highlighted car is enough guidance without interrupting play
            // with tutorial copy or a hand cursor.
            title = string.Empty;
            message = string.Empty;
            isGameplayHint = true;
        }
        else if (focusPiece == null && Time.unscaledTime > tutorialMessageExpiresAt)
        {
            title = string.Empty;
            message = string.Empty;
        }
        showSpotlight = focusPiece != null;
        if (focusPiece != null)
        {
            Vector3 world = focusPiece.transform.position + Vector3.up * 0.55f;
            screenPosition = prototypeCamera.WorldToScreenPoint(world);
        }

        return showSpotlight || !string.IsNullOrEmpty(message);
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Screen.orientation = ScreenOrientation.Portrait;
    }

    private void Start()
    {
        CarPrototypeFeedback.EnsureExists();
        sceneLayout = CarPrototypeHudLayout.LoadOrDefault();
        CreateCamera();
        CreateLighting();
        CreateVisualEffects();
        matchVfx = gameObject.GetComponent<CarMatchVfx>();
        if (matchVfx == null) matchVfx = gameObject.AddComponent<CarMatchVfx>();
        matchVfx.Initialize(prototypeCamera, transform);
        LoadSavedDirectionOverrides();
        LoadFixedLevels();
        LoadSavedCampaignProgress();
        CreateMatchTray();
        CreateParkingSlot();
        hud = gameObject.AddComponent<CarPrototypeHud>();
        hud.Initialize(this);
        hud.BeginStartupFlow();
    }

    /// <summary>
    /// Applies the scene controls exposed by the 3D layout editor. This is safe
    /// to call repeatedly while Play Mode is running.
    /// </summary>
    public bool Apply3DSettingsFromEditor(CarPrototypeHudLayout editedLayout)
    {
        if (editedLayout == null) return false;
        sceneLayout = editedLayout;

        ApplyCameraSettings();
        ApplyAsphaltMaterialTint(backgroundAsphaltMaterial, sceneLayout.sceneBackgroundAsphaltColor);
        ApplyAsphaltMaterialTint(playfieldAsphaltMaterial, sceneLayout.scenePlayfieldAsphaltColor);

        if (roadBoardRenderer != null && roadBoardRenderer.sharedMaterial != null)
            roadBoardRenderer.sharedMaterial.color = sceneLayout.sceneRoadBorderColor;

        float boardWidth = activeBoardSize >= 4 ? 7.2f : 6.9f;
        float roadDepth = Mathf.Max(1f, sceneLayout.sceneRoadDepth);
        CalculateFullScreenAsphaltSurface(
            boardWidth - 0.65f,
            Mathf.Max(0.35f, roadDepth - 0.65f),
            sceneLayout.sceneRoadCenterZ,
            out Vector3 roadSurfacePosition,
            out Vector3 roadSurfaceScale);
        if (asphaltGroundTransform != null)
        {
            asphaltGroundTransform.position = sceneLayout.sceneAsphaltGroundPosition;
            asphaltGroundTransform.localScale = PositiveScale(sceneLayout.sceneAsphaltGroundSize);
        }
        if (roadBoardTransform != null)
        {
            roadBoardTransform.position = new Vector3(roadSurfacePosition.x, -0.25f, roadSurfacePosition.z);
            roadBoardTransform.localScale = new Vector3(roadSurfaceScale.x + 0.65f, 0.5f, roadSurfaceScale.z + 0.65f);
        }
        if (roadInsetTransform != null)
        {
            roadInsetTransform.position = roadSurfacePosition;
            roadInsetTransform.localScale = roadSurfaceScale;
        }

        UpdateMatchTrayRoadMarkings();
        UpdateParkingHighlight();
        activeBoardFirstRowZ = CalculateBoardFirstRowZ();

        float boardScale = GetBoardPieceScale();
        for (int index = 0; index < trayPieces.Count; index++)
        {
            trayPieces[index].UpdateScaleSettings(boardScale, GetOffBoardPieceScale(trayPieces[index].CellLength));
            trayPieces[index].SetTrayPose(GetTraySlotPosition(index));
        }
        if (parkedPiece != null)
        {
            parkedPiece.UpdateScaleSettings(boardScale, GetOffBoardPieceScale(parkedPiece.CellLength));
            parkedPiece.SetParkingPose(GetParkingSlotPosition());
        }

        for (int index = 0; index < boardGridCells.Count; index++)
        {
            int row = index / activeBoardSize;
            int col = index % activeBoardSize;
            boardGridCells[index].position = GetBoardCellPosition(row, col) + new Vector3(0f, -0.18f, 0f);
        }

        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece == null) continue;
            piece.UpdateScaleSettings(boardScale, GetOffBoardPieceScale(piece.CellLength));
            piece.SetBoardPose(GetBoardPiecePosition(piece));
        }

        for (int index = 0; index < activeGarages.Count; index++)
        {
            ActiveGarage garage = activeGarages[index];
            if (garage.visual == null) continue;
            garage.visual.transform.position = GetBoardCellPosition(
                garage.specification.row,
                garage.specification.col) + new Vector3(0f, 0.015f, 0f);
            garage.visual.transform.localScale = Vector3.one * boardScale;
        }

        return true;
    }

    private void Update()
    {
        if (demoAutoPlayPending && demoAutoPlayCoroutine == null)
        {
            demoAutoPlayPending = false;
            demoAutoPlayCoroutine = StartCoroutine(PlayDemoAutomatically());
        }

        UpdateGameplayHint();
        if (outcomeResolved || Time.timeScale == 0f) return;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TrySelectPiece(Touchscreen.current.primaryTouch.position.ReadValue());
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TrySelectPiece(Mouse.current.position.ReadValue());

        if (Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame) LoadNextLevel();
            if (Keyboard.current.digit1Key.wasPressedThisFrame) LoadLevel(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) LoadLevel(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) LoadLevel(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) LoadLevel(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) LoadLevel(4);
        }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            TrySelectPiece(Input.GetTouch(0).position);
        else if (Input.GetMouseButtonDown(0))
            TrySelectPiece(Input.mousePosition);
#endif
    }

    [ContextMenu("Load Next Prototype Level")]
    public void LoadNextLevel()
    {
        List<PrototypeLevel> activeLevels = GetActiveLevels();
        if (activeLevels.Count == 0) return;

        if (isDemoMode && levelIndex >= activeLevels.Count - 1)
        {
            if (hud != null) hud.ShowMainMenu();
            return;
        }

        LoadLevel((levelIndex + 1) % activeLevels.Count);
    }

    [ContextMenu("Load Previous Prototype Level")]
    public void LoadPreviousLevel()
    {
        List<PrototypeLevel> activeLevels = GetActiveLevels();
        if (activeLevels.Count == 0) return;
        if (isDemoMode)
        {
            LoadLevel(Mathf.Max(0, levelIndex - 1));
            return;
        }

        LoadLevel((levelIndex - 1 + activeLevels.Count) % activeLevels.Count);
    }

    public void StartRegularGame()
    {
        isDemoMode = false;
        LoadLevel(Mathf.Clamp(lastStandardLevelIndex, 0, CampaignLevelCount - 1));
    }

    public void StartDemo()
    {
        if (demoLevels.Count == 0) return;
        isDemoMode = true;
        LoadLevel(0);
    }

    public void ToggleDemoAutoPlay()
    {
        if (demoAutoPlayCoroutine != null)
        {
            StopCoroutine(demoAutoPlayCoroutine);
            demoAutoPlayCoroutine = null;
            demoAutoPlayPending = false;
            if (hud != null) hud.SetDemoAutoPlayState(false);
            return;
        }

        demoAutoPlayPending = false;
        demoAutoPlayCoroutine = StartCoroutine(PlayDemoAutomatically());
        if (hud != null) hud.SetDemoAutoPlayState(true);
    }

    public void ResumeDemoAutoPlayAfterLevelChange()
    {
        demoAutoPlayCoroutine = null;
        demoAutoPlayPending = true;
        if (hud != null) hud.SetDemoAutoPlayState(true);
    }

    private IEnumerator PlayDemoAutomatically()
    {
        while (true)
        {
            if (!TryBuildDemoAutoPlayPlan(out List<DemoAutoPlayAction> plan))
            {
                Debug.LogWarning("Demo Auto Play could not find a safe move sequence for this board.");
                break;
            }

            for (int index = 0; index < plan.Count; index++)
            {
                DemoAutoPlayAction action = plan[index];
                yield return new WaitForSecondsRealtime(0.34f);
                switch (action.type)
                {
                    case DemoAutoPlayActionType.Move:
                        if (action.piece != null && boardPieces.Contains(action.piece))
                            TryMovePiece(action.piece);
                        break;
                    case DemoAutoPlayActionType.Park:
                        CarPuzzlePiece trayPiece = trayPieces.Find(piece => piece != null && piece.PieceColor == action.color);
                        if (trayPiece != null) TryParkTrayPiece(trayPiece);
                        break;
                    case DemoAutoPlayActionType.ReturnFromParking:
                        if (parkedPiece != null) TryReturnParkedPiece();
                        break;
                }
                while (isAnimating || isClearingTrayMatch
                    || boardCarsInTransit > 0 || boxRevealAnimationsInProgress > 0
                    || garageTransitionsInProgress > 0)
                    yield return null;
            }

            while (!outcomeResolved)
                yield return null;

            yield return new WaitForSecondsRealtime(1.0f);
            int finalAutoPlayLevel = isDemoMode ? GetActiveLevels().Count - 1 : CampaignLevelCount - 1;
            if (levelIndex >= finalAutoPlayLevel) break;
            if (hud != null) hud.ContinueDemoAutoPlay();
            else LoadNextLevel();
            yield return new WaitForSecondsRealtime(0.55f);
        }

        demoAutoPlayCoroutine = null;
        if (hud != null) hud.SetDemoAutoPlayState(false);
    }

    private enum DemoAutoPlayActionType { Move, Park, ReturnFromParking }

    private struct DemoAutoPlayAction
    {
        public DemoAutoPlayActionType type;
        public CarPuzzlePiece piece;
        public PieceColor color;
    }

    private bool TryBuildDemoAutoPlayPlan(out List<DemoAutoPlayAction> plan)
    {
        plan = new List<DemoAutoPlayAction>();
        if (boardPieces.Count == 0) return false;

        var pieces = new List<CarPuzzlePiece>(boardPieces);
        int goalMask = 0;
        for (int index = 0; index < PlayableColors.Length; index++)
        {
            PieceColor color = PlayableColors[index];
            if (GetMatchTarget(color) > 0) goalMask |= 1 << (int)color;
        }

        var visited = new HashSet<string>();
        var actionIndexes = new List<DemoAutoPlayAction>();
        DemoAutoPlayState start = new DemoAutoPlayState { remainingMask = (1 << pieces.Count) - 1, parkedColor = -1 };
        // The limousine board has substantially more valid-looking branches
        // than a normal board. Give the demo-only planner enough room to
        // explore those alternatives before it gives up.
        int searchBudget = 750000;
        if (!SearchDemoAutoPlayPlan(pieces, goalMask, start, visited, actionIndexes, ref searchBudget)) return false;

        plan.AddRange(actionIndexes);
        return true;
    }

    private struct DemoAutoPlayState
    {
        public int remainingMask;
        public int clearedMask;
        public int[] clearedCounts;
        public int[] trayCounts;
        public int parkedColor;

        public int TrayCount
        {
            get
            {
                int count = 0;
                if (trayCounts == null) return 0;
                for (int index = 0; index < trayCounts.Length; index++) count += trayCounts[index];
                return count;
            }
        }

        public string Key()
        {
            string tray = trayCounts == null ? "" : string.Join(",", trayCounts);
            string cleared = clearedCounts == null ? "" : string.Join(",", clearedCounts);
            return remainingMask + "|" + clearedMask + "|" + parkedColor + "|" + tray + "|" + cleared;
        }
    }

    private bool SearchDemoAutoPlayPlan(
        List<CarPuzzlePiece> pieces,
        int goalMask,
        DemoAutoPlayState state,
        HashSet<string> visited,
        List<DemoAutoPlayAction> actions,
        ref int searchBudget)
    {
        // Demo boards can contain more cars of a colour than its required group.
        // The level is complete once every active colour goal is cleared.
        if (state.clearedMask == goalMask) return true;
        if (searchBudget-- <= 0 || !visited.Add(state.Key())) return false;

        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 0; index < pieces.Count; index++)
            {
                if ((state.remainingMask & (1 << index)) == 0) continue;
                CarPuzzlePiece piece = pieces[index];
                if (!IsDemoAutoExitClear(pieces, state.remainingMask, piece)) continue;

                bool isTrash = piece.IsTrash;
                int colorIndex = (int)piece.PieceColor;
                // Moving a colour after its goal is cleared only takes tray space.
                if (!isTrash && (state.clearedMask & (1 << colorIndex)) != 0) continue;
                bool continuesTrayColor = !isTrash && state.trayCounts != null && state.trayCounts[colorIndex] > 0;
                if ((pass == 0) != (isTrash || continuesTrayColor)) continue;
                if (!isTrash && state.TrayCount >= trayCapacity) continue;

                DemoAutoPlayState next = state;
                next.remainingMask &= ~(1 << index);
                if (!isTrash)
                {
                    if (next.trayCounts == null) next.trayCounts = new int[7];
                    else next.trayCounts = (int[])next.trayCounts.Clone();
                    next.trayCounts[colorIndex]++;
                    int target = GetMatchTarget(piece.PieceColor);
                    if (target > 0 && next.trayCounts[colorIndex] >= target)
                    {
                        next.trayCounts[colorIndex] -= target;
                        if (next.clearedCounts == null) next.clearedCounts = new int[7];
                        else next.clearedCounts = (int[])next.clearedCounts.Clone();
                        next.clearedCounts[colorIndex] += target;
                        if (next.clearedCounts[colorIndex] >= GetActiveTarget(piece.PieceColor))
                            next.clearedMask |= 1 << colorIndex;
                    }
                }

                actions.Add(new DemoAutoPlayAction
                {
                    type = DemoAutoPlayActionType.Move,
                    piece = piece,
                    color = piece.PieceColor
                });
                if (SearchDemoAutoPlayPlan(pieces, goalMask, next, visited, actions, ref searchBudget)) return true;
                actions.RemoveAt(actions.Count - 1);
            }
        }

        // The four demo boards are designed to showcase the side parking bay.
        // Simulate parking a tray car (or returning one) as part of the plan.
        if (state.parkedColor >= 0 && state.TrayCount < trayCapacity)
        {
            DemoAutoPlayState next = state;
            if (next.trayCounts == null) next.trayCounts = new int[7];
            else next.trayCounts = (int[])next.trayCounts.Clone();
            next.trayCounts[next.parkedColor]++;
            PieceColor returnedColor = (PieceColor)next.parkedColor;
            next.parkedColor = -1;
            NormalizeDemoAutoPlayMatches(ref next);
            actions.Add(new DemoAutoPlayAction { type = DemoAutoPlayActionType.ReturnFromParking, color = returnedColor });
            if (SearchDemoAutoPlayPlan(pieces, goalMask, next, visited, actions, ref searchBudget)) return true;
            actions.RemoveAt(actions.Count - 1);
        }

        if (state.TrayCount > 0)
        {
            for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
            {
                int color = (int)PlayableColors[colorIndex];
                if (state.trayCounts == null || state.trayCounts[color] == 0) continue;

                DemoAutoPlayState next = state;
                next.trayCounts = (int[])next.trayCounts.Clone();
                next.trayCounts[color]--;
                if (next.parkedColor >= 0) next.trayCounts[next.parkedColor]++;
                next.parkedColor = color;
                NormalizeDemoAutoPlayMatches(ref next);
                actions.Add(new DemoAutoPlayAction { type = DemoAutoPlayActionType.Park, color = (PieceColor)color });
                if (SearchDemoAutoPlayPlan(pieces, goalMask, next, visited, actions, ref searchBudget)) return true;
                actions.RemoveAt(actions.Count - 1);
            }
        }
        return false;
    }

    private void NormalizeDemoAutoPlayMatches(ref DemoAutoPlayState state)
    {
        if (state.trayCounts == null) return;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
        {
            int color = (int)PlayableColors[colorIndex];
            if ((state.clearedMask & (1 << color)) != 0) continue;
            int target = GetMatchTarget((PieceColor)color);
            if (target <= 0 || state.trayCounts[color] < target) continue;
            state.trayCounts[color] -= target;
            if (state.clearedCounts == null) state.clearedCounts = new int[7];
            else state.clearedCounts = (int[])state.clearedCounts.Clone();
            state.clearedCounts[color] += target;
            if (state.clearedCounts[color] >= GetActiveTarget((PieceColor)color))
                state.clearedMask |= 1 << color;
        }
    }

    private bool IsDemoAutoExitClear(List<CarPuzzlePiece> pieces, int remainingMask, CarPuzzlePiece piece)
    {
        Vector2Int step = DirectionToGridStep(piece.Direction);
        int row = piece.Row + step.y;
        int col = piece.Col + step.x;
        while (row >= 0 && row < activeBoardSize && col >= 0 && col < activeBoardSize)
        {
            for (int index = 0; index < pieces.Count; index++)
            {
                if ((remainingMask & (1 << index)) == 0 || pieces[index] == piece) continue;
                if (pieces[index].OccupiesCell(row, col)) return false;
            }
            row += step.y;
            col += step.x;
        }
        return true;
    }

    /// <summary>
    /// Opens the optional post-campaign challenge samples, or returns to the regular
    /// board the player was testing before entering the lab.
    /// </summary>
    public void ToggleChallengeLab()
    {
        if (isDemoMode || !ChallengeLabAvailable) return;

        if (IsChallengeLabActive)
        {
            LoadLevel(Mathf.Clamp(lastStandardLevelIndex, 0, CampaignLevelCount - 1));
            return;
        }

        lastStandardLevelIndex = Mathf.Clamp(levelIndex, 0, CampaignLevelCount - 1);
        LoadLevel(CampaignLevelCount);
    }

    private void TrySelectPiece(Vector2 screenPosition)
    {
        RegisterGameplayActivity();
        if (outcomeResolved) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (levelEditMode && LevelEditGarageMode)
        {
            SelectEditedGarage(screenPosition);
            return;
        }

        if (levelEditMode && LevelEditCarMode)
        {
            SelectEditedCar(screenPosition);
            return;
        }

        CarPuzzlePiece piece = PickPiece(screenPosition);
        if (piece == null) return;

        if (levelEditMode)
        {
            if (!boardPieces.Contains(piece)) return;
            if (LevelEditBoxMode) SelectEditedBox(piece);
            else if (LevelEditSwapMode) SelectEditedPieceForSwap(piece);
            else RotateEditedPiece(piece);
            return;
        }

        // The hand and the input lock share the same focus source. This keeps
        // tutorial input deterministic: a dimmed car cannot be moved by a
        // direct hit or by the mobile tap-assist radius, and the lock advances
        // to the next car as soon as the authored tutorial step advances.
        if (!IsTutorialInteractionAllowed(piece)) return;
        if (carsCurrentlyMoving.Contains(piece)) return;

        // Undo, tow-rescue, and other system-owned sequences still require an
        // exclusive state. Ordinary board, tray, match, and side-parking motion
        // always has a tracked car and therefore remains fully responsive.
        if (IsVehicleInputBlockedByExclusiveAnimation()) return;

        if (boardPieces.Contains(piece))
        {
            TryMovePiece(piece);
            return;
        }

        if (trayPieces.Contains(piece))
        {
            TryParkTrayPiece(piece);
            return;
        }

        if (parkedPiece == piece) TryReturnParkedPiece();
    }

    private bool IsVehicleInputBlockedByExclusiveAnimation()
    {
        if (!isAnimating) return false;

        // These are ordinary overlapping gameplay animations. Their own cars
        // remain protected individually, while unrelated valid taps stay live.
        bool responsiveGameplayAnimationActive = boardCarsInTransit > 0
            || carsCurrentlyMoving.Count > 0
            || isClearingTrayMatch
            || boxRevealAnimationsInProgress > 0
            || garageTransitionsInProgress > 0;
        return !responsiveGameplayAnimationActive;
    }

    private CarPuzzlePiece PickPiece(Vector2 screenPosition)
    {
        Ray ray = prototypeCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        CarPuzzlePiece closestCar = null;
        float closestDistance = float.MaxValue;

        // The road tiles also have colliders. Search every hit so a road collider
        // can never hide a car that the player has tapped.
        for (int index = 0; index < hits.Length; index++)
        {
            CarPuzzlePiece candidate = hits[index].collider.GetComponentInParent<CarPuzzlePiece>();
            if (candidate == null || !candidate.IsTouchable || hits[index].distance >= closestDistance) continue;
            if (carsCurrentlyMoving.Contains(candidate)) continue;

            closestCar = candidate;
            closestDistance = hits[index].distance;
        }

        if (closestCar != null) return closestCar;

        // Mobile touches are imprecise, especially on the small parked and tray cars.
        // If the ray barely misses a collider, choose the nearest visible car in a
        // restrained screen-space radius instead of ignoring the tap.
        CarPuzzlePiece assistedCar = null;
        float nearestScreenDistance = TapAssistRadiusPixels * TapAssistRadiusPixels;
        for (int index = 0; index < allPieces.Count; index++)
        {
            CarPuzzlePiece candidate = allPieces[index];
            if (candidate == null || !candidate.IsTouchable) continue;
            if (carsCurrentlyMoving.Contains(candidate)) continue;

            Vector3 projected = prototypeCamera.WorldToScreenPoint(candidate.transform.position + Vector3.up * 0.42f);
            if (projected.z < 0f) continue;

            float distance = ((Vector2)projected - screenPosition).sqrMagnitude;
            if (distance >= nearestScreenDistance) continue;

            assistedCar = candidate;
            nearestScreenDistance = distance;
        }

        return assistedCar;
    }

    private ActiveGarage PickGarage(Vector2 screenPosition)
    {
        Ray ray = prototypeCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            100f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        ActiveGarage closest = null;
        float closestDistance = float.MaxValue;
        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            GaragePuzzleVisual visual = hits[hitIndex].collider.GetComponentInParent<GaragePuzzleVisual>();
            if (visual == null || hits[hitIndex].distance >= closestDistance) continue;
            for (int garageIndex = 0; garageIndex < activeGarages.Count; garageIndex++)
            {
                ActiveGarage candidate = activeGarages[garageIndex];
                if (candidate.visual != visual) continue;
                closest = candidate;
                closestDistance = hits[hitIndex].distance;
                break;
            }
        }
        return closest;
    }

    private bool TryGetEditedBoardCell(Vector2 screenPosition, out int row, out int col)
    {
        row = -1;
        col = -1;
        if (prototypeCamera == null) return false;

        Ray ray = prototypeCamera.ScreenPointToRay(screenPosition);
        var boardPlane = new Plane(Vector3.up, new Vector3(0f, 0.35f, 0f));
        if (!boardPlane.Raycast(ray, out float distance)) return false;

        Vector3 world = ray.GetPoint(distance);
        float spacing = GetBoardSpacing();
        float center = (activeBoardSize - 1) * 0.5f;
        col = Mathf.RoundToInt(world.x / spacing + center);
        row = Mathf.RoundToInt((GetBoardFirstRowZ() - world.z) / spacing);
        if (row < 0 || row >= activeBoardSize || col < 0 || col >= activeBoardSize)
            return false;

        Vector3 cell = GetBoardCellPosition(row, col);
        Vector2 offset = new Vector2(world.x - cell.x, world.z - cell.z);
        return offset.sqrMagnitude <= spacing * spacing * 0.34f;
    }

    private void TryMovePiece(CarPuzzlePiece piece)
    {
        if (piece.IsRevealCovered)
        {
            piece.Reject();
            CarPrototypeFeedback.Error();
            return;
        }

        if (piece.IsLocked)
        {
            piece.Reject();
            CarPrototypeFeedback.Error();
            RefreshHud();
            return;
        }

        if (IsExitBlockedByGarage(piece))
        {
            piece.Reject();
            RegisterWrongMove();
            return;
        }

        CarPuzzlePiece blockingPiece = FindFirstExitBlocker(piece);
        if (blockingPiece != null)
        {
            piece.Reject();
            RegisterWrongMove();
            ShowGameplayHint(
                blockingPiece,
                GameplayHintKind.BlockingCar,
                BlockingCarHintDuration);
            return;
        }

        // A fast tap immediately after completing a color used to be judged
        // while the matched cars still occupied the tray during their short
        // celebration. Preserve that tap and launch it as soon as the matched
        // cars release their slots instead of incorrectly removing a heart.
        if (!piece.IsTrash && trayPieces.Count >= trayCapacity)
        {
            if (TryBeginFullTrayDominantExchange(piece))
                return;

            if (HasCompletedTrayMatchWaiting())
            {
                QueueBoardPieceAfterTrayMatch(piece);
                return;
            }

            piece.Reject();
            RegisterWrongMove();
            return;
        }

        piece.BeginExitPoliceLights();
        CarPrototypeFeedback.CarMove();
        boardPieces.Remove(piece);
        lastMovedPiece = piece;

        // Mark the car as travelling before reserving its tray slot or
        // refreshing the HUD. Otherwise the no-move evaluator sees a full tray
        // for one frame and opens Traffic Jam while the car is still driving.
        boardCarsInTransit++;
        boardCarsCurrentlyDriving.Add(piece);
        carsCurrentlyMoving.Add(piece);
        isAnimating = true;

        // A valid tap owns both actions: the car begins its exit and every
        // adjacent box begins rupturing in this same input call. Starting the
        // coroutine here (rather than after the car reaches the tray) makes
        // its first visual state visible on the tap frame.
        BeginAdjacentBoxReveals(piece);

        Vector3 trayTarget = Vector3.zero;
        if (!piece.IsTrash)
        {
            int trayIndex = InsertTrayPieceBesideMatchingColor(piece);
            trayTarget = GetTraySlotPosition(trayIndex);
            UpdateTrayHighlights();
            RefreshHud();
        }

        if (!piece.IsTrash)
            StartCoroutine(AnimateTrayLayout(0.11f, true));
        StartCoroutine(ExitBoard(piece, trayTarget));
    }

    private bool TryBeginFullTrayDominantExchange(CarPuzzlePiece incomingPiece)
    {
        if (!TryGetFullTrayDominantExchange(
                incomingPiece,
                out CarPuzzlePiece outgoingPiece,
                out int trayIndex))
            return false;

        incomingPiece.BeginExitPoliceLights();
        CarPrototypeFeedback.CarMove();
        boardPieces.Remove(incomingPiece);
        lastMovedPiece = incomingPiece;

        // Reserve both destinations before either transform moves. From this
        // point the yellow/outlier owns the side bay and the incoming dominant
        // car owns exactly the tray bay it vacates; no transient fifth tray car
        // can be observed by input, matching, hints, or the no-move evaluator.
        trayPieces[trayIndex] = incomingPiece;
        parkedPiece = outgoingPiece;
        RegisterParkingEntry();

        boardCarsInTransit++;
        boardCarsCurrentlyDriving.Add(incomingPiece);
        carsCurrentlyMoving.Add(incomingPiece);
        carsCurrentlyMoving.Add(outgoingPiece);
        isAnimating = true;

        BeginAdjacentBoxReveals(incomingPiece);
        UpdateTrayHighlights();
        UpdateParkingHighlight();
        RefreshHud();

        // Own the shared tray mouth immediately, before either child coroutine
        // gets a frame. The outlier releases this broad gate only after its full
        // footprint has reached the left-side escape lane.
        sideRoadMotionGroupsInProgress++;
        StartCoroutine(AnimateFullTrayDominantExchange(
            incomingPiece,
            outgoingPiece,
            trayIndex));
        return true;
    }

    private bool TryGetFullTrayDominantExchange(
        CarPuzzlePiece incomingPiece,
        out CarPuzzlePiece outgoingPiece,
        out int outgoingTrayIndex)
    {
        outgoingPiece = null;
        outgoingTrayIndex = -1;
        if (incomingPiece == null
            || incomingPiece.IsTrash
            || trayPieces.Count != trayCapacity
            || trayCapacity < 3
            || parkedPiece != null
            || !CanEnterSideParking()
            || isClearingTrayMatch
            || boardCarsInTransit > 0
            || carsCurrentlyMoving.Count > 0
            || activeTrayTrafficClaims.Count > 0
            || sideRoadMotionGroupsInProgress > 0
            || !TryGetSideRoadGuideGeometry(
                GetSceneLayout(),
                out SideRoadGuideGeometry _))
            return false;

        PieceColor dominantColor = incomingPiece.PieceColor;
        if (!TryFindDominantExchangeOutlier(
                trayPieces,
                dominantColor,
                trayCapacity,
                GetMatchTarget(dominantColor),
                out outgoingPiece,
                out outgoingTrayIndex))
            return false;

        if (TryGetNextOrderedColor(out PieceColor orderedColor)
            && orderedColor != dominantColor)
            return false;

        return outgoingPiece != null && outgoingTrayIndex >= 0;
    }

    private static bool TryFindDominantExchangeOutlier(
        IList<CarPuzzlePiece> currentTray,
        PieceColor incomingColor,
        int capacity,
        int matchTarget,
        out CarPuzzlePiece outlier,
        out int outlierIndex)
    {
        outlier = null;
        outlierIndex = -1;
        if (currentTray == null
            || capacity < 3
            || currentTray.Count != capacity)
            return false;

        int dominantCount = 0;
        int outlierCount = 0;
        for (int index = 0; index < currentTray.Count; index++)
        {
            CarPuzzlePiece trayPiece = currentTray[index];
            if (trayPiece == null) return false;
            if (trayPiece.PieceColor == incomingColor)
            {
                dominantCount++;
                continue;
            }

            outlier = trayPiece;
            outlierIndex = index;
            outlierCount++;
        }

        // This mechanic is intentionally unambiguous: 3+1 on a four-slot tray
        // (or the equivalent N-1+1 layout), never a tie or a 2+1+1 choice.
        return outlierCount == 1
            && dominantCount == capacity - 1
            && dominantCount > outlierCount
            && dominantCount == matchTarget - 1;
    }

    private void QueueBoardPieceAfterTrayMatch(CarPuzzlePiece piece)
    {
        if (piece == null || queuedBoardPieces.Contains(piece)) return;

        // One completed group frees at most the tray's capacity. Limiting the
        // input buffer prevents later unrelated cars from moving automatically.
        if (queuedBoardPieces.Count >= trayCapacity) return;

        queuedBoardPieces.Add(piece);
        StartCoroutine(MoveQueuedBoardPieceWhenTrayIsReady(piece));
    }

    private IEnumerator MoveQueuedBoardPieceWhenTrayIsReady(CarPuzzlePiece piece)
    {
        while (trayPieces.Count >= trayCapacity)
        {
            if (piece == null || !boardPieces.Contains(piece))
            {
                queuedBoardPieces.Remove(piece);
                yield break;
            }

            yield return null;
        }

        queuedBoardPieces.Remove(piece);
        if (piece == null || !boardPieces.Contains(piece)) yield break;
        TryMovePiece(piece);
    }

    private bool HasCompletedTrayMatchWaiting()
    {
        if (TryGetNextOrderedColor(out PieceColor orderedColor))
            return HasCompletedTrayMatchForColor(orderedColor);

        for (int index = 0; index < PlayableColors.Length; index++)
        {
            PieceColor color = PlayableColors[index];
            int target = GetMatchTarget(color);
            if (target > 0
                && !IsColorCleared(color)
                && FindMatchingCars(color, RequiresAdjacency(color)).Count >= target)
                return true;
        }

        return false;
    }

    private bool HasCompletedTrayMatchForColor(PieceColor color)
    {
        int target = GetMatchTarget(color);
        if (target <= 0 || IsColorCleared(color)) return false;
        return FindMatchingCars(color, RequiresAdjacency(color)).Count >= target;
    }

    private bool IsExitPathClear(CarPuzzlePiece piece)
    {
        return !IsExitBlockedByGarage(piece) && FindFirstExitBlocker(piece) == null;
    }

    private bool IsExitBlockedByGarage(CarPuzzlePiece piece)
    {
        Vector2Int step = DirectionToGridStep(piece.Direction);
        int row = piece.Row + step.y;
        int col = piece.Col + step.x;
        while (row >= 0 && row < activeBoardSize && col >= 0 && col < activeBoardSize)
        {
            for (int index = 0; index < activeGarages.Count; index++)
            {
                GarageSpec garage = activeGarages[index].specification;
                if (garage.row == row && garage.col == col) return true;
            }
            row += step.y;
            col += step.x;
        }
        return false;
    }

    private CarPuzzlePiece FindFirstExitBlocker(CarPuzzlePiece piece)
    {
        Vector2Int step = DirectionToGridStep(piece.Direction);
        int row = piece.Row + step.y;
        int col = piece.Col + step.x;

        while (row >= 0 && row < activeBoardSize && col >= 0 && col < activeBoardSize)
        {
            for (int index = 0; index < boardPieces.Count; index++)
            {
                CarPuzzlePiece blocker = boardPieces[index];
                if (blocker != piece && blocker.OccupiesCell(row, col))
                    return blocker;
            }

            row += step.y;
            col += step.x;
        }

        return null;
    }

    private IEnumerator ExitBoard(CarPuzzlePiece piece, Vector3 trayTarget)
    {
        Vector3 boardExit = GetBoardExitPosition(piece);
        if (piece.IsTrash)
        {
            yield return StartCoroutine(DriveRouteSegment(piece, boardExit));
        }
        else
        {
            float distance = Vector3.Distance(piece.transform.position, boardExit);
            float duration = Mathf.Clamp(distance / CarDriveSpeed, 0.035f, 0.38f);
            // Shrink limousines to their travel size while they are still in
            // their verified-clear row. This lets them turn outside the board
            // without their long body sweeping through a neighbouring car.
            IEnumerator boardExitMotion = piece.DriveToOutsideRoute(boardExit, duration);
            while (boardExitMotion.MoveNext()) yield return boardExitMotion.Current;
        }

        if (piece.IsTrash)
        {
            yield return StartCoroutine(piece.Despawn(0.16f));
            FinishBoardCarTransit(piece);
            yield break;
        }

        IEnumerator trayMotion = DriveCarToTray(piece, boardExit, trayTarget);
        while (trayMotion.MoveNext()) yield return trayMotion.Current;
        FinishBoardCarTransit(piece);
    }

    private IEnumerator AnimateFullTrayDominantExchange(
        CarPuzzlePiece incomingPiece,
        CarPuzzlePiece outgoingPiece,
        int trayIndex)
    {
        Vector3 boardExit = GetBoardExitPosition(incomingPiece);
        bool departureCleared = false;
        bool delayBoardExit = DominantExchangeDepartureOverlapsBoardExit(
            incomingPiece,
            boardExit,
            outgoingPiece);
        int completedMotions = 0;

        StartCoroutine(TrackParkingExchangeMotion(
            DriveDominantExchangeOutlierToParking(
                outgoingPiece,
                () => departureCleared = true),
            () => completedMotions++));
        StartCoroutine(TrackParkingExchangeMotion(
            DriveDominantExchangeBoardCarToTray(
                incomingPiece,
                boardExit,
                GetTraySlotPosition(trayIndex),
                () => !delayBoardExit || departureCleared),
            () => completedMotions++));

        while (completedMotions < 2) yield return null;

        CarPrototypeFeedback.CarParked();
        ReleaseNextGarageCar(incomingPiece);
        boardCarsCurrentlyDriving.Remove(incomingPiece);
        carsCurrentlyMoving.Remove(incomingPiece);
        carsCurrentlyMoving.Remove(outgoingPiece);
        boardCarsInTransit = Mathf.Max(0, boardCarsInTransit - 1);
        UpdateTrayHighlights();
        UpdateParkingHighlight();

        if (boardCarsInTransit > 0
            || boxRevealAnimationsInProgress > 0
            || isClearingTrayMatch)
        {
            isAnimating = true;
            RefreshHud();
            yield break;
        }

        CheckTrayMatches();
        RefreshHud();
    }

    private IEnumerator DriveDominantExchangeBoardCarToTray(
        CarPuzzlePiece piece,
        Vector3 boardExit,
        Vector3 trayTarget,
        System.Func<bool> mayStartBoardExit)
    {
        while (mayStartBoardExit != null && !mayStartBoardExit())
            yield return null;

        float distance = Vector3.Distance(piece.transform.position, boardExit);
        float duration = Mathf.Clamp(distance / CarDriveSpeed, 0.035f, 0.38f);
        IEnumerator boardExitMotion = piece.DriveToOutsideRoute(boardExit, duration);
        while (boardExitMotion.MoveNext()) yield return boardExitMotion.Current;

        IEnumerator trayMotion = DriveCarToTray(
            piece,
            boardExit,
            trayTarget,
            true);
        while (trayMotion.MoveNext()) yield return trayMotion.Current;
    }

    private IEnumerator DriveDominantExchangeOutlierToParking(
        CarPuzzlePiece piece,
        System.Action onDepartureCleared)
    {
        bool ownsBroadRoadGate = true;
        ActiveTrayTrafficClaim protectedSideLane = null;
        try
        {
            if (!TryGetSideRoadGuideGeometry(
                    GetSceneLayout(),
                    out SideRoadGuideGeometry road))
                throw new System.InvalidOperationException(
                    "The dominant-color exchange has no valid side-road geometry.");

            Vector3 parkingTarget = GetParkingSlotPosition();
            float routeHeight = parkingTarget.y;
            float safeRoadTopZ = GetSideRoadSafeTopZ(piece, road);
            Vector3 trayDeparture = new Vector3(
                piece.transform.position.x,
                routeHeight,
                safeRoadTopZ);
            Vector3 leftRoadTop = new Vector3(
                road.LeftGuideX,
                routeHeight,
                safeRoadTopZ);
            Vector3 leftRoadCorner = new Vector3(
                road.LeftGuideX,
                routeHeight,
                parkingTarget.z);

            // The outlier leaves the tray first through its own vertical bay,
            // then turns onto the left escape lane. The incoming car may move
            // elsewhere at the same time, but cannot reserve the shared tray
            // mouth until this complete footprint is clear.
            yield return StartCoroutine(DriveReverseRouteSegment(
                piece,
                trayDeparture,
                ParkingDriveSpeed));
            yield return StartCoroutine(DriveRouteSegment(
                piece,
                leftRoadTop,
                ParkingDriveSpeed));

            float clearanceRadius = GetDominantExchangeClearanceRadius(piece, false);
            protectedSideLane = new ActiveTrayTrafficClaim(
                piece,
                new[] { leftRoadTop, leftRoadCorner, parkingTarget },
                clearanceRadius);
            activeTrayTrafficClaims.Add(protectedSideLane);

            sideRoadMotionGroupsInProgress = Mathf.Max(
                0,
                sideRoadMotionGroupsInProgress - 1);
            ownsBroadRoadGate = false;
            onDepartureCleared?.Invoke();

            yield return StartCoroutine(DriveRoundedRouteToFinalApproach(
                piece,
                new List<Vector3> { leftRoadCorner, parkingTarget },
                ParkingDriveSpeed,
                road.curveRadius));
            yield return StartCoroutine(FinishParkingEntry(piece, parkingTarget));
        }
        finally
        {
            if (protectedSideLane != null)
                activeTrayTrafficClaims.Remove(protectedSideLane);
            if (ownsBroadRoadGate)
                sideRoadMotionGroupsInProgress = Mathf.Max(
                    0,
                    sideRoadMotionGroupsInProgress - 1);
        }
    }

    private bool DominantExchangeDepartureOverlapsBoardExit(
        CarPuzzlePiece incomingPiece,
        Vector3 boardExit,
        CarPuzzlePiece outgoingPiece)
    {
        if (!TryGetSideRoadGuideGeometry(
                GetSceneLayout(),
                out SideRoadGuideGeometry road))
            return true;

        Vector3 parkingTarget = GetParkingSlotPosition();
        float safeRoadTopZ = GetSideRoadSafeTopZ(outgoingPiece, road);
        Vector3 trayDeparture = new Vector3(
            outgoingPiece.transform.position.x,
            parkingTarget.y,
            safeRoadTopZ);
        Vector3 leftRoadTop = new Vector3(
            road.LeftGuideX,
            parkingTarget.y,
            safeRoadTopZ);
        return TrayTrafficCenterlinesOverlap(
            new[] { incomingPiece.transform.position, boardExit },
            GetDominantExchangeClearanceRadius(incomingPiece, true),
            new[] { outgoingPiece.transform.position, trayDeparture, leftRoadTop },
            GetDominantExchangeClearanceRadius(outgoingPiece, false));
    }

    private float GetDominantExchangeClearanceRadius(
        CarPuzzlePiece piece,
        bool includeBoardPose)
    {
        if (piece == null) return 0.20f;
        float clearanceRadius = GetOutsideRouteTurnRadius(piece);
        if (includeBoardPose)
        {
            float halfLength = piece.CellLength > 1 ? 2.275f : 1.05f;
            const float halfWidth = 0.775f;
            float boardRadius = Mathf.Sqrt(
                halfLength * halfLength + halfWidth * halfWidth)
                * GetBoardPieceScale();
            clearanceRadius = Mathf.Max(clearanceRadius, boardRadius);
        }

        // This is an actual air gap, not just a non-intersection test. At the
        // exact threshold both cars still retain ten centimetres of clearance.
        return clearanceRadius + 0.10f;
    }

    private void FinishBoardCarTransit(CarPuzzlePiece piece)
    {
        if (piece != null && !piece.IsTrash)
            CarPrototypeFeedback.CarParked();
        ReleaseNextGarageCar(piece);
        boardCarsCurrentlyDriving.Remove(piece);
        carsCurrentlyMoving.Remove(piece);
        boardCarsInTransit = Mathf.Max(0, boardCarsInTransit - 1);
        if (boardCarsInTransit > 0)
        {
            StartCoroutine(AnimateTrayLayout(0.10f, true));
            RefreshHud();
            return;
        }

        if (boxRevealAnimationsInProgress > 0)
        {
            isAnimating = true;
            RefreshHud();
            return;
        }

        // A previous color may still be playing its celebration while newly
        // released cars arrive. Let that clear finish, then evaluate the new
        // tray once so overlapping clear coroutines cannot fight each other.
        if (isClearingTrayMatch)
        {
            RefreshHud();
            return;
        }

        CheckTrayMatches();
        if (!isAnimating) UpdateTrayHighlights();
        RefreshHud();
    }

    private void BeginAdjacentBoxReveals(CarPuzzlePiece removedPiece)
    {
        if (removedPiece == null) return;
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece hiddenPiece = boardPieces[index];
            if (hiddenPiece == null
                || !hiddenPiece.IsRevealCovered
                || !IsOrthogonallyAdjacent(hiddenPiece, removedPiece))
                continue;
            boxRevealAnimationsInProgress++;
            StartCoroutine(RevealMysteryBox(hiddenPiece));
        }
    }

    private static bool IsOrthogonallyAdjacent(
        CarPuzzlePiece centerPiece,
        CarPuzzlePiece neighborPiece)
    {
        if (centerPiece == null || neighborPiece == null || centerPiece == neighborPiece)
            return false;

        int row = centerPiece.Row;
        int col = centerPiece.Col;
        return neighborPiece.OccupiesCell(row - 1, col)
            || neighborPiece.OccupiesCell(row + 1, col)
            || neighborPiece.OccupiesCell(row, col - 1)
            || neighborPiece.OccupiesCell(row, col + 1);
    }

    private IEnumerator RevealMysteryBox(CarPuzzlePiece hiddenPiece)
    {
        yield return StartCoroutine(hiddenPiece.RevealFromBox());
        boxRevealAnimationsInProgress = Mathf.Max(0, boxRevealAnimationsInProgress - 1);
        if (boxRevealAnimationsInProgress > 0 || boardCarsInTransit > 0) yield break;

        if (!isClearingTrayMatch) CheckTrayMatches();
        isAnimating = isClearingTrayMatch;
        UpdateTutorialPieceDimming();
        RefreshHud();
    }

    private void UpdateTutorialStep()
    {
        if (isDemoMode) return;

        switch (BoardNumber)
        {
            case 1:
                if (tutorialStep == 0 && FindBoardPiece(1, 0, PieceColor.Red) == null) tutorialStep = 1;
                if (tutorialStep == 1 && FindBoardPiece(0, 0, PieceColor.Red) == null) tutorialStep = 2;
                break;
            case 2:
                if (tutorialStep == 0 && FindBoardPiece(2, 1, PieceColor.Trash) == null) tutorialStep = 1;
                break;
            case 5:
                if (tutorialStep == 0 && FindBoardPiece(2, 0, PieceColor.Blue) == null) tutorialStep = 1;
                if (tutorialStep == 1 && FindBoardPiece(2, 1, PieceColor.Blue) == null) tutorialStep = 2;
                if (tutorialStep == 2 && FindBoardPiece(2, 2, PieceColor.Yellow) == null) tutorialStep = 3;
                if (tutorialStep == 3 && parkedPiece != null && parkedPiece.PieceColor == PieceColor.Yellow) tutorialStep = 4;
                if (tutorialStep == 4 && FindBoardPiece(1, 2, PieceColor.Blue) == null) tutorialStep = 5;
                if (tutorialStep == 5 && blueCleared >= activeBlueTarget) tutorialStep = 6;
                if (tutorialStep == 6 && parkedPiece == null) tutorialStep = 7;
                break;
        }
    }

    private bool IsGuidedTutorialSequenceActive()
    {
        if (levelEditMode || isDemoMode) return false;

        switch (BoardNumber)
        {
            case 1: return tutorialStep < 2;
            case 2: return tutorialStep < 1;
            case 5: return tutorialStep < 7;
            default: return false;
        }
    }

    private bool IsTutorialInteractionAllowed(CarPuzzlePiece piece)
    {
        UpdateTutorialStep();
        if (!IsGuidedTutorialSequenceActive()) return true;

        CarPuzzlePiece focusPiece = GetTutorialFocusPiece();
        return focusPiece != null && piece == focusPiece;
    }

    private CarPuzzlePiece GetTutorialFocusPiece()
    {
        switch (BoardNumber)
        {
            case 1:
                if (tutorialStep == 0) return FindBoardPiece(1, 0, PieceColor.Red);
                if (tutorialStep == 1) return FindBoardPiece(0, 0, PieceColor.Red);
                break;
            case 2:
                if (tutorialStep == 0) return FindBoardPiece(2, 1, PieceColor.Trash);
                break;
            case 5:
                if (tutorialStep == 0) return FindBoardPiece(2, 0, PieceColor.Blue);
                if (tutorialStep == 1) return FindBoardPiece(2, 1, PieceColor.Blue);
                if (tutorialStep == 2) return FindBoardPiece(2, 2, PieceColor.Yellow);
                if (tutorialStep == 3) return FindTrayPiece(PieceColor.Yellow);
                if (tutorialStep == 4) return FindBoardPiece(1, 2, PieceColor.Blue);
                if (tutorialStep == 6) return parkedPiece;
                break;
        }

        return null;
    }

    private void GetTutorialCopy(out string title, out string message)
    {
        title = string.Empty;
        message = string.Empty;

        switch (BoardNumber)
        {
            case 1:
                if (tutorialStep < 2)
                {
                    title = "MATCH A PAIR";
                    message = "Tap 2 cars of the same color.";
                }
                break;
            case 2:
                if (tutorialStep == 0)
                {
                    title = "POLICE CAR";
                    message = "Move the police car out of the way. It is an obstacle and does not join a color match.";
                }
                break;
            case 5:
                title = tutorialStep == 3 ? "SIDE PARKING" : tutorialStep == 6 ? "BRING IT BACK" : "MAKE ROOM";
                message = tutorialStep == 3
                    ? "Tap the yellow car in the tray to park it temporarily."
                    : tutorialStep == 6
                        ? "Tap the parked yellow car to return it to the normal tray."
                    : tutorialStep < 5
                        ? "Follow the cars. You do not need to keep every color in the tray."
                        : string.Empty;
                break;
        }
    }

    private CarPuzzlePiece FindBoardPiece(int row, int col, PieceColor color)
    {
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece != null && piece.Row == row && piece.Col == col && piece.PieceColor == color)
                return piece;
        }

        return null;
    }

    private CarPuzzlePiece FindTrayPiece(PieceColor color)
    {
        for (int index = 0; index < trayPieces.Count; index++)
        {
            CarPuzzlePiece piece = trayPieces[index];
            if (piece != null && piece.PieceColor == color)
                return piece;
        }

        return null;
    }

    private void UpdateGameplayHint()
    {
        if (gameplayHintKind == GameplayHintKind.BlockingCar
            && Time.unscaledTime >= gameplayHintExpiresAt)
            ClearGameplayHint();

        if (gameplayHintPiece != null)
        {
            if (!allPieces.Contains(gameplayHintPiece)
                || !boardPieces.Contains(gameplayHintPiece)
                || !gameplayHintPiece.IsTouchable)
                ClearGameplayHint();
            else
                return;
        }

        if (outcomeResolved
            || Time.timeScale == 0f
            || levelEditMode
            || isDemoMode
            || IsGuidedTutorialSequenceActive()
            || isAnimating
            || isClearingTrayMatch
            || boardCarsInTransit > 0
            || boxRevealAnimationsInProgress > 0
            || garageTransitionsInProgress > 0
            || Time.unscaledTime < nextAutomaticHintTime)
            return;

        // Schedule the next attempt before running the solver. If the current
        // position has no solver-proven hint, this prevents an expensive search
        // from repeating every frame on a phone.
        nextAutomaticHintTime = Time.unscaledTime + AutomaticSafeMoveHintDelay;
        if (TryFindSolverApprovedHintPiece(out CarPuzzlePiece safePiece))
            ShowGameplayHint(safePiece, GameplayHintKind.SafeMove, 0f);
    }

    private void RegisterGameplayActivity()
    {
        nextAutomaticHintTime = Time.unscaledTime + AutomaticSafeMoveHintDelay;
        ClearGameplayHint();
    }

    private void ResetGameplayHintTimer()
    {
        ClearGameplayHint();
        nextAutomaticHintTime = Time.unscaledTime + AutomaticSafeMoveHintDelay;
    }

    private void ShowGameplayHint(
        CarPuzzlePiece piece,
        GameplayHintKind kind,
        float duration)
    {
        if (piece == null
            || kind == GameplayHintKind.None
            || !boardPieces.Contains(piece))
            return;
        gameplayHintPiece = piece;
        gameplayHintKind = kind;
        gameplayHintExpiresAt = duration > 0f
            ? Time.unscaledTime + duration
            : float.PositiveInfinity;
    }

    private void ClearGameplayHint()
    {
        gameplayHintPiece = null;
        gameplayHintKind = GameplayHintKind.None;
        gameplayHintExpiresAt = 0f;
    }

    private void UpdateTutorialPieceDimming()
    {
        bool guideMovability = !isDemoMode && BoardNumber >= 1 && BoardNumber <= 10;
        bool guidedSequenceActive = IsGuidedTutorialSequenceActive();
        CarPuzzlePiece focusPiece = guideMovability ? GetTutorialFocusPiece() : null;

        for (int index = 0; index < allPieces.Count; index++)
        {
            CarPuzzlePiece piece = allPieces[index];
            if (piece == null) continue;

            bool dimmed = false;
            bool canMove = false;
            if (guidedSequenceActive)
            {
                // A transition with no current focus deliberately dims every
                // car until the next authored hand target becomes available.
                dimmed = piece != focusPiece;
            }
            else if (guideMovability)
            {
                if (boardPieces.Contains(piece))
                    dimmed = !IsExitPathClear(piece);
            }

            // Cars in the normal tray or side bay are safely parked, not
            // traffic-blocked. Only a board car with no clear exit closes its
            // eyes; parked cars remain awake and keep their normal blink.
            canMove = !piece.IsLocked
                && !piece.IsRevealCovered
                && (!boardPieces.Contains(piece) || IsExitPathClear(piece))
                && (!guidedSequenceActive || piece == focusPiece);

            piece.SetTutorialDimmed(dimmed);
            piece.SetEyesCanMove(canMove);
        }
    }

    // Side parking is deliberately introduced as a new mechanic on Level 5.
    // Earlier tutorial boards neither display the bay nor allow parking actions.
    private bool IsSideParkingAvailable()
    {
        return isDemoMode || BoardNumber >= 5;
    }

    private sealed class TrayRoutePlan
    {
        internal readonly List<Vector3> waypoints;
        internal readonly float cornerRadius;
        internal readonly float score;

        internal TrayRoutePlan(List<Vector3> routeWaypoints, float routeCornerRadius, float routeScore)
        {
            waypoints = routeWaypoints;
            cornerRadius = routeCornerRadius;
            score = routeScore;
        }
    }

    private sealed class ActiveTrayTrafficClaim
    {
        internal readonly CarPuzzlePiece piece;
        internal readonly Vector3[] centerline;
        internal readonly float halfWidth;

        internal ActiveTrayTrafficClaim(
            CarPuzzlePiece claimedPiece,
            Vector3[] claimedCenterline,
            float claimedHalfWidth)
        {
            piece = claimedPiece;
            centerline = claimedCenterline;
            halfWidth = claimedHalfWidth;
        }
    }

    private sealed class TrayTrafficReservation
    {
        internal ActiveTrayTrafficClaim claim;
        internal bool cancelled;
        internal float minimumHalfWidth;
    }

    private IEnumerator DriveCarToTray(
        CarPuzzlePiece piece,
        Vector3 boardExit,
        Vector3 trayTarget,
        bool protectFullVehicleFootprint = false)
    {
        if (piece == null) yield break;
        var traffic = new TrayTrafficReservation
        {
            minimumHalfWidth = protectFullVehicleFootprint
                ? GetOutsideRouteTurnRadius(piece) + 0.10f
                : 0f
        };
        try
        {
            int currentTrayIndex = trayPieces.IndexOf(piece);
            if (currentTrayIndex >= 0)
                trayTarget = GetTraySlotPosition(currentTrayIndex);

            TrayRoutePlan routePlan = BuildTrayRoutePlan(
                piece,
                boardExit,
                trayTarget,
                currentTrayIndex);
            List<Vector3> continuousPath = BuildContinuousTrayPath(
                piece.transform.position,
                routePlan.waypoints,
                routePlan.cornerRadius);
            IEnumerator continuousMotion = piece.DriveContinuousTrayPath(
                continuousPath,
                OutsideCarDriveSpeed,
                CarDriveSpeed * ContinuousRouteFinalSpeedRatio,
                matchVfx,
                centerline => TryAcquireTrayTrafficClaim(
                    piece,
                    traffic,
                    centerline));
            while (continuousMotion.MoveNext()) yield return continuousMotion.Current;
            if (traffic.cancelled) yield break;

            // The car is physically inside its bay now. Release the road before
            // the cosmetic squash so the next accepted car never waits for an
            // animation that no longer occupies the traffic corridor.
            ReleaseTrayTrafficClaim(traffic);
            IEnumerator settleMotion = piece.PlayTrayArrivalSettle(0.040f);
            while (settleMotion.MoveNext()) yield return settleMotion.Current;
        }
        finally
        {
            ReleaseTrayTrafficClaim(traffic);
        }
    }

    private static List<Vector3> BuildContinuousTrayPath(
        Vector3 start,
        List<Vector3> waypoints,
        float cornerRadius)
    {
        var cleanWaypoints = new List<Vector3>();
        Vector3 previous = start;
        for (int index = 0; index < waypoints.Count; index++)
        {
            if (Vector3.Distance(previous, waypoints[index]) < 0.025f) continue;
            cleanWaypoints.Add(waypoints[index]);
            previous = waypoints[index];
        }

        var path = new List<Vector3> { start };
        previous = start;
        for (int index = 0; index < cleanWaypoints.Count; index++)
        {
            Vector3 corner = cleanWaypoints[index];
            if (index >= cleanWaypoints.Count - 1)
            {
                AddContinuousLinePoints(path, corner);
                break;
            }

            Vector3 next = cleanWaypoints[index + 1];
            Vector3 incoming = corner - previous;
            Vector3 outgoing = next - corner;
            if (incoming.sqrMagnitude < 0.0025f || outgoing.sqrMagnitude < 0.0025f)
            {
                AddContinuousLinePoints(path, corner);
                previous = corner;
                continue;
            }

            Vector3 incomingDirection = incoming.normalized;
            Vector3 outgoingDirection = outgoing.normalized;
            float directionDot = Mathf.Clamp(
                Vector3.Dot(incomingDirection, outgoingDirection),
                -1f,
                1f);
            if (directionDot > 0.997f)
            {
                AddContinuousLinePoints(path, corner);
                previous = corner;
                continue;
            }

            float turnAngle = Mathf.Acos(directionDot);
            float trimDistance = Mathf.Min(
                cornerRadius,
                incoming.magnitude * 0.42f,
                outgoing.magnitude * 0.42f);
            Vector3 entry = corner - incomingDirection * trimDistance;
            Vector3 exit = corner + outgoingDirection * trimDistance;
            AddContinuousLinePoints(path, entry);

            float safeTurnAngle = Mathf.Min(turnAngle, 170f * Mathf.Deg2Rad);
            float halfAngleTangent = Mathf.Max(0.001f, Mathf.Tan(safeTurnAngle * 0.5f));
            float circleRadius = trimDistance / halfAngleTangent;
            float handleLength = (4f / 3f)
                * circleRadius
                * Mathf.Tan(safeTurnAngle * 0.25f);
            Vector3 firstControl = entry + incomingDirection * handleLength;
            Vector3 secondControl = exit - outgoingDirection * handleLength;
            Vector3[] curve = BuildCubicTrafficCenterline(
                entry,
                firstControl,
                secondControl,
                exit);
            for (int sample = 1; sample < curve.Length; sample++)
                AddContinuousPathPoint(path, curve[sample]);
            previous = exit;
        }

        return path;
    }

    private static void AddContinuousLinePoints(List<Vector3> path, Vector3 target)
    {
        if (path.Count == 0)
        {
            path.Add(target);
            return;
        }

        Vector3 start = path[path.Count - 1];
        float distance = Vector3.Distance(start, target);
        if (distance < 0.015f) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.16f));
        for (int step = 1; step <= steps; step++)
            AddContinuousPathPoint(path, Vector3.Lerp(start, target, step / (float)steps));
    }

    private static void AddContinuousPathPoint(List<Vector3> path, Vector3 point)
    {
        if (path.Count > 0 && Vector3.Distance(path[path.Count - 1], point) < 0.015f)
            return;
        path.Add(point);
    }

    private bool TryAcquireTrayTrafficClaim(
        CarPuzzlePiece piece,
        TrayTrafficReservation reservation,
        Vector3[] centerline)
    {
        if (reservation == null || centerline == null || centerline.Length < 2)
        {
            if (reservation != null) reservation.cancelled = true;
            return true;
        }

        if (piece == null || !trayPieces.Contains(piece))
        {
            reservation.cancelled = true;
            return true;
        }

        float halfWidth = Mathf.Max(
            GetTrayTrafficHalfWidth(piece),
            reservation.minimumHalfWidth);
        Vector3[] sweptCenterline = ExpandTrayTrafficCenterline(piece, centerline);
        if (sideRoadMotionGroupsInProgress > 0
            || HasConflictingTrayTrafficClaim(
                piece,
                reservation.claim,
                sweptCenterline,
                halfWidth))
            return false;

        var nextClaim = new ActiveTrayTrafficClaim(piece, sweptCenterline, halfWidth);
        activeTrayTrafficClaims.Add(nextClaim);
        if (reservation.claim != null)
            activeTrayTrafficClaims.Remove(reservation.claim);
        reservation.claim = nextClaim;
        return true;
    }

    private void ReleaseTrayTrafficClaim(TrayTrafficReservation reservation)
    {
        if (reservation == null || reservation.claim == null) return;
        activeTrayTrafficClaims.Remove(reservation.claim);
        reservation.claim = null;
    }

    private bool HasConflictingTrayTrafficClaim(
        CarPuzzlePiece piece,
        ActiveTrayTrafficClaim previousClaim,
        Vector3[] centerline,
        float halfWidth)
    {
        for (int index = activeTrayTrafficClaims.Count - 1; index >= 0; index--)
        {
            ActiveTrayTrafficClaim activeClaim = activeTrayTrafficClaims[index];
            if (activeClaim == null || activeClaim.piece == null)
            {
                activeTrayTrafficClaims.RemoveAt(index);
                continue;
            }

            if (activeClaim == previousClaim || activeClaim.piece == piece) continue;
            if (TrayTrafficCenterlinesOverlap(
                centerline,
                halfWidth,
                activeClaim.centerline,
                activeClaim.halfWidth))
                return true;
        }

        return false;
    }

    private float GetTrayTrafficHalfWidth(CarPuzzlePiece piece)
    {
        if (piece == null) return 0.1f;
        float travelScale = GetOffBoardPieceScale(piece.CellLength) * 0.68f;
        return 0.775f * travelScale + 0.025f;
    }

    private Vector3[] ExpandTrayTrafficCenterline(
        CarPuzzlePiece piece,
        Vector3[] centerline)
    {
        var expanded = (Vector3[])centerline.Clone();
        if (piece == null || expanded.Length < 2) return expanded;

        float travelScale = GetOffBoardPieceScale(piece.CellLength) * 0.68f;
        float halfLength = (piece.CellLength > 1 ? 2.275f : 1.05f) * travelScale;
        Vector3 startDirection = Vector3.ProjectOnPlane(expanded[1] - expanded[0], Vector3.up);
        Vector3 endDirection = Vector3.ProjectOnPlane(
            expanded[expanded.Length - 1] - expanded[expanded.Length - 2],
            Vector3.up);
        if (startDirection.sqrMagnitude > 0.0001f)
            expanded[0] -= startDirection.normalized * halfLength;
        if (endDirection.sqrMagnitude > 0.0001f)
            expanded[expanded.Length - 1] += endDirection.normalized * halfLength;
        return expanded;
    }

    private static Vector3[] BuildCubicTrafficCenterline(
        Vector3 start,
        Vector3 firstControl,
        Vector3 secondControl,
        Vector3 target)
    {
        const int segmentCount = 8;
        var centerline = new Vector3[segmentCount + 1];
        for (int index = 0; index <= segmentCount; index++)
        {
            float progress = index / (float)segmentCount;
            float remaining = 1f - progress;
            centerline[index] = remaining * remaining * remaining * start
                + 3f * remaining * remaining * progress * firstControl
                + 3f * remaining * progress * progress * secondControl
                + progress * progress * progress * target;
        }
        return centerline;
    }

    private static bool TrayTrafficCenterlinesOverlap(
        Vector3[] first,
        float firstHalfWidth,
        Vector3[] second,
        float secondHalfWidth)
    {
        float requiredDistance = firstHalfWidth + secondHalfWidth;
        float requiredDistanceSquared = requiredDistance * requiredDistance;
        for (int firstIndex = 0; firstIndex < first.Length - 1; firstIndex++)
        {
            Vector2 firstStart = new Vector2(first[firstIndex].x, first[firstIndex].z);
            Vector2 firstEnd = new Vector2(first[firstIndex + 1].x, first[firstIndex + 1].z);
            for (int secondIndex = 0; secondIndex < second.Length - 1; secondIndex++)
            {
                Vector2 secondStart = new Vector2(second[secondIndex].x, second[secondIndex].z);
                Vector2 secondEnd = new Vector2(second[secondIndex + 1].x, second[secondIndex + 1].z);
                if (SegmentDistanceSquared(firstStart, firstEnd, secondStart, secondEnd)
                    <= requiredDistanceSquared)
                    return true;
            }
        }

        return false;
    }

    private static float SegmentDistanceSquared(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd)
    {
        if (SegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd)) return 0f;
        return Mathf.Min(
            Mathf.Min(
                PointSegmentDistanceSquared(firstStart, secondStart, secondEnd),
                PointSegmentDistanceSquared(firstEnd, secondStart, secondEnd)),
            Mathf.Min(
                PointSegmentDistanceSquared(secondStart, firstStart, firstEnd),
                PointSegmentDistanceSquared(secondEnd, firstStart, firstEnd)));
    }

    private static bool SegmentsIntersect(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd)
    {
        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        float denominator = Cross2D(firstDirection, secondDirection);
        Vector2 offset = secondStart - firstStart;
        if (Mathf.Abs(denominator) < 0.00001f)
        {
            if (Mathf.Abs(Cross2D(offset, firstDirection)) > 0.00001f) return false;
            float firstLengthSquared = firstDirection.sqrMagnitude;
            if (firstLengthSquared < 0.00001f)
                return (firstStart - secondStart).sqrMagnitude < 0.00001f;
            float startProjection = Vector2.Dot(offset, firstDirection) / firstLengthSquared;
            float endProjection = startProjection
                + Vector2.Dot(secondDirection, firstDirection) / firstLengthSquared;
            return Mathf.Max(Mathf.Min(startProjection, endProjection), 0f)
                <= Mathf.Min(Mathf.Max(startProjection, endProjection), 1f);
        }

        float firstProgress = Cross2D(offset, secondDirection) / denominator;
        float secondProgress = Cross2D(offset, firstDirection) / denominator;
        return firstProgress >= 0f && firstProgress <= 1f
            && secondProgress >= 0f && secondProgress <= 1f;
    }

    private static float Cross2D(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private static float PointSegmentDistanceSquared(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.00001f) return (point - segmentStart).sqrMagnitude;
        float progress = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
        return (point - (segmentStart + segment * progress)).sqrMagnitude;
    }

    private static bool IsUninterruptedStraightTrayRoute(
        TrayRoutePlan routePlan,
        Vector3 start,
        Vector3 trayTarget)
    {
        const float laneAlignmentTolerance = 0.025f;
        if (routePlan == null
            || routePlan.waypoints == null
            || routePlan.waypoints.Count < 2
            || trayTarget.z >= start.z - 0.01f
            || Mathf.Abs(start.x - trayTarget.x) > laneAlignmentTolerance)
            return false;

        float previousZ = start.z;
        for (int index = 0; index < routePlan.waypoints.Count; index++)
        {
            Vector3 waypoint = routePlan.waypoints[index];
            if (Mathf.Abs(waypoint.x - trayTarget.x) > laneAlignmentTolerance
                || waypoint.z > previousZ + 0.01f)
                return false;
            previousZ = waypoint.z;
        }

        return true;
    }

    private TrayRoutePlan BuildTrayRoutePlan(
        CarPuzzlePiece piece,
        Vector3 boardExit,
        Vector3 trayTarget,
        int targetTrayIndex)
    {
        float routeHeight = trayTarget.y;
        Vector3 start = new Vector3(boardExit.x, routeHeight, boardExit.z);
        float sweepRadius = GetOutsideRouteTurnRadius(piece);
        float cornerRadius = Mathf.Max(RouteCornerRadius, sweepRadius * 0.82f);
        float curbTopZ = GetTrayCurbTopZ();
        float gateZ = curbTopZ + sweepRadius + 0.14f;
        float boardSafeLaneZ = GetExpandedBoardBottomZ(sweepRadius) - 0.04f;
        float laneChangeZ = Mathf.Min(gateZ + cornerRadius, boardSafeLaneZ);
        if (piece.Direction == ExitDirection.Down)
        {
            // Large boards leave a shorter strip between their bottom row and
            // the tray. Never send a downward car back toward that row just to
            // obtain the preferred turn radius; use the clear strip it has
            // already entered and let the corner radius contract naturally.
            const float gateLaneMargin = 0.025f;
            float preferredProgress = Mathf.Clamp(sweepRadius * 0.20f, 0.06f, 0.10f);
            float availableProgress = Mathf.Max(0f, start.z - (gateZ + gateLaneMargin));
            float forwardProgress = Mathf.Min(preferredProgress, availableProgress);
            laneChangeZ = Mathf.Min(laneChangeZ, start.z - forwardProgress);
            laneChangeZ = Mathf.Max(laneChangeZ, gateZ + gateLaneMargin);
        }
        float targetX = trayTarget.x;
        var candidates = new List<TrayRoutePlan>();

        // A downward car whose board column maps to the destination bay takes
        // the direct route. It may make a small smooth alignment correction in
        // open asphalt, but never visits a perimeter lane.
        int nearestSlot = GetNearestTraySlotIndex(start.x);
        bool sameDestinationLane = targetTrayIndex >= 0 && nearestSlot == targetTrayIndex;
        if (piece.Direction == ExitDirection.Down && sameDestinationLane)
        {
            // On compact boards the fully swept curb gate can sit slightly
            // behind the already-cleared board exit. A car that needs no lane
            // change must never reverse toward that gate and then visit a side
            // lane. Put its straight-approach gate ahead of its current travel
            // direction; no turning sweep is needed on this vertical section.
            const float directForwardProgress = 0.08f;
            float directGateZ = Mathf.Min(gateZ, start.z - directForwardProgress);
            AddTrayRouteCandidate(
                candidates,
                start,
                trayTarget,
                directGateZ,
                sweepRadius,
                cornerRadius,
                new List<Vector3>
                {
                    new Vector3(targetX, routeHeight, directGateZ),
                    trayTarget
                });
        }

        // Destination-first staged route. Downward cars change lanes only in
        // the clear strip above the curb, then become perfectly vertical before
        // crossing the top of the parking artwork.
        if (piece.Direction == ExitDirection.Down)
        {
            AddTrayRouteCandidate(
                candidates,
                start,
                trayTarget,
                gateZ,
                sweepRadius,
                cornerRadius,
                BuildStagedTrayWaypoints(start.x, targetX, routeHeight, laneChangeZ, gateZ, trayTarget));
        }

        // Side and upward exits must clear the board perimeter. Evaluate both
        // sides and let route length plus turn count choose the appropriate one
        // for the actual destination instead of choosing from current X alone.
        float routeSideX = GetVisibleBoardRouteLaneX(piece);
        AddTrayRouteCandidate(
            candidates,
            start,
            trayTarget,
            gateZ,
            sweepRadius,
            cornerRadius,
            BuildPerimeterTrayWaypoints(
                start,
                -routeSideX,
                targetX,
                routeHeight,
                laneChangeZ,
                gateZ,
                trayTarget));
        AddTrayRouteCandidate(
            candidates,
            start,
            trayTarget,
            gateZ,
            sweepRadius,
            cornerRadius,
            BuildPerimeterTrayWaypoints(
                start,
                routeSideX,
                targetX,
                routeHeight,
                laneChangeZ,
                gateZ,
                trayTarget));

        TrayRoutePlan best = null;
        for (int index = 0; index < candidates.Count; index++)
        {
            TrayRoutePlan candidate = candidates[index];
            if (best == null || candidate.score < best.score) best = candidate;
        }

        if (best != null) return best;

        // Never execute an unvalidated fallback: a failed invariant is safer
        // and diagnosable, whereas an improvised route can visibly cross a car,
        // divider, or sidewalk. Authored layouts are exhaustively verified.
        throw new System.InvalidOperationException($"No collision-free tray route for {piece.name}.");
    }

    private List<Vector3> BuildStagedTrayWaypoints(
        float startX,
        float targetX,
        float routeHeight,
        float laneChangeZ,
        float gateZ,
        Vector3 trayTarget)
    {
        return new List<Vector3>
        {
            new Vector3(startX, routeHeight, laneChangeZ),
            new Vector3(targetX, routeHeight, laneChangeZ),
            new Vector3(targetX, routeHeight, gateZ),
            trayTarget
        };
    }

    private List<Vector3> BuildPerimeterTrayWaypoints(
        Vector3 start,
        float sideX,
        float targetX,
        float routeHeight,
        float laneChangeZ,
        float gateZ,
        Vector3 trayTarget)
    {
        return new List<Vector3>
        {
            new Vector3(sideX, routeHeight, start.z),
            new Vector3(sideX, routeHeight, laneChangeZ),
            new Vector3(targetX, routeHeight, laneChangeZ),
            new Vector3(targetX, routeHeight, gateZ),
            trayTarget
        };
    }

    private void AddTrayRouteCandidate(
        List<TrayRoutePlan> candidates,
        Vector3 start,
        Vector3 trayTarget,
        float gateZ,
        float sweepRadius,
        float cornerRadius,
        List<Vector3> waypoints)
    {
        List<Vector3> cleaned = CleanRouteWaypoints(start, waypoints);
        if (cleaned.Count < 2) return;
        if (!IsTrayRouteCandidateSafe(start, cleaned, trayTarget, gateZ, sweepRadius)) return;

        float length = 0f;
        int turnCount = 0;
        Vector3 previous = start;
        Vector3 previousDirection = Vector3.zero;
        for (int index = 0; index < cleaned.Count; index++)
        {
            Vector3 delta = cleaned[index] - previous;
            float distance = delta.magnitude;
            if (distance > 0.001f)
            {
                Vector3 direction = delta / distance;
                length += distance;
                if (previousDirection.sqrMagnitude > 0.5f
                    && Vector3.Dot(previousDirection, direction) < 0.985f)
                    turnCount++;
                previousDirection = direction;
            }
            previous = cleaned[index];
        }

        // Prefer a short, calm route when two candidates are both clear.
        float score = length + turnCount * 0.36f;
        candidates.Add(new TrayRoutePlan(cleaned, cornerRadius, score));
    }

    private static List<Vector3> CleanRouteWaypoints(Vector3 start, List<Vector3> waypoints)
    {
        var cleaned = new List<Vector3>(waypoints.Count);
        Vector3 previous = start;
        for (int index = 0; index < waypoints.Count; index++)
        {
            Vector3 point = waypoints[index];
            if (Vector3.Distance(previous, point) < 0.035f) continue;
            cleaned.Add(point);
            previous = point;
        }
        return cleaned;
    }

    private bool IsTrayRouteCandidateSafe(
        Vector3 start,
        List<Vector3> waypoints,
        Vector3 trayTarget,
        float gateZ,
        float sweepRadius)
    {
        Vector3 previous = start;
        for (int waypointIndex = 0; waypointIndex < waypoints.Count; waypointIndex++)
        {
            Vector3 next = waypoints[waypointIndex];
            float distance = Vector3.Distance(previous, next);
            int samples = Mathf.Max(2, Mathf.CeilToInt(distance / 0.08f));
            for (int sampleIndex = 1; sampleIndex <= samples; sampleIndex++)
            {
                Vector3 point = Vector3.Lerp(previous, next, sampleIndex / (float)samples);
                if (point.z < gateZ - 0.01f
                    && Mathf.Abs(point.x - trayTarget.x) > 0.015f)
                    return false;
                if (IsInsideExpandedBoardRouteBounds(point, sweepRadius))
                    return false;
            }
            previous = next;
        }

        return true;
    }

    private bool IsInsideExpandedBoardRouteBounds(Vector3 point, float sweepRadius)
    {
        float spacing = GetBoardSpacing();
        float topZ = GetBoardFirstRowZ();
        float halfBoardWidth = (activeBoardSize - 1) * spacing * 0.5f;
        float stationaryHalfWidth = GetBoardPieceScale() * 0.775f;
        float stationaryHalfLength = GetBoardPieceScale() * 1.05f;
        float minX = -halfBoardWidth - stationaryHalfWidth - sweepRadius - 0.08f;
        float maxX = halfBoardWidth + stationaryHalfWidth + sweepRadius + 0.08f;
        float minZ = GetExpandedBoardBottomZ(sweepRadius);
        float maxZ = topZ + stationaryHalfLength + sweepRadius + 0.08f;
        return point.x > minX && point.x < maxX && point.z > minZ && point.z < maxZ;
    }

    private float GetExpandedBoardBottomZ(float sweepRadius)
    {
        float spacing = GetBoardSpacing();
        float topZ = GetBoardFirstRowZ();
        float bottomZ = topZ - (activeBoardSize - 1) * spacing;
        float stationaryHalfLength = GetBoardPieceScale() * 1.05f;
        return bottomZ - stationaryHalfLength - sweepRadius - 0.08f;
    }

    private int GetNearestTraySlotIndex(float x)
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;
        for (int index = 0; index < trayCapacity; index++)
        {
            float distance = Mathf.Abs(GetTraySlotPosition(index).x - x);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearestIndex = index;
        }
        return nearestIndex;
    }

    private float GetTrayCurbTopZ()
    {
        int visibleCapacity = Mathf.Clamp(trayCapacity, 2, MaximumTraySlots);
        float pixelsPerWorldUnit = ApprovedParkingBayPitchPixels
            / Mathf.Max(0.01f, GetMatchTraySlotSpacing(visibleCapacity));
        return GetSceneLayout().sceneMatchTrayPosition.y
            + ApprovedParkingBayCenterFromTopPixels / pixelsPerWorldUnit;
    }

    private Vector3 GetBoardExitPosition(CarPuzzlePiece piece)
    {
        float spacing = GetBoardSpacing();
        float topZ = GetBoardFirstRowZ();
        float bottomZ = topZ - (activeBoardSize - 1) * spacing;
        float boardForwardHalf = GetBoardPieceScale() * 1.05f;
        float movingTurnRadius = GetOutsideRouteTurnRadius(piece);
        float verticalClearance = Mathf.Max(spacing * 0.85f, boardForwardHalf + movingTurnRadius + 0.15f);
        float horizontalRouteX = GetVisibleBoardRouteLaneX(piece);

        switch (piece.Direction)
        {
            case ExitDirection.Up:
                return new Vector3(piece.transform.position.x, piece.transform.position.y, topZ + verticalClearance);
            case ExitDirection.Down:
                return new Vector3(piece.transform.position.x, piece.transform.position.y, bottomZ - verticalClearance);
            case ExitDirection.Left:
                return new Vector3(-horizontalRouteX, piece.transform.position.y, piece.transform.position.z);
            default:
                return new Vector3(horizontalRouteX, piece.transform.position.y, piece.transform.position.z);
        }
    }

    private float GetBoardRouteSideX(CarPuzzlePiece piece, float currentX)
    {
        float routeX = GetVisibleBoardRouteLaneX(piece);
        if (piece.Direction == ExitDirection.Left) return -routeX;
        if (piece.Direction == ExitDirection.Right) return routeX;
        return currentX < 0f ? -routeX : routeX;
    }

    private float GetVisibleBoardRouteLaneX(CarPuzzlePiece piece)
    {
        float spacing = GetBoardSpacing();
        float boardHalfWidth = (activeBoardSize - 1) * spacing * 0.5f;
        float stationaryCarHalfWidth = GetBoardPieceScale() * 0.775f;
        float desiredLane = boardHalfWidth
            + stationaryCarHalfWidth
            + GetOutsideRouteTurnRadius(piece)
            + 0.15f;
        // Never pull the lane inward to fit the camera: doing so makes the car
        // clip through the outer column. Camera framing is widened separately.
        return desiredLane;
    }

    private float GetOutsideRouteTurnRadius(CarPuzzlePiece piece)
    {
        float travelScale = GetOffBoardPieceScale(piece.CellLength) * 0.68f;
        float halfLength = piece.CellLength > 1 ? 2.275f : 1.05f;
        const float halfWidth = 0.775f;
        return Mathf.Sqrt(halfLength * halfLength + halfWidth * halfWidth) * travelScale;
    }

    private float GetMaximumOutsideRouteTurnRadius()
    {
        const float halfWidth = 0.775f;
        float regularRadius = Mathf.Sqrt(1.05f * 1.05f + halfWidth * halfWidth)
            * GetOffBoardPieceScale(1) * 0.68f;
        float limousineRadius = Mathf.Sqrt(2.275f * 2.275f + halfWidth * halfWidth)
            * GetOffBoardPieceScale(2) * 0.68f;
        return Mathf.Max(regularRadius, limousineRadius);
    }

    private float GetBoardFirstRowZ()
    {
        return float.IsNaN(activeBoardFirstRowZ)
            ? CalculateBoardFirstRowZ()
            : activeBoardFirstRowZ;
    }

    private float CalculateBoardFirstRowZ()
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        float authoredTopZ = currentLayout.sceneBoardFirstRowZ;

        // A taller board can extend so close to the parking entrance that a
        // full-size car physically cannot turn without overlapping either the
        // bottom row or the curb. Derive the smallest upward offset from the
        // actual largest travelling footprint. Small boards naturally produce
        // a zero offset; every layout keeps a real collision-free corridor.
        float spacing = GetBoardSpacing();
        float authoredBottomZ = authoredTopZ - (activeBoardSize - 1) * spacing;
        float stationaryHalfLength = GetBoardPieceScale() * 1.05f;
        float maximumSweepRadius = GetMaximumOutsideRouteTurnRadius();
        const float boardMargin = 0.08f;
        const float curbMargin = 0.14f;
        const float corridorMargin = 0.10f;
        float requiredBottomZ = GetTrayCurbTopZ()
            + stationaryHalfLength
            + maximumSweepRadius * 2f
            + boardMargin
            + curbMargin
            + corridorMargin;
        return authoredTopZ + Mathf.Max(0f, requiredBottomZ - authoredBottomZ);
    }

    private IEnumerator DriveRoundedRouteToFinalApproach(
        CarPuzzlePiece piece,
        List<Vector3> waypoints,
        float speed,
        float cornerRadius,
        TrayTrafficReservation traffic = null)
    {
        var cleanWaypoints = new List<Vector3>();
        Vector3 previous = piece.transform.position;
        for (int index = 0; index < waypoints.Count; index++)
        {
            if (Vector3.Distance(previous, waypoints[index]) < 0.04f) continue;
            cleanWaypoints.Add(waypoints[index]);
            previous = waypoints[index];
        }

        // The final point is deliberately left for DriveToTraySlot or
        // DriveToParkingSlot, which eases the car into its exact pose and size.
        for (int index = 0; index < cleanWaypoints.Count - 1; index++)
        {
            Vector3 corner = cleanWaypoints[index];
            Vector3 next = cleanWaypoints[index + 1];
            Vector3 incoming = corner - piece.transform.position;
            Vector3 outgoing = next - corner;

            if (incoming.sqrMagnitude < 0.0025f || outgoing.sqrMagnitude < 0.0025f)
                continue;

            Vector3 incomingDirection = incoming.normalized;
            Vector3 outgoingDirection = outgoing.normalized;
            float directionDot = Mathf.Clamp(Vector3.Dot(incomingDirection, outgoingDirection), -1f, 1f);

            // A practically straight waypoint does not need a manufactured
            // bend. Driving through it also avoids tiny steering twitches.
            if (directionDot > 0.997f)
            {
                if (traffic != null)
                {
                    while (!TryAcquireTrayTrafficClaim(
                        piece,
                        traffic,
                        new[] { piece.transform.position, corner }))
                        yield return null;
                    if (traffic.cancelled) yield break;
                }
                yield return StartCoroutine(DriveRouteSegment(piece, corner, speed));
                continue;
            }

            float turnAngle = Mathf.Acos(directionDot);
            float trimDistance = Mathf.Min(
                cornerRadius,
                incoming.magnitude * 0.42f,
                outgoing.magnitude * 0.42f);
            Vector3 entry = corner - incomingDirection * trimDistance;
            Vector3 exit = corner + outgoingDirection * trimDistance;
            if (traffic != null)
            {
                while (!TryAcquireTrayTrafficClaim(
                    piece,
                    traffic,
                    new[] { piece.transform.position, entry }))
                    yield return null;
                if (traffic.cancelled) yield break;
            }
            yield return StartCoroutine(DriveRouteSegment(piece, entry, speed));

            // Build a tangent-matched cubic approximation of a circular arc.
            // Both end tangents are identical to their adjoining road legs,
            // so neither position nor steering snaps at the joins.
            float safeTurnAngle = Mathf.Min(turnAngle, 170f * Mathf.Deg2Rad);
            float halfAngleTangent = Mathf.Max(0.001f, Mathf.Tan(safeTurnAngle * 0.5f));
            float circleRadius = trimDistance / halfAngleTangent;
            float handleLength = (4f / 3f)
                * circleRadius
                * Mathf.Tan(safeTurnAngle * 0.25f);
            Vector3 firstControl = entry + incomingDirection * handleLength;
            Vector3 secondControl = exit - outgoingDirection * handleLength;

            float arcLength = Mathf.Max(0.01f, safeTurnAngle * circleRadius);
            float curveDuration = Mathf.Clamp(
                arcLength / Mathf.Min(speed, NaturalCurveSpeed),
                MinimumNaturalCurveDuration,
                MaximumNaturalCurveDuration);
            if (traffic != null)
            {
                while (!TryAcquireTrayTrafficClaim(
                    piece,
                    traffic,
                    BuildCubicTrafficCenterline(
                        piece.transform.position,
                        firstControl,
                        secondControl,
                        exit)))
                    yield return null;
                if (traffic.cancelled) yield break;
            }
            yield return StartCoroutine(piece.DriveCubicCurve(
                firstControl,
                secondControl,
                exit,
                curveDuration));
        }
    }

    private IEnumerator DriveRouteSegment(CarPuzzlePiece piece, Vector3 target, float speed = CarDriveSpeed)
    {
        float distance = Vector3.Distance(piece.transform.position, target);
        if (distance < 0.035f) yield break;

        // Do not stretch a tiny connector into a visible slow segment. If the
        // calculated travel time is shorter than one rendered frame, complete
        // it on that frame and preserve the route's forward momentum.
        float duration = Mathf.Clamp(distance / Mathf.Max(1f, speed), 0.001f, 0.72f);
        yield return StartCoroutine(piece.DriveRouteStraight(target, duration));
    }

    private IEnumerator DriveReverseRouteSegment(
        CarPuzzlePiece piece,
        Vector3 target,
        float speed = CarDriveSpeed)
    {
        float distance = Vector3.Distance(piece.transform.position, target);
        if (distance < 0.035f) yield break;

        float duration = Mathf.Clamp(distance / Mathf.Max(1f, speed), 0.001f, 0.72f);
        yield return StartCoroutine(piece.DriveReverseTo(target, duration));
    }

    private void CheckTrayMatches()
    {
        // Tray membership reserves destinations immediately, before a vehicle
        // physically arrives. Never merge a logical reservation that is still
        // travelling; its completion path will request this check again.
        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (!carsCurrentlyMoving.Contains(trayPieces[index])) continue;
            isAnimating = true;
            RefreshHud();
            return;
        }

        if (TryGetNextOrderedColor(out PieceColor orderedColor))
        {
            int orderedTarget = GetMatchTarget(orderedColor);
            List<CarPuzzlePiece> orderedCars = FindMatchingCars(orderedColor, RequiresAdjacency(orderedColor));
            if (orderedTarget > 0 && orderedCars.Count >= orderedTarget)
                BeginTrayMatch(orderedColor, orderedCars);
            else
            {
                isAnimating = isClearingTrayMatch;
                RefreshHud();
            }
            return;
        }

        for (int index = 0; index < PlayableColors.Length; index++)
        {
            PieceColor color = PlayableColors[index];
            int target = GetMatchTarget(color);
            if (target <= 0 || IsColorCleared(color)) continue;
            List<CarPuzzlePiece> cars = FindMatchingCars(color, RequiresAdjacency(color));
            if (cars.Count < target) continue;
            BeginTrayMatch(color, cars);
            return;
        }

        isAnimating = isClearingTrayMatch;
        RefreshHud();
    }

    private void BeginTrayMatch(PieceColor color, List<CarPuzzlePiece> matchingCars)
    {
        switch (color)
        {
            case PieceColor.Red: redCleared = Mathf.Min(activeRedTarget, redCleared + activeMatchTarget); break;
            case PieceColor.Green: greenCleared = Mathf.Min(activeGreenTarget, greenCleared + activeMatchTarget); break;
            case PieceColor.Blue: blueCleared = Mathf.Min(activeBlueTarget, blueCleared + activeMatchTarget); break;
            case PieceColor.Purple: purpleCleared = Mathf.Min(activePurpleTarget, purpleCleared + activeMatchTarget); break;
            case PieceColor.Yellow: yellowCleared = Mathf.Min(activeYellowTarget, yellowCleared + activeMatchTarget); break;
            case PieceColor.Pink: pinkCleared = Mathf.Min(activePinkTarget, pinkCleared + activeMatchTarget); break;
        }

        UpdateExperimentalLocks();
        lastMovedPiece = null;
        isClearingTrayMatch = true;
        for (int index = 0; index < matchingCars.Count; index++)
        {
            if (matchingCars[index] != null)
                carsCurrentlyMoving.Add(matchingCars[index]);
        }
        RefreshHud();
        StartCoroutine(ClearMatchedCars(matchingCars));
    }

    private List<CarPuzzlePiece> FindMatchingCars(PieceColor color, bool requiresAdjacency)
    {
        int target = GetMatchTarget(color);
        var result = new List<CarPuzzlePiece>();
        if (target <= 0) return result;

        if (!requiresAdjacency)
        {
            for (int index = 0; index < trayPieces.Count && result.Count < target; index++)
            {
                if (trayPieces[index].PieceColor == color) result.Add(trayPieces[index]);
            }
            return result;
        }

        // The source database's special adjacent-match family must occupy
        // consecutive bays even when that family uses a different display
        // color on an early board.
        for (int start = 0; start <= trayPieces.Count - target; start++)
        {
            bool isMatch = true;
            for (int offset = 0; offset < target; offset++)
            {
                if (trayPieces[start + offset].PieceColor == color) continue;
                isMatch = false;
                break;
            }

            if (!isMatch) continue;
            for (int offset = 0; offset < target; offset++) result.Add(trayPieces[start + offset]);
            return result;
        }

        return result;
    }

    private int GetMatchTarget(PieceColor color)
    {
        switch (color)
        {
            case PieceColor.Red: return activeRedTarget > redCleared ? activeMatchTarget : 0;
            case PieceColor.Green: return activeGreenTarget > greenCleared ? activeMatchTarget : 0;
            case PieceColor.Blue: return activeBlueTarget > blueCleared ? activeMatchTarget : 0;
            case PieceColor.Purple: return activePurpleTarget > purpleCleared ? activeMatchTarget : 0;
            case PieceColor.Yellow: return activeYellowTarget > yellowCleared ? activeMatchTarget : 0;
            case PieceColor.Pink: return activePinkTarget > pinkCleared ? activeMatchTarget : 0;
            default: return 0;
        }
    }

    private bool RequiresAdjacency(PieceColor color)
    {
        return activeAdjacentMatchColors.Contains(color);
    }

    private int GetObjectiveRemaining(PieceColor color)
    {
        int matchTarget = GetMatchTarget(color);
        if (matchTarget <= 0) return 0;
        if (IsColorCleared(color)) return 0;

        int carsInTray = 0;
        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (trayPieces[index].PieceColor == color)
                carsInTray++;
        }

        return Mathf.Max(0, GetActiveTarget(color) - GetClearedCount(color) - carsInTray);
    }

    private int GetActiveTarget(PieceColor color)
    {
        switch (color)
        {
            case PieceColor.Red: return activeRedTarget;
            case PieceColor.Green: return activeGreenTarget;
            case PieceColor.Blue: return activeBlueTarget;
            case PieceColor.Purple: return activePurpleTarget;
            case PieceColor.Yellow: return activeYellowTarget;
            case PieceColor.Pink: return activePinkTarget;
            default: return 0;
        }
    }

    private int GetClearedCount(PieceColor color)
    {
        switch (color)
        {
            case PieceColor.Red: return redCleared;
            case PieceColor.Green: return greenCleared;
            case PieceColor.Blue: return blueCleared;
            case PieceColor.Purple: return purpleCleared;
            case PieceColor.Yellow: return yellowCleared;
            case PieceColor.Pink: return pinkCleared;
            default: return 0;
        }
    }

    private bool HasCompletedAllColorGoals()
    {
        bool redDone = activeRedTarget == 0 || redCleared >= activeRedTarget;
        bool greenDone = activeGreenTarget == 0 || greenCleared >= activeGreenTarget;
        bool blueDone = activeBlueTarget == 0 || blueCleared >= activeBlueTarget;
        bool purpleDone = activePurpleTarget == 0 || purpleCleared >= activePurpleTarget;
        bool yellowDone = activeYellowTarget == 0 || yellowCleared >= activeYellowTarget;
        bool pinkDone = activePinkTarget == 0 || pinkCleared >= activePinkTarget;
        return redDone && greenDone && blueDone && purpleDone && yellowDone && pinkDone;
    }

    private IEnumerator ClearMatchedCars(List<CarPuzzlePiece> matchingCars)
    {
        // Keep only a three-frame recognition beat. The previous 130 ms hold
        // made a rapid next command look ignored while the tray was full.
        yield return new WaitForSeconds(MatchAnticipationDuration);
        for (int index = 0; index < matchingCars.Count; index++)
            trayPieces.Remove(matchingCars[index]);

        yield return StartCoroutine(AnimateMatchedCarsMerge(matchingCars));
        for (int index = 0; index < matchingCars.Count; index++)
            carsCurrentlyMoving.Remove(matchingCars[index]);
        yield return StartCoroutine(AnimateTrayLayout(0.12f, true));
        UpdateTrayHighlights();

        if (HasCompletedAllColorGoals())
        {
            StartCoroutine(CompleteLevel());
            yield break;
        }

        isClearingTrayMatch = false;
        if (boardCarsInTransit == 0)
        {
            CheckTrayMatches();
            if (isClearingTrayMatch)
            {
                RefreshHud();
                yield break;
            }
        }

        isAnimating = boardCarsInTransit > 0;
        RefreshHud();
    }

    private IEnumerator AnimateMatchedCarsMerge(List<CarPuzzlePiece> matchingCars)
    {
        if (matchingCars == null || matchingCars.Count == 0)
            yield break;

        Vector3 groupCenter = Vector3.zero;
        for (int index = 0; index < matchingCars.Count; index++)
            groupCenter += matchingCars[index].transform.position;
        groupCenter /= matchingCars.Count;

        // Measured from the reference recording:
        // 109 ms lift/converge, 50 ms compression, 75 ms visible core hold,
        // then a 33 ms final disappearance at the peak flash.
        Vector3 mergeCenter = new Vector3(groupCenter.x, groupCenter.y + 0.78f, groupCenter.z + 0.36f);
        int coreIndex = matchingCars.Count / 2;
        const float liftAndConvergeDuration = 0.109f;
        const float compressionDuration = 0.050f;
        const float orbitDelay = 0.033f;
        const float compressedCoreHoldAfterOrbit = 0.042f;
        const float finalCoreCollapseDuration = 0.033f;
        float effectDiameter = CalculateMergeEffectDiameter(matchingCars);
        float overlapSpacing = Mathf.Clamp(effectDiameter * 0.48f, 0.54f, 0.74f);
        var formationTargets = new Vector3[matchingCars.Count];

        for (int index = 0; index < matchingCars.Count; index++)
        {
            float centeredIndex = index - (matchingCars.Count - 1) * 0.5f;
            formationTargets[index] = mergeCenter + new Vector3(
                centeredIndex * overlapSpacing,
                index == coreIndex ? 0.12f : 0f,
                index == coreIndex ? 0.20f : 0f);
            StartCoroutine(matchingCars[index].RiseIntoMergeFormation(
                formationTargets[index],
                liftAndConvergeDuration));
        }

        yield return new WaitForSeconds(liftAndConvergeDuration);

        CarPuzzlePiece coreCar = matchingCars[coreIndex];
        Vector3 finalMergePosition = mergeCenter + new Vector3(0f, 0.12f, 0.20f);
        for (int index = 0; index < matchingCars.Count; index++)
        {
            StartCoroutine(matchingCars[index].CompressIntoMergeCore(
                finalMergePosition,
                compressionDuration,
                index == coreIndex));
        }

        yield return new WaitForSeconds(compressionDuration);
        Color effectColor = GetMergeSparkleColor(coreCar.PieceColor);
        if (matchVfx != null)
            matchVfx.PlayMergeCore(finalMergePosition, effectColor, effectDiameter);

        yield return new WaitForSeconds(orbitDelay);
        if (matchVfx != null)
            matchVfx.PlayMergeOrbit(
                finalMergePosition,
                effectColor,
                effectDiameter,
                matchingCars.Count);

        yield return new WaitForSeconds(compressedCoreHoldAfterOrbit);
        yield return StartCoroutine(coreCar.CollapseMergeCore(finalCoreCollapseDuration));

        if (matchVfx != null)
            matchVfx.PlayMergePeak(finalMergePosition, effectColor, effectDiameter);
        // Sound and haptic land on the white flash, not on match detection.
        CarPrototypeFeedback.Match();

        // The compression routine already hides the outside cars. This final
        // pass also safely handles interrupted or one-car matches.
        for (int index = 0; index < matchingCars.Count; index++)
            matchingCars[index].SetVisible(false);
    }

    private static float CalculateMergeEffectDiameter(List<CarPuzzlePiece> matchingCars)
    {
        float largestPlanarSize = 0f;
        for (int carIndex = 0; carIndex < matchingCars.Count; carIndex++)
        {
            CarPuzzlePiece car = matchingCars[carIndex];
            if (car == null) continue;
            Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || !renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                largestPlanarSize = Mathf.Max(
                    largestPlanarSize,
                    Mathf.Max(bounds.size.x, bounds.size.z));
            }
        }

        // The aura is slightly smaller than one car length in the reference;
        // the fragment shell grows beyond it. Clamp limousines so their longer
        // mesh does not create a screen-filling flash.
        return Mathf.Clamp(largestPlanarSize, 1.55f, 3.15f);
    }

    internal static Color GetMergeSparkleColor(PieceColor color)
    {
        switch (color)
        {
            case PieceColor.Red: return new Color(1f, 0.18f, 0.12f);
            case PieceColor.Green: return new Color(0.25f, 1f, 0.18f);
            case PieceColor.Blue: return new Color(0.15f, 0.72f, 1f);
            case PieceColor.Purple: return new Color(0.82f, 0.20f, 1f);
            case PieceColor.Yellow: return new Color(1f, 0.82f, 0.12f);
            case PieceColor.Pink: return new Color(1f, 0.22f, 0.66f);
            default: return Color.white;
        }
    }

    private IEnumerator CompleteLevel()
    {
        outcomeResolved = true;
        isAnimating = true;
        RecordCampaignLevelCompleted();

        // The final match now hands off to the measured four-second Park Dash
        // logo construction and firework celebration. It runs on unscaled UI
        // time while gameplay is paused, then yields directly to the existing
        // Win UI without changing completion persistence or outcome controls.
        if (hud != null)
            yield return hud.PlayVictoryCelebration();

        isAnimating = false;
        isClearingTrayMatch = false;
        if (hud != null)
            hud.ShowVictory(false);
    }

    private void LoadLevel(int targetIndex)
    {
        List<PrototypeLevel> activeLevels = GetActiveLevels();
        if (activeLevels.Count == 0) return;

        StopAllCoroutines();
        isAnimating = false;
        isClearingTrayMatch = false;
        outcomeResolved = false;
        isEvaluatingAvailableMoves = false;
        boardCarsInTransit = 0;
        boxRevealAnimationsInProgress = 0;
        garageTransitionsInProgress = 0;
        activeTrayTrafficClaims.Clear();
        sideRoadMotionGroupsInProgress = 0;
        levelEditSwapSelection = null;
        levelEditCarGarageSelection = null;
        levelEditCarGarageSlot = -1;
        levelEditGarageSelection = null;
        levelEditBoxSelection = null;
        levelEditLastBoxVisualRotation = Quaternion.identity;
        levelEditHasBoxVisualRotation = false;
        queuedBoardPieces.Clear();
        boardCarsCurrentlyDriving.Clear();
        carsCurrentlyMoving.Clear();
        levelIndex = Mathf.Clamp(targetIndex, 0, activeLevels.Count - 1);
        if (!isDemoMode && levelIndex < CampaignLevelCount)
        {
            lastStandardLevelIndex = levelIndex;
            highestUnlockedLevelIndex = Mathf.Max(highestUnlockedLevelIndex, levelIndex);
            SaveCampaignProgress();
        }
        PrototypeLevel level = activeLevels[levelIndex];
        activeBoardSize = level.boardSize;
        activeMatchTarget = level.matchTarget;
        activeRedTarget = 0;
        activeGreenTarget = 0;
        activeBlueTarget = 0;
        activePurpleTarget = 0;
        activeYellowTarget = 0;
        activePinkTarget = 0;
        activeAdjacentMatchColors.Clear();
        redCleared = 0;
        greenCleared = 0;
        blueCleared = 0;
        purpleCleared = 0;
        yellowCleared = 0;
        pinkCleared = 0;
        hearts = 3;
        trayCapacity = activeMatchTarget;
        activeBoardFirstRowZ = CalculateBoardFirstRowZ();
        parkingUses = 0;
        extraSlotUsed = false;
        towRescueUsed = false;
        trafficJamPopupOpen = false;
        boardPieces.Clear();
        trayPieces.Clear();
        allPieces.Clear();
        activeGarages.Clear();
        levelEditStoredPieces.Clear();
        parkedPiece = null;
        lastMovedPiece = null;
        activeExperimentalLocks.Clear();
        activeExperimentalRules = isDemoMode ? null : CreateExperimentalRules(level.boardNumber);
        tutorialStep = 0;
        tutorialMessageExpiresAt = Time.unscaledTime + 4.5f;
        ResetGameplayHintTimer();

        if (levelRoot != null) Destroy(levelRoot);
        levelRoot = new GameObject($"Car Board {level.boardNumber}");
        CreateBoard(activeBoardSize);

        // Build only the selected board. Previously every board's 3D piece
        // specification was generated during startup even though only one
        // board can be visible at a time.
        PieceSpec[] specifications = level.customSpecifications != null
            ? (PieceSpec[])level.customSpecifications.Clone()
            : BuildPrototypePieceSpecs(level.source);
        GarageSpec[] garageSpecifications = CloneGarageSpecifications(level.garageSpecifications);
        PieceSpec[] authoredSpecifications = (PieceSpec[])specifications.Clone();
        GarageSpec[] authoredGarageSpecifications = CloneGarageSpecifications(garageSpecifications);
        string levelEditKey = GetDirectionOverrideKey(level.boardNumber);
        bool savedCarPlacementsApplied = HasSavedCarPlacements(levelEditKey);
        bool savedStructuralEditsApplied = HasSavedStructuralEdits(levelEditKey);
        garageSpecifications = ApplySavedLevelOverrides(
            levelEditKey,
            ref specifications,
            garageSpecifications);
        if (!IsLoadedLevelGeometryValid(specifications, garageSpecifications))
        {
            Debug.LogWarning(
                $"Ignored invalid saved object placements for Board {level.boardNumber}; "
                + "the authored layout was loaded instead.");
            specifications = authoredSpecifications;
            garageSpecifications = authoredGarageSpecifications;
            savedCarPlacementsApplied = false;
            savedStructuralEditsApplied = false;
        }
        HashSet<int> storedPieceIndexes = GetStoredPieceIndexes(garageSpecifications);
        ValidateParkingCapacityPrincipleOrThrow(
            level.boardNumber,
            level.matchTarget,
            specifications,
            garageSpecifications,
            storedPieceIndexes,
            savedStructuralEditsApplied);
        if (level.customSpecifications != null || savedCarPlacementsApplied)
        {
            ConfigureCustomColorGoals(specifications, garageSpecifications, storedPieceIndexes);
            if (RequiresLegacySideParkingRedesign(level.boardNumber))
                activeAdjacentMatchColors.Add(PieceColor.Blue);
        }
        else
            ConfigureActiveColorGoals(level.source, specifications);
        for (int index = 0; index < specifications.Length; index++)
        {
            PieceSpec spec = specifications[index];
            CarPuzzlePiece piece = CreatePuzzlePiece(spec);
            piece.LevelEditSourceIndex = index;
            if (piece.IsRevealCovered)
            {
                levelEditLastBoxVisualRotation = piece.GetEditedRevealBoxWorldRotation();
                levelEditHasBoxVisualRotation = true;
            }
            if (storedPieceIndexes.Contains(index))
            {
                piece.gameObject.SetActive(false);
                levelEditStoredPieces[index] = piece;
            }
            else
            {
                boardPieces.Add(piece);
                allPieces.Add(piece);
            }
        }

        CreateActiveGarages(garageSpecifications);

        ApplyExperimentalLocks();

        if (hud != null)
        {
            hud.ResetTrafficJamUi();
            hud.ClearLevelEditResult();
        }
        UpdateTrayHighlights();
        UpdateParkingHighlight();
        RefreshHud();
        string levelKind = isDemoMode ? "demo" : "fixed";
        Debug.Log($"Loaded {levelKind} 3D car Board {level.boardNumber} of {activeLevels.Count}.");
    }

    private void LoadSavedCampaignProgress()
    {
        int availableCampaignLevels = Mathf.Min(CampaignLevelCount, levels.Count);
        if (availableCampaignLevels <= 0)
        {
            lastStandardLevelIndex = 0;
            highestUnlockedLevelIndex = 0;
            highestCompletedLevelIndex = -1;
            return;
        }

        int resumeBoard = PlayerPrefs.GetInt(CampaignResumeBoardKey, 1);
        int highestUnlockedBoard = PlayerPrefs.GetInt(CampaignHighestUnlockedBoardKey, 1);
        int highestCompletedBoard = PlayerPrefs.GetInt(CampaignHighestCompletedBoardKey, 0);

        resumeBoard = Mathf.Clamp(resumeBoard, 1, availableCampaignLevels);
        highestCompletedBoard = Mathf.Clamp(highestCompletedBoard, 0, availableCampaignLevels);
        highestUnlockedBoard = Mathf.Clamp(
            Mathf.Max(
                highestUnlockedBoard,
                resumeBoard,
                Mathf.Min(availableCampaignLevels, highestCompletedBoard + 1)),
            1,
            availableCampaignLevels);

        lastStandardLevelIndex = resumeBoard - 1;
        highestUnlockedLevelIndex = highestUnlockedBoard - 1;
        highestCompletedLevelIndex = highestCompletedBoard - 1;

        // Normalize old or partially written values immediately so an update,
        // interrupted save, or future campaign-size change cannot leave the
        // player pointing at an unavailable board.
        SaveCampaignProgress();
    }

    private void RecordCampaignLevelCompleted()
    {
        if (isDemoMode || levelIndex < 0 || levelIndex >= CampaignLevelCount)
            return;

        int finalCampaignIndex = Mathf.Max(0, Mathf.Min(CampaignLevelCount, levels.Count) - 1);
        highestCompletedLevelIndex = Mathf.Max(highestCompletedLevelIndex, levelIndex);
        highestUnlockedLevelIndex = Mathf.Max(
            highestUnlockedLevelIndex,
            Mathf.Min(levelIndex + 1, finalCampaignIndex));

        // Continue from the next unlocked board after returning home or
        // reopening the app. Completing the finale keeps the finale selected.
        lastStandardLevelIndex = Mathf.Min(levelIndex + 1, finalCampaignIndex);
        SaveCampaignProgress();
    }

    private void SaveCampaignProgress()
    {
        int availableCampaignLevels = Mathf.Min(CampaignLevelCount, levels.Count);
        if (availableCampaignLevels <= 0) return;

        int finalIndex = availableCampaignLevels - 1;
        lastStandardLevelIndex = Mathf.Clamp(lastStandardLevelIndex, 0, finalIndex);
        highestUnlockedLevelIndex = Mathf.Clamp(
            Mathf.Max(highestUnlockedLevelIndex, lastStandardLevelIndex),
            0,
            finalIndex);
        highestCompletedLevelIndex = Mathf.Clamp(highestCompletedLevelIndex, -1, finalIndex);

        PlayerPrefs.SetInt(CampaignProgressVersionKey, CampaignProgressVersion);
        PlayerPrefs.SetInt(CampaignResumeBoardKey, lastStandardLevelIndex + 1);
        PlayerPrefs.SetInt(CampaignHighestUnlockedBoardKey, highestUnlockedLevelIndex + 1);
        PlayerPrefs.SetInt(CampaignHighestCompletedBoardKey, highestCompletedLevelIndex + 1);
        PlayerPrefs.Save();
    }

    private CarPuzzlePiece CreatePuzzlePiece(PieceSpec specification)
    {
        string kind = specification.color == PieceColor.Trash
            ? "Police Car Block"
            : specification.isRevealBox ? "Mystery Box"
            : specification.cellLength > 1 ? $"{specification.color} Limousine" : $"{specification.color} Car";
        GameObject pieceObject = new GameObject($"{kind}_{specification.row}_{specification.col}");
        pieceObject.transform.SetParent(levelRoot.transform, true);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();
        float boardScale = GetBoardPieceScale();
        float offBoardScale = GetOffBoardPieceScale(specification.cellLength);
        piece.Configure(
            specification.row,
            specification.col,
            specification.color,
            specification.direction,
            GetBoardPiecePosition(specification.row, specification.col, specification.direction, specification.cellLength),
            boardScale,
            offBoardScale,
            specification.cellLength,
            specification.isRevealBox,
            !isDemoMode && BoardNumber == 1);
        if (specification.isRevealBox)
        {
            piece.SetMysteryBoxRevealVfx(matchVfx);
            if (BoardNumber == 50)
                piece.UseLevel50MysteryBoxVisual();
        }
        return piece;
    }

    private void CreateActiveGarages(GarageSpec[] garageSpecifications)
    {
        if (garageSpecifications == null) return;
        for (int index = 0; index < garageSpecifications.Length; index++)
            CreateActiveGarage(garageSpecifications[index]);
    }

    private ActiveGarage CreateActiveGarage(GarageSpec specification)
    {
        if (specification == null) return null;
        var activeGarage = new ActiveGarage(specification);
        activeGarages.Add(activeGarage);

        GameObject garageObject = new GameObject($"Garage {specification.id + 1}");
        garageObject.transform.SetParent(levelRoot.transform, true);
        garageObject.transform.position = GetBoardCellPosition(specification.row, specification.col)
            + new Vector3(0f, 0.04f, 0f);
        activeGarage.visual = garageObject.AddComponent<GaragePuzzleVisual>();
        activeGarage.visual.Configure(
            GetApprovedMechanicMaterial(ApprovedGarageOpenResourcePath),
            GetApprovedMechanicMaterial(ApprovedGarageClosedResourcePath),
            GetBoardPieceScale());

        SpawnNextGarageCar(activeGarage, false);
        return activeGarage;
    }

    private void SpawnNextGarageCar(ActiveGarage garage, bool animateEmergence)
    {
        if (garage == null || garage.specification.carQueue.Length <= garage.nextQueueIndex)
        {
            if (garage != null && garage.transitionCoroutine == null)
                garage.transitionCoroutine = StartCoroutine(CloseExhaustedGarage(garage));
            return;
        }

        PieceColor color = garage.specification.carQueue[garage.nextQueueIndex++];
        int exitRow = garage.specification.row + 1;
        int exitCol = garage.specification.col;
        PieceSpec carSpecification = new PieceSpec(
            exitRow,
            exitCol,
            color,
            ExitDirection.Down);
        CarPuzzlePiece piece = CreatePuzzlePiece(carSpecification);
        piece.SourceGarageId = garage.specification.id;
        garage.exposedPiece = piece;
        boardPieces.Add(piece);
        allPieces.Add(piece);

        if (animateEmergence)
        {
            garageTransitionsInProgress++;
            garage.transitionCoroutine = StartCoroutine(AnimateGarageCarEmergence(garage, piece));
        }
    }

    private IEnumerator AnimateGarageCarEmergence(ActiveGarage garage, CarPuzzlePiece piece)
    {
        carsCurrentlyMoving.Add(piece);
        Vector3 target = GetBoardPiecePosition(piece);
        Vector3 start = GetBoardCellPosition(garage.specification.row, garage.specification.col)
            + new Vector3(0f, 0.03f, 0f);
        piece.transform.position = start;
        piece.SetDirectionArrowVisible(false);
        yield return StartCoroutine(piece.DriveRouteStraight(target, 0.22f));
        piece.SetDirectionArrowVisible(directionArrowsEnabled);
        carsCurrentlyMoving.Remove(piece);
        garageTransitionsInProgress = Mathf.Max(0, garageTransitionsInProgress - 1);
        garage.transitionCoroutine = null;
        UpdateTutorialPieceDimming();
        RefreshHud();
    }

    private IEnumerator CloseExhaustedGarage(ActiveGarage garage)
    {
        garageTransitionsInProgress++;
        if (garage.visual != null)
            yield return StartCoroutine(garage.visual.CloseDoor(0.42f));
        garageTransitionsInProgress = Mathf.Max(0, garageTransitionsInProgress - 1);
        garage.transitionCoroutine = null;
        RefreshHud();
    }

    private void ReleaseNextGarageCar(CarPuzzlePiece departedPiece)
    {
        if (departedPiece == null || departedPiece.SourceGarageId < 0) return;
        for (int index = 0; index < activeGarages.Count; index++)
        {
            ActiveGarage garage = activeGarages[index];
            if (garage.specification.id != departedPiece.SourceGarageId
                || garage.exposedPiece != departedPiece)
                continue;
            garage.exposedPiece = null;
            SpawnNextGarageCar(garage, true);
            return;
        }
    }

    private bool IsGarageLevelActive()
    {
        return activeGarages.Count > 0;
    }

    private void LoadFixedLevels()
    {
        ColorSortLevelDatabase database = Resources.Load<ColorSortLevelDatabase>("ColorSortLevelDatabase");
        if (database == null || database.levels == null || database.levels.Count < LegacyFixedLevelCount)
        {
            Debug.LogError($"The fixed Color Sort level database is missing or has fewer than {LegacyFixedLevelCount} boards.");
            return;
        }

        levels.Clear();
        BuildTutorialLevels(database);
        BuildRefinedCatalogLevels();

        BuildDemoLevels();
        Debug.Log($"Loaded {TutorialLevelCount} onboarding boards, "
            + $"{CampaignLevelCount - TutorialLevelCount} refined campaign boards, "
            + $"and {levels.Count - CampaignLevelCount} separate challenge samples.");
    }

    private struct RefinedRemovalEvent
    {
        public int row;
        public int col;
        public ExitDirection direction;
        public int garageId;

        public bool IsGarageFront => garageId >= 0;
    }

    private void BuildRefinedCatalogLevels()
    {
        for (int boardNumber = TutorialLevelCount + 1;
            boardNumber <= CampaignLevelCount;
            boardNumber++)
        {
            LevelDifficulty difficulty = GetRefinedDifficulty(boardNumber);
            bool hasGarage = boardNumber >= 50;
            levels.Add(BuildRefinedCatalogLevel(boardNumber, difficulty, hasGarage));
        }
    }

    private static PrototypeLevel BuildRefinedCatalogLevel(
        int boardNumber,
        LevelDifficulty difficulty,
        bool hasGarage)
    {
        if (!hasGarage) return BuildRefinedStandardLevel(boardNumber, difficulty);
        return BuildRefinedGarageLevel(boardNumber, difficulty);
    }

    private static LevelDifficulty GetRefinedDifficulty(int boardNumber)
    {
        int positionInCycle = (boardNumber - 21) % 5;
        if (positionInCycle == 3) return LevelDifficulty.Hard;
        if (positionInCycle == 4) return LevelDifficulty.SuperHard;
        return LevelDifficulty.Normal;
    }

    private static PrototypeLevel BuildRefinedStandardLevel(
        int boardNumber,
        LevelDifficulty difficulty)
    {
        int boardSize;
        int matchTarget;
        int colorCount;
        int limousineCount;
        if (boardNumber <= 29)
        {
            boardSize = 3;
            matchTarget = 2;
            colorCount = difficulty == LevelDifficulty.SuperHard ? 4 : 3;
            limousineCount = difficulty == LevelDifficulty.Normal
                ? 1
                : difficulty == LevelDifficulty.Hard ? 2 : 1;
        }
        else if (boardNumber <= 39)
        {
            boardSize = 4;
            matchTarget = 3;
            colorCount = difficulty == LevelDifficulty.SuperHard ? 5 : 4;
            limousineCount = difficulty == LevelDifficulty.Normal
                ? boardNumber % 2
                : difficulty == LevelDifficulty.Hard ? 2 : 1;
        }
        else if (boardNumber <= 44)
        {
            boardSize = 4;
            matchTarget = 4;
            colorCount = 3;
            limousineCount = difficulty == LevelDifficulty.Normal ? 1 : difficulty == LevelDifficulty.Hard ? 2 : 3;
        }
        else
        {
            boardSize = 5;
            matchTarget = 4;
            colorCount = difficulty == LevelDifficulty.SuperHard ? 6 : 5;
            limousineCount = difficulty == LevelDifficulty.Normal
                ? 1
                : difficulty == LevelDifficulty.Hard ? 3 : 1;
        }

        PieceSpec[] specifications = BuildSolvableCampaignBoard(
            boardNumber,
            boardSize,
            matchTarget,
            limousineCount,
            colorCount,
            difficulty);
        if (boardNumber >= 30)
        {
            int boxCount = difficulty == LevelDifficulty.Normal
                ? 1
                : difficulty == LevelDifficulty.Hard ? 2 : 3;
            ApplyRefinedMysteryBoxes(specifications, boxCount, boardNumber);
        }
        return new PrototypeLevel(
            boardNumber,
            boardSize,
            matchTarget,
            specifications,
            null,
            difficulty);
    }

    private static PrototypeLevel BuildRefinedGarageLevel(
        int boardNumber,
        LevelDifficulty difficulty)
    {
        const int boardSize = 5;
        int matchTarget = boardNumber >= 85 ? 5 : 4;
        int colorCount = 5;
        int garageCount = difficulty == LevelDifficulty.Normal ? 1 : 2;
        int queueCarCount;
        if (boardNumber >= 85)
            queueCarCount = difficulty == LevelDifficulty.Normal
                ? 12
                : difficulty == LevelDifficulty.Hard ? 19 : 24;
        else if (boardNumber >= 70)
            queueCarCount = difficulty == LevelDifficulty.Normal
                ? 10
                : difficulty == LevelDifficulty.Hard ? 15 : 19;
        else
            queueCarCount = difficulty == LevelDifficulty.Normal
                ? 6
                : difficulty == LevelDifficulty.Hard ? 11 : 15;
        int totalCarCount = boardSize * boardSize - garageCount * 2 + queueCarCount;
        int policeCount = totalCarCount % matchTarget;
        int totalSetCount = (totalCarCount - policeCount) / matchTarget;
        int extraSetCount = totalSetCount - colorCount;

        GarageSpec[] garageLayout = BuildRefinedGarageLayout(
            boardNumber,
            garageCount,
            queueCarCount);
        int[] setCounts = new int[colorCount];
        for (int index = 0; index < setCounts.Length; index++) setCounts[index] = 1;
        for (int index = 0; index < extraSetCount; index++)
            setCounts[(boardNumber + index * 2) % setCounts.Length]++;

        int removalPlanCount = 0;
        int populatedPlanCount = 0;
        int bestInitialMovableCount = int.MaxValue;
        int bestBlockerChain = 0;
        for (int attempt = 0; attempt < 512; attempt++)
        {
            var random = new System.Random(boardNumber * 48611 + attempt * 104729);
            if (!TryBuildRefinedRemovalEvents(
                boardSize,
                garageLayout,
                difficulty,
                random,
                out List<RefinedRemovalEvent> removalEvents))
                continue;
            removalPlanCount++;

            if (!TryPopulateRefinedGarageLevel(
                boardNumber,
                matchTarget,
                difficulty,
                setCounts,
                garageLayout,
                removalEvents,
                random,
                out PieceSpec[] specifications,
                out GarageSpec[] populatedGarages))
                continue;
            populatedPlanCount++;

            if (!MeetsRefinedDifficultyGeometry(
                boardSize,
                specifications,
                populatedGarages,
                difficulty,
                out int initialMovableCount,
                out int blockerChain))
            {
                bestInitialMovableCount = Mathf.Min(bestInitialMovableCount, initialMovableCount);
                bestBlockerChain = Mathf.Max(bestBlockerChain, blockerChain);
                continue;
            }

            int boxCount = difficulty == LevelDifficulty.Normal
                ? 1
                : difficulty == LevelDifficulty.Hard ? 2 : 3;
            ApplyRefinedMysteryBoxes(specifications, boxCount, boardNumber);
            return new PrototypeLevel(
                boardNumber,
                boardSize,
                matchTarget,
                specifications,
                populatedGarages,
                difficulty);
        }

        throw new System.InvalidOperationException(
            $"Could not construct refined garage Level {boardNumber} "
            + $"({removalPlanCount} removal plans, {populatedPlanCount} populated plans, "
            + $"best opening {bestInitialMovableCount}, best chain {bestBlockerChain}).");
    }

    private static GarageSpec[] BuildRefinedGarageLayout(
        int boardNumber,
        int garageCount,
        int queueCarCount)
    {
        int row = boardNumber % 3 == 0 ? 0 : 1;
        if (garageCount == 1)
        {
            int col = boardNumber % 2 == 0 ? 2 : 1;
            return new[] { new GarageSpec(0, row, col, new PieceColor[queueCarCount]) };
        }

        int firstLength = (queueCarCount + 1) / 2;
        int secondLength = queueCarCount / 2;
        return new[]
        {
            new GarageSpec(0, row, 1, new PieceColor[firstLength]),
            new GarageSpec(1, row, 3, new PieceColor[secondLength])
        };
    }

    private static bool TryBuildRefinedRemovalEvents(
        int boardSize,
        GarageSpec[] garages,
        LevelDifficulty difficulty,
        System.Random random,
        out List<RefinedRemovalEvent> removalEvents)
    {
        if (difficulty != LevelDifficulty.Normal)
            return TryBuildRefinedChainedRemovalEvents(
                boardSize,
                garages,
                random,
                out removalEvents);

        var remaining = new bool[boardSize, boardSize];
        var garageCells = new bool[boardSize, boardSize];
        var garageFrontIds = new int[boardSize, boardSize];
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
            garageFrontIds[row, col] = -1;

        for (int index = 0; index < garages.Length; index++)
        {
            GarageSpec garage = garages[index];
            if (garage.row < 0 || garage.row >= boardSize - 1
                || garage.col < 0 || garage.col >= boardSize)
            {
                removalEvents = null;
                return false;
            }
            garageCells[garage.row, garage.col] = true;
            garageFrontIds[garage.row + 1, garage.col] = garage.id;
        }

        int remainingCount = 0;
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
        {
            if (garageCells[row, col]) continue;
            remaining[row, col] = true;
            remainingCount++;
        }

        removalEvents = new List<RefinedRemovalEvent>(remainingCount);
        while (remainingCount > 0)
        {
            var options = new List<RefinedRemovalEvent>();
            for (int row = 0; row < boardSize; row++)
            for (int col = 0; col < boardSize; col++)
            {
                if (!remaining[row, col]) continue;
                int garageId = garageFrontIds[row, col];
                if (garageId >= 0)
                {
                    if (IsRefinedExitClear(
                        row, col, ExitDirection.Down, boardSize, remaining, garageCells))
                        options.Add(new RefinedRemovalEvent
                        {
                            row = row,
                            col = col,
                            direction = ExitDirection.Down,
                            garageId = garageId
                        });
                    continue;
                }

                for (int direction = 0; direction < 4; direction++)
                {
                    ExitDirection exitDirection = (ExitDirection)direction;
                    if (!IsRefinedExitClear(
                        row, col, exitDirection, boardSize, remaining, garageCells))
                        continue;
                    options.Add(new RefinedRemovalEvent
                    {
                        row = row,
                        col = col,
                        direction = exitDirection,
                        garageId = -1
                    });
                }
            }

            if (options.Count == 0)
            {
                removalEvents = null;
                return false;
            }

            int selectedIndex;
            if (difficulty == LevelDifficulty.Normal)
            {
                selectedIndex = random.Next(options.Count);
            }
            else
            {
                int start = random.Next(options.Count);
                selectedIndex = start;
                float bestScore = float.NegativeInfinity;
                for (int offset = 0; offset < options.Count; offset++)
                {
                    int index = (start + offset) % options.Count;
                    RefinedRemovalEvent option = options[index];
                    float centerDistance = Mathf.Abs(option.row - (boardSize - 1) * 0.5f)
                        + Mathf.Abs(option.col - (boardSize - 1) * 0.5f);
                    float score = option.IsGarageFront ? 4f : -centerDistance;
                    if (difficulty == LevelDifficulty.SuperHard) score -= centerDistance * 0.75f;
                    score += (float)random.NextDouble() * 0.2f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    selectedIndex = index;
                }
            }

            RefinedRemovalEvent selected = options[selectedIndex];
            removalEvents.Add(selected);
            remaining[selected.row, selected.col] = false;
            remainingCount--;
        }

        int directionMask = 0;
        for (int index = 0; index < removalEvents.Count; index++)
            if (!removalEvents[index].IsGarageFront)
                directionMask |= 1 << (int)removalEvents[index].direction;
        return CountSetBits(directionMask) >= 3;
    }

    private static bool RefinedExitRayContainsRemovedCell(
        RefinedRemovalEvent removalEvent,
        int boardSize,
        bool[,] remaining,
        bool[,] garageCells)
    {
        Vector2Int step = DirectionToGridStep(removalEvent.direction);
        int row = removalEvent.row + step.y;
        int col = removalEvent.col + step.x;
        while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
        {
            if (!remaining[row, col] && !garageCells[row, col]) return true;
            row += step.y;
            col += step.x;
        }
        return false;
    }

    private static bool TryBuildRefinedChainedRemovalEvents(
        int boardSize,
        GarageSpec[] garages,
        System.Random random,
        out List<RefinedRemovalEvent> removalEvents)
    {
        var remaining = new bool[boardSize, boardSize];
        var garageCells = new bool[boardSize, boardSize];
        var garageFrontIds = new int[boardSize, boardSize];
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++) garageFrontIds[row, col] = -1;

        for (int index = 0; index < garages.Length; index++)
        {
            GarageSpec garage = garages[index];
            if (garage.row < 0 || garage.row >= boardSize - 1
                || garage.col < 0 || garage.col >= boardSize)
            {
                removalEvents = null;
                return false;
            }
            garageCells[garage.row, garage.col] = true;
            garageFrontIds[garage.row + 1, garage.col] = garage.id;
        }

        int remainingCount = 0;
        ulong remainingMask = 0UL;
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
        {
            if (garageCells[row, col]) continue;
            remaining[row, col] = true;
            remainingMask |= 1UL << (row * boardSize + col);
            remainingCount++;
        }

        removalEvents = new List<RefinedRemovalEvent>(remainingCount);
        var failedStates = new HashSet<ulong>();
        int searchBudget = 2000000;
        if (!SearchRefinedChainedRemovalEvents(
            boardSize,
            remaining,
            garageCells,
            garageFrontIds,
            remainingMask,
            remainingCount,
            -1,
            -1,
            random,
            failedStates,
            removalEvents,
            ref searchBudget))
        {
            removalEvents = null;
            return false;
        }

        int directionMask = 0;
        for (int index = 0; index < removalEvents.Count; index++)
            if (!removalEvents[index].IsGarageFront)
                directionMask |= 1 << (int)removalEvents[index].direction;
        return CountSetBits(directionMask) >= 3;
    }

    private static bool SearchRefinedChainedRemovalEvents(
        int boardSize,
        bool[,] remaining,
        bool[,] garageCells,
        int[,] garageFrontIds,
        ulong remainingMask,
        int remainingCount,
        int previousRow,
        int previousCol,
        System.Random random,
        HashSet<ulong> failedStates,
        List<RefinedRemovalEvent> removalEvents,
        ref int searchBudget)
    {
        if (remainingCount == 0) return true;
        if (--searchBudget <= 0) return false;
        if (!failedStates.Add(remainingMask)) return false;

        var options = new List<RefinedRemovalEvent>();
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
        {
            if (!remaining[row, col]) continue;
            int garageId = garageFrontIds[row, col];
            int firstDirection = garageId >= 0 ? (int)ExitDirection.Down : 0;
            int directionCount = garageId >= 0 ? 1 : 4;
            for (int offset = 0; offset < directionCount; offset++)
            {
                ExitDirection direction = (ExitDirection)(firstDirection + offset);
                if (!IsRefinedExitClear(
                    row,
                    col,
                    direction,
                    boardSize,
                    remaining,
                    garageCells))
                    continue;
                var option = new RefinedRemovalEvent
                {
                    row = row,
                    col = col,
                    direction = direction,
                    garageId = garageId
                };
                if (previousRow >= 0
                    && !RefinedExitRayContainsRemovedCell(
                        option,
                        boardSize,
                        remaining,
                        garageCells))
                    continue;
                options.Add(option);
            }
        }

        ShuffleCampaignList(options, random);
        for (int index = 0; index < options.Count; index++)
        {
            RefinedRemovalEvent selected = options[index];
            remaining[selected.row, selected.col] = false;
            removalEvents.Add(selected);
            int cell = selected.row * boardSize + selected.col;
            if (SearchRefinedChainedRemovalEvents(
                boardSize,
                remaining,
                garageCells,
                garageFrontIds,
                remainingMask & ~(1UL << cell),
                remainingCount - 1,
                selected.row,
                selected.col,
                random,
                failedStates,
                removalEvents,
                ref searchBudget))
                return true;
            removalEvents.RemoveAt(removalEvents.Count - 1);
            remaining[selected.row, selected.col] = true;
        }
        return false;
    }

    private static bool RefinedExitRayContainsCell(
        RefinedRemovalEvent removalEvent,
        int targetRow,
        int targetCol,
        int boardSize)
    {
        Vector2Int step = DirectionToGridStep(removalEvent.direction);
        int row = removalEvent.row + step.y;
        int col = removalEvent.col + step.x;
        while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
        {
            if (row == targetRow && col == targetCol) return true;
            row += step.y;
            col += step.x;
        }
        return false;
    }

    private static bool IsRefinedExitClear(
        int row,
        int col,
        ExitDirection direction,
        int boardSize,
        bool[,] remaining,
        bool[,] garageCells)
    {
        Vector2Int step = DirectionToGridStep(direction);
        row += step.y;
        col += step.x;
        while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
        {
            if (remaining[row, col] || garageCells[row, col]) return false;
            row += step.y;
            col += step.x;
        }
        return true;
    }

    private static bool TryPopulateRefinedGarageLevel(
        int boardNumber,
        int matchTarget,
        LevelDifficulty difficulty,
        int[] setCounts,
        GarageSpec[] garageLayout,
        List<RefinedRemovalEvent> removalEvents,
        System.Random random,
        out PieceSpec[] specifications,
        out GarageSpec[] populatedGarages)
    {
        var selectedColors = new List<PieceColor>
        {
            PieceColor.Red,
            PieceColor.Green,
            PieceColor.Blue,
            PieceColor.Purple,
            PieceColor.Yellow,
            PieceColor.Pink
        };
        ShuffleCampaignList(selectedColors, random);
        selectedColors.RemoveRange(setCounts.Length, selectedColors.Count - setCounts.Length);
        List<PieceColor> colorSequence = BuildRefinedSetSequence(
            selectedColors,
            setCounts,
            matchTarget,
            difficulty,
            random);

        int queueCarCount = 0;
        for (int index = 0; index < garageLayout.Length; index++)
            queueCarCount += garageLayout[index].carQueue.Length;
        int visibleEventCount = removalEvents.Count - garageLayout.Length;
        int totalCarCount = visibleEventCount + queueCarCount;
        int policeCount = totalCarCount - colorSequence.Count;
        if (policeCount < 0 || policeCount > visibleEventCount)
        {
            specifications = null;
            populatedGarages = null;
            return false;
        }

        var queues = new List<PieceColor>[garageLayout.Length];
        for (int index = 0; index < queues.Length; index++)
            queues[index] = new List<PieceColor>(garageLayout[index].carQueue.Length);
        int colorIndex = 0;
        var policeEvents = new HashSet<int>();
        if (policeCount > 0)
        {
            var regularEventIndexes = new List<int>();
            for (int eventIndex = 0; eventIndex < removalEvents.Count; eventIndex++)
                if (!removalEvents[eventIndex].IsGarageFront) regularEventIndexes.Add(eventIndex);
            for (int policeIndex = 0; policeIndex < policeCount; policeIndex++)
            {
                int ordinal = Mathf.RoundToInt(
                    (policeIndex + 1f) * (regularEventIndexes.Count - 1) / (policeCount + 1f));
                policeEvents.Add(regularEventIndexes[Mathf.Clamp(ordinal, 0, regularEventIndexes.Count - 1)]);
            }
        }
        var result = new List<PieceSpec>(visibleEventCount);
        for (int eventIndex = 0; eventIndex < removalEvents.Count; eventIndex++)
        {
            RefinedRemovalEvent removalEvent = removalEvents[eventIndex];
            if (removalEvent.IsGarageFront)
            {
                int garageIndex = FindGarageIndex(garageLayout, removalEvent.garageId);
                if (garageIndex < 0)
                {
                    specifications = null;
                    populatedGarages = null;
                    return false;
                }
                int queueLength = garageLayout[garageIndex].carQueue.Length;
                for (int queueIndex = 0; queueIndex < queueLength; queueIndex++)
                {
                    if (colorIndex >= colorSequence.Count)
                    {
                        specifications = null;
                        populatedGarages = null;
                        return false;
                    }
                    queues[garageIndex].Add(colorSequence[colorIndex++]);
                }
                // The first queued car is the visible car parked immediately
                // in front of the garage. Runtime spawns it from the queue, so
                // it is deliberately not duplicated in specifications.
                continue;
            }

            PieceColor color = policeEvents.Contains(eventIndex)
                ? PieceColor.Trash
                : colorSequence[colorIndex++];
            result.Add(new PieceSpec(
                removalEvent.row,
                removalEvent.col,
                color,
                removalEvent.direction));
        }

        if (colorIndex != colorSequence.Count)
        {
            throw new System.InvalidOperationException(
                $"Populate {boardNumber}: consumed={colorIndex}, colors={colorSequence.Count}.");
        }

        populatedGarages = new GarageSpec[garageLayout.Length];
        for (int index = 0; index < garageLayout.Length; index++)
        {
            bool queueIsMixed = false;
            for (int queueIndex = 1; queueIndex < queues[index].Count; queueIndex++)
                if (queues[index][queueIndex] != queues[index][0])
                {
                    queueIsMixed = true;
                    break;
                }
            if (!queueIsMixed)
            {
                specifications = null;
                populatedGarages = null;
                return false;
            }
            populatedGarages[index] = new GarageSpec(
                garageLayout[index].id,
                garageLayout[index].row,
                garageLayout[index].col,
                queues[index].ToArray());
        }
        specifications = result.ToArray();
        return true;
    }

    private static List<PieceColor> BuildRefinedSetSequence(
        List<PieceColor> colors,
        int[] setCounts,
        int matchTarget,
        LevelDifficulty difficulty,
        System.Random random)
    {
        int[] remainingSets = (int[])setCounts.Clone();
        var sequence = new List<PieceColor>();
        while (true)
        {
            var roundColors = new List<PieceColor>();
            for (int index = 0; index < remainingSets.Length; index++)
                if (remainingSets[index] > 0) roundColors.Add(colors[index]);
            if (roundColors.Count == 0) break;
            ShuffleCampaignList(roundColors, random);

            int interlockCount = difficulty == LevelDifficulty.Normal
                ? Mathf.Min(2, roundColors.Count)
                : difficulty == LevelDifficulty.Hard
                    ? Mathf.Min(3, roundColors.Count)
                    : roundColors.Count;
            if (interlockCount <= 1)
            {
                for (int index = 0; index < roundColors.Count; index++)
                    AddCampaignColorCopies(sequence, roundColors[index], matchTarget);
            }
            else
            {
                AddCampaignColorCopies(sequence, roundColors[0], matchTarget - 1);
                sequence.Add(roundColors[1]);
                sequence.Add(roundColors[0]);
                for (int index = 1; index < interlockCount - 1; index++)
                {
                    sequence.Add(roundColors[index + 1]);
                    AddCampaignColorCopies(sequence, roundColors[index], matchTarget - 1);
                }
                AddCampaignColorCopies(sequence, roundColors[interlockCount - 1], matchTarget - 1);
                for (int index = interlockCount; index < roundColors.Count; index++)
                    AddCampaignColorCopies(sequence, roundColors[index], matchTarget);
            }

            for (int colorIndex = 0; colorIndex < colors.Count; colorIndex++)
                if (remainingSets[colorIndex] > 0) remainingSets[colorIndex]--;
        }
        return sequence;
    }

    private static void ApplyRefinedMysteryBoxes(
        PieceSpec[] specifications,
        int requestedCount,
        int boardNumber)
    {
        if (specifications == null || specifications.Length < 2 || requestedCount <= 0) return;
        var candidates = new List<int>();
        for (int index = 0; index < specifications.Length; index++)
        {
            if (specifications[index].color == PieceColor.Trash
                || specifications[index].cellLength != 1
                // Refined specifications are stored in their proven removal
                // order. A covered car must therefore touch a car that is
                // removed earlier in that order. That guarantees its box has
                // opened before the covered car is needed and prevents a set
                // of boxes from covering every opening move (Level 75 was one
                // deterministic example of that instant-failure state).
                || !HasEarlierOrthogonallyAdjacentSpecification(specifications, index))
                continue;
            candidates.Add(index);
        }
        var random = new System.Random(boardNumber * 31337);
        ShuffleCampaignList(candidates, random);
        int boxCount = Mathf.Min(requestedCount, candidates.Count);
        int applied = 0;
        for (int candidateIndex = 0; candidateIndex < candidates.Count && applied < boxCount; candidateIndex++)
        {
            int index = candidates[candidateIndex];
            PieceSpec hidden = specifications[index];
            hidden.isRevealBox = true;
            specifications[index] = hidden;
            applied++;
        }
    }

    private static bool HasEarlierOrthogonallyAdjacentSpecification(
        PieceSpec[] specifications,
        int centerIndex)
    {
        if (specifications == null || centerIndex <= 0 || centerIndex >= specifications.Length)
            return false;

        PieceSpec center = specifications[centerIndex];
        for (int index = 0; index < centerIndex; index++)
        {
            PieceSpec earlier = specifications[index];
            if (SpecificationOccupiesCell(earlier, center.row - 1, center.col)
                || SpecificationOccupiesCell(earlier, center.row + 1, center.col)
                || SpecificationOccupiesCell(earlier, center.row, center.col - 1)
                || SpecificationOccupiesCell(earlier, center.row, center.col + 1))
                return true;
        }
        return false;
    }

    private static bool HasOrthogonallyAdjacentSpecification(
        PieceSpec[] specifications,
        int centerIndex)
    {
        if (specifications == null || centerIndex < 0 || centerIndex >= specifications.Length)
            return false;

        PieceSpec center = specifications[centerIndex];
        for (int index = 0; index < specifications.Length; index++)
        {
            if (index == centerIndex) continue;
            PieceSpec candidate = specifications[index];
            if (SpecificationOccupiesCell(candidate, center.row - 1, center.col)
                || SpecificationOccupiesCell(candidate, center.row + 1, center.col)
                || SpecificationOccupiesCell(candidate, center.row, center.col - 1)
                || SpecificationOccupiesCell(candidate, center.row, center.col + 1))
                return true;
        }
        return false;
    }

    private static bool SpecificationOccupiesCell(PieceSpec specification, int row, int col)
    {
        Vector2Int step = DirectionToGridStep(specification.direction);
        for (int offset = 0; offset < specification.cellLength; offset++)
            if (specification.row - step.y * offset == row
                && specification.col - step.x * offset == col)
                return true;
        return false;
    }

    private static int FindGarageIndex(GarageSpec[] garages, int id)
    {
        for (int index = 0; index < garages.Length; index++)
            if (garages[index].id == id) return index;
        return -1;
    }

    private static int CountSetBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }

    private static PieceSpec[] BuildMysteryBoxLevel81()
    {
        PieceSpec[] specifications = BuildSolvableCampaignBoard(81, 6, 5, 0, 6);
        if (specifications.Length == 0) return specifications;

        PieceColor[] hiddenColors =
        {
            PieceColor.Pink,
            PieceColor.Red,
            PieceColor.Blue,
            PieceColor.Yellow
        };
        var usedIndices = new HashSet<int>();
        for (int colorIndex = 0; colorIndex < hiddenColors.Length; colorIndex++)
        {
            int bestIndex = -1;
            int desiredIndex = Mathf.RoundToInt(
                (colorIndex + 1f) * (specifications.Length - 1) / (hiddenColors.Length + 1f));
            int bestDistance = int.MaxValue;
            for (int index = 0; index < specifications.Length; index++)
            {
                if (specifications[index].color != hiddenColors[colorIndex]
                    || usedIndices.Contains(index)
                    || !HasOrthogonallyAdjacentSpecification(specifications, index))
                    continue;
                int distance = Mathf.Abs(index - desiredIndex);
                if (distance >= bestDistance) continue;
                bestIndex = index;
                bestDistance = distance;
            }
            if (bestIndex < 0) continue;

            PieceSpec hidden = specifications[bestIndex];
            hidden.isRevealBox = true;
            specifications[bestIndex] = hidden;
            usedIndices.Add(bestIndex);
        }
        return specifications;
    }

    private void BuildTutorialLevels(ColorSortLevelDatabase database)
    {
        levels.Add(new PrototypeLevel(1, 2, 2, BuildTutorialLevel1()));
        levels.Add(new PrototypeLevel(2, 3, 2, BuildTutorialLevel2()));
        levels.Add(new PrototypeLevel(3, 3, 2, BuildTutorialLevel3()));
        levels.Add(new PrototypeLevel(4, 3, 2, BuildTutorialLevel4()));
        levels.Add(new PrototypeLevel(5, 3, 3, BuildTutorialLevel5()));
        levels.Add(new PrototypeLevel(6, 3, 3, BuildTutorialLevel6()));
        levels.Add(new PrototypeLevel(7, 3, 2, BuildTutorialLevel7()));
        levels.Add(new PrototypeLevel(8, 3, 2, BuildTutorialLevel8()));
        levels.Add(new PrototypeLevel(9, 3, 2, BuildTutorialLevel9()));
        // Levels 10-20 form a dedicated 3x3 difficulty arc. The first six
        // deepen route reading with mixed exits and police gates; limousines
        // arrive at Level 16 and become increasingly central through Level 20.
        levels.Add(new PrototypeLevel(10, 3, 2, BuildTutorialLevel10()));
        levels.Add(new PrototypeLevel(11, 3, 2, BuildTutorialLevel11()));
        levels.Add(new PrototypeLevel(12, 3, 2, BuildTutorialLevel12()));
        levels.Add(new PrototypeLevel(13, 3, 2, BuildTutorialLevel13()));
        levels.Add(new PrototypeLevel(14, 3, 2, BuildTutorialLevel14()));
        levels.Add(new PrototypeLevel(15, 3, 2, BuildTutorialLevel15()));
        levels.Add(new PrototypeLevel(16, 3, 2, BuildTutorialLevel16()));
        levels.Add(new PrototypeLevel(17, 3, 2, BuildTutorialLevel17()));
        levels.Add(new PrototypeLevel(18, 3, 2, BuildTutorialLevel18()));
        levels.Add(new PrototypeLevel(19, 3, 2, BuildTutorialLevel19()));
        levels.Add(new PrototypeLevel(20, 3, 2, BuildTutorialLevel20()));
    }

    private static PieceSpec[] BuildTutorialLevel1()
    {
        return new[]
        {
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(0, 1, PieceColor.Blue, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel2()
    {
        return new[]
        {
            new PieceSpec(2, 1, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(2, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 0, PieceColor.Trash, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel3()
    {
        return new[]
        {
            new PieceSpec(2, 0, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(0, 2, PieceColor.Purple, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel4()
    {
        return new[]
        {
            new PieceSpec(2, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel5()
    {
        // The third blue is intentionally behind the yellow car. After two blue
        // cars and that yellow enter the full tray, the yellow must be parked.
        return new[]
        {
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 0, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Yellow, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel6()
    {
        return new[]
        {
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 0, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(0, 1, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(0, 2, PieceColor.Yellow, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel7()
    {
        return new[]
        {
            new PieceSpec(2, 0, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(0, 2, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(0, 1, PieceColor.Trash, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel8()
    {
        return new[]
        {
            new PieceSpec(0, 0, PieceColor.Trash, ExitDirection.Up),
            new PieceSpec(1, 0, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(2, 0, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(0, 2, PieceColor.Trash, ExitDirection.Right),
            new PieceSpec(1, 2, PieceColor.Green, ExitDirection.Right),
            new PieceSpec(2, 2, PieceColor.Green, ExitDirection.Right),
            new PieceSpec(0, 1, PieceColor.Trash, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel9()
    {
        // Full 3x3 transition puzzle: four split color pairs surround a police
        // gate, preparing the dense route-reading progression in Level 10.
        return new[]
        {
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(0, 1, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(2, 1, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Yellow, ExitDirection.Left),
            new PieceSpec(0, 2, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Trash, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildTutorialLevel10()
    {
        // Three color pairs share a full board with three police gates. Only
        // one opening route preserves a completable tray pair.
        return new[]
        {
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(0, 1, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(2, 0, PieceColor.Trash, ExitDirection.Left),
            new PieceSpec(0, 2, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(2, 2, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(0, 0, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Trash, ExitDirection.Left)
        };
    }

    private static PieceSpec[] BuildTutorialLevel11()
    {
        // Three police gates must be removed in sequence. The exposed red car is
        // tempting, but committing to it first fills the tray incorrectly.
        return new[]
        {
            new PieceSpec(2, 2, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(2, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Right),
            new PieceSpec(1, 0, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Trash, ExitDirection.Left),
            new PieceSpec(0, 1, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(0, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Trash, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel12()
    {
        // The first four-color board. A central vertical queue and a sideways
        // lower lane create a single safe opening among three visible choices.
        return new[]
        {
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(0, 1, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(1, 1, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(0, 2, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(1, 0, PieceColor.Yellow, ExitDirection.Right),
            new PieceSpec(2, 2, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Trash, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildTutorialLevel13()
    {
        // Three police cars occupy the central crossing. Every color is split
        // across a different edge, so no pair is free at the start.
        return new[]
        {
            new PieceSpec(1, 0, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(0, 2, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Trash, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Trash, ExitDirection.Right),
            new PieceSpec(0, 1, PieceColor.Trash, ExitDirection.Left)
        };
    }

    private static PieceSpec[] BuildTutorialLevel14()
    {
        // Four colors now weave around a police-blocked edge. Four cars can move,
        // but only one opening choice preserves a completable tray pair.
        return new[]
        {
            new PieceSpec(1, 2, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(2, 2, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(2, 0, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(0, 2, PieceColor.Yellow, ExitDirection.Up),
            new PieceSpec(0, 1, PieceColor.Yellow, ExitDirection.Right),
            new PieceSpec(1, 0, PieceColor.Trash, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel15()
    {
        // A full 3x3 finale for regular cars: four split pairs surround a
        // central police gate, with one valid opening sequence.
        return new[]
        {
            new PieceSpec(0, 2, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(0, 1, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(1, 0, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(1, 2, PieceColor.Yellow, ExitDirection.Up),
            new PieceSpec(2, 2, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Trash, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildTutorialLevel16()
    {
        // Limousine introduction: the red limousine spans the right lane and
        // must be paired with its red partner after the police gates move.
        return new[]
        {
            new PieceSpec(1, 2, PieceColor.Red, ExitDirection.Up, 2),
            new PieceSpec(2, 1, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(0, 1, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Right),
            new PieceSpec(2, 0, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(0, 0, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(0, 2, PieceColor.Trash, ExitDirection.Right),
            new PieceSpec(1, 0, PieceColor.Trash, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildTutorialLevel17()
    {
        // One top-row limousine and two regular pairs are woven through two
        // police gates. No complete color pair is initially free.
        return new[]
        {
            new PieceSpec(0, 2, PieceColor.Red, ExitDirection.Right, 2),
            new PieceSpec(1, 1, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(1, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(0, 0, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(2, 1, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(2, 2, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(2, 0, PieceColor.Trash, ExitDirection.Right),
            new PieceSpec(1, 2, PieceColor.Trash, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildTutorialLevel18()
    {
        // Two limousines enter from opposite horizontal edges. Purple cars
        // bridge the remaining lanes and force the player to alternate sides.
        return new[]
        {
            new PieceSpec(2, 2, PieceColor.Red, ExitDirection.Right, 2),
            new PieceSpec(0, 0, PieceColor.Blue, ExitDirection.Left, 2),
            new PieceSpec(1, 1, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(0, 2, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(2, 0, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(1, 0, PieceColor.Trash, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildTutorialLevel19()
    {
        // A full board with perpendicular limousines and a police car sealing
        // the lower-right route. Only one of three opening moves is safe.
        return new[]
        {
            new PieceSpec(0, 2, PieceColor.Red, ExitDirection.Up, 2),
            new PieceSpec(1, 1, PieceColor.Blue, ExitDirection.Right, 2),
            new PieceSpec(0, 1, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(0, 0, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(2, 1, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(2, 2, PieceColor.Trash, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildTutorialLevel20()
    {
        // 3x3 finale: three differently oriented limousines tile the board with
        // their matching regular cars. The apparent open exits form one chain.
        return new[]
        {
            new PieceSpec(1, 1, PieceColor.Red, ExitDirection.Right, 2),
            new PieceSpec(0, 2, PieceColor.Blue, ExitDirection.Up, 2),
            new PieceSpec(2, 0, PieceColor.Purple, ExitDirection.Left, 2),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(2, 2, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(0, 1, PieceColor.Purple, ExitDirection.Right)
        };
    }

    private const int LegacyAuthoredProgressionEndLevel = 50;

    private void BuildAuthoredProgressionLevels()
    {
        for (int boardNumber = TutorialLevelCount + 1;
            boardNumber <= LegacyAuthoredProgressionEndLevel;
            boardNumber++)
        {
            GetAuthoredProgressionRecipe(
                boardNumber,
                out int boardSize,
                out int matchTarget,
                out int limousineCount,
                out int colorCount);
            levels.Add(new PrototypeLevel(
                boardNumber,
                boardSize,
                matchTarget,
                BuildSolvableCampaignBoard(
                    boardNumber,
                    boardSize,
                    matchTarget,
                    limousineCount,
                    colorCount)));
        }
    }

    private static void GetAuthoredProgressionRecipe(
        int boardNumber,
        out int boardSize,
        out int matchTarget,
        out int limousineCount,
        out int colorCount)
    {
        if (boardNumber <= 29)
        {
            int offset = boardNumber - 21;
            int[] limousines = { 1, 2, 1, 2, 3, 1, 2, 3, 1 };
            int[] colors = { 4, 3, 3, 3, 3, 4, 3, 3, 4 };
            boardSize = 3;
            matchTarget = 2;
            limousineCount = limousines[offset];
            colorCount = colors[offset];
            return;
        }

        if (boardNumber <= 39)
        {
            int offset = boardNumber - 30;
            int[] limousines = { 0, 1, 1, 2, 3, 4, 1, 2, 3, 4 };
            int[] colors = { 5, 5, 4, 4, 4, 4, 5, 4, 4, 4 };
            boardSize = 4;
            matchTarget = 3;
            limousineCount = limousines[offset];
            colorCount = colors[offset];
            return;
        }

        if (boardNumber <= 44)
        {
            boardSize = 4;
            matchTarget = 4;
            limousineCount = boardNumber - 40;
            colorCount = boardNumber == 40 ? 4 : 3;
            return;
        }

        boardSize = 5;
        if (boardNumber <= 48)
        {
            matchTarget = 4;
            limousineCount = boardNumber - 45;
            colorCount = 5;
            return;
        }

        matchTarget = 5;
        limousineCount = boardNumber - 45;
        colorCount = 4;
    }

    private static PieceSpec[] BuildSolvableCampaignBoard(
        int boardNumber,
        int boardSize,
        int matchTarget,
        int limousineCount,
        int colorCount,
        LevelDifficulty? refinedDifficulty = null)
    {
        int cellCount = boardSize * boardSize;
        int coloredPieceCount = matchTarget * colorCount;
        int policeCount = cellCount - limousineCount - coloredPieceCount;
        if (policeCount < 0)
        {
            Debug.LogError($"Board {boardNumber} recipe overfills its {boardSize}x{boardSize} grid.");
            return new PieceSpec[0];
        }

        int seed = boardNumber * 7919 + boardSize * 101 + matchTarget * 17;
        bool requiresChainedGeometry = refinedDifficulty.HasValue
            ? refinedDifficulty.Value != LevelDifficulty.Normal
            : boardNumber >= 30;
        if (requiresChainedGeometry)
        {
            int layoutAttempts = refinedDifficulty.HasValue ? 64 : boardNumber == 81 ? 64 : 1;
            for (int layoutAttempt = 0; layoutAttempt < layoutAttempts; layoutAttempt++)
            {
                var sequentialRandom = new System.Random(seed + layoutAttempt * 104729);
                if (!TryBuildSequentialCampaignRemovalOrder(
                    boardNumber,
                    boardSize,
                    limousineCount,
                    sequentialRandom,
                    out List<PieceSpec> sequentialOrder))
                    continue;
                if (refinedDifficulty.HasValue)
                    ApplyRefinedDifficultyColorTokens(
                        colorCount,
                        matchTarget,
                        policeCount,
                        refinedDifficulty.Value,
                        sequentialRandom,
                        sequentialOrder);
                else
                    ApplyCampaignColorTokens(
                        boardNumber,
                        colorCount,
                        matchTarget,
                        policeCount,
                        sequentialRandom,
                        sequentialOrder);
                PieceSpec[] sequentialResult = sequentialOrder.ToArray();
                if (ValidateCampaignBoard(boardSize, matchTarget, sequentialResult)
                    && (!refinedDifficulty.HasValue
                        || MeetsRefinedDifficultyGeometry(
                            boardSize,
                            sequentialResult,
                            null,
                            refinedDifficulty.Value,
                            out _,
                            out _))
                    && (boardNumber != 81 || HasMixedVisibleRows(boardSize, sequentialOrder)))
                    return sequentialResult;
            }

            if (refinedDifficulty.HasValue)
                throw new System.InvalidOperationException(
                    $"Could not construct {refinedDifficulty.Value} blocker-chain Board {boardNumber}.");
            Debug.LogError($"Could not construct sequential campaign Board {boardNumber}.");
            return new PieceSpec[0];
        }

        for (int attempt = 0; attempt < 96; attempt++)
        {
            var random = new System.Random(seed + attempt * 104729);
            if (!TryBuildCampaignTiling(boardSize, limousineCount, random, out List<CampaignTile> tiles))
                continue;
            if (!TryBuildCampaignRemovalOrder(
                boardSize,
                tiles,
                random,
                requiresChainedGeometry,
                out List<PieceSpec> removalOrder))
                continue;

            if (refinedDifficulty.HasValue)
                ApplyRefinedDifficultyColorTokens(
                    colorCount,
                    matchTarget,
                    policeCount,
                    refinedDifficulty.Value,
                    random,
                    removalOrder);
            else
                ApplyCampaignColorTokens(
                    boardNumber,
                    colorCount,
                    matchTarget,
                    policeCount,
                    random,
                    removalOrder);

            PieceSpec[] result = removalOrder.ToArray();
            if (ValidateCampaignBoard(boardSize, matchTarget, result))
                return result;
        }

        if (refinedDifficulty.HasValue)
            throw new System.InvalidOperationException(
                $"Could not construct {refinedDifficulty.Value} refined Board {boardNumber}.");
        Debug.LogError($"Could not construct authored campaign Board {boardNumber}.");
        return new PieceSpec[0];
    }

    private static bool HasMixedVisibleRows(int boardSize, List<PieceSpec> specifications)
    {
        for (int row = 0; row < boardSize; row++)
        {
            int directionMask = 0;
            int visibleCount = 0;
            for (int index = 0; index < specifications.Count; index++)
            {
                PieceSpec specification = specifications[index];
                if (specification.row != row || specification.color == PieceColor.Trash) continue;
                directionMask |= 1 << (int)specification.direction;
                visibleCount++;
            }
            if (visibleCount >= 3 && (directionMask & (directionMask - 1)) == 0)
                return false;
        }
        return true;
    }

    private static bool TryBuildSequentialCampaignRemovalOrder(
        int boardNumber,
        int boardSize,
        int limousineCount,
        System.Random random,
        out List<PieceSpec> removalOrder)
    {
        for (int pathAttempt = 0; pathAttempt < 192; pathAttempt++)
        {
            if (!TryBuildVariedCampaignPath(boardSize, random, out List<Vector2Int> path))
                continue;
            if (!TryChooseCampaignLimousines(path, limousineCount, random, out bool[] limousineStarts))
                continue;

            var candidateOrder = new List<PieceSpec>(path.Count - limousineCount);
            for (int pathIndex = 0; pathIndex < path.Count;)
            {
                Vector2Int current = path[pathIndex];
                Vector2Int step = pathIndex == 0
                    ? current - path[pathIndex + 1]
                    : path[pathIndex - 1] - current;
                int length = limousineStarts[pathIndex] ? 2 : 1;
                candidateOrder.Add(new PieceSpec(
                    current.y,
                    current.x,
                    PieceColor.Trash,
                    GridStepToDirection(step),
                    length));
                pathIndex += length;
            }

            if (!HasVariedCampaignDirections(boardNumber, boardSize, candidateOrder))
                continue;

            removalOrder = candidateOrder;
            return true;
        }

        removalOrder = null;
        return false;
    }

    private static bool TryBuildVariedCampaignPath(
        int boardSize,
        System.Random random,
        out List<Vector2Int> path)
    {
        var starts = new List<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(boardSize - 1, 0),
            new Vector2Int(0, boardSize - 1),
            new Vector2Int(boardSize - 1, boardSize - 1)
        };
        ShuffleCampaignList(starts, random);

        for (int startIndex = 0; startIndex < starts.Count; startIndex++)
        {
            var candidate = new List<Vector2Int>(boardSize * boardSize) { starts[startIndex] };
            var visited = new bool[boardSize, boardSize];
            visited[starts[startIndex].y, starts[startIndex].x] = true;
            int searchBudget = 120000;
            if (SearchCampaignHamiltonianPath(
                boardSize,
                random,
                visited,
                candidate,
                ref searchBudget))
            {
                path = candidate;
                return true;
            }
        }

        path = null;
        return false;
    }

    private static bool SearchCampaignHamiltonianPath(
        int boardSize,
        System.Random random,
        bool[,] visited,
        List<Vector2Int> path,
        ref int searchBudget)
    {
        if (path.Count == boardSize * boardSize) return true;
        if (--searchBudget <= 0) return false;

        Vector2Int current = path[path.Count - 1];
        var nextCells = new List<Vector2Int>(4);
        AddCampaignPathNeighbor(current.x + 1, current.y, boardSize, visited, nextCells);
        AddCampaignPathNeighbor(current.x - 1, current.y, boardSize, visited, nextCells);
        AddCampaignPathNeighbor(current.x, current.y + 1, boardSize, visited, nextCells);
        AddCampaignPathNeighbor(current.x, current.y - 1, boardSize, visited, nextCells);
        ShuffleCampaignList(nextCells, random);
        nextCells.Sort((left, right) =>
            CountCampaignPathOptions(left, boardSize, visited)
                .CompareTo(CountCampaignPathOptions(right, boardSize, visited)));

        for (int index = 0; index < nextCells.Count; index++)
        {
            Vector2Int next = nextCells[index];
            visited[next.y, next.x] = true;
            path.Add(next);
            if (SearchCampaignHamiltonianPath(
                boardSize,
                random,
                visited,
                path,
                ref searchBudget))
                return true;
            path.RemoveAt(path.Count - 1);
            visited[next.y, next.x] = false;
        }
        return false;
    }

    private static void AddCampaignPathNeighbor(
        int col,
        int row,
        int boardSize,
        bool[,] visited,
        List<Vector2Int> neighbors)
    {
        if (row >= 0 && row < boardSize && col >= 0 && col < boardSize && !visited[row, col])
            neighbors.Add(new Vector2Int(col, row));
    }

    private static int CountCampaignPathOptions(
        Vector2Int cell,
        int boardSize,
        bool[,] visited)
    {
        int count = 0;
        if (cell.x + 1 < boardSize && !visited[cell.y, cell.x + 1]) count++;
        if (cell.x > 0 && !visited[cell.y, cell.x - 1]) count++;
        if (cell.y + 1 < boardSize && !visited[cell.y + 1, cell.x]) count++;
        if (cell.y > 0 && !visited[cell.y - 1, cell.x]) count++;
        return count;
    }

    private static bool TryChooseCampaignLimousines(
        List<Vector2Int> path,
        int limousineCount,
        System.Random random,
        out bool[] limousineStarts)
    {
        var candidates = new List<int>();
        for (int index = 1; index < path.Count - 1; index++)
            if (path[index - 1] + path[index + 1] == path[index] * 2)
                candidates.Add(index);

        limousineStarts = new bool[path.Count];
        bool[] consumed = new bool[path.Count];
        for (int attempt = 0; attempt < 48; attempt++)
        {
            System.Array.Clear(limousineStarts, 0, limousineStarts.Length);
            System.Array.Clear(consumed, 0, consumed.Length);
            ShuffleCampaignList(candidates, random);
            int selected = 0;
            for (int index = 0; index < candidates.Count && selected < limousineCount; index++)
            {
                int start = candidates[index];
                if (consumed[start] || consumed[start + 1]) continue;
                limousineStarts[start] = true;
                consumed[start] = true;
                consumed[start + 1] = true;
                selected++;
            }
            if (selected == limousineCount) return true;
        }
        return false;
    }

    private static bool HasVariedCampaignDirections(
        int boardNumber,
        int boardSize,
        List<PieceSpec> specifications)
    {
        int[] directionCounts = new int[4];
        int directionMask = 0;
        for (int index = 0; index < specifications.Count; index++)
        {
            int direction = (int)specifications[index].direction;
            directionCounts[direction]++;
            directionMask |= 1 << direction;
        }

        int distinctDirections = 0;
        for (int direction = 0; direction < directionCounts.Length; direction++)
            if (directionCounts[direction] > 0) distinctDirections++;
        if (distinctDirections < 3) return false;

        int largestDirectionGroup = 0;
        for (int direction = 0; direction < directionCounts.Length; direction++)
            largestDirectionGroup = Mathf.Max(largestDirectionGroup, directionCounts[direction]);
        if (largestDirectionGroup * 5 > specifications.Count * 3 + 2) return false;

        int uniformRows = 0;
        int uniformColumns = 0;
        for (int line = 0; line < boardSize; line++)
        {
            int rowMask = 0;
            int rowCount = 0;
            int colMask = 0;
            int colCount = 0;
            for (int index = 0; index < specifications.Count; index++)
            {
                PieceSpec specification = specifications[index];
                if (specification.row == line)
                {
                    rowMask |= 1 << (int)specification.direction;
                    rowCount++;
                }
                if (specification.col == line)
                {
                    colMask |= 1 << (int)specification.direction;
                    colCount++;
                }
            }
            if (rowCount >= 3 && (rowMask & (rowMask - 1)) == 0) uniformRows++;
            if (colCount >= 3 && (colMask & (colMask - 1)) == 0) uniformColumns++;
        }

        if (boardNumber == 81)
            return uniformRows == 0 && uniformColumns <= 1 && directionMask != 0;
        int allowedUniformLines = boardNumber >= 45 ? 1 : 2;
        return uniformRows + uniformColumns <= allowedUniformLines && directionMask != 0;
    }

    private static ExitDirection GridStepToDirection(Vector2Int step)
    {
        if (step.y < 0) return ExitDirection.Up;
        if (step.y > 0) return ExitDirection.Down;
        return step.x < 0 ? ExitDirection.Left : ExitDirection.Right;
    }

    private static void ApplyCampaignColorTokens(
        int boardNumber,
        int colorCount,
        int matchTarget,
        int policeCount,
        System.Random random,
        List<PieceSpec> removalOrder)
    {
        List<PieceColor> tokens = BuildCampaignColorTokens(
            boardNumber,
            colorCount,
            matchTarget,
            policeCount,
            random);
        if (tokens.Count != removalOrder.Count) return;

        for (int index = 0; index < removalOrder.Count; index++)
        {
            PieceSpec specification = removalOrder[index];
            specification.color = tokens[index];
            removalOrder[index] = specification;
        }
    }

    private static void ApplyRefinedDifficultyColorTokens(
        int colorCount,
        int matchTarget,
        int policeCount,
        LevelDifficulty difficulty,
        System.Random random,
        List<PieceSpec> removalOrder)
    {
        var colors = new List<PieceColor>
        {
            PieceColor.Red,
            PieceColor.Green,
            PieceColor.Blue,
            PieceColor.Purple,
            PieceColor.Yellow,
            PieceColor.Pink
        };
        ShuffleCampaignList(colors, random);
        colors.RemoveRange(colorCount, colors.Count - colorCount);
        int[] setCounts = new int[colorCount];
        for (int index = 0; index < setCounts.Length; index++) setCounts[index] = 1;
        List<PieceColor> tokens = BuildRefinedSetSequence(
            colors,
            setCounts,
            matchTarget,
            difficulty,
            random);

        int coloredTokenCount = tokens.Count;
        for (int policeIndex = 0; policeIndex < policeCount; policeIndex++)
        {
            int insertionIndex = Mathf.RoundToInt(
                (policeIndex + 1f) * coloredTokenCount / (policeCount + 1f));
            insertionIndex = Mathf.Clamp(insertionIndex + policeIndex, 0, tokens.Count);
            tokens.Insert(insertionIndex, PieceColor.Trash);
        }
        if (tokens.Count != removalOrder.Count)
            throw new System.InvalidOperationException(
                $"Refined color sequence has {tokens.Count} tokens for {removalOrder.Count} cars.");

        for (int index = 0; index < removalOrder.Count; index++)
        {
            PieceSpec specification = removalOrder[index];
            specification.color = tokens[index];
            removalOrder[index] = specification;
        }
    }

    private static bool MeetsRefinedDifficultyGeometry(
        int boardSize,
        PieceSpec[] specifications,
        GarageSpec[] garages,
        LevelDifficulty difficulty,
        out int initialMovableCount,
        out int longestBlockerChain)
    {
        EvaluateRefinedDifficultyGeometry(
            boardSize,
            specifications,
            garages,
            out initialMovableCount,
            out longestBlockerChain);
        if (difficulty == LevelDifficulty.Normal) return true;

        int pieceCount = specifications != null ? specifications.Length : 0;
        if (garages != null) pieceCount += garages.Length;
        int requiredDepth = difficulty == LevelDifficulty.SuperHard
            ? Mathf.Min(6, Mathf.Max(4, pieceCount - 2))
            : Mathf.Min(4, Mathf.Max(3, pieceCount - 2));
        bool hasGarage = garages != null && garages.Length > 0;
        int maximumOpeningMoves;
        if (hasGarage)
            maximumOpeningMoves = difficulty == LevelDifficulty.SuperHard ? 2 : 4;
        else
            maximumOpeningMoves = difficulty == LevelDifficulty.SuperHard ? 1 : 2;
        return initialMovableCount <= maximumOpeningMoves
            && longestBlockerChain >= requiredDepth;
    }

    private static void EvaluateRefinedDifficultyGeometry(
        int boardSize,
        PieceSpec[] specifications,
        GarageSpec[] garages,
        out int initialMovableCount,
        out int longestBlockerChain)
    {
        var pieces = new List<PieceSpec>();
        if (specifications != null) pieces.AddRange(specifications);
        if (garages != null)
        {
            for (int index = 0; index < garages.Length; index++)
                pieces.Add(new PieceSpec(
                    garages[index].row + 1,
                    garages[index].col,
                    PieceColor.Red,
                    ExitDirection.Down));
        }

        int[,] owners = new int[boardSize, boardSize];
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++) owners[row, col] = -1;
        for (int index = 0; index < pieces.Count; index++)
        {
            PieceSpec piece = pieces[index];
            Vector2Int behind = DirectionToGridStep(piece.direction);
            for (int offset = 0; offset < piece.cellLength; offset++)
            {
                int row = piece.row - behind.y * offset;
                int col = piece.col - behind.x * offset;
                if (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
                    owners[row, col] = index;
            }
        }

        var garageCells = new HashSet<int>();
        if (garages != null)
            for (int index = 0; index < garages.Length; index++)
                garageCells.Add(garages[index].row * boardSize + garages[index].col);

        int[] blockers = new int[pieces.Count];
        initialMovableCount = 0;
        for (int index = 0; index < pieces.Count; index++)
        {
            blockers[index] = -1;
            PieceSpec piece = pieces[index];
            Vector2Int step = DirectionToGridStep(piece.direction);
            int row = piece.row + step.y;
            int col = piece.col + step.x;
            while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
            {
                if (garageCells.Contains(row * boardSize + col))
                {
                    blockers[index] = -2;
                    break;
                }
                int owner = owners[row, col];
                if (owner >= 0 && owner != index)
                {
                    blockers[index] = owner;
                    break;
                }
                row += step.y;
                col += step.x;
            }
            bool coveredOpening = specifications != null
                && index < specifications.Length
                && specifications[index].isRevealBox;
            if (blockers[index] == -1 && !coveredOpening) initialMovableCount++;
        }

        longestBlockerChain = 0;
        for (int start = 0; start < blockers.Length; start++)
        {
            var visited = new HashSet<int>();
            int current = start;
            int depth = 0;
            while (current >= 0 && current < blockers.Length && visited.Add(current))
            {
                int blocker = blockers[current];
                if (blocker < 0) break;
                depth++;
                current = blocker;
            }
            longestBlockerChain = Mathf.Max(longestBlockerChain, depth);
        }
    }

    private static bool TryBuildCampaignTiling(
        int boardSize,
        int limousineCount,
        System.Random random,
        out List<CampaignTile> tiles)
    {
        tiles = new List<CampaignTile>();
        var adjacentPairs = new List<CampaignTile>();
        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
        {
            if (col + 1 < boardSize)
                adjacentPairs.Add(new CampaignTile(row, col, row, col + 1));
            if (row + 1 < boardSize)
                adjacentPairs.Add(new CampaignTile(row, col, row + 1, col));
        }
        ShuffleCampaignList(adjacentPairs, random);

        bool[,] occupied = new bool[boardSize, boardSize];
        for (int pairIndex = 0; pairIndex < adjacentPairs.Count && tiles.Count < limousineCount; pairIndex++)
        {
            CampaignTile pair = adjacentPairs[pairIndex];
            if (occupied[pair.firstRow, pair.firstCol]
                || occupied[pair.secondRow, pair.secondCol])
                continue;

            occupied[pair.firstRow, pair.firstCol] = true;
            occupied[pair.secondRow, pair.secondCol] = true;
            tiles.Add(pair);
        }
        if (tiles.Count != limousineCount) return false;

        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
            if (!occupied[row, col]) tiles.Add(new CampaignTile(row, col));
        return true;
    }

    private static bool TryBuildCampaignRemovalOrder(
        int boardSize,
        List<CampaignTile> tiles,
        System.Random random,
        bool requireSequentialUnlock,
        out List<PieceSpec> removalOrder)
    {
        removalOrder = new List<PieceSpec>(tiles.Count);
        bool[] remaining = new bool[tiles.Count];
        for (int index = 0; index < remaining.Length; index++) remaining[index] = true;

        int previouslyRemovedTileIndex = -1;
        for (int removedCount = 0; removedCount < tiles.Count; removedCount++)
        {
            bool[,] occupied = BuildCampaignOccupancy(boardSize, tiles, remaining);
            var options = new List<CampaignExitOption>();
            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                if (!remaining[tileIndex]) continue;
                AddCampaignExitOptions(boardSize, occupied, tiles[tileIndex], tileIndex, options);
            }
            if (options.Count == 0) return false;

            if (requireSequentialUnlock && previouslyRemovedTileIndex >= 0)
            {
                CampaignTile previousTile = tiles[previouslyRemovedTileIndex];
                var sequentialOptions = new List<CampaignExitOption>();
                for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
                {
                    CampaignExitOption option = options[optionIndex];
                    if (CampaignExitRayContainsTile(boardSize, option, previousTile))
                        sequentialOptions.Add(option);
                }
                if (sequentialOptions.Count == 0) return false;
                options = sequentialOptions;
            }

            CampaignExitOption selected = options[random.Next(options.Count)];
            CampaignTile tile = tiles[selected.tileIndex];
            removalOrder.Add(new PieceSpec(
                selected.leadingRow,
                selected.leadingCol,
                PieceColor.Trash,
                selected.direction,
                tile.isLimousine ? 2 : 1));
            remaining[selected.tileIndex] = false;
            previouslyRemovedTileIndex = selected.tileIndex;
        }
        return true;
    }

    private static bool CampaignExitRayContainsTile(
        int boardSize,
        CampaignExitOption option,
        CampaignTile tile)
    {
        Vector2Int step = DirectionToGridStep(option.direction);
        int row = option.leadingRow + step.y;
        int col = option.leadingCol + step.x;
        while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
        {
            if ((row == tile.firstRow && col == tile.firstCol)
                || (tile.isLimousine && row == tile.secondRow && col == tile.secondCol))
                return true;
            row += step.y;
            col += step.x;
        }
        return false;
    }

    private static bool[,] BuildCampaignOccupancy(
        int boardSize,
        List<CampaignTile> tiles,
        bool[] remaining)
    {
        bool[,] occupied = new bool[boardSize, boardSize];
        for (int index = 0; index < tiles.Count; index++)
        {
            if (!remaining[index]) continue;
            CampaignTile tile = tiles[index];
            occupied[tile.firstRow, tile.firstCol] = true;
            if (tile.isLimousine)
                occupied[tile.secondRow, tile.secondCol] = true;
        }
        return occupied;
    }

    private static void AddCampaignExitOptions(
        int boardSize,
        bool[,] occupied,
        CampaignTile tile,
        int tileIndex,
        List<CampaignExitOption> options)
    {
        if (!tile.isLimousine)
        {
            TryAddCampaignExitOption(boardSize, occupied, tileIndex, tile.firstRow, tile.firstCol, ExitDirection.Up, options);
            TryAddCampaignExitOption(boardSize, occupied, tileIndex, tile.firstRow, tile.firstCol, ExitDirection.Down, options);
            TryAddCampaignExitOption(boardSize, occupied, tileIndex, tile.firstRow, tile.firstCol, ExitDirection.Left, options);
            TryAddCampaignExitOption(boardSize, occupied, tileIndex, tile.firstRow, tile.firstCol, ExitDirection.Right, options);
            return;
        }

        if (tile.firstRow == tile.secondRow)
        {
            int leftCol = Mathf.Min(tile.firstCol, tile.secondCol);
            int rightCol = Mathf.Max(tile.firstCol, tile.secondCol);
            TryAddCampaignExitOption(boardSize, occupied, tileIndex, tile.firstRow, leftCol, ExitDirection.Left, options);
            TryAddCampaignExitOption(boardSize, occupied, tileIndex, tile.firstRow, rightCol, ExitDirection.Right, options);
            return;
        }

        int topRow = Mathf.Min(tile.firstRow, tile.secondRow);
        int bottomRow = Mathf.Max(tile.firstRow, tile.secondRow);
        TryAddCampaignExitOption(boardSize, occupied, tileIndex, topRow, tile.firstCol, ExitDirection.Up, options);
        TryAddCampaignExitOption(boardSize, occupied, tileIndex, bottomRow, tile.firstCol, ExitDirection.Down, options);
    }

    private static void TryAddCampaignExitOption(
        int boardSize,
        bool[,] occupied,
        int tileIndex,
        int leadingRow,
        int leadingCol,
        ExitDirection direction,
        List<CampaignExitOption> options)
    {
        Vector2Int step = DirectionToGridStep(direction);
        int row = leadingRow + step.y;
        int col = leadingCol + step.x;
        while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
        {
            if (occupied[row, col]) return;
            row += step.y;
            col += step.x;
        }
        options.Add(new CampaignExitOption(tileIndex, leadingRow, leadingCol, direction));
    }

    private static List<PieceColor> BuildCampaignColorTokens(
        int boardNumber,
        int colorCount,
        int matchTarget,
        int policeCount,
        System.Random random)
    {
        var availableColors = new List<PieceColor>
        {
            PieceColor.Red,
            PieceColor.Green,
            PieceColor.Blue,
            PieceColor.Purple,
            PieceColor.Yellow
        };
        if (colorCount > availableColors.Count)
            availableColors.Add(PieceColor.Pink);
        ShuffleCampaignList(availableColors, random);
        if (RequiresLegacySideParkingRedesign(boardNumber))
        {
            // These boards keep the legacy blue adjacency rule. Put blue in
            // the held-color position of the three-family parking chain so it
            // must return from the side bay beside the other blue cars.
            availableColors.Remove(PieceColor.Blue);
            availableColors.Insert(Mathf.Min(1, availableColors.Count), PieceColor.Blue);
        }

        var tokens = new List<PieceColor>();
        int interlockedColorCount = GetSideParkingChainColorCount(boardNumber, colorCount);
        if (interlockedColorCount >= 2)
        {
            // This is the same exchange rhythm used by the strongest demos:
            // collect all but one A, hold B, finish A; introduce C, finish B
            // by swapping B out of the side bay; continue the occupant chain,
            // then return the final held color to complete it.
            AddCampaignColorCopies(tokens, availableColors[0], matchTarget - 1);
            tokens.Add(availableColors[1]);
            tokens.Add(availableColors[0]);

            for (int colorIndex = 1; colorIndex < interlockedColorCount - 1; colorIndex++)
            {
                tokens.Add(availableColors[colorIndex + 1]);
                AddCampaignColorCopies(tokens, availableColors[colorIndex], matchTarget - 1);
            }
            AddCampaignColorCopies(
                tokens,
                availableColors[interlockedColorCount - 1],
                matchTarget - 1);
        }
        else if (interlockedColorCount == 1)
        {
            AddCampaignColorCopies(tokens, availableColors[0], matchTarget);
        }

        for (int colorIndex = interlockedColorCount; colorIndex < colorCount; colorIndex++)
            AddCampaignColorCopies(tokens, availableColors[colorIndex], matchTarget);

        int coloredTokenCount = tokens.Count;
        for (int policeIndex = 0; policeIndex < policeCount; policeIndex++)
        {
            int insertionIndex = Mathf.RoundToInt(
                (policeIndex + 1f) * coloredTokenCount / (policeCount + 1f));
            insertionIndex = Mathf.Clamp(insertionIndex + policeIndex, 0, tokens.Count);
            tokens.Insert(insertionIndex, PieceColor.Trash);
        }
        return tokens;
    }

    private static int GetSideParkingChainColorCount(int boardNumber, int colorCount)
    {
        if (boardNumber < 30) return 1;
        if (boardNumber == 30) return Mathf.Min(2, colorCount);
        if (boardNumber <= 32) return Mathf.Min(3, colorCount);
        if (boardNumber <= 35) return Mathf.Min(4, colorCount);
        if (boardNumber == 36) return Mathf.Min(5, colorCount);
        if (boardNumber <= 40) return Mathf.Min(4, colorCount);
        if (boardNumber <= 42) return Mathf.Min(3 + (boardNumber - 40), colorCount);
        if (boardNumber <= 44) return colorCount;
        if (boardNumber == 45) return Mathf.Min(3, colorCount);
        if (boardNumber == 46) return Mathf.Min(4, colorCount);
        return colorCount;
    }

    private static bool RequiresLegacySideParkingRedesign(int boardNumber)
    {
        switch (boardNumber)
        {
            case 52:
            case 54:
            case 56:
            case 57:
            case 58:
            case 60:
            case 61:
            case 62:
            case 63:
            case 64:
            case 66:
            case 67:
            case 68:
            case 70:
                return true;
            default:
                return false;
        }
    }

    private static void AddCampaignColorCopies(
        List<PieceColor> tokens,
        PieceColor color,
        int count)
    {
        for (int index = 0; index < count; index++) tokens.Add(color);
    }

    private static bool ValidateCampaignBoard(
        int boardSize,
        int matchTarget,
        PieceSpec[] specifications)
    {
        bool[,] occupied = new bool[boardSize, boardSize];
        int occupiedCellCount = 0;
        int[] colorCounts = new int[7];
        for (int index = 0; index < specifications.Length; index++)
        {
            PieceSpec spec = specifications[index];
            Vector2Int step = DirectionToGridStep(spec.direction);
            for (int offset = 0; offset < spec.cellLength; offset++)
            {
                int row = spec.row - step.y * offset;
                int col = spec.col - step.x * offset;
                if (row < 0 || row >= boardSize || col < 0 || col >= boardSize || occupied[row, col])
                    return false;
                occupied[row, col] = true;
                occupiedCellCount++;
            }
            if (spec.color != PieceColor.Trash)
                colorCounts[(int)spec.color]++;
        }
        if (occupiedCellCount != boardSize * boardSize) return false;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
        {
            int color = (int)PlayableColors[colorIndex];
            if (colorCounts[color] != 0 && colorCounts[color] != matchTarget) return false;
        }

        for (int index = 0; index < specifications.Length; index++)
        {
            PieceSpec spec = specifications[index];
            Vector2Int step = DirectionToGridStep(spec.direction);
            int row = spec.row + step.y;
            int col = spec.col + step.x;
            while (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
            {
                if (occupied[row, col]) return false;
                row += step.y;
                col += step.x;
            }
            for (int offset = 0; offset < spec.cellLength; offset++)
                occupied[spec.row - step.y * offset, spec.col - step.x * offset] = false;
        }
        return true;
    }

    private static void ShuffleCampaignList<T>(List<T> list, System.Random random)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            T value = list[index];
            list[index] = list[swapIndex];
            list[swapIndex] = value;
        }
    }

    private List<PrototypeLevel> GetActiveLevels()
    {
        return isDemoMode ? demoLevels : levels;
    }

    private void BuildDemoLevels()
    {
        demoLevels.Clear();
        demoLevels.Add(new PrototypeLevel(1, 3, 3, BuildDemoThreeByThree()));
        demoLevels.Add(new PrototypeLevel(2, 4, 3, BuildDemoLimousineFourByFour()));
        demoLevels.Add(new PrototypeLevel(3, 5, 5, BuildDemoFullColorFiveByFive()));
        demoLevels.Add(new PrototypeLevel(4, 5, 4, BuildDemoDifficultFiveByFive()));
        Debug.Log($"Loaded {demoLevels.Count} separate showcase demo boards.");
    }

    private static PieceSpec[] BuildDemoThreeByThree()
    {
        // Compact but unforgiving: several edge cars can leave immediately,
        // but the only clean color order is Red -> Purple -> Blue.
        return new[]
        {
            new PieceSpec(0, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(0, 1, PieceColor.Red, ExitDirection.Right),
            new PieceSpec(0, 2, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(1, 0, PieceColor.Blue, ExitDirection.Down),
            new PieceSpec(1, 1, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(1, 2, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Right),
            new PieceSpec(2, 1, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(2, 2, PieceColor.Purple, ExitDirection.Up)
        };
    }

    private static PieceSpec[] BuildDemoFullColorFiveByFive()
    {
        // A completely full board with no police cars. Sixteen cars appear
        // available at the start, but the only safe family order is
        // Red -> Blue -> Purple -> Yellow -> Green.
        return new[]
        {
            new PieceSpec(0, 0, PieceColor.Green, ExitDirection.Up),
            new PieceSpec(0, 1, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(0, 2, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(0, 3, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(0, 4, PieceColor.Yellow, ExitDirection.Up),

            new PieceSpec(1, 0, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(1, 1, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(1, 2, PieceColor.Yellow, ExitDirection.Left),
            new PieceSpec(1, 3, PieceColor.Green, ExitDirection.Right),
            new PieceSpec(1, 4, PieceColor.Red, ExitDirection.Right),

            new PieceSpec(2, 0, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(2, 1, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(3, 4, PieceColor.Yellow, ExitDirection.Left),
            new PieceSpec(2, 3, PieceColor.Green, ExitDirection.Right),
            new PieceSpec(2, 2, PieceColor.Blue, ExitDirection.Right),

            new PieceSpec(3, 0, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(3, 1, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(3, 2, PieceColor.Green, ExitDirection.Up),
            new PieceSpec(3, 3, PieceColor.Green, ExitDirection.Up),
            new PieceSpec(2, 4, PieceColor.Purple, ExitDirection.Right),

            new PieceSpec(4, 0, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(4, 1, PieceColor.Purple, ExitDirection.Down),
            new PieceSpec(4, 2, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(4, 3, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(4, 4, PieceColor.Purple, ExitDirection.Right)
        };
    }

    private static PieceSpec[] BuildDemoLimousineFourByFour()
    {
        // Four different limousine colors fill the central lanes. Many edge
        // cars are tempting, while the intended order is
        // Blue -> Red -> Purple -> Yellow.
        return new[]
        {
            new PieceSpec(3, 3, PieceColor.Purple, ExitDirection.Down, 2),
            new PieceSpec(3, 1, PieceColor.Red, ExitDirection.Down, 2),
            new PieceSpec(1, 0, PieceColor.Yellow, ExitDirection.Left, 2),
            new PieceSpec(1, 2, PieceColor.Blue, ExitDirection.Left, 2),

            new PieceSpec(0, 1, PieceColor.Purple, ExitDirection.Up),
            new PieceSpec(0, 0, PieceColor.Yellow, ExitDirection.Left),
            new PieceSpec(0, 3, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(2, 0, PieceColor.Blue, ExitDirection.Left),
            new PieceSpec(0, 2, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(3, 0, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(2, 2, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(3, 2, PieceColor.Blue, ExitDirection.Down)
        };
    }

    private static PieceSpec[] BuildDemoDifficultFiveByFive()
    {
        // Publisher showcase finale: two limousines, three police blockers and
        // five interlocked color families. Clear the police routes first; the
        // only safe family order is Red -> Blue -> Purple -> Yellow -> Green.
        return new[]
        {
            new PieceSpec(3, 4, PieceColor.Blue, ExitDirection.Right, 2),
            new PieceSpec(1, 1, PieceColor.Yellow, ExitDirection.Left, 2),

            new PieceSpec(4, 0, PieceColor.Red, ExitDirection.Left),
            new PieceSpec(4, 3, PieceColor.Yellow, ExitDirection.Down),
            // This yellow car now points directly into the lower police car.
            new PieceSpec(3, 1, PieceColor.Yellow, ExitDirection.Down),
            new PieceSpec(0, 2, PieceColor.Trash, ExitDirection.Up),
            new PieceSpec(2, 2, PieceColor.Green, ExitDirection.Up),
            new PieceSpec(1, 0, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(1, 3, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(4, 2, PieceColor.Red, ExitDirection.Down),
            new PieceSpec(3, 0, PieceColor.Blue, ExitDirection.Left),
            // Purple and green also point directly into police positions. The
            // blockers must be removed before either colored route can open.
            new PieceSpec(0, 1, PieceColor.Purple, ExitDirection.Right),
            new PieceSpec(2, 4, PieceColor.Green, ExitDirection.Up),
            new PieceSpec(0, 4, PieceColor.Blue, ExitDirection.Up),
            new PieceSpec(2, 1, PieceColor.Green, ExitDirection.Down),
            new PieceSpec(3, 2, PieceColor.Green, ExitDirection.Left),
            new PieceSpec(4, 1, PieceColor.Trash, ExitDirection.Right),
            new PieceSpec(4, 4, PieceColor.Yellow, ExitDirection.Up),
            new PieceSpec(0, 0, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(0, 3, PieceColor.Red, ExitDirection.Up),
            new PieceSpec(2, 0, PieceColor.Purple, ExitDirection.Left),
            new PieceSpec(1, 4, PieceColor.Trash, ExitDirection.Left),
            new PieceSpec(2, 3, PieceColor.Purple, ExitDirection.Up)
        };
    }

    private static PrototypeLevel CreatePrototypeLevel(UnityGameManager.LevelConfig source, int boardNumber)
    {
        return new PrototypeLevel(boardNumber, source);
    }

    private static PieceSpec[] BuildPrototypePieceSpecs(UnityGameManager.LevelConfig source)
    {
        var pieces = new List<PieceSpec>();
        bool[] consumed = new bool[source.blocks.Count];
        int limousineTarget = source.id <= 30 ? 0 : source.id <= 40 ? 1 : 2;
        int limousineCount = 0;

        while (limousineCount < limousineTarget)
        {
            LimousineCandidate candidate;
            bool found = TryFindAlignedLimousineCandidate(source, consumed, limousineCount, out candidate)
                || TryFindAdjacentLimousineCandidate(source, consumed, limousineCount, out candidate);
            if (!found) break;

            UnityGameManager.BlockData colorBlock = source.blocks[candidate.carIndex];
            pieces.Add(new PieceSpec(
                candidate.leadingRow,
                candidate.leadingCol,
                ConvertColor(colorBlock.color, source.id),
                candidate.direction,
                2));
            consumed[candidate.carIndex] = true;
            consumed[candidate.neutralIndex] = true;
            limousineCount++;
        }

        for (int index = 0; index < source.blocks.Count; index++)
        {
            if (consumed[index]) continue;
            UnityGameManager.BlockData block = source.blocks[index];
            pieces.Add(new PieceSpec(
                block.row,
                block.col,
                ConvertColor(block.color, source.id),
                ConvertDirection(block.direction)));
        }

        return pieces.ToArray();
    }

    private static bool TryFindAlignedLimousineCandidate(
        UnityGameManager.LevelConfig source,
        bool[] consumed,
        int limousineNumber,
        out LimousineCandidate candidate)
    {
        int count = source.blocks.Count;
        int start = count == 0 ? 0 : (source.id * 7 + limousineNumber * 5) % count;
        for (int offset = 0; offset < count; offset++)
        {
            int carIndex = (start + offset) % count;
            if (consumed[carIndex] || source.blocks[carIndex].color == UnityGameManager.BlockColor.Neutral) continue;

            UnityGameManager.BlockData car = source.blocks[carIndex];
            ExitDirection direction = ConvertDirection(car.direction);
            Vector2Int step = DirectionToGridStep(direction);

            int neutralBehind = FindAvailableNeutral(source, consumed, car.row - step.y, car.col - step.x);
            if (neutralBehind >= 0)
            {
                candidate = new LimousineCandidate(carIndex, neutralBehind, car.row, car.col, direction);
                return true;
            }

            int neutralAhead = FindAvailableNeutral(source, consumed, car.row + step.y, car.col + step.x);
            if (neutralAhead >= 0)
            {
                UnityGameManager.BlockData leadingCell = source.blocks[neutralAhead];
                candidate = new LimousineCandidate(carIndex, neutralAhead, leadingCell.row, leadingCell.col, direction);
                return true;
            }
        }

        candidate = default;
        return false;
    }

    private static bool TryFindAdjacentLimousineCandidate(
        UnityGameManager.LevelConfig source,
        bool[] consumed,
        int limousineNumber,
        out LimousineCandidate candidate)
    {
        int count = source.blocks.Count;
        int start = count == 0 ? 0 : (source.id * 11 + limousineNumber * 3) % count;
        for (int offset = 0; offset < count; offset++)
        {
            int carIndex = (start + offset) % count;
            if (consumed[carIndex] || source.blocks[carIndex].color == UnityGameManager.BlockColor.Neutral) continue;

            UnityGameManager.BlockData car = source.blocks[carIndex];
            int[,] neighbors = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };
            for (int neighborIndex = 0; neighborIndex < 4; neighborIndex++)
            {
                int neutralIndex = FindAvailableNeutral(
                    source,
                    consumed,
                    car.row + neighbors[neighborIndex, 0],
                    car.col + neighbors[neighborIndex, 1]);
                if (neutralIndex < 0) continue;

                UnityGameManager.BlockData neutral = source.blocks[neutralIndex];
                ChooseOutwardLimousineDirection(
                    car.row,
                    car.col,
                    neutral.row,
                    neutral.col,
                    source.boardSize,
                    out int leadingRow,
                    out int leadingCol,
                    out ExitDirection direction);
                candidate = new LimousineCandidate(carIndex, neutralIndex, leadingRow, leadingCol, direction);
                return true;
            }
        }

        candidate = default;
        return false;
    }

    private static int FindAvailableNeutral(UnityGameManager.LevelConfig source, bool[] consumed, int row, int col)
    {
        for (int index = 0; index < source.blocks.Count; index++)
        {
            if (consumed[index]) continue;
            UnityGameManager.BlockData block = source.blocks[index];
            if (block.row == row && block.col == col && block.color == UnityGameManager.BlockColor.Neutral)
                return index;
        }

        return -1;
    }

    private static void ChooseOutwardLimousineDirection(
        int firstRow,
        int firstCol,
        int secondRow,
        int secondCol,
        int boardSize,
        out int leadingRow,
        out int leadingCol,
        out ExitDirection direction)
    {
        if (firstRow == secondRow)
        {
            int leftCol = Mathf.Min(firstCol, secondCol);
            int rightCol = Mathf.Max(firstCol, secondCol);
            bool exitLeft = leftCol <= boardSize - 1 - rightCol;
            leadingRow = firstRow;
            leadingCol = exitLeft ? leftCol : rightCol;
            direction = exitLeft ? ExitDirection.Left : ExitDirection.Right;
            return;
        }

        int topRow = Mathf.Min(firstRow, secondRow);
        int bottomRow = Mathf.Max(firstRow, secondRow);
        bool exitUp = topRow <= boardSize - 1 - bottomRow;
        leadingRow = exitUp ? topRow : bottomRow;
        leadingCol = firstCol;
        direction = exitUp ? ExitDirection.Up : ExitDirection.Down;
    }

    private static PieceColor ConvertColor(UnityGameManager.BlockColor color, int boardNumber)
    {
        PieceColor sourceColor;
        switch (color)
        {
            case UnityGameManager.BlockColor.Red: sourceColor = PieceColor.Red; break;
            case UnityGameManager.BlockColor.Green: sourceColor = PieceColor.Green; break;
            case UnityGameManager.BlockColor.Blue: sourceColor = PieceColor.Blue; break;
            case UnityGameManager.BlockColor.Yellow: sourceColor = PieceColor.Yellow; break;
            default: return PieceColor.Trash;
        }

        return RemapEarlyLevelColor(sourceColor, boardNumber);
    }

    private static PieceColor RemapEarlyLevelColor(PieceColor sourceColor, int boardNumber)
    {
        // These are one-to-one palette swaps. They change only the displayed
        // match colors; the database positions, directions, group sizes, and
        // special adjacency rule remain attached to their original families.
        if (boardNumber >= 1 && boardNumber <= 5)
        {
            if (sourceColor == PieceColor.Red) return PieceColor.Blue;
            if (sourceColor == PieceColor.Green) return PieceColor.Purple;
            if (sourceColor == PieceColor.Blue) return PieceColor.Red;
            if (sourceColor == PieceColor.Yellow) return PieceColor.Green;
        }
        else if (boardNumber <= 10)
        {
            if (sourceColor == PieceColor.Red) return PieceColor.Yellow;
            if (sourceColor == PieceColor.Green) return PieceColor.Blue;
            if (sourceColor == PieceColor.Blue) return PieceColor.Purple;
            if (sourceColor == PieceColor.Yellow) return PieceColor.Red;
        }
        else if (boardNumber <= 15)
        {
            if (sourceColor == PieceColor.Red) return PieceColor.Purple;
            if (sourceColor == PieceColor.Green) return PieceColor.Green;
            if (sourceColor == PieceColor.Blue) return PieceColor.Blue;
            if (sourceColor == PieceColor.Yellow) return PieceColor.Yellow;
        }

        return sourceColor;
    }

    private void ConfigureActiveColorGoals(
        UnityGameManager.LevelConfig source,
        PieceSpec[] specifications)
    {
        PieceColor mappedRed = RemapEarlyLevelColor(PieceColor.Red, source.id);
        PieceColor mappedGreen = RemapEarlyLevelColor(PieceColor.Green, source.id);
        PieceColor mappedBlue = RemapEarlyLevelColor(PieceColor.Blue, source.id);
        PieceColor mappedYellow = RemapEarlyLevelColor(PieceColor.Yellow, source.id);

        SetActiveTarget(mappedRed, ContainsColor(specifications, mappedRed) ? source.matchTarget : 0);
        SetActiveTarget(mappedGreen, ContainsColor(specifications, mappedGreen) ? source.matchTarget : 0);
        SetActiveTarget(mappedBlue, ContainsColor(specifications, mappedBlue) ? source.matchTarget : 0);
        SetActiveTarget(mappedYellow, ContainsColor(specifications, mappedYellow) ? source.matchTarget : 0);
        if (GetMatchTarget(mappedBlue) > 0)
            activeAdjacentMatchColors.Add(mappedBlue);
    }

    private void ConfigureCustomColorGoals(
        PieceSpec[] specifications,
        GarageSpec[] garageSpecifications,
        HashSet<int> excludedPieceIndexes = null)
    {
        PieceColor[] colors =
        {
            PieceColor.Red,
            PieceColor.Green,
            PieceColor.Blue,
            PieceColor.Purple,
            PieceColor.Yellow,
            PieceColor.Pink
        };

        for (int index = 0; index < colors.Length; index++)
        {
            PieceColor color = colors[index];
            int colorCount = CountColorExcluding(specifications, color, excludedPieceIndexes)
                + CountGarageColor(garageSpecifications, color);
            if (colorCount <= 0) continue;

            SetActiveTarget(
                color,
                isDemoMode ? activeMatchTarget : colorCount);
        }
    }

    private static void ValidateParkingCapacityPrincipleOrThrow(
        int boardNumber,
        int parkingCapacity,
        PieceSpec[] specifications,
        GarageSpec[] garageSpecifications,
        HashSet<int> excludedPieceIndexes = null,
        bool allowIncompleteEditedColorSets = false)
    {
        var violations = new List<string>();
        int includedColorCount = 0;
        bool hasGarage = garageSpecifications != null && garageSpecifications.Length > 0;
        for (int index = 0; index < PlayableColors.Length; index++)
        {
            PieceColor color = PlayableColors[index];
            int carCount = CountColorExcluding(specifications, color, excludedPieceIndexes)
                + CountGarageColor(garageSpecifications, color);
            if (carCount <= 0) continue;

            includedColorCount++;
            bool valid = hasGarage
                ? carCount % parkingCapacity == 0
                : carCount == parkingCapacity;
            if (!valid)
                violations.Add($"{color}={carCount}");
        }

        if (parkingCapacity > 0 && includedColorCount > 0 && violations.Count == 0)
            return;

        string details = violations.Count > 0
            ? string.Join(", ", violations)
            : includedColorCount == 0 ? "no colored cars" : $"invalid capacity {parkingCapacity}";
        if (allowIncompleteEditedColorSets)
        {
            Debug.LogWarning(
                $"Edited Board {boardNumber} has incomplete color sets ({details}). "
                + "It can still be loaded and revised in Edit Mode, but may not be solvable.");
            return;
        }
        throw new System.InvalidOperationException(
            $"Board {boardNumber} violates the parking-capacity principle: {details}; " +
            (hasGarage
                ? $"every included non-police color must contain a whole multiple of {parkingCapacity} cars. "
                : $"every included non-police color must contain exactly one {parkingCapacity}-car set. ") +
            "Police cars are excluded from this calculation.");
    }

    private bool IsLoadedLevelGeometryValid(
        PieceSpec[] specifications,
        GarageSpec[] garageSpecifications)
    {
        if (specifications == null) return false;
        var occupied = new bool[activeBoardSize, activeBoardSize];
        var storedPieceIndexes = new HashSet<int>();
        if (garageSpecifications != null)
        {
            for (int index = 0; index < garageSpecifications.Length; index++)
            {
                GarageSpec garage = garageSpecifications[index];
                if (garage == null || garage.carQueue.Length == 0
                    || garage.row < 0 || garage.row >= activeBoardSize - 1
                    || garage.col < 0 || garage.col >= activeBoardSize)
                    return false;

                int frontRow = garage.row + 1;
                if (occupied[garage.row, garage.col] || occupied[frontRow, garage.col])
                    return false;
                occupied[garage.row, garage.col] = true;
                occupied[frontRow, garage.col] = true;

                if (garage.isEditorCreated)
                {
                    if (garage.carQueue.Length != 2 || garage.storedPieceIndexes.Length != 2)
                        return false;
                    for (int storedIndex = 0; storedIndex < garage.storedPieceIndexes.Length; storedIndex++)
                    {
                        int pieceIndex = garage.storedPieceIndexes[storedIndex];
                        if (pieceIndex < 0 || pieceIndex >= specifications.Length
                            || !storedPieceIndexes.Add(pieceIndex))
                            return false;
                        PieceSpec storedPiece = specifications[pieceIndex];
                        if (storedPiece.cellLength != 1
                            || storedPiece.isRevealBox
                            || storedPiece.color != garage.carQueue[storedIndex])
                            return false;
                    }
                }
            }
        }

        for (int index = 0; index < specifications.Length; index++)
        {
            if (storedPieceIndexes.Contains(index)) continue;
            PieceSpec specification = specifications[index];
            Vector2Int step = DirectionToGridStep(specification.direction);
            for (int offset = 0; offset < specification.cellLength; offset++)
            {
                int row = specification.row - step.y * offset;
                int col = specification.col - step.x * offset;
                if (row < 0 || row >= activeBoardSize || col < 0 || col >= activeBoardSize
                    || occupied[row, col])
                    return false;
                occupied[row, col] = true;
            }

            if (specification.isRevealBox
                && (specification.color == PieceColor.Trash || specification.cellLength != 1))
                return false;
        }
        return true;
    }

    private static int CountGarageColor(GarageSpec[] garageSpecifications, PieceColor color)
    {
        int count = 0;
        if (garageSpecifications == null) return count;
        for (int garageIndex = 0; garageIndex < garageSpecifications.Length; garageIndex++)
        {
            PieceColor[] queue = garageSpecifications[garageIndex].carQueue;
            for (int queueIndex = 0; queueIndex < queue.Length; queueIndex++)
                if (queue[queueIndex] == color) count++;
        }
        return count;
    }

    private static int CountColorExcluding(
        PieceSpec[] specifications,
        PieceColor color,
        HashSet<int> excludedPieceIndexes)
    {
        if (excludedPieceIndexes == null || excludedPieceIndexes.Count == 0)
            return CountColor(specifications, color);

        int count = 0;
        for (int index = 0; index < specifications.Length; index++)
            if (!excludedPieceIndexes.Contains(index) && specifications[index].color == color)
                count++;
        return count;
    }

    private static bool ContainsColor(PieceSpec[] specifications, PieceColor color)
    {
        return CountColor(specifications, color) > 0;
    }

    private static int CountColor(PieceSpec[] specifications, PieceColor color)
    {
        int count = 0;
        for (int index = 0; index < specifications.Length; index++)
        {
            if (specifications[index].color == color)
                count++;
        }
        return count;
    }

    private void SetActiveTarget(PieceColor color, int target)
    {
        switch (color)
        {
            case PieceColor.Red: activeRedTarget = target; break;
            case PieceColor.Green: activeGreenTarget = target; break;
            case PieceColor.Blue: activeBlueTarget = target; break;
            case PieceColor.Purple: activePurpleTarget = target; break;
            case PieceColor.Yellow: activeYellowTarget = target; break;
            case PieceColor.Pink: activePinkTarget = target; break;
        }
    }

    private static ExitDirection ConvertDirection(UnityGameManager.Direction direction)
    {
        switch (direction)
        {
            case UnityGameManager.Direction.Down: return ExitDirection.Down;
            case UnityGameManager.Direction.Left: return ExitDirection.Left;
            case UnityGameManager.Direction.Right: return ExitDirection.Right;
            default: return ExitDirection.Up;
        }
    }

    private void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Prototype Camera");
        prototypeCamera = cameraObject.AddComponent<Camera>();
        if (FindFirstObjectByType<AudioListener>() == null)
            cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";
        prototypeCamera.clearFlags = CameraClearFlags.SolidColor;
        prototypeCamera.backgroundColor = new Color(0.08f, 0.13f, 0.22f);
        prototypeCamera.orthographic = true;
        prototypeCamera.allowHDR = true;
        prototypeCamera.nearClipPlane = 0.1f;
        prototypeCamera.farClipPlane = 60f;
        ApplyCameraSettings();

        UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.requiresDepthOption = CameraOverrideOption.On;
        cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset;
        if (pipeline == null) return;

        for (int index = 0; index < pipeline.rendererDataList.Length; index++)
        {
            if (pipeline.rendererDataList[index] != null && pipeline.rendererDataList[index].name == "Renderer3D")
            {
                cameraData.SetRenderer(index);
                break;
            }
        }
    }

    private void ApplyCameraSettings()
    {
        if (prototypeCamera == null) return;
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        prototypeCamera.orthographicSize = Mathf.Max(1f, currentLayout.sceneCameraOrthographicSize);
        prototypeCamera.transform.position = currentLayout.sceneCameraPosition;

        Vector3 lookDirection = currentLayout.sceneCameraLookAt - currentLayout.sceneCameraPosition;
        if (lookDirection.sqrMagnitude < 0.0001f) lookDirection = Vector3.forward;
        prototypeCamera.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private void CalculateFullScreenAsphaltSurface(
        float minimumWidth,
        float minimumDepth,
        float fallbackCenterZ,
        out Vector3 position,
        out Vector3 scale)
    {
        const float surfaceY = 0.02f;
        position = new Vector3(0f, surfaceY, fallbackCenterZ);
        scale = new Vector3(minimumWidth, 0.06f, minimumDepth);
        if (prototypeCamera == null) return;

        Plane surfacePlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        bool foundIntersection = false;

        for (int viewportY = 0; viewportY <= 1; viewportY++)
        {
            for (int viewportX = 0; viewportX <= 1; viewportX++)
            {
                Ray cornerRay = prototypeCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
                if (!surfacePlane.Raycast(cornerRay, out float distance)) continue;

                Vector3 corner = cornerRay.GetPoint(distance);
                minX = Mathf.Min(minX, corner.x);
                maxX = Mathf.Max(maxX, corner.x);
                minZ = Mathf.Min(minZ, corner.z);
                maxZ = Mathf.Max(maxZ, corner.z);
                foundIntersection = true;
            }
        }

        if (!foundIntersection) return;

        float visibleWidth = maxX - minX + FullScreenAsphaltPadding * 2f;
        float visibleDepth = maxZ - minZ + FullScreenAsphaltPadding * 2f;
        position = new Vector3((minX + maxX) * 0.5f, surfaceY, (minZ + maxZ) * 0.5f);
        scale = new Vector3(
            Mathf.Max(minimumWidth, visibleWidth),
            0.06f,
            Mathf.Max(minimumDepth, visibleDepth));
    }

    private static void CreateLighting()
    {
        // A cool-to-warm ambient gradient keeps the colorful materials readable
        // while leaving enough contrast for contact shadows and SSAO.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.58f, 0.73f);
        RenderSettings.ambientEquatorColor = new Color(0.31f, 0.38f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.17f, 0.20f, 0.27f);
        RenderSettings.ambientIntensity = 0.88f;
        RenderSettings.reflectionIntensity = 0.62f;

        GameObject lightObject = new GameObject("Sun");
        Light sun = lightObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.94f, 0.82f);
        sun.intensity = 1.20f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;
        sun.shadowBias = 0.045f;
        sun.shadowNormalBias = 0.28f;
        sun.shadowNearPlane = 0.2f;
        sun.shadowResolution = LightShadowResolution.High;
        lightObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        RenderSettings.sun = sun;
    }

    private static void CreateVisualEffects()
    {
        GameObject volumeObject = new GameObject("Prototype Visual Volume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Prototype Polished Visuals";
        volume.sharedProfile = profile;

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.92f);
        bloom.intensity.Override(0.22f);
        bloom.scatter.Override(0.55f);
        bloom.highQualityFiltering.Override(false);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.04f);
        color.contrast.Override(6f);
        color.saturation.Override(4f);

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.Neutral);
    }

    private void CreateBoard(int size)
    {
        if (boardEnvironmentRoot != null) Destroy(boardEnvironmentRoot);
        boardEnvironmentRoot = new GameObject("Board Environment");
        boardGridCells.Clear();
        CarPrototypeHudLayout currentLayout = GetSceneLayout();

        float boardWidth = size >= 4 ? 7.2f : 6.9f;
        float roadDepth = Mathf.Max(1f, currentLayout.sceneRoadDepth);
        CalculateFullScreenAsphaltSurface(
            boardWidth - 0.65f,
            Mathf.Max(0.35f, roadDepth - 0.65f),
            currentLayout.sceneRoadCenterZ,
            out Vector3 roadSurfacePosition,
            out Vector3 roadSurfaceScale);

        // This broad surface sits beneath every visible world-space element. It
        // replaces the flat camera clear color behind the tray and booster area.
        GameObject asphaltGround = CreateEnvironmentBox(
            "Continuous Asphalt Ground",
            currentLayout.sceneAsphaltGroundPosition,
            PositiveScale(currentLayout.sceneAsphaltGroundSize),
            GetAsphaltMaterial(false),
            false);
        asphaltGroundTransform = asphaltGround.transform;

        GameObject roadBoard = CreateEnvironmentBox(
            "Road Board",
            new Vector3(roadSurfacePosition.x, -0.25f, roadSurfacePosition.z),
            new Vector3(roadSurfaceScale.x + 0.65f, 0.5f, roadSurfaceScale.z + 0.65f),
            currentLayout.sceneRoadBorderColor);
        roadBoardTransform = roadBoard.transform;
        roadBoardRenderer = roadBoard.GetComponent<Renderer>();
        GameObject roadInset = CreateEnvironmentBox(
            "Road Inset",
            roadSurfacePosition,
            roadSurfaceScale,
            GetAsphaltMaterial(true),
            true);
        roadInsetTransform = roadInset.transform;

        float cellSize = size >= 5 ? 1.15f : size == 4 ? 1.37f : 1.7f;
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                Vector3 cell = GetBoardCellPosition(row, col);
                Transform parkingBay = CreatePaintedParkingBay(row, col, cell, cellSize);
                boardGridCells.Add(parkingBay);
            }
        }
    }

    private Transform CreatePaintedParkingBay(int row, int col, Vector3 cellPosition, float cellSize)
    {
        GameObject bayRoot = new GameObject($"Parking Bay {row + 1}-{col + 1}");
        bayRoot.transform.SetParent(boardEnvironmentRoot.transform, true);
        bayRoot.transform.position = cellPosition + new Vector3(0f, -0.18f, 0f);

        // Keep the logical cell transform for positioning, but leave the main
        // puzzle grid as uninterrupted asphalt with no U-shaped bay markings.
        return bayRoot.transform;
    }

    private static Transform CreateParkingPaintStripe(string stripeName, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        // Painted lines do not need physics. Reusing the built-in cube mesh
        // avoids creating and deleting 75 colliders on each 5x5 board load.
        GameObject stripe = new GameObject(stripeName, typeof(MeshFilter), typeof(MeshRenderer));
        stripe.transform.SetParent(parent, false);
        stripe.transform.localPosition = localPosition;
        stripe.transform.localScale = scale;
        stripe.GetComponent<MeshFilter>().sharedMesh = GetSharedCubeMesh();
        stripe.GetComponent<MeshRenderer>().sharedMaterial = material;
        return stripe.transform;
    }

    private static Material GetParkingLineMaterial()
    {
        if (parkingLineMaterial != null) return parkingLineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        Color paintedWhite = Color.white;
        parkingLineMaterial = new Material(shader)
        {
            name = "Runtime Parking Line Paint",
            hideFlags = HideFlags.HideAndDontSave,
            color = paintedWhite
        };

        if (parkingLineMaterial.HasProperty("_BaseColor"))
            parkingLineMaterial.SetColor("_BaseColor", paintedWhite);
        if (parkingLineMaterial.HasProperty("_Smoothness"))
            parkingLineMaterial.SetFloat("_Smoothness", 0.06f);
        if (parkingLineMaterial.HasProperty("_Metallic"))
            parkingLineMaterial.SetFloat("_Metallic", 0f);

        return parkingLineMaterial;
    }

    private GameObject CreateEnvironmentBox(string objectName, Vector3 position, Vector3 scale, Color color)
    {
        GameObject box = CreateBox(objectName, position, scale, color);
        box.transform.SetParent(boardEnvironmentRoot.transform, true);
        return box;
    }

    private GameObject CreateEnvironmentBox(string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        box.transform.SetParent(boardEnvironmentRoot.transform, true);

        if (!keepCollider)
        {
            Collider surfaceCollider = box.GetComponent<Collider>();
            if (surfaceCollider != null) Destroy(surfaceCollider);
        }

        return box;
    }

    private Material GetAsphaltMaterial(bool brighterPlayfield)
    {
        Material cachedMaterial = brighterPlayfield ? playfieldAsphaltMaterial : backgroundAsphaltMaterial;
        Color asphaltTint = brighterPlayfield
            ? GetSceneLayout().scenePlayfieldAsphaltColor
            : GetSceneLayout().sceneBackgroundAsphaltColor;
        if (cachedMaterial != null)
        {
            ApplyAsphaltMaterialTint(cachedMaterial, asphaltTint);
            return cachedMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader)
        {
            name = brighterPlayfield ? "Runtime Playfield Asphalt" : "Runtime Background Asphalt",
            hideFlags = HideFlags.HideAndDontSave
        };

        material.color = asphaltTint;

        Texture2D texture = GetAsphaltTexture();
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", brighterPlayfield ? new Vector2(4f, 5f) : new Vector2(4f, 8f));
            material.SetColor("_BaseColor", asphaltTint);
        }
        else if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", brighterPlayfield ? new Vector2(4f, 5f) : new Vector2(4f, 8f));
        }

        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

        if (brighterPlayfield) playfieldAsphaltMaterial = material;
        else backgroundAsphaltMaterial = material;
        return material;
    }

    private static void ApplyAsphaltMaterialTint(Material material, Color color)
    {
        if (material == null) return;
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private CarPrototypeHudLayout GetSceneLayout()
    {
        if (sceneLayout == null) sceneLayout = CarPrototypeHudLayout.LoadOrDefault();
        return sceneLayout;
    }

    private static Vector3 PositiveScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            Mathf.Max(0.01f, Mathf.Abs(scale.z)));
    }

    private static Texture2D GetAsphaltTexture()
    {
        if (asphaltTexture != null) return asphaltTexture;

        asphaltTexture = Resources.Load<Texture2D>(PremiumAsphaltResourcePath);
        if (asphaltTexture != null)
        {
            asphaltTexture.wrapMode = TextureWrapMode.Repeat;
            asphaltTexture.filterMode = FilterMode.Trilinear;
            asphaltTexture.anisoLevel = 2;
            return asphaltTexture;
        }

        const int textureSize = 64;
        asphaltTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true)
        {
            name = "Runtime Asphalt Grain",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                uint hash = (uint)(x * 374761393 + y * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                float fineGrain = ((hash & 1023u) / 1023f - 0.5f) * 0.18f;
                float broadGrain = Mathf.Sin((x + y * 0.71f) * 0.42f) * 0.025f;
                float value = Mathf.Clamp01(0.86f + fineGrain + broadGrain);
                pixels[y * textureSize + x] = new Color(value, value * 0.99f, value * 0.97f, 1f);
            }
        }

        asphaltTexture.SetPixels(pixels);
        asphaltTexture.Apply(true, false);
        return asphaltTexture;
    }

    private void CreateMatchTray()
    {
        GameObject trayRoot = new GameObject("Approved Parking Sidewalk Artwork");
        matchTrayRootTransform = trayRoot.transform;

        GameObject artwork = GameObject.CreatePrimitive(PrimitiveType.Quad);
        artwork.name = "Imported Approved Parking Sidewalk";
        artwork.transform.SetParent(trayRoot.transform, false);
        // Unity's Quad renders its local -Z face. Point that face upward while
        // mapping the artwork's top edge toward the board (+world Z).
        artwork.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        matchTraySidewalkTransform = artwork.transform;

        Collider artworkCollider = artwork.GetComponent<Collider>();
        if (artworkCollider != null)
        {
            if (Application.isPlaying) Destroy(artworkCollider);
            else DestroyImmediate(artworkCollider);
        }

        matchTraySidewalkRenderer = artwork.GetComponent<Renderer>();
        matchTraySidewalkRenderer.shadowCastingMode = ShadowCastingMode.Off;
        matchTraySidewalkRenderer.receiveShadows = false;
        matchTraySidewalkRenderer.lightProbeUsage = LightProbeUsage.Off;
        matchTraySidewalkRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        matchTraySidewalkRenderer.sortingOrder = -20;

        GameObject dividerRoot = new GameObject("Original White Parking Divider Lines");
        matchTrayDividerRootTransform = dividerRoot.transform;
        Material roadPaint = GetParkingLineMaterial();
        for (int index = 0; index < matchTrayDividerLines.Length; index++)
        {
            matchTrayDividerLines[index] = CreateParkingPaintStripe(
                $"Normal Bay Divider {index + 1}",
                matchTrayDividerRootTransform,
                Vector3.zero,
                new Vector3(0.08f, 0.018f, 0.08f),
                roadPaint);
        }

        UpdateMatchTrayRoadMarkings();
    }

    private void CreateParkingSlot()
    {
        GameObject parkingRoot = new GameObject("Side Parking Bay Road Markings");
        sideParkingRootTransform = parkingRoot.transform;

        Material roadPaint = GetParkingLineMaterial();
        Vector3 placeholderScale = new Vector3(0.08f, 0.018f, 0.08f);
        sideParkingTopLine = CreateParkingPaintStripe("Side Bay Top Line", parkingRoot.transform, Vector3.zero, placeholderScale, roadPaint);
        sideParkingBottomLine = CreateParkingPaintStripe("Side Bay Bottom Line", parkingRoot.transform, Vector3.zero, placeholderScale, roadPaint);
        sideParkingLeftLine = CreateParkingPaintStripe("Side Bay Left Line", parkingRoot.transform, Vector3.zero, placeholderScale, roadPaint);
        sideParkingRightLine = CreateParkingPaintStripe("Side Bay Right Line", parkingRoot.transform, Vector3.zero, placeholderScale, roadPaint);

        GameObject roadGuideRoot = new GameObject("Premium Side Road Guide Markings");
        sideRoadGuideRootTransform = roadGuideRoot.transform;
        leftSideRoadGuideFilter = CreateRoadGuideRenderer("Left Side Road Guide", sideRoadGuideRootTransform, roadPaint);
        rightSideRoadGuideFilter = CreateRoadGuideRenderer("Right Side Road Guide", sideRoadGuideRootTransform, roadPaint);
        UpdateParkingHighlight();
    }

    private static MeshFilter CreateRoadGuideRenderer(string objectName, Transform parent, Material material)
    {
        GameObject guide = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        guide.transform.SetParent(parent, false);
        MeshRenderer renderer = guide.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return guide.GetComponent<MeshFilter>();
    }

    private void UpdateTrayHighlights()
    {
        UpdateMatchTrayRoadMarkings();
    }

    private void UpdateMatchTrayRoadMarkings()
    {
        if (matchTrayRootTransform == null || matchTraySidewalkTransform == null || matchTraySidewalkRenderer == null
            || matchTrayDividerRootTransform == null) return;

        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        int visibleCapacity = Mathf.Clamp(trayCapacity, 2, MaximumTraySlots);
        float slotSpacing = GetMatchTraySlotSpacing(visibleCapacity);
        Texture2D texture = Resources.Load<Texture2D>($"{ApprovedParkingSidewalkResourcePrefix}{visibleCapacity}");
        Material material = GetApprovedParkingSidewalkMaterial(visibleCapacity, texture);
        if (texture == null || material == null)
        {
            matchTraySidewalkRenderer.enabled = false;
            matchTrayDividerRootTransform.gameObject.SetActive(false);
            return;
        }

        float pixelsPerWorldUnit = ApprovedParkingBayPitchPixels
            / Mathf.Max(0.01f, slotSpacing);
        float artworkWidth = texture.width / pixelsPerWorldUnit;
        float artworkDepth = texture.height / pixelsPerWorldUnit;
        float bayCenterOffset = (texture.height * 0.5f - ApprovedParkingBayCenterFromTopPixels) / pixelsPerWorldUnit;

        // Position the imported artwork by its baked parking-bay center, not its
        // texture center. Its deeper bottom sidewalk can then extend downward
        // without changing the live car positions or route calculations.
        matchTrayRootTransform.position = new Vector3(
            currentLayout.sceneMatchTrayPosition.x,
            0.055f,
            currentLayout.sceneMatchTrayPosition.y - bayCenterOffset);
        matchTraySidewalkTransform.localPosition = Vector3.zero;
        matchTraySidewalkTransform.localScale = new Vector3(artworkWidth, artworkDepth, 1f);
        matchTraySidewalkRenderer.sharedMaterial = material;
        matchTraySidewalkRenderer.enabled = true;

        matchTrayDividerRootTransform.gameObject.SetActive(true);
        matchTrayDividerRootTransform.position = new Vector3(
            currentLayout.sceneMatchTrayPosition.x,
            0f,
            currentLayout.sceneMatchTrayPosition.y);
        float dividerDepth = (ApprovedParkingSidewalkInnerEdgePixels - ApprovedParkingDividerTopPixels) / pixelsPerWorldUnit;
        float dividerCenterPixels = (ApprovedParkingDividerTopPixels + ApprovedParkingSidewalkInnerEdgePixels) * 0.5f;
        float dividerCenterZ = -(dividerCenterPixels - ApprovedParkingBayCenterFromTopPixels) / pixelsPerWorldUnit;
        float lineWidth = Mathf.Clamp(
            currentLayout.sceneParkingLineWidth * GetParkingGeometryScale(),
            0.025f,
            Mathf.Max(0.08f, slotSpacing * 0.18f));
        float dividerCenterIndex = (visibleCapacity - 2) * 0.5f;
        for (int index = 0; index < matchTrayDividerLines.Length; index++)
        {
            bool dividerVisible = index < visibleCapacity - 1;
            matchTrayDividerLines[index].gameObject.SetActive(dividerVisible);
            if (!dividerVisible) continue;
            float x = (index - dividerCenterIndex) * slotSpacing;
            SetTrayRoadLine(
                matchTrayDividerLines[index],
                new Vector3(x, 0.071f, dividerCenterZ),
                new Vector3(lineWidth, 0.018f, dividerDepth + 0.025f));
        }
    }

    private static Material GetApprovedParkingSidewalkMaterial(int capacity, Texture2D texture)
    {
        if (texture == null) return null;
        if (approvedParkingSidewalkMaterials.TryGetValue(capacity, out Material cachedMaterial)
            && cachedMaterial != null)
            return cachedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        Material material = new Material(shader)
        {
            name = $"Approved Parking Sidewalk {capacity}-Space Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white,
            mainTexture = texture,
            renderQueue = (int)RenderQueue.Transparent
        };
        material.enableInstancing = true;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        approvedParkingSidewalkMaterials[capacity] = material;
        return material;
    }

    private static void SetTrayRoadLine(Transform line, Vector3 localPosition, Vector3 localScale)
    {
        if (line == null) return;
        line.localPosition = localPosition;
        line.localScale = localScale;
    }

    private void UpdateParkingHighlight()
    {
        UpdateSideParkingRoadMarkings();
    }

    private void UpdateSideParkingRoadMarkings()
    {
        if (sideParkingRootTransform == null || sideParkingTopLine == null) return;

        bool visible = IsSideParkingAvailable();
        if (sideParkingRootTransform.gameObject.activeSelf != visible)
            sideParkingRootTransform.gameObject.SetActive(visible);
        if (sideRoadGuideRootTransform != null
            && sideRoadGuideRootTransform.gameObject.activeSelf != visible)
            sideRoadGuideRootTransform.gameObject.SetActive(visible);
        if (!visible) return;

        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        float bayWidth = Mathf.Max(0.55f, Mathf.Abs(currentLayout.sceneSideParkingSize.x));
        float bayDepth = Mathf.Max(0.65f, Mathf.Abs(currentLayout.sceneSideParkingSize.y));
        float lineWidth = Mathf.Clamp(currentLayout.sceneParkingLineWidth, 0.025f, Mathf.Min(bayWidth, bayDepth) * 0.22f);
        const float lineHeight = 0.018f;
        const float paintY = 0.068f;

        sideParkingRootTransform.position = new Vector3(currentLayout.sceneSideParkingPosition.x, 0f, currentLayout.sceneSideParkingPosition.y);
        float sideX = bayWidth * 0.5f - lineWidth * 0.5f;
        float edgeZ = bayDepth * 0.5f - lineWidth * 0.5f;
        SetTrayRoadLine(sideParkingTopLine, new Vector3(0f, paintY, edgeZ), new Vector3(bayWidth, lineHeight, lineWidth));
        SetTrayRoadLine(sideParkingBottomLine, new Vector3(0f, paintY, -edgeZ), new Vector3(bayWidth, lineHeight, lineWidth));
        SetTrayRoadLine(sideParkingLeftLine, new Vector3(-sideX, paintY, 0f), new Vector3(lineWidth, lineHeight, bayDepth));
        SetTrayRoadLine(sideParkingRightLine, new Vector3(sideX, paintY, 0f), new Vector3(lineWidth, lineHeight, bayDepth));
        UpdateSideRoadGuideMarkings(currentLayout, lineWidth);

        // This is a usable side-parking slot, not a no-parking hatch.
        // Keep any legacy runtime hatch objects disabled for saved scenes.
        for (int index = 0; index < sideParkingHatchLines.Count; index++)
        {
            Renderer hatchRenderer = sideParkingHatchLines[index].GetComponent<Renderer>();
            if (hatchRenderer != null) hatchRenderer.enabled = false;
        }
    }

    private struct SideRoadGuideGeometry
    {
        internal float centerX;
        internal float guideOffset;
        internal float guideTopZ;
        internal float sidewalkTopZ;
        internal float curveStartZ;
        internal float curveRadius;
        internal float geometryScale;

        internal float LeftGuideX => centerX - guideOffset;
        internal float RightGuideX => centerX + guideOffset;
    }

    private bool TryGetSideRoadGuideGeometry(
        CarPrototypeHudLayout currentLayout,
        out SideRoadGuideGeometry geometry)
    {
        geometry = default;
        int visibleCapacity = Mathf.Clamp(trayCapacity, 2, MaximumTraySlots);
        Texture2D sidewalkTexture = Resources.Load<Texture2D>(
            $"{ApprovedParkingSidewalkResourcePrefix}{visibleCapacity}");
        if (sidewalkTexture == null) return false;

        float pixelsPerWorldUnit = ApprovedParkingBayPitchPixels
            / Mathf.Max(0.01f, GetMatchTraySlotSpacing(visibleCapacity));
        float artworkWidth = sidewalkTexture.width / pixelsPerWorldUnit;
        float artworkDepth = sidewalkTexture.height / pixelsPerWorldUnit;
        float bayCenterOffset = (sidewalkTexture.height * 0.5f - ApprovedParkingBayCenterFromTopPixels)
            / pixelsPerWorldUnit;
        float artworkCenterZ = currentLayout.sceneMatchTrayPosition.y - bayCenterOffset;

        float geometryScale = GetParkingGeometryScale();
        float sidewalkClearance = 0.72f * geometryScale;
        float curveRadius = 0.66f * geometryScale;
        float guideOffset = artworkWidth * 0.5f + sidewalkClearance;
        if (prototypeCamera != null)
        {
            const float viewportMargin = 0.28f;
            float visibleHalfWidth = prototypeCamera.orthographicSize * prototypeCamera.aspect;
            guideOffset = Mathf.Min(
                guideOffset,
                Mathf.Max(curveRadius + 0.35f, visibleHalfWidth - viewportMargin));
        }
        geometry = new SideRoadGuideGeometry
        {
            centerX = currentLayout.sceneMatchTrayPosition.x,
            guideOffset = guideOffset,
            guideTopZ = artworkCenterZ + artworkDepth * 0.5f - 0.55f * geometryScale,
            sidewalkTopZ = artworkCenterZ + artworkDepth * 0.5f,
            // A quarter-circle finishes one radius below its starting Z, so
            // this makes the painted endpoint equal the parking-bay center Z.
            curveStartZ = currentLayout.sceneSideParkingPosition.y + curveRadius,
            curveRadius = curveRadius,
            geometryScale = geometryScale
        };
        return true;
    }

    private void UpdateSideRoadGuideMarkings(
        CarPrototypeHudLayout currentLayout,
        float lineWidth)
    {
        if (sideRoadGuideRootTransform == null
            || leftSideRoadGuideFilter == null
            || rightSideRoadGuideFilter == null)
            return;

        if (!TryGetSideRoadGuideGeometry(currentLayout, out SideRoadGuideGeometry geometry))
        {
            leftSideRoadGuideFilter.gameObject.SetActive(false);
            rightSideRoadGuideFilter.gameObject.SetActive(false);
            return;
        }

        leftSideRoadGuideFilter.gameObject.SetActive(true);
        rightSideRoadGuideFilter.gameObject.SetActive(true);
        float guideWidth = Mathf.Clamp(lineWidth * geometry.geometryScale * 0.92f, 0.045f, 0.11f);

        List<Vector3> leftPath = BuildSideRoadGuidePath(
            geometry.LeftGuideX,
            geometry.guideTopZ,
            geometry.curveStartZ,
            geometry.curveRadius,
            true);
        List<Vector3> rightPath = BuildSideRoadGuidePath(
            geometry.RightGuideX,
            geometry.guideTopZ,
            geometry.curveStartZ,
            geometry.curveRadius,
            false);
        SetRoadGuideMesh(leftSideRoadGuideFilter, leftPath, guideWidth);
        SetRoadGuideMesh(rightSideRoadGuideFilter, rightPath, guideWidth);
    }

    private static List<Vector3> BuildSideRoadGuidePath(
        float guideX,
        float guideTopZ,
        float curveStartZ,
        float curveRadius,
        bool isLeftSide)
    {
        const float paintY = 0.073f;
        const int curveSegments = 10;
        var points = new List<Vector3>(curveSegments + 2)
        {
            new Vector3(guideX, paintY, guideTopZ),
            new Vector3(guideX, paintY, curveStartZ)
        };

        float centerX = guideX + (isLeftSide ? curveRadius : -curveRadius);
        float startAngle = isLeftSide ? Mathf.PI : 0f;
        float angleDelta = isLeftSide ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f;
        for (int segment = 1; segment <= curveSegments; segment++)
        {
            float angle = startAngle + angleDelta * (segment / (float)curveSegments);
            points.Add(new Vector3(
                centerX + Mathf.Cos(angle) * curveRadius,
                paintY,
                curveStartZ + Mathf.Sin(angle) * curveRadius));
        }

        return points;
    }

    private static void SetRoadGuideMesh(MeshFilter filter, List<Vector3> points, float width)
    {
        if (filter == null || points == null || points.Count < 2) return;

        Mesh mesh = filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = $"{filter.gameObject.name} Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        int pointCount = points.Count;
        var vertices = new Vector3[pointCount * 2];
        var normals = new Vector3[pointCount * 2];
        var ultraviolet = new Vector2[pointCount * 2];
        var triangles = new int[(pointCount - 1) * 6];
        float halfWidth = width * 0.5f;
        float accumulatedLength = 0f;

        for (int index = 0; index < pointCount; index++)
        {
            Vector3 tangent;
            if (index == 0) tangent = points[1] - points[0];
            else if (index == pointCount - 1) tangent = points[index] - points[index - 1];
            else tangent = points[index + 1] - points[index - 1];
            tangent.y = 0f;
            tangent.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;

            if (index > 0) accumulatedLength += Vector3.Distance(points[index - 1], points[index]);
            vertices[index * 2] = points[index] + side * halfWidth;
            vertices[index * 2 + 1] = points[index] - side * halfWidth;
            normals[index * 2] = Vector3.up;
            normals[index * 2 + 1] = Vector3.up;
            ultraviolet[index * 2] = new Vector2(0f, accumulatedLength);
            ultraviolet[index * 2 + 1] = new Vector2(1f, accumulatedLength);
        }

        for (int index = 0; index < pointCount - 1; index++)
        {
            int vertex = index * 2;
            int triangle = index * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 2;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = ultraviolet;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private Transform GetOrCreateSideParkingHatchLine(int index)
    {
        while (sideParkingHatchLines.Count <= index)
        {
            int lineNumber = sideParkingHatchLines.Count + 1;
            Transform hatch = CreateParkingPaintStripe($"Side Bay Diagonal Hatch {lineNumber}", sideParkingRootTransform,
                Vector3.zero, new Vector3(0.05f, 0.018f, 0.1f), GetParkingLineMaterial());
            sideParkingHatchLines.Add(hatch);
        }

        return sideParkingHatchLines[index];
    }

    private void RepositionTrayCars()
    {
        for (int index = 0; index < trayPieces.Count; index++)
            trayPieces[index].SetTrayPose(GetTraySlotPosition(index));
    }

    private int InsertTrayPieceBesideMatchingColor(CarPuzzlePiece piece)
    {
        int lastMatchingIndex = -1;
        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (trayPieces[index].PieceColor == piece.PieceColor)
                lastMatchingIndex = index;
        }

        int insertionIndex = lastMatchingIndex >= 0
            ? lastMatchingIndex + 1
            : trayPieces.Count;

        // Rapid commands may reserve several destinations before the first car
        // arrives. Never insert ahead of an existing in-motion reservation,
        // because that would move its authoritative slot while it is already
        // following a validated route. Append after the final protected slot
        // when the preferred color-group insertion would shift one.
        int lastProtectedIndex = -1;
        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (carsCurrentlyMoving.Contains(trayPieces[index]))
                lastProtectedIndex = index;
        }
        if (insertionIndex <= lastProtectedIndex)
            insertionIndex = lastProtectedIndex + 1;

        trayPieces.Insert(insertionIndex, piece);
        return insertionIndex;
    }

    private IEnumerator AnimateTrayLayout(
        float duration,
        bool skipCarsDrivingFromBoard = false,
        CarPuzzlePiece skipPiece = null)
    {
        int animatedCars = 0;
        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (carsCurrentlyMoving.Contains(trayPieces[index]))
                continue;
            if (skipCarsDrivingFromBoard && boardCarsCurrentlyDriving.Contains(trayPieces[index]))
                continue;
            if (trayPieces[index] == skipPiece)
                continue;

            StartCoroutine(trayPieces[index].DriveToTraySlot(GetTraySlotPosition(index), duration));
            animatedCars++;
        }

        if (animatedCars > 0)
            yield return new WaitForSeconds(duration);
    }

    private void TryParkTrayPiece(CarPuzzlePiece selectedPiece)
    {
        if (selectedPiece == null || carsCurrentlyMoving.Contains(selectedPiece)) return;

        int trayIndex = trayPieces.IndexOf(selectedPiece);
        if (trayIndex < 0) return;

        if (!CanEnterSideParking())
        {
            selectedPiece.Reject();
            RefreshHud();
            return;
        }

        // The single side bay may already be reserved by a vehicle that is on
        // its way there. Keep that vehicle protected without reintroducing a
        // board-wide input lock.
        if (parkedPiece != null && carsCurrentlyMoving.Contains(parkedPiece))
            return;

        isAnimating = true;
        RegisterParkingEntry();
        if (parkedPiece == null)
        {
            trayPieces.RemoveAt(trayIndex);
            parkedPiece = selectedPiece;
            carsCurrentlyMoving.Add(selectedPiece);
            UpdateTrayHighlights();
            UpdateParkingHighlight();
            RefreshHud();
            StartCoroutine(DriveTrayPieceToParking(selectedPiece));
            return;
        }

        CarPuzzlePiece previouslyParked = parkedPiece;
        trayPieces[trayIndex] = previouslyParked;
        parkedPiece = selectedPiece;
        carsCurrentlyMoving.Add(selectedPiece);
        carsCurrentlyMoving.Add(previouslyParked);
        UpdateTrayHighlights();
        UpdateParkingHighlight();
        RefreshHud();
        StartCoroutine(AnimateParkingSwap(selectedPiece, previouslyParked, trayIndex));
    }

    private void TryReturnParkedPiece()
    {
        if (parkedPiece == null || carsCurrentlyMoving.Contains(parkedPiece)) return;

        isAnimating = true;
        if (trayPieces.Count < trayCapacity)
        {
            CarPuzzlePiece returningPiece = parkedPiece;
            int returningIndex = InsertTrayPieceBesideMatchingColor(returningPiece);
            parkedPiece = null;
            carsCurrentlyMoving.Add(returningPiece);
            UpdateTrayHighlights();
            UpdateParkingHighlight();
            RefreshHud();
            StartCoroutine(AnimateTrayLayout(0.11f, false, returningPiece));
            StartCoroutine(DriveParkingPieceToTray(returningPiece, returningIndex));
            return;
        }

        int swapIndex = FindTraySwapIndexForParkingMatch();
        if (swapIndex < 0)
        {
            parkedPiece.Reject();
            RegisterWrongMove();
            isAnimating = boardCarsInTransit > 0
                || carsCurrentlyMoving.Count > 0
                || isClearingTrayMatch;
            return;
        }

        if (!CanEnterSideParking())
        {
            parkedPiece.Reject();
            isAnimating = boardCarsInTransit > 0
                || carsCurrentlyMoving.Count > 0
                || isClearingTrayMatch;
            RefreshHud();
            return;
        }

        CarPuzzlePiece trayPiece = trayPieces[swapIndex];
        CarPuzzlePiece returningParkedPiece = parkedPiece;
        trayPieces[swapIndex] = returningParkedPiece;
        parkedPiece = trayPiece;
        carsCurrentlyMoving.Add(returningParkedPiece);
        carsCurrentlyMoving.Add(trayPiece);
        RegisterParkingEntry();
        UpdateTrayHighlights();
        UpdateParkingHighlight();
        RefreshHud();
        StartCoroutine(AnimateReturnParkingSwap(returningParkedPiece, trayPiece, swapIndex));
    }

    private IEnumerator DriveTrayPieceToParking(CarPuzzlePiece piece)
    {
        yield return StartCoroutine(AcquireSideRoadMotionGroup());
        try
        {
            yield return StartCoroutine(DriveTrayPieceAlongSideRoadToParking(piece, true));
        }
        finally
        {
            sideRoadMotionGroupsInProgress = Mathf.Max(0, sideRoadMotionGroupsInProgress - 1);
        }
        carsCurrentlyMoving.Remove(piece);
        isAnimating = isClearingTrayMatch
            || boardCarsInTransit > 0
            || carsCurrentlyMoving.Count > 0
            || boxRevealAnimationsInProgress > 0
            || garageTransitionsInProgress > 0;
        RefreshHud();
    }

    private IEnumerator DriveTrayPieceAlongSideRoadToParking(
        CarPuzzlePiece piece,
        bool closeTrayGap)
    {
        if (!TryGetSideRoadGuideGeometry(GetSceneLayout(), out SideRoadGuideGeometry road))
            yield break;

        Vector3 parkingTarget = GetParkingSlotPosition();
        float routeHeight = parkingTarget.y;
        float safeRoadTopZ = GetSideRoadSafeTopZ(piece, road);
        Vector3 trayDeparture = new Vector3(piece.transform.position.x, routeHeight, safeRoadTopZ);
        Vector3 leftRoadTop = new Vector3(road.LeftGuideX, routeHeight, safeRoadTopZ);
        Vector3 leftRoadCorner = new Vector3(road.LeftGuideX, routeHeight, parkingTarget.z);
        // Matching-tray cars face the closed lower curb. Back them through the
        // open top first, preserving their orientation until the full vehicle
        // footprint is clear of the sidewalk.
        yield return StartCoroutine(DriveReverseRouteSegment(piece, trayDeparture, ParkingDriveSpeed));

        // An empty-bay parking action closes the tray gap. During an exchange,
        // the returning car owns that exact bay and animates into it directly.
        if (closeTrayGap) StartCoroutine(AnimateTrayLayout(0.24f));
        yield return StartCoroutine(DriveRoundedRouteToFinalApproach(
            piece,
            new List<Vector3> { leftRoadTop, leftRoadCorner, parkingTarget },
            ParkingDriveSpeed,
            road.curveRadius));
        yield return StartCoroutine(FinishParkingEntry(piece, parkingTarget));
    }

    private IEnumerator AnimateParkingSwap(CarPuzzlePiece trayPiece, CarPuzzlePiece previouslyParked, int trayIndex)
    {
        yield return StartCoroutine(AcquireSideRoadMotionGroup());
        try
        {
            yield return StartCoroutine(AnimateSimultaneousParkingExchange(
                trayPiece,
                previouslyParked,
                trayIndex));
        }
        finally
        {
            sideRoadMotionGroupsInProgress = Mathf.Max(0, sideRoadMotionGroupsInProgress - 1);
        }
        carsCurrentlyMoving.Remove(trayPiece);
        carsCurrentlyMoving.Remove(previouslyParked);
        CompleteConcurrentParkingMotion();
    }

    private IEnumerator DriveParkingPieceToTray(CarPuzzlePiece piece, int trayIndex)
    {
        yield return StartCoroutine(AcquireSideRoadMotionGroup());
        try
        {
            yield return StartCoroutine(DriveParkingPieceToOpenTraySlot(piece, trayIndex));
        }
        finally
        {
            sideRoadMotionGroupsInProgress = Mathf.Max(0, sideRoadMotionGroupsInProgress - 1);
        }
        carsCurrentlyMoving.Remove(piece);
        CompleteConcurrentParkingMotion();
    }

    private IEnumerator DriveParkingPieceToOpenTraySlot(CarPuzzlePiece piece, int trayIndex)
    {
        if (!TryGetSideRoadGuideGeometry(GetSceneLayout(), out SideRoadGuideGeometry road))
            yield break;

        Vector3 trayTarget = GetTraySlotPosition(trayIndex);
        Vector3 parkingTarget = GetParkingSlotPosition();
        float safeRoadTopZ = GetSideRoadSafeTopZ(piece, road);
        Vector3 rightRoadCorner = new Vector3(road.RightGuideX, trayTarget.y, parkingTarget.z);
        Vector3 rightRoadTop = new Vector3(road.RightGuideX, trayTarget.y, safeRoadTopZ);
        Vector3 aboveTrayTarget = new Vector3(trayTarget.x, trayTarget.y, safeRoadTopZ);
        var route = new List<Vector3>();

        bool startsInsideParkingBay = Mathf.Abs(piece.transform.position.x - parkingTarget.x) < 0.45f
            && Mathf.Abs(piece.transform.position.z - parkingTarget.z) < 0.40f;
        if (startsInsideParkingBay) route.Add(rightRoadCorner);
        route.Add(rightRoadTop);
        route.Add(aboveTrayTarget);
        route.Add(trayTarget);

        yield return StartCoroutine(DriveRoundedRouteToFinalApproach(
            piece,
            route,
            ParkingDriveSpeed,
            road.curveRadius));
        float finalDuration = Mathf.Clamp(
            Vector3.Distance(piece.transform.position, trayTarget) / CarDriveSpeed,
            0.085f,
            0.17f);
        yield return StartCoroutine(piece.DriveToTraySlot(trayTarget, finalDuration));
    }

    private IEnumerator DriveParkingPieceToTraySlot(CarPuzzlePiece piece, int trayIndex)
    {
        yield return StartCoroutine(DriveParkingPieceToOpenTraySlot(piece, trayIndex));
    }

    private IEnumerator AnimateReturnParkingSwap(CarPuzzlePiece returningPiece, CarPuzzlePiece outgoingTrayPiece, int trayIndex)
    {
        yield return StartCoroutine(AcquireSideRoadMotionGroup());
        try
        {
            yield return StartCoroutine(AnimateSimultaneousParkingExchange(
                outgoingTrayPiece,
                returningPiece,
                trayIndex));
        }
        finally
        {
            sideRoadMotionGroupsInProgress = Mathf.Max(0, sideRoadMotionGroupsInProgress - 1);
        }
        carsCurrentlyMoving.Remove(returningPiece);
        carsCurrentlyMoving.Remove(outgoingTrayPiece);
        CompleteConcurrentParkingMotion();
    }

    private IEnumerator AnimateSimultaneousParkingExchange(
        CarPuzzlePiece outgoingTrayPiece,
        CarPuzzlePiece returningParkedPiece,
        int trayIndex)
    {
        int completedMotions = 0;
        StartCoroutine(TrackParkingExchangeMotion(
            DriveTrayPieceAlongSideRoadToParking(outgoingTrayPiece, false),
            () => completedMotions++));
        StartCoroutine(TrackParkingExchangeMotion(
            DriveParkingPieceToOpenTraySlot(returningParkedPiece, trayIndex),
            () => completedMotions++));

        while (completedMotions < 2) yield return null;
    }

    private void CompleteConcurrentParkingMotion()
    {
        // Reserved board arrivals already occupy their logical tray slots. Let
        // the last one to physically arrive perform the authoritative match
        // check so a merge can never start on a car that is still on the road.
        if (boardCarsInTransit == 0 && !isClearingTrayMatch)
            CheckTrayMatches();
        else
            isAnimating = true;

        RefreshHud();
    }

    private IEnumerator TrackParkingExchangeMotion(IEnumerator motion, System.Action onComplete)
    {
        yield return StartCoroutine(motion);
        onComplete?.Invoke();
    }

    private IEnumerator AcquireSideRoadMotionGroup()
    {
        // The tap and logical reservation have already been accepted. Wait only
        // at the physical shared-corridor boundary if a board arrival currently
        // owns it; this is route arbitration, not an input lock.
        while (activeTrayTrafficClaims.Count > 0 || sideRoadMotionGroupsInProgress > 0)
            yield return null;

        sideRoadMotionGroupsInProgress++;
    }

    private float GetSideRoadSafeTopZ(CarPuzzlePiece piece, SideRoadGuideGeometry road)
    {
        float fullVehicleTurnRadius = GetOutsideRouteTurnRadius(piece);
        return Mathf.Max(
            GetTrayApproachZ(),
            road.sidewalkTopZ + fullVehicleTurnRadius + 0.14f);
    }

    private IEnumerator FinishParkingEntry(CarPuzzlePiece piece, Vector3 parkingTarget)
    {
        float finalDuration = Mathf.Clamp(
            Vector3.Distance(piece.transform.position, parkingTarget) / CarDriveSpeed,
            0.075f,
            0.15f);
        yield return StartCoroutine(piece.DriveToParkingSlot(parkingTarget, finalDuration));
    }

    private int FindTraySwapIndexForParkingMatch()
    {
        if (parkedPiece == null) return -1;

        if (RequiresAdjacency(parkedPiece.PieceColor))
        {
            PieceColor adjacentColor = parkedPiece.PieceColor;
            int target = GetMatchTarget(adjacentColor);
            if (target <= 0) return -1;

            for (int index = 0; index < trayPieces.Count; index++)
            {
                if (carsCurrentlyMoving.Contains(trayPieces[index])) continue;
                if (trayPieces[index].PieceColor == adjacentColor) continue;

                int adjacentCount = 1;
                for (int left = index - 1; left >= 0 && trayPieces[left].PieceColor == adjacentColor; left--) adjacentCount++;
                for (int right = index + 1; right < trayPieces.Count && trayPieces[right].PieceColor == adjacentColor; right++) adjacentCount++;
                if (adjacentCount >= target) return index;
            }

            return -1;
        }

        int matchingCars = 0;
        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (trayPieces[index].PieceColor == parkedPiece.PieceColor)
                matchingCars++;
        }

        if (matchingCars < GetMatchTarget(parkedPiece.PieceColor) - 1) return -1;

        for (int index = 0; index < trayPieces.Count; index++)
        {
            if (!carsCurrentlyMoving.Contains(trayPieces[index])
                && trayPieces[index].PieceColor != parkedPiece.PieceColor)
                return index;
        }

        return -1;
    }

    public void UseExtraSlot()
    {
        if (!CanUseExtraSlot) return;

        trayCapacity++;
        extraSlotUsed = true;
        lastMovedPiece = null;
        RepositionTrayCars();
        UpdateTrayHighlights();
        RefreshHud();
    }

    public void RestartCurrentBoard()
    {
        LoadLevel(levelIndex);
    }

    public void UseUndo()
    {
        if (!CanUseUndo) return;

        if (lastMovedPiece.SourceGarageId >= 0)
        {
            // A garage move changes the hidden queue and may start its door
            // transition. Reloading the deterministic level is the only safe
            // one-step rollback until the future full history system is added.
            LoadLevel(levelIndex);
            return;
        }

        isAnimating = true;
        CarPuzzlePiece undoPiece = lastMovedPiece;
        lastMovedPiece = null;
        trayPieces.Remove(undoPiece);
        if (parkedPiece == undoPiece) parkedPiece = null;
        if (!boardPieces.Contains(undoPiece)) boardPieces.Add(undoPiece);
        undoPiece.SetVisible(true);
        undoPiece.SetBoardPose(GetBoardPiecePosition(undoPiece));
        UpdateTrayHighlights();
        UpdateParkingHighlight();
        StartCoroutine(FinishUndoLayout());
    }

    private IEnumerator FinishUndoLayout()
    {
        yield return StartCoroutine(AnimateTrayLayout(0.22f));
        isAnimating = false;
        RefreshHud();
    }

    public void BeginTowTruckRescue()
    {
        if (towRescueUsed || trayPieces.Count == 0 || outcomeResolved) return;

        List<CarPuzzlePiece> rescuePieces = FindMinimalSolvableRescueSet();
        if (rescuePieces == null || rescuePieces.Count == 0)
        {
            if (hud != null) hud.ShowNoSolvableRescue();
            return;
        }

        towRescueUsed = true;
        trafficJamPopupOpen = false;
        if (hud != null) hud.HideTrafficJamForRescue();
        TogglePause(false);
        StartCoroutine(ReturnSolverSelectedTrayPieces(rescuePieces));
    }

    private List<CarPuzzlePiece> FindMinimalSolvableRescueSet()
    {
        if (IsGarageLevelActive())
            return BuildGarageLevelRescueSet();
        var candidates = new List<CarPuzzlePiece>();
        for (int index = 0; index < trayPieces.Count; index++)
            if (trayPieces[index] != null && !trayPieces[index].IsTrash) candidates.Add(trayPieces[index]);

        int maximumReturns = Mathf.Min(4, candidates.Count);
        for (int returnCount = 1; returnCount <= maximumReturns; returnCount++)
        {
            var selection = new List<CarPuzzlePiece>(returnCount);
            if (TryFindSolvableRescueCombination(candidates, returnCount, 0, selection, out List<CarPuzzlePiece> result))
                return result;
        }

        return null;
    }

    private List<CarPuzzlePiece> BuildGarageLevelRescueSet()
    {
        if (trayPieces.Count == 0) return null;
        int bestColor = -1;
        int bestCount = -1;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
        {
            int color = (int)PlayableColors[colorIndex];
            int count = GetRescueTrayCountForRuntime((PieceColor)color);
            if (count <= bestCount) continue;
            bestColor = color;
            bestCount = count;
        }
        var result = new List<CarPuzzlePiece>();
        for (int index = trayPieces.Count - 1; index >= 0 && result.Count < 4; index--)
            if ((int)trayPieces[index].PieceColor != bestColor) result.Add(trayPieces[index]);
        if (result.Count == 0 && trayPieces.Count > 0) result.Add(trayPieces[trayPieces.Count - 1]);
        return result;
    }

    private bool TryFindSolvableRescueCombination(
        List<CarPuzzlePiece> candidates,
        int targetCount,
        int startIndex,
        List<CarPuzzlePiece> selection,
        out List<CarPuzzlePiece> result)
    {
        if (selection.Count == targetCount)
        {
            if (IsRescueSetSolvable(selection))
            {
                result = new List<CarPuzzlePiece>(selection);
                return true;
            }

            result = null;
            return false;
        }

        int stillNeeded = targetCount - selection.Count;
        for (int index = startIndex; index <= candidates.Count - stillNeeded; index++)
        {
            selection.Add(candidates[index]);
            if (TryFindSolvableRescueCombination(candidates, targetCount, index + 1, selection, out result))
                return true;
            selection.RemoveAt(selection.Count - 1);
        }

        result = null;
        return false;
    }

    private bool IsRescueSetSolvable(
        IList<CarPuzzlePiece> returnedPieces,
        int maximumStates = RescueSearchContext.MaximumStates)
    {
        var context = new RescueSearchContext(this, returnedPieces);
        var returnedSet = new HashSet<CarPuzzlePiece>(returnedPieces);
        RescueSearchState state = BuildCurrentRescueSearchState(context, returnedSet);
        return SearchRescueSolution(context, state, maximumStates);
    }

    private RescueSearchState BuildCurrentRescueSearchState(
        RescueSearchContext context,
        HashSet<CarPuzzlePiece> returnedSet)
    {
        var state = new RescueSearchState
        {
            boardMask = context.pieces.Count == 64 ? ulong.MaxValue : (1UL << context.pieces.Count) - 1UL,
            trayCounts = 0,
            clearedColors = GetCurrentClearedColorMask(),
            clearedSetCounts = GetCurrentClearedSetCounts(),
            parkedColor = parkedPiece != null ? (sbyte)parkedPiece.PieceColor : (sbyte)-1,
            parkingUses = context.parkingLimit >= 0
                ? (byte)Mathf.Clamp(parkingUses, 0, byte.MaxValue)
                : (byte)0
        };

        for (int index = 0; index < trayPieces.Count; index++)
        {
            CarPuzzlePiece trayPiece = trayPieces[index];
            if (returnedSet.Contains(trayPiece)) continue;
            state.trayCounts = AddRescueTrayCount(state.trayCounts, (int)trayPiece.PieceColor, 1);
        }

        return NormalizeRescueMatches(context, state);
    }

    private bool TryFindSolverApprovedHintPiece(out CarPuzzlePiece safePiece)
    {
        safePiece = null;
        if (IsGarageLevelActive())
            return TryFindCatalogHintPiece(out safePiece);
        var returnedPieces = new List<CarPuzzlePiece>();
        var context = new RescueSearchContext(this, returnedPieces);
        RescueSearchState state = BuildCurrentRescueSearchState(
            context,
            new HashSet<CarPuzzlePiece>());

        // A hint is a board-exit suggestion, never an instruction to interact
        // with a car that is already in the tray or side parking. Test each
        // immediately movable board car as the first move, then reuse the full
        // solver to prove that the resulting position can still be completed.
        for (int pieceIndex = 0; pieceIndex < context.pieces.Count; pieceIndex++)
        {
            CarPuzzlePiece candidate = context.pieces[pieceIndex];
            if (!IsEligibleAutomaticHintPiece(candidate)) continue;

            ulong bit = 1UL << pieceIndex;
            if ((state.boardMask & bit) == 0UL
                || !CanRescuePieceExit(context, state, pieceIndex))
                continue;

            RescueSearchState next = state;
            next.boardMask &= ~bit;
            if (!TryApplyRescueBoardCarMove(context, next, candidate, out next))
                continue;

            context.exploredStates = 0;
            if (!SearchRescueSolution(context, next, RescueSearchContext.MaximumStates))
                continue;

            safePiece = candidate;
            return true;
        }

        return false;
    }

    private bool TryFindCatalogHintPiece(out CarPuzzlePiece safePiece)
    {
        safePiece = null;
        int bestScore = int.MinValue;
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece candidate = boardPieces[index];
            if (!IsEligibleAutomaticHintPiece(candidate)
                || !IsExitPathClear(candidate))
                continue;
            int score = candidate.IsTrash ? 20 : 0;
            score += GetRescueTrayCountForRuntime(candidate.PieceColor) * 4;
            if (candidate.SourceGarageId >= 0) score += 2;
            if (score <= bestScore) continue;
            bestScore = score;
            safePiece = candidate;
        }
        return safePiece != null;
    }

    private int GetRescueTrayCountForRuntime(PieceColor color)
    {
        int count = 0;
        for (int index = 0; index < trayPieces.Count; index++)
            if (trayPieces[index].PieceColor == color) count++;
        return count;
    }

    private bool IsEligibleAutomaticHintPiece(CarPuzzlePiece piece)
    {
        return piece != null
            && boardPieces.Contains(piece)
            && !trayPieces.Contains(piece)
            && parkedPiece != piece
            && !carsCurrentlyMoving.Contains(piece)
            && piece.IsTouchable;
    }

    private bool SearchRescueSolution(
        RescueSearchContext context,
        RescueSearchState start,
        int maximumStates)
    {
        return SearchRescueSolution(
            context,
            start,
            maximumStates,
            out _);
    }

    private bool SearchRescueSolution(
        RescueSearchContext context,
        RescueSearchState start,
        int maximumStates,
        out CarPuzzlePiece firstActionPiece)
    {
        firstActionPiece = null;
        var pending = new Stack<RescueSearchNode>();
        var visited = new HashSet<RescueSearchState>();
        pending.Push(new RescueSearchNode(start, null));

        while (pending.Count > 0 && context.exploredStates < maximumStates)
        {
            RescueSearchNode node = pending.Pop();
            RescueSearchState state = node.state;
            if (!visited.Add(state)) continue;
            context.exploredStates++;

            if ((state.clearedColors & context.activeColorMask) == context.activeColorMask)
            {
                firstActionPiece = node.firstActionPiece;
                return true;
            }

            int trayTotal = GetRescueTrayTotal(state.trayCounts);
            for (int pieceIndex = 0; pieceIndex < context.pieces.Count; pieceIndex++)
            {
                ulong bit = 1UL << pieceIndex;
                if ((state.boardMask & bit) == 0UL || !CanRescuePieceExit(context, state, pieceIndex))
                    continue;

                CarPuzzlePiece piece = context.pieces[pieceIndex];
                RescueSearchState next = state;
                next.boardMask &= ~bit;
                if (!TryApplyRescueBoardCarMove(context, next, piece, out next))
                    continue;
                CarPuzzlePiece firstMove = node.firstActionPiece != null
                    ? node.firstActionPiece
                    : piece;
                pending.Push(new RescueSearchNode(next, firstMove));
            }

            if (state.parkedColor >= 0 && trayTotal < trayCapacity)
            {
                RescueSearchState next = state;
                next.trayCounts = AddRescueTrayCount(next.trayCounts, state.parkedColor, 1);
                next.parkedColor = -1;
                CarPuzzlePiece firstMove = node.firstActionPiece != null
                    ? node.firstActionPiece
                    : parkedPiece;
                pending.Push(new RescueSearchNode(
                    NormalizeRescueMatches(context, next),
                    firstMove));
            }

            bool parkingAvailable = IsSideParkingAvailable()
                && (context.parkingLimit < 0 || state.parkingUses < context.parkingLimit);
            if (!parkingAvailable) continue;

            for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
            {
                int color = (int)PlayableColors[colorIndex];
                if (GetRescueTrayCount(state.trayCounts, color) == 0) continue;

                RescueSearchState next = state;
                next.trayCounts = AddRescueTrayCount(next.trayCounts, color, -1);
                if (context.parkingLimit >= 0) next.parkingUses++;
                if (state.parkedColor >= 0)
                {
                    if (state.parkedColor == color) continue;
                    next.trayCounts = AddRescueTrayCount(next.trayCounts, state.parkedColor, 1);
                }
                next.parkedColor = (sbyte)color;
                CarPuzzlePiece firstMove = node.firstActionPiece != null
                    ? node.firstActionPiece
                    : FindTrayPiece((PieceColor)color);
                pending.Push(new RescueSearchNode(
                    NormalizeRescueMatches(context, next),
                    firstMove));
            }
        }

        return false;
    }

    private bool TryApplyRescueBoardCarMove(
        RescueSearchContext context,
        RescueSearchState state,
        CarPuzzlePiece piece,
        out RescueSearchState next)
    {
        next = state;
        if (piece == null) return false;
        if (piece.IsTrash) return true;

        int trayTotal = GetRescueTrayTotal(state.trayCounts);
        int incomingColor = (int)piece.PieceColor;
        if (trayTotal < trayCapacity)
        {
            next.trayCounts = AddRescueTrayCount(
                next.trayCounts,
                incomingColor,
                1);
            next = NormalizeRescueMatches(context, next);
            return true;
        }

        if (!TryGetRescueDominantExchangeOutlier(
                state,
                incomingColor,
                out int outlierColor))
            return false;

        next.trayCounts = AddRescueTrayCount(next.trayCounts, outlierColor, -1);
        next.trayCounts = AddRescueTrayCount(next.trayCounts, incomingColor, 1);
        next.parkedColor = (sbyte)outlierColor;
        if (context.parkingLimit >= 0) next.parkingUses++;
        next = NormalizeRescueMatches(context, next);
        return true;
    }

    private bool TryGetRescueDominantExchangeOutlier(
        RescueSearchState state,
        int incomingColor,
        out int outlierColor)
    {
        outlierColor = -1;
        if (state.parkedColor >= 0
            || trayCapacity < 3
            || GetRescueTrayTotal(state.trayCounts) != trayCapacity
            || !IsSideParkingAvailable()
            || (activeExperimentalRules != null
                && activeExperimentalRules.parkingUseLimit >= 0
                && state.parkingUses >= activeExperimentalRules.parkingUseLimit))
            return false;

        int dominantCount = GetRescueTrayCount(state.trayCounts, incomingColor);
        if (dominantCount != trayCapacity - 1
            || dominantCount <= 1
            || dominantCount != GetMatchTarget((PieceColor)incomingColor) - 1
            || !IsRescueColorNextInOrder(state, incomingColor))
            return false;

        int outlierCount = 0;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
        {
            int color = (int)PlayableColors[colorIndex];
            if (color == incomingColor) continue;
            int count = GetRescueTrayCount(state.trayCounts, color);
            if (count == 0) continue;
            outlierCount += count;
            outlierColor = color;
        }

        return outlierCount == 1 && outlierColor >= 0;
    }

    private bool IsRescueColorNextInOrder(
        RescueSearchState state,
        int incomingColor)
    {
        if (activeExperimentalRules == null
            || activeExperimentalRules.requiredColorOrder.Length == 0)
            return true;

        for (int index = 0;
            index < activeExperimentalRules.requiredColorOrder.Length;
            index++)
        {
            int color = (int)activeExperimentalRules.requiredColorOrder[index];
            if ((state.clearedColors & (1 << color)) != 0) continue;
            return color == incomingColor;
        }

        return false;
    }

    private bool CanRescuePieceExit(RescueSearchContext context, RescueSearchState state, int pieceIndex)
    {
        CarPuzzlePiece piece = context.pieces[pieceIndex];
        if (piece.IsRevealCovered)
        {
            ulong initialNeighbors = context.revealNeighborMaskByPiece[pieceIndex];
            // A covered car becomes available permanently as soon as any car
            // that originally touched one of the box's four sides is removed.
            if (initialNeighbors == 0UL
                || (initialNeighbors & ~state.boardMask) == 0UL)
                return false;
        }
        int unlockColor = context.unlockColorByPiece[pieceIndex];
        if (unlockColor >= 0 && (state.clearedColors & (1 << unlockColor)) == 0) return false;
        if (piece.IsLocked && unlockColor < 0) return false;

        Vector2Int step = DirectionToGridStep(piece.Direction);
        int row = piece.Row + step.y;
        int col = piece.Col + step.x;
        ulong ownBit = 1UL << pieceIndex;
        while (row >= 0 && row < activeBoardSize && col >= 0 && col < activeBoardSize)
        {
            ulong blockers = context.cellOccupants[row * activeBoardSize + col] & state.boardMask & ~ownBit;
            if (blockers != 0UL) return false;
            row += step.y;
            col += step.x;
        }
        return true;
    }

    private RescueSearchState NormalizeRescueMatches(RescueSearchContext context, RescueSearchState state)
    {
        bool ordered = activeExperimentalRules != null
            && activeExperimentalRules.requiredColorOrder.Length > 0;
        bool changed;
        do
        {
            changed = false;
            if (ordered)
            {
                for (int orderIndex = 0; orderIndex < activeExperimentalRules.requiredColorOrder.Length; orderIndex++)
                {
                    int color = (int)activeExperimentalRules.requiredColorOrder[orderIndex];
                    if ((state.clearedColors & (1 << color)) != 0) continue;
                    int target = GetMatchTarget((PieceColor)color);
                    if (target > 0 && HasRescueTrayMatch(state.trayCounts, color, target))
                    {
                        state.trayCounts = AddRescueTrayCount(state.trayCounts, color, -target);
                        state.clearedSetCounts = AddRescueClearedSetCount(
                            state.clearedSetCounts,
                            color,
                            1);
                        if (GetRescueClearedSetCount(state.clearedSetCounts, color)
                            >= GetRequiredSetCount((PieceColor)color))
                            state.clearedColors |= (byte)(1 << color);
                        changed = true;
                    }
                    break;
                }
            }
            else
            {
                for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
                {
                    int color = (int)PlayableColors[colorIndex];
                    if ((state.clearedColors & (1 << color)) != 0) continue;
                    int target = GetMatchTarget((PieceColor)color);
                    if (target <= 0 || !HasRescueTrayMatch(state.trayCounts, color, target)) continue;
                    state.trayCounts = AddRescueTrayCount(state.trayCounts, color, -target);
                    state.clearedSetCounts = AddRescueClearedSetCount(
                        state.clearedSetCounts,
                        color,
                        1);
                    if (GetRescueClearedSetCount(state.clearedSetCounts, color)
                        >= GetRequiredSetCount((PieceColor)color))
                        state.clearedColors |= (byte)(1 << color);
                    changed = true;
                }
            }
        } while (changed);

        return state;
    }

    private bool HasRescueTrayMatch(int encodedCounts, int color, int target)
    {
        int matchingCount = GetRescueTrayCount(encodedCounts, color);
        if (matchingCount < target) return false;
        if (!RequiresAdjacency((PieceColor)color)) return true;

        // The compact search stores color counts rather than every tray order.
        // Requiring the adjacency color to fill the simulated tray is a
        // conservative proof that its cars are consecutive. This prevents the
        // tow truck from promising a solution based on a merely theoretical
        // arrangement after a parking swap.
        return matchingCount == GetRescueTrayTotal(encodedCounts);
    }

    private byte GetCurrentClearedColorMask()
    {
        byte mask = 0;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
        {
            int color = (int)PlayableColors[colorIndex];
            if (IsColorCleared(PlayableColors[colorIndex])) mask |= (byte)(1 << color);
        }
        return mask;
    }

    private int GetCurrentClearedSetCounts()
    {
        int encoded = 0;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
        {
            PieceColor color = PlayableColors[colorIndex];
            int clearedSets = activeMatchTarget > 0
                ? GetClearedCount(color) / activeMatchTarget
                : 0;
            encoded = SetRescueClearedSetCount(encoded, (int)color, clearedSets);
        }
        return encoded;
    }

    private int GetRequiredSetCount(PieceColor color)
    {
        return activeMatchTarget > 0
            ? GetActiveTarget(color) / activeMatchTarget
            : 0;
    }

    private static int GetRescueClearedSetCount(int encodedCounts, int color)
    {
        return (encodedCounts >> (color * 4)) & 0xF;
    }

    private static int SetRescueClearedSetCount(int encodedCounts, int color, int count)
    {
        int shift = color * 4;
        return (encodedCounts & ~(0xF << shift)) | (Mathf.Clamp(count, 0, 15) << shift);
    }

    private static int AddRescueClearedSetCount(int encodedCounts, int color, int amount)
    {
        return SetRescueClearedSetCount(
            encodedCounts,
            color,
            GetRescueClearedSetCount(encodedCounts, color) + amount);
    }

    private static int GetRescueTrayCount(int encodedCounts, int color)
    {
        return (encodedCounts >> (color * 3)) & 0x7;
    }

    private static int SetRescueTrayCount(int encodedCounts, int color, int count)
    {
        int shift = color * 3;
        return (encodedCounts & ~(0x7 << shift)) | (Mathf.Clamp(count, 0, 7) << shift);
    }

    private static int AddRescueTrayCount(int encodedCounts, int color, int amount)
    {
        return SetRescueTrayCount(encodedCounts, color, GetRescueTrayCount(encodedCounts, color) + amount);
    }

    private static int GetRescueTrayTotal(int encodedCounts)
    {
        int total = 0;
        for (int colorIndex = 0; colorIndex < PlayableColors.Length; colorIndex++)
            total += GetRescueTrayCount(encodedCounts, (int)PlayableColors[colorIndex]);
        return total;
    }

    private IEnumerator ReturnSolverSelectedTrayPieces(List<CarPuzzlePiece> pieces)
    {
        if (pieces == null || pieces.Count == 0) yield break;

        isAnimating = true;
        lastMovedPiece = null;
        List<CarPuzzlePiece> orderedPieces = OrderRescuePiecesForReturn(pieces);
        for (int index = 0; index < orderedPieces.Count; index++)
        {
            CarPuzzlePiece piece = orderedPieces[index];
            if (piece == null || !trayPieces.Contains(piece)) continue;

            yield return StartCoroutine(piece.PulseTowSelection(0.34f));
            trayPieces.Remove(piece);
            if (!boardPieces.Contains(piece)) boardPieces.Add(piece);
            UpdateTrayHighlights();
            StartCoroutine(AnimateTrayLayout(0.20f));
            piece.SetDirectionArrowVisible(false);
            yield return StartCoroutine(DriveRescuedPieceBackToBoard(piece));
        }

        isAnimating = false;
        CheckTrayMatches();
    }

    private List<CarPuzzlePiece> OrderRescuePiecesForReturn(IList<CarPuzzlePiece> selectedPieces)
    {
        var remaining = new List<CarPuzzlePiece>(selectedPieces);
        var ordered = new List<CarPuzzlePiece>(selectedPieces.Count);
        var simulatedBoard = new List<CarPuzzlePiece>(boardPieces);

        while (remaining.Count > 0)
        {
            int selectedIndex = -1;
            for (int index = 0; index < remaining.Count; index++)
            {
                if (!IsReturnPathClear(remaining[index], simulatedBoard)) continue;
                selectedIndex = index;
                break;
            }

            // Opposing cars can occasionally have mutually crossing original
            // lanes. Keep a stable fallback order; the board cells themselves
            // remain distinct and the perimeter portion is collision-free.
            if (selectedIndex < 0) selectedIndex = 0;
            CarPuzzlePiece piece = remaining[selectedIndex];
            remaining.RemoveAt(selectedIndex);
            ordered.Add(piece);
            simulatedBoard.Add(piece);
        }

        return ordered;
    }

    private bool IsReturnPathClear(CarPuzzlePiece piece, IList<CarPuzzlePiece> simulatedBoard)
    {
        Vector2Int step = DirectionToGridStep(piece.Direction);
        int row = piece.Row + step.y;
        int col = piece.Col + step.x;
        while (row >= 0 && row < activeBoardSize && col >= 0 && col < activeBoardSize)
        {
            for (int index = 0; index < simulatedBoard.Count; index++)
            {
                CarPuzzlePiece blocker = simulatedBoard[index];
                if (blocker != piece && blocker.OccupiesCell(row, col)) return false;
            }
            row += step.y;
            col += step.x;
        }
        return true;
    }

    private IEnumerator DriveRescuedPieceBackToBoard(CarPuzzlePiece piece)
    {
        Vector3 boardTarget = GetBoardPiecePosition(piece);
        Vector3 boardExit = GetBoardExitPosition(piece);
        float routeHeight = boardTarget.y;
        float trayApproachZ = GetTrayApproachZ();
        float sideX = GetBoardRouteSideX(piece, boardExit.x);

        if (!IsReturnPathClear(piece, boardPieces))
        {
            // If two restored cars originally crossed one another's exit lanes,
            // the tow truck lifts the later car above the board and lowers it
            // straight into its own empty cells instead of touching either car.
            Vector3 sideLane = new Vector3(sideX, routeHeight, trayApproachZ);
            yield return StartCoroutine(DriveRouteSegment(piece,
                new Vector3(piece.transform.position.x, routeHeight, trayApproachZ), OutsideCarDriveSpeed));
            yield return StartCoroutine(DriveRouteSegment(piece, sideLane, OutsideCarDriveSpeed));

            float liftHeight = routeHeight + 3.2f;
            Vector3 liftedSide = new Vector3(sideX, liftHeight, trayApproachZ);
            Vector3 liftedTarget = new Vector3(boardTarget.x, liftHeight, boardTarget.z);
            yield return StartCoroutine(piece.DriveTowLiftTo(liftedSide, 0.24f));
            yield return StartCoroutine(DriveRouteSegment(piece, liftedTarget, OutsideCarDriveSpeed));
            yield return StartCoroutine(piece.DriveToBoardSlot(boardTarget, 0.30f));
            yield break;
        }

        // Reverse the same collision-free perimeter used when cars leave the
        // board. The original grid cells are still empty because board cars
        // never move into one another's spaces.
        var route = new List<Vector3>
        {
            new Vector3(piece.transform.position.x, routeHeight, trayApproachZ),
            new Vector3(sideX, routeHeight, trayApproachZ),
            new Vector3(sideX, routeHeight, boardExit.z),
            boardExit,
            boardTarget
        };
        yield return StartCoroutine(DriveRoundedRouteToFinalApproach(
            piece, route, OutsideCarDriveSpeed, RouteCornerRadius));
        float duration = Mathf.Clamp(
            Vector3.Distance(piece.transform.position, boardTarget) / CarDriveSpeed,
            0.10f,
            0.26f);
        yield return StartCoroutine(piece.DriveToBoardSlot(boardTarget, duration));
    }

    public void TogglePause(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
        if (paused) ClearGameplayHint();
        else ResetGameplayHintTimer();
    }

    private void RegisterWrongMove()
    {
        if (outcomeResolved) return;

        if (!EnableHeartSystem)
        {
            CarPrototypeFeedback.Error();
            RefreshHud();
            return;
        }

        if (hearts <= 0) return;

        hearts--;
        bool lostAllHearts = hearts == 0;
        if (!lostAllHearts) CarPrototypeFeedback.Error();
        if (lostAllHearts)
            outcomeResolved = true;
        RefreshHud();
        if (lostAllHearts && hud != null)
            hud.ShowDefeat(true);
    }

    private void RefreshHud()
    {
        UpdateTutorialStep();
        UpdateTutorialPieceDimming();
        UpdateDirectionArrows();
        if (hud != null)
            hud.Refresh(
                BoardNumber,
                redCleared,
                greenCleared,
                blueCleared,
                purpleCleared,
                yellowCleared,
                pinkCleared,
                activeRedTarget,
                activeGreenTarget,
                activeBlueTarget,
                activePurpleTarget,
                activeYellowTarget,
                activePinkTarget,
                hearts,
                trayCapacity,
                extraSlotUsed);

        EvaluateAvailableMoves();
    }

    public void ToggleDirectionArrows()
    {
        directionArrowsEnabled = !directionArrowsEnabled;
        UpdateDirectionArrows();
        RefreshHud();
    }

    public void ToggleLevelEditMode()
    {
        levelEditMode = !levelEditMode;
        SetLevelEditTool(LevelEditTool.RotateCars, false);
        if (levelEditMode)
        {
            directionArrowsEnabled = true;
            bool boardAlreadyChanged = trayPieces.Count > 0 || parkedPiece != null || boardPieces.Count != allPieces.Count;
            if (boardAlreadyChanged)
            {
                LoadLevel(levelIndex);
                return;
            }

            if (hud != null) hud.ClearLevelEditResult();
            UpdateDirectionArrows();
            RefreshHud();
            return;
        }

        // Leaving edit mode discards any unsaved test state and reloads the
        // last saved layout (or the authored layout when no edit was saved).
        LoadLevel(levelIndex);
    }

    public void ToggleLevelEditSwapMode()
    {
        if (!levelEditMode || isAnimating || outcomeResolved) return;

        SetLevelEditTool(
            LevelEditSwapMode ? LevelEditTool.RotateCars : LevelEditTool.SwapCars,
            true);
    }

    public void ToggleLevelEditCarMode()
    {
        if (!levelEditMode || isAnimating || outcomeResolved) return;
        SetLevelEditTool(
            LevelEditCarMode ? LevelEditTool.RotateCars : LevelEditTool.EditCars,
            true);
    }

    public void SelectLevelEditCarColor(int color)
    {
        if (!levelEditMode || isAnimating || outcomeResolved
            || color < (int)PieceColor.Red || color > (int)PieceColor.Pink)
            return;

        levelEditCarColor = (PieceColor)color;
        if (!LevelEditCarMode) SetLevelEditTool(LevelEditTool.EditCars, false);
        if (hud != null) hud.ShowLevelEditCarColorSelected(ColorName(levelEditCarColor));
        CarPrototypeFeedback.ButtonTap();
        RefreshHud();
    }

    public int GetLevelEditGarageCarColor(int queueIndex)
    {
        if (levelEditCarGarageSelection == null
            || queueIndex < 0
            || queueIndex >= levelEditCarGarageSelection.specification.carQueue.Length)
            return -1;
        return (int)levelEditCarGarageSelection.specification.carQueue[queueIndex];
    }

    public void SelectLevelEditGarageCarSlot(int queueIndex)
    {
        if (!levelEditMode || !LevelEditCarMode || isAnimating || outcomeResolved
            || levelEditCarGarageSelection == null
            || queueIndex < 0
            || queueIndex >= levelEditCarGarageSelection.specification.carQueue.Length)
            return;

        levelEditCarGarageSlot = queueIndex;
        if (!SetEditedGarageCarColor(
                levelEditCarGarageSelection,
                queueIndex,
                levelEditCarColor))
        {
            if (hud != null) hud.ShowLevelEditGarageCarEditFailed();
            CarPrototypeFeedback.Error();
            return;
        }

        if (hud != null)
            hud.ShowLevelEditGarageCarChanged(queueIndex + 1, ColorName(levelEditCarColor));
        CarPrototypeFeedback.ButtonTap();
        RecalculateEditedColorGoals();
        RefreshHud();
    }

    public void AddLevelEditGarageCar()
    {
        if (!CanResizeSelectedGarageQueue(out ActiveGarage garage)) return;
        if (garage.specification.carQueue.Length >= MaximumEditableGarageQueueSlots)
        {
            if (hud != null) hud.ShowLevelEditGarageQueueLimit();
            CarPrototypeFeedback.Error();
            return;
        }

        var expanded = new PieceColor[garage.specification.carQueue.Length + 1];
        System.Array.Copy(garage.specification.carQueue, expanded, garage.specification.carQueue.Length);
        expanded[expanded.Length - 1] = levelEditCarColor;
        garage.specification.carQueue = expanded;
        levelEditCarGarageSlot = expanded.Length - 1;
        if (hud != null) hud.ShowLevelEditGarageCarAdded(ColorName(levelEditCarColor));
        CarPrototypeFeedback.ButtonTap();
        RecalculateEditedColorGoals();
        RefreshHud();
    }

    public void DeleteLevelEditGarageCar()
    {
        if (!CanResizeSelectedGarageQueue(out ActiveGarage garage)) return;
        PieceColor[] queue = garage.specification.carQueue;
        if (queue.Length <= 1 || levelEditCarGarageSlot < 0
            || levelEditCarGarageSlot >= queue.Length)
        {
            if (hud != null) hud.ShowLevelEditGarageNeedsOneCar();
            CarPrototypeFeedback.Error();
            return;
        }

        int removedIndex = levelEditCarGarageSlot;
        var reduced = new PieceColor[queue.Length - 1];
        if (removedIndex > 0)
            System.Array.Copy(queue, 0, reduced, 0, removedIndex);
        if (removedIndex < queue.Length - 1)
            System.Array.Copy(
                queue,
                removedIndex + 1,
                reduced,
                removedIndex,
                queue.Length - removedIndex - 1);
        garage.specification.carQueue = reduced;

        int exposedQueueIndex = garage.nextQueueIndex - 1;
        if (removedIndex == exposedQueueIndex)
        {
            garage.nextQueueIndex = 1;
            ReplaceExposedGarageCarColor(garage, reduced[0]);
        }
        levelEditCarGarageSlot = Mathf.Clamp(removedIndex, 0, reduced.Length - 1);
        if (hud != null) hud.ShowLevelEditGarageCarDeleted();
        CarPrototypeFeedback.ButtonTap();
        RecalculateEditedColorGoals();
        RefreshHud();
    }

    private bool CanResizeSelectedGarageQueue(out ActiveGarage garage)
    {
        garage = levelEditCarGarageSelection;
        if (!levelEditMode || !LevelEditCarMode || isAnimating || outcomeResolved
            || garage == null || garage.specification == null)
            return false;
        if (garage.specification.isEditorCreated)
        {
            if (hud != null) hud.ShowLevelEditCreatedGarageQueueFixed();
            CarPrototypeFeedback.Error();
            return false;
        }
        return true;
    }

    public void ToggleLevelEditGarageMode()
    {
        if (!levelEditMode || isAnimating || outcomeResolved) return;
        SetLevelEditTool(
            LevelEditGarageMode ? LevelEditTool.RotateCars : LevelEditTool.MoveGarages,
            true);
    }

    public void ToggleLevelEditBoxMode()
    {
        if (!levelEditMode || isAnimating || outcomeResolved) return;
        SetLevelEditTool(
            LevelEditBoxMode ? LevelEditTool.RotateCars : LevelEditTool.MoveBoxes,
            true);
    }

    private void SetLevelEditTool(LevelEditTool tool, bool provideFeedback)
    {
        levelEditTool = tool;
        levelEditSwapSelection = null;
        levelEditCarGarageSelection = null;
        levelEditCarGarageSlot = -1;
        levelEditGarageSelection = null;
        levelEditBoxSelection = null;
        if (hud != null) hud.ShowLevelEditModeInstruction();
        if (provideFeedback) CarPrototypeFeedback.ButtonTap();
        RefreshHud();
    }

    public void CheckEditedLevelSolvability()
    {
        if (!levelEditMode || isAnimating || outcomeResolved) return;

        bool solvable = IsGarageLevelActive()
            ? TryFindCatalogHintPiece(out _)
            : IsRescueSetSolvable(
                new List<CarPuzzlePiece>(),
                1500000);
        if (hud != null) hud.ShowLevelEditSolvability(solvable);
    }

    public void SaveEditedLevelDirections()
    {
        if (!levelEditMode || isAnimating || outcomeResolved) return;

        if (!IsEditedLevelGeometryValid(out string validationError))
        {
            Debug.LogError($"Edited level was not saved: {validationError}");
            if (hud != null) hud.ShowLevelEditSaveFailed();
            CarPrototypeFeedback.Error();
            return;
        }

        string levelKey = GetDirectionOverrideKey(BoardNumber);
        LevelDirectionOverride savedLevel = null;
        for (int index = 0; index < directionOverrideStore.levels.Count; index++)
        {
            if (directionOverrideStore.levels[index].levelKey != levelKey) continue;
            savedLevel = directionOverrideStore.levels[index];
            break;
        }

        if (savedLevel == null)
        {
            savedLevel = new LevelDirectionOverride { levelKey = levelKey };
            directionOverrideStore.levels.Add(savedLevel);
        }

        savedLevel.storesCarPlacements = true;
        if (savedLevel.cars == null)
            savedLevel.cars = new List<CarDirectionOverride>();
        savedLevel.cars.Clear();
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece == null || piece.LevelEditSourceIndex < 0) continue;
            savedLevel.cars.Add(BuildEditedCarOverride(piece));
        }
        var storedIndexes = new List<int>(levelEditStoredPieces.Keys);
        storedIndexes.Sort();
        for (int index = 0; index < storedIndexes.Count; index++)
            if (levelEditStoredPieces.TryGetValue(storedIndexes[index], out CarPuzzlePiece stored)
                && stored != null)
                savedLevel.cars.Add(BuildEditedCarOverride(stored));

        savedLevel.storesGaragePlacements = true;
        if (savedLevel.garages == null)
            savedLevel.garages = new List<GaragePlacementOverride>();
        savedLevel.garages.Clear();
        for (int index = 0; index < activeGarages.Count; index++)
        {
            ActiveGarage garage = activeGarages[index];
            if (garage == null || garage.specification == null) continue;
            var savedGarage = new GaragePlacementOverride
            {
                id = garage.specification.id,
                row = garage.specification.row,
                col = garage.specification.col,
                isEditorCreated = garage.specification.isEditorCreated,
                storesQueueColors = true
            };
            for (int queueIndex = 0; queueIndex < garage.specification.carQueue.Length; queueIndex++)
                savedGarage.queueColors.Add((int)garage.specification.carQueue[queueIndex]);
            if (garage.specification.isEditorCreated)
            {
                savedGarage.storedPieceIndexes.AddRange(garage.specification.storedPieceIndexes);
            }
            savedLevel.garages.Add(savedGarage);
        }

        savedLevel.storesBoxPlacements = true;
        if (savedLevel.boxes == null)
            savedLevel.boxes = new List<BoxPlacementOverride>();
        savedLevel.boxes.Clear();
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece == null || piece.LevelEditSourceIndex < 0 || !piece.IsRevealCovered) continue;
            Quaternion visualRotation = piece.GetEditedRevealBoxWorldRotation();
            savedLevel.boxes.Add(new BoxPlacementOverride
            {
                pieceIndex = piece.LevelEditSourceIndex,
                storesVisualRotation = true,
                rotationX = visualRotation.x,
                rotationY = visualRotation.y,
                rotationZ = visualRotation.z,
                rotationW = visualRotation.w
            });
        }

        if (!WriteSavedDirectionOverrides())
        {
            if (hud != null) hud.ShowLevelEditSaveFailed();
            return;
        }

        if (hud != null) hud.ShowLevelEditSaved();
    }

    private static CarDirectionOverride BuildEditedCarOverride(CarPuzzlePiece piece)
    {
        return new CarDirectionOverride
        {
            storesPosition = true,
            pieceIndex = piece.LevelEditSourceIndex,
            row = piece.Row,
            col = piece.Col,
            color = (int)piece.PieceColor,
            cellLength = piece.CellLength,
            direction = (int)piece.Direction
        };
    }

    private bool IsEditedLevelGeometryValid(out string error)
    {
        error = string.Empty;
        var occupied = new bool[activeBoardSize, activeBoardSize];
        var sourceIndexes = new HashSet<int>();
        var expectedStoredIndexes = new HashSet<int>();

        for (int index = 0; index < activeGarages.Count; index++)
        {
            ActiveGarage garage = activeGarages[index];
            if (garage == null || garage.specification == null || garage.exposedPiece == null)
            {
                error = "a garage has no exposed car";
                return false;
            }

            int row = garage.specification.row;
            int col = garage.specification.col;
            if (row < 0 || row >= activeBoardSize - 1 || col < 0 || col >= activeBoardSize)
            {
                error = "a garage is outside the board";
                return false;
            }
            if (occupied[row, col])
            {
                error = "two garage footprints overlap";
                return false;
            }
            occupied[row, col] = true;
            if (garage.exposedPiece.SourceGarageId != garage.specification.id
                || garage.exposedPiece.Row != row + 1
                || garage.exposedPiece.Col != col)
            {
                error = "a garage's exposed car is detached from it";
                return false;
            }

            if (garage.specification.isEditorCreated)
            {
                if (garage.specification.carQueue.Length != 2
                    || garage.specification.storedPieceIndexes.Length != 2)
                {
                    error = "a created garage has invalid contents";
                    return false;
                }
                for (int storedIndex = 0;
                     storedIndex < garage.specification.storedPieceIndexes.Length;
                     storedIndex++)
                {
                    int sourceIndex = garage.specification.storedPieceIndexes[storedIndex];
                    if (!expectedStoredIndexes.Add(sourceIndex)
                        || !levelEditStoredPieces.TryGetValue(sourceIndex, out CarPuzzlePiece stored)
                        || stored == null
                        || stored.PieceColor != garage.specification.carQueue[storedIndex]
                        || stored.CellLength != 1
                        || stored.IsRevealCovered)
                    {
                        error = "a created garage has missing or mismatched stored cars";
                        return false;
                    }
                }
            }
        }

        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece == null) continue;
            if (piece.LevelEditSourceIndex >= 0
                && !sourceIndexes.Add(piece.LevelEditSourceIndex))
            {
                error = "two cars share the same saved identity";
                return false;
            }

            Vector2Int step = DirectionToGridStep(piece.Direction);
            for (int offset = 0; offset < piece.CellLength; offset++)
            {
                int row = piece.Row - step.y * offset;
                int col = piece.Col - step.x * offset;
                if (row < 0 || row >= activeBoardSize || col < 0 || col >= activeBoardSize)
                {
                    error = "a car is outside the board";
                    return false;
                }
                if (occupied[row, col])
                {
                    error = "cars or garage footprints overlap";
                    return false;
                }
                occupied[row, col] = true;
            }

            if (piece.IsRevealCovered && !CanEditBoxOnPiece(piece))
            {
                error = "a box has an invalid hidden car";
                return false;
            }
        }
        if (levelEditStoredPieces.Count != expectedStoredIndexes.Count)
        {
            error = "an unassigned stored car exists";
            return false;
        }
        foreach (KeyValuePair<int, CarPuzzlePiece> storedEntry in levelEditStoredPieces)
        {
            CarPuzzlePiece stored = storedEntry.Value;
            if (!expectedStoredIndexes.Contains(storedEntry.Key)
                || stored == null
                || stored.LevelEditSourceIndex != storedEntry.Key
                || !sourceIndexes.Add(storedEntry.Key))
            {
                error = "a stored car has an invalid saved identity";
                return false;
            }
        }
        return true;
    }

    private string GetDirectionOverrideKey(int boardNumber)
    {
        if (isDemoMode) return $"demo:{boardNumber}";
        // The 3x3 redesign replaces the previous Level 9-20 layouts completely.
        // Keep old editor overrides from being applied by piece index to the
        // new boards while allowing fresh edits to save normally.
        if (boardNumber == 9)
            return "level-3x3-v1:9";
        if (boardNumber >= 10 && boardNumber <= 20)
            return $"level-3x3-v3:{boardNumber}";
        if (boardNumber >= 21 && boardNumber <= CampaignLevelCount)
            return $"level-refined-catalog-v1:{boardNumber}";
        return $"level:{boardNumber}";
    }

    private void LoadSavedDirectionOverrides()
    {
        string json = string.Empty;
        TextAsset bundled = Resources.Load<TextAsset>("CarPrototype/level_direction_overrides");
        if (bundled != null) json = bundled.text;

#if !UNITY_EDITOR
        string persistentPath = System.IO.Path.Combine(
            Application.persistentDataPath, "level_direction_overrides.json");
        if (System.IO.File.Exists(persistentPath))
            json = System.IO.File.ReadAllText(persistentPath);
#endif

        if (string.IsNullOrWhiteSpace(json))
        {
            directionOverrideStore = new DirectionOverrideStore();
            return;
        }

        try
        {
            directionOverrideStore = JsonUtility.FromJson<DirectionOverrideStore>(json)
                ?? new DirectionOverrideStore();
            if (directionOverrideStore.levels == null)
                directionOverrideStore.levels = new List<LevelDirectionOverride>();
            for (int index = 0; index < directionOverrideStore.levels.Count; index++)
            {
                LevelDirectionOverride savedLevel = directionOverrideStore.levels[index];
                if (savedLevel == null) continue;
                if (savedLevel.cars == null)
                    savedLevel.cars = new List<CarDirectionOverride>();
                if (savedLevel.garages == null)
                    savedLevel.garages = new List<GaragePlacementOverride>();
                for (int garageIndex = 0; garageIndex < savedLevel.garages.Count; garageIndex++)
                {
                    GaragePlacementOverride garage = savedLevel.garages[garageIndex];
                    if (garage == null) continue;
                    if (garage.queueColors == null) garage.queueColors = new List<int>();
                    if (garage.storedPieceIndexes == null)
                        garage.storedPieceIndexes = new List<int>();
                }
                if (savedLevel.boxes == null)
                    savedLevel.boxes = new List<BoxPlacementOverride>();
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Could not read saved level edits: {exception.Message}");
            directionOverrideStore = new DirectionOverrideStore();
        }
    }

    private static GarageSpec[] CloneGarageSpecifications(GarageSpec[] authored)
    {
        if (authored == null || authored.Length == 0) return new GarageSpec[0];

        var clones = new GarageSpec[authored.Length];
        for (int index = 0; index < authored.Length; index++)
        {
            GarageSpec source = authored[index];
            clones[index] = new GarageSpec(
                source.id,
                source.row,
                source.col,
                source.carQueue,
                source.isEditorCreated,
                source.storedPieceIndexes);
        }
        return clones;
    }

    private static HashSet<int> GetStoredPieceIndexes(GarageSpec[] garages)
    {
        var indexes = new HashSet<int>();
        if (garages == null) return indexes;
        for (int garageIndex = 0; garageIndex < garages.Length; garageIndex++)
        {
            GarageSpec garage = garages[garageIndex];
            if (garage == null || !garage.isEditorCreated) continue;
            for (int storedIndex = 0; storedIndex < garage.storedPieceIndexes.Length; storedIndex++)
                indexes.Add(garage.storedPieceIndexes[storedIndex]);
        }
        return indexes;
    }

    private bool HasSavedStructuralEdits(string levelKey)
    {
        if (directionOverrideStore == null || directionOverrideStore.levels == null)
            return false;
        for (int index = 0; index < directionOverrideStore.levels.Count; index++)
        {
            LevelDirectionOverride saved = directionOverrideStore.levels[index];
            if (saved != null && saved.levelKey == levelKey)
                return saved.storesGaragePlacements || saved.storesCarPlacements;
        }
        return false;
    }

    private bool HasSavedCarPlacements(string levelKey)
    {
        if (directionOverrideStore == null || directionOverrideStore.levels == null)
            return false;
        for (int index = 0; index < directionOverrideStore.levels.Count; index++)
        {
            LevelDirectionOverride saved = directionOverrideStore.levels[index];
            if (saved != null && saved.levelKey == levelKey)
                return saved.storesCarPlacements;
        }
        return false;
    }

    private GarageSpec[] ApplySavedLevelOverrides(
        string levelKey,
        ref PieceSpec[] specifications,
        GarageSpec[] garageSpecifications)
    {
        GarageSpec[] resolvedGarages = garageSpecifications ?? new GarageSpec[0];
        if (specifications == null || directionOverrideStore == null) return resolvedGarages;

        LevelDirectionOverride savedLevel = null;
        for (int index = 0; index < directionOverrideStore.levels.Count; index++)
        {
            if (directionOverrideStore.levels[index].levelKey != levelKey) continue;
            savedLevel = directionOverrideStore.levels[index];
            break;
        }
        if (savedLevel == null) return resolvedGarages;

        Dictionary<int, int> savedPieceIndexMap = null;
        if (savedLevel.storesCarPlacements)
        {
            if (!TryBuildSavedCarPlacements(
                    savedLevel.cars,
                    out PieceSpec[] savedSpecifications,
                    out savedPieceIndexMap))
            {
                specifications = null;
                return resolvedGarages;
            }
            specifications = savedSpecifications;
        }

        if (savedLevel.storesGaragePlacements && savedLevel.garages != null)
        {
            var savedGarages = new List<GarageSpec>();
            var usedIds = new HashSet<int>();
            for (int savedIndex = 0; savedIndex < savedLevel.garages.Count; savedIndex++)
            {
                GaragePlacementOverride saved = savedLevel.garages[savedIndex];
                if (saved == null || !usedIds.Add(saved.id)) continue;

                if (saved.isEditorCreated)
                {
                    if (saved.queueColors == null || saved.storedPieceIndexes == null
                        || saved.queueColors.Count != 2 || saved.storedPieceIndexes.Count != 2)
                        continue;
                    var queue = new List<PieceColor>();
                    for (int queueIndex = 0; queueIndex < saved.queueColors.Count; queueIndex++)
                    {
                        int color = saved.queueColors[queueIndex];
                        if (color < (int)PieceColor.Red || color > (int)PieceColor.Pink)
                            continue;
                        queue.Add((PieceColor)color);
                    }
                    if (queue.Count < 2) continue;
                    int[] storedIndexes = saved.storedPieceIndexes.ToArray();
                    if (savedPieceIndexMap != null)
                    {
                        for (int storedIndex = 0; storedIndex < storedIndexes.Length; storedIndex++)
                            storedIndexes[storedIndex] = savedPieceIndexMap.TryGetValue(
                                storedIndexes[storedIndex],
                                out int remappedIndex)
                                ? remappedIndex
                                : -1;
                    }
                    savedGarages.Add(new GarageSpec(
                        saved.id,
                        saved.row,
                        saved.col,
                        queue.ToArray(),
                        true,
                        storedIndexes));
                    continue;
                }

                for (int garageIndex = 0; garageIndex < resolvedGarages.Length; garageIndex++)
                {
                    GarageSpec authored = resolvedGarages[garageIndex];
                    if (authored.id != saved.id) continue;
                    PieceColor[] queue = authored.carQueue;
                    if (saved.storesQueueColors && saved.queueColors != null)
                    {
                        var editedQueue = new List<PieceColor>();
                        for (int queueIndex = 0; queueIndex < saved.queueColors.Count; queueIndex++)
                        {
                            int color = saved.queueColors[queueIndex];
                            if (color >= (int)PieceColor.Red && color <= (int)PieceColor.Pink)
                                editedQueue.Add((PieceColor)color);
                        }
                        if (editedQueue.Count > 0) queue = editedQueue.ToArray();
                    }
                    savedGarages.Add(new GarageSpec(
                        authored.id,
                        saved.row,
                        saved.col,
                        queue));
                    break;
                }
            }
            resolvedGarages = savedGarages.ToArray();
        }

        if (savedLevel.storesBoxPlacements)
        {
            for (int index = 0; index < specifications.Length; index++)
            {
                PieceSpec cleared = specifications[index];
                cleared.isRevealBox = false;
                cleared.storesRevealVisualRotation = false;
                cleared.revealVisualRotation = Quaternion.identity;
                specifications[index] = cleared;
            }

            if (savedLevel.boxes != null)
            {
                for (int savedIndex = 0; savedIndex < savedLevel.boxes.Count; savedIndex++)
                {
                    BoxPlacementOverride saved = savedLevel.boxes[savedIndex];
                    if (saved == null) continue;
                    int pieceIndex = saved.pieceIndex;
                    if (savedPieceIndexMap != null)
                    {
                        if (!savedPieceIndexMap.TryGetValue(
                                pieceIndex,
                                out int remappedPieceIndex))
                            continue;
                        pieceIndex = remappedPieceIndex;
                    }
                    if (pieceIndex < 0 || pieceIndex >= specifications.Length)
                        continue;

                    PieceSpec boxed = specifications[pieceIndex];
                    if (boxed.color == PieceColor.Trash || boxed.cellLength != 1) continue;
                    boxed.isRevealBox = true;
                    if (saved.storesVisualRotation)
                    {
                        Quaternion rotation = new Quaternion(
                            saved.rotationX,
                            saved.rotationY,
                            saved.rotationZ,
                            saved.rotationW);
                        if (Quaternion.Dot(rotation, rotation) > 0.5f)
                        {
                            boxed.storesRevealVisualRotation = true;
                            boxed.revealVisualRotation = rotation.normalized;
                        }
                    }
                    specifications[pieceIndex] = boxed;
                }
            }
        }

        if (savedLevel.storesCarPlacements) return resolvedGarages;
        if (savedLevel.cars == null) return resolvedGarages;

        for (int specIndex = 0; specIndex < specifications.Length; specIndex++)
        {
            PieceSpec spec = specifications[specIndex];

            // New editor saves identify the original car by its stable creation
            // index, allowing both its position and its direction to persist.
            bool appliedPositionSave = false;
            for (int savedIndex = 0; savedIndex < savedLevel.cars.Count; savedIndex++)
            {
                CarDirectionOverride saved = savedLevel.cars[savedIndex];
                if (!saved.storesPosition || saved.pieceIndex != specIndex) continue;

                if (saved.row >= 0 && saved.row < activeBoardSize
                    && saved.col >= 0 && saved.col < activeBoardSize)
                {
                    spec.row = saved.row;
                    spec.col = saved.col;
                }
                if (saved.direction >= (int)ExitDirection.Up
                    && saved.direction <= (int)ExitDirection.Right)
                    spec.direction = (ExitDirection)saved.direction;
                appliedPositionSave = true;
                break;
            }
            if (appliedPositionSave)
            {
                specifications[specIndex] = spec;
                continue;
            }

            // Compatibility with direction-only saves created by the previous
            // editor version, which identified cars by their authored cell.
            for (int savedIndex = 0; savedIndex < savedLevel.cars.Count; savedIndex++)
            {
                CarDirectionOverride saved = savedLevel.cars[savedIndex];
                if (saved.row != spec.row
                    || saved.col != spec.col
                    || saved.color != (int)spec.color
                    || saved.cellLength != spec.cellLength)
                    continue;

                if (saved.direction >= (int)ExitDirection.Up
                    && saved.direction <= (int)ExitDirection.Right)
                    spec.direction = (ExitDirection)saved.direction;
                break;
            }
            specifications[specIndex] = spec;
        }
        return resolvedGarages;
    }

    private bool TryBuildSavedCarPlacements(
        List<CarDirectionOverride> savedCars,
        out PieceSpec[] specifications,
        out Dictionary<int, int> savedIndexMap)
    {
        specifications = null;
        savedIndexMap = new Dictionary<int, int>();
        if (savedCars == null) return false;

        var orderedCars = new List<CarDirectionOverride>(savedCars.Count);
        var seenIndexes = new HashSet<int>();
        for (int index = 0; index < savedCars.Count; index++)
        {
            CarDirectionOverride saved = savedCars[index];
            if (saved == null || !saved.storesPosition || saved.pieceIndex < 0
                || !seenIndexes.Add(saved.pieceIndex)
                || saved.row < 0 || saved.row >= activeBoardSize
                || saved.col < 0 || saved.col >= activeBoardSize
                || saved.color < (int)PieceColor.Red || saved.color > (int)PieceColor.Pink
                || saved.cellLength < 1 || saved.cellLength > activeBoardSize
                || saved.direction < (int)ExitDirection.Up
                || saved.direction > (int)ExitDirection.Right)
                return false;
            orderedCars.Add(saved);
        }

        orderedCars.Sort((first, second) => first.pieceIndex.CompareTo(second.pieceIndex));
        specifications = new PieceSpec[orderedCars.Count];
        for (int index = 0; index < orderedCars.Count; index++)
        {
            CarDirectionOverride saved = orderedCars[index];
            specifications[index] = new PieceSpec(
                saved.row,
                saved.col,
                (PieceColor)saved.color,
                (ExitDirection)saved.direction,
                saved.cellLength);
            savedIndexMap.Add(saved.pieceIndex, index);
        }
        return true;
    }

    private bool WriteSavedDirectionOverrides()
    {
        try
        {
            string json = JsonUtility.ToJson(directionOverrideStore, true);
#if UNITY_EDITOR
            const string assetRelativePath = "Assets/Resources/CarPrototype/level_direction_overrides.json";
            string path = System.IO.Path.Combine(
                Application.dataPath, "Resources/CarPrototype/level_direction_overrides.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, json);
            UnityEditor.AssetDatabase.ImportAsset(
                assetRelativePath,
                UnityEditor.ImportAssetOptions.ForceUpdate);
#else
            string path = System.IO.Path.Combine(
                Application.persistentDataPath, "level_direction_overrides.json");
            System.IO.File.WriteAllText(path, json);
#endif
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"Could not save edited level: {exception.Message}");
            return false;
        }
    }

    private void SelectEditedCar(Vector2 screenPosition)
    {
        ActiveGarage tappedGarage = PickGarage(screenPosition);
        if (tappedGarage != null)
        {
            SelectGarageForCarEditing(tappedGarage);
            return;
        }

        if (!TryGetEditedBoardCell(screenPosition, out int row, out int col))
        {
            if (hud != null) hud.ShowLevelEditInvalidCarCell();
            CarPrototypeFeedback.Error();
            return;
        }

        CarPuzzlePiece existing = FindEditedOccupantAt(row, col);
        if (existing != null)
        {
            if (existing.SourceGarageId >= 0)
            {
                ActiveGarage owner = FindActiveGarage(existing.SourceGarageId);
                if (owner != null) SelectGarageForCarEditing(owner);
                else
                {
                    existing.Reject();
                    if (hud != null) hud.ShowLevelEditGarageCarEditFailed();
                    CarPrototypeFeedback.Error();
                }
                return;
            }
            if (existing.LevelEditSourceIndex < 0) return;

            ClearGarageCarEditSelection();
            DeleteEditedCar(existing);
            if (hud != null) hud.ShowLevelEditCarDeleted();
            CarPrototypeFeedback.ButtonTap();
            RecalculateEditedColorGoals();
            RefreshHud();
            return;
        }

        if (IsEditedGarageFootprintCell(row, col, null))
        {
            if (hud != null) hud.ShowLevelEditInvalidCarCell();
            CarPrototypeFeedback.Error();
            return;
        }

        ClearGarageCarEditSelection();
        PieceSpec specification = new PieceSpec(
            row,
            col,
            levelEditCarColor,
            GetDefaultEditedCarDirection(row, col));
        CarPuzzlePiece added = CreatePuzzlePiece(specification);
        added.LevelEditSourceIndex = FindNextEditedPieceIndex();
        boardPieces.Add(added);
        allPieces.Add(added);
        StartCoroutine(added.PulseTowSelection(0.48f));
        if (hud != null) hud.ShowLevelEditCarAdded(ColorName(levelEditCarColor));
        CarPrototypeFeedback.ButtonTap();
        RecalculateEditedColorGoals();
        RefreshHud();
    }

    private void SelectGarageForCarEditing(ActiveGarage garage)
    {
        if (garage == null || garage.specification == null) return;
        if (levelEditCarGarageSelection == garage)
        {
            ClearGarageCarEditSelection();
            if (hud != null) hud.ShowLevelEditModeInstruction();
            CarPrototypeFeedback.ButtonTap();
            RefreshHud();
            return;
        }

        levelEditCarGarageSelection = garage;
        levelEditCarGarageSlot = garage.specification.carQueue.Length > 0 ? 0 : -1;
        StartCoroutine(PulseEditedGarage(garage, 0.48f));
        if (garage.exposedPiece != null)
            StartCoroutine(garage.exposedPiece.PulseTowSelection(0.45f));
        if (hud != null) hud.ShowLevelEditGarageCarSelection();
        CarPrototypeFeedback.ButtonTap();
        RefreshHud();
    }

    private void ClearGarageCarEditSelection()
    {
        levelEditCarGarageSelection = null;
        levelEditCarGarageSlot = -1;
    }

    private ActiveGarage FindActiveGarage(int garageId)
    {
        for (int index = 0; index < activeGarages.Count; index++)
        {
            ActiveGarage garage = activeGarages[index];
            if (garage != null && garage.specification != null
                && garage.specification.id == garageId)
                return garage;
        }
        return null;
    }

    private bool SetEditedGarageCarColor(
        ActiveGarage garage,
        int queueIndex,
        PieceColor color)
    {
        if (garage == null || garage.specification == null
            || queueIndex < 0 || queueIndex >= garage.specification.carQueue.Length)
            return false;

        if (garage.specification.isEditorCreated
            && queueIndex < garage.specification.storedPieceIndexes.Length
            && !ReplaceStoredGarageCarColor(garage, queueIndex, color))
            return false;

        garage.specification.carQueue[queueIndex] = color;
        int exposedQueueIndex = garage.nextQueueIndex - 1;
        if (queueIndex == exposedQueueIndex)
            ReplaceExposedGarageCarColor(garage, color);
        return true;
    }

    private bool ReplaceStoredGarageCarColor(
        ActiveGarage garage,
        int queueIndex,
        PieceColor color)
    {
        int sourceIndex = garage.specification.storedPieceIndexes[queueIndex];
        if (!levelEditStoredPieces.TryGetValue(sourceIndex, out CarPuzzlePiece stored)
            || stored == null)
            return false;
        if (stored.PieceColor == color) return true;

        PieceSpec replacementSpecification = new PieceSpec(
            stored.Row,
            stored.Col,
            color,
            stored.Direction,
            stored.CellLength);
        CarPuzzlePiece replacement = CreatePuzzlePiece(replacementSpecification);
        replacement.LevelEditSourceIndex = sourceIndex;
        replacement.gameObject.SetActive(false);
        levelEditStoredPieces[sourceIndex] = replacement;
        stored.gameObject.SetActive(false);
        Destroy(stored.gameObject);
        return true;
    }

    private void ReplaceExposedGarageCarColor(ActiveGarage garage, PieceColor color)
    {
        CarPuzzlePiece exposed = garage.exposedPiece;
        if (exposed != null && exposed.PieceColor == color)
        {
            StartCoroutine(exposed.PulseTowSelection(0.42f));
            return;
        }

        int row = garage.specification.row + 1;
        int col = garage.specification.col;
        if (exposed != null)
        {
            boardPieces.Remove(exposed);
            allPieces.Remove(exposed);
            exposed.gameObject.SetActive(false);
            Destroy(exposed.gameObject);
        }

        CarPuzzlePiece replacement = CreatePuzzlePiece(new PieceSpec(
            row,
            col,
            color,
            ExitDirection.Down));
        replacement.SourceGarageId = garage.specification.id;
        garage.exposedPiece = replacement;
        boardPieces.Add(replacement);
        allPieces.Add(replacement);
        StartCoroutine(replacement.PulseTowSelection(0.48f));
    }

    private CarPuzzlePiece FindEditedOccupantAt(int row, int col)
    {
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece candidate = boardPieces[index];
            if (candidate != null && candidate.OccupiesCell(row, col)) return candidate;
        }
        return null;
    }

    private void DeleteEditedCar(CarPuzzlePiece piece)
    {
        if (piece == null) return;
        boardPieces.Remove(piece);
        allPieces.Remove(piece);
        queuedBoardPieces.Remove(piece);
        boardCarsCurrentlyDriving.Remove(piece);
        carsCurrentlyMoving.Remove(piece);
        if (lastMovedPiece == piece) lastMovedPiece = null;
        if (gameplayHintPiece == piece) ClearGameplayHint();
        for (int index = activeExperimentalLocks.Count - 1; index >= 0; index--)
            if (activeExperimentalLocks[index].piece == piece)
                activeExperimentalLocks.RemoveAt(index);
        piece.gameObject.SetActive(false);
        Destroy(piece.gameObject);
    }

    private int FindNextEditedPieceIndex()
    {
        int nextIndex = 0;
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece != null) nextIndex = Mathf.Max(nextIndex, piece.LevelEditSourceIndex + 1);
        }
        foreach (int storedIndex in levelEditStoredPieces.Keys)
            nextIndex = Mathf.Max(nextIndex, storedIndex + 1);
        return nextIndex;
    }

    private ExitDirection GetDefaultEditedCarDirection(int row, int col)
    {
        int bestDistance = row;
        ExitDirection direction = ExitDirection.Up;
        int distance = activeBoardSize - 1 - row;
        if (distance < bestDistance)
        {
            bestDistance = distance;
            direction = ExitDirection.Down;
        }
        if (col < bestDistance)
        {
            bestDistance = col;
            direction = ExitDirection.Left;
        }
        if (activeBoardSize - 1 - col < bestDistance)
            direction = ExitDirection.Right;
        return direction;
    }

    private void RecalculateEditedColorGoals()
    {
        activeRedTarget = 0;
        activeGreenTarget = 0;
        activeBlueTarget = 0;
        activePurpleTarget = 0;
        activeYellowTarget = 0;
        activePinkTarget = 0;

        var counts = new int[7];
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece != null && !piece.IsTrash) counts[(int)piece.PieceColor]++;
        }
        for (int garageIndex = 0; garageIndex < activeGarages.Count; garageIndex++)
        {
            ActiveGarage garage = activeGarages[garageIndex];
            if (garage == null || garage.specification == null) continue;
            for (int queueIndex = garage.nextQueueIndex;
                 queueIndex < garage.specification.carQueue.Length;
                 queueIndex++)
            {
                PieceColor color = garage.specification.carQueue[queueIndex];
                if (color != PieceColor.Trash) counts[(int)color]++;
            }
        }
        for (int index = 0; index < PlayableColors.Length; index++)
        {
            PieceColor color = PlayableColors[index];
            SetActiveTarget(color, counts[(int)color]);
        }
    }

    private void SelectEditedGarage(Vector2 screenPosition)
    {
        ActiveGarage tappedGarage = PickGarage(screenPosition);
        if (levelEditGarageSelection == null)
        {
            if (tappedGarage == null)
            {
                if (TryGetEditedBoardCell(screenPosition, out int addRow, out int addCol)
                    && TryAddEditedGarage(addRow, addCol, out ActiveGarage addedGarage))
                {
                    StartCoroutine(PulseEditedGarage(addedGarage, 0.48f));
                    if (addedGarage.exposedPiece != null)
                        StartCoroutine(addedGarage.exposedPiece.PulseTowSelection(0.45f));
                    if (hud != null) hud.ShowLevelEditGarageAdded();
                    CarPrototypeFeedback.ButtonTap();
                    RecalculateEditedColorGoals();
                    RefreshHud();
                    return;
                }

                if (hud != null) hud.ShowLevelEditInvalidGarageAdd();
                CarPrototypeFeedback.Error();
                return;
            }

            levelEditGarageSelection = tappedGarage;
            StartCoroutine(PulseEditedGarage(tappedGarage, 0.5f));
            if (tappedGarage.exposedPiece != null)
                StartCoroutine(tappedGarage.exposedPiece.PulseTowSelection(0.5f));
            if (hud != null) hud.ShowLevelEditGarageSelection();
            CarPrototypeFeedback.ButtonTap();
            return;
        }

        if (tappedGarage != null)
        {
            if (tappedGarage == levelEditGarageSelection)
            {
                ActiveGarage deletedGarage = levelEditGarageSelection;
                levelEditGarageSelection = null;
                List<CarPuzzlePiece> restoredCars = DeleteEditedGarage(deletedGarage);
                for (int index = 0; index < restoredCars.Count; index++)
                    StartCoroutine(restoredCars[index].PulseTowSelection(0.42f));
                if (hud != null) hud.ShowLevelEditGarageDeleted();
                CarPrototypeFeedback.ButtonTap();
                RecalculateEditedColorGoals();
                RefreshHud();
                return;
            }

            levelEditGarageSelection = tappedGarage;
            StartCoroutine(PulseEditedGarage(tappedGarage, 0.5f));
            if (hud != null) hud.ShowLevelEditGarageSelection();
            CarPrototypeFeedback.ButtonTap();
            return;
        }

        if (!TryGetEditedBoardCell(screenPosition, out int row, out int col)
            || !TryMoveEditedGarage(levelEditGarageSelection, row, col, out List<CarPuzzlePiece> movedCars))
        {
            if (hud != null) hud.ShowLevelEditInvalidGarageMove();
            CarPrototypeFeedback.Error();
            return;
        }

        ActiveGarage movedGarage = levelEditGarageSelection;
        levelEditGarageSelection = null;
        StartCoroutine(PulseEditedGarage(movedGarage, 0.45f));
        for (int index = 0; index < movedCars.Count; index++)
            if (movedCars[index] != null)
                StartCoroutine(movedCars[index].PulseTowSelection(0.42f));
        if (hud != null) hud.ShowLevelEditGarageMoveComplete();
        CarPrototypeFeedback.ButtonTap();
        RefreshHud();
    }

    private bool TryAddEditedGarage(int targetRow, int targetCol, out ActiveGarage addedGarage)
    {
        addedGarage = null;
        if (targetRow < 0 || targetRow >= activeBoardSize - 1
            || targetCol < 0 || targetCol >= activeBoardSize
            || IsEditedGarageFootprintCell(targetRow, targetCol, null)
            || IsEditedGarageFootprintCell(targetRow + 1, targetCol, null))
            return false;

        CarPuzzlePiece anchorCar = FindEditedStableOccupantAt(targetRow, targetCol);
        CarPuzzlePiece frontCar = FindEditedStableOccupantAt(targetRow + 1, targetCol);
        if (!CanStoreCarInCreatedGarage(anchorCar)
            || !CanStoreCarInCreatedGarage(frontCar)
            || anchorCar == frontCar)
            return false;

        int garageId = 0;
        for (int index = 0; index < activeGarages.Count; index++)
            if (activeGarages[index] != null && activeGarages[index].specification != null)
                garageId = Mathf.Max(garageId, activeGarages[index].specification.id + 1);

        var storedIndexes = new[]
        {
            frontCar.LevelEditSourceIndex,
            anchorCar.LevelEditSourceIndex
        };
        var queue = new[]
        {
            frontCar.PieceColor,
            anchorCar.PieceColor
        };
        var specification = new GarageSpec(
            garageId,
            targetRow,
            targetCol,
            queue,
            true,
            storedIndexes);

        StoreEditedCarInsideGarage(frontCar);
        StoreEditedCarInsideGarage(anchorCar);
        addedGarage = CreateActiveGarage(specification);
        UpdateCreatedGarageStoredPositions(addedGarage);
        return addedGarage != null && addedGarage.exposedPiece != null;
    }

    private static bool CanStoreCarInCreatedGarage(CarPuzzlePiece piece)
    {
        return piece != null
            && piece.LevelEditSourceIndex >= 0
            && piece.SourceGarageId < 0
            && piece.CellLength == 1
            && !piece.IsRevealCovered;
    }

    private void StoreEditedCarInsideGarage(CarPuzzlePiece piece)
    {
        boardPieces.Remove(piece);
        allPieces.Remove(piece);
        levelEditStoredPieces[piece.LevelEditSourceIndex] = piece;
        piece.gameObject.SetActive(false);
    }

    private List<CarPuzzlePiece> DeleteEditedGarage(ActiveGarage garage)
    {
        var restored = new List<CarPuzzlePiece>();
        if (garage == null || garage.specification == null) return restored;

        if (garage.exposedPiece != null)
        {
            boardPieces.Remove(garage.exposedPiece);
            allPieces.Remove(garage.exposedPiece);
            garage.exposedPiece.gameObject.SetActive(false);
            Destroy(garage.exposedPiece.gameObject);
            garage.exposedPiece = null;
        }

        if (garage.specification.isEditorCreated
            && garage.specification.storedPieceIndexes.Length == 2)
        {
            for (int storedIndex = 0; storedIndex < 2; storedIndex++)
            {
                int sourceIndex = garage.specification.storedPieceIndexes[storedIndex];
                if (!levelEditStoredPieces.TryGetValue(sourceIndex, out CarPuzzlePiece piece)
                    || piece == null)
                    continue;
                levelEditStoredPieces.Remove(sourceIndex);
                int row = storedIndex == 0
                    ? garage.specification.row + 1
                    : garage.specification.row;
                piece.SetEditedGridPosition(row, garage.specification.col);
                piece.gameObject.SetActive(true);
                piece.SetBoardPose(GetBoardPiecePosition(piece));
                boardPieces.Add(piece);
                allPieces.Add(piece);
                restored.Add(piece);
            }
        }

        if (garage.visual != null)
        {
            garage.visual.gameObject.SetActive(false);
            Destroy(garage.visual.gameObject);
            garage.visual = null;
        }
        activeGarages.Remove(garage);
        return restored;
    }

    private void UpdateCreatedGarageStoredPositions(ActiveGarage garage)
    {
        if (garage == null || garage.specification == null
            || !garage.specification.isEditorCreated
            || garage.specification.storedPieceIndexes.Length != 2)
            return;

        int frontIndex = garage.specification.storedPieceIndexes[0];
        if (levelEditStoredPieces.TryGetValue(frontIndex, out CarPuzzlePiece front)
            && front != null)
            front.SetEditedGridPosition(
                garage.specification.row + 1,
                garage.specification.col);

        int anchorIndex = garage.specification.storedPieceIndexes[1];
        if (levelEditStoredPieces.TryGetValue(anchorIndex, out CarPuzzlePiece anchor)
            && anchor != null)
            anchor.SetEditedGridPosition(
                garage.specification.row,
                garage.specification.col);
    }

    private bool TryMoveEditedGarage(
        ActiveGarage garage,
        int targetRow,
        int targetCol,
        out List<CarPuzzlePiece> movedCars)
    {
        movedCars = new List<CarPuzzlePiece>();
        if (garage == null || garage.specification == null || garage.exposedPiece == null)
            return false;
        if (targetRow < 0 || targetRow >= activeBoardSize - 1
            || targetCol < 0 || targetCol >= activeBoardSize)
            return false;

        int oldRow = garage.specification.row;
        int oldCol = garage.specification.col;
        if (targetRow == oldRow && targetCol == oldCol) return false;

        int targetFrontRow = targetRow + 1;
        if (IsEditedGarageFootprintCell(targetRow, targetCol, garage)
            || IsEditedGarageFootprintCell(targetFrontRow, targetCol, garage))
            return false;

        CarPuzzlePiece anchorCar = FindEditedStableOccupantAt(targetRow, targetCol);
        CarPuzzlePiece frontCar = FindEditedStableOccupantAt(targetFrontRow, targetCol);
        if (anchorCar == null || frontCar == null) return false;

        bool movingOneVerticalLimousine = anchorCar == frontCar
            && anchorCar.CellLength == 2
            && anchorCar.OccupiesCell(targetRow, targetCol)
            && anchorCar.OccupiesCell(targetFrontRow, targetCol);
        bool movingTwoSingleCars = anchorCar != frontCar
            && anchorCar.CellLength == 1
            && frontCar.CellLength == 1
            && anchorCar.Row == targetRow
            && anchorCar.Col == targetCol
            && frontCar.Row == targetFrontRow
            && frontCar.Col == targetCol;
        if (!movingOneVerticalLimousine && !movingTwoSingleCars) return false;
        garage.specification.row = targetRow;
        garage.specification.col = targetCol;
        UpdateCreatedGarageStoredPositions(garage);

        garage.exposedPiece.SetEditedGridPosition(targetFrontRow, targetCol);
        garage.exposedPiece.SetEditedDirection(ExitDirection.Down);
        garage.exposedPiece.SetBoardPose(GetBoardPiecePosition(garage.exposedPiece));

        if (movingOneVerticalLimousine)
        {
            int leadingRowOffset = anchorCar.Row - targetRow;
            anchorCar.SetEditedGridPosition(oldRow + leadingRowOffset, oldCol);
            anchorCar.SetBoardPose(GetBoardPiecePosition(anchorCar));
        }
        else
        {
            anchorCar.SetEditedGridPosition(oldRow, oldCol);
            anchorCar.SetBoardPose(GetBoardPiecePosition(anchorCar));
            frontCar.SetEditedGridPosition(oldRow + 1, oldCol);
            frontCar.SetBoardPose(GetBoardPiecePosition(frontCar));
        }
        if (garage.visual != null)
            garage.visual.transform.position = GetBoardCellPosition(targetRow, targetCol)
                + new Vector3(0f, 0.04f, 0f);

        movedCars.Add(garage.exposedPiece);
        movedCars.Add(anchorCar);
        if (frontCar != anchorCar) movedCars.Add(frontCar);
        return true;
    }

    private bool IsEditedGarageFootprintCell(int row, int col, ActiveGarage excluded)
    {
        for (int index = 0; index < activeGarages.Count; index++)
        {
            ActiveGarage garage = activeGarages[index];
            if (garage == null || garage == excluded || garage.specification == null) continue;
            if (garage.specification.col != col) continue;
            if (garage.specification.row == row || garage.specification.row + 1 == row)
                return true;
        }
        return false;
    }

    private CarPuzzlePiece FindEditedStableOccupantAt(int row, int col)
    {
        CarPuzzlePiece found = null;
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece candidate = boardPieces[index];
            if (candidate == null || !candidate.OccupiesCell(row, col)) continue;
            if (candidate.LevelEditSourceIndex < 0) return null;
            if (found != null) return null;
            found = candidate;
        }
        return found;
    }

    private IEnumerator PulseEditedGarage(ActiveGarage garage, float duration)
    {
        if (garage == null || garage.visual == null) yield break;
        Transform visual = garage.visual.transform;
        Vector3 baseScale = visual.localScale;
        float elapsed = 0f;
        while (visual != null && elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float pulse = 1f + Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * 0.10f;
            visual.localScale = baseScale * pulse;
            yield return null;
        }
        if (visual != null) visual.localScale = baseScale;
    }

    private void SelectEditedBox(CarPuzzlePiece piece)
    {
        if (piece == null || !boardPieces.Contains(piece)) return;

        if (levelEditBoxSelection == null)
        {
            if (!CanEditBoxOnPiece(piece))
            {
                if (hud != null) hud.ShowLevelEditBoxRequired();
                piece.Reject();
                CarPrototypeFeedback.Error();
                return;
            }

            if (piece.IsRevealCovered)
            {
                levelEditBoxSelection = piece;
                StartCoroutine(piece.PulseTowSelection(0.52f));
                if (hud != null) hud.ShowLevelEditBoxSelection();
                CarPrototypeFeedback.ButtonTap();
                return;
            }

            if (!piece.SetEditedRevealBox(
                    true,
                    BoardNumber == 50))
            {
                piece.Reject();
                if (hud != null) hud.ShowLevelEditInvalidBoxAdd();
                CarPrototypeFeedback.Error();
                return;
            }
            piece.SetMysteryBoxRevealVfx(matchVfx);
            levelEditLastBoxVisualRotation = piece.GetEditedRevealBoxWorldRotation();
            levelEditHasBoxVisualRotation = true;
            StartCoroutine(piece.PulseTowSelection(0.52f));
            if (hud != null) hud.ShowLevelEditBoxAdded();
            CarPrototypeFeedback.ButtonTap();
            RefreshHud();
            return;
        }

        if (piece == levelEditBoxSelection)
        {
            levelEditLastBoxVisualRotation = piece.GetEditedRevealBoxWorldRotation();
            levelEditHasBoxVisualRotation = true;
            piece.SetEditedRevealBox(false);
            levelEditBoxSelection = null;
            StartCoroutine(piece.PulseTowSelection(0.42f));
            if (hud != null) hud.ShowLevelEditBoxDeleted();
            CarPrototypeFeedback.ButtonTap();
            RefreshHud();
            return;
        }

        if (piece.IsRevealCovered && CanEditBoxOnPiece(piece))
        {
            levelEditBoxSelection = piece;
            StartCoroutine(piece.PulseTowSelection(0.52f));
            if (hud != null) hud.ShowLevelEditBoxSelection();
            CarPrototypeFeedback.ButtonTap();
            return;
        }

        CarPuzzlePiece source = levelEditBoxSelection;
        if (!CanEditBoxOnPiece(piece) || piece.IsRevealCovered)
        {
            piece.Reject();
            if (hud != null) hud.ShowLevelEditInvalidBoxMove();
            CarPrototypeFeedback.Error();
            return;
        }

        Quaternion visualRotation = source.GetEditedRevealBoxWorldRotation();
        levelEditLastBoxVisualRotation = visualRotation;
        levelEditHasBoxVisualRotation = true;
        source.SetEditedRevealBox(false);
        if (!piece.SetEditedRevealBox(
                true,
                BoardNumber == 50))
        {
            source.SetEditedRevealBox(
                true,
                BoardNumber == 50);
            if (hud != null) hud.ShowLevelEditInvalidBoxMove();
            CarPrototypeFeedback.Error();
            return;
        }

        piece.SetMysteryBoxRevealVfx(matchVfx);
        levelEditBoxSelection = null;
        StartCoroutine(source.PulseTowSelection(0.38f));
        StartCoroutine(piece.PulseTowSelection(0.48f));
        if (hud != null) hud.ShowLevelEditBoxMoveComplete();
        CarPrototypeFeedback.ButtonTap();
        RefreshHud();
    }

    private static bool CanEditBoxOnPiece(CarPuzzlePiece piece)
    {
        return piece != null
            && piece.LevelEditSourceIndex >= 0
            && piece.SourceGarageId < 0
            && !piece.IsTrash
            && piece.CellLength == 1;
    }

    private void SelectEditedPieceForSwap(CarPuzzlePiece piece)
    {
        if (piece == null || !boardPieces.Contains(piece)) return;
        if (piece.LevelEditSourceIndex < 0)
        {
            piece.Reject();
            if (hud != null) hud.ShowLevelEditInvalidSwap();
            CarPrototypeFeedback.Error();
            return;
        }

        if (levelEditSwapSelection == null)
        {
            levelEditSwapSelection = piece;
            StartCoroutine(piece.PulseTowSelection(0.55f));
            if (hud != null) hud.ShowLevelEditSwapSelection();
            CarPrototypeFeedback.ButtonTap();
            return;
        }

        if (levelEditSwapSelection == piece)
        {
            levelEditSwapSelection = null;
            if (hud != null) hud.ShowLevelEditModeInstruction();
            CarPrototypeFeedback.ButtonTap();
            return;
        }

        CarPuzzlePiece first = levelEditSwapSelection;
        levelEditSwapSelection = null;
        if (!TrySwapEditedPieces(first, piece, out List<CarPuzzlePiece> swappedPieces))
        {
            first.Reject();
            piece.Reject();
            if (hud != null) hud.ShowLevelEditInvalidSwap();
            CarPrototypeFeedback.Error();
            return;
        }

        for (int index = 0; index < swappedPieces.Count; index++)
        {
            CarPuzzlePiece swapped = swappedPieces[index];
            if (swapped != null) StartCoroutine(swapped.PulseTowSelection(0.45f));
        }
        if (hud != null) hud.ShowLevelEditSwapComplete();
        CarPrototypeFeedback.ButtonTap();
        RefreshHud();
    }

    private bool TrySwapEditedPieces(
        CarPuzzlePiece first,
        CarPuzzlePiece second,
        out List<CarPuzzlePiece> swappedPieces)
    {
        swappedPieces = new List<CarPuzzlePiece>();
        if (first == null || second == null || first == second) return false;

        if (first.CellLength == 1 && second.CellLength == 1)
        {
            int firstRow = first.Row;
            int firstCol = first.Col;
            first.SetEditedGridPosition(second.Row, second.Col);
            second.SetEditedGridPosition(firstRow, firstCol);
            first.SetBoardPose(GetBoardPiecePosition(first));
            second.SetBoardPose(GetBoardPiecePosition(second));
            swappedPieces.Add(first);
            swappedPieces.Add(second);
            return true;
        }

        if (first.CellLength > 1 && second.CellLength > 1)
        {
            if (first.CellLength != second.CellLength) return false;
            SwapEditedLongCarFootprints(first, second);
            swappedPieces.Add(first);
            swappedPieces.Add(second);
            return true;
        }

        CarPuzzlePiece limousine = first.CellLength > 1 ? first : second;
        CarPuzzlePiece tappedSingle = first.CellLength == 1 ? first : second;
        return TrySwapEditedLimousineWithPair(limousine, tappedSingle, swappedPieces);
    }

    private void SwapEditedLongCarFootprints(CarPuzzlePiece first, CarPuzzlePiece second)
    {
        int firstRow = first.Row;
        int firstCol = first.Col;
        ExitDirection firstDirection = first.Direction;
        int secondRow = second.Row;
        int secondCol = second.Col;
        ExitDirection secondDirection = second.Direction;

        // Each limousine takes the other one's complete authored footprint.
        // Taking the destination direction as well lets horizontal and vertical
        // limousines exchange places without overlaps or empty grid cells.
        first.SetEditedGridPosition(secondRow, secondCol);
        first.SetEditedDirection(secondDirection);
        second.SetEditedGridPosition(firstRow, firstCol);
        second.SetEditedDirection(firstDirection);
        first.SetBoardPose(GetBoardPiecePosition(first));
        second.SetBoardPose(GetBoardPiecePosition(second));
    }

    private bool TrySwapEditedLimousineWithPair(
        CarPuzzlePiece limousine,
        CarPuzzlePiece tappedSingle,
        List<CarPuzzlePiece> swappedPieces)
    {
        if (limousine == null || tappedSingle == null
            || limousine.CellLength != 2 || tappedSingle.CellLength != 1)
            return false;

        Vector2Int step = DirectionToGridStep(limousine.Direction);
        int targetLeadingRow = tappedSingle.Row;
        int targetLeadingCol = tappedSingle.Col;
        int neighborRow = targetLeadingRow - step.y;
        int neighborCol = targetLeadingCol - step.x;
        CarPuzzlePiece neighbor = FindEditedSingleAt(neighborRow, neighborCol, tappedSingle);

        if (neighbor == null)
        {
            // The tapped car may be the trailing half of the destination pair.
            targetLeadingRow = tappedSingle.Row + step.y;
            targetLeadingCol = tappedSingle.Col + step.x;
            neighbor = FindEditedSingleAt(targetLeadingRow, targetLeadingCol, tappedSingle);
        }
        if (neighbor == null) return false;

        int oldLeadingRow = limousine.Row;
        int oldLeadingCol = limousine.Col;
        int oldTrailingRow = oldLeadingRow - step.y;
        int oldTrailingCol = oldLeadingCol - step.x;

        limousine.SetEditedGridPosition(targetLeadingRow, targetLeadingCol);
        tappedSingle.SetEditedGridPosition(oldLeadingRow, oldLeadingCol);
        neighbor.SetEditedGridPosition(oldTrailingRow, oldTrailingCol);
        limousine.SetBoardPose(GetBoardPiecePosition(limousine));
        tappedSingle.SetBoardPose(GetBoardPiecePosition(tappedSingle));
        neighbor.SetBoardPose(GetBoardPiecePosition(neighbor));
        swappedPieces.Add(limousine);
        swappedPieces.Add(tappedSingle);
        swappedPieces.Add(neighbor);
        return true;
    }

    private CarPuzzlePiece FindEditedSingleAt(int row, int col, CarPuzzlePiece excluded)
    {
        if (row < 0 || row >= activeBoardSize || col < 0 || col >= activeBoardSize)
            return null;

        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece candidate = boardPieces[index];
            if (candidate == null || candidate == excluded || candidate.CellLength != 1
                || candidate.LevelEditSourceIndex < 0)
                continue;
            if (candidate.Row == row && candidate.Col == col) return candidate;
        }
        return null;
    }

    private void RotateEditedPiece(CarPuzzlePiece piece)
    {
        if (piece == null || !boardPieces.Contains(piece)) return;
        if (piece.LevelEditSourceIndex < 0)
        {
            piece.Reject();
            CarPrototypeFeedback.Error();
            return;
        }

        int originalRow = piece.Row;
        int originalCol = piece.Col;
        Vector2Int originalStep = DirectionToGridStep(piece.Direction);
        int originalTrailingRow = originalRow - originalStep.y * (piece.CellLength - 1);
        int originalTrailingCol = originalCol - originalStep.x * (piece.CellLength - 1);
        ExitDirection candidate = piece.Direction;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            candidate = NextClockwiseDirection(candidate);
            int candidateRow = originalRow;
            int candidateCol = originalCol;

            // Reversing a limousine must move its leading cell to the other
            // end. That changes which way it faces while preserving the exact
            // same two occupied cells on a completely full board.
            if (piece.CellLength > 1 && candidate == OppositeDirection(piece.Direction))
            {
                candidateRow = originalTrailingRow;
                candidateCol = originalTrailingCol;
            }

            bool fits = CanUseEditedPlacement(piece, candidate, candidateRow, candidateCol);
            if (!fits && piece.CellLength > 1 && candidate != OppositeDirection(piece.Direction))
            {
                // A 90-degree turn may fit by pivoting around the limousine's
                // other endpoint instead of its current leading cell.
                candidateRow = originalTrailingRow;
                candidateCol = originalTrailingCol;
                fits = CanUseEditedPlacement(piece, candidate, candidateRow, candidateCol);
            }
            if (!fits) continue;

            piece.SetEditedGridPosition(candidateRow, candidateCol);
            piece.SetEditedDirection(candidate);
            piece.SetBoardPose(GetBoardPiecePosition(piece));
            if (hud != null) hud.ClearLevelEditResult();
            CarPrototypeFeedback.ButtonTap();
            RefreshHud();
            return;
        }

        piece.Reject();
        CarPrototypeFeedback.Error();
    }

    private bool CanUseEditedPlacement(
        CarPuzzlePiece piece,
        ExitDirection direction,
        int leadingRow,
        int leadingCol)
    {
        Vector2Int step = DirectionToGridStep(direction);
        for (int offset = 0; offset < piece.CellLength; offset++)
        {
            int row = leadingRow - step.y * offset;
            int col = leadingCol - step.x * offset;
            if (row < 0 || row >= activeBoardSize || col < 0 || col >= activeBoardSize) return false;
            for (int garageIndex = 0; garageIndex < activeGarages.Count; garageIndex++)
            {
                ActiveGarage garage = activeGarages[garageIndex];
                if (garage != null && garage.specification.row == row
                    && garage.specification.col == col)
                    return false;
            }

            for (int index = 0; index < boardPieces.Count; index++)
            {
                CarPuzzlePiece blocker = boardPieces[index];
                if (blocker != piece && blocker.OccupiesCell(row, col)) return false;
            }
        }

        return true;
    }

    private static ExitDirection OppositeDirection(ExitDirection direction)
    {
        switch (direction)
        {
            case ExitDirection.Up: return ExitDirection.Down;
            case ExitDirection.Down: return ExitDirection.Up;
            case ExitDirection.Left: return ExitDirection.Right;
            default: return ExitDirection.Left;
        }
    }

    private static ExitDirection NextClockwiseDirection(ExitDirection direction)
    {
        switch (direction)
        {
            case ExitDirection.Up: return ExitDirection.Right;
            case ExitDirection.Right: return ExitDirection.Down;
            case ExitDirection.Down: return ExitDirection.Left;
            default: return ExitDirection.Up;
        }
    }

    private void UpdateDirectionArrows()
    {
        // Direction is core information on onboarding boards. Tutorial cars
        // must retain their arrows even if the optional global arrow display
        // has been switched off elsewhere.
        bool tutorialForcesArrows = !isDemoMode
            && BoardNumber >= 1
            && BoardNumber <= TutorialLevelCount;
        for (int index = 0; index < allPieces.Count; index++)
        {
            CarPuzzlePiece piece = allPieces[index];
            if (piece == null) continue;
            piece.SetDirectionArrowVisible(
                (tutorialForcesArrows || directionArrowsEnabled)
                && !piece.IsTrash
                && boardPieces.Contains(piece));
        }
    }

    private void EvaluateAvailableMoves()
    {
        if (levelEditMode || isEvaluatingAvailableMoves || outcomeResolved || (EnableHeartSystem && hearts <= 0) || HasCompletedAllColorGoals())
            return;
        if (isAnimating || isClearingTrayMatch || boardCarsInTransit > 0
            || garageTransitionsInProgress > 0 || queuedBoardPieces.Count > 0)
            return;

        isEvaluatingAvailableMoves = true;
        bool hasClearBoardExit = false;
        for (int index = 0; index < boardPieces.Count; index++)
        {
            CarPuzzlePiece piece = boardPieces[index];
            if (piece == null || piece.IsLocked || piece.IsRevealCovered || !IsExitPathClear(piece)) continue;

            hasClearBoardExit = true;
            if (piece.IsTrash || trayPieces.Count < trayCapacity)
            {
                isEvaluatingAvailableMoves = false;
                return;
            }

            if (TryGetFullTrayDominantExchange(
                    piece,
                    out CarPuzzlePiece _,
                    out int _))
            {
                isEvaluatingAvailableMoves = false;
                return;
            }
        }

        // An empty parking bay can free one tray slot for a car that already has
        // a clear road exit. A filled bay is useful only when it can return to an
        // open slot or make a tray match through the game's validated swap rule.
        if (CanEnterSideParking())
        {
            if (parkedPiece == null && trayPieces.Count > 0 && hasClearBoardExit)
            {
                isEvaluatingAvailableMoves = false;
                return;
            }

            if (parkedPiece != null
                && (trayPieces.Count < trayCapacity || FindTraySwapIndexForParkingMatch() >= 0))
            {
                isEvaluatingAvailableMoves = false;
                return;
            }
        }
        else if (parkedPiece != null && trayPieces.Count < trayCapacity)
        {
            isEvaluatingAvailableMoves = false;
            return;
        }

        isEvaluatingAvailableMoves = false;
        // Towing is a remedy for a full tray, not for a board whose traffic is
        // intrinsically locked. Only offer it when freeing one tray bay creates
        // a real legal board move immediately.
        if (!towRescueUsed
            && hasClearBoardExit
            && trayPieces.Count >= trayCapacity)
        {
            // Do not offer the booster unless its tow-truck solver has found
            // a real set of tray cars that restores a playable next move.
            if (!trafficJamPopupOpen
                && hud != null
                && FindMinimalSolvableRescueSet() != null)
            {
                trafficJamPopupOpen = true;
                hud.ShowTrafficJam();
            }
            return;
        }

        outcomeResolved = true;
        if (hud != null) hud.ShowDefeat(false);
    }

    private bool CanEnterSideParking()
    {
        return IsSideParkingAvailable()
            && (activeExperimentalRules == null
            || activeExperimentalRules.parkingUseLimit < 0
            || parkingUses < activeExperimentalRules.parkingUseLimit);
    }

    private void RegisterParkingEntry()
    {
        if (activeExperimentalRules != null && activeExperimentalRules.parkingUseLimit > 0)
            parkingUses++;
    }

    private bool IsColorCleared(PieceColor color)
    {
        switch (color)
        {
            case PieceColor.Red: return activeRedTarget == 0 || redCleared >= activeRedTarget;
            case PieceColor.Green: return activeGreenTarget == 0 || greenCleared >= activeGreenTarget;
            case PieceColor.Blue: return activeBlueTarget == 0 || blueCleared >= activeBlueTarget;
            case PieceColor.Purple: return activePurpleTarget == 0 || purpleCleared >= activePurpleTarget;
            case PieceColor.Yellow: return activeYellowTarget == 0 || yellowCleared >= activeYellowTarget;
            case PieceColor.Pink: return activePinkTarget == 0 || pinkCleared >= activePinkTarget;
            default: return false;
        }
    }

    private bool TryGetNextOrderedColor(out PieceColor color)
    {
        color = PieceColor.Trash;
        if (activeExperimentalRules == null || activeExperimentalRules.requiredColorOrder.Length == 0)
            return false;

        for (int index = 0; index < activeExperimentalRules.requiredColorOrder.Length; index++)
        {
            PieceColor candidate = activeExperimentalRules.requiredColorOrder[index];
            if (IsColorCleared(candidate)) continue;
            color = candidate;
            return true;
        }

        return false;
    }

    private void ApplyExperimentalLocks()
    {
        if (activeExperimentalRules == null) return;

        for (int ruleIndex = 0; ruleIndex < activeExperimentalRules.locks.Length; ruleIndex++)
        {
            ExperimentalLockRule rule = activeExperimentalRules.locks[ruleIndex];
            CarPuzzlePiece lockedPiece = null;
            for (int pieceIndex = 0; pieceIndex < boardPieces.Count; pieceIndex++)
            {
                CarPuzzlePiece candidate = boardPieces[pieceIndex];
                if (candidate.IsTrash || !candidate.OccupiesCell(rule.row, rule.col)) continue;
                lockedPiece = candidate;
                break;
            }

            if (lockedPiece == null)
            {
                Debug.LogWarning($"Temporary Level {BoardNumber} could not find its lock at {rule.row},{rule.col}.");
                continue;
            }

            activeExperimentalLocks.Add(new ActiveExperimentalLock(lockedPiece, rule.unlockAfterColor));
            lockedPiece.SetLocked(!IsColorCleared(rule.unlockAfterColor), rule.unlockAfterColor);
        }
    }

    private void UpdateExperimentalLocks()
    {
        for (int index = 0; index < activeExperimentalLocks.Count; index++)
        {
            ActiveExperimentalLock activeLock = activeExperimentalLocks[index];
            if (activeLock.piece != null)
                activeLock.piece.SetLocked(!IsColorCleared(activeLock.unlockAfterColor), activeLock.unlockAfterColor);
        }
    }

    private string BuildExperimentalRuleStatus()
    {
        if (activeExperimentalRules == null) return string.Empty;

        string status = activeExperimentalRules.planningHint;
        if (TryGetNextOrderedColor(out PieceColor nextColor))
            status = $"NEXT: {ColorName(nextColor)}";

        if (activeExperimentalRules.parkingUseLimit > 0)
        {
            int remaining = Mathf.Max(0, activeExperimentalRules.parkingUseLimit - parkingUses);
            status += $"   PARK: {remaining}/{activeExperimentalRules.parkingUseLimit}";
        }

        int lockedCount = 0;
        for (int index = 0; index < activeExperimentalLocks.Count; index++)
        {
            if (activeExperimentalLocks[index].piece != null && activeExperimentalLocks[index].piece.IsLocked)
                lockedCount++;
        }
        if (lockedCount > 0) status += $"   LOCKED: {lockedCount}";
        return $"SAMPLE • {activeExperimentalRules.title}\n{status}";
    }

    private static string ColorName(PieceColor color)
    {
        return color == PieceColor.Trash
            ? "POLICE"
            : color.ToString().ToUpperInvariant();
    }

    private static ExperimentalRuleSet CreateExperimentalRules(int boardNumber)
    {
        if (boardNumber <= CampaignLevelCount) return null;
        switch (boardNumber)
        {
            case 101:
                return new ExperimentalRuleSet("MYSTERY BOXES", "MOVE THE FRONT CAR TO REVEAL");
            case 102:
                return new ExperimentalRuleSet("THE LONG WAY OUT", "TRACE THE EXIT CHAIN");
            case 103:
                return new ExperimentalRuleSet("FOUR IS NOT FIVE", "FIND ALL 5 BEFORE COMMITTING");
            case 104:
                return new ExperimentalRuleSet("VALET EXCHANGE", "PLAN EACH SIDE-BAY MOVE", 5);
            case 105:
                return new ExperimentalRuleSet(
                    "KEY COLOR",
                    "CLEAR GREEN TO UNLOCK RED",
                    locks: new[] { new ExperimentalLockRule(3, 2, PieceColor.Green) });
            case 106:
                return new ExperimentalRuleSet(
                    "TRAFFIC SCHEDULE",
                    "FOLLOW THE COLOR ORDER",
                    requiredColorOrder: new[] { PieceColor.Red, PieceColor.Green, PieceColor.Blue, PieceColor.Yellow });
            case 107:
                return new ExperimentalRuleSet(
                    "RUSH-HOUR FINALE",
                    "COMBINE EVERY RULE",
                    4,
                    new[] { PieceColor.Green, PieceColor.Red, PieceColor.Yellow, PieceColor.Blue },
                    new[]
                    {
                        new ExperimentalLockRule(0, 2, PieceColor.Green),
                        new ExperimentalLockRule(3, 4, PieceColor.Red)
                    });
            default:
                return null;
        }
    }

    private Vector3 GetBoardCellPosition(int row, int col)
    {
        float spacing = GetBoardSpacing();
        float center = (activeBoardSize - 1) * 0.5f;
        return new Vector3((col - center) * spacing, 0.35f, GetBoardFirstRowZ() - row * spacing);
    }

    private float GetBoardSpacing()
    {
        if (activeBoardSize >= 5) return 1.30f;
        return activeBoardSize >= 4 ? 1.55f : 1.92f;
    }

    private Vector3 GetBoardPiecePosition(CarPuzzlePiece piece)
    {
        return GetBoardPiecePosition(piece.Row, piece.Col, piece.Direction, piece.CellLength);
    }

    private Vector3 GetBoardPiecePosition(int leadingRow, int leadingCol, ExitDirection direction, int cellLength)
    {
        Vector3 leadingPosition = GetBoardCellPosition(leadingRow, leadingCol);
        if (cellLength <= 1) return leadingPosition;

        Vector2Int step = DirectionToGridStep(direction);
        int trailingRow = leadingRow - step.y * (cellLength - 1);
        int trailingCol = leadingCol - step.x * (cellLength - 1);
        Vector3 trailingPosition = GetBoardCellPosition(trailingRow, trailingCol);
        return (leadingPosition + trailingPosition) * 0.5f;
    }

    private float GetBoardPieceScale()
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        float scale = activeBoardSize >= 5
            ? currentLayout.scenePieceScale5x5
            : activeBoardSize >= 4 ? currentLayout.scenePieceScale4x4 : currentLayout.scenePieceScale3x3;
        return Mathf.Clamp(scale, 0.3f, 1.2f);
    }

    private float GetOffBoardPieceScale(int cellLength)
    {
        if (cellLength > 1)
            return Mathf.Clamp(GetSceneLayout().sceneLimousineOffBoardScale, 0.25f, 0.9f);

        // CarPuzzlePiece applies the shared 0.68 tray/parking presentation
        // multiplier. Compensate here so a regular car's final visible size is
        // identical to its current 3x3, 4x4, or 5x5 board size.
        return Mathf.Clamp(GetBoardPieceScale() / 0.68f, 0.3f, 1.2f);
    }

    private Vector3 GetTraySlotPosition(int index)
    {
        return GetTraySlotPosition(index, trayCapacity);
    }

    private float GetTrayApproachZ()
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        float halfDepth = Mathf.Max(0.55f, Mathf.Abs(GetMatchTrayBaySize(trayCapacity).y)) * 0.5f;
        return currentLayout.sceneMatchTrayPosition.y + halfDepth + 0.45f;
    }

    private Vector3 GetTraySlotPosition(int index, int capacity)
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        float spacing = GetMatchTraySlotSpacing(capacity);
        float center = (capacity - 1) * 0.5f;
        float x = currentLayout.sceneMatchTrayPosition.x + (index - center) * spacing;
        return new Vector3(
            x,
            0.34f,
            currentLayout.sceneMatchTrayPosition.y);
    }

    private float GetMatchTraySlotSpacing(int capacity)
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        return Mathf.Max(
            0.5f,
            currentLayout.sceneMatchTraySlotSpacing * GetParkingGeometryScale());
    }

    private Vector2 GetMatchTrayBaySize(int capacity)
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        return currentLayout.sceneMatchTrayBaySize * GetParkingGeometryScale();
    }

    private float GetParkingGeometryScale()
    {
        CarPrototypeHudLayout currentLayout = GetSceneLayout();
        float referenceCarScale = Mathf.Max(0.3f, currentLayout.scenePieceScale4x4);
        // Demo 2 is the approved reference. Smaller 5x5 cars reduce every bay
        // dimension proportionally; the slightly larger 3x3 cars retain the
        // established early-level parking footprint.
        return Mathf.Clamp(GetBoardPieceScale() / referenceCarScale, 0.55f, 1f);
    }

    private Vector3 GetParkingSlotPosition()
    {
        Vector2 parkingPosition = GetSceneLayout().sceneSideParkingPosition;
        return new Vector3(parkingPosition.x, 0.34f, parkingPosition.y);
    }

    internal static Vector2Int DirectionToGridStep(ExitDirection direction)
    {
        switch (direction)
        {
            case ExitDirection.Up: return new Vector2Int(0, -1);
            case ExitDirection.Down: return new Vector2Int(0, 1);
            case ExitDirection.Left: return new Vector2Int(-1, 0);
            default: return new Vector2Int(1, 0);
        }
    }

    internal static Vector3 DirectionToWorld(ExitDirection direction)
    {
        switch (direction)
        {
            case ExitDirection.Up: return Vector3.forward;
            case ExitDirection.Down: return Vector3.back;
            case ExitDirection.Left: return Vector3.left;
            default: return Vector3.right;
        }
    }

    internal static Quaternion GetMechanicArtworkCameraRotation()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            // Unity's built-in Quad presents its local -Z face. Point that
            // face back at the game camera and keep image-up equal to screen-up.
            return Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
        }

        // Deterministic editor fallback matching the shipped prototype camera.
        Vector3 cameraForward = new Vector3(0f, -15.4f, 7.6f).normalized;
        Quaternion cameraRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
        return Quaternion.LookRotation(cameraForward, cameraRotation * Vector3.up);
    }

    internal static GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Color color)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);
        return box;
    }

    internal static Mesh GetSharedCubeMesh()
    {
        if (sharedCubeMesh == null)
            sharedCubeMesh = GetBuiltInPrimitiveMesh("Cube.fbx", PrimitiveType.Cube);
        return sharedCubeMesh;
    }

    internal static Mesh GetSharedCylinderMesh()
    {
        if (sharedCylinderMesh == null)
            sharedCylinderMesh = GetBuiltInPrimitiveMesh("Cylinder.fbx", PrimitiveType.Cylinder);
        return sharedCylinderMesh;
    }

    private static Mesh GetBuiltInPrimitiveMesh(string resourceName, PrimitiveType fallbackType)
    {
        Mesh mesh = Resources.GetBuiltinResource<Mesh>(resourceName);
        if (mesh != null) return mesh;

        GameObject temporary = GameObject.CreatePrimitive(fallbackType);
        temporary.name = "Temporary Shared Primitive Mesh Source";
        temporary.SetActive(false);
        mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying) Destroy(temporary);
        else DestroyImmediate(temporary);
        return mesh;
    }

    internal static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    internal static Material GetApprovedMechanicMaterial(string resourcePath)
    {
        Material cached = null;
        if (resourcePath == ApprovedMysteryBoxResourcePath) cached = approvedMysteryBoxMaterial;
        else if (resourcePath == Level38MysteryBoxResourcePath) cached = level38MysteryBoxMaterial;
        else if (resourcePath == ApprovedGarageOpenResourcePath) cached = approvedGarageOpenMaterial;
        else if (resourcePath == ApprovedGarageClosedResourcePath) cached = approvedGarageClosedMaterial;
        if (cached != null
            && cached.shader != null
            && cached.shader.isSupported
            && cached.shader.name == "Universal Render Pipeline/Unlit")
            return cached;

        // Also recover cleanly when Enter Play Mode Options retained one of
        // the old incompatible sprite materials across a script reload.
        if (cached != null)
        {
            if (Application.isPlaying) Object.Destroy(cached);
            else Object.DestroyImmediate(cached);
            if (resourcePath == ApprovedMysteryBoxResourcePath) approvedMysteryBoxMaterial = null;
            else if (resourcePath == Level38MysteryBoxResourcePath) level38MysteryBoxMaterial = null;
            else if (resourcePath == ApprovedGarageOpenResourcePath) approvedGarageOpenMaterial = null;
            else if (resourcePath == ApprovedGarageClosedResourcePath) approvedGarageClosedMaterial = null;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogError(
                $"Missing approved mechanic texture at Resources/{resourcePath}. "
                + "The mechanic artwork object will be hidden to prevent Unity's magenta error material.");
            return null;
        }

        // Load a serialized URP material so its shader is an explicit build
        // dependency. Shader.Find("Sprites/Default") produced Unity's magenta
        // error material under the project's Universal Render Pipeline.
        Material template = Resources.Load<Material>(ApprovedMechanicMaterialResourcePath);
        if (template == null || template.shader == null || !template.shader.isSupported)
        {
            Debug.LogError(
                $"Missing or unsupported URP mechanic material at Resources/"
                + $"{ApprovedMechanicMaterialResourcePath}.");
            return null;
        }

        Material material = new Material(template)
        {
            name = texture.name + " Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white,
            mainTexture = texture,
            renderQueue = (int)RenderQueue.Transparent
        };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        if (resourcePath == ApprovedMysteryBoxResourcePath) approvedMysteryBoxMaterial = material;
        else if (resourcePath == Level38MysteryBoxResourcePath) level38MysteryBoxMaterial = material;
        else if (resourcePath == ApprovedGarageOpenResourcePath) approvedGarageOpenMaterial = material;
        else if (resourcePath == ApprovedGarageClosedResourcePath) approvedGarageClosedMaterial = material;
        return material;
    }
}

public sealed class GaragePuzzleVisual : MonoBehaviour
{
    private Mesh closedRevealMesh;
    private Renderer openRenderer;
    private Renderer closedRenderer;
    private Transform closedArtwork;

    internal void Configure(Material openMaterial, Material closedMaterial, float boardScale)
    {
        transform.localScale = Vector3.one * boardScale;
        BoxCollider tapCollider = gameObject.GetComponent<BoxCollider>();
        if (tapCollider == null) tapCollider = gameObject.AddComponent<BoxCollider>();
        tapCollider.center = new Vector3(0f, 0.35f, 0f);
        tapCollider.size = new Vector3(1.75f, 1.15f, 1.9f);
        openRenderer = CreateArtworkQuad("Approved Open Garage", openMaterial, 30, false, out _);
        closedRenderer = CreateArtworkQuad("Approved Closed Garage", closedMaterial, 31, true, out closedArtwork);
        if (closedArtwork != null)
            closedRevealMesh = closedArtwork.GetComponent<MeshFilter>().sharedMesh;
        SetDoorProgress(0f);
    }

    private Renderer CreateArtworkQuad(
        string objectName,
        Material material,
        int sortingOrder,
        bool useRevealMesh,
        out Transform artworkTransform)
    {
        artworkTransform = null;
        if (material == null || material.mainTexture == null)
        {
            Debug.LogError($"Skipping {objectName}: its approved mechanic material or texture is missing.");
            return null;
        }
        GameObject artwork = new GameObject(objectName);
        artwork.transform.SetParent(transform, false);
        artwork.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        artwork.transform.rotation = CarPrototype3D.GetMechanicArtworkCameraRotation();
        float size = CarPrototype3D.MechanicArtworkDisplaySize;
        artwork.transform.localScale = new Vector3(size, size, 1f);
        MeshFilter filter = artwork.AddComponent<MeshFilter>();
        filter.sharedMesh = useRevealMesh
            ? CreateRevealQuadMesh()
            : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer renderer = artwork.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
        artworkTransform = artwork.transform;
        return renderer;
    }

    private static Mesh CreateRevealQuadMesh()
    {
        var mesh = new Mesh { name = "Garage Door Top-Down Reveal Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    internal void SetOpen()
    {
        SetDoorProgress(0f);
    }

    internal IEnumerator CloseDoor(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetDoorProgress(progress);
            yield return null;
        }
        SetDoorProgress(1f);
    }

    private void SetDoorProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (closedRenderer != null) closedRenderer.gameObject.SetActive(progress > 0.001f);
        if (closedRevealMesh == null) return;
        float bottom = 0.5f - progress;
        Vector3[] vertices = closedRevealMesh.vertices;
        vertices[0] = new Vector3(-0.5f, 0.5f, 0f);
        vertices[1] = new Vector3(0.5f, 0.5f, 0f);
        vertices[2] = new Vector3(-0.5f, bottom, 0f);
        vertices[3] = new Vector3(0.5f, bottom, 0f);
        closedRevealMesh.vertices = vertices;
        Vector2[] uv = closedRevealMesh.uv;
        uv[0] = new Vector2(0f, 1f);
        uv[1] = new Vector2(1f, 1f);
        uv[2] = new Vector2(0f, 1f - progress);
        uv[3] = new Vector2(1f, 1f - progress);
        closedRevealMesh.uv = uv;
        closedRevealMesh.RecalculateBounds();
    }
}

public sealed class CarPuzzlePiece : MonoBehaviour
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private const string ImportedPoliceCarResourcePath =
        "CarModels/PoliceCar/policecar_blend";
    private const string ImportedPoliceCarTexturePath = "CarModels/PoliceCar/carPolice";
    private const string ImportedPoliceCarBodyTexturePath =
        "CarModels/PoliceCar/Purple_Coated_Glitter-color";
    private const string ImportedPoliceCarGlassTexturePath =
        "CarModels/PoliceCar/Material.004-color";
    private const string ImportedPoliceCarTrimTexturePath =
        "CarModels/PoliceCar/Material-color";
    private const string ImportedPoliceCarDetailTexturePath =
        "CarModels/PoliceCar/Material.005-color";
    private const string ImportedPurpleCarResourcePath = "CarModels/PurpleCar/purplecarrpro_7";
    private const string ImportedPurpleCarBodyTexturePath =
        "CarModels/PurpleCar/Purple_Coated_Glitter-color";
    private const string ImportedPurpleCarGlassTexturePath =
        "CarModels/PurpleCar/Material.004-color";
    private const string ImportedPurpleCarTrimTexturePath =
        "CarModels/PurpleCar/Material-color";
    private const string ImportedPurpleCarDetailTexturePath =
        "CarModels/PurpleCar/Material.005-color";
    private const string ImportedStandardCarResourcePath = "CarModels/YellowCar/yellowcar_final";
    private const string ImportedStandardCarYellowTexturePath = "CarModels/YellowCar/Material-color";
    private const string ImportedStandardCarRedTexturePath = "CarModels/YellowCar/Material-color-red";
    private const string ImportedStandardCarGreenTexturePath = "CarModels/YellowCar/Material-color-green";
    private const string ImportedStandardCarBlueTexturePath = "CarModels/YellowCar/Material-color-blue";
    private const string ImportedStandardCarPurpleTexturePath = "CarModels/YellowCar/Material-color-purple";
    // Retained only by the dormant pre-unification helpers below. Runtime
    // standard cars no longer load these Blue Car variants.
    private const string ImportedRedCarTexturePath = "CarModels/BlueCar/Material-color-red-from-blue";
    private const string ImportedGreenCarTexturePath = "CarModels/BlueCar/Material-color-green-from-yellow";
    private const string ImportedBlueCarResourcePath = "CarModels/BlueCar/bluecar";
    private const string ImportedBlueCarTexturePath = "CarModels/BlueCar/Material-color";
    private const string ImportedRedLimousineResourcePath =
        "CarModels/Limousines/redlimousine";
    private const string ImportedPurpleLimousineResourcePath =
        "CarModels/Limousines/purplelimousine";
    private const string ImportedYellowLimousineResourcePath =
        "CarModels/Limousines/yellowlimousine";
    private const string ImportedBlueLimousineResourcePath =
        "CarModels/Limousines/bluelimousine";
    // Pink deliberately reuses the authored red limousine body and UVs. Only
    // its baked paint atlas changes, leaving every other limousine untouched.
    private const string ImportedPinkLimousineResourcePath =
        ImportedRedLimousineResourcePath;
    // Green uses the same authored geometry and UV layout as the four supplied
    // limousine exports, with its own hue-shifted baked paint atlas.
    private const string ImportedGreenLimousineResourcePath =
        "CarModels/Limousines/bluelimousine";
    private const string ImportedRedLimousineTexturePath =
        "CarModels/Limousines/redlimousine_ColorMatched";
    private const string ImportedGreenLimousineTexturePath =
        "CarModels/Limousines/greenlimousine_ColorMatched";
    private const string ImportedPurpleLimousineTexturePath =
        "CarModels/Limousines/purplelimousine_ColorMatched";
    private const string ImportedYellowLimousineTexturePath =
        "CarModels/Limousines/yellowlimousine_ColorMatched";
    private const string ImportedBlueLimousineTexturePath =
        "CarModels/Limousines/bluelimousine_ColorMatched";
    private const string ImportedPinkLimousineTexturePath =
        "CarModels/Limousines/pinklimousine_ColorMatched";
    private const string GlobalMysteryBoxResourcePath =
        "CarPrototype/Mechanics/MysteryBox";
    private const string PremiumMysteryBoxBurstResourcePath =
        "CarPrototype/Mechanics/RevealVfx/PremiumMysteryBoxBurst";
    private const string PremiumMysteryBoxFragmentAtlasResourcePath =
        "CarPrototype/Mechanics/RevealVfx/PremiumMysteryBoxFragments";
    private const string PremiumMysteryBoxFragmentManifestResourcePath =
        "CarPrototype/Mechanics/RevealVfx/PremiumMysteryBoxFragments";
    private const string PremiumMysteryQuestionResourcePath =
        "CarPrototype/Mechanics/RevealVfx/PremiumMysteryQuestion";
    private const float PremiumMysteryDebrisRadiusScale = 0.65f;
    private const float PremiumMysteryOrbitDiameterScale = 0.72f;
    private const string Level35MysteryBoxResourcePath =
        "CarPrototype/Mechanics/Level35Box/BOXX";
    private const string Level38MysteryBoxResourcePath =
        "CarPrototype/Mechanics/Level38Box/Level38MysteryBox";
    private const string Level50MysteryBoxResourcePath =
        "CarPrototype/Mechanics/Level50Box/Level50MysteryBox";
    // Level 50 keeps its premium burst choreography, but every intact box and
    // every animated fragment now uses the same approved global artwork.
    private const string Level50MysteryBoxTexturePath = GlobalMysteryBoxResourcePath;

    private static GameObject importedPurpleCarPrefab;
    private static GameObject importedStandardCarPrefab;
    private static GameObject importedBlueCarPrefab;
    private static GameObject importedPoliceCarPrefab;
    private static GameObject importedRedLimousinePrefab;
    private static GameObject importedGreenLimousinePrefab;
    private static GameObject importedPurpleLimousinePrefab;
    private static GameObject importedYellowLimousinePrefab;
    private static GameObject importedBlueLimousinePrefab;
    private static GameObject importedPinkLimousinePrefab;
    private static GameObject level35MysteryBoxPrefab;
    private static GameObject level50MysteryBoxPrefab;
    private static Material importedStandardHeadlightMaterial;
    private static Mesh importedStandardHeadlightLensMesh;
    private static Material importedStandardSideWindowFrameMaterial;
    private static Mesh importedStandardSideWindowFrameMesh;
    private static Material importedRedCarMaterial;
    private static Material importedGreenCarMaterial;
    private static Material importedBlueCarMaterial;
    private static Material importedRedRearWindowFrameMaterial;
    private static Material importedRedRearWindowGlassMaterial;
    private static Mesh importedRedRearWindowFrameMesh;
    private static Mesh importedRedRearWindowGlassMesh;
    private static Material directionArrowMaterial;
    private static Texture2D directionArrowTexture;
    private static readonly Dictionary<string, Material> importedPurpleCarMaterials =
        new Dictionary<string, Material>();
    private static readonly Dictionary<string, Material> importedPinkCarMaterials =
        new Dictionary<string, Material>();
    private static readonly Dictionary<string, Material> importedPoliceCarMaterials =
        new Dictionary<string, Material>();
    private static readonly Dictionary<string, Material> importedStandardCarMaterials =
        new Dictionary<string, Material>();
    private static readonly Dictionary<string, Material> importedLimousineMaterials =
        new Dictionary<string, Material>();
    private static readonly Dictionary<CarPrototype3D.PieceColor, Texture2D> importedLimousineTextures =
        new Dictionary<CarPrototype3D.PieceColor, Texture2D>();
    private static readonly Dictionary<Color32, Material> sharedProceduralCarMaterials =
        new Dictionary<Color32, Material>();
    private static readonly Dictionary<string, Material> level35MysteryBoxMaterials =
        new Dictionary<string, Material>();
    private static readonly Dictionary<string, Material> level50MysteryBoxMaterials =
        new Dictionary<string, Material>();
    private static Texture2D level50RevealGlowTexture;
    private static Material premiumMysteryBoxBurstMaterial;
    private static Material premiumMysteryBoxFragmentMaterial;
    private static Material premiumMysteryBoxFragmentShadowMaterial;
    private static Material premiumMysteryQuestionMaterial;
    private static Level50FragmentAtlasManifest premiumMysteryBoxFragmentManifest;
    private readonly List<Transform> wheels = new List<Transform>();
    private readonly List<GameObject> lockVisuals = new List<GameObject>();
    private Vector3 visualBaseScale = Vector3.one;
    private float boardVisualScale = 1f;
    private float offBoardVisualScale = 1f;
    private bool usesImportedStandardCar;
    private bool usesImportedPurpleCar;
    private bool usesLimousineVisual;
    private bool useCartoonOutline;
    private static Material cartoonOutlineMaterial;
    private PoliceLightController policeLightController;
    private CarEyeController eyeController;
    private Collider rootCollider;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private MaterialPropertyBlock tutorialPropertyBlock;
    private bool tutorialDimmed;
    private GameObject directionArrowRoot;
    private GameObject revealCarVisualRoot;
    private GameObject revealBoxVisualRoot;
    private bool usesLevel35MysteryBoxVisual;
    private bool usesLevel50MysteryBoxVisual;
    private CarMatchVfx mysteryBoxRevealVfx;

    private sealed class Level50FragmentMotion
    {
        internal GameObject gameObject;
        internal Renderer renderer;
        internal Transform shadowTransform;
        internal Renderer shadowRenderer;
        internal Mesh runtimeMesh;
        internal Vector3 startPosition;
        internal Quaternion startRotation;
        internal Vector3 startScale;
        internal Vector3 moveDirection;
        internal Vector3 spinAxis;
        internal Color tint;
        internal float distance;
        internal float spinDegrees;
        internal float delay;
        internal float fadeStart;
        internal float fadeEnd;
        internal float shadowOffset;
    }

    [System.Serializable]
    private sealed class Level50FragmentAtlasManifest
    {
        public Level50FragmentAtlasEntry[] fragments;
    }

    [System.Serializable]
    private sealed class Level50FragmentAtlasEntry
    {
        public string name;
        public int atlasIndex;
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public float uvX;
        public float uvY;
        public float uvWidth;
        public float uvHeight;
    }

    internal int Row { get; private set; }
    internal int Col { get; private set; }
    internal int CellLength { get; private set; } = 1;
    internal bool IsTrash { get; private set; }
    internal CarPrototype3D.PieceColor PieceColor { get; private set; }
    internal CarPrototype3D.ExitDirection Direction { get; private set; }
    internal bool IsRevealCovered { get; private set; }
    internal int SourceGarageId { get; set; } = -1;
    internal int LevelEditSourceIndex { get; set; } = -1;
    internal bool IsTouchable => rootCollider != null && rootCollider.enabled && transform.localScale.sqrMagnitude > 0.0001f;
    internal bool IsLocked { get; private set; }

    internal void SetLocked(bool locked, CarPrototype3D.PieceColor unlockAfterColor)
    {
        if (lockVisuals.Count == 0) BuildLockBadge(unlockAfterColor);
        IsLocked = locked;
        for (int index = 0; index < lockVisuals.Count; index++)
            if (lockVisuals[index] != null) lockVisuals[index].SetActive(locked);
    }

    internal void SetEyesCanMove(bool canMove)
    {
        if (eyeController != null)
            eyeController.SetCanMove(canMove);
    }

    private void BuildLockBadge(CarPrototype3D.PieceColor unlockAfterColor)
    {
        Color keyColor;
        switch (unlockAfterColor)
        {
            case CarPrototype3D.PieceColor.Red: keyColor = new Color(0.96f, 0.13f, 0.16f); break;
            case CarPrototype3D.PieceColor.Green: keyColor = new Color(0.18f, 0.88f, 0.18f); break;
            case CarPrototype3D.PieceColor.Blue: keyColor = new Color(0.10f, 0.58f, 0.96f); break;
            case CarPrototype3D.PieceColor.Purple: keyColor = new Color(0.57f, 0.11f, 0.88f); break;
            default: keyColor = new Color(1f, 0.78f, 0.12f); break;
        }

        lockVisuals.Add(CreateLocalBox("Color Lock Body", new Vector3(0f, 1.16f, -0.08f), new Vector3(0.58f, 0.12f, 0.52f), keyColor));
        lockVisuals.Add(CreateLocalBox("Color Lock Left Shackle", new Vector3(-0.22f, 1.19f, 0.22f), new Vector3(0.10f, 0.13f, 0.35f), Color.white));
        lockVisuals.Add(CreateLocalBox("Color Lock Right Shackle", new Vector3(0.22f, 1.19f, 0.22f), new Vector3(0.10f, 0.13f, 0.35f), Color.white));
        lockVisuals.Add(CreateLocalBox("Color Lock Top Shackle", new Vector3(0f, 1.19f, 0.38f), new Vector3(0.52f, 0.13f, 0.10f), Color.white));
    }

    internal void Configure(
        int row,
        int col,
        CarPrototype3D.PieceColor pieceColor,
        CarPrototype3D.ExitDirection exitDirection,
        Vector3 position,
        float boardScale,
        float offBoardScale,
        int cellLength,
        bool isRevealBox = false,
        bool useCartoonOutline = false)
    {
        Row = row;
        Col = col;
        CellLength = Mathf.Max(1, cellLength);
        PieceColor = pieceColor;
        Direction = exitDirection;
        IsTrash = pieceColor == CarPrototype3D.PieceColor.Trash;
        IsRevealCovered = isRevealBox && !IsTrash;
        this.useCartoonOutline = useCartoonOutline;
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(CarPrototype3D.DirectionToWorld(exitDirection), Vector3.up);
        boardVisualScale = boardScale;
        offBoardVisualScale = offBoardScale;
        visualBaseScale = Vector3.one * boardVisualScale;
        transform.localScale = visualBaseScale;

        if (IsTrash)
        {
            if (!BuildImportedPoliceCar()) BuildDeliveryCrateCart();
        }
        else
        {
            if (CellLength > 1) BuildLimousine(pieceColor);
            else BuildCar(pieceColor);
            BuildAnimatedEyes();
            BuildDirectionArrow();
            if (IsRevealCovered)
            {
                WrapCarVisualsForReveal();
                BuildRevealBoxVisual();
            }
        }

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.size = IsTrash
            ? new Vector3(1.55f, 1.45f, 2.1f)
            : new Vector3(1.55f, 1.2f, CellLength > 1 ? 4.55f : 2.1f);
        collider.center = IsTrash ? new Vector3(0f, 0.5f, 0f) : new Vector3(0f, 0.42f, 0f);
        rootCollider = collider;
        CacheVisibilityComponents();
    }

    private void BuildCartoonOutline()
    {
        Material outline = GetCartoonOutlineMaterial();
        if (outline == null) return;

        // Clone only the completed car visual, before eyes and direction arrows
        // are added. Front-face culling leaves a slim black silhouette visible
        // around the original model, like a cartoon ink outline.
        var visualRoots = new List<Transform>();
        for (int index = 0; index < transform.childCount; index++)
            visualRoots.Add(transform.GetChild(index));

        for (int index = 0; index < visualRoots.Count; index++)
        {
            GameObject outlineRoot = Instantiate(visualRoots[index].gameObject, transform);
            outlineRoot.name = "Cartoon Outline " + visualRoots[index].name;
            outlineRoot.transform.localScale *= 1.075f;

            foreach (Renderer renderer in outlineRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex] = outline;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            foreach (Collider collider in outlineRoot.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
        }
    }

    private static Material GetCartoonOutlineMaterial()
    {
        if (cartoonOutlineMaterial != null) return cartoonOutlineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        cartoonOutlineMaterial = new Material(shader)
        {
            name = "Level 1 Cartoon Outline",
            color = new Color(0.025f, 0.035f, 0.06f, 1f),
            hideFlags = HideFlags.HideAndDontSave
        };
        if (cartoonOutlineMaterial.HasProperty("_BaseColor"))
            cartoonOutlineMaterial.SetColor("_BaseColor", new Color(0.025f, 0.035f, 0.06f, 1f));
        if (cartoonOutlineMaterial.HasProperty("_Smoothness")) cartoonOutlineMaterial.SetFloat("_Smoothness", 0.15f);
        if (cartoonOutlineMaterial.HasProperty("_Cull")) cartoonOutlineMaterial.SetFloat("_Cull", 1f);
        return cartoonOutlineMaterial;
    }

    private void WrapCarVisualsForReveal()
    {
        var children = new List<Transform>();
        for (int index = 0; index < transform.childCount; index++)
            children.Add(transform.GetChild(index));

        revealCarVisualRoot = new GameObject("Hidden Car Visual");
        revealCarVisualRoot.transform.SetParent(transform, false);
        for (int index = 0; index < children.Count; index++)
            children[index].SetParent(revealCarVisualRoot.transform, false);
        revealCarVisualRoot.SetActive(false);
    }

    private void BuildRevealBoxVisual(
        string resourcePath = GlobalMysteryBoxResourcePath,
        string artworkName = "Approved Mystery Box Artwork",
        float sizeMultiplier = 1.40f)
    {
        revealBoxVisualRoot = new GameObject("Mystery Box Visual");
        revealBoxVisualRoot.transform.SetParent(transform, false);
        revealBoxVisualRoot.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        // The sprite already contains the approved near-overhead perspective.
        // Present it square-on to the camera so Unity does not apply a second,
        // incorrect perspective squeeze when the hidden car faces another way.
        revealBoxVisualRoot.transform.rotation =
            CarPrototype3D.GetMechanicArtworkCameraRotation();

        GameObject artwork = GameObject.CreatePrimitive(PrimitiveType.Quad);
        artwork.name = artworkName;
        artwork.transform.SetParent(revealBoxVisualRoot.transform, false);
        artwork.transform.localRotation = Quaternion.identity;
        float size = CarPrototype3D.MechanicArtworkDisplaySize
            * Mathf.Max(0.1f, sizeMultiplier);
        artwork.transform.localScale = new Vector3(size, size, 1f);
        Collider collider = artwork.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
        Renderer renderer = artwork.GetComponent<Renderer>();
        Material artworkMaterial = CarPrototype3D.GetApprovedMechanicMaterial(
            resourcePath);
        if (artworkMaterial == null || artworkMaterial.mainTexture == null)
        {
            // Never leave an unmaterialed MeshRenderer active: Unity renders
            // that state as the exact magenta square reported by the player.
            artwork.SetActive(false);
            return;
        }
        Texture texture = artworkMaterial.mainTexture;
        float aspect = texture.height > 0
            ? (float)texture.width / texture.height
            : 1f;
        artwork.transform.localScale = new Vector3(size * aspect, size, 1f);
        renderer.sharedMaterial = artworkMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 45;
    }

    internal void UseLevel38MysteryBoxVisual()
    {
        UseGlobalMysteryBoxVisual();
    }

    internal void UseLevel35MysteryBoxVisual()
    {
        UseGlobalMysteryBoxVisual();
    }

    private void UseGlobalMysteryBoxVisual()
    {
        if (!IsRevealCovered) return;

        usesLevel35MysteryBoxVisual = false;
        usesLevel50MysteryBoxVisual = false;
        if (revealBoxVisualRoot != null)
        {
            revealBoxVisualRoot.SetActive(false);
            if (Application.isPlaying) Destroy(revealBoxVisualRoot);
            else DestroyImmediate(revealBoxVisualRoot);
            revealBoxVisualRoot = null;
        }
        BuildRevealBoxVisual();
        cachedRenderers = null;
        cachedColliders = null;
        CacheVisibilityComponents();
    }

    private bool BuildLevel35MysteryBoxVisual()
    {
        if (level35MysteryBoxPrefab == null)
            level35MysteryBoxPrefab = Resources.Load<GameObject>(Level35MysteryBoxResourcePath);
        if (level35MysteryBoxPrefab == null)
        {
            Debug.LogError("Missing the Level 35 BOXX.obj test model.");
            return false;
        }

        revealBoxVisualRoot = new GameObject("Level 35 Blender Mystery Box Visual");
        revealBoxVisualRoot.transform.SetParent(transform, false);
        revealBoxVisualRoot.transform.localPosition = Vector3.zero;
        revealBoxVisualRoot.transform.rotation = Quaternion.identity;

        GameObject model = Instantiate(level35MysteryBoxPrefab, revealBoxVisualRoot.transform, false);
        model.name = "Level 35 BOXX Blender Model";
        model.transform.localPosition = Vector3.zero;
        // The supplied OBJ is authored Z-up. Convert it to Unity's Y-up
        // coordinates without changing the geometry itself.
        model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
        {
            if (Application.isPlaying) Destroy(importedCamera.gameObject);
            else DestroyImmediate(importedCamera.gameObject);
        }
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
        {
            if (Application.isPlaying) Destroy(importedLight.gameObject);
            else DestroyImmediate(importedLight.gameObject);
        }
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(importedCollider);
            else DestroyImmediate(importedCollider);
        }

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return false;

        Material goldMaterial = GetLevel35MysteryBoxMaterial(
            "Gold",
            new Color(1f, 0.54f, 0.075f, 1f),
            0.38f,
            0.70f);
        Material woodMaterial = GetLevel35MysteryBoxMaterial(
            "Procedural Wood",
            new Color(0.70f, 0.18f, 0.03f, 1f),
            0.03f,
            0.38f);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            Material[] importedMaterials = renderer.sharedMaterials;
            var correctedMaterials = new Material[importedMaterials.Length];
            for (int materialIndex = 0; materialIndex < importedMaterials.Length; materialIndex++)
            {
                string importedName = importedMaterials[materialIndex] != null
                    ? importedMaterials[materialIndex].name
                    : string.Empty;
                correctedMaterials[materialIndex] =
                    importedName.IndexOf("gold", System.StringComparison.OrdinalIgnoreCase) >= 0
                        ? goldMaterial
                        : woodMaterial;
            }
            renderer.sharedMaterials = correctedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        if (TryGetLocalRenderBounds(renderers, out Bounds bounds))
        {
            const float targetFootprint = 1.72f;
            float widthScale = bounds.size.x > 0.001f ? targetFootprint / bounds.size.x : 1f;
            float depthScale = bounds.size.z > 0.001f ? targetFootprint / bounds.size.z : 1f;
            model.transform.localScale = Vector3.one * Mathf.Min(widthScale, depthScale);
            if (TryGetLocalRenderBounds(renderers, out bounds))
            {
                // Level 35 uses a four-column board (0.66 piece scale). The
                // piece origin is Y=0.35 and the asphalt surface is Y=0.02,
                // so a local bottom of -0.50 lands exactly on the ground:
                // 0.35 + (-0.50 * 0.66) = 0.02. The previous shared-car
                // offset (-0.22) left the tall box visibly floating.
                const float desiredBottom = -0.50f;
                model.transform.localPosition += new Vector3(
                    -bounds.center.x,
                    desiredBottom - bounds.min.y,
                    -bounds.center.z);
            }
        }

        return true;
    }

    private static Material GetLevel35MysteryBoxMaterial(
        string materialKey,
        Color color,
        float metallic,
        float smoothness)
    {
        if (level35MysteryBoxMaterials.TryGetValue(materialKey, out Material cached)
            && cached != null)
            return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader)
        {
            name = "Level 35 BOXX - " + materialKey,
            hideFlags = HideFlags.HideAndDontSave,
            color = color
        };
        material.enableInstancing = true;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        level35MysteryBoxMaterials[materialKey] = material;
        return material;
    }

    internal void UseLevel50MysteryBoxVisual()
    {
        if (!IsRevealCovered) return;

        // All levels share the same corrected 2D box. Level 50 only opts into
        // its existing premium reveal choreography; it no longer swaps the
        // intact artwork for a separate 3D model.
        usesLevel50MysteryBoxVisual = true;
        usesLevel35MysteryBoxVisual = false;
        if (revealBoxVisualRoot == null)
            BuildRevealBoxVisual();
        cachedRenderers = null;
        cachedColliders = null;
        CacheVisibilityComponents();
    }

    internal void SetMysteryBoxRevealVfx(CarMatchVfx revealVfx)
    {
        mysteryBoxRevealVfx = revealVfx;
    }

    private bool BuildLevel50MysteryBoxVisual()
    {
        if (level50MysteryBoxPrefab == null)
            level50MysteryBoxPrefab = Resources.Load<GameObject>(Level50MysteryBoxResourcePath);
        Texture2D correctTexture = Resources.Load<Texture2D>(Level50MysteryBoxTexturePath);
        if (level50MysteryBoxPrefab == null || correctTexture == null)
        {
            Debug.LogError(
                "Missing the Level 50 mystery-box FBX or its corrected projected texture.");
            return false;
        }

        revealBoxVisualRoot = new GameObject("Level 50 3D Mystery Box Visual");
        revealBoxVisualRoot.transform.SetParent(transform, false);
        revealBoxVisualRoot.transform.localPosition = Vector3.zero;
        revealBoxVisualRoot.transform.rotation = Quaternion.identity;

        GameObject model = Instantiate(level50MysteryBoxPrefab, revealBoxVisualRoot.transform, false);
        model.name = "Level 50 Correctly Textured 3D Mystery Box Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
        {
            if (Application.isPlaying) Destroy(importedCamera.gameObject);
            else DestroyImmediate(importedCamera.gameObject);
        }
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
        {
            if (Application.isPlaying) Destroy(importedLight.gameObject);
            else DestroyImmediate(importedLight.gameObject);
        }
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(importedCollider);
            else DestroyImmediate(importedCollider);
        }

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return false;
        Material projectedMaterial = GetLevel50MysteryBoxProjectedMaterial(correctTexture);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            renderer.sharedMaterial = projectedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        if (TryGetLocalRenderBounds(renderers, out Bounds bounds))
        {
            const float targetFootprint = 1.72f;
            float widthScale = bounds.size.x > 0.001f ? targetFootprint / bounds.size.x : 1f;
            float depthScale = bounds.size.z > 0.001f ? targetFootprint / bounds.size.z : 1f;
            model.transform.localScale = Vector3.one * Mathf.Min(widthScale, depthScale);
            if (TryGetLocalRenderBounds(renderers, out bounds))
            {
                const float desiredBottom = -0.22f;
                model.transform.localPosition += new Vector3(
                    -bounds.center.x,
                    desiredBottom - bounds.min.y,
                    -bounds.center.z);
            }
        }

        return true;
    }

    private static Material GetLevel50MysteryBoxProjectedMaterial(Texture2D texture)
    {
        const string materialKey = "Correct Projected Texture";
        if (level50MysteryBoxMaterials.TryGetValue(materialKey, out Material cached)
            && cached != null
            && cached.mainTexture == texture)
            return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        Material material = new Material(shader)
        {
            name = "Level 50 Box - Correct Projected Texture",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white,
            mainTexture = texture
        };
        material.enableInstancing = true;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        level50MysteryBoxMaterials[materialKey] = material;
        return material;
    }

    private static Material GetLevel50RevealVfxMaterial(string materialKey, Color color, bool additive)
    {
        string dictionaryKey = "Reveal VFX - " + materialKey;
        if (level50MysteryBoxMaterials.TryGetValue(dictionaryKey, out Material cached)
            && cached != null)
            return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) return null;

        Material material = new Material(shader)
        {
            name = "Level 50 Box - " + dictionaryKey,
            hideFlags = HideFlags.HideAndDontSave,
            color = color,
            mainTexture = GetLevel50RevealGlowTexture()
        };
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", GetLevel50RevealGlowTexture());
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        ConfigureLevel50TransparentMaterial(material, additive);
        level50MysteryBoxMaterials[dictionaryKey] = material;
        return material;
    }

    private static Material GetPremiumMysteryBoxBurstMaterial()
    {
        Texture2D texture = Resources.Load<Texture2D>(PremiumMysteryBoxBurstResourcePath);
        if (texture == null) return null;
        if (premiumMysteryBoxBurstMaterial != null
            && premiumMysteryBoxBurstMaterial.mainTexture == texture)
            return premiumMysteryBoxBurstMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) return null;
        premiumMysteryBoxBurstMaterial = new Material(shader)
        {
            name = "Approved Premium Mystery Box Burst",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white,
            mainTexture = texture
        };
        if (premiumMysteryBoxBurstMaterial.HasProperty("_BaseMap"))
            premiumMysteryBoxBurstMaterial.SetTexture("_BaseMap", texture);
        if (premiumMysteryBoxBurstMaterial.HasProperty("_BaseColor"))
            premiumMysteryBoxBurstMaterial.SetColor("_BaseColor", Color.white);
        if (premiumMysteryBoxBurstMaterial.HasProperty("_Color"))
            premiumMysteryBoxBurstMaterial.SetColor("_Color", Color.white);
        ConfigureLevel50TransparentMaterial(premiumMysteryBoxBurstMaterial, true);
        return premiumMysteryBoxBurstMaterial;
    }

    private static Material GetPremiumMysteryBoxFragmentMaterial()
    {
        Texture2D texture = Resources.Load<Texture2D>(
            PremiumMysteryBoxFragmentAtlasResourcePath);
        if (texture == null) return null;
        if (premiumMysteryBoxFragmentMaterial != null
            && premiumMysteryBoxFragmentMaterial.mainTexture == texture)
            return premiumMysteryBoxFragmentMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) return null;
        premiumMysteryBoxFragmentMaterial = new Material(shader)
        {
            name = "Premium Mystery Box Fragment Atlas",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white,
            mainTexture = texture
        };
        if (premiumMysteryBoxFragmentMaterial.HasProperty("_BaseMap"))
            premiumMysteryBoxFragmentMaterial.SetTexture("_BaseMap", texture);
        if (premiumMysteryBoxFragmentMaterial.HasProperty("_BaseColor"))
            premiumMysteryBoxFragmentMaterial.SetColor("_BaseColor", Color.white);
        if (premiumMysteryBoxFragmentMaterial.HasProperty("_Color"))
            premiumMysteryBoxFragmentMaterial.SetColor("_Color", Color.white);
        ConfigureLevel50TransparentMaterial(premiumMysteryBoxFragmentMaterial, false);
        return premiumMysteryBoxFragmentMaterial;
    }

    private static Material GetPremiumMysteryBoxFragmentShadowMaterial()
    {
        Texture2D texture = Resources.Load<Texture2D>(
            PremiumMysteryBoxFragmentAtlasResourcePath);
        if (texture == null) return null;
        if (premiumMysteryBoxFragmentShadowMaterial != null
            && premiumMysteryBoxFragmentShadowMaterial.mainTexture == texture)
            return premiumMysteryBoxFragmentShadowMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) return null;
        Color shadowColor = new Color(0.035f, 0.045f, 0.09f, 0.43f);
        premiumMysteryBoxFragmentShadowMaterial = new Material(shader)
        {
            name = "Premium Mystery Box Fragment Shadows",
            hideFlags = HideFlags.HideAndDontSave,
            color = shadowColor,
            mainTexture = texture
        };
        if (premiumMysteryBoxFragmentShadowMaterial.HasProperty("_BaseMap"))
            premiumMysteryBoxFragmentShadowMaterial.SetTexture("_BaseMap", texture);
        if (premiumMysteryBoxFragmentShadowMaterial.HasProperty("_BaseColor"))
            premiumMysteryBoxFragmentShadowMaterial.SetColor("_BaseColor", shadowColor);
        if (premiumMysteryBoxFragmentShadowMaterial.HasProperty("_Color"))
            premiumMysteryBoxFragmentShadowMaterial.SetColor("_Color", shadowColor);
        ConfigureLevel50TransparentMaterial(premiumMysteryBoxFragmentShadowMaterial, false);
        return premiumMysteryBoxFragmentShadowMaterial;
    }

    private static Material GetPremiumMysteryQuestionMaterial()
    {
        Texture2D texture = Resources.Load<Texture2D>(PremiumMysteryQuestionResourcePath);
        if (texture == null) return null;
        if (premiumMysteryQuestionMaterial != null
            && premiumMysteryQuestionMaterial.mainTexture == texture)
            return premiumMysteryQuestionMaterial;

        Material template = Resources.Load<Material>(
            "CarPrototype/Mechanics/MechanicArtworkMaterial");
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (template == null && fallbackShader == null)
            fallbackShader = Shader.Find("Unlit/Transparent");
        if (template == null && fallbackShader == null) return null;
        premiumMysteryQuestionMaterial = template != null
            ? new Material(template)
            : new Material(fallbackShader);
        premiumMysteryQuestionMaterial.name = "Premium Rising Mystery Question";
        premiumMysteryQuestionMaterial.hideFlags = HideFlags.HideAndDontSave;
        premiumMysteryQuestionMaterial.color = Color.white;
        premiumMysteryQuestionMaterial.mainTexture = texture;
        if (premiumMysteryQuestionMaterial.HasProperty("_BaseMap"))
            premiumMysteryQuestionMaterial.SetTexture("_BaseMap", texture);
        if (premiumMysteryQuestionMaterial.HasProperty("_BaseColor"))
            premiumMysteryQuestionMaterial.SetColor("_BaseColor", Color.white);
        if (premiumMysteryQuestionMaterial.HasProperty("_Color"))
            premiumMysteryQuestionMaterial.SetColor("_Color", Color.white);
        ConfigureLevel50TransparentMaterial(premiumMysteryQuestionMaterial, false);
        return premiumMysteryQuestionMaterial;
    }

    private static Level50FragmentAtlasManifest GetPremiumMysteryBoxFragmentManifest()
    {
        if (premiumMysteryBoxFragmentManifest != null
            && premiumMysteryBoxFragmentManifest.fragments != null
            && premiumMysteryBoxFragmentManifest.fragments.Length == 14)
            return premiumMysteryBoxFragmentManifest;

        TextAsset manifestAsset = Resources.Load<TextAsset>(
            PremiumMysteryBoxFragmentManifestResourcePath);
        if (manifestAsset == null) return null;
        premiumMysteryBoxFragmentManifest = JsonUtility.FromJson<Level50FragmentAtlasManifest>(
            manifestAsset.text);
        return premiumMysteryBoxFragmentManifest;
    }

    private static void ConfigureLevel50TransparentMaterial(Material material, bool additive)
    {
        if (material == null) return;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 1f : 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        material.doubleSidedGI = true;
        material.enableInstancing = true;
    }

    private static Texture2D GetLevel50RevealGlowTexture()
    {
        if (level50RevealGlowTexture != null) return level50RevealGlowTexture;

        const int size = 64;
        level50RevealGlowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Level 50 Reveal Soft Glow",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = ((x + 0.5f) / size) * 2f - 1f;
                float normalizedY = ((y + 0.5f) / size) * 2f - 1f;
                float radius = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                // Keep the shoulder broad: the reference impact reads as a
                // box-width warm-white bloom, not as a pin-point lens flare.
                float alpha = 1f - Mathf.SmoothStep(0.02f, 1f, radius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        level50RevealGlowTexture.SetPixels32(pixels);
        level50RevealGlowTexture.Apply(false, true);
        return level50RevealGlowTexture;
    }

    internal IEnumerator RevealFromBox()
    {
        if (!IsRevealCovered) yield break;
        if (rootCollider != null) rootCollider.enabled = false;

        if (revealBoxVisualRoot != null
            && revealCarVisualRoot != null)
        {
            yield return RevealFromPremiumMysteryBox();
            yield break;
        }

        if (revealBoxVisualRoot != null) revealBoxVisualRoot.SetActive(false);
        if (revealCarVisualRoot != null) revealCarVisualRoot.SetActive(true);
        IsRevealCovered = false;
        if (rootCollider != null) rootCollider.enabled = true;
        yield return null;
    }

    private IEnumerator RevealFromPremiumMysteryBox()
    {
        // Start on the first presentation update and keep every layer moving
        // until cleanup. This uses the same frame-clock interpolation as the
        // car merge, avoiding idle frames and realtime-clock quantization.
        const float totalDuration = 0.510f;
        const float impactTime = 0f;
        const float carPopEnd = 0.145f;
        const float carSettleEnd = 0.275f;
        const float carStartScale = 0.82f;
        const float carPeakScale = 1.14f;
        const float carStartTuck = 0.085f;
        const float carPeakLift = 0.070f;

        Camera revealCamera = Camera.main != null ? Camera.main : Camera.current;
        Vector3 screenRight = revealCamera != null
            ? revealCamera.transform.right
            : Vector3.right;
        if (screenRight.sqrMagnitude < 0.001f) screenRight = Vector3.right;
        screenRight.Normalize();
        Vector3 screenUp = revealCamera != null
            ? revealCamera.transform.up
            : Vector3.forward;
        if (screenUp.sqrMagnitude < 0.001f) screenUp = Vector3.forward;
        screenUp.Normalize();
        Vector3 cameraForward = revealCamera != null ? revealCamera.transform.forward : Vector3.down;

        Bounds intactBounds = GetLevel50RevealBounds();
        Vector3 revealCenter = intactBounds.center;
        float boxDisplayWidth;
        float boxDisplayHeight;
        if (!TryGetLevel50ProjectedRevealGeometry(
                screenRight,
                screenUp,
                out revealCenter,
                out boxDisplayWidth,
                out boxDisplayHeight))
        {
            GetLevel50ProjectedRevealSize(
                intactBounds,
                screenRight,
                screenUp,
                out boxDisplayWidth,
                out boxDisplayHeight);
        }
        var fragments = new List<Level50FragmentMotion>();
        var intactRenderers = new List<Renderer>();
        BuildLevel50FractureFragments(
            fragments,
            intactRenderers,
            revealCamera,
            screenRight,
            screenUp,
            revealCenter,
            boxDisplayWidth,
            boxDisplayHeight);
        GameObject localVfxRoot = null;
        Renderer outerFlash = null;
        Renderer coreFlash = null;
        Renderer premiumBurst = null;
        Renderer risingQuestion = null;
        Vector3 questionStartPosition = revealCenter;
        Vector3 questionBaseScale = Vector3.one;
        float effectDiameter = Mathf.Clamp(
            boxDisplayWidth,
            1.25f,
            2.15f);

        Vector3 carBasePosition = revealCarVisualRoot.transform.localPosition;
        Vector3 carBaseScale = revealCarVisualRoot.transform.localScale;
        Vector3 carScreenPopOffset = transform.InverseTransformVector(
            screenUp * boxDisplayHeight);
        bool arrowWasActive = directionArrowRoot != null && directionArrowRoot.activeSelf;
        if (directionArrowRoot != null) directionArrowRoot.SetActive(false);

        revealCarVisualRoot.transform.localPosition = carBasePosition;
        revealCarVisualRoot.transform.localScale = carBaseScale;
        revealCarVisualRoot.SetActive(false);
        IsRevealCovered = false;

        bool impactStarted = false;
        bool arrowRestored = false;
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            if (!impactStarted && elapsed >= impactTime)
            {
                for (int rendererIndex = 0; rendererIndex < intactRenderers.Count; rendererIndex++)
                    if (intactRenderers[rendererIndex] != null)
                        intactRenderers[rendererIndex].enabled = false;
                for (int fragmentIndex = 0; fragmentIndex < fragments.Count; fragmentIndex++)
                {
                    if (fragments[fragmentIndex].renderer != null)
                        fragments[fragmentIndex].renderer.enabled = true;
                    if (fragments[fragmentIndex].shadowRenderer != null)
                        fragments[fragmentIndex].shadowRenderer.enabled = true;
                }

                revealCarVisualRoot.transform.localPosition = carBasePosition
                    - carScreenPopOffset * carStartTuck;
                revealCarVisualRoot.transform.localScale = carBaseScale * carStartScale;
                revealCarVisualRoot.SetActive(true);

                if (mysteryBoxRevealVfx != null)
                {
                    mysteryBoxRevealVfx.PlayMysteryBoxBurst(revealCenter, effectDiameter);
                    mysteryBoxRevealVfx.PlayMergeOrbit(
                        revealCenter,
                        CarPrototype3D.GetMergeSparkleColor(PieceColor),
                        effectDiameter * PremiumMysteryOrbitDiameterScale,
                        3);
                }

                Transform vfxParent = transform.parent != null ? transform.parent : transform;
                localVfxRoot = new GameObject("Premium Mystery Box Burst");
                localVfxRoot.transform.SetParent(vfxParent, true);
                localVfxRoot.transform.position = revealCenter;
                localVfxRoot.transform.rotation = Quaternion.identity;
                Quaternion billboardRotation = revealCamera != null
                    ? revealCamera.transform.rotation
                    : CarPrototype3D.GetMechanicArtworkCameraRotation();
                // The car mesh has real depth. Bring the screen-space burst
                // ahead of its bonnet so the reference's white-hot impact is
                // not depth-occluded by the newly revealed car.
                Vector3 flashPosition = revealCenter - cameraForward * 1.80f;
                outerFlash = CreateLevel50RevealQuad(
                    "Reference Warm Flash",
                    localVfxRoot.transform,
                    flashPosition,
                    billboardRotation,
                    GetLevel50RevealVfxMaterial(
                        "Reference Warm Flash",
                        new Color(1f, 0.31f, 0.06f, 0.90f),
                        true),
                    130);
                coreFlash = CreateLevel50RevealQuad(
                    "Reference White Core",
                    localVfxRoot.transform,
                    flashPosition - cameraForward * 0.018f,
                    billboardRotation,
                    GetLevel50RevealVfxMaterial(
                        "Reference White Core",
                        new Color(1f, 0.94f, 0.70f, 1f),
                        true),
                    132);
                premiumBurst = CreateLevel50RevealQuad(
                    "Approved White Gold Impact Artwork",
                    localVfxRoot.transform,
                    revealCenter - cameraForward * 1.90f,
                    billboardRotation,
                    GetPremiumMysteryBoxBurstMaterial(),
                    144);
                if (premiumBurst != null)
                    premiumBurst.transform.localScale = Vector3.zero;
                questionStartPosition = revealCenter
                    + screenUp * boxDisplayHeight * 0.02f
                    - cameraForward * 2.00f;
                risingQuestion = CreateLevel50RevealQuad(
                    "Rising Mystery Question Mark",
                    localVfxRoot.transform,
                    questionStartPosition,
                    billboardRotation,
                    GetPremiumMysteryQuestionMaterial(),
                    148);
                Texture2D questionTexture = Resources.Load<Texture2D>(
                    PremiumMysteryQuestionResourcePath);
                float questionAspect = questionTexture != null && questionTexture.height > 0
                    ? (float)questionTexture.width / questionTexture.height
                    : 0.72f;
                float questionHeight = boxDisplayHeight * 0.60f;
                questionBaseScale = new Vector3(
                    questionHeight * questionAspect,
                    questionHeight,
                    1f);
                if (risingQuestion != null)
                    risingQuestion.transform.localScale = Vector3.zero;
                impactStarted = true;
            }

            if (impactStarted)
            {
                float impactAge = elapsed - impactTime;
                if (elapsed <= carPopEnd)
                {
                    float progress = Mathf.InverseLerp(impactTime, carPopEnd, elapsed);
                    float eased = EaseOutCubic(progress);
                    revealCarVisualRoot.transform.localPosition = Vector3.LerpUnclamped(
                        carBasePosition - carScreenPopOffset * carStartTuck,
                        carBasePosition + carScreenPopOffset * carPeakLift,
                        eased);
                    revealCarVisualRoot.transform.localScale = Vector3.LerpUnclamped(
                        carBaseScale * carStartScale,
                        carBaseScale * carPeakScale,
                        eased);
                }
                else if (elapsed <= carSettleEnd)
                {
                    float progress = Mathf.InverseLerp(carPopEnd, carSettleEnd, elapsed);
                    float eased = EaseInOutSine(progress);
                    revealCarVisualRoot.transform.localPosition = Vector3.LerpUnclamped(
                        carBasePosition + carScreenPopOffset * carPeakLift,
                        carBasePosition,
                        eased);
                    revealCarVisualRoot.transform.localScale = Vector3.LerpUnclamped(
                        carBaseScale * carPeakScale,
                        carBaseScale,
                        eased);
                }
                else
                {
                    revealCarVisualRoot.transform.localPosition = carBasePosition;
                    revealCarVisualRoot.transform.localScale = carBaseScale;
                }

                UpdateLevel50Fragments(
                    fragments,
                    impactAge,
                    screenUp,
                    cameraForward,
                    propertyBlock);
                UpdateLevel50RevealFlash(
                    outerFlash,
                    coreFlash,
                    impactAge,
                    effectDiameter,
                    propertyBlock);
                UpdatePremiumMysteryBoxBurst(
                    premiumBurst,
                    impactAge,
                    effectDiameter,
                    propertyBlock);
                UpdatePremiumMysteryQuestion(
                    risingQuestion,
                    questionStartPosition,
                    questionBaseScale,
                    impactAge,
                    boxDisplayHeight,
                    screenUp,
                    propertyBlock);
            }
            if (elapsed >= totalDuration)
                break;
            yield return null;
        }

        revealCarVisualRoot.transform.localPosition = carBasePosition;
        revealCarVisualRoot.transform.localScale = carBaseScale;
        if (!arrowRestored && directionArrowRoot != null)
        {
            directionArrowRoot.SetActive(arrowWasActive);
            arrowRestored = true;
        }

        if (revealBoxVisualRoot != null) revealBoxVisualRoot.SetActive(false);
        DestroyLevel50RevealObjects(fragments);
        if (localVfxRoot != null) Destroy(localVfxRoot);
        if (rootCollider != null) rootCollider.enabled = true;
    }

    private Bounds GetLevel50RevealBounds()
    {
        Renderer[] renderers = revealBoxVisualRoot != null
            ? revealBoxVisualRoot.GetComponentsInChildren<Renderer>(true)
            : null;
        if (renderers == null || renderers.Length == 0)
            return new Bounds(transform.position + Vector3.up * 0.4f, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private bool TryGetLevel50ProjectedRevealGeometry(
        Vector3 screenRight,
        Vector3 screenUp,
        out Vector3 center,
        out float width,
        out float height)
    {
        center = GetLevel50RevealBounds().center;
        width = 0f;
        height = 0f;
        if (revealBoxVisualRoot == null) return false;

        MeshFilter[] filters = revealBoxVisualRoot.GetComponentsInChildren<MeshFilter>(true);
        float minRight = float.PositiveInfinity;
        float maxRight = float.NegativeInfinity;
        float minUp = float.PositiveInfinity;
        float maxUp = float.NegativeInfinity;
        bool foundVertex = false;
        for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
        {
            MeshFilter filter = filters[filterIndex];
            Renderer renderer = filter != null ? filter.GetComponent<Renderer>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (renderer == null
                || !renderer.enabled
                || !renderer.gameObject.activeInHierarchy
                || mesh == null)
                continue;

            Vector3[] vertices = mesh.vertices;
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                Vector3 worldVertex = filter.transform.TransformPoint(vertices[vertexIndex]);
                float right = Vector3.Dot(worldVertex, screenRight);
                float up = Vector3.Dot(worldVertex, screenUp);
                minRight = Mathf.Min(minRight, right);
                maxRight = Mathf.Max(maxRight, right);
                minUp = Mathf.Min(minUp, up);
                maxUp = Mathf.Max(maxUp, up);
                foundVertex = true;
            }
        }

        if (!foundVertex) return false;
        width = Mathf.Max(0.1f, maxRight - minRight);
        height = Mathf.Max(0.1f, maxUp - minUp);
        float centerRight = (minRight + maxRight) * 0.5f;
        float centerUp = (minUp + maxUp) * 0.5f;
        center += screenRight * (centerRight - Vector3.Dot(center, screenRight));
        center += screenUp * (centerUp - Vector3.Dot(center, screenUp));
        return true;
    }

    private static void GetLevel50ProjectedRevealSize(
        Bounds bounds,
        Vector3 screenRight,
        Vector3 screenUp,
        out float width,
        out float height)
    {
        Vector3 extents = bounds.extents;
        width = 2f * (
            Mathf.Abs(screenRight.x) * extents.x
            + Mathf.Abs(screenRight.y) * extents.y
            + Mathf.Abs(screenRight.z) * extents.z);
        height = 2f * (
            Mathf.Abs(screenUp.x) * extents.x
            + Mathf.Abs(screenUp.y) * extents.y
            + Mathf.Abs(screenUp.z) * extents.z);
        width = Mathf.Max(width, 0.1f);
        height = Mathf.Max(height, 0.1f);
    }

    private void BuildLevel50FractureFragments(
        List<Level50FragmentMotion> fragments,
        List<Renderer> intactRenderers,
        Camera revealCamera,
        Vector3 screenRight,
        Vector3 screenUp,
        Vector3 revealCenter,
        float boxDisplayWidth,
        float boxDisplayHeight)
    {
        if (revealBoxVisualRoot == null) return;
        Renderer[] sourceRenderers = revealBoxVisualRoot.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < sourceRenderers.Length; index++)
            intactRenderers.Add(sourceRenderers[index]);

        Level50FragmentAtlasManifest manifest = GetPremiumMysteryBoxFragmentManifest();
        Material fragmentMaterial = GetPremiumMysteryBoxFragmentMaterial();
        Material shadowMaterial = GetPremiumMysteryBoxFragmentShadowMaterial();
        if (manifest == null
            || manifest.fragments == null
            || manifest.fragments.Length != 14
            || fragmentMaterial == null
            || shadowMaterial == null)
        {
            Debug.LogError(
                "The premium mystery-box reveal requires its 14-fragment atlas and manifest.");
            return;
        }

        Quaternion billboardRotation = revealCamera != null
            ? revealCamera.transform.rotation
            : CarPrototype3D.GetMechanicArtworkCameraRotation();
        Vector3 cameraForward = revealCamera != null
            ? revealCamera.transform.forward
            : Vector3.down;
        Transform fragmentParent = transform.parent != null ? transform.parent : transform;
        for (int index = 0; index < manifest.fragments.Length; index++)
        {
            Level50FragmentAtlasEntry entry = manifest.fragments[index];
            Vector3 position = revealCenter
                + screenRight * entry.centerX * boxDisplayWidth
                + screenUp * entry.centerY * boxDisplayHeight
                - cameraForward * 0.45f;
            Vector3 scale = new Vector3(
                boxDisplayWidth * entry.width,
                boxDisplayHeight * entry.height,
                1f);
            CreateLevel50AtlasFragment(
                fragments,
                entry,
                fragmentParent,
                position,
                billboardRotation,
                scale,
                fragmentMaterial,
                shadowMaterial,
                screenRight,
                screenUp,
                cameraForward,
                boxDisplayWidth,
                boxDisplayHeight);
        }
    }

    private static void CreateLevel50AtlasFragment(
        List<Level50FragmentMotion> fragments,
        Level50FragmentAtlasEntry entry,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Material fragmentMaterial,
        Material shadowMaterial,
        Vector3 screenRight,
        Vector3 screenUp,
        Vector3 cameraForward,
        float boxDisplayWidth,
        float boxDisplayHeight)
    {
        Rect atlasUv = new Rect(
            entry.uvX,
            entry.uvY,
            entry.uvWidth,
            entry.uvHeight);
        Mesh fragmentMesh = CreateLevel50FragmentQuadMesh(entry.name, atlasUv);

        GameObject fragmentObject = new GameObject(
            entry.name,
            typeof(MeshFilter),
            typeof(MeshRenderer));
        fragmentObject.transform.SetParent(parent, true);
        fragmentObject.transform.SetPositionAndRotation(position, rotation);
        fragmentObject.transform.localScale = scale;
        fragmentObject.GetComponent<MeshFilter>().sharedMesh = fragmentMesh;
        MeshRenderer fragmentRenderer = fragmentObject.GetComponent<MeshRenderer>();
        ConfigureLevel50FragmentRenderer(fragmentRenderer, fragmentMaterial, 126 + entry.atlasIndex % 3);

        GameObject shadowObject = new GameObject(
            entry.name + " Shadow",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        shadowObject.transform.SetParent(parent, true);
        shadowObject.transform.SetPositionAndRotation(position, rotation);
        shadowObject.transform.localScale = scale * 1.025f;
        shadowObject.GetComponent<MeshFilter>().sharedMesh = fragmentMesh;
        MeshRenderer shadowRenderer = shadowObject.GetComponent<MeshRenderer>();
        ConfigureLevel50FragmentRenderer(shadowRenderer, shadowMaterial, 116 + entry.atlasIndex % 2);

        var motion = new Level50FragmentMotion
        {
            gameObject = fragmentObject,
            renderer = fragmentRenderer,
            shadowTransform = shadowObject.transform,
            shadowRenderer = shadowRenderer,
            runtimeMesh = fragmentMesh,
            startPosition = position,
            startRotation = rotation,
            startScale = scale,
            spinAxis = cameraForward.normalized,
            tint = Color.white,
            delay = 0.004f * (entry.atlasIndex % 3),
            shadowOffset = boxDisplayHeight * (0.014f + 0.003f * (entry.atlasIndex % 3))
        };
        ConfigureLevel50AtlasFragmentMotion(
            motion,
            entry.atlasIndex,
            screenRight,
            screenUp,
            boxDisplayWidth);
        fragments.Add(motion);
    }

    private static Mesh CreateLevel50FragmentQuadMesh(string fragmentName, Rect uvRect)
    {
        Mesh mesh = new Mesh
        {
            name = fragmentName + " Atlas Mesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            },
            normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back },
            uv = new[]
            {
                new Vector2(uvRect.xMin, uvRect.yMin),
                new Vector2(uvRect.xMax, uvRect.yMin),
                new Vector2(uvRect.xMax, uvRect.yMax),
                new Vector2(uvRect.xMin, uvRect.yMax)
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void ConfigureLevel50FragmentRenderer(
        Renderer renderer,
        Material material,
        int sortingOrder)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
    }

    private static void ConfigureLevel50AtlasFragmentMotion(
        Level50FragmentMotion motion,
        int fragmentIndex,
        Vector3 screenRight,
        Vector3 screenUp,
        float boxDisplayWidth)
    {
        Vector2[] directions =
        {
            new Vector2(-0.72f, 0.70f),
            new Vector2(0.72f, 0.70f),
            new Vector2(-0.98f, -0.16f),
            new Vector2(0.98f, -0.12f),
            new Vector2(-0.08f, -1f),
            new Vector2(0.86f, 0.18f),
            new Vector2(0.98f, -0.20f),
            new Vector2(0.82f, 0.46f),
            new Vector2(-0.90f, -0.18f),
            new Vector2(-0.96f, 0.22f),
            new Vector2(-0.92f, -0.40f),
            new Vector2(0.96f, -0.28f),
            new Vector2(-0.10f, -1f),
            new Vector2(0.24f, 0.97f)
        };
        float[] distances =
        {
            0.43f, 0.43f, 0.56f, 0.58f, 0.42f,
            0.48f, 0.64f, 0.52f, 0.54f, 0.67f,
            0.70f, 0.72f, 0.56f, 0.50f
        };
        float[] spins =
        {
            -34f, 32f, -16f, 18f, -9f,
            42f, 72f, 58f, -64f, -112f,
            126f, 132f, -94f, 98f
        };

        int index = Mathf.Clamp(fragmentIndex, 0, directions.Length - 1);
        Vector2 direction = directions[index].normalized;
        motion.moveDirection = (
            screenRight * direction.x
            + screenUp * direction.y).normalized;
        motion.distance = boxDisplayWidth
            * distances[index]
            * PremiumMysteryDebrisRadiusScale;
        motion.spinDegrees = spins[index];
        if (index <= 4)
        {
            motion.fadeStart = 0.335f;
            motion.fadeEnd = 0.445f;
        }
        else if (index <= 8)
        {
            motion.fadeStart = 0.360f;
            motion.fadeEnd = 0.485f;
        }
        else
        {
            motion.fadeStart = 0.385f;
            motion.fadeEnd = 0.510f;
        }
    }

    private static Renderer CreateLevel50RevealQuad(
        string objectName,
        Transform parent,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Material material,
        int sortingOrder)
    {
        if (material == null) return null;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objectName;
        quad.transform.SetParent(parent, true);
        quad.transform.SetPositionAndRotation(worldPosition, worldRotation);
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static void UpdateLevel50Fragments(
        List<Level50FragmentMotion> fragments,
        float elapsed,
        Vector3 screenUp,
        Vector3 cameraForward,
        MaterialPropertyBlock propertyBlock)
    {
        for (int index = 0; index < fragments.Count; index++)
        {
            Level50FragmentMotion fragment = fragments[index];
            if (fragment.gameObject == null || fragment.renderer == null) continue;
            float age = Mathf.Max(0f, elapsed - fragment.delay);
            float motionDuration = Mathf.Max(
                0.001f,
                fragment.fadeEnd - fragment.delay);
            float travelProgress = Mathf.Clamp01(age / motionDuration);
            float easedTravel = Mathf.SmoothStep(0f, 1f, travelProgress);
            float rotationProgress = easedTravel;
            fragment.gameObject.transform.position = fragment.startPosition
                + fragment.moveDirection * fragment.distance * easedTravel
                - cameraForward * (0.012f * easedTravel);
            fragment.gameObject.transform.rotation = Quaternion.AngleAxis(
                fragment.spinDegrees * rotationProgress,
                fragment.spinAxis) * fragment.startRotation;
            fragment.gameObject.transform.localScale = fragment.startScale;

            float fadeProgress = Mathf.InverseLerp(
                fragment.fadeStart,
                fragment.fadeEnd,
                elapsed);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            Color visibleColor = fragment.tint;
            visibleColor.a *= fade;
            SetLevel50RendererColor(fragment.renderer, visibleColor, propertyBlock);
            fragment.renderer.enabled = fade > 0.001f;

            if (fragment.shadowTransform == null || fragment.shadowRenderer == null)
                continue;

            fragment.shadowTransform.position = fragment.gameObject.transform.position
                - screenUp * fragment.shadowOffset
                + cameraForward * 0.035f;
            fragment.shadowTransform.rotation = fragment.gameObject.transform.rotation;
            fragment.shadowTransform.localScale = fragment.startScale * 1.025f;
            SetLevel50RendererColor(
                fragment.shadowRenderer,
                new Color(0.16f, 0.055f, 0.018f, 0.25f * fade),
                propertyBlock);
            fragment.shadowRenderer.enabled = fade > 0.001f;
        }
    }

    private static void UpdateLevel50RevealFlash(
        Renderer outerFlash,
        Renderer coreFlash,
        float elapsed,
        float effectDiameter,
        MaterialPropertyBlock propertyBlock)
    {
        if (outerFlash != null)
        {
            float progress = Mathf.Clamp01(elapsed / 0.24f);
            float scale = effectDiameter * Mathf.Lerp(
                0.08f,
                0.92f,
                EaseOutCubic(Mathf.Clamp01(progress / 0.48f)));
            outerFlash.transform.localScale = Vector3.one * scale;
            Color color = new Color(1f, 0.30f, 0.055f, 0.92f);
            color.a *= 1f - Mathf.SmoothStep(0.16f, 1f, progress);
            SetLevel50RendererColor(outerFlash, color, propertyBlock);
            outerFlash.enabled = progress < 1f;
        }
        if (coreFlash != null)
        {
            float progress = Mathf.Clamp01(elapsed / 0.18f);
            float scale = effectDiameter * Mathf.Lerp(
                0.05f,
                0.58f,
                EaseOutCubic(Mathf.Clamp01(progress / 0.38f)));
            coreFlash.transform.localScale = Vector3.one * scale;
            Color color = new Color(1f, 0.94f, 0.66f, 1f);
            color.a *= 1f - Mathf.SmoothStep(0.12f, 1f, progress);
            SetLevel50RendererColor(coreFlash, color, propertyBlock);
            coreFlash.enabled = progress < 1f;
        }
    }

    private static void UpdatePremiumMysteryBoxBurst(
        Renderer burst,
        float elapsed,
        float effectDiameter,
        MaterialPropertyBlock propertyBlock)
    {
        if (burst == null) return;
        const float life = 0.27f;
        float progress = Mathf.Clamp01(elapsed / life);
        float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.12f));
        float disappear = 1f - Mathf.SmoothStep(0.38f, 1f, progress);
        float scale = effectDiameter
            * Mathf.Lerp(0.16f, 0.98f, EaseOutCubic(progress))
            * appear;
        burst.transform.localScale = Vector3.one * scale;
        Color color = Color.white;
        color.a = disappear;
        SetLevel50RendererColor(burst, color, propertyBlock);
        burst.enabled = progress < 1f;
    }

    private static void UpdatePremiumMysteryQuestion(
        Renderer question,
        Vector3 startPosition,
        Vector3 baseScale,
        float elapsed,
        float boxDisplayHeight,
        Vector3 screenUp,
        MaterialPropertyBlock propertyBlock)
    {
        if (question == null) return;
        const float life = 0.505f;
        float progress = Mathf.Clamp01(elapsed / life);
        float riseProgress = Mathf.SmoothStep(0f, 1f, progress);
        question.transform.position = startPosition
            + screenUp * boxDisplayHeight * 0.18f * riseProgress;

        float scale;
        if (progress < 0.22f)
        {
            scale = Mathf.Lerp(
                0.72f,
                1.04f,
                EaseOutCubic(progress / 0.22f));
        }
        else if (progress < 0.52f)
        {
            scale = Mathf.Lerp(
                1.04f,
                1f,
                EaseInOutSine((progress - 0.22f) / 0.30f));
        }
        else
        {
            scale = 1f;
        }

        question.transform.localScale = baseScale * scale;
        Color color = Color.white;
        color.a = 1f - Mathf.SmoothStep(0.78f, 1f, progress);
        SetLevel50RendererColor(question, color, propertyBlock);
        question.enabled = progress < 1f;
    }

    private static void SetLevel50RendererColor(
        Renderer renderer,
        Color color,
        MaterialPropertyBlock propertyBlock)
    {
        if (renderer == null || propertyBlock == null) return;
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorProperty, color);
        propertyBlock.SetColor(ColorProperty, color);
        renderer.SetPropertyBlock(propertyBlock);
    }

    private static void DestroyLevel50RevealObjects(
        List<Level50FragmentMotion> fragments)
    {
        for (int index = 0; index < fragments.Count; index++)
        {
            Level50FragmentMotion fragment = fragments[index];
            if (fragment.gameObject != null) Destroy(fragment.gameObject);
            if (fragment.shadowTransform != null)
                Destroy(fragment.shadowTransform.gameObject);
            if (fragment.runtimeMesh != null) Destroy(fragment.runtimeMesh);
        }
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOutSine(float value)
    {
        return -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(value)) - 1f) * 0.5f;
    }

    internal void SetDirectionArrowVisible(bool visible)
    {
        if (directionArrowRoot != null)
            directionArrowRoot.SetActive(visible);
    }

    internal void SetEditedDirection(CarPrototype3D.ExitDirection direction)
    {
        Quaternion boxWorldRotation = revealBoxVisualRoot != null
            ? revealBoxVisualRoot.transform.rotation
            : Quaternion.identity;
        Direction = direction;
        transform.rotation = Quaternion.LookRotation(
            CarPrototype3D.DirectionToWorld(direction), Vector3.up);
        if (IsRevealCovered && revealBoxVisualRoot != null)
        {
            revealBoxVisualRoot.transform.rotation = usesLevel35MysteryBoxVisual
                ? Quaternion.identity
                : usesLevel50MysteryBoxVisual
                    ? boxWorldRotation
                    : CarPrototype3D.GetMechanicArtworkCameraRotation();
        }
    }

    internal void SetEditedGridPosition(int row, int col)
    {
        Row = row;
        Col = col;
    }

    internal bool SetEditedRevealBox(
        bool covered,
        bool useLevel50Visual = false)
    {
        if (IsTrash || CellLength != 1) return false;

        if (covered)
        {
            if (IsRevealCovered) return true;

            IsRevealCovered = true;
            WrapCarVisualsForReveal();
            BuildRevealBoxVisual();
            if (useLevel50Visual) UseLevel50MysteryBoxVisual();
            cachedRenderers = null;
            cachedColliders = null;
            CacheVisibilityComponents();
            return true;
        }

        if (!IsRevealCovered) return true;
        if (revealCarVisualRoot != null)
        {
            revealCarVisualRoot.SetActive(true);
            var carChildren = new List<Transform>();
            for (int index = 0; index < revealCarVisualRoot.transform.childCount; index++)
                carChildren.Add(revealCarVisualRoot.transform.GetChild(index));
            for (int index = 0; index < carChildren.Count; index++)
                carChildren[index].SetParent(transform, false);
        }

        DestroyEditedRevealRoot(revealBoxVisualRoot);
        DestroyEditedRevealRoot(revealCarVisualRoot);
        revealBoxVisualRoot = null;
        revealCarVisualRoot = null;
        usesLevel35MysteryBoxVisual = false;
        usesLevel50MysteryBoxVisual = false;
        IsRevealCovered = false;
        cachedRenderers = null;
        cachedColliders = null;
        CacheVisibilityComponents();
        return true;
    }

    internal Quaternion GetEditedRevealBoxWorldRotation()
    {
        return revealBoxVisualRoot != null
            ? revealBoxVisualRoot.transform.rotation
            : transform.rotation;
    }

    internal void SetEditedRevealBoxWorldRotation(Quaternion rotation)
    {
        if (IsRevealCovered && revealBoxVisualRoot != null)
            revealBoxVisualRoot.transform.rotation = rotation;
    }

    private static void DestroyEditedRevealRoot(GameObject root)
    {
        if (root == null) return;
        root.SetActive(false);
        if (Application.isPlaying) Destroy(root);
        else DestroyImmediate(root);
    }

    private void BuildDirectionArrow()
    {
        Texture2D fallbackTexture = Resources.Load<Texture2D>("CarPrototype/direction_arrow_white");
        Texture2D arrowTexture = GetDirectionArrowTexture(fallbackTexture);
        if (arrowTexture == null) return;

        directionArrowRoot = GameObject.CreatePrimitive(PrimitiveType.Quad);
        directionArrowRoot.name = "Movement Direction Arrow";
        directionArrowRoot.transform.SetParent(transform, false);
        // Measured from the normalized shared-car and limousine meshes. Their
        // roof panels are both centered 0.345 local units behind the complete
        // vehicle bounds center. Keeping this as a car-local anchor makes it
        // rotate with the body and remain on the same roof point in every
        // direction instead of orbiting around the root center.
        const float normalizedRoofCenterZ = -0.345f;
        Vector3 arrowCenter = new Vector3(
            0f,
            CellLength > 1 ? 0.89f : 0.91f,
            normalizedRoofCenterZ);
        Renderer[] carRenderers = GetComponentsInChildren<Renderer>(true);
        if (TryGetLocalRenderBounds(carRenderers, out Bounds carBounds))
        {
            // Every imported vehicle is normalized to the piece root when it
            // is built. Renderer bounds also contain details such as eyes and
            // mirrors, whose tiny asymmetries used to nudge the arrow sideways.
            // Use bounds only for roof height and lock the arrow to the exact
            // longitudinal and lateral center of the vehicle.
            // Keep only a hairline separation to avoid texture flicker. The
            // former 0.49-unit gap made the arrow visibly float and introduced
            // perspective drift as the car rotated under the angled camera.
            const float roofSurfaceClearance = 0.015f;
            arrowCenter.y = carBounds.max.y + roofSurfaceClearance;
        }
        directionArrowRoot.transform.localPosition = arrowCenter;
        // The source artwork points right. Map the quad's local right axis to
        // the car's local forward axis and its visible face upward, so it
        // always indicates the car's exit and remains readable by the camera.
        directionArrowRoot.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
        // Increase presence primarily across the roof instead of extending the
        // arrow toward the windshield. The thicker generated silhouette fills
        // this modestly larger quad without breaking the roof-only boundary.
        // The regular roof's measured longitudinal span is 0.871 units. An
        // 0.88 quad with the inset texture silhouette stays completely inside
        // that panel, including its black outline, while retaining the new
        // wide and heavy visual weight. Limousines have a much longer roof.
        float arrowLength = CellLength > 1 ? 1.30f : 0.88f;
        directionArrowRoot.transform.localScale = new Vector3(arrowLength, arrowLength * 0.82f, 1f);

        Collider arrowCollider = directionArrowRoot.GetComponent<Collider>();
        if (arrowCollider != null)
        {
            if (Application.isPlaying) Destroy(arrowCollider);
            else DestroyImmediate(arrowCollider);
        }

        Renderer renderer = directionArrowRoot.GetComponent<Renderer>();
        renderer.sharedMaterial = GetDirectionArrowMaterial(arrowTexture);
        ConfigureProceduralPartRenderer(renderer, false);
    }

    private static Texture2D GetDirectionArrowTexture(Texture2D fallbackTexture)
    {
        if (directionArrowTexture != null) return directionArrowTexture;

        const int textureSize = 256;
        const float outlineWidth = 0.13f;
        float antialiasWidth = 3f / textureSize;
        List<Vector2> polygon = BuildRoundedDirectionArrowPolygon();
        if (polygon.Count < 3) return fallbackTexture;

        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "Thick Roof-Safe Direction Arrow",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int row = 0; row < textureSize; row++)
        {
            float y = ((row + 0.5f) / textureSize) * 2f - 1f;
            for (int column = 0; column < textureSize; column++)
            {
                float x = ((column + 0.5f) / textureSize) * 2f - 1f;
                float signedDistance = SignedDistanceToPolygon(new Vector2(x, y), polygon);
                float outerAlpha = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(-antialiasWidth, antialiasWidth, signedDistance + outlineWidth));
                if (outerAlpha <= 0.001f)
                {
                    pixels[row * textureSize + column] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float whiteCoverage = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(-antialiasWidth, antialiasWidth, signedDistance));
                byte shade = (byte)Mathf.RoundToInt(255f * whiteCoverage);
                byte alpha = (byte)Mathf.RoundToInt(255f * outerAlpha);
                pixels[row * textureSize + column] = new Color32(shade, shade, shade, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        directionArrowTexture = texture;
        return directionArrowTexture;
    }

    private static List<Vector2> BuildRoundedDirectionArrowPolygon()
    {
        // The wider shaft, larger head, and heavier outline make the direction
        // readable immediately while all outer edges remain inside the texture.
        Vector2[] vertices =
        {
            new Vector2(-0.84f, -0.42f),
            new Vector2(-0.02f, -0.42f),
            new Vector2(-0.02f, -0.84f),
            new Vector2(0.85f, 0f),
            new Vector2(-0.02f, 0.84f),
            new Vector2(-0.02f, 0.42f),
            new Vector2(-0.84f, 0.42f)
        };
        float[] cornerCuts = { 0.16f, 0.12f, 0.11f, 0.14f, 0.11f, 0.12f, 0.16f };
        var rounded = new List<Vector2>(vertices.Length * 6);

        for (int index = 0; index < vertices.Length; index++)
        {
            Vector2 vertex = vertices[index];
            Vector2 previous = vertices[(index - 1 + vertices.Length) % vertices.Length];
            Vector2 next = vertices[(index + 1) % vertices.Length];
            Vector2 towardPrevious = (previous - vertex).normalized;
            Vector2 towardNext = (next - vertex).normalized;
            float cut = Mathf.Min(
                cornerCuts[index],
                Mathf.Min(Vector2.Distance(vertex, previous), Vector2.Distance(vertex, next)) * 0.32f);
            Vector2 curveStart = vertex + towardPrevious * cut;
            Vector2 curveEnd = vertex + towardNext * cut;

            for (int sample = 0; sample < 6; sample++)
            {
                float progress = sample / 5f;
                float remaining = 1f - progress;
                rounded.Add(
                    remaining * remaining * curveStart
                    + 2f * remaining * progress * vertex
                    + progress * progress * curveEnd);
            }
        }

        return rounded;
    }

    private static float SignedDistanceToPolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        float nearestSquaredDistance = float.MaxValue;
        int previousIndex = polygon.Count - 1;

        for (int index = 0; index < polygon.Count; index++)
        {
            Vector2 start = polygon[previousIndex];
            Vector2 end = polygon[index];
            Vector2 segment = end - start;
            float segmentSquaredLength = segment.sqrMagnitude;
            float projection = segmentSquaredLength > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentSquaredLength)
                : 0f;
            nearestSquaredDistance = Mathf.Min(
                nearestSquaredDistance,
                (point - (start + segment * projection)).sqrMagnitude);

            bool crossesScanline = (start.y > point.y) != (end.y > point.y);
            if (crossesScanline)
            {
                float crossingX = (end.x - start.x) * (point.y - start.y)
                    / (end.y - start.y) + start.x;
                if (point.x < crossingX) inside = !inside;
            }

            previousIndex = index;
        }

        float distance = Mathf.Sqrt(nearestSquaredDistance);
        return inside ? distance : -distance;
    }

    private static Material GetDirectionArrowMaterial(Texture2D texture)
    {
        if (directionArrowMaterial != null) return directionArrowMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        Material material = new Material(shader)
        {
            name = "White and Black Movement Direction Arrow",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white,
            mainTexture = texture,
            renderQueue = (int)RenderQueue.Transparent
        };
        material.enableInstancing = true;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);

        directionArrowMaterial = material;
        return material;
    }

    internal bool OccupiesCell(int row, int col)
    {
        Vector2Int step = CarPrototype3D.DirectionToGridStep(Direction);
        for (int offset = 0; offset < CellLength; offset++)
        {
            if (Row - step.y * offset == row && Col - step.x * offset == col)
                return true;
        }

        return false;
    }

    internal IEnumerator DriveTo(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        Quaternion targetRotation = Quaternion.LookRotation((target - start).normalized, Vector3.up);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(start, target, eased);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, eased);
            SetWheelSpin(progress * 520f);
            yield return null;
        }

        transform.position = target;
    }

    internal IEnumerator DriveReverseTo(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        Vector3 travelDirection = target - start;
        if (travelDirection.sqrMagnitude < 0.0001f) yield break;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(-travelDirection.normalized, Vector3.up);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            // A reverse road leg is still travel, not a parking settle. Keep
            // its positional velocity constant and finish steering early so
            // the following leg inherits motion instead of a full stop.
            transform.position = Vector3.Lerp(start, target, progress);
            float steeringProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.42f));
            transform.rotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                steeringProgress);
            SetWheelSpin(-progress * 420f);
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
    }

    internal IEnumerator DriveToOutsideRoute(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        Quaternion targetRotation = Quaternion.LookRotation((target - start).normalized, Vector3.up);
        Vector3 startScale = transform.localScale;
        Vector3 travelScale = Vector3.one * GetTrayVisualScale();
        visualBaseScale = travelScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            // Route travel must not ease to a stop before the next segment.
            // Rotation and scale may settle smoothly while position retains
            // a constant forward velocity through the handoff.
            transform.position = Vector3.Lerp(start, target, progress);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, eased);
            transform.localScale = Vector3.Lerp(startScale, travelScale, eased);
            SetWheelSpin(progress * 520f);
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
        transform.localScale = travelScale;
    }

    internal IEnumerator DriveRouteStraight(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        Vector3 travelDirection = target - start;
        if (travelDirection.sqrMagnitude < 0.0001f) yield break;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, target, progress);

            // Finish aligning well before the end of the leg. The car then
            // enters the cubic arc with exactly the same tangent and without
            // the stop caused by SmoothStep at the old segment boundary.
            float steeringProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.42f));
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, steeringProgress);
            SetWheelSpin(progress * 520f);
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
    }

    internal IEnumerator DriveCubicCurve(
        Vector3 firstControl,
        Vector3 secondControl,
        Vector3 target,
        float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float remaining = 1f - progress;
            transform.position = remaining * remaining * remaining * start
                + 3f * remaining * remaining * progress * firstControl
                + 3f * remaining * progress * progress * secondControl
                + progress * progress * progress * target;

            Vector3 tangent = 3f * remaining * remaining * (firstControl - start)
                + 6f * remaining * progress * (secondControl - firstControl)
                + 3f * progress * progress * (target - secondControl);
            if (tangent.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);

            SetWheelSpin(progress * 520f);
            yield return null;
        }

        transform.position = target;
        Vector3 finalTangent = target - secondControl;
        if (finalTangent.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(finalTangent.normalized, Vector3.up);
    }

    internal IEnumerator DriveContinuousTrayPath(
        List<Vector3> path,
        float cruiseSpeed,
        float finalSpeed,
        CarMatchVfx arrivalVfx,
        System.Func<Vector3[], bool> tryReserveTraffic)
    {
        if (path == null || path.Count < 2) yield break;

        var cumulativeDistance = new float[path.Count];
        for (int index = 1; index < path.Count; index++)
        {
            cumulativeDistance[index] = cumulativeDistance[index - 1]
                + Vector3.Distance(path[index - 1], path[index]);
        }

        float totalDistance = cumulativeDistance[cumulativeDistance.Length - 1];
        if (totalDistance < 0.001f) yield break;

        Vector3 trayScale = Vector3.one * GetTrayVisualScale();
        Quaternion targetRotation = Quaternion.Euler(0f, 180f, 0f);
        float travelled = 0f;
        int segmentIndex = 0;
        int claimedSegmentIndex = -1;
        // Board exit hands off at approximately 18 units/s. Starting this
        // driver at 60% of its 30-unit cruise preserves that velocity, then
        // reaches cruise in roughly three 60 FPS frames.
        float currentSpeed = Mathf.Max(finalSpeed, cruiseSpeed * 0.60f);
        float cornerSpeed = Mathf.Max(
            finalSpeed,
            cruiseSpeed * CarPrototype3D.ContinuousRouteCornerSpeedRatio);
        float slowdownDistance = Mathf.Clamp(
            totalDistance * CarPrototype3D.ContinuousRouteSlowdownFraction,
            0.24f,
            0.55f);
        float alignmentDistance = Mathf.Clamp(totalDistance * 0.10f, 0.16f, 0.36f);
        float smokeDistance = 0f;
        Vector3 previousPosition = transform.position;
        float smokeSpacing = Mathf.Lerp(
            0.13f,
            0.20f,
            Mathf.InverseLerp(0.42f, 1.15f, trayScale.x));
        visualBaseScale = trayScale;

        while (travelled < totalDistance)
        {
            if (tryReserveTraffic != null && claimedSegmentIndex != segmentIndex)
            {
                Vector3[] trafficWindow = BuildContinuousTrafficWindow(
                    path,
                    segmentIndex,
                    transform.position,
                    8);
                if (!tryReserveTraffic(trafficWindow))
                {
                    currentSpeed = Mathf.MoveTowards(
                        currentSpeed,
                        Mathf.Max(2.5f, finalSpeed * 0.24f),
                        190f * Time.deltaTime);
                    yield return null;
                    continue;
                }
                claimedSegmentIndex = segmentIndex;
            }

            float remainingDistance = totalDistance - travelled;
            float slowdown = Mathf.SmoothStep(
                0f,
                1f,
                1f - Mathf.Clamp01(remainingDistance / slowdownDistance));
            float turnAngle = 0f;
            Vector3 currentDirection = path[segmentIndex + 1] - path[segmentIndex];
            if (segmentIndex > 0)
            {
                Vector3 previousDirection = path[segmentIndex] - path[segmentIndex - 1];
                if (previousDirection.sqrMagnitude > 0.0001f
                    && currentDirection.sqrMagnitude > 0.0001f)
                    turnAngle = Mathf.Max(
                        turnAngle,
                        Vector3.Angle(previousDirection, currentDirection));
            }
            if (segmentIndex < path.Count - 2)
            {
                Vector3 nextDirection = path[segmentIndex + 2] - path[segmentIndex + 1];
                if (currentDirection.sqrMagnitude > 0.0001f
                    && nextDirection.sqrMagnitude > 0.0001f)
                    turnAngle = Mathf.Max(
                        turnAngle,
                        Vector3.Angle(currentDirection, nextDirection));
            }
            float cornerBlend = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(2f, 13f, turnAngle));
            float roadSpeed = Mathf.Lerp(cruiseSpeed, cornerSpeed, cornerBlend);
            float targetSpeed = Mathf.Lerp(
                Mathf.Max(1f, roadSpeed),
                Mathf.Max(1f, finalSpeed),
                slowdown);
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                240f * Time.deltaTime);
            travelled = Mathf.Min(
                totalDistance,
                travelled + currentSpeed * Time.deltaTime);

            while (segmentIndex < path.Count - 2
                && cumulativeDistance[segmentIndex + 1] < travelled)
                segmentIndex++;

            Vector3 position = SampleContinuousPathAtDistance(
                path,
                cumulativeDistance,
                travelled,
                ref segmentIndex);

            // Derive steering from a short world-distance window instead of
            // the current sampled segment. At high speed one rendered frame
            // can cross several 0.16-unit samples; this central tangent keeps
            // steering continuous and frame-rate independent through curves.
            float tangentWindow = Mathf.Clamp(
                currentSpeed * Mathf.Max(Time.deltaTime, 1f / 120f) * 0.72f,
                0.10f,
                0.30f);
            int beforeSegment = segmentIndex;
            int afterSegment = segmentIndex;
            Vector3 before = SampleContinuousPathAtDistance(
                path,
                cumulativeDistance,
                Mathf.Max(0f, travelled - tangentWindow),
                ref beforeSegment);
            Vector3 after = SampleContinuousPathAtDistance(
                path,
                cumulativeDistance,
                Mathf.Min(totalDistance, travelled + tangentWindow),
                ref afterSegment);
            Vector3 tangent = after - before;
            if (tangent.sqrMagnitude < 0.0001f) tangent = transform.forward;

            Quaternion steeringRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            float finalAlignment = Mathf.SmoothStep(
                0f,
                1f,
                1f - Mathf.Clamp01((totalDistance - travelled) / alignmentDistance));
            transform.position = position;
            Quaternion desiredRotation = Quaternion.Slerp(
                steeringRotation,
                targetRotation,
                finalAlignment);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                Mathf.Clamp01(Time.deltaTime * 24f));
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                trayScale,
                Mathf.Clamp01(Time.deltaTime * 18f));
            SetWheelSpin(travelled * 360f);

            if (arrivalVfx != null && remainingDistance < slowdownDistance * 1.15f)
            {
                Vector3 smokeSegment = position - previousPosition;
                float smokeSegmentLength = smokeSegment.magnitude;
                float distanceAlongSegment = 0f;
                while (smokeSegmentLength > 0.0001f
                    && smokeDistance + smokeSegmentLength >= smokeSpacing)
                {
                    float distanceToEmission = smokeSpacing - smokeDistance;
                    distanceAlongSegment += distanceToEmission;
                    smokeSegmentLength -= distanceToEmission;
                    smokeDistance = 0f;
                    float emissionProgress = Mathf.Clamp01(
                        distanceAlongSegment / Mathf.Max(0.0001f, smokeSegment.magnitude));
                    arrivalVfx.EmitArrivalSmoke(
                        Vector3.Lerp(previousPosition, position, emissionProgress),
                        tangent,
                        trayScale.x);
                }
                smokeDistance += smokeSegmentLength;
            }
            previousPosition = position;
            yield return null;
        }

        transform.position = path[path.Count - 1];
        transform.rotation = targetRotation;
        transform.localScale = trayScale;
    }

    private static Vector3 SampleContinuousPathAtDistance(
        List<Vector3> path,
        float[] cumulativeDistance,
        float distance,
        ref int segmentIndex)
    {
        segmentIndex = Mathf.Clamp(segmentIndex, 0, path.Count - 2);
        distance = Mathf.Clamp(
            distance,
            0f,
            cumulativeDistance[cumulativeDistance.Length - 1]);

        while (segmentIndex > 0
            && cumulativeDistance[segmentIndex] > distance)
            segmentIndex--;
        while (segmentIndex < path.Count - 2
            && cumulativeDistance[segmentIndex + 1] < distance)
            segmentIndex++;

        float segmentStartDistance = cumulativeDistance[segmentIndex];
        float segmentLength = Mathf.Max(
            0.0001f,
            cumulativeDistance[segmentIndex + 1] - segmentStartDistance);
        float segmentProgress = Mathf.Clamp01(
            (distance - segmentStartDistance) / segmentLength);
        return Vector3.Lerp(
            path[segmentIndex],
            path[segmentIndex + 1],
            segmentProgress);
    }

    private static Vector3[] BuildContinuousTrafficWindow(
        List<Vector3> path,
        int segmentIndex,
        Vector3 currentPosition,
        int lookAheadPoints)
    {
        int lastIndex = Mathf.Min(path.Count - 1, segmentIndex + lookAheadPoints);
        int pointCount = Mathf.Max(2, lastIndex - segmentIndex + 1);
        var window = new Vector3[pointCount];
        window[0] = currentPosition;
        int outputIndex = 1;
        for (int pathIndex = segmentIndex + 1;
            pathIndex <= lastIndex && outputIndex < window.Length;
            pathIndex++)
        {
            window[outputIndex] = path[pathIndex];
            outputIndex++;
        }
        if (outputIndex < window.Length)
            window[outputIndex] = path[path.Count - 1];
        return window;
    }

    internal IEnumerator PlayTrayArrivalSettle(float duration)
    {
        Vector3 trayScale = Vector3.one * GetTrayVisualScale();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float impact = progress < 0.42f
                ? Mathf.SmoothStep(0f, 1f, progress / 0.42f)
                : 1f - Mathf.SmoothStep(0f, 1f, (progress - 0.42f) / 0.58f);
            Vector3 squash = new Vector3(1.035f, 0.982f, 0.955f);
            transform.localScale = Vector3.Scale(
                trayScale,
                Vector3.Lerp(Vector3.one, squash, impact));
            yield return null;
        }

        transform.localScale = trayScale;
    }

    internal IEnumerator DriveToTraySlot(
        Vector3 target,
        float duration,
        CarMatchVfx arrivalVfx = null,
        bool playArrivalEffects = false,
        bool forceStraightApproach = false)
    {
        Vector3 trayScale = Vector3.one * GetTrayVisualScale();
        if (!playArrivalEffects)
        {
            yield return DriveToPose(
                target,
                Quaternion.Euler(0f, 180f, 0f),
                trayScale,
                duration,
                true);
            yield break;
        }

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;
        Quaternion targetRotation = Quaternion.Euler(0f, 180f, 0f);
        visualBaseScale = trayScale;

        Vector3 planarDirection = Vector3.ProjectOnPlane(target - startPosition, Vector3.up);
        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = targetRotation * Vector3.forward;
        planarDirection.Normalize();

        float signedTurn = forceStraightApproach
            ? 0f
            : Vector3.SignedAngle(
                Vector3.ProjectOnPlane(transform.forward, Vector3.up),
                planarDirection,
                Vector3.up);
        float turnSign = Mathf.Abs(signedTurn) > 2f
            ? Mathf.Sign(signedTurn)
            : Mathf.Sign(startPosition.x - target.x);
        if (Mathf.Abs(turnSign) < 0.5f) turnSign = 1f;

        Vector3 perpendicular = Vector3.Cross(Vector3.up, planarDirection).normalized;
        float routeDistance = Vector3.Distance(startPosition, target);
        float arcAmount = forceStraightApproach
            ? 0f
            : Mathf.Clamp(routeDistance * 0.13f, 0.055f, 0.16f) * turnSign;
        Vector3 control = Vector3.Lerp(startPosition, target, 0.53f) + perpendicular * arcAmount;

        float elapsed = 0f;
        float smokeDistance = 0f;
        Vector3 previousPosition = startPosition;
        float smokeSpacing = Mathf.Lerp(0.13f, 0.20f, Mathf.InverseLerp(0.42f, 1.15f, trayScale.x));
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            // A perfectly straight board-to-bay arrival starts at the same
            // normalized velocity as the preceding travel segment, then
            // decelerates only at the final pose. Curved arrivals retain the
            // stronger premium ease-out used by their parking flourish.
            float eased = forceStraightApproach
                ? progress + progress * progress - progress * progress * progress
                : 1f - Mathf.Pow(1f - progress, 2.35f);
            float remaining = 1f - eased;
            Vector3 position = remaining * remaining * startPosition
                + 2f * remaining * eased * control
                + eased * eased * target;
            Vector3 tangent = 2f * remaining * (control - startPosition)
                + 2f * eased * (target - control);
            if (tangent.sqrMagnitude < 0.0001f) tangent = planarDirection;

            Quaternion steeringRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            float finalAlignment = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.70f) / 0.30f));
            Quaternion drivenRotation = Quaternion.Slerp(steeringRotation, targetRotation, finalAlignment);
            float bank = forceStraightApproach
                ? 0f
                : Mathf.Sin(progress * Mathf.PI) * turnSign * 6.5f;
            transform.rotation = drivenRotation * Quaternion.Euler(0f, 0f, bank);
            transform.position = position;
            transform.localScale = Vector3.Lerp(startScale, trayScale, Mathf.SmoothStep(0f, 1f, progress));
            SetWheelSpin(progress * 560f);

            if (arrivalVfx != null && progress > 0.10f && progress < 0.94f)
            {
                Vector3 smokeSegment = position - previousPosition;
                float segmentLength = smokeSegment.magnitude;
                float distanceAlongSegment = 0f;
                while (segmentLength > 0.0001f && smokeDistance + segmentLength >= smokeSpacing)
                {
                    float distanceToEmission = smokeSpacing - smokeDistance;
                    distanceAlongSegment += distanceToEmission;
                    segmentLength -= distanceToEmission;
                    smokeDistance = 0f;
                    float segmentProgress = Mathf.Clamp01(
                        distanceAlongSegment / Mathf.Max(0.0001f, smokeSegment.magnitude));
                    Vector3 emissionPosition = Vector3.Lerp(previousPosition, position, segmentProgress);
                    arrivalVfx.EmitArrivalSmoke(emissionPosition, tangent, trayScale.x);
                }
                smokeDistance += segmentLength;
            }
            else if (progress <= 0.10f)
            {
                // Do not carry the hidden first part of the route into one
                // large emission on the first visible smoke frame.
                smokeDistance = 0f;
            }
            previousPosition = position;
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
        transform.localScale = trayScale;

        // The recording settles from the last tilted frame to an upright pose
        // in about two frames, followed by a restrained 70 ms shape recovery.
        const float settleDuration = 0.070f;
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / settleDuration);
            float impact = progress < 0.42f
                ? Mathf.SmoothStep(0f, 1f, progress / 0.42f)
                : 1f - Mathf.SmoothStep(0f, 1f, (progress - 0.42f) / 0.58f);
            Vector3 squash = new Vector3(1.045f, 0.975f, 0.94f);
            transform.localScale = Vector3.Scale(trayScale, Vector3.Lerp(Vector3.one, squash, impact));
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
        transform.localScale = trayScale;
    }

    internal IEnumerator DriveToBoardSlot(Vector3 target, float duration)
    {
        Vector3 boardScale = Vector3.one * boardVisualScale;
        Quaternion boardRotation = Quaternion.LookRotation(
            CarPrototype3D.DirectionToWorld(Direction), Vector3.up);
        yield return DriveToPose(target, boardRotation, boardScale, duration);
    }

    internal IEnumerator DriveTowLiftTo(Vector3 target, float duration)
    {
        yield return DriveToPose(target, transform.rotation, transform.localScale, duration);
    }

    internal IEnumerator PulseTowSelection(float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI * 2f);
            transform.localScale = startScale * (1f + Mathf.Max(0f, pulse) * 0.16f);
            yield return null;
        }

        transform.localScale = startScale;
    }

    internal IEnumerator DriveToParkingSlot(Vector3 target, float duration)
    {
        Vector3 parkingScale = Vector3.one * GetParkingVisualScale();
        yield return DriveToPose(
            target,
            Quaternion.Euler(0f, 90f, 0f),
            parkingScale,
            duration,
            true);
    }

    private IEnumerator DriveToPose(
        Vector3 target,
        Quaternion targetRotation,
        Vector3 targetScale,
        float duration,
        bool preserveEntryVelocity = false)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;
        visualBaseScale = targetScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = preserveEntryVelocity
                // Unit starting derivative matches the preceding constant-
                // speed road leg; the cubic reaches zero only at the pose.
                ? progress + progress * progress - progress * progress * progress
                : Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, target, eased);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            transform.localScale = Vector3.Lerp(startScale, targetScale, eased);
            SetWheelSpin(progress * 520f);
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
        transform.localScale = targetScale;
    }

    internal void SetTrayPose(Vector3 position, bool animateArrival = false)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        visualBaseScale = Vector3.one * GetTrayVisualScale();
        transform.localScale = animateArrival ? Vector3.zero : visualBaseScale;
    }

    internal void SetBoardPose(Vector3 position)
    {
        Quaternion boxWorldRotation = revealBoxVisualRoot != null
            ? revealBoxVisualRoot.transform.rotation
            : Quaternion.identity;
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(CarPrototype3D.DirectionToWorld(Direction), Vector3.up);
        if (IsRevealCovered && revealBoxVisualRoot != null)
        {
            revealBoxVisualRoot.transform.rotation = usesLevel35MysteryBoxVisual
                ? Quaternion.identity
                : usesLevel50MysteryBoxVisual
                    ? boxWorldRotation
                    : CarPrototype3D.GetMechanicArtworkCameraRotation();
        }
        visualBaseScale = Vector3.one * boardVisualScale;
        transform.localScale = visualBaseScale;
    }

    internal void UpdateScaleSettings(float boardScale, float offBoardScale)
    {
        boardVisualScale = boardScale;
        offBoardVisualScale = offBoardScale;
    }

    internal void SetParkingPose(Vector3 position)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        visualBaseScale = Vector3.one * GetParkingVisualScale();
        transform.localScale = visualBaseScale;
    }

    private float GetParkingVisualScale()
    {
        return GetTrayVisualScale();
    }

    private float GetTrayVisualScale()
    {
        return CellLength > 1 ? boardVisualScale : offBoardVisualScale * 0.68f;
    }

    internal IEnumerator ArriveInTray()
    {
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.2f);
            float pop = progress < 0.72f
                ? Mathf.Lerp(0f, 1.12f, progress / 0.72f)
                : Mathf.Lerp(1.12f, 1f, (progress - 0.72f) / 0.28f);
            transform.localScale = visualBaseScale * pop;
            yield return null;
        }
        transform.localScale = visualBaseScale;
    }

    internal IEnumerator Despawn(float duration)
    {
        float elapsed = 0f;
        Vector3 start = transform.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, Vector3.zero, elapsed / duration);
            yield return null;
        }
        SetVisible(false);
    }

    internal IEnumerator CelebrateAndDespawn()
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.3f);
            float scale = progress < 0.25f
                ? Mathf.Lerp(1f, 1.18f, progress / 0.25f)
                : Mathf.Lerp(1.18f, 0f, (progress - 0.25f) / 0.75f);
            transform.localScale = start * scale;
            yield return null;
        }
        SetVisible(false);
    }

    internal IEnumerator RiseIntoMergeFormation(Vector3 target, float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0f, 180f, 0f);
        Vector3 startScale = transform.localScale;
        Vector3 raisedScale = visualBaseScale * 1.035f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float lift = Mathf.SmoothStep(0f, 1f, progress);
            float convergeProgress = Mathf.Clamp01((progress - 0.28f) / 0.72f);
            float converge = Mathf.SmoothStep(0f, 1f, convergeProgress);
            Vector3 position = startPosition;
            position.x = Mathf.Lerp(startPosition.x, target.x, converge);
            position.z = Mathf.Lerp(startPosition.z, target.z, converge);
            position.y = Mathf.Lerp(startPosition.y, target.y, lift)
                + Mathf.Sin(progress * Mathf.PI) * 0.11f;
            transform.position = position;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, converge);
            transform.localScale = Vector3.Lerp(startScale, raisedScale, lift);
            yield return null;
        }

        transform.position = target;
        transform.rotation = targetRotation;
        transform.localScale = raisedScale;
    }

    internal IEnumerator CompressIntoMergeCore(Vector3 target, float duration, bool keepAsCore)
    {
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        Vector3 compressedCoreScale = new Vector3(
            startScale.x * 0.76f,
            startScale.y * 0.58f,
            startScale.z * 0.14f);
        Vector3 targetScale = keepAsCore
            ? compressedCoreScale
            : startScale * 0.44f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            transform.position = Vector3.Lerp(startPosition, target, eased);
            transform.rotation = startRotation;
            transform.localScale = Vector3.Lerp(startScale, targetScale, eased);
            yield return null;
        }

        transform.position = target;
        transform.rotation = startRotation;
        transform.localScale = targetScale;
        if (!keepAsCore) SetVisible(false);
    }

    internal IEnumerator CollapseMergeCore(float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        SetVisible(false);
    }

    internal void SetVisible(bool visible)
    {
        if (!visible && policeLightController != null) policeLightController.StopFlashing();
        CacheVisibilityComponents();
        for (int index = 0; index < cachedRenderers.Length; index++)
            if (cachedRenderers[index] != null) cachedRenderers[index].enabled = visible;
        for (int index = 0; index < cachedColliders.Length; index++)
            if (cachedColliders[index] != null) cachedColliders[index].enabled = visible;
    }

    internal void SetTutorialDimmed(bool dimmed)
    {
        if (tutorialDimmed == dimmed) return;
        tutorialDimmed = dimmed;
        CacheVisibilityComponents();
        if (tutorialPropertyBlock == null)
            tutorialPropertyBlock = new MaterialPropertyBlock();

        for (int index = 0; index < cachedRenderers.Length; index++)
        {
            Renderer renderer = cachedRenderers[index];
            if (renderer == null) continue;

            tutorialPropertyBlock.Clear();
            if (dimmed)
            {
                // Preserve each car's authored hue. The previous multiplier
                // made red, blue, and purple cars indistinguishable on phones.
                Color dimColor = new Color(0.66f, 0.68f, 0.74f, 1f);
                tutorialPropertyBlock.SetColor(BaseColorProperty, dimColor);
                tutorialPropertyBlock.SetColor(ColorProperty, dimColor);
                tutorialPropertyBlock.SetColor(EmissionColorProperty, Color.black);
            }
            renderer.SetPropertyBlock(tutorialPropertyBlock);
        }
    }

    private void CacheVisibilityComponents()
    {
        if (cachedRenderers == null)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        if (cachedColliders == null)
            cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    internal void BeginExitPoliceLights()
    {
        if (policeLightController != null) policeLightController.BeginExitFlash();
    }

    internal void Reject()
    {
        if (policeLightController != null) policeLightController.FlashBriefly(0.62f);
        StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < 0.16f)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 82f) * 0.09f * (1f - elapsed / 0.16f);
            transform.position = start + transform.right * offset;
            yield return null;
        }
        transform.position = start;
    }

    private void BuildCar(CarPrototype3D.PieceColor pieceColor)
    {
        if (pieceColor == CarPrototype3D.PieceColor.Red && BuildImportedRedCar())
            return;
        if (pieceColor == CarPrototype3D.PieceColor.Green && BuildImportedGreenCar())
            return;
        if (pieceColor == CarPrototype3D.PieceColor.Blue && BuildImportedBlueCar())
            return;
        if (pieceColor == CarPrototype3D.PieceColor.Purple && BuildImportedPurpleCar())
            return;
        if (pieceColor == CarPrototype3D.PieceColor.Pink && BuildImportedPinkCar())
            return;
        if (pieceColor == CarPrototype3D.PieceColor.Yellow && BuildImportedYellowCar())
            return;

        Color bodyColor;
        switch (pieceColor)
        {
            case CarPrototype3D.PieceColor.Red:
                bodyColor = new Color(0.92f, 0.18f, 0.16f);
                break;
            case CarPrototype3D.PieceColor.Blue:
                bodyColor = new Color(0.10f, 0.58f, 0.96f);
                break;
            case CarPrototype3D.PieceColor.Purple:
                bodyColor = new Color(0.57f, 0.11f, 0.88f);
                break;
            case CarPrototype3D.PieceColor.Yellow:
                bodyColor = new Color(0.96f, 0.74f, 0.12f);
                break;
            case CarPrototype3D.PieceColor.Pink:
                bodyColor = new Color(1f, 0.18f, 0.62f);
                break;
            default:
                bodyColor = new Color(0.18f, 0.74f, 0.32f);
                break;
        }
        CreateLocalBox("Body", new Vector3(0f, 0.22f, 0f), new Vector3(1.55f, 0.45f, 2.1f), bodyColor);
        CreateLocalBox("Cabin", new Vector3(0f, 0.56f, 0.08f), new Vector3(1.18f, 0.35f, 0.98f), Color.Lerp(bodyColor, Color.white, 0.42f));
        CreateLocalBox("Windshield", new Vector3(0f, 0.74f, 0.47f), new Vector3(1.02f, 0.18f, 0.24f), new Color(0.12f, 0.28f, 0.4f));
        CreateLocalBox("Front Bumper", new Vector3(0f, 0.19f, 1.08f), new Vector3(1.2f, 0.16f, 0.12f), new Color(0.08f, 0.11f, 0.16f));
        CreateLocalBox("Left Headlight", new Vector3(-0.48f, 0.36f, 1.08f), new Vector3(0.26f, 0.17f, 0.1f), new Color(1f, 0.89f, 0.52f));
        CreateLocalBox("Right Headlight", new Vector3(0.48f, 0.36f, 1.08f), new Vector3(0.26f, 0.17f, 0.1f), new Color(1f, 0.89f, 0.52f));

        CreateWheel(new Vector3(-0.78f, 0.06f, -0.62f));
        CreateWheel(new Vector3(0.78f, 0.06f, -0.62f));
        CreateWheel(new Vector3(-0.78f, 0.06f, 0.62f));
        CreateWheel(new Vector3(0.78f, 0.06f, 0.62f));
    }

    private void BuildLimousine(CarPrototype3D.PieceColor pieceColor)
    {
        usesLimousineVisual = true;
        if (BuildImportedLimousine(pieceColor))
            return;

        Color bodyColor = GetProceduralBodyColor(pieceColor);
        Color glass = new Color(0.08f, 0.20f, 0.31f);
        Color trim = new Color(0.96f, 0.72f, 0.20f);
        Color dark = new Color(0.045f, 0.06f, 0.085f);

        CreateLocalBox("Limousine Body", new Vector3(0f, 0.22f, 0f), new Vector3(1.52f, 0.45f, 4.42f), bodyColor);
        CreateLocalBox("Limousine Cabin", new Vector3(0f, 0.58f, -0.12f), new Vector3(1.16f, 0.38f, 2.62f), Color.Lerp(bodyColor, Color.white, 0.26f));
        CreateLocalBox("Limousine Roof", new Vector3(0f, 0.80f, -0.18f), new Vector3(1.06f, 0.12f, 2.20f), Color.Lerp(bodyColor, Color.white, 0.12f));
        CreateLocalBox("Limousine Windshield", new Vector3(0f, 0.78f, 1.23f), new Vector3(1.04f, 0.20f, 0.25f), glass);
        CreateLocalBox("Limousine Rear Window", new Vector3(0f, 0.76f, -1.46f), new Vector3(1.02f, 0.18f, 0.22f), glass);
        CreateLocalBox("Limousine Left Window Strip", new Vector3(-0.585f, 0.66f, -0.12f), new Vector3(0.055f, 0.25f, 2.28f), glass);
        CreateLocalBox("Limousine Right Window Strip", new Vector3(0.585f, 0.66f, -0.12f), new Vector3(0.055f, 0.25f, 2.28f), glass);
        CreateLocalBox("Limousine Left Gold Trim", new Vector3(-0.755f, 0.38f, 0f), new Vector3(0.045f, 0.07f, 3.72f), trim);
        CreateLocalBox("Limousine Right Gold Trim", new Vector3(0.755f, 0.38f, 0f), new Vector3(0.045f, 0.07f, 3.72f), trim);
        CreateLocalBox("Limousine Front Bumper", new Vector3(0f, 0.18f, 2.25f), new Vector3(1.24f, 0.16f, 0.12f), dark);
        CreateLocalBox("Limousine Rear Bumper", new Vector3(0f, 0.18f, -2.25f), new Vector3(1.24f, 0.16f, 0.12f), dark);
        CreateLocalBox("Limousine Left Headlight", new Vector3(-0.48f, 0.36f, 2.24f), new Vector3(0.27f, 0.17f, 0.10f), new Color(1f, 0.91f, 0.55f));
        CreateLocalBox("Limousine Right Headlight", new Vector3(0.48f, 0.36f, 2.24f), new Vector3(0.27f, 0.17f, 0.10f), new Color(1f, 0.91f, 0.55f));

        float[] axlePositions = { -1.62f, 0f, 1.62f };
        for (int index = 0; index < axlePositions.Length; index++)
        {
            CreateWheel(new Vector3(-0.78f, 0.06f, axlePositions[index]));
            CreateWheel(new Vector3(0.78f, 0.06f, axlePositions[index]));
        }
    }

    private bool BuildImportedLimousine(CarPrototype3D.PieceColor pieceColor)
    {
        string resourcePath;
        GameObject cachedPrefab;
        switch (pieceColor)
        {
            case CarPrototype3D.PieceColor.Red:
                resourcePath = ImportedRedLimousineResourcePath;
                cachedPrefab = importedRedLimousinePrefab;
                break;
            case CarPrototype3D.PieceColor.Green:
                resourcePath = ImportedGreenLimousineResourcePath;
                cachedPrefab = importedGreenLimousinePrefab;
                break;
            case CarPrototype3D.PieceColor.Purple:
                resourcePath = ImportedPurpleLimousineResourcePath;
                cachedPrefab = importedPurpleLimousinePrefab;
                break;
            case CarPrototype3D.PieceColor.Yellow:
                resourcePath = ImportedYellowLimousineResourcePath;
                cachedPrefab = importedYellowLimousinePrefab;
                break;
            case CarPrototype3D.PieceColor.Blue:
                resourcePath = ImportedBlueLimousineResourcePath;
                cachedPrefab = importedBlueLimousinePrefab;
                break;
            case CarPrototype3D.PieceColor.Pink:
                resourcePath = ImportedPinkLimousineResourcePath;
                cachedPrefab = importedPinkLimousinePrefab;
                break;
            default:
                return false;
        }

        if (cachedPrefab == null)
            cachedPrefab = Resources.Load<GameObject>(resourcePath);
        if (cachedPrefab == null)
            return false;

        switch (pieceColor)
        {
            case CarPrototype3D.PieceColor.Red:
                importedRedLimousinePrefab = cachedPrefab;
                break;
            case CarPrototype3D.PieceColor.Green:
                importedGreenLimousinePrefab = cachedPrefab;
                break;
            case CarPrototype3D.PieceColor.Purple:
                importedPurpleLimousinePrefab = cachedPrefab;
                break;
            case CarPrototype3D.PieceColor.Yellow:
                importedYellowLimousinePrefab = cachedPrefab;
                break;
            case CarPrototype3D.PieceColor.Blue:
                importedBlueLimousinePrefab = cachedPrefab;
                break;
            case CarPrototype3D.PieceColor.Pink:
                importedPinkLimousinePrefab = cachedPrefab;
                break;
        }

        GameObject model = Instantiate(cachedPrefab, transform, false);
        model.name = $"Imported {pieceColor} Limousine Model";
        model.transform.localPosition = Vector3.zero;
        // These four exports share the same geometry: the hood points toward
        // local +Z, which is also gameplay-forward for every puzzle piece.
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            Destroy(importedCamera.gameObject);
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
            Destroy(importedLight.gameObject);
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
            Destroy(importedCollider);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                materials[materialIndex] = GetImportedLimousineMaterial(
                    pieceColor,
                    materials[materialIndex]);
            }
            renderer.sharedMaterials = materials;
            ConfigureImportedCarRenderer(renderer);
        }

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        // Preserve the established two-cell footprint regardless of the
        // different world-space X offsets stored in the four Blender exports.
        const float targetWidth = 1.52f;
        const float targetLength = 4.42f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        model.transform.localScale = Vector3.one * Mathf.Min(widthScale, lengthScale);

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        return true;
    }

    private static Material GetImportedLimousineMaterial(
        CarPrototype3D.PieceColor pieceColor,
        Material sourceMaterial)
    {
        string sourceName = sourceMaterial != null
            ? sourceMaterial.name
            : "Body";
        string cacheKey = pieceColor + "::" + sourceName;
        if (importedLimousineMaterials.TryGetValue(cacheKey, out Material cachedMaterial)
            && cachedMaterial != null)
            return cachedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return sourceMaterial;

        // Material.006 is the shared dark wheel/tire group in all four OBJ
        // files. The other active material is the color-specific body.
        bool isTire = sourceName.Contains("Material.006");
        string texturePath = null;
        switch (pieceColor)
        {
            case CarPrototype3D.PieceColor.Red:
                texturePath = ImportedRedLimousineTexturePath;
                break;
            case CarPrototype3D.PieceColor.Green:
                texturePath = ImportedGreenLimousineTexturePath;
                break;
            case CarPrototype3D.PieceColor.Purple:
                texturePath = ImportedPurpleLimousineTexturePath;
                break;
            case CarPrototype3D.PieceColor.Yellow:
                texturePath = ImportedYellowLimousineTexturePath;
                break;
            case CarPrototype3D.PieceColor.Blue:
                texturePath = ImportedBlueLimousineTexturePath;
                break;
            case CarPrototype3D.PieceColor.Pink:
                texturePath = ImportedPinkLimousineTexturePath;
                break;
        }

        Texture2D bakedTexture = null;
        if (texturePath != null)
        {
            if (!importedLimousineTextures.TryGetValue(pieceColor, out bakedTexture)
                || bakedTexture == null)
            {
                bakedTexture = Resources.Load<Texture2D>(texturePath);
                importedLimousineTextures[pieceColor] = bakedTexture;
            }
        }

        Color color = bakedTexture != null
            ? Color.white
            : isTire
                ? new Color(0.025f, 0.022f, 0.030f)
                : GetProceduralBodyColor(pieceColor);
        Material material = new Material(shader)
        {
            name = $"{pieceColor} Limousine {sourceName} Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = color
        };
        material.enableInstancing = true;
        if (bakedTexture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", bakedTexture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", bakedTexture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);

            // The long side panels receive much less direct light than the
            // compact car bodies. A restrained atlas-driven emission lift
            // keeps the paint equally readable without flattening windows,
            // tires, trim, highlights, or the baked texture shading.
            if (material.HasProperty("_EmissionMap")
                && material.HasProperty("_EmissionColor"))
            {
                material.SetTexture("_EmissionMap", bakedTexture);
                material.SetColor("_EmissionColor", new Color(0.22f, 0.22f, 0.22f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
        }
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", isTire ? 0.02f : 0.08f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", isTire ? 0.18f : 0.60f);

        importedLimousineMaterials[cacheKey] = material;
        return material;
    }

    private static Color GetProceduralBodyColor(CarPrototype3D.PieceColor pieceColor)
    {
        switch (pieceColor)
        {
            case CarPrototype3D.PieceColor.Red: return new Color(0.92f, 0.18f, 0.16f);
            case CarPrototype3D.PieceColor.Blue: return new Color(0.10f, 0.58f, 0.96f);
            case CarPrototype3D.PieceColor.Purple: return new Color(0.57f, 0.11f, 0.88f);
            case CarPrototype3D.PieceColor.Yellow: return new Color(0.96f, 0.74f, 0.12f);
            case CarPrototype3D.PieceColor.Pink: return new Color(1f, 0.18f, 0.62f);
            default: return new Color(0.18f, 0.74f, 0.32f);
        }
    }

    private bool BuildImportedRedCar()
    {
        if (importedStandardCarPrefab == null)
            importedStandardCarPrefab = Resources.Load<GameObject>(ImportedStandardCarResourcePath);
        if (importedStandardCarPrefab == null) return false;

        GameObject model = Instantiate(importedStandardCarPrefab, transform, false);
        model.name = "Yellow Geometry Red Car Model";
        model.transform.localPosition = Vector3.zero;
        // Every standard color uses the Yellow Car export and its +Z hood.
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Camera[] importedCameras = model.GetComponentsInChildren<Camera>(true);
        for (int index = 0; index < importedCameras.Length; index++)
            Destroy(importedCameras[index].gameObject);

        Light[] importedLights = model.GetComponentsInChildren<Light>(true);
        for (int index = 0; index < importedLights.Length; index++)
            Destroy(importedLights[index].gameObject);

        Collider[] importedColliders = model.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < importedColliders.Length; index++)
            Destroy(importedColliders[index]);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Material[] materials = renderers[index].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GetImportedStandardCarMaterial(
                    CarPrototype3D.PieceColor.Red,
                    materials[materialIndex]);
            renderers[index].sharedMaterials = materials;
        }
        for (int index = 0; index < renderers.Length; index++)
            ConfigureImportedCarRenderer(renderers[index]);

        AddImportedStandardCarHeadlights(model.transform, CarPrototype3D.PieceColor.Red);
        AddImportedStandardCarSideWindowFrames(model.transform);
        usesImportedStandardCar = true;

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        float uniformScale = Mathf.Min(widthScale, lengthScale);
        model.transform.localScale = Vector3.one * uniformScale;

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        return true;
    }

    private void BuildImportedRedRearWindow()
    {
        GameObject frame = new GameObject(
            "Red Car Rear Window Frame",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        frame.transform.SetParent(transform, false);
        frame.GetComponent<MeshFilter>().sharedMesh = GetImportedRedRearWindowFrameMesh();
        Renderer frameRenderer = frame.GetComponent<MeshRenderer>();
        frameRenderer.sharedMaterial = GetImportedRedRearWindowFrameMaterial();
        ConfigureProceduralPartRenderer(frameRenderer, false);

        GameObject glass = new GameObject(
            "Red Car Rear Window Glass",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        glass.transform.SetParent(transform, false);
        glass.GetComponent<MeshFilter>().sharedMesh = GetImportedRedRearWindowGlassMesh();
        Renderer glassRenderer = glass.GetComponent<MeshRenderer>();
        glassRenderer.sharedMaterial = GetImportedRedRearWindowGlassMaterial();
        ConfigureProceduralPartRenderer(glassRenderer, false);
    }

    private static Mesh GetImportedRedRearWindowFrameMesh()
    {
        if (importedRedRearWindowFrameMesh != null)
            return importedRedRearWindowFrameMesh;

        importedRedRearWindowFrameMesh = CreateImportedRedRearWindowMesh(
            "Red Car Rear Window Frame Mesh",
            0.60f,
            0.51f,
            new Vector2(0.805f, -0.805f),
            new Vector2(0.493f, -0.982f),
            0.010f);
        return importedRedRearWindowFrameMesh;
    }

    private static Mesh GetImportedRedRearWindowGlassMesh()
    {
        if (importedRedRearWindowGlassMesh != null)
            return importedRedRearWindowGlassMesh;

        importedRedRearWindowGlassMesh = CreateImportedRedRearWindowMesh(
            "Red Car Rear Window Glass Mesh",
            0.54f,
            0.46f,
            new Vector2(0.772f, -0.826f),
            new Vector2(0.534f, -0.957f),
            0.020f);
        return importedRedRearWindowGlassMesh;
    }

    private static Mesh CreateImportedRedRearWindowMesh(
        string meshName,
        float roofHalfWidth,
        float rearHalfWidth,
        Vector2 roofYz,
        Vector2 rearYz,
        float surfaceOffset)
    {
        // These endpoints follow the normalized Blue Car hatch plane. The
        // glass mesh is inset inside the frame and raised by only one
        // additional centimetre along the measured surface normal, avoiding
        // both z-fighting and any visible floating gap.
        Vector3 surfaceNormal = new Vector3(0f, 0.492f, -0.871f);
        Vector3 offset = surfaceNormal * surfaceOffset;
        var mesh = new Mesh
        {
            name = meshName,
            vertices = new[]
            {
                new Vector3(-roofHalfWidth, roofYz.x, roofYz.y) + offset,
                new Vector3( roofHalfWidth, roofYz.x, roofYz.y) + offset,
                new Vector3( rearHalfWidth, rearYz.x, rearYz.y) + offset,
                new Vector3(-rearHalfWidth, rearYz.x, rearYz.y) + offset
            },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
            uv = new[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f)
            }
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }

    private static Material GetImportedRedRearWindowFrameMaterial()
    {
        if (importedRedRearWindowFrameMaterial != null)
            return importedRedRearWindowFrameMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Color frameColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        importedRedRearWindowFrameMaterial = new Material(shader)
        {
            name = "Red Car Rear Window Frame Material",
            color = frameColor,
            hideFlags = HideFlags.HideAndDontSave,
            enableInstancing = true
        };
        if (importedRedRearWindowFrameMaterial.HasProperty("_BaseColor"))
            importedRedRearWindowFrameMaterial.SetColor("_BaseColor", frameColor);
        if (importedRedRearWindowFrameMaterial.HasProperty("_Smoothness"))
            importedRedRearWindowFrameMaterial.SetFloat("_Smoothness", 0.34f);
        if (importedRedRearWindowFrameMaterial.HasProperty("_Cull"))
            importedRedRearWindowFrameMaterial.SetFloat("_Cull", 0f);
        return importedRedRearWindowFrameMaterial;
    }

    private static Material GetImportedRedRearWindowGlassMaterial()
    {
        if (importedRedRearWindowGlassMaterial != null)
            return importedRedRearWindowGlassMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Color glassColor = new Color(0.34f, 0.67f, 0.82f, 1f);
        importedRedRearWindowGlassMaterial = new Material(shader)
        {
            name = "Red Car Rear Window Glass Material",
            color = glassColor,
            hideFlags = HideFlags.HideAndDontSave,
            enableInstancing = true
        };
        if (importedRedRearWindowGlassMaterial.HasProperty("_BaseColor"))
            importedRedRearWindowGlassMaterial.SetColor("_BaseColor", glassColor);
        if (importedRedRearWindowGlassMaterial.HasProperty("_Metallic"))
            importedRedRearWindowGlassMaterial.SetFloat("_Metallic", 0.05f);
        if (importedRedRearWindowGlassMaterial.HasProperty("_Smoothness"))
            importedRedRearWindowGlassMaterial.SetFloat("_Smoothness", 0.72f);
        if (importedRedRearWindowGlassMaterial.HasProperty("_Cull"))
            importedRedRearWindowGlassMaterial.SetFloat("_Cull", 0f);
        return importedRedRearWindowGlassMaterial;
    }

    private bool BuildImportedGreenCar()
    {
        if (importedStandardCarPrefab == null)
            importedStandardCarPrefab = Resources.Load<GameObject>(ImportedStandardCarResourcePath);
        if (importedStandardCarPrefab == null) return false;

        GameObject model = Instantiate(importedStandardCarPrefab, transform, false);
        model.name = "Yellow Geometry Green Car Model";
        model.transform.localPosition = Vector3.zero;
        // Green uses the exact same Yellow Car geometry and forward orientation.
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Camera[] importedCameras = model.GetComponentsInChildren<Camera>(true);
        for (int index = 0; index < importedCameras.Length; index++)
            Destroy(importedCameras[index].gameObject);

        Light[] importedLights = model.GetComponentsInChildren<Light>(true);
        for (int index = 0; index < importedLights.Length; index++)
            Destroy(importedLights[index].gameObject);

        Collider[] importedColliders = model.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < importedColliders.Length; index++)
            Destroy(importedColliders[index]);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Material[] materials = renderers[index].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GetImportedStandardCarMaterial(
                    CarPrototype3D.PieceColor.Green,
                    materials[materialIndex]);
            renderers[index].sharedMaterials = materials;
        }
        for (int index = 0; index < renderers.Length; index++)
            ConfigureImportedCarRenderer(renderers[index]);

        AddImportedStandardCarHeadlights(model.transform, CarPrototype3D.PieceColor.Green);
        AddImportedStandardCarSideWindowFrames(model.transform);
        usesImportedStandardCar = true;

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        float uniformScale = Mathf.Min(widthScale, lengthScale);
        model.transform.localScale = Vector3.one * uniformScale;

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        return true;
    }

    private bool BuildImportedPurpleCar()
    {
        if (importedStandardCarPrefab == null)
            importedStandardCarPrefab = Resources.Load<GameObject>(ImportedStandardCarResourcePath);
        if (importedStandardCarPrefab == null) return false;

        GameObject model = Instantiate(importedStandardCarPrefab, transform, false);
        model.name = "Yellow Geometry Purple Car Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            Destroy(importedCamera.gameObject);
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
            Destroy(importedLight.gameObject);
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
            Destroy(importedCollider);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GetImportedStandardCarMaterial(
                    CarPrototype3D.PieceColor.Purple,
                    materials[materialIndex]);
            renderer.sharedMaterials = materials;
            ConfigureImportedCarRenderer(renderer);
        }

        AddImportedStandardCarHeadlights(model.transform, CarPrototype3D.PieceColor.Purple);
        AddImportedStandardCarSideWindowFrames(model.transform);
        usesImportedStandardCar = true;

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        model.transform.localScale = Vector3.one * Mathf.Min(widthScale, lengthScale);

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        return true;
    }

    private bool BuildImportedPinkCar()
    {
        if (importedPurpleCarPrefab == null)
            importedPurpleCarPrefab = Resources.Load<GameObject>(ImportedPurpleCarResourcePath);
        if (importedPurpleCarPrefab == null) return false;

        GameObject model = Instantiate(importedPurpleCarPrefab, transform, false);
        model.name = "Imported Pink Car Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            Destroy(importedCamera.gameObject);
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
            Destroy(importedLight.gameObject);
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
            Destroy(importedCollider);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GetImportedPinkCarMaterial(materials[materialIndex]);
            renderer.sharedMaterials = materials;
            ConfigureImportedCarRenderer(renderer);
        }

        // Pink deliberately reuses the purple model's geometry, scale and eye
        // placement while replacing only its coated body material.
        usesImportedPurpleCar = true;
        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds)) return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        model.transform.localScale = Vector3.one * Mathf.Min(widthScale, lengthScale);

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }
        return true;
    }

    private bool BuildImportedYellowCar()
    {
        if (importedStandardCarPrefab == null)
            importedStandardCarPrefab = Resources.Load<GameObject>(ImportedStandardCarResourcePath);
        if (importedStandardCarPrefab == null) return false;

        GameObject model = Instantiate(importedStandardCarPrefab, transform, false);
        model.name = "Imported Yellow Car Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            Destroy(importedCamera.gameObject);
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
            Destroy(importedLight.gameObject);
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
            Destroy(importedCollider);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GetImportedStandardCarMaterial(
                    CarPrototype3D.PieceColor.Yellow,
                    materials[materialIndex]);
            renderer.sharedMaterials = materials;
            ConfigureImportedCarRenderer(renderer);
        }

        AddImportedStandardCarHeadlights(model.transform, CarPrototype3D.PieceColor.Yellow);
        AddImportedStandardCarSideWindowFrames(model.transform);
        usesImportedStandardCar = true;

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        model.transform.localScale = Vector3.one * Mathf.Min(widthScale, lengthScale);

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        return true;
    }

    private bool BuildImportedBlueCar()
    {
        if (importedStandardCarPrefab == null)
            importedStandardCarPrefab = Resources.Load<GameObject>(ImportedStandardCarResourcePath);
        if (importedStandardCarPrefab == null) return false;

        GameObject model = Instantiate(importedStandardCarPrefab, transform, false);
        model.name = "Yellow Geometry Blue Car Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            Destroy(importedCamera.gameObject);
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
            Destroy(importedLight.gameObject);
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
            Destroy(importedCollider);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GetImportedStandardCarMaterial(
                    CarPrototype3D.PieceColor.Blue,
                    materials[materialIndex]);
            renderer.sharedMaterials = materials;
            ConfigureImportedCarRenderer(renderer);
        }

        AddImportedStandardCarHeadlights(model.transform, CarPrototype3D.PieceColor.Blue);
        AddImportedStandardCarSideWindowFrames(model.transform);
        usesImportedStandardCar = true;

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        model.transform.localScale = Vector3.one * Mathf.Min(widthScale, lengthScale);

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        return true;
    }

    private bool BuildImportedPoliceCar()
    {
        if (importedPoliceCarPrefab == null)
            importedPoliceCarPrefab = Resources.Load<GameObject>(ImportedPoliceCarResourcePath);
        GameObject policeCarPrefab = importedPoliceCarPrefab;
        if (policeCarPrefab == null) return false;

        GameObject model = Instantiate(policeCarPrefab, transform, false);
        model.name = "Imported Police Car Model";
        model.transform.localPosition = Vector3.zero;
        // This FBX already exports with its hood on local +Z, matching the
        // direction used by the puzzle movement code.
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Camera[] importedCameras = model.GetComponentsInChildren<Camera>(true);
        for (int index = 0; index < importedCameras.Length; index++)
            Destroy(importedCameras[index].gameObject);

        Light[] importedLights = model.GetComponentsInChildren<Light>(true);
        for (int index = 0; index < importedLights.Length; index++)
            Destroy(importedLights[index].gameObject);

        Collider[] importedColliders = model.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < importedColliders.Length; index++)
            Destroy(importedColliders[index]);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(model);
            return false;
        }

        Material redRoofLightMaterial = null;
        Material blueRoofLightMaterial = null;
        for (int index = 0; index < renderers.Length; index++)
        {
            Material[] materials = renderers[index].sharedMaterials;
            if (materials.Length == 0)
            {
                renderers[index].sharedMaterial = GetImportedPoliceCarMaterial(null);
                continue;
            }

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material sourceMaterial = materials[materialIndex];
                string sourceName = sourceMaterial != null ? sourceMaterial.name : string.Empty;
                Material runtimeMaterial = GetImportedPoliceCarMaterial(sourceMaterial);
                if (sourceName.Contains("Material.008"))
                {
                    runtimeMaterial = CreatePoliceRoofLightMaterial(
                        runtimeMaterial,
                        "Police Red Roof Light");
                    redRoofLightMaterial = runtimeMaterial;
                }
                else if (sourceName.Contains("Material.009"))
                {
                    runtimeMaterial = CreatePoliceRoofLightMaterial(
                        runtimeMaterial,
                        "Police Blue Roof Light");
                    blueRoofLightMaterial = runtimeMaterial;
                }
                materials[materialIndex] = runtimeMaterial;
            }
            renderers[index].sharedMaterials = materials;
        }
        for (int index = 0; index < renderers.Length; index++)
            ConfigureImportedCarRenderer(renderers[index]);

        if (!TryGetLocalRenderBounds(renderers, out Bounds bounds))
            return true;

        const float targetWidth = 1.55f;
        const float targetLength = 2.10f;
        float widthScale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
        float lengthScale = bounds.size.z > 0.001f ? targetLength / bounds.size.z : 1f;
        float uniformScale = Mathf.Min(widthScale, lengthScale);
        model.transform.localScale = Vector3.one * uniformScale;

        if (TryGetLocalRenderBounds(renderers, out bounds))
        {
            const float desiredWheelBottom = -0.22f;
            model.transform.localPosition += new Vector3(
                -bounds.center.x,
                desiredWheelBottom - bounds.min.y,
                -bounds.center.z);
        }

        policeLightController = gameObject.AddComponent<PoliceLightController>();
        policeLightController.Initialize(redRoofLightMaterial, blueRoofLightMaterial);

        return true;
    }

    private void BuildAnimatedEyes()
    {
        eyeController = gameObject.AddComponent<CarEyeController>();
        Vector3 windshieldPosition;
        Vector2 windshieldSize;
        Vector2 windshieldSurfaceScale;
        float windshieldTiltDegrees;

        if (usesLimousineVisual)
        {
            // Measured from the shared limousine export after its established
            // two-cell normalization: roof edge y/z 0.81/0.86, hood edge
            // 0.45/1.09, and full hood-edge width 1.13.
            windshieldPosition = new Vector3(0f, 0.625f, 0.975f);
            windshieldSize = new Vector2(1.132f, 0.421f);
            windshieldSurfaceScale = new Vector2(0.98f, 0.98f);
            windshieldTiltDegrees = 57.5f;
        }
        else if (usesImportedStandardCar || usesImportedPurpleCar)
        {
            // Measured from the normalized standard Yellow Car export. Pink
            // retains the same established windshield placement.
            windshieldPosition = new Vector3(0f, 0.644f, 0.195f);
            windshieldSize = new Vector2(1.154f, 0.423f);
            windshieldSurfaceScale = new Vector2(0.98f, 0.98f);
            windshieldTiltDegrees = 61.5f;
        }
        else
        {
            // The procedural windshield is an upright box face at local +Z.
            windshieldPosition = new Vector3(0f, 0.74f, 0.595f);
            windshieldSize = new Vector2(0.96f, 0.16f);
            windshieldSurfaceScale = new Vector2(0.98f, 0.94f);
            windshieldTiltDegrees = 90f;
        }

        eyeController.Initialize(
            windshieldPosition,
            windshieldSize,
            windshieldTiltDegrees,
            windshieldSurfaceScale,
            true);
    }

    private static Material GetImportedRedCarMaterial()
    {
        if (importedRedCarMaterial != null) return importedRedCarMaterial;

        Texture2D colorTexture = Resources.Load<Texture2D>(ImportedRedCarTexturePath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        importedRedCarMaterial = new Material(shader)
        {
            name = "Blue Car Red Paint Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white
        };
        importedRedCarMaterial.enableInstancing = true;

        if (colorTexture != null)
        {
            if (importedRedCarMaterial.HasProperty("_BaseMap")) importedRedCarMaterial.SetTexture("_BaseMap", colorTexture);
            if (importedRedCarMaterial.HasProperty("_MainTex")) importedRedCarMaterial.SetTexture("_MainTex", colorTexture);
        }

        // Keep the Blue Car material response. The derived atlas already holds
        // the red paint and all baked details, so no whole-material tint is
        // needed and neutral regions remain unchanged.
        if (importedRedCarMaterial.HasProperty("_BaseColor"))
            importedRedCarMaterial.SetColor("_BaseColor", Color.white);
        if (importedRedCarMaterial.HasProperty("_MetallicGlossMap"))
            importedRedCarMaterial.SetTexture("_MetallicGlossMap", null);
        if (importedRedCarMaterial.HasProperty("_OcclusionMap"))
            importedRedCarMaterial.SetTexture("_OcclusionMap", null);
        if (importedRedCarMaterial.HasProperty("_EmissionMap"))
            importedRedCarMaterial.SetTexture("_EmissionMap", null);
        importedRedCarMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");
        importedRedCarMaterial.DisableKeyword("_OCCLUSIONMAP");
        importedRedCarMaterial.DisableKeyword("_EMISSION");
        if (importedRedCarMaterial.HasProperty("_Metallic"))
            importedRedCarMaterial.SetFloat("_Metallic", 0.16f);
        if (importedRedCarMaterial.HasProperty("_Smoothness"))
            importedRedCarMaterial.SetFloat("_Smoothness", 0.56f);
        return importedRedCarMaterial;
    }

    private static Material GetImportedGreenCarMaterial()
    {
        if (importedGreenCarMaterial != null) return importedGreenCarMaterial;

        Texture2D colorTexture = Resources.Load<Texture2D>(ImportedGreenCarTexturePath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        importedGreenCarMaterial = new Material(shader)
        {
            name = "Blue Car Green Paint Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white
        };
        importedGreenCarMaterial.enableInstancing = true;

        if (colorTexture != null)
        {
            if (importedGreenCarMaterial.HasProperty("_BaseMap")) importedGreenCarMaterial.SetTexture("_BaseMap", colorTexture);
            if (importedGreenCarMaterial.HasProperty("_MainTex")) importedGreenCarMaterial.SetTexture("_MainTex", colorTexture);
        }

        if (importedGreenCarMaterial.HasProperty("_BaseColor"))
            importedGreenCarMaterial.SetColor("_BaseColor", Color.white);
        if (importedGreenCarMaterial.HasProperty("_MetallicGlossMap"))
            importedGreenCarMaterial.SetTexture("_MetallicGlossMap", null);
        if (importedGreenCarMaterial.HasProperty("_OcclusionMap"))
            importedGreenCarMaterial.SetTexture("_OcclusionMap", null);
        if (importedGreenCarMaterial.HasProperty("_EmissionMap"))
            importedGreenCarMaterial.SetTexture("_EmissionMap", null);
        importedGreenCarMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");
        importedGreenCarMaterial.DisableKeyword("_OCCLUSIONMAP");
        importedGreenCarMaterial.DisableKeyword("_EMISSION");
        if (importedGreenCarMaterial.HasProperty("_Metallic")) importedGreenCarMaterial.SetFloat("_Metallic", 0.16f);
        if (importedGreenCarMaterial.HasProperty("_Smoothness")) importedGreenCarMaterial.SetFloat("_Smoothness", 0.56f);
        return importedGreenCarMaterial;
    }

    private static Material GetImportedPoliceCarMaterial(Material sourceMaterial)
    {
        string sourceName = sourceMaterial != null ? sourceMaterial.name : "Police Body";
        if (importedPoliceCarMaterials.TryGetValue(sourceName, out Material cachedMaterial))
            return cachedMaterial;

        Texture sourceTexture = sourceMaterial != null
            ? sourceMaterial.mainTexture
            : Resources.Load<Texture2D>(ImportedPoliceCarTexturePath);
        if (sourceTexture == null)
        {
            if (sourceName.Contains("Purple_Coated"))
                sourceTexture = Resources.Load<Texture2D>(ImportedPoliceCarBodyTexturePath);
            else if (sourceName.Contains("Material.002"))
                sourceTexture = Resources.Load<Texture2D>(ImportedPoliceCarGlassTexturePath);
            else if (sourceName.Contains("Material.003"))
                sourceTexture = Resources.Load<Texture2D>(ImportedPoliceCarTrimTexturePath);
            else if (sourceName.Contains("Material.006"))
                sourceTexture = Resources.Load<Texture2D>(ImportedPoliceCarDetailTexturePath);
        }
        Color sourceColor = sourceMaterial != null ? sourceMaterial.color : Color.white;
        float metallic = 0.05f;
        float smoothness = 0.42f;
        if (sourceTexture != null && sourceTexture.name.Contains("Material.004"))
        {
            metallic = 0.02f;
            smoothness = 0.80f;
        }
        else if (sourceTexture != null && sourceTexture.name.Contains("Material-color"))
        {
            metallic = 0.02f;
            smoothness = 0.25f;
        }
        else if (sourceTexture != null && sourceTexture.name.Contains("Material.005"))
        {
            metallic = 0.06f;
            smoothness = 0.66f;
        }
        else if (sourceName.Contains("Purple_Coated"))
        {
            metallic = 0.30f;
            smoothness = 0.68f;
        }

        Material material = CreateImportedPoliceCarMaterial(
            "Police Car " + sourceName + " Runtime Material",
            sourceColor,
            sourceTexture,
            metallic,
            smoothness);
        importedPoliceCarMaterials[sourceName] = material;
        return material;
    }

    private static Material GetImportedPurpleCarMaterial(Material sourceMaterial)
    {
        string sourceName = sourceMaterial != null ? sourceMaterial.name : "Purple Coated Glitter";
        if (importedPurpleCarMaterials.TryGetValue(sourceName, out Material cachedMaterial))
            return cachedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Color color;
        float metallic;
        float smoothness;
        bool headlights;
        string texturePath;
        if (sourceName.Contains("Purple"))
        {
            color = new Color(0.57f, 0.11f, 0.88f);
            metallic = 0.32f;
            smoothness = 0.78f;
            headlights = false;
            texturePath = ImportedPurpleCarBodyTexturePath;
        }
        else if (sourceName.Contains("004"))
        {
            color = new Color(0.19f, 0.72f, 0.84f);
            metallic = 0.02f;
            smoothness = 0.80f;
            headlights = false;
            texturePath = ImportedPurpleCarGlassTexturePath;
        }
        else if (sourceName.Contains("005"))
        {
            color = new Color(0.90f, 0.97f, 1f);
            metallic = 0.06f;
            smoothness = 0.66f;
            headlights = true;
            texturePath = ImportedPurpleCarDetailTexturePath;
        }
        else
        {
            color = new Color(0.025f, 0.04f, 0.08f);
            metallic = 0.02f;
            smoothness = 0.25f;
            headlights = false;
            texturePath = ImportedPurpleCarTrimTexturePath;
        }

        Texture2D paintTexture = Resources.Load<Texture2D>(texturePath);
        bool hasPaintTexture = paintTexture != null;

        Material material = new Material(shader)
        {
            name = "Purple Car " + sourceName + " Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = hasPaintTexture ? Color.white : color
        };
        material.enableInstancing = true;
        if (hasPaintTexture)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", paintTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", paintTexture);
        }
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (headlights && material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(0.22f, 0.54f, 0.90f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        importedPurpleCarMaterials[sourceName] = material;
        return material;
    }

    private static Material GetImportedPinkCarMaterial(Material sourceMaterial)
    {
        string sourceName = sourceMaterial != null ? sourceMaterial.name : "Purple Coated Glitter";
        if (!sourceName.Contains("Purple"))
            return GetImportedPurpleCarMaterial(sourceMaterial);
        if (importedPinkCarMaterials.TryGetValue(sourceName, out Material cachedMaterial))
            return cachedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Color pink = new Color(1f, 0.12f, 0.58f);
        Material material = new Material(shader)
        {
            name = "Pink Car " + sourceName + " Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = pink
        };
        material.enableInstancing = true;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", pink);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.30f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.78f);
        importedPinkCarMaterials[sourceName] = material;
        return material;
    }

    private static Material GetImportedStandardCarMaterial(
        CarPrototype3D.PieceColor pieceColor,
        Material sourceMaterial)
    {
        string sourceName = sourceMaterial != null ? sourceMaterial.name : "Material.004";
        string cacheKey = pieceColor + ":" + sourceName;
        if (importedStandardCarMaterials.TryGetValue(cacheKey, out Material cachedMaterial))
            return cachedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        // Blender renumbers the material slots when the model is edited. The
        // current export uses Material.004 for the textured body and
        // Material.005 for the separate blue glass, while older exports used
        // .001/.002. Detect both versions so another export cannot silently
        // remove the yellow paint, windows, or baked headlights.
        bool namedPaintedBody =
            sourceName.Contains("Material.001") ||
            sourceName.Contains("Material.004");
        bool namedSeparateGlass =
            sourceName.Contains("Material.002") ||
            sourceName.Contains("Material.005");
        bool isPaintedAtlas =
            namedPaintedBody ||
            (!namedSeparateGlass && sourceMaterial != null && sourceMaterial.mainTexture != null);
        Texture2D colorTexture = isPaintedAtlas
            ? Resources.Load<Texture2D>(GetImportedStandardCarTexturePath(pieceColor))
            : null;
        // The untextured slot is the exported glass. Use the pale toy-car blue
        // seen in Blender instead of the darker raw MTL swatch.
        Color sourceColor = isPaintedAtlas
            ? Color.white
            : new Color(0.48f, 0.72f, 0.88f);

        Material material = new Material(shader)
        {
            name = pieceColor + " Standard Car " + sourceName + " Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = colorTexture != null ? Color.white : sourceColor
        };
        material.enableInstancing = true;
        if (colorTexture != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", colorTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", colorTexture);
        }
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", isPaintedAtlas ? 0.18f : 0.03f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", isPaintedAtlas ? 0.52f : 0.75f);

        importedStandardCarMaterials[cacheKey] = material;
        return material;
    }

    private static string GetImportedStandardCarTexturePath(
        CarPrototype3D.PieceColor pieceColor)
    {
        switch (pieceColor)
        {
            case CarPrototype3D.PieceColor.Red:
                return ImportedStandardCarRedTexturePath;
            case CarPrototype3D.PieceColor.Green:
                return ImportedStandardCarGreenTexturePath;
            case CarPrototype3D.PieceColor.Blue:
                return ImportedStandardCarBlueTexturePath;
            case CarPrototype3D.PieceColor.Purple:
                return ImportedStandardCarPurpleTexturePath;
            default:
                return ImportedStandardCarYellowTexturePath;
        }
    }

    private static Material GetImportedBlueCarMaterial()
    {
        if (importedBlueCarMaterial != null)
            return importedBlueCarMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Texture2D colorTexture = Resources.Load<Texture2D>(ImportedBlueCarTexturePath);
        importedBlueCarMaterial = new Material(shader)
        {
            name = "Blue Car Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white
        };
        importedBlueCarMaterial.enableInstancing = true;
        if (colorTexture != null)
        {
            if (importedBlueCarMaterial.HasProperty("_BaseMap"))
                importedBlueCarMaterial.SetTexture("_BaseMap", colorTexture);
            if (importedBlueCarMaterial.HasProperty("_MainTex"))
                importedBlueCarMaterial.SetTexture("_MainTex", colorTexture);
        }
        if (importedBlueCarMaterial.HasProperty("_Metallic"))
            importedBlueCarMaterial.SetFloat("_Metallic", 0.16f);
        if (importedBlueCarMaterial.HasProperty("_Smoothness"))
            importedBlueCarMaterial.SetFloat("_Smoothness", 0.56f);
        return importedBlueCarMaterial;
    }

    private static void AddImportedStandardCarHeadlights(
        Transform model,
        CarPrototype3D.PieceColor pieceColor)
    {
        // These points and normals are measured directly from the Yellow Car
        // hood triangles beneath the two painted recesses. Offset each thin
        // lens by only its half-thickness, then align its up axis to the hood
        // normal so it remains attached from both the top and side views.
        Vector3 leftNormal = new Vector3(-0.1381f, 0.7297f, 0.6697f).normalized;
        Vector3 rightNormal = new Vector3(0.1381f, 0.7297f, 0.6697f).normalized;
        const float lensSurfaceOffset = 0.002f;
        CreateImportedStandardHeadlight(
            model,
            pieceColor + " Left Headlight",
            new Vector3(-0.7130f, 0.2091f, 0.6592f) + leftNormal * lensSurfaceOffset,
            leftNormal);
        CreateImportedStandardHeadlight(
            model,
            pieceColor + " Right Headlight",
            new Vector3(0.7130f, 0.2091f, 0.6592f) + rightNormal * lensSurfaceOffset,
            rightNormal);
    }

    private static void CreateImportedStandardHeadlight(
        Transform model,
        string objectName,
        Vector3 localPosition,
        Vector3 localSurfaceNormal)
    {
        GameObject headlight = new GameObject(
            objectName,
            typeof(MeshFilter),
            typeof(MeshRenderer));
        headlight.transform.SetParent(model, false);
        headlight.transform.localPosition = localPosition;
        headlight.transform.localRotation = Quaternion.FromToRotation(
            Vector3.up,
            localSurfaceNormal);
        headlight.transform.localScale = new Vector3(0.195f, 1f, 0.185f);
        headlight.GetComponent<MeshFilter>().sharedMesh =
            GetImportedStandardHeadlightLensMesh();

        MeshRenderer renderer = headlight.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetImportedStandardHeadlightMaterial();
        ConfigureImportedCarRenderer(renderer);
    }

    private static Mesh GetImportedStandardHeadlightLensMesh()
    {
        if (importedStandardHeadlightLensMesh != null)
            return importedStandardHeadlightLensMesh;

        const int segmentCount = 32;
        Vector3[] vertices = new Vector3[segmentCount + 1];
        Vector3[] normals = new Vector3[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];
        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        for (int index = 0; index < segmentCount; index++)
        {
            float angle = index * Mathf.PI * 2f / segmentCount;
            vertices[index + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            normals[index + 1] = Vector3.up;

            int next = (index + 1) % segmentCount;
            triangles[index * 3] = 0;
            triangles[index * 3 + 1] = next + 1;
            triangles[index * 3 + 2] = index + 1;
        }

        importedStandardHeadlightLensMesh = new Mesh
        {
            name = "Standard Car Flush Headlight Lens Mesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            normals = normals,
            triangles = triangles
        };
        importedStandardHeadlightLensMesh.RecalculateBounds();
        return importedStandardHeadlightLensMesh;
    }

    private static void AddImportedStandardCarSideWindowFrames(Transform model)
    {
        GameObject frames = new GameObject(
            "Standard Car Side Window Frames",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        frames.transform.SetParent(model, false);
        frames.GetComponent<MeshFilter>().sharedMesh = GetImportedStandardSideWindowFrameMesh();

        MeshRenderer renderer = frames.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetImportedStandardSideWindowFrameMaterial();
        ConfigureImportedCarRenderer(renderer);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh GetImportedStandardSideWindowFrameMesh()
    {
        if (importedStandardSideWindowFrameMesh != null)
            return importedStandardSideWindowFrameMesh;

        List<Vector3> vertices = new List<Vector3>(64);
        List<int> triangles = new List<int>(96);

        // Coordinates are the four corrected window polygons from the Yellow
        // Car export, ordered with their normals facing out of the car.
        AddImportedStandardSideWindowFrame(
            vertices,
            triangles,
            new[]
            {
                new Vector3(-0.8844f, 0.3968f, -0.1912f),
                new Vector3(-0.6575f, 0.9228f, -0.5132f),
                new Vector3(-0.6637f, 0.9270f, -0.9964f),
                new Vector3(-0.8927f, 0.3916f, -0.9290f)
            });
        AddImportedStandardSideWindowFrame(
            vertices,
            triangles,
            new[]
            {
                new Vector3(-0.6843f, 0.9168f, -1.5234f),
                new Vector3(-0.8893f, 0.4038f, -1.6118f),
                new Vector3(-0.8929f, 0.3881f, -1.1266f),
                new Vector3(-0.6622f, 0.9270f, -1.1819f)
            });
        AddImportedStandardSideWindowFrame(
            vertices,
            triangles,
            new[]
            {
                new Vector3(0.8844f, 0.3968f, -0.1912f),
                new Vector3(0.8927f, 0.3916f, -0.9290f),
                new Vector3(0.6637f, 0.9270f, -0.9964f),
                new Vector3(0.6575f, 0.9228f, -0.5132f)
            });
        AddImportedStandardSideWindowFrame(
            vertices,
            triangles,
            new[]
            {
                new Vector3(0.6843f, 0.9168f, -1.5234f),
                new Vector3(0.6622f, 0.9270f, -1.1819f),
                new Vector3(0.8929f, 0.3881f, -1.1266f),
                new Vector3(0.8893f, 0.4038f, -1.6118f)
            });

        importedStandardSideWindowFrameMesh = new Mesh
        {
            name = "Standard Car Side Window Frame Mesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        importedStandardSideWindowFrameMesh.RecalculateNormals();
        importedStandardSideWindowFrameMesh.RecalculateBounds();
        return importedStandardSideWindowFrameMesh;
    }

    private static void AddImportedStandardSideWindowFrame(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3[] outerCorners)
    {
        Vector3 center = Vector3.zero;
        for (int index = 0; index < outerCorners.Length; index++)
            center += outerCorners[index];
        center /= outerCorners.Length;

        Vector3 normal = Vector3.Cross(
            outerCorners[1] - outerCorners[0],
            outerCorners[2] - outerCorners[0]).normalized;
        const float surfaceOffset = 0.003f;
        const float innerScale = 0.94f;
        for (int edge = 0; edge < outerCorners.Length; edge++)
        {
            int next = (edge + 1) % outerCorners.Length;
            Vector3 outerStart = outerCorners[edge] + normal * surfaceOffset;
            Vector3 outerEnd = outerCorners[next] + normal * surfaceOffset;
            Vector3 innerStart = Vector3.Lerp(center, outerCorners[edge], innerScale) + normal * surfaceOffset;
            Vector3 innerEnd = Vector3.Lerp(center, outerCorners[next], innerScale) + normal * surfaceOffset;
            int first = vertices.Count;
            vertices.Add(outerStart);
            vertices.Add(outerEnd);
            vertices.Add(innerEnd);
            vertices.Add(innerStart);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }
    }

    private static Material GetImportedStandardSideWindowFrameMaterial()
    {
        if (importedStandardSideWindowFrameMaterial != null)
            return importedStandardSideWindowFrameMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Color frameColor = new Color(0.055f, 0.070f, 0.090f, 1f);
        importedStandardSideWindowFrameMaterial = new Material(shader)
        {
            name = "Standard Car Side Window Frame Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = frameColor
        };
        importedStandardSideWindowFrameMaterial.enableInstancing = true;
        if (importedStandardSideWindowFrameMaterial.HasProperty("_BaseColor"))
            importedStandardSideWindowFrameMaterial.SetColor("_BaseColor", frameColor);
        if (importedStandardSideWindowFrameMaterial.HasProperty("_Metallic"))
            importedStandardSideWindowFrameMaterial.SetFloat("_Metallic", 0f);
        if (importedStandardSideWindowFrameMaterial.HasProperty("_Smoothness"))
            importedStandardSideWindowFrameMaterial.SetFloat("_Smoothness", 0.16f);
        return importedStandardSideWindowFrameMaterial;
    }

    private static Material GetImportedStandardHeadlightMaterial()
    {
        if (importedStandardHeadlightMaterial != null)
            return importedStandardHeadlightMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        importedStandardHeadlightMaterial = new Material(shader)
        {
            name = "Standard Car Headlight Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white
        };
        importedStandardHeadlightMaterial.enableInstancing = true;
        if (importedStandardHeadlightMaterial.HasProperty("_BaseColor"))
            importedStandardHeadlightMaterial.SetColor("_BaseColor", Color.white);
        if (importedStandardHeadlightMaterial.HasProperty("_Metallic"))
            importedStandardHeadlightMaterial.SetFloat("_Metallic", 0.02f);
        if (importedStandardHeadlightMaterial.HasProperty("_Smoothness"))
            importedStandardHeadlightMaterial.SetFloat("_Smoothness", 0.62f);
        if (importedStandardHeadlightMaterial.HasProperty("_EmissionColor"))
        {
            importedStandardHeadlightMaterial.SetColor(
                "_EmissionColor",
                new Color(0.12f, 0.12f, 0.12f, 1f));
            importedStandardHeadlightMaterial.EnableKeyword("_EMISSION");
            importedStandardHeadlightMaterial.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.None;
        }
        return importedStandardHeadlightMaterial;
    }

    private static Material CreateImportedPoliceCarMaterial(
        string materialName,
        Color color,
        Texture colorTexture,
        float metallic,
        float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            color = colorTexture != null ? Color.white : color
        };
        material.enableInstancing = true;

        if (colorTexture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", colorTexture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", colorTexture);
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static Material CreatePoliceRoofLightMaterial(
        Material sourceMaterial,
        string materialName)
    {
        if (sourceMaterial == null) return null;
        Material material = new Material(sourceMaterial)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };
        material.enableInstancing = true;
        return material;
    }

    private bool TryGetLocalRenderBounds(Renderer[] renderers, out Bounds localBounds)
    {
        localBounds = default;
        bool hasBounds = false;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Bounds worldBounds = renderers[rendererIndex].bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        return hasBounds;
    }

    private static void ConfigureImportedCarRenderer(Renderer renderer)
    {
        if (renderer == null) return;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void BuildDeliveryCrateCart()
    {
        Color metal = new Color(0.18f, 0.25f, 0.31f);
        Color darkMetal = new Color(0.055f, 0.07f, 0.09f);
        Color wood = new Color(0.74f, 0.40f, 0.17f);
        Color lightWood = new Color(0.94f, 0.63f, 0.29f);
        Color darkWood = new Color(0.46f, 0.24f, 0.09f);

        CreateLocalBox("Cart Platform", new Vector3(0f, 0.18f, 0f), new Vector3(1.34f, 0.16f, 1.28f), metal);
        CreateLocalBox("Delivery Crate", new Vector3(0f, 0.72f, 0.02f), new Vector3(1.08f, 0.92f, 1.02f), wood);
        CreateLocalBox("Crate Top", new Vector3(0f, 1.22f, 0.02f), new Vector3(1.16f, 0.10f, 1.10f), lightWood);

        CreateLocalBox("Crate Front Left Brace", new Vector3(-0.43f, 0.72f, 0.55f), new Vector3(0.11f, 0.88f, 0.07f), darkWood);
        CreateLocalBox("Crate Front Right Brace", new Vector3(0.43f, 0.72f, 0.55f), new Vector3(0.11f, 0.88f, 0.07f), darkWood);
        CreateLocalBox("Crate Front Cross Brace", new Vector3(0f, 0.72f, 0.56f), new Vector3(0.96f, 0.11f, 0.07f), darkWood);
        CreateLocalBox("Shipping Label", new Vector3(0f, 0.90f, 0.60f), new Vector3(0.42f, 0.27f, 0.035f), new Color(0.96f, 0.91f, 0.72f));

        CreateLocalBox("Cart Handle Left", new Vector3(-0.48f, 0.82f, -0.66f), new Vector3(0.08f, 1.05f, 0.08f), metal);
        CreateLocalBox("Cart Handle Right", new Vector3(0.48f, 0.82f, -0.66f), new Vector3(0.08f, 1.05f, 0.08f), metal);
        CreateLocalBox("Cart Handle Grip", new Vector3(0f, 1.34f, -0.66f), new Vector3(1.04f, 0.09f, 0.09f), metal);

        GameObject arrowShaft = CreateLocalBox("Direction Arrow Shaft", new Vector3(0f, 1.285f, 0.08f), new Vector3(0.12f, 0.035f, 0.48f), darkMetal);
        GameObject arrowLeft = CreateLocalBox("Direction Arrow Left", new Vector3(-0.085f, 1.285f, 0.29f), new Vector3(0.10f, 0.035f, 0.30f), darkMetal);
        GameObject arrowRight = CreateLocalBox("Direction Arrow Right", new Vector3(0.085f, 1.285f, 0.29f), new Vector3(0.10f, 0.035f, 0.30f), darkMetal);
        arrowShaft.transform.localRotation = Quaternion.identity;
        arrowLeft.transform.localRotation = Quaternion.Euler(0f, -42f, 0f);
        arrowRight.transform.localRotation = Quaternion.Euler(0f, 42f, 0f);

        CreateCartWheel(new Vector3(-0.61f, 0.08f, -0.42f));
        CreateCartWheel(new Vector3(0.61f, 0.08f, -0.42f));
        CreateCartWheel(new Vector3(-0.61f, 0.08f, 0.42f));
        CreateCartWheel(new Vector3(0.61f, 0.08f, 0.42f));
    }

    private GameObject CreateLocalBox(string objectName, Vector3 localPosition, Vector3 scale, Color color)
    {
        GameObject box = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        box.transform.localScale = scale;
        box.transform.SetParent(transform, false);
        box.transform.localPosition = localPosition;
        box.GetComponent<MeshFilter>().sharedMesh = CarPrototype3D.GetSharedCubeMesh();
        Renderer renderer = box.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetSharedProceduralCarMaterial(color);
        ConfigureProceduralPartRenderer(renderer, ShouldCastProceduralShadow(objectName));
        return box;
    }

    private void CreateCartWheel(Vector3 localPosition)
    {
        GameObject wheel = new GameObject("Cart Wheel", typeof(MeshFilter), typeof(MeshRenderer));
        wheel.transform.SetParent(transform, false);
        wheel.transform.localPosition = localPosition;
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheel.transform.localScale = new Vector3(0.17f, 0.10f, 0.17f);
        wheel.GetComponent<MeshFilter>().sharedMesh = CarPrototype3D.GetSharedCylinderMesh();
        Renderer renderer = wheel.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetSharedProceduralCarMaterial(new Color(0.04f, 0.05f, 0.065f));
        ConfigureProceduralPartRenderer(renderer, false);
    }

    private void CreateWheel(Vector3 localPosition)
    {
        GameObject wheel = new GameObject("Wheel", typeof(MeshFilter), typeof(MeshRenderer));
        wheel.transform.SetParent(transform, false);
        wheel.transform.localPosition = localPosition;
        wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        wheel.transform.localScale = new Vector3(0.32f, 0.14f, 0.32f);
        wheel.GetComponent<MeshFilter>().sharedMesh = CarPrototype3D.GetSharedCylinderMesh();
        Renderer renderer = wheel.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetSharedProceduralCarMaterial(new Color(0.035f, 0.045f, 0.06f));
        ConfigureProceduralPartRenderer(renderer, false);
        wheels.Add(wheel.transform);
    }

    private static Material GetSharedProceduralCarMaterial(Color color)
    {
        Color32 key = color;
        if (sharedProceduralCarMaterials.TryGetValue(key, out Material material) && material != null)
            return material;

        material = CarPrototype3D.CreateMaterial(color);
        material.name = $"Shared Mobile Car Material {key.r:X2}{key.g:X2}{key.b:X2}{key.a:X2}";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.enableInstancing = true;
        sharedProceduralCarMaterials[key] = material;
        return material;
    }

    private static void ConfigureProceduralPartRenderer(Renderer renderer, bool castsShadow)
    {
        if (renderer == null) return;
        renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = castsShadow;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static bool ShouldCastProceduralShadow(string objectName)
    {
        switch (objectName)
        {
            case "Body":
            case "Cabin":
            case "Limousine Body":
            case "Limousine Cabin":
            case "Limousine Roof":
            case "Cart Platform":
            case "Delivery Crate":
            case "Crate Top":
                return true;
            default:
                return false;
        }
    }

    private void SetWheelSpin(float degrees)
    {
        for (int index = 0; index < wheels.Count; index++)
            wheels[index].localRotation = Quaternion.Euler(90f, 0f, degrees);
    }
}

public sealed class PoliceLightController : MonoBehaviour
{
    private const float PulseInterval = 0.115f;
    private static readonly Color RedIdle = new Color(0.70f, 0.035f, 0.025f);
    private static readonly Color BlueIdle = new Color(0.025f, 0.25f, 0.76f);
    private static readonly Color RedActive = new Color(1f, 0.10f, 0.06f);
    private static readonly Color BlueActive = new Color(0.05f, 0.48f, 1f);

    private Material redMaterial;
    private Material blueMaterial;
    private Coroutine flashRoutine;
    private WaitForSeconds pulseDelay;
    private float briefFlashEndTime;
    private bool continuousFlash;
    private bool redPhase = true;
    private bool initialized;

    public void Initialize(Material redRoofMaterial, Material blueRoofMaterial)
    {
        if (initialized) return;
        if (redRoofMaterial == null || blueRoofMaterial == null) return;

        redMaterial = redRoofMaterial;
        blueMaterial = blueRoofMaterial;
        PrepareRoofLightMaterial(redMaterial);
        PrepareRoofLightMaterial(blueMaterial);
        pulseDelay = new WaitForSeconds(PulseInterval);
        initialized = true;
        SetIdle();
    }

    public void BeginExitFlash()
    {
        if (!initialized) return;
        continuousFlash = true;
        EnsureFlashing();
    }

    public void FlashBriefly(float duration)
    {
        if (!initialized) return;
        briefFlashEndTime = Mathf.Max(briefFlashEndTime, Time.time + Mathf.Max(PulseInterval, duration));
        EnsureFlashing();
    }

    public void StopFlashing()
    {
        continuousFlash = false;
        briefFlashEndTime = 0f;
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        SetIdle();
    }

    private void EnsureFlashing()
    {
        if (flashRoutine == null) flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        while (continuousFlash || Time.time < briefFlashEndTime)
        {
            SetPhase(redPhase);
            redPhase = !redPhase;
            yield return pulseDelay;
        }

        flashRoutine = null;
        SetIdle();
    }

    private static void PrepareRoofLightMaterial(Material material)
    {
        if (material == null) return;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.04f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.58f);
        if (material.HasProperty("_EmissionColor")) material.EnableKeyword("_EMISSION");
    }

    private void SetPhase(bool showRed)
    {
        SetMaterialState(redMaterial, showRed ? RedActive : RedIdle, showRed ? RedActive * 5.5f : Color.black);
        SetMaterialState(blueMaterial, showRed ? BlueIdle : BlueActive, showRed ? Color.black : BlueActive * 5.5f);
    }

    private void SetIdle()
    {
        SetMaterialState(redMaterial, RedIdle, Color.black);
        SetMaterialState(blueMaterial, BlueIdle, Color.black);
        redPhase = true;
    }

    private static void SetMaterialState(Material material, Color baseColor, Color emissionColor)
    {
        if (material == null) return;
        material.color = baseColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emissionColor);
    }

    private void OnDisable()
    {
        StopFlashing();
    }

    private void OnDestroy()
    {
        DestroyMaterial(redMaterial);
        DestroyMaterial(blueMaterial);
    }

    private static void DestroyMaterial(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }
}
