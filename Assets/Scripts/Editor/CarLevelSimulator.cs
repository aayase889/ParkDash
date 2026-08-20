using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the fixed boards with the same logical rules used by the 3D car game.
/// It searches legal board exits, tray matches, parking swaps, and the extra slot.
/// </summary>
public sealed class CarLevelSimulatorWindow : EditorWindow
{
    private const string DatabasePath = "Assets/Resources/ColorSortLevelDatabase.asset";

    private Vector2 scroll;
    private string report = "Run the first-60 check to validate every fixed 3D board.";
    private int boardNumber = 1;

    [MenuItem("Color Sort/3D Car Level Simulator")]
    public static void Open()
    {
        GetWindow<CarLevelSimulatorWindow>("3D Level Simulator");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("This checks legal exits, color matching, parking swaps, and the +Slot booster. A solved result means a player can finish without spending hearts on invalid taps.", MessageType.Info);

        if (GUILayout.Button("Validate Fixed Boards 1-60", GUILayout.Height(32f)))
            report = CarLevelSimulator.ValidateRange(0, 60);

        if (GUILayout.Button("Validate 1-60 Without +Slot", GUILayout.Height(28f)))
            report = CarLevelSimulator.ValidateStandardTrayRange(0, 60);

        EditorGUILayout.Space(6f);
        boardNumber = EditorGUILayout.IntField("Board", Mathf.Max(1, boardNumber));
        if (GUILayout.Button("Validate This Board"))
            report = CarLevelSimulator.ValidateRange(boardNumber - 1, 1);

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
}

public static class CarLevelSimulator
{
    private const int RedFlag = 1;
    private const int GreenFlag = 2;
    private const int BlueFlag = 4;
    private const int PurpleFlag = 8;
    private const int YellowFlag = 16;
    private const int PinkFlag = 32;
    // Breadth-first search gives concise solutions on normal boards. The focused
    // fallback prevents dense late boards from being rejected just because their
    // shortest-path state space is large.
    private const int MaxBreadthFirstStates = 500000;
    private const int MaxFocusedSearchStates = 2000000;

    private enum Token : byte
    {
        Trash,
        Red,
        Green,
        Blue,
        Purple,
        Yellow,
        Pink
    }

    private struct State
    {
        public int remainingMask;
        public string tray;
        public char parked;
        public int clearedFlags;
        public bool extraSlotUsed;
        public int parkingUses;

        public string Key()
        {
            return remainingMask + "|" + tray + "|" + parked + "|" + clearedFlags + "|" + (extraSlotUsed ? "1" : "0") + "|" + parkingUses;
        }
    }

    private struct Node
    {
        public State state;
        public int parent;
        public string action;

        public Node(State state, int parent, string action)
        {
            this.state = state;
            this.parent = parent;
            this.action = action;
        }
    }

    private sealed class Rules
    {
        public int size;
        public int baseTrayCapacity;
        public int redTarget;
        public int greenTarget;
        public int blueTarget;
        public int purpleTarget;
        public int yellowTarget;
        public int pinkTarget;
        public char adjacentMatchToken;
        public int goals;
        public Token[] tokens;
        public UnityGameManager.Direction[] directions;
        public int[] leadingCells;
        public int[] footprintMasks;
        public int parkingUseLimit = -1;
        public int[] lockRequirements;
        public int[] orderedGoalFlags;
    }

    [MenuItem("Color Sort/Validate First 60 Fixed 3D Boards")]
    public static void ValidateFirstSixty()
    {
        string report = ValidateRange(0, 60);
        Debug.Log(report);
    }

    [MenuItem("Color Sort/Validate First 60 Without +Slot")]
    public static void ValidateFirstSixtyWithoutExtraSlot()
    {
        string report = ValidateStandardTrayRangeOrThrow(0, 60);
        Debug.Log(report);
    }

    [MenuItem("Color Sort/Validate Post-30 Limousine Boards")]
    public static void ValidatePostThirtyLimousineLevels()
    {
        Debug.Log(ValidateRange(30, 20));
    }

    [MenuItem("Color Sort/Validate 5x5 Boards 51-60")]
    public static void ValidateFiveByFiveLevels()
    {
        Debug.Log(ValidateRange(50, 10));
    }

    [MenuItem("Color Sort/Validate Playable Samples 61-66")]
    public static void ValidatePlayableExperimentalSamples()
    {
        Debug.Log(ValidatePlayableExperimentalSampleRange());
    }

    public static string ValidatePlayableExperimentalSampleRange()
    {
        ColorSortLevelDatabase database = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>("Assets/Resources/ColorSortLevelDatabase.asset");
        if (database == null || database.levels == null || database.levels.Count < 56)
            return "Could not load the six base boards for temporary Levels 61-66.";

        var report = new StringBuilder();
        int solved = 0;
        for (int sampleIndex = 0; sampleIndex < 6; sampleIndex++)
        {
            UnityGameManager.LevelConfig baseBoard = database.levels[50 + sampleIndex];
            if (!TryCreateRules(baseBoard, out Rules rules, out string setupError))
            {
                report.AppendLine("Level " + (61 + sampleIndex) + ": INVALID DATA - " + setupError);
                continue;
            }

            ConfigureExperimentalRules(61 + sampleIndex, rules);
            string result = ValidateRules(61 + sampleIndex, rules, out bool isSolved);
            if (isSolved) solved++;
            report.AppendLine(result);
        }

        report.Insert(0, "Checked temporary Levels 61-66: " + solved + " solvable, " + (6 - solved) + " need attention.\n\n");
        return report.ToString();
    }

    public static string ValidateRange(int startIndex, int count)
    {
        return ValidateRangeInternal(startIndex, count, true, out _);
    }

    public static string ValidateStandardTrayRange(int startIndex, int count)
    {
        return ValidateRangeInternal(startIndex, count, false, out _);
    }

    public static string ValidateStandardTrayRangeOrThrow(int startIndex, int count)
    {
        string report = ValidateRangeInternal(startIndex, count, false, out int failed);
        if (failed > 0)
        {
            Debug.LogError(report);
            throw new InvalidOperationException("At least one fixed board is not proven solvable with the normal tray. See the simulator report.");
        }

        return report;
    }

    private static string ValidateRangeInternal(int startIndex, int count, bool allowExtraSlot, out int failed)
    {
        ColorSortLevelDatabase database = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>("Assets/Resources/ColorSortLevelDatabase.asset");
        if (database == null || database.levels == null)
        {
            failed = 1;
            return "Could not load Assets/Resources/ColorSortLevelDatabase.asset.";
        }

        int start = Mathf.Clamp(startIndex, 0, database.levels.Count);
        int end = Mathf.Min(start + Mathf.Max(1, count), database.levels.Count);
        var report = new StringBuilder();
        int solved = 0;
        failed = 0;

        for (int index = start; index < end; index++)
        {
            UnityGameManager.LevelConfig level = database.levels[index];
            string result = ValidateLevel(level, allowExtraSlot, out bool isSolved);
            if (isSolved) solved++;
            else failed++;
            report.AppendLine(result);
        }

        string mode = allowExtraSlot ? "all legal tools" : "normal tray + parking (no +Slot)";
        report.Insert(0, string.Format("Checked Boards {0}-{1} with {2}: {3} solvable, {4} need attention.\n\n", start + 1, end, mode, solved, failed));
        return report.ToString();
    }

    private static string ValidateLevel(UnityGameManager.LevelConfig level, bool allowExtraSlot, out bool isSolved)
    {
        if (!TryCreateRules(level, out Rules rules, out string setupError))
        {
            isSolved = false;
            return "Board " + level.id + ": INVALID DATA - " + setupError;
        }

        return ValidateRules(level.id, rules, out isSolved, true, allowExtraSlot);
    }

    private static string ValidateRules(int boardNumber, Rules rules, out bool isSolved, bool allowParking = true, bool allowExtraSlot = true)
    {
        var start = new State
        {
            remainingMask = (1 << rules.tokens.Length) - 1,
            tray = string.Empty,
            parked = '\0',
            clearedFlags = 0,
            extraSlotUsed = false
        };

        var nodes = new List<Node> { new Node(start, -1, string.Empty) };
        var frontier = new Queue<int>();
        var visited = new HashSet<string> { start.Key() };
        frontier.Enqueue(0);

        while (frontier.Count > 0)
        {
            int nodeIndex = frontier.Dequeue();
            State state = nodes[nodeIndex].state;
            if (state.clearedFlags == rules.goals)
            {
                isSolved = true;
                return BuildSolvedReport(boardNumber, nodes, nodeIndex, visited.Count);
            }

            if (visited.Count >= MaxBreadthFirstStates)
            {
                if (TryFocusedSolve(start, rules, allowParking, allowExtraSlot, out List<string> actions, out int focusedStates))
                {
                    isSolved = true;
                    return "Board " + boardNumber + ": SOLVABLE in " + actions.Count + " legal actions after a focused search of " + focusedStates + " states. " + string.Join(" -> ", actions.ToArray());
                }

                isSolved = false;
                return "Board " + boardNumber + ": INCONCLUSIVE after " + visited.Count + " breadth-first states and " + focusedStates + " focused states. Review this board before approving it.";
            }

            AddBoardMoves(state, nodeIndex, rules, nodes, frontier, visited);
            if (allowParking)
            {
                AddParkingMoves(state, nodeIndex, rules, nodes, frontier, visited);
            }

            if (allowExtraSlot && !state.extraSlotUsed && rules.baseTrayCapacity < 5)
            {
                State boosted = state;
                boosted.extraSlotUsed = true;
                AddState(boosted, nodeIndex, "Use +Slot", nodes, frontier, visited);
            }
        }

        isSolved = false;
        return "Board " + boardNumber + ": UNSOLVABLE after checking " + visited.Count + " legal states.";
    }

    private static bool TryCreateRules(UnityGameManager.LevelConfig level, out Rules rules, out string error)
    {
        rules = null;
        error = null;
        if (level == null || level.blocks == null)
        {
            error = "Missing board data.";
            return false;
        }

        if (level.boardSize < 3 || level.boardSize > 5 || level.blocks.Count != level.boardSize * level.boardSize)
        {
            error = "Expected a complete 3x3, 4x4, or 5x5 board.";
            return false;
        }

        MethodInfo buildPieces = typeof(CarPrototype3D).GetMethod("BuildPrototypePieceSpecs", BindingFlags.NonPublic | BindingFlags.Static);
        if (buildPieces == null)
        {
            error = "Missing 3D prototype piece builder.";
            return false;
        }

        Array specs = buildPieces.Invoke(null, new object[] { level }) as Array;
        if (specs == null || specs.Length == 0 || specs.Length > 25)
        {
            error = "The 3D prototype pieces could not be generated.";
            return false;
        }

        rules = new Rules
        {
            size = level.boardSize,
            baseTrayCapacity = level.matchTarget,
            tokens = new Token[specs.Length],
            directions = new UnityGameManager.Direction[specs.Length],
            leadingCells = new int[specs.Length],
            footprintMasks = new int[specs.Length],
            adjacentMatchToken = level.id <= 5 ? 'R' : level.id <= 10 ? 'P' : 'B'
        };

        int red = 0;
        int green = 0;
        int blue = 0;
        int purple = 0;
        int yellow = 0;
        int pink = 0;
        var occupied = new bool[level.boardSize * level.boardSize];

        for (int index = 0; index < specs.Length; index++)
        {
            object spec = specs.GetValue(index);
            Type specType = spec.GetType();
            int leadingRow = (int)specType.GetField("row").GetValue(spec);
            int leadingCol = (int)specType.GetField("col").GetValue(spec);
            int color = Convert.ToInt32(specType.GetField("color").GetValue(spec));
            int direction = Convert.ToInt32(specType.GetField("direction").GetValue(spec));
            int cellLength = (int)specType.GetField("cellLength").GetValue(spec);
            rules.tokens[index] = ConvertPrototypeToken(color);
            rules.directions[index] = (UnityGameManager.Direction)direction;
            rules.leadingCells[index] = leadingRow * rules.size + leadingCol;

            GetDirectionStep(rules.directions[index], out int rowStep, out int colStep);
            int footprintMask = 0;
            for (int footprintIndex = 0; footprintIndex < cellLength; footprintIndex++)
            {
                int row = leadingRow - rowStep * footprintIndex;
                int col = leadingCol - colStep * footprintIndex;
                if (row < 0 || row >= rules.size || col < 0 || col >= rules.size)
                {
                    error = "A prototype piece extends outside the board.";
                    return false;
                }

                int cell = row * rules.size + col;
                if (occupied[cell])
                {
                    error = "Two prototype pieces share one cell.";
                    return false;
                }

                occupied[cell] = true;
                footprintMask |= 1 << cell;
            }
            rules.footprintMasks[index] = footprintMask;

            switch (rules.tokens[index])
            {
                case Token.Red: red++; break;
                case Token.Green: green++; break;
                case Token.Blue: blue++; break;
                case Token.Purple: purple++; break;
                case Token.Yellow: yellow++; break;
                case Token.Pink: pink++; break;
            }
        }

        if (!IsAbsentOrExact(red, level.matchTarget) ||
            !IsAbsentOrExact(green, level.matchTarget) ||
            !IsAbsentOrExact(blue, level.matchTarget) ||
            !IsAbsentOrExact(purple, level.matchTarget) ||
            !IsAbsentOrExact(yellow, level.matchTarget) ||
            !IsAbsentOrExact(pink, level.matchTarget))
        {
            error =
                $"A non-police color count does not equal parking capacity {level.matchTarget}: " +
                $"R{red} G{green} B{blue} P{purple} Y{yellow} Pink{pink}.";
            return false;
        }

        rules.redTarget = red;
        rules.greenTarget = green;
        rules.blueTarget = blue;
        rules.purpleTarget = purple;
        rules.yellowTarget = yellow;
        rules.pinkTarget = pink;
        rules.lockRequirements = new int[specs.Length];
        rules.goals = 0;
        if (red > 0) rules.goals |= RedFlag;
        if (green > 0) rules.goals |= GreenFlag;
        if (blue > 0) rules.goals |= BlueFlag;
        if (purple > 0) rules.goals |= PurpleFlag;
        if (yellow > 0) rules.goals |= YellowFlag;
        if (pink > 0) rules.goals |= PinkFlag;
        return true;
    }

    private static bool IsAbsentOrExact(int count, int target)
    {
        return count == 0 || count == target;
    }

    private static void ConfigureExperimentalRules(int boardNumber, Rules rules)
    {
        if (boardNumber == 63) rules.parkingUseLimit = 5;
        if (boardNumber == 65)
            rules.orderedGoalFlags = new[] { RedFlag, GreenFlag, BlueFlag, YellowFlag };
        if (boardNumber == 66)
        {
            rules.parkingUseLimit = 4;
            rules.orderedGoalFlags = new[] { GreenFlag, RedFlag, YellowFlag, BlueFlag };
        }

        if (boardNumber == 64)
            LockPieceAtCell(rules, 3, 2, GreenFlag);
        if (boardNumber == 66)
        {
            LockPieceAtCell(rules, 0, 2, GreenFlag);
            LockPieceAtCell(rules, 3, 4, RedFlag);
        }
    }

    private static void LockPieceAtCell(Rules rules, int row, int col, int requiredGoalFlag)
    {
        int cellMask = 1 << (row * rules.size + col);
        for (int index = 0; index < rules.footprintMasks.Length; index++)
        {
            if ((rules.footprintMasks[index] & cellMask) == 0) continue;
            rules.lockRequirements[index] |= requiredGoalFlag;
            return;
        }

        throw new InvalidOperationException("No prototype piece occupies experimental lock cell " + row + "," + col + ".");
    }

    private static Token ConvertPrototypeToken(int color)
    {
        if (color == 0) return Token.Red;
        if (color == 1) return Token.Green;
        if (color == 2) return Token.Blue;
        if (color == 3) return Token.Purple;
        if (color == 4) return Token.Yellow;
        if (color == 6) return Token.Pink;
        return Token.Trash;
    }

    private static Token ConvertToken(UnityGameManager.BlockColor color)
    {
        switch (color)
        {
            case UnityGameManager.BlockColor.Red: return Token.Red;
            case UnityGameManager.BlockColor.Green: return Token.Green;
            case UnityGameManager.BlockColor.Blue: return Token.Blue;
            case UnityGameManager.BlockColor.Yellow: return Token.Yellow;
            default: return Token.Trash;
        }
    }

    private static void AddBoardMoves(State state, int parent, Rules rules, List<Node> nodes, Queue<int> frontier, HashSet<string> visited)
    {
        for (int pieceIndex = 0; pieceIndex < rules.tokens.Length; pieceIndex++)
        {
            if ((state.remainingMask & (1 << pieceIndex)) == 0 || !IsPathClear(pieceIndex, rules, state.remainingMask))
                continue;
            if (rules.lockRequirements != null
                && (state.clearedFlags & rules.lockRequirements[pieceIndex]) != rules.lockRequirements[pieceIndex])
                continue;

            Token token = rules.tokens[pieceIndex];
            State moved = state;
            moved.remainingMask &= ~(1 << pieceIndex);
            int cell = rules.leadingCells[pieceIndex];
            int row = cell / rules.size;
            int col = cell % rules.size;

            if (token == Token.Trash)
            {
                AddState(moved, parent, "Remove trash (" + row + "," + col + ")", nodes, frontier, visited);
                continue;
            }

            int capacity = rules.baseTrayCapacity + (state.extraSlotUsed ? 1 : 0);
            char incoming = ToChar(token);
            string action = "Exit " + incoming + " (" + row + "," + col + ")";
            if (state.tray.Length >= capacity)
            {
                if (!TryApplyDominantBoardExchange(
                        state,
                        incoming,
                        capacity,
                        rules,
                        out State exchanged))
                    continue;
                exchanged.remainingMask = moved.remainingMask;
                AddState(
                    exchanged,
                    parent,
                    action + " and park outlier",
                    nodes,
                    frontier,
                    visited);
                continue;
            }

            moved.tray += incoming;
            moved = ResolveOneMatch(moved, rules);
            AddState(moved, parent, action, nodes, frontier, visited);
        }
    }

    private static bool TryApplyDominantBoardExchange(
        State state,
        char incoming,
        int capacity,
        Rules rules,
        out State exchanged)
    {
        exchanged = state;
        bool canEnterParking = rules.parkingUseLimit < 0
            || state.parkingUses < rules.parkingUseLimit;
        int incomingFlag = FlagFor(incoming);
        if (!canEnterParking
            || state.parked != '\0'
            || capacity < 3
            || state.tray.Length != capacity
            || Count(state.tray, incoming) != capacity - 1
            || TargetFor(incoming, rules) != capacity
            || !CanClearGoal(state, rules, incomingFlag))
            return false;

        int outlierIndex = -1;
        for (int index = 0; index < state.tray.Length; index++)
        {
            if (state.tray[index] == incoming) continue;
            if (outlierIndex >= 0) return false;
            outlierIndex = index;
        }
        if (outlierIndex < 0) return false;

        exchanged.tray = ReplaceAt(state.tray, outlierIndex, incoming);
        exchanged.parked = state.tray[outlierIndex];
        if (rules.parkingUseLimit > 0) exchanged.parkingUses++;
        exchanged = ResolveOneMatch(exchanged, rules);
        return true;
    }

    private static void AddParkingMoves(State state, int parent, Rules rules, List<Node> nodes, Queue<int> frontier, HashSet<string> visited)
    {
        bool canEnterParking = rules.parkingUseLimit < 0 || state.parkingUses < rules.parkingUseLimit;
        for (int index = 0; index < state.tray.Length; index++)
        {
            if (!canEnterParking) break;
            State parked = state;
            char selected = state.tray[index];
            if (state.parked == '\0')
            {
                parked.tray = state.tray.Remove(index, 1);
                parked.parked = selected;
                if (rules.parkingUseLimit > 0) parked.parkingUses++;
                AddState(parked, parent, "Park " + selected, nodes, frontier, visited);
                continue;
            }

            parked.tray = ReplaceAt(state.tray, index, state.parked);
            parked.parked = selected;
            if (rules.parkingUseLimit > 0) parked.parkingUses++;
            parked = ResolveOneMatch(parked, rules);
            AddState(parked, parent, "Swap parked " + selected, nodes, frontier, visited);
        }

        if (state.parked == '\0') return;

        int capacity = rules.baseTrayCapacity + (state.extraSlotUsed ? 1 : 0);
        if (state.tray.Length < capacity)
        {
            State returned = state;
            returned.tray += returned.parked;
            returned.parked = '\0';
            returned = ResolveOneMatch(returned, rules);
            AddState(returned, parent, "Return parked car", nodes, frontier, visited);
            return;
        }

        int swapIndex = FindParkingSwapIndex(state, rules);
        if (swapIndex < 0 || !canEnterParking) return;

        State swapped = state;
        char trayCar = state.tray[swapIndex];
        swapped.tray = ReplaceAt(state.tray, swapIndex, state.parked);
        swapped.parked = trayCar;
        if (rules.parkingUseLimit > 0) swapped.parkingUses++;
        swapped = ResolveOneMatch(swapped, rules);
        AddState(swapped, parent, "Return parked car and swap", nodes, frontier, visited);
    }

    private static bool TryFocusedSolve(State start, Rules rules, bool allowParking, bool allowExtraSlot, out List<string> actions, out int explored)
    {
        var nodes = new List<Node> { new Node(start, -1, string.Empty) };
        var frontier = new Stack<int>();
        var visited = new HashSet<string> { start.Key() };
        frontier.Push(0);

        while (frontier.Count > 0 && visited.Count < MaxFocusedSearchStates)
        {
            int currentIndex = frontier.Pop();
            State current = nodes[currentIndex].state;
            if (current.clearedFlags == rules.goals)
            {
                actions = BuildActionList(nodes, currentIndex);
                explored = visited.Count;
                return true;
            }

            List<Node> candidates = GetCandidates(current, rules, allowParking, allowExtraSlot);
            candidates.Sort((left, right) => ScoreCandidate(right.state, current, rules).CompareTo(ScoreCandidate(left.state, current, rules)));
            for (int index = candidates.Count - 1; index >= 0; index--)
            {
                Node candidate = candidates[index];
                if (!visited.Add(candidate.state.Key())) continue;
                nodes.Add(new Node(candidate.state, currentIndex, candidate.action));
                frontier.Push(nodes.Count - 1);
            }
        }

        actions = null;
        explored = visited.Count;
        return false;
    }

    private static List<Node> GetCandidates(State state, Rules rules, bool allowParking, bool allowExtraSlot)
    {
        var nodes = new List<Node> { new Node(state, -1, string.Empty) };
        var frontier = new Queue<int>();
        var visited = new HashSet<string>();
        AddBoardMoves(state, 0, rules, nodes, frontier, visited);
        if (allowParking)
        {
            AddParkingMoves(state, 0, rules, nodes, frontier, visited);
        }

        if (allowExtraSlot && !state.extraSlotUsed && rules.baseTrayCapacity < 5)
        {
            State boosted = state;
            boosted.extraSlotUsed = true;
            AddState(boosted, 0, "Use +Slot", nodes, frontier, visited);
        }

        nodes.RemoveAt(0);
        return nodes;
    }

    private static int ScoreCandidate(State candidate, State previous, Rules rules)
    {
        int score = 0;
        score += CountBits(candidate.clearedFlags) * 100000;
        score += (CountBits(previous.remainingMask) - CountBits(candidate.remainingMask)) * 10000;
        score += candidate.tray.Length * 10;
        if (candidate.extraSlotUsed && !previous.extraSlotUsed) score -= 25;
        return score;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }

    private static int FindParkingSwapIndex(State state, Rules rules)
    {
        char parked = state.parked;
        if (parked == rules.adjacentMatchToken)
        {
            int adjacentTarget = TargetFor(parked, rules);
            for (int index = 0; index < state.tray.Length; index++)
            {
                if (state.tray[index] == parked) continue;
                int adjacentCount = 1;
                for (int left = index - 1; left >= 0 && state.tray[left] == parked; left--) adjacentCount++;
                for (int right = index + 1; right < state.tray.Length && state.tray[right] == parked; right++) adjacentCount++;
                if (adjacentCount >= adjacentTarget) return index;
            }
            return -1;
        }

        int target = TargetFor(parked, rules);
        int matching = 0;
        for (int index = 0; index < state.tray.Length; index++)
        {
            if (state.tray[index] == parked) matching++;
        }
        if (matching < target - 1) return -1;

        for (int index = 0; index < state.tray.Length; index++)
        {
            if (state.tray[index] != parked) return index;
        }
        return -1;
    }

    private static State ResolveOneMatch(State state, Rules rules)
    {
        char[] colorTokens = { 'R', 'G', 'B', 'P', 'Y', 'K' };
        int[] colorFlags = { RedFlag, GreenFlag, BlueFlag, PurpleFlag, YellowFlag, PinkFlag };
        for (int colorIndex = 0; colorIndex < colorTokens.Length; colorIndex++)
        {
            char colorToken = colorTokens[colorIndex];
            int colorFlag = colorFlags[colorIndex];
            int target = TargetFor(colorToken, rules);
            if (target <= 0 || !CanClearGoal(state, rules, colorFlag)) continue;

            if (colorToken == rules.adjacentMatchToken)
            {
                int start = FindAdjacentRun(state.tray, colorToken, target);
                if (start < 0) continue;
                state.tray = state.tray.Remove(start, target);
            }
            else
            {
                if (Count(state.tray, colorToken) < target) continue;
                state.tray = RemoveFirst(state.tray, colorToken, target);
            }

            state.clearedFlags |= colorFlag;
            return state;
        }

        return state;
    }

    private static bool CanClearGoal(State state, Rules rules, int goalFlag)
    {
        if ((state.clearedFlags & goalFlag) != 0) return false;
        if (rules.orderedGoalFlags == null || rules.orderedGoalFlags.Length == 0) return true;

        for (int index = 0; index < rules.orderedGoalFlags.Length; index++)
        {
            int orderedFlag = rules.orderedGoalFlags[index];
            if ((state.clearedFlags & orderedFlag) != 0) continue;
            return orderedFlag == goalFlag;
        }
        return false;
    }

    private static bool IsPathClear(int pieceIndex, Rules rules, int remainingMask)
    {
        int occupiedCells = 0;
        for (int index = 0; index < rules.tokens.Length; index++)
        {
            if (index == pieceIndex || (remainingMask & (1 << index)) == 0) continue;
            occupiedCells |= rules.footprintMasks[index];
        }

        int cell = rules.leadingCells[pieceIndex];
        UnityGameManager.Direction direction = rules.directions[pieceIndex];
        int size = rules.size;
        int row = cell / size;
        int col = cell % size;
        if (direction == UnityGameManager.Direction.Up)
        {
            for (int check = row - 1; check >= 0; check--) if ((occupiedCells & (1 << (check * size + col))) != 0) return false;
        }
        else if (direction == UnityGameManager.Direction.Down)
        {
            for (int check = row + 1; check < size; check++) if ((occupiedCells & (1 << (check * size + col))) != 0) return false;
        }
        else if (direction == UnityGameManager.Direction.Left)
        {
            for (int check = col - 1; check >= 0; check--) if ((occupiedCells & (1 << (row * size + check))) != 0) return false;
        }
        else
        {
            for (int check = col + 1; check < size; check++) if ((occupiedCells & (1 << (row * size + check))) != 0) return false;
        }
        return true;
    }

    private static void GetDirectionStep(UnityGameManager.Direction direction, out int rowStep, out int colStep)
    {
        rowStep = direction == UnityGameManager.Direction.Up ? -1 : direction == UnityGameManager.Direction.Down ? 1 : 0;
        colStep = direction == UnityGameManager.Direction.Left ? -1 : direction == UnityGameManager.Direction.Right ? 1 : 0;
    }

    private static void AddState(State state, int parent, string action, List<Node> nodes, Queue<int> frontier, HashSet<string> visited)
    {
        if (!visited.Add(state.Key())) return;
        nodes.Add(new Node(state, parent, action));
        frontier.Enqueue(nodes.Count - 1);
    }

    private static string BuildSolvedReport(int boardNumber, List<Node> nodes, int index, int explored)
    {
        List<string> actions = BuildActionList(nodes, index);
        return "Board " + boardNumber + ": SOLVABLE in " + actions.Count + " legal actions after exploring " + explored + " states. " + string.Join(" -> ", actions.ToArray());
    }

    private static List<string> BuildActionList(List<Node> nodes, int index)
    {
        var actions = new List<string>();
        for (int current = index; current >= 0 && nodes[current].parent >= 0; current = nodes[current].parent)
            actions.Add(nodes[current].action);
        actions.Reverse();
        return actions;
    }

    private static char ToChar(Token token)
    {
        if (token == Token.Red) return 'R';
        if (token == Token.Green) return 'G';
        if (token == Token.Blue) return 'B';
        if (token == Token.Purple) return 'P';
        if (token == Token.Yellow) return 'Y';
        if (token == Token.Pink) return 'K';
        return 'T';
    }

    private static int TargetFor(char token, Rules rules)
    {
        if (token == 'R') return rules.redTarget;
        if (token == 'G') return rules.greenTarget;
        if (token == 'B') return rules.blueTarget;
        if (token == 'P') return rules.purpleTarget;
        if (token == 'Y') return rules.yellowTarget;
        if (token == 'K') return rules.pinkTarget;
        return 0;
    }

    private static int FlagFor(char token)
    {
        if (token == 'R') return RedFlag;
        if (token == 'G') return GreenFlag;
        if (token == 'B') return BlueFlag;
        if (token == 'P') return PurpleFlag;
        if (token == 'Y') return YellowFlag;
        if (token == 'K') return PinkFlag;
        return 0;
    }

    private static int Count(string value, char token)
    {
        int total = 0;
        for (int index = 0; index < value.Length; index++) if (value[index] == token) total++;
        return total;
    }

    private static int FindAdjacentRun(string value, char token, int count)
    {
        for (int start = 0; start <= value.Length - count; start++)
        {
            bool match = true;
            for (int offset = 0; offset < count; offset++)
            {
                if (value[start + offset] == token) continue;
                match = false;
                break;
            }
            if (match) return start;
        }
        return -1;
    }

    private static string RemoveFirst(string value, char token, int count)
    {
        var builder = new StringBuilder(value.Length);
        int removed = 0;
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == token && removed < count)
            {
                removed++;
                continue;
            }
            builder.Append(value[index]);
        }
        return builder.ToString();
    }

    private static string ReplaceAt(string value, int index, char replacement)
    {
        var builder = new StringBuilder(value);
        builder[index] = replacement;
        return builder.ToString();
    }
}
