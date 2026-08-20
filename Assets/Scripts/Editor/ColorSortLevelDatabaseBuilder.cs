using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ColorSortLevelDatabaseBuilder
{
    private const int LevelCount = 60;
    private const string AssetPath = "Assets/Resources/ColorSortLevelDatabase.asset";

    [MenuItem("Color Sort/Regenerate Fixed Level Database")]
    public static void RegenerateFixedLevelDatabase()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Color Sort Levels", "Generating fixed solvable levels...", 0.25f);
            List<UnityGameManager.LevelConfig> generatedLevels = ColorSortLevelBuilder.BuildLevels(LevelCount);

            EditorUtility.DisplayProgressBar("Color Sort Levels", "Validating generated levels...", 0.75f);
            ColorSortLevelDesignValidator.ValidateLevelStructure(generatedLevels);

            Directory.CreateDirectory("Assets/Resources");
            ColorSortLevelDatabase database = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>(AssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ColorSortLevelDatabase>();
                AssetDatabase.CreateAsset(database, AssetPath);
            }

            database.levels = CloneLevelList(generatedLevels);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            string solverReport = CarLevelSimulator.ValidateStandardTrayRangeOrThrow(0, LevelCount);
            Debug.Log(solverReport);
            Debug.Log($"Saved {database.levels.Count} fixed Color Sort levels to {AssetPath}.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Color Sort/Repair Parking Capacity Violations")]
    public static void RepairParkingCapacityViolations()
    {
        try
        {
            ColorSortLevelDatabase database = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>(AssetPath);
            if (database == null || database.levels == null || database.levels.Count != LevelCount)
                throw new System.InvalidOperationException($"Expected {LevelCount} existing fixed boards at {AssetPath}.");

            EditorUtility.DisplayProgressBar(
                "Color Sort Levels",
                "Generating exact-capacity replacements...",
                0.25f);
            List<UnityGameManager.LevelConfig> generatedLevels = ColorSortLevelBuilder.BuildLevels(LevelCount);
            var repairedLevels = new List<UnityGameManager.LevelConfig>(LevelCount);
            var repairedBoardNumbers = new List<int>();

            for (int index = 0; index < LevelCount; index++)
            {
                UnityGameManager.LevelConfig existingLevel = database.levels[index];
                if (FollowsParkingCapacityPrinciple(existingLevel))
                {
                    repairedLevels.Add(CloneLevel(existingLevel));
                    continue;
                }

                repairedLevels.Add(CloneLevel(generatedLevels[index]));
                repairedBoardNumbers.Add(index + 1);
            }

            EditorUtility.DisplayProgressBar(
                "Color Sort Levels",
                "Validating repaired levels...",
                0.75f);
            ColorSortLevelDesignValidator.ValidateLevelStructure(repairedLevels);

            database.levels = repairedLevels;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);

            string solverReport = CarLevelSimulator.ValidateStandardTrayRangeOrThrow(0, LevelCount);
            Debug.Log(solverReport);
            Debug.Log(
                $"Repaired {repairedBoardNumbers.Count} parking-capacity boards: " +
                string.Join(", ", repairedBoardNumbers) + ". Police cars were excluded.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Color Sort/Add 5x5 Boards 51-60")]
    public static void AddFiveByFiveBoards()
    {
        try
        {
            ColorSortLevelDatabase database = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>(AssetPath);
            if (database == null || database.levels == null || database.levels.Count < 50)
                throw new System.InvalidOperationException("The existing first 50 fixed boards are unavailable.");

            var updatedLevels = new List<UnityGameManager.LevelConfig>(LevelCount);
            for (int index = 0; index < 50; index++)
                updatedLevels.Add(CloneLevel(database.levels[index]));

            for (int boardIndex = 50; boardIndex < LevelCount; boardIndex++)
            {
                EditorUtility.DisplayProgressBar("Color Sort Levels", $"Generating 5x5 Board {boardIndex + 1}...", (boardIndex - 50) / 10f);
                UnityGameManager.LevelConfig level = ColorSortLevelBuilder.BuildLevel(boardIndex);
                if (level.boardSize != 5 || level.matchTarget != 5)
                    throw new System.InvalidOperationException($"Board {boardIndex + 1} was not generated as 5x5.");
                updatedLevels.Add(CloneLevel(level));
            }

            database.levels = updatedLevels;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            Debug.Log("Saved the original Boards 1-50 plus new 5x5 Boards 51-60.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static List<UnityGameManager.LevelConfig> CloneLevelList(List<UnityGameManager.LevelConfig> source)
    {
        var result = new List<UnityGameManager.LevelConfig>(source.Count);
        foreach (UnityGameManager.LevelConfig level in source)
        {
            result.Add(new UnityGameManager.LevelConfig
            {
                id = level.id,
                name = level.name,
                boardSize = level.boardSize,
                matchTarget = level.matchTarget,
                blocks = new List<UnityGameManager.BlockData>(level.blocks)
            });
        }

        return result;
    }

    private static bool FollowsParkingCapacityPrinciple(UnityGameManager.LevelConfig level)
    {
        if (level == null || level.blocks == null || level.matchTarget <= 0) return false;

        int[] colorCounts = new int[4];
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            if (block.color == UnityGameManager.BlockColor.Neutral) continue;
            colorCounts[(int)block.color]++;
        }

        bool hasColoredCars = false;
        for (int index = 0; index < colorCounts.Length; index++)
        {
            if (colorCounts[index] == 0) continue;
            hasColoredCars = true;
            if (colorCounts[index] != level.matchTarget) return false;
        }

        return hasColoredCars;
    }

    private static UnityGameManager.LevelConfig CloneLevel(UnityGameManager.LevelConfig level)
    {
        return new UnityGameManager.LevelConfig
        {
            id = level.id,
            name = level.name,
            boardSize = level.boardSize,
            matchTarget = level.matchTarget,
            blocks = new List<UnityGameManager.BlockData>(level.blocks)
        };
    }
}
