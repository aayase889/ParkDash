using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

public static class RefinedCatalogAudit
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static string VerifyGeneratedDefinitions(CarPrototype3D game)
    {
        if (ReferenceEquals(game, null)) throw new ArgumentNullException(nameof(game));
        Type gameType = typeof(CarPrototype3D);
        IList levels = (IList)GetField(gameType, game, "levels");
        levels.Clear();
        Invoke(gameType, game, "BuildTutorialLevels", new object[] { null });
        Invoke(gameType, game, "BuildRefinedCatalogLevels", null);
        if (levels.Count != 100)
            throw new InvalidOperationException($"Expected 100 levels, found {levels.Count}.");

        int boxLevels = 0;
        int garageLevels = 0;
        int repeatedSetGarageLevels = 0;
        int oneGarageLevels = 0;
        int twoGarageLevels = 0;
        int fiveCapacityLevels = 0;
        int normalLevels = 0;
        int hardLevels = 0;
        int superHardLevels = 0;
        int hardestOpeningCount = 0;
        int shallowestHardChain = int.MaxValue;
        int previousBoardSize = 0;
        for (int levelIndex = 20; levelIndex < levels.Count; levelIndex++)
        {
            object level = levels[levelIndex];
            Type levelType = level.GetType();
            int boardNumber = (int)GetField(levelType, level, "boardNumber");
            int boardSize = (int)GetField(levelType, level, "boardSize");
            int capacity = (int)GetField(levelType, level, "matchTarget");
            Array specifications = (Array)GetField(levelType, level, "customSpecifications");
            Array garages = (Array)GetField(levelType, level, "garageSpecifications");
            string difficulty = GetField(levelType, level, "difficulty").ToString();

            if (boardNumber != levelIndex + 1)
                throw new InvalidOperationException($"Level index {levelIndex + 1} maps to {boardNumber}.");
            if (difficulty != ExpectedDifficulty(boardNumber))
                throw new InvalidOperationException($"Level {boardNumber} has wrong difficulty {difficulty}.");
            if (boardNumber > 21 && boardSize < previousBoardSize)
                throw new InvalidOperationException(
                    $"Level {boardNumber} shrinks the grid from {previousBoardSize} to {boardSize}.");
            previousBoardSize = boardSize;
            if (difficulty == "Normal") normalLevels++;
            else if (difficulty == "Hard") hardLevels++;
            else if (difficulty == "SuperHard") superHardLevels++;

            if (difficulty != "Normal")
            {
                MethodInfo geometryMethod = gameType.GetMethod(
                    "MeetsRefinedDifficultyGeometry",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (geometryMethod == null)
                    throw new MissingMethodException(
                        gameType.FullName,
                        "MeetsRefinedDifficultyGeometry");
                object difficultyValue = GetField(levelType, level, "difficulty");
                object[] geometryArguments =
                {
                    boardSize,
                    specifications,
                    garages,
                    difficultyValue,
                    0,
                    0
                };
                bool meetsDifficulty = (bool)geometryMethod.Invoke(null, geometryArguments);
                int openingMoves = (int)geometryArguments[4];
                int blockerChain = (int)geometryArguments[5];
                if (!meetsDifficulty)
                    throw new InvalidOperationException(
                        $"Level {boardNumber} is labelled {difficulty} but has "
                        + $"{openingMoves} opening moves and blocker depth {blockerChain}.");
                hardestOpeningCount = Math.Max(hardestOpeningCount, openingMoves);
                shallowestHardChain = Math.Min(shallowestHardChain, blockerChain);
            }

            bool[,] occupied = new bool[boardSize, boardSize];
            int[] colors = new int[7];
            int boxes = 0;
            for (int pieceIndex = 0; pieceIndex < specifications.Length; pieceIndex++)
            {
                object specification = specifications.GetValue(pieceIndex);
                Type pieceType = specification.GetType();
                int row = (int)GetField(pieceType, specification, "row");
                int col = (int)GetField(pieceType, specification, "col");
                int length = (int)GetField(pieceType, specification, "cellLength");
                int color = Convert.ToInt32(GetField(pieceType, specification, "color"));
                string direction = GetField(pieceType, specification, "direction").ToString();
                bool boxed = (bool)GetField(pieceType, specification, "isRevealBox");
                if (color != 5) colors[color]++;
                if (boxed) boxes++;
                Vector2Int step = Step(direction);
                for (int offset = 0; offset < length; offset++)
                {
                    int cellRow = row - step.y * offset;
                    int cellCol = col - step.x * offset;
                    if (cellRow < 0 || cellRow >= boardSize || cellCol < 0 || cellCol >= boardSize
                        || occupied[cellRow, cellCol])
                        throw new InvalidOperationException($"Level {boardNumber} has an invalid grid tiling.");
                    occupied[cellRow, cellCol] = true;
                }
            }

            if ((boardNumber >= 30) != (boxes > 0))
                throw new InvalidOperationException($"Level {boardNumber} violates the Level 30 box start.");
            if (boxes > 0) boxLevels++;
            bool hasGarage = garages.Length > 0;
            if ((boardNumber >= 50) != hasGarage)
                throw new InvalidOperationException($"Level {boardNumber} violates the Level 50 garage start.");
            if (hasGarage) garageLevels++;
            if (garages.Length == 1) oneGarageLevels++;
            if (garages.Length == 2) twoGarageLevels++;
            if (capacity == 5) fiveCapacityLevels++;

            for (int garageIndex = 0; garageIndex < garages.Length; garageIndex++)
            {
                object garage = garages.GetValue(garageIndex);
                Type garageType = garage.GetType();
                int row = (int)GetField(garageType, garage, "row");
                int col = (int)GetField(garageType, garage, "col");
                Array queue = (Array)GetField(garageType, garage, "carQueue");
                if (row < 0 || row >= boardSize - 1 || occupied[row, col])
                    throw new InvalidOperationException($"Level {boardNumber} has invalid garage placement.");
                occupied[row, col] = true;
                if (occupied[row + 1, col])
                    throw new InvalidOperationException($"Level {boardNumber} garage front overlaps another piece.");
                occupied[row + 1, col] = true;
                bool[] queueColors = new bool[7];
                int distinct = 0;
                for (int queueIndex = 0; queueIndex < queue.Length; queueIndex++)
                {
                    int color = Convert.ToInt32(queue.GetValue(queueIndex));
                    colors[color]++;
                    if (!queueColors[color])
                    {
                        queueColors[color] = true;
                        distinct++;
                    }
                }
                if (distinct < 2)
                    throw new InvalidOperationException($"Level {boardNumber} has a single-color garage queue.");
            }

            for (int row = 0; row < boardSize; row++)
            for (int col = 0; col < boardSize; col++)
                if (!occupied[row, col])
                    throw new InvalidOperationException($"Level {boardNumber} leaves {row},{col} empty.");

            bool repeatedSet = false;
            for (int color = 0; color < colors.Length; color++)
            {
                if (color == 5 || colors[color] == 0) continue;
                bool valid = hasGarage ? colors[color] % capacity == 0 : colors[color] == capacity;
                if (!valid)
                    throw new InvalidOperationException(
                        $"Level {boardNumber}: color {color} count {colors[color]} is invalid for capacity {capacity}.");
                repeatedSet |= colors[color] > capacity;
            }
            if (hasGarage && repeatedSet) repeatedSetGarageLevels++;
        }

        if (boxLevels != 71 || garageLevels != 51 || repeatedSetGarageLevels != 51
            || oneGarageLevels == 0 || twoGarageLevels == 0 || fiveCapacityLevels == 0)
            throw new InvalidOperationException(
                $"Unexpected feature totals: boxes {boxLevels}, garages {garageLevels}, "
                + $"one-garage {oneGarageLevels}, two-garage {twoGarageLevels}, "
                + $"capacity-five {fiveCapacityLevels}, repeated-set garages {repeatedSetGarageLevels}.");
        if (normalLevels != 48 || hardLevels != 16 || superHardLevels != 16)
            throw new InvalidOperationException(
                $"Difficulty totals are wrong: normal {normalLevels}, hard {hardLevels}, super hard {superHardLevels}.");
        return "Levels 21-100 passed: cadence, tiling, box/garage starts, mixed queues, "
            + $"color-set arithmetic, and measured Hard/Super Hard blocker geometry "
            + $"(maximum openings {hardestOpeningCount}, minimum blocker depth {shallowestHardChain}).";
    }

    public static string VerifyGeneratedDefinitionsWithoutScene()
    {
        CarPrototype3D game = (CarPrototype3D)FormatterServices.GetUninitializedObject(
            typeof(CarPrototype3D));
        SetField(
            typeof(CarPrototype3D),
            game,
            "levels",
            Activator.CreateInstance(typeof(List<>).MakeGenericType(
                typeof(CarPrototype3D).GetNestedType("PrototypeLevel", BindingFlags.NonPublic))));
        return VerifyGeneratedDefinitions(game);
    }

    private static string ExpectedDifficulty(int boardNumber)
    {
        int position = (boardNumber - 21) % 5;
        return position == 3 ? "Hard" : position == 4 ? "SuperHard" : "Normal";
    }

    private static Vector2Int Step(string direction)
    {
        if (direction == "Up") return new Vector2Int(0, -1);
        if (direction == "Down") return new Vector2Int(0, 1);
        if (direction == "Left") return new Vector2Int(-1, 0);
        return new Vector2Int(1, 0);
    }

    private static object GetField(Type type, object instance, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, AnyInstance);
        if (field == null) throw new MissingFieldException(type.FullName, fieldName);
        return field.GetValue(instance);
    }

    private static object Invoke(Type type, object instance, string methodName, object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, PrivateInstance);
        if (method == null) throw new MissingMethodException(type.FullName, methodName);
        return method.Invoke(instance, arguments);
    }

    private static void SetField(Type type, object instance, string fieldName, object value)
    {
        FieldInfo field = type.GetField(fieldName, AnyInstance);
        if (field == null) throw new MissingFieldException(type.FullName, fieldName);
        field.SetValue(instance, value);
    }
}
