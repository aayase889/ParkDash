using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ExperimentalCarLevelDesignValidator
{
    private const string DesignPath = "Assets/Experimental/Levels61-66/ExperimentalLevels61-66.json";
    private const string DatabasePath = "Assets/Resources/ColorSortLevelDatabase.asset";

    [Serializable]
    private sealed class Catalog
    {
        public int catalogVersion;
        public bool runtimeEnabled;
        public string summary;
        public List<ExperimentalLevel> levels;
    }

    [Serializable]
    private sealed class ExperimentalLevel
    {
        public int id;
        public int baseBoardId;
        public string title;
        public string primaryMechanic;
        public string difficultyIntent;
        public int parkingUseLimit;
        public int minimumParkingUses;
        public List<string> requiredColorOrder;
        public List<LockRule> locks;
        public List<string> designRules;
        public List<string> intendedStrategy;
        public List<string> successCriteria;
    }

    [Serializable]
    private sealed class LockRule
    {
        public int row;
        public int col;
        public string unlockAfterColor;
        public string presentation;
    }

    [MenuItem("Color Sort/Validate Experimental Levels 61-66")]
    public static void ValidateExperimentalLevels()
    {
        Require(File.Exists(DesignPath), $"Missing experimental level catalog at {DesignPath}.");
        Catalog catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(DesignPath));
        Require(catalog != null, "The experimental level catalog could not be parsed.");
        Require(catalog.catalogVersion == 1, "Unsupported experimental catalog version.");
        Require(catalog.runtimeEnabled, "Challenge Lab Levels 61-66 should be runtime-enabled for prototype testing.");
        Require(!string.IsNullOrWhiteSpace(catalog.summary), "The experimental catalog needs a summary.");
        Require(catalog.levels != null && catalog.levels.Count == 6, "Expected exactly six experimental levels.");

        ColorSortLevelDatabase database = AssetDatabase.LoadAssetAtPath<ColorSortLevelDatabase>(DatabasePath);
        Require(database != null && database.levels != null, "The fixed level database is unavailable.");
        Require(database.levels.Count == 60, "Experimental samples must not be appended to the playable database.");

        var ids = new HashSet<int>();
        for (int index = 0; index < catalog.levels.Count; index++)
        {
            ExperimentalLevel sample = catalog.levels[index];
            int expectedId = 61 + index;
            int expectedBaseId = 51 + index;
            Require(sample.id == expectedId, $"Experimental entry {index + 1} should be Level {expectedId}.");
            Require(sample.baseBoardId == expectedBaseId, $"Level {sample.id} should inherit Board {expectedBaseId}.");
            Require(ids.Add(sample.id), $"Experimental Level {sample.id} is duplicated.");
            Require(!string.IsNullOrWhiteSpace(sample.title), $"Level {sample.id} needs a title.");
            Require(!string.IsNullOrWhiteSpace(sample.primaryMechanic), $"Level {sample.id} needs a primary mechanic.");
            Require(!string.IsNullOrWhiteSpace(sample.difficultyIntent), $"Level {sample.id} needs a difficulty intention.");
            Require(sample.parkingUseLimit == -1 || sample.parkingUseLimit > 0,
                $"Level {sample.id} has an invalid parking-use limit.");
            Require(sample.minimumParkingUses >= 0, $"Level {sample.id} has a negative minimum parking-use count.");
            if (sample.parkingUseLimit > 0)
                Require(sample.minimumParkingUses <= sample.parkingUseLimit,
                    $"Level {sample.id} requires more parking entries than it allows.");

            UnityGameManager.LevelConfig baseBoard = database.levels[sample.baseBoardId - 1];
            Require(baseBoard.id == sample.baseBoardId, $"Level {sample.id} references the wrong base-board index.");
            Require(baseBoard.boardSize == 5 && baseBoard.matchTarget == 5,
                $"Level {sample.id} must inherit a validated 5x5 board with a five-car tray.");

            ValidateColorOrder(sample);
            ValidateLocks(sample, baseBoard);
            Require(sample.designRules != null && sample.designRules.Count > 0, $"Level {sample.id} needs design rules.");
            Require(sample.intendedStrategy != null && sample.intendedStrategy.Count > 0, $"Level {sample.id} needs an intended strategy.");
            Require(sample.successCriteria != null && sample.successCriteria.Count > 0, $"Level {sample.id} needs success criteria.");
        }

        Require(catalog.levels[2].primaryMechanic == "forced_parking" && catalog.levels[2].parkingUseLimit == 5,
            "Level 63 must prototype its validated five-entry parking challenge.");
        Require(catalog.levels[3].primaryMechanic == "locked_car" && catalog.levels[3].locks.Count > 0,
            "Level 64 must introduce a locked car.");
        Require(catalog.levels[4].primaryMechanic == "ordered_goals" && catalog.levels[4].requiredColorOrder.Count == 4,
            "Level 65 must introduce the four-color order.");
        Require(catalog.levels[5].primaryMechanic == "combined_mastery" && catalog.levels[5].locks.Count >= 2 && catalog.levels[5].parkingUseLimit == 4,
            "Level 66 must combine locks with limited parking.");

        VerifyRuntimeSequence();

        Debug.Log("[Experimental Level Designs] PASS: optional Challenge Lab Levels 61-66 are playable while the permanent database remains capped at 60.");
    }

    private static void VerifyRuntimeSequence()
    {
        GameObject testRoot = null;
        try
        {
            testRoot = new GameObject("Temporary Level Sequence Verification");
            CarPrototype3D game = testRoot.AddComponent<CarPrototype3D>();
            MethodInfo loadLevels = typeof(CarPrototype3D).GetMethod("LoadFixedLevels", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo levelsField = typeof(CarPrototype3D).GetField("levels", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(loadLevels != null && levelsField != null, "The 3D runtime level loader could not be inspected.");
            loadLevels.Invoke(game, null);

            System.Collections.IList runtimeLevels = levelsField.GetValue(game) as System.Collections.IList;
            Require(runtimeLevels != null && runtimeLevels.Count == 86,
                "The 3D runtime should expose 20 tutorials, 60 preserved boards, and six Challenge Lab samples.");

            object lastLevel = runtimeLevels[85];
            FieldInfo boardNumber = lastLevel.GetType().GetField("boardNumber", BindingFlags.Public | BindingFlags.Instance);
            Require(boardNumber != null && (int)boardNumber.GetValue(lastLevel) == 86,
                "The Challenge Lab runtime sequence does not end at Level 86.");
        }
        finally
        {
            if (testRoot != null) UnityEngine.Object.DestroyImmediate(testRoot);
        }
    }

    private static void ValidateColorOrder(ExperimentalLevel sample)
    {
        if (sample.requiredColorOrder == null) return;
        var colors = new HashSet<string>();
        foreach (string color in sample.requiredColorOrder)
        {
            Require(IsGoalColor(color), $"Level {sample.id} has an invalid ordered-goal color '{color}'.");
            Require(colors.Add(color), $"Level {sample.id} repeats '{color}' in its ordered goals.");
        }
    }

    private static void ValidateLocks(ExperimentalLevel sample, UnityGameManager.LevelConfig baseBoard)
    {
        if (sample.locks == null) return;
        var lockedCells = new HashSet<int>();
        foreach (LockRule lockRule in sample.locks)
        {
            Require(lockRule.row >= 0 && lockRule.row < 5 && lockRule.col >= 0 && lockRule.col < 5,
                $"Level {sample.id} has a lock outside the 5x5 board.");
            Require(IsGoalColor(lockRule.unlockAfterColor),
                $"Level {sample.id} has an invalid lock color '{lockRule.unlockAfterColor}'.");
            Require(!string.IsNullOrWhiteSpace(lockRule.presentation),
                $"Level {sample.id} lock at {lockRule.row},{lockRule.col} needs visible presentation guidance.");
            Require(lockedCells.Add(lockRule.row * 5 + lockRule.col),
                $"Level {sample.id} repeats a locked cell.");

            UnityGameManager.BlockData block = FindBlock(baseBoard, lockRule.row, lockRule.col);
            Require(block.color != UnityGameManager.BlockColor.Neutral,
                $"Level {sample.id} lock at {lockRule.row},{lockRule.col} points to a police blocker instead of a colored car.");
        }
    }

    private static UnityGameManager.BlockData FindBlock(UnityGameManager.LevelConfig level, int row, int col)
    {
        foreach (UnityGameManager.BlockData block in level.blocks)
        {
            if (block.row == row && block.col == col) return block;
        }

        throw new InvalidOperationException($"Board {level.id} is missing cell {row},{col}.");
    }

    private static bool IsGoalColor(string color)
    {
        return color == "red" || color == "green" || color == "blue" || color == "yellow";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
