using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ColorSortLevelDesignValidator
{
    private const char EmptyPark = '-';
    private const int LevelCountToValidate = 60;

    private struct PlayState
    {
        public int mask;
        public string tray;
        public char park;
        public bool redCleared;
        public bool greenCleared;
        public bool blueCleared;
        public bool yellowCleared;

        public string Key()
        {
            return mask + "|" + tray + "|" + park + "|" +
                   (redCleared ? "1" : "0") +
                   (greenCleared ? "1" : "0") +
                   (blueCleared ? "1" : "0") +
                   (yellowCleared ? "1" : "0");
        }
    }

    [MenuItem("Color Sort/Validate Generated Levels")]
    public static void ValidateGeneratedLevelsMenu()
    {
        ValidateGeneratedLevelsBatch();
    }

    public static void ValidateGeneratedLevelsBatch()
    {
        List<UnityGameManager.LevelConfig> levels = ColorSortLevelBuilder.BuildLevels(LevelCountToValidate);
        ValidateLevels(levels);

        Debug.Log($"Generated level validation passed for {levels.Count} boards.");
    }

    public static void ValidateLevels(List<UnityGameManager.LevelConfig> levels)
    {
        ValidateLevelsInternal(levels, true);
    }

    public static void ValidateLevelStructure(List<UnityGameManager.LevelConfig> levels)
    {
        ValidateLevelsInternal(levels, false);
    }

    private static void ValidateLevelsInternal(List<UnityGameManager.LevelConfig> levels, bool validatePlayability)
    {
        var canonicalLayouts = new HashSet<string>();

        foreach (UnityGameManager.LevelConfig level in levels)
        {
            ValidateCounts(level);
            ValidateDirectionMix(level);
            ValidateNoFacingArrows(level);
            ValidateColorMix(level);
            ValidateDifficultyShape(level);
            ValidateUniqueLayout(level, canonicalLayouts);
            if (validatePlayability) ValidatePlayable(level);
        }
    }

    private static void ValidateCounts(UnityGameManager.LevelConfig level)
    {
        int red = 0;
        int green = 0;
        int blue = 0;
        int yellow = 0;
        int neutral = 0;

        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            if (block.color == UnityGameManager.BlockColor.Red) red++;
            else if (block.color == UnityGameManager.BlockColor.Green) green++;
            else if (block.color == UnityGameManager.BlockColor.Blue) blue++;
            else if (block.color == UnityGameManager.BlockColor.Yellow) yellow++;
            else neutral++;
        }

        int expectedNeutral = level.boardSize * level.boardSize - red - green - blue - yellow;
        bool valid = IsAbsentOrExact(red, level.matchTarget) &&
                     IsAbsentOrExact(green, level.matchTarget) &&
                     IsAbsentOrExact(blue, level.matchTarget) &&
                     IsAbsentOrExact(yellow, level.matchTarget) &&
                     neutral == expectedNeutral;

        if (!valid)
        {
            throw new Exception(
                $"{level.name} violates the parking-capacity principle (capacity {level.matchTarget}): " +
                $"R{red} G{green} B{blue} Y{yellow} Police{neutral}. " +
                "Every included non-police color must exactly equal the capacity.");
        }
    }

    private static bool IsAbsentOrExact(int colorCount, int parkingCapacity)
    {
        return colorCount == 0 || colorCount == parkingCapacity;
    }

    private static void ValidateDirectionMix(UnityGameManager.LevelConfig level)
    {
        var used = new HashSet<UnityGameManager.Direction>();
        var directions = new UnityGameManager.Direction[level.boardSize, level.boardSize];

        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            used.Add(block.direction);
            directions[block.row, block.col] = block.direction;
        }

        int requiredDirections = level.boardSize == 3 ? 3 : 4;
        if (used.Count < requiredDirections)
        {
            throw new Exception($"{level.name} does not use enough arrow directions.");
        }

        for (int r = 0; r < level.boardSize; r++)
        {
            bool same = true;
            for (int c = 1; c < level.boardSize; c++)
            {
                if (directions[r, c] != directions[r, 0]) same = false;
            }

            if (same) throw new Exception($"{level.name} has a same-direction row.");
        }

        for (int c = 0; c < level.boardSize; c++)
        {
            bool same = true;
            for (int r = 1; r < level.boardSize; r++)
            {
                if (directions[r, c] != directions[0, c]) same = false;
            }

            if (same) throw new Exception($"{level.name} has a same-direction column.");
        }
    }

    private static void ValidateNoFacingArrows(UnityGameManager.LevelConfig level)
    {
        var directions = new UnityGameManager.Direction[level.boardSize, level.boardSize];
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            directions[block.row, block.col] = block.direction;
        }

        for (int r = 0; r < level.boardSize; r++)
        {
            for (int c = 0; c < level.boardSize; c++)
            {
                if (c + 1 < level.boardSize &&
                    directions[r, c] == UnityGameManager.Direction.Right &&
                    directions[r, c + 1] == UnityGameManager.Direction.Left)
                {
                    throw new Exception($"{level.name} has horizontal arrows facing each other at {r},{c}.");
                }

                if (r + 1 < level.boardSize &&
                    directions[r, c] == UnityGameManager.Direction.Down &&
                    directions[r + 1, c] == UnityGameManager.Direction.Up)
                {
                    throw new Exception($"{level.name} has vertical arrows facing each other at {r},{c}.");
                }
            }
        }
    }

    private static void ValidateColorMix(UnityGameManager.LevelConfig level)
    {
        var colors = new UnityGameManager.BlockColor[level.boardSize, level.boardSize];
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            colors[block.row, block.col] = block.color;
        }

        for (int r = 0; r < level.boardSize; r++)
        {
            ValidateColorLine(level, colors, r, 0, 0, 1, "row");
        }

        for (int c = 0; c < level.boardSize; c++)
        {
            ValidateColorLine(level, colors, 0, c, 1, 0, "column");
        }
    }

    private static void ValidateColorLine(UnityGameManager.LevelConfig level, UnityGameManager.BlockColor[,] colors, int startRow, int startCol, int rowStep, int colStep, string lineName)
    {
        var counts = new Dictionary<UnityGameManager.BlockColor, int>();
        for (int i = 0; i < level.boardSize; i++)
        {
            UnityGameManager.BlockColor color = colors[startRow + rowStep * i, startCol + colStep * i];
            if (!counts.ContainsKey(color)) counts[color] = 0;
            counts[color]++;
        }

        foreach (var pair in counts)
        {
            if (pair.Key == UnityGameManager.BlockColor.Neutral && pair.Value == level.boardSize)
            {
                throw new Exception($"{level.name} has an all-grey {lineName}.");
            }

            if (pair.Key != UnityGameManager.BlockColor.Neutral && pair.Value >= Mathf.Min(3, level.boardSize))
            {
                throw new Exception($"{level.name} has too many {pair.Key} blocks in one {lineName}.");
            }
        }
    }

    private static void ValidateUniqueLayout(UnityGameManager.LevelConfig level, HashSet<string> canonicalLayouts)
    {
        string canonical = CanonicalColorLayout(ColorLayoutKey(level), level.boardSize);
        if (!canonicalLayouts.Add(canonical))
        {
            throw new Exception($"{level.name} repeats or mirrors an earlier color layout.");
        }
    }

    private static void ValidateDifficultyShape(UnityGameManager.LevelConfig level)
    {
        if (level.id <= 1) return;

        int size = level.boardSize;
        int count = size * size;
        int fullMask = (1 << count) - 1;
        var colors = new UnityGameManager.BlockColor[count];
        var directions = new UnityGameManager.Direction[count];
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            int index = block.row * size + block.col;
            colors[index] = block.color;
            directions[index] = block.direction;
        }

        int openingMoves = 0;
        var openingColors = new HashSet<UnityGameManager.BlockColor>();
        for (int index = 0; index < count; index++)
        {
            if (!IsPathClear(index, directions[index], fullMask, size)) continue;
            openingMoves++;
            if (colors[index] != UnityGameManager.BlockColor.Neutral)
            {
                openingColors.Add(colors[index]);
            }
        }

        int minimumMoves = size == 3 ? 2 : size == 4 ? 3 : 2;
        int maximumMoves = size == 3 ? 5 : size == 4 ? 7 : 12;
        if (openingMoves < minimumMoves || openingMoves > maximumMoves)
        {
            throw new Exception($"{level.name} has {openingMoves} legal opening exits; expected {minimumMoves}-{maximumMoves} for its difficulty tier.");
        }

        int minimumOpeningColors = level.id <= 5 || size >= 5 ? 1 : 2;
        if (openingColors.Count < minimumOpeningColors)
        {
            throw new Exception($"{level.name} does not present enough distinct colored opening choices.");
        }
    }

    private static void ValidatePlayable(UnityGameManager.LevelConfig level)
    {
        int size = level.boardSize;
        int count = size * size;
        int fullMask = (1 << count) - 1;
        int blueMatchTarget = CountColor(level, UnityGameManager.BlockColor.Blue);
        int yellowMatchTarget = CountColor(level, UnityGameManager.BlockColor.Yellow);
        bool blueActive = blueMatchTarget > 0;
        bool yellowActive = yellowMatchTarget > 0;
        var colors = new UnityGameManager.BlockColor[count];
        var directions = new UnityGameManager.Direction[count];

        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            int index = block.row * size + block.col;
            colors[index] = block.color;
            directions[index] = block.direction;
        }

        var stack = new Stack<PlayState>();
        var visited = new HashSet<string>();
        PlayState start = Normalize(new PlayState { mask = fullMask, tray = string.Empty, park = EmptyPark }, level.matchTarget, blueMatchTarget, yellowMatchTarget);
        stack.Push(start);
        visited.Add(start.Key());

        while (stack.Count > 0)
        {
            PlayState state = stack.Pop();
            foreach (PlayState parkingState in ExpandParkingClosure(state, level.matchTarget, blueMatchTarget, yellowMatchTarget))
            {
                if (IsSolved(parkingState, blueActive, yellowActive)) return;

                for (int index = 0; index < count; index++)
                {
                    if ((parkingState.mask & (1 << index)) == 0) continue;
                    if (!IsPathClear(index, directions[index], parkingState.mask, size)) continue;

                    PlayState next = parkingState;
                    next.mask &= ~(1 << index);

                    if (colors[index] != UnityGameManager.BlockColor.Neutral)
                    {
                        char incoming = ColorChar(colors[index]);
                        if (next.tray.Length >= level.matchTarget)
                        {
                            if (!TryApplyDominantBoardExchange(
                                    parkingState,
                                    incoming,
                                    level.matchTarget,
                                    blueMatchTarget,
                                    yellowMatchTarget,
                                    out next))
                                continue;
                            next.mask = parkingState.mask & ~(1 << index);
                        }
                        else
                        {
                            next.tray += incoming;
                        }
                    }

                    next = Normalize(next, level.matchTarget, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(next.Key()))
                    {
                        stack.Push(next);
                    }
                }
            }
        }

        throw new Exception($"{level.name} is not playable with the normal tray and parking rules.");
    }

    private static bool TryApplyDominantBoardExchange(
        PlayState state,
        char incoming,
        int capacity,
        int blueMatchTarget,
        int yellowMatchTarget,
        out PlayState exchanged)
    {
        exchanged = state;
        int incomingTarget = incoming == 'B'
            ? blueMatchTarget
            : incoming == 'Y' ? yellowMatchTarget : capacity;
        if (state.park != EmptyPark
            || capacity < 3
            || state.tray.Length != capacity
            || CountChar(state.tray, incoming) != capacity - 1
            || incomingTarget != capacity)
            return false;

        int outlierIndex = -1;
        for (int index = 0; index < state.tray.Length; index++)
        {
            if (state.tray[index] == incoming) continue;
            if (outlierIndex >= 0) return false;
            outlierIndex = index;
        }
        if (outlierIndex < 0) return false;

        char[] tray = state.tray.ToCharArray();
        exchanged.park = tray[outlierIndex];
        tray[outlierIndex] = incoming;
        exchanged.tray = new string(tray);
        exchanged = Normalize(
            exchanged,
            capacity,
            blueMatchTarget,
            yellowMatchTarget);
        return true;
    }

    private static IEnumerable<PlayState> ExpandParkingClosure(PlayState start, int capacity, int blueMatchTarget, int yellowMatchTarget)
    {
        var queue = new Queue<PlayState>();
        var visited = new HashSet<string>();
        PlayState normalized = Normalize(start, capacity, blueMatchTarget, yellowMatchTarget);
        queue.Enqueue(normalized);
        visited.Add(normalized.Key());

        while (queue.Count > 0)
        {
            PlayState state = queue.Dequeue();
            yield return state;

            if (state.park == EmptyPark)
            {
                for (int i = 0; i < state.tray.Length; i++)
                {
                    PlayState moved = state;
                    moved.park = state.tray[i];
                    moved.tray = state.tray.Remove(i, 1);
                    moved = Normalize(moved, capacity, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(moved.Key())) queue.Enqueue(moved);
                }
            }
            else
            {
                if (state.tray.Length < capacity)
                {
                    PlayState returned = state;
                    returned.tray += state.park;
                    returned.park = EmptyPark;
                    returned = Normalize(returned, capacity, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(returned.Key())) queue.Enqueue(returned);
                }

                for (int i = 0; i < state.tray.Length; i++)
                {
                    char[] trayChars = state.tray.ToCharArray();
                    char oldTrayChar = trayChars[i];
                    trayChars[i] = state.park;

                    PlayState swapped = state;
                    swapped.tray = new string(trayChars);
                    swapped.park = oldTrayChar;
                    swapped = Normalize(swapped, capacity, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(swapped.Key())) queue.Enqueue(swapped);
                }
            }
        }
    }

    private static PlayState Normalize(PlayState state, int matchTarget, int blueMatchTarget, int yellowMatchTarget)
    {
        bool blueActive = blueMatchTarget > 0;
        bool yellowActive = yellowMatchTarget > 0;
        bool changed = true;
        while (changed)
        {
            changed = false;
            if (!state.redCleared && CountChar(state.tray, 'R') >= matchTarget)
            {
                state.tray = RemoveFirstCount(state.tray, 'R', matchTarget);
                state.redCleared = true;
                changed = true;
            }

            if (!state.greenCleared && CountChar(state.tray, 'G') >= matchTarget)
            {
                state.tray = RemoveFirstCount(state.tray, 'G', matchTarget);
                state.greenCleared = true;
                changed = true;
            }

            if (blueActive && !state.blueCleared)
            {
                int blueRun = FindAdjacentBlueRun(state.tray, blueMatchTarget);
                if (blueRun >= 0)
                {
                    state.tray = state.tray.Remove(blueRun, blueMatchTarget);
                    state.blueCleared = true;
                    changed = true;
                }
            }

            if (yellowActive && !state.yellowCleared && CountChar(state.tray, 'Y') >= yellowMatchTarget)
            {
                state.tray = RemoveFirstCount(state.tray, 'Y', yellowMatchTarget);
                state.yellowCleared = true;
                changed = true;
            }
        }

        return state;
    }

    private static bool IsSolved(PlayState state, bool blueActive, bool yellowActive)
    {
        return state.redCleared &&
               state.greenCleared &&
               (!blueActive || state.blueCleared) &&
               (!yellowActive || state.yellowCleared);
    }

    private static bool IsPathClear(int index, UnityGameManager.Direction direction, int mask, int size)
    {
        int row = index / size;
        int col = index % size;

        if (direction == UnityGameManager.Direction.Up)
        {
            for (int r = row - 1; r >= 0; r--)
            {
                if ((mask & (1 << (r * size + col))) != 0) return false;
            }
        }
        else if (direction == UnityGameManager.Direction.Down)
        {
            for (int r = row + 1; r < size; r++)
            {
                if ((mask & (1 << (r * size + col))) != 0) return false;
            }
        }
        else if (direction == UnityGameManager.Direction.Left)
        {
            for (int c = col - 1; c >= 0; c--)
            {
                if ((mask & (1 << (row * size + c))) != 0) return false;
            }
        }
        else
        {
            for (int c = col + 1; c < size; c++)
            {
                if ((mask & (1 << (row * size + c))) != 0) return false;
            }
        }

        return true;
    }

    private static int CountColor(UnityGameManager.LevelConfig level, UnityGameManager.BlockColor target)
    {
        int count = 0;
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            if (block.color == target) count++;
        }

        return count;
    }

    private static int CountChar(string value, char target)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == target) count++;
        }

        return count;
    }

    private static string RemoveFirstCount(string value, char target, int count)
    {
        int removed = 0;
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == target && removed < count)
            {
                removed++;
                continue;
            }

            result.Append(value[i]);
        }

        return result.ToString();
    }

    private static int FindAdjacentBlueRun(string tray, int target)
    {
        if (target <= 0) return -1;

        for (int i = 0; i <= tray.Length - target; i++)
        {
            bool matches = true;
            for (int offset = 0; offset < target; offset++)
            {
                if (tray[i + offset] != 'B')
                {
                    matches = false;
                    break;
                }
            }

            if (matches) return i;
        }

        return -1;
    }

    private static string ColorLayoutKey(UnityGameManager.LevelConfig level)
    {
        char[] chars = new char[level.boardSize * level.boardSize];
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            chars[block.row * level.boardSize + block.col] = ColorChar(block.color);
        }

        return new string(chars);
    }

    private static string CanonicalColorLayout(string raw, int size)
    {
        string best = null;
        for (int transform = 0; transform < 8; transform++)
        {
            string transformed = TransformLayout(raw, size, transform);
            string swapped = SwapRedGreen(transformed);

            if (best == null || string.CompareOrdinal(transformed, best) < 0) best = transformed;
            if (string.CompareOrdinal(swapped, best) < 0) best = swapped;
        }

        return best;
    }

    private static string TransformLayout(string raw, int size, int transform)
    {
        char[] result = new char[raw.Length];
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                int sourceR = r;
                int sourceC = c;

                switch (transform)
                {
                    case 1:
                        sourceR = size - 1 - c;
                        sourceC = r;
                        break;
                    case 2:
                        sourceR = size - 1 - r;
                        sourceC = size - 1 - c;
                        break;
                    case 3:
                        sourceR = c;
                        sourceC = size - 1 - r;
                        break;
                    case 4:
                        sourceR = r;
                        sourceC = size - 1 - c;
                        break;
                    case 5:
                        sourceR = size - 1 - r;
                        sourceC = c;
                        break;
                    case 6:
                        sourceR = c;
                        sourceC = r;
                        break;
                    case 7:
                        sourceR = size - 1 - c;
                        sourceC = size - 1 - r;
                        break;
                }

                result[r * size + c] = raw[sourceR * size + sourceC];
            }
        }

        return new string(result);
    }

    private static string SwapRedGreen(string raw)
    {
        char[] chars = raw.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == 'R') chars[i] = 'G';
            else if (chars[i] == 'G') chars[i] = 'R';
        }

        return new string(chars);
    }

    private static char ColorChar(UnityGameManager.BlockColor color)
    {
        if (color == UnityGameManager.BlockColor.Red) return 'R';
        if (color == UnityGameManager.BlockColor.Green) return 'G';
        if (color == UnityGameManager.BlockColor.Blue) return 'B';
        if (color == UnityGameManager.BlockColor.Yellow) return 'Y';
        return 'N';
    }
}
