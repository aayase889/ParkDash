using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class ColorSortLevelBuilder
{
    private const int FirstBlueBoardIndex = 16;
    private const int FirstYellowBoardIndex = 45;
    private const int FirstFiveByFiveBoardIndex = 50;
    private const int MaxGenerationAttempts = 5000;
    private const int MaxColorSequenceAttempts = 120;
    private const char EmptyPark = '-';

    private static readonly UnityGameManager.Direction[] AllDirections =
    {
        UnityGameManager.Direction.Up,
        UnityGameManager.Direction.Down,
        UnityGameManager.Direction.Left,
        UnityGameManager.Direction.Right
    };

    private struct RemovalOption
    {
        public int index;
        public UnityGameManager.Direction direction;
    }

    private struct TrayState
    {
        public string tray;
        public char park;
        public bool redCleared;
        public bool greenCleared;
        public bool blueCleared;
        public bool yellowCleared;

        public string Key()
        {
            return tray + "|" + park + "|" +
                   (redCleared ? "1" : "0") +
                   (greenCleared ? "1" : "0") +
                   (blueCleared ? "1" : "0") +
                   (yellowCleared ? "1" : "0");
        }
    }

    private struct CostedTrayState
    {
        public TrayState state;
        public int parkingActions;

        public CostedTrayState(TrayState state, int parkingActions)
        {
            this.state = state;
            this.parkingActions = parkingActions;
        }
    }

    public static List<UnityGameManager.LevelConfig> BuildLevels(int count)
    {
        var levels = new List<UnityGameManager.LevelConfig>();
        var canonicalShapes = new HashSet<string>();
        var acceptedLayouts = new List<string>();

        for (int i = 0; i < count; i++)
        {
            levels.Add(BuildLevel(i, canonicalShapes, acceptedLayouts));
            Debug.Log($"Generated fixed puzzle Board {i + 1}/{count}.");
        }

        return levels;
    }

    public static UnityGameManager.LevelConfig BuildLevel(int boardIndex)
    {
        return BuildLevel(boardIndex, null, null);
    }

    private static UnityGameManager.LevelConfig BuildLevel(int boardIndex, HashSet<string> canonicalShapes, List<string> acceptedLayouts)
    {
        bool usesBlue = boardIndex >= FirstBlueBoardIndex;
        int blueMatchTarget = usesBlue ? GetBlueMatchTarget(boardIndex) : 0;
        int yellowMatchTarget = GetYellowMatchTarget(boardIndex);
        int size = GetBoardSize(boardIndex, usesBlue, yellowMatchTarget);
        int matchTarget = size;
        var sequenceRng = new System.Random(SeedFor(boardIndex, -31));
        if (!TryBuildColorSequence(boardIndex, size, matchTarget, blueMatchTarget, yellowMatchTarget, sequenceRng, out List<UnityGameManager.BlockColor> colorSequence))
        {
            Debug.LogWarning($"Could not build the preferred hard color rhythm for Board {boardIndex + 1}; using the structured parking fallback.");
            colorSequence = BuildStructuredColorSequence(boardIndex, size, blueMatchTarget, yellowMatchTarget, sequenceRng);
            if (!IsTraySequenceSolvable(colorSequence, matchTarget, blueMatchTarget, yellowMatchTarget))
            {
                return BuildEmergencyLevel(boardIndex);
            }
        }

        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var rng = new System.Random(SeedFor(boardIndex, attempt));

            if (!TryBuildRemovalPlan(size, rng, out int[] removalOrder, out UnityGameManager.Direction[] directions))
            {
                continue;
            }

            UnityGameManager.LevelConfig level = CreateLevelFromPlan(boardIndex, size, matchTarget, removalOrder, directions, colorSequence);
            string rawLayout = ColorLayoutKey(level.blocks, size);
            string canonicalLayout = CanonicalColorLayout(rawLayout, size);

            if (canonicalShapes != null && canonicalShapes.Contains(canonicalLayout))
            {
                continue;
            }

            if (acceptedLayouts != null && IsTooSimilarToAccepted(rawLayout, size, acceptedLayouts))
            {
                continue;
            }

            if (!HasHealthyColorMix(level.blocks, size))
            {
                continue;
            }

            if (!HasHealthyDirectionMix(level.blocks, size))
            {
                continue;
            }

            if (HasFacingArrows(level.blocks, size))
            {
                continue;
            }

            if (!ValidatePlannedSolution(level.blocks, size, removalOrder))
            {
                continue;
            }

            if (!IsTraySequenceSolvable(colorSequence, matchTarget, blueMatchTarget, yellowMatchTarget))
            {
                continue;
            }

            if (!HasDifficultyShape(level, boardIndex, removalOrder))
            {
                continue;
            }

            canonicalShapes?.Add(canonicalLayout);
            acceptedLayouts?.Add(rawLayout);
            return level;
        }

        Debug.LogWarning($"Could not generate a fully unique level for Board {boardIndex + 1}; using a relaxed validated fallback.");
        return BuildFallbackLevel(boardIndex);
    }

    private static int SeedFor(int boardIndex, int attempt)
    {
        unchecked
        {
            return 17391 + boardIndex * 92821 + attempt * 131071;
        }
    }

    private static bool TryBuildRemovalPlan(int size, System.Random rng, out int[] removalOrder, out UnityGameManager.Direction[] directions)
    {
        int count = size * size;
        removalOrder = new int[count];
        directions = new UnityGameManager.Direction[count];

        int remainingMask = (1 << count) - 1;
        int[] directionCounts = new int[AllDirections.Length];
        UnityGameManager.Direction? previousDirection = null;

        for (int step = 0; step < count; step++)
        {
            var options = new List<RemovalOption>();

            for (int index = 0; index < count; index++)
            {
                if ((remainingMask & (1 << index)) == 0) continue;

                foreach (UnityGameManager.Direction direction in AllDirections)
                {
                    if (IsPathClear(index, direction, remainingMask, size))
                    {
                        options.Add(new RemovalOption { index = index, direction = direction });
                    }
                }
            }

            if (options.Count == 0) return false;

            int bestScore = int.MinValue;
            var bestOptions = new List<RemovalOption>();
            foreach (RemovalOption option in options)
            {
                int score = rng.Next(0, 7);
                int directionIndex = DirectionIndex(option.direction);
                score -= directionCounts[directionIndex] * 5;

                if (previousDirection.HasValue && previousDirection.Value == option.direction)
                {
                    score -= 4;
                }

                if (WouldFaceAssignedNeighbor(option.index, option.direction, directions, removalOrder, step, size))
                {
                    score -= 12;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestOptions.Clear();
                    bestOptions.Add(option);
                }
                else if (score == bestScore)
                {
                    bestOptions.Add(option);
                }
            }

            RemovalOption chosen = bestOptions[rng.Next(bestOptions.Count)];
            removalOrder[step] = chosen.index;
            directions[chosen.index] = chosen.direction;
            directionCounts[DirectionIndex(chosen.direction)]++;
            previousDirection = chosen.direction;
            remainingMask &= ~(1 << chosen.index);
        }

        return true;
    }

    private static bool TryBuildColorSequence(int boardIndex, int size, int matchTarget, int blueMatchTarget, int yellowMatchTarget, System.Random rng, out List<UnityGameManager.BlockColor> sequence)
    {
        if (size >= 5)
        {
            sequence = BuildStructuredColorSequence(boardIndex, size, blueMatchTarget, yellowMatchTarget, rng);
            return MinimumParkingActions(sequence, matchTarget, blueMatchTarget, yellowMatchTarget) >= RequiredPlannedParkingActions(boardIndex);
        }

        sequence = null;
        var colors = BuildColorBag(size, blueMatchTarget, yellowMatchTarget);
        bool shouldNeedParking = boardIndex > 0;
        int requiredParkingActions = RequiredPlannedParkingActions(boardIndex);
        int bestScore = int.MinValue;
        List<UnityGameManager.BlockColor> bestCandidate = null;

        for (int attempt = 0; attempt < MaxColorSequenceAttempts; attempt++)
        {
            var candidate = new List<UnityGameManager.BlockColor>(colors);
            Shuffle(candidate, rng);

            if (!HasUsefulNeutralTiming(candidate, size))
            {
                continue;
            }

            int parkingActions = MinimumParkingActions(candidate, matchTarget, blueMatchTarget, yellowMatchTarget);
            if (parkingActions < 0)
            {
                continue;
            }

            if (shouldNeedParking && parkingActions == 0)
            {
                continue;
            }

            int score = ScoreColorSequence(candidate, parkingActions, boardIndex, matchTarget);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }

            if (parkingActions >= requiredParkingActions &&
                HasChallengingColorRhythm(candidate, boardIndex, matchTarget))
            {
                sequence = candidate;
                return true;
            }
        }

        if (size >= 5)
        {
            List<UnityGameManager.BlockColor> fallback = BuildStructuredColorSequence(boardIndex, size, blueMatchTarget, yellowMatchTarget, rng);
            int fallbackParking = MinimumParkingActions(fallback, matchTarget, blueMatchTarget, yellowMatchTarget);
            if (fallbackParking >= 0 && (fallbackParking > 0 || !shouldNeedParking))
            {
                int fallbackScore = ScoreColorSequence(fallback, fallbackParking, boardIndex, matchTarget);
                if (fallbackScore > bestScore)
                {
                    bestCandidate = fallback;
                }
            }
        }

        if (bestCandidate != null)
        {
            sequence = bestCandidate;
            return true;
        }

        return false;
    }

    private static List<UnityGameManager.BlockColor> BuildFiveByFiveColorSequence(int boardIndex, System.Random rng)
    {
        return BuildStructuredColorSequence(boardIndex, 5, 5, 5, rng);
    }

    private static List<UnityGameManager.BlockColor> BuildStructuredColorSequence(int boardIndex, int size, int blueMatchTarget, int yellowMatchTarget, System.Random rng)
    {
        var colored = new List<UnityGameManager.BlockColor>(size * size);
        bool greenFirst = (boardIndex & 1) != 0;
        UnityGameManager.BlockColor first = greenFirst ? UnityGameManager.BlockColor.Green : UnityGameManager.BlockColor.Red;
        UnityGameManager.BlockColor second = greenFirst ? UnityGameManager.BlockColor.Red : UnityGameManager.BlockColor.Green;

        // Fill the normal tray with an almost-complete set plus one blocker.
        // The player must park that blocker, finish the first set, then return it
        // to complete the second set.
        AddColors(colored, first, size - 1);
        colored.Add(second);
        colored.Add(first);
        AddColors(colored, second, size - 1);

        if (blueMatchTarget > 0 && yellowMatchTarget > 0)
        {
            // Blue must stay adjacent in the tray. A yellow interruption just
            // before the final blue adds a second intentional parking decision.
            AddColors(colored, UnityGameManager.BlockColor.Blue, blueMatchTarget - 1);
            colored.Add(UnityGameManager.BlockColor.Yellow);
            colored.Add(UnityGameManager.BlockColor.Blue);
            AddColors(colored, UnityGameManager.BlockColor.Yellow, yellowMatchTarget - 1);
        }
        else
        {
            AddColors(colored, UnityGameManager.BlockColor.Blue, blueMatchTarget);
            AddColors(colored, UnityGameManager.BlockColor.Yellow, yellowMatchTarget);
        }

        int totalSlots = size * size;
        int neutralCount = totalSlots - colored.Count;
        var neutralSlots = new HashSet<int>();
        if (neutralCount > 0) neutralSlots.Add(1);
        if (neutralCount > 1) neutralSlots.Add(totalSlots / 2);
        if (neutralCount > 2) neutralSlots.Add(totalSlots - 2);
        while (neutralSlots.Count < neutralCount)
            neutralSlots.Add(rng.Next(0, totalSlots));

        var sequence = new List<UnityGameManager.BlockColor>(totalSlots);
        int coloredIndex = 0;
        for (int slot = 0; slot < totalSlots; slot++)
        {
            sequence.Add(neutralSlots.Contains(slot)
                ? UnityGameManager.BlockColor.Neutral
                : colored[coloredIndex++]);
        }

        return sequence;
    }

    private static List<UnityGameManager.BlockColor> BuildColorBag(int size, int blueMatchTarget, int yellowMatchTarget)
    {
        var colors = new List<UnityGameManager.BlockColor>();
        int redCount = size;
        int greenCount = size;
        int blueCount = Mathf.Max(0, blueMatchTarget);
        int yellowCount = Mathf.Max(0, yellowMatchTarget);
        int neutralCount = size * size - redCount - greenCount - blueCount - yellowCount;

        AddColors(colors, UnityGameManager.BlockColor.Red, redCount);
        AddColors(colors, UnityGameManager.BlockColor.Green, greenCount);
        AddColors(colors, UnityGameManager.BlockColor.Blue, blueCount);
        AddColors(colors, UnityGameManager.BlockColor.Yellow, yellowCount);
        AddColors(colors, UnityGameManager.BlockColor.Neutral, neutralCount);
        return colors;
    }

    private static void AddColors(List<UnityGameManager.BlockColor> colors, UnityGameManager.BlockColor color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            colors.Add(color);
        }
    }

    private static UnityGameManager.LevelConfig CreateLevelFromPlan(int boardIndex, int size, int matchTarget, int[] removalOrder, UnityGameManager.Direction[] directions, List<UnityGameManager.BlockColor> colorSequence)
    {
        var blockByIndex = new UnityGameManager.BlockData[size * size];

        for (int step = 0; step < removalOrder.Length; step++)
        {
            int index = removalOrder[step];
            UnityGameManager.BlockColor color = colorSequence[step];
            blockByIndex[index] = new UnityGameManager.BlockData
            {
                row = index / size,
                col = index % size,
                color = color,
                direction = directions[index],
                textLabel = LabelForColor(color)
            };
        }

        return new UnityGameManager.LevelConfig
        {
            id = boardIndex + 1,
            name = "Board " + (boardIndex + 1),
            boardSize = size,
            matchTarget = matchTarget,
            blocks = new List<UnityGameManager.BlockData>(blockByIndex)
        };
    }

    private static bool ValidatePlannedSolution(List<UnityGameManager.BlockData> blocks, int size, int[] removalOrder)
    {
        int remainingMask = (1 << (size * size)) - 1;
        var byIndex = new UnityGameManager.BlockData[size * size];

        foreach (UnityGameManager.BlockData block in blocks)
        {
            byIndex[block.row * size + block.col] = block;
        }

        foreach (int index in removalOrder)
        {
            UnityGameManager.BlockData block = byIndex[index];
            if (!IsPathClear(index, block.direction, remainingMask, size))
            {
                return false;
            }

            remainingMask &= ~(1 << index);
        }

        return true;
    }

    private static bool IsPathClear(int index, UnityGameManager.Direction direction, int remainingMask, int size)
    {
        int row = index / size;
        int col = index % size;

        if (direction == UnityGameManager.Direction.Up)
        {
            for (int r = row - 1; r >= 0; r--)
            {
                if ((remainingMask & (1 << (r * size + col))) != 0) return false;
            }
        }
        else if (direction == UnityGameManager.Direction.Down)
        {
            for (int r = row + 1; r < size; r++)
            {
                if ((remainingMask & (1 << (r * size + col))) != 0) return false;
            }
        }
        else if (direction == UnityGameManager.Direction.Left)
        {
            for (int c = col - 1; c >= 0; c--)
            {
                if ((remainingMask & (1 << (row * size + c))) != 0) return false;
            }
        }
        else if (direction == UnityGameManager.Direction.Right)
        {
            for (int c = col + 1; c < size; c++)
            {
                if ((remainingMask & (1 << (row * size + c))) != 0) return false;
            }
        }

        return true;
    }

    private static bool HasDifficultyShape(UnityGameManager.LevelConfig level, int boardIndex, int[] removalOrder)
    {
        if (boardIndex <= 0) return true;

        int size = level.boardSize;
        int count = size * size;
        int fullMask = (1 << count) - 1;
        var directions = new UnityGameManager.Direction[count];
        var colors = new UnityGameManager.BlockColor[count];
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            int index = block.row * size + block.col;
            directions[index] = block.direction;
            colors[index] = block.color;
        }

        int initialLegal = 0;
        var openingColors = new HashSet<UnityGameManager.BlockColor>();
        for (int index = 0; index < count; index++)
        {
            if (!IsPathClear(index, directions[index], fullMask, size)) continue;
            initialLegal++;
            if (colors[index] != UnityGameManager.BlockColor.Neutral)
            {
                openingColors.Add(colors[index]);
            }
        }

        int minimumOpeningMoves = size == 3 ? 2 : size == 4 ? 3 : 2;
        int maximumOpeningMoves = size == 3 ? 5 : size == 4 ? 7 : 12;
        if (initialLegal < minimumOpeningMoves || initialLegal > maximumOpeningMoves)
        {
            return false;
        }

        int requiredOpeningColors = boardIndex < 5 || size >= 5 ? 1 : 2;
        if (openingColors.Count < requiredOpeningColors)
        {
            return false;
        }

        int remainingMask = fullMask;
        int decisionSteps = 0;
        int constrainedSteps = 0;
        int inspectedSteps = Mathf.Max(0, count - 2);
        int constrainedLimit = size == 3 ? 3 : size == 4 ? 5 : 8;

        for (int step = 0; step < inspectedSteps; step++)
        {
            int legalMoves = 0;
            for (int index = 0; index < count; index++)
            {
                if ((remainingMask & (1 << index)) == 0) continue;
                if (IsPathClear(index, directions[index], remainingMask, size)) legalMoves++;
            }

            if (legalMoves >= 2) decisionSteps++;
            if (legalMoves <= constrainedLimit) constrainedSteps++;
            remainingMask &= ~(1 << removalOrder[step]);
        }

        int requiredDecisionSteps = size == 3 ? 2 : size == 4 ? 5 : 6;
        int requiredConstrainedSteps = size == 3 ? 3 : size == 4 ? 7 : 8;
        return decisionSteps >= requiredDecisionSteps &&
               constrainedSteps >= requiredConstrainedSteps;
    }

    private static bool IsTraySequenceSolvable(List<UnityGameManager.BlockColor> sequence, int capacity, int blueMatchTarget, int yellowMatchTarget)
    {
        bool usesBlue = blueMatchTarget > 0;
        bool usesYellow = yellowMatchTarget > 0;
        var states = new HashSet<string>();
        var stateList = new List<TrayState>();
        TrayState start = NormalizeState(new TrayState { tray = string.Empty, park = EmptyPark }, capacity, blueMatchTarget, yellowMatchTarget);
        states.Add(start.Key());
        stateList.Add(start);

        foreach (UnityGameManager.BlockColor color in sequence)
        {
            if (color == UnityGameManager.BlockColor.Neutral) continue;

            stateList = ExpandParkingClosure(stateList, capacity, blueMatchTarget, yellowMatchTarget);
            var nextKeys = new HashSet<string>();
            var nextStates = new List<TrayState>();
            char colorChar = ColorChar(color);

            foreach (TrayState state in stateList)
            {
                if (state.tray.Length >= capacity) continue;

                TrayState added = state;
                added.tray += colorChar;
                added = NormalizeState(added, capacity, blueMatchTarget, yellowMatchTarget);
                if (nextKeys.Add(added.Key()))
                {
                    nextStates.Add(added);
                }
            }

            if (nextStates.Count == 0) return false;

            stateList = nextStates;
            states.Clear();
            foreach (TrayState state in stateList)
            {
                states.Add(state.Key());
            }
        }

        stateList = ExpandParkingClosure(stateList, capacity, blueMatchTarget, yellowMatchTarget);
        foreach (TrayState state in stateList)
        {
            if (state.redCleared && state.greenCleared && (!usesBlue || state.blueCleared) && (!usesYellow || state.yellowCleared))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTraySequenceSolvableWithoutParking(List<UnityGameManager.BlockColor> sequence, int capacity, int blueMatchTarget, int yellowMatchTarget)
    {
        bool usesBlue = blueMatchTarget > 0;
        bool usesYellow = yellowMatchTarget > 0;
        TrayState state = NormalizeState(new TrayState { tray = string.Empty, park = EmptyPark }, capacity, blueMatchTarget, yellowMatchTarget);

        foreach (UnityGameManager.BlockColor color in sequence)
        {
            if (color == UnityGameManager.BlockColor.Neutral) continue;
            if (state.tray.Length >= capacity) return false;

            state.tray += ColorChar(color);
            state = NormalizeState(state, capacity, blueMatchTarget, yellowMatchTarget);
        }

        return state.redCleared && state.greenCleared && (!usesBlue || state.blueCleared) && (!usesYellow || state.yellowCleared);
    }

    private static int MinimumParkingActions(List<UnityGameManager.BlockColor> sequence, int capacity, int blueMatchTarget, int yellowMatchTarget)
    {
        bool usesBlue = blueMatchTarget > 0;
        bool usesYellow = yellowMatchTarget > 0;
        TrayState start = NormalizeState(new TrayState { tray = string.Empty, park = EmptyPark }, capacity, blueMatchTarget, yellowMatchTarget);
        var states = new List<CostedTrayState> { new CostedTrayState(start, 0) };

        foreach (UnityGameManager.BlockColor color in sequence)
        {
            if (color == UnityGameManager.BlockColor.Neutral) continue;

            states = ExpandParkingClosureWithCosts(states, capacity, blueMatchTarget, yellowMatchTarget);
            var nextByKey = new Dictionary<string, CostedTrayState>();
            char colorChar = ColorChar(color);

            foreach (CostedTrayState costed in states)
            {
                TrayState state = costed.state;
                if (state.tray.Length >= capacity) continue;

                state.tray += colorChar;
                state = NormalizeState(state, capacity, blueMatchTarget, yellowMatchTarget);
                KeepCheapest(nextByKey, state, costed.parkingActions);
            }

            if (nextByKey.Count == 0) return -1;
            states = new List<CostedTrayState>(nextByKey.Values);
        }

        states = ExpandParkingClosureWithCosts(states, capacity, blueMatchTarget, yellowMatchTarget);
        int best = int.MaxValue;
        foreach (CostedTrayState costed in states)
        {
            TrayState state = costed.state;
            if (!state.redCleared || !state.greenCleared || (usesBlue && !state.blueCleared) || (usesYellow && !state.yellowCleared))
            {
                continue;
            }

            best = Mathf.Min(best, costed.parkingActions);
        }

        return best == int.MaxValue ? -1 : best;
    }

    private static List<CostedTrayState> ExpandParkingClosureWithCosts(List<CostedTrayState> startStates, int capacity, int blueMatchTarget, int yellowMatchTarget)
    {
        var queue = new Queue<CostedTrayState>();
        var bestByKey = new Dictionary<string, CostedTrayState>();

        foreach (CostedTrayState costed in startStates)
        {
            TrayState normalized = NormalizeState(costed.state, capacity, blueMatchTarget, yellowMatchTarget);
            if (KeepCheapest(bestByKey, normalized, costed.parkingActions))
            {
                queue.Enqueue(new CostedTrayState(normalized, costed.parkingActions));
            }
        }

        while (queue.Count > 0)
        {
            CostedTrayState current = queue.Dequeue();
            string currentKey = current.state.Key();
            if (!bestByKey.TryGetValue(currentKey, out CostedTrayState cheapest) ||
                cheapest.parkingActions != current.parkingActions)
            {
                continue;
            }

            TrayState state = current.state;
            int nextCost = current.parkingActions + 1;

            if (state.park == EmptyPark)
            {
                for (int i = 0; i < state.tray.Length; i++)
                {
                    TrayState moved = state;
                    moved.park = state.tray[i];
                    moved.tray = state.tray.Remove(i, 1);
                    moved = NormalizeState(moved, capacity, blueMatchTarget, yellowMatchTarget);
                    if (KeepCheapest(bestByKey, moved, nextCost))
                    {
                        queue.Enqueue(new CostedTrayState(moved, nextCost));
                    }
                }
            }
            else
            {
                if (state.tray.Length < capacity)
                {
                    TrayState returned = state;
                    returned.tray += state.park;
                    returned.park = EmptyPark;
                    returned = NormalizeState(returned, capacity, blueMatchTarget, yellowMatchTarget);
                    if (KeepCheapest(bestByKey, returned, nextCost))
                    {
                        queue.Enqueue(new CostedTrayState(returned, nextCost));
                    }
                }

                for (int i = 0; i < state.tray.Length; i++)
                {
                    char[] chars = state.tray.ToCharArray();
                    char oldTrayChar = chars[i];
                    chars[i] = state.park;

                    TrayState swapped = state;
                    swapped.tray = new string(chars);
                    swapped.park = oldTrayChar;
                    swapped = NormalizeState(swapped, capacity, blueMatchTarget, yellowMatchTarget);
                    if (KeepCheapest(bestByKey, swapped, nextCost))
                    {
                        queue.Enqueue(new CostedTrayState(swapped, nextCost));
                    }
                }
            }
        }

        return new List<CostedTrayState>(bestByKey.Values);
    }

    private static bool KeepCheapest(Dictionary<string, CostedTrayState> bestByKey, TrayState state, int cost)
    {
        string key = state.Key();
        if (bestByKey.TryGetValue(key, out CostedTrayState existing) && existing.parkingActions <= cost)
        {
            return false;
        }

        bestByKey[key] = new CostedTrayState(state, cost);
        return true;
    }

    private static int RequiredPlannedParkingActions(int boardIndex)
    {
        if (boardIndex <= 0) return 0;
        if (boardIndex < 5) return 2;
        if (boardIndex < 25) return 2;
        if (boardIndex < 36) return 3;
        if (boardIndex < 45) return 2;
        if (boardIndex < 50) return 3;
        return 3;
    }

    private static int ScoreColorSequence(List<UnityGameManager.BlockColor> sequence, int parkingActions, int boardIndex, int matchTarget)
    {
        int transitions = 0;
        int longestNonBlueRun = 0;
        int currentRun = 0;
        UnityGameManager.BlockColor previous = UnityGameManager.BlockColor.Neutral;

        foreach (UnityGameManager.BlockColor color in sequence)
        {
            if (color == UnityGameManager.BlockColor.Neutral) continue;
            if (previous != UnityGameManager.BlockColor.Neutral && previous != color) transitions++;

            if (color != UnityGameManager.BlockColor.Blue && color == previous)
            {
                currentRun++;
            }
            else
            {
                currentRun = color == UnityGameManager.BlockColor.Blue ? 0 : 1;
            }

            longestNonBlueRun = Mathf.Max(longestNonBlueRun, currentRun);
            previous = color;
        }

        int runPenalty = Mathf.Max(0, longestNonBlueRun - Mathf.Max(1, matchTarget - 2));
        return parkingActions * 100 + transitions * 6 - runPenalty * 12 + Mathf.Min(boardIndex, 50);
    }

    private static bool HasChallengingColorRhythm(List<UnityGameManager.BlockColor> sequence, int boardIndex, int matchTarget)
    {
        if (boardIndex <= 0) return true;

        int transitions = 0;
        int longestNonBlueRun = 0;
        int currentRun = 0;
        int coloredSeen = 0;
        var earlyColors = new HashSet<UnityGameManager.BlockColor>();
        UnityGameManager.BlockColor previous = UnityGameManager.BlockColor.Neutral;

        foreach (UnityGameManager.BlockColor color in sequence)
        {
            if (color == UnityGameManager.BlockColor.Neutral) continue;
            coloredSeen++;
            if (coloredSeen <= matchTarget + 1) earlyColors.Add(color);
            if (previous != UnityGameManager.BlockColor.Neutral && previous != color) transitions++;

            if (color != UnityGameManager.BlockColor.Blue && color == previous)
            {
                currentRun++;
            }
            else
            {
                currentRun = color == UnityGameManager.BlockColor.Blue ? 0 : 1;
            }

            longestNonBlueRun = Mathf.Max(longestNonBlueRun, currentRun);
            previous = color;
        }

        int requiredTransitions = boardIndex < 5 ? 3 :
                                  boardIndex < 16 ? 4 :
                                  boardIndex < 25 ? 5 :
                                  boardIndex < 36 ? 6 :
                                  boardIndex < 45 ? 7 :
                                  boardIndex < 50 ? 8 : 10;

        return earlyColors.Count >= 2 &&
               transitions >= requiredTransitions &&
               longestNonBlueRun < matchTarget;
    }

    private static List<TrayState> ExpandParkingClosure(List<TrayState> startStates, int capacity, int blueMatchTarget, int yellowMatchTarget)
    {
        var queue = new Queue<TrayState>();
        var visited = new HashSet<string>();
        var result = new List<TrayState>();

        foreach (TrayState state in startStates)
        {
            TrayState normalized = NormalizeState(state, capacity, blueMatchTarget, yellowMatchTarget);
            if (visited.Add(normalized.Key()))
            {
                queue.Enqueue(normalized);
                result.Add(normalized);
            }
        }

        while (queue.Count > 0)
        {
            TrayState state = queue.Dequeue();

            if (state.park == EmptyPark)
            {
                for (int i = 0; i < state.tray.Length; i++)
                {
                    TrayState moved = state;
                    moved.park = state.tray[i];
                    moved.tray = state.tray.Remove(i, 1);
                    moved = NormalizeState(moved, capacity, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(moved.Key()))
                    {
                        queue.Enqueue(moved);
                        result.Add(moved);
                    }
                }
            }
            else
            {
                if (state.tray.Length < capacity)
                {
                    TrayState returned = state;
                    returned.tray += state.park;
                    returned.park = EmptyPark;
                    returned = NormalizeState(returned, capacity, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(returned.Key()))
                    {
                        queue.Enqueue(returned);
                        result.Add(returned);
                    }
                }

                for (int i = 0; i < state.tray.Length; i++)
                {
                    char[] chars = state.tray.ToCharArray();
                    char oldTrayChar = chars[i];
                    chars[i] = state.park;

                    TrayState swapped = state;
                    swapped.tray = new string(chars);
                    swapped.park = oldTrayChar;
                    swapped = NormalizeState(swapped, capacity, blueMatchTarget, yellowMatchTarget);
                    if (visited.Add(swapped.Key()))
                    {
                        queue.Enqueue(swapped);
                        result.Add(swapped);
                    }
                }
            }
        }

        return result;
    }

    private static TrayState NormalizeState(TrayState state, int matchTarget, int blueMatchTarget, int yellowMatchTarget)
    {
        bool usesBlue = blueMatchTarget > 0;
        bool usesYellow = yellowMatchTarget > 0;
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

            if (usesBlue && !state.blueCleared)
            {
                int blueRunIndex = FindAdjacentBlueRun(state.tray, blueMatchTarget);
                if (blueRunIndex >= 0)
                {
                    state.tray = state.tray.Remove(blueRunIndex, blueMatchTarget);
                    state.blueCleared = true;
                    changed = true;
                }
            }

            if (usesYellow && !state.yellowCleared && CountChar(state.tray, 'Y') >= yellowMatchTarget)
            {
                state.tray = RemoveFirstCount(state.tray, 'Y', yellowMatchTarget);
                state.yellowCleared = true;
                changed = true;
            }
        }

        return state;
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
        var builder = new StringBuilder();
        int removed = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == target && removed < count)
            {
                removed++;
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
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

    private static int GetBlueMatchTarget(int boardIndex)
    {
        if (boardIndex < FirstBlueBoardIndex) return 0;
        return boardIndex >= FirstFiveByFiveBoardIndex ? 5 : 4;
    }

    private static int GetYellowMatchTarget(int boardIndex)
    {
        if (boardIndex < FirstYellowBoardIndex) return 0;
        return boardIndex >= FirstFiveByFiveBoardIndex ? 5 : 4;
    }

    private static int GetBoardSize(int boardIndex, bool usesBlue, int yellowMatchTarget)
    {
        if (boardIndex >= FirstFiveByFiveBoardIndex) return 5;
        return boardIndex >= 5 || usesBlue || yellowMatchTarget > 0 ? 4 : 3;
    }

    private static bool HasUsefulNeutralTiming(List<UnityGameManager.BlockColor> sequence, int size)
    {
        int firstNeutral = -1;
        int lastNeutral = -1;
        int neutralCount = 0;
        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] != UnityGameManager.BlockColor.Neutral) continue;
            neutralCount++;
            if (firstNeutral < 0) firstNeutral = i;
            lastNeutral = i;
        }

        if (neutralCount == 0) return true;
        if (neutralCount <= 2) return lastNeutral >= size;
        return firstNeutral >= 0 && firstNeutral <= size && lastNeutral >= sequence.Count / 2;
    }

    private static bool HasHealthyColorMix(List<UnityGameManager.BlockData> blocks, int size)
    {
        var colors = ToColorGrid(blocks, size);

        for (int r = 0; r < size; r++)
        {
            if (HasLineClump(colors, size, r, 0, 0, 1)) return false;
        }

        for (int c = 0; c < size; c++)
        {
            if (HasLineClump(colors, size, 0, c, 1, 0)) return false;
        }

        return true;
    }

    private static bool HasLineClump(UnityGameManager.BlockColor[,] colors, int size, int startRow, int startCol, int rowStep, int colStep)
    {
        var counts = new Dictionary<UnityGameManager.BlockColor, int>();
        for (int i = 0; i < size; i++)
        {
            UnityGameManager.BlockColor color = colors[startRow + rowStep * i, startCol + colStep * i];
            if (!counts.ContainsKey(color)) counts[color] = 0;
            counts[color]++;
        }

        foreach (var pair in counts)
        {
            if (pair.Key == UnityGameManager.BlockColor.Neutral && pair.Value == size) return true;
            if (pair.Key != UnityGameManager.BlockColor.Neutral && pair.Value >= Mathf.Min(3, size)) return true;
        }

        return false;
    }

    private static bool HasHealthyDirectionMix(List<UnityGameManager.BlockData> blocks, int size)
    {
        var used = new HashSet<UnityGameManager.Direction>();
        var directionGrid = new UnityGameManager.Direction[size, size];

        foreach (UnityGameManager.BlockData block in blocks)
        {
            used.Add(block.direction);
            directionGrid[block.row, block.col] = block.direction;
        }

        int minimumDirections = size == 3 ? 3 : 4;
        if (used.Count < minimumDirections) return false;

        for (int r = 0; r < size; r++)
        {
            bool allSame = true;
            for (int c = 1; c < size; c++)
            {
                if (directionGrid[r, c] != directionGrid[r, 0]) allSame = false;
            }

            if (allSame) return false;
        }

        for (int c = 0; c < size; c++)
        {
            bool allSame = true;
            for (int r = 1; r < size; r++)
            {
                if (directionGrid[r, c] != directionGrid[0, c]) allSame = false;
            }

            if (allSame) return false;
        }

        return true;
    }

    private static bool HasFacingArrows(List<UnityGameManager.BlockData> blocks, int size)
    {
        var directionGrid = new UnityGameManager.Direction[size, size];

        foreach (UnityGameManager.BlockData block in blocks)
        {
            directionGrid[block.row, block.col] = block.direction;
        }

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (c + 1 < size &&
                    directionGrid[r, c] == UnityGameManager.Direction.Right &&
                    directionGrid[r, c + 1] == UnityGameManager.Direction.Left)
                {
                    return true;
                }

                if (r + 1 < size &&
                    directionGrid[r, c] == UnityGameManager.Direction.Down &&
                    directionGrid[r + 1, c] == UnityGameManager.Direction.Up)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool WouldFaceAssignedNeighbor(int index, UnityGameManager.Direction direction, UnityGameManager.Direction[] directions, int[] removalOrder, int assignedCount, int size)
    {
        int row = index / size;
        int col = index % size;

        for (int i = 0; i < assignedCount; i++)
        {
            int assignedIndex = removalOrder[i];
            int assignedRow = assignedIndex / size;
            int assignedCol = assignedIndex % size;
            UnityGameManager.Direction assignedDirection = directions[assignedIndex];

            if (row == assignedRow && col + 1 == assignedCol &&
                direction == UnityGameManager.Direction.Right &&
                assignedDirection == UnityGameManager.Direction.Left)
            {
                return true;
            }

            if (row == assignedRow && col - 1 == assignedCol &&
                direction == UnityGameManager.Direction.Left &&
                assignedDirection == UnityGameManager.Direction.Right)
            {
                return true;
            }

            if (row + 1 == assignedRow && col == assignedCol &&
                direction == UnityGameManager.Direction.Down &&
                assignedDirection == UnityGameManager.Direction.Up)
            {
                return true;
            }

            if (row - 1 == assignedRow && col == assignedCol &&
                direction == UnityGameManager.Direction.Up &&
                assignedDirection == UnityGameManager.Direction.Down)
            {
                return true;
            }
        }

        return false;
    }

    private static string ColorLayoutKey(List<UnityGameManager.BlockData> blocks, int size)
    {
        char[] chars = new char[size * size];
        foreach (UnityGameManager.BlockData block in blocks)
        {
            chars[block.row * size + block.col] = ColorChar(block.color);
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

    private static bool IsTooSimilarToAccepted(string raw, int size, List<string> acceptedLayouts)
    {
        int allowedDifference = size == 3 ? 1 : 2;

        foreach (string previous in acceptedLayouts)
        {
            if (previous.Length != raw.Length) continue;

            for (int transform = 0; transform < 8; transform++)
            {
                string transformed = TransformLayout(raw, size, transform);
                if (HammingDistance(transformed, previous) <= allowedDifference) return true;

                string swapped = SwapRedGreen(transformed);
                if (HammingDistance(swapped, previous) <= allowedDifference) return true;
            }
        }

        return false;
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

    private static int HammingDistance(string a, string b)
    {
        int distance = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) distance++;
        }

        return distance;
    }

    private static UnityGameManager.BlockColor[,] ToColorGrid(List<UnityGameManager.BlockData> blocks, int size)
    {
        var grid = new UnityGameManager.BlockColor[size, size];
        foreach (UnityGameManager.BlockData block in blocks)
        {
            grid[block.row, block.col] = block.color;
        }

        return grid;
    }

    private static int DirectionIndex(UnityGameManager.Direction direction)
    {
        if (direction == UnityGameManager.Direction.Up) return 0;
        if (direction == UnityGameManager.Direction.Down) return 1;
        if (direction == UnityGameManager.Direction.Left) return 2;
        return 3;
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private static char ColorChar(UnityGameManager.BlockColor color)
    {
        if (color == UnityGameManager.BlockColor.Red) return 'R';
        if (color == UnityGameManager.BlockColor.Green) return 'G';
        if (color == UnityGameManager.BlockColor.Blue) return 'B';
        if (color == UnityGameManager.BlockColor.Yellow) return 'Y';
        return 'N';
    }

    private static string LabelForColor(UnityGameManager.BlockColor color)
    {
        if (color == UnityGameManager.BlockColor.Red) return "RED";
        if (color == UnityGameManager.BlockColor.Green) return "GREEN";
        if (color == UnityGameManager.BlockColor.Blue) return "BLUE";
        if (color == UnityGameManager.BlockColor.Yellow) return "YELLOW";
        return "BLOCK";
    }

    private static UnityGameManager.LevelConfig BuildFallbackLevel(int boardIndex)
    {
        bool usesBlue = boardIndex >= FirstBlueBoardIndex;
        int blueMatchTarget = usesBlue ? GetBlueMatchTarget(boardIndex) : 0;
        int yellowMatchTarget = GetYellowMatchTarget(boardIndex);
        int size = GetBoardSize(boardIndex, usesBlue, yellowMatchTarget);
        int matchTarget = size;
        var sequenceRng = new System.Random(SeedFor(boardIndex, MaxGenerationAttempts + 11));
        if (!TryBuildColorSequence(boardIndex, size, matchTarget, blueMatchTarget, yellowMatchTarget, sequenceRng, out List<UnityGameManager.BlockColor> colorSequence))
        {
            colorSequence = BuildStructuredColorSequence(boardIndex, size, blueMatchTarget, yellowMatchTarget, sequenceRng);
            if (!IsTraySequenceSolvable(colorSequence, matchTarget, blueMatchTarget, yellowMatchTarget))
            {
                return BuildEmergencyLevel(boardIndex);
            }
        }

        for (int attempt = 0; attempt < MaxGenerationAttempts * 2; attempt++)
        {
            var rng = new System.Random(SeedFor(boardIndex, MaxGenerationAttempts + 17 + attempt));

            if (!TryBuildRemovalPlan(size, rng, out int[] removalOrder, out UnityGameManager.Direction[] directions))
            {
                continue;
            }

            UnityGameManager.LevelConfig level = CreateLevelFromPlan(boardIndex, size, matchTarget, removalOrder, directions, colorSequence);
            if (!HasHealthyColorMix(level.blocks, size)) continue;
            if (!HasHealthyDirectionMix(level.blocks, size)) continue;
            if (HasFacingArrows(level.blocks, size)) continue;
            if (!ValidatePlannedSolution(level.blocks, size, removalOrder)) continue;
            if (!IsTraySequenceSolvable(colorSequence, matchTarget, blueMatchTarget, yellowMatchTarget)) continue;
            if (!HasDifficultyShape(level, boardIndex, removalOrder)) continue;

            return level;
        }

        Debug.LogError($"Could not generate a valid fallback for Board {boardIndex + 1}. Returning an emergency board.");
        return BuildEmergencyLevel(boardIndex);
    }

    private static UnityGameManager.LevelConfig BuildEmergencyLevel(int boardIndex)
    {
        bool usesBlue = boardIndex >= FirstBlueBoardIndex;
        int blueMatchTarget = usesBlue ? GetBlueMatchTarget(boardIndex) : 0;
        int yellowMatchTarget = GetYellowMatchTarget(boardIndex);
        int size = GetBoardSize(boardIndex, usesBlue, yellowMatchTarget);
        int matchTarget = size;
        var colors = BuildColorBag(size, blueMatchTarget, yellowMatchTarget);

        var blocks = new List<UnityGameManager.BlockData>();
        for (int i = 0; i < size * size; i++)
        {
            UnityGameManager.BlockColor color = colors[i];
            blocks.Add(new UnityGameManager.BlockData
            {
                row = i / size,
                col = i % size,
                color = color,
                direction = i % 2 == 0 ? UnityGameManager.Direction.Left : UnityGameManager.Direction.Right,
                textLabel = LabelForColor(color)
            });
        }

        return new UnityGameManager.LevelConfig
        {
            id = boardIndex + 1,
            name = "Board " + (boardIndex + 1),
            boardSize = size,
            matchTarget = matchTarget,
            blocks = blocks
        };
    }
}
