using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CarParkingCapacityEditorVerifier
{
    [MenuItem("Car Prototype/Verify Parking Capacity Principle")]
    public static void VerifyAllLevels()
    {
        GameObject root = new GameObject("Parking Capacity Verifier");
        try
        {
            CarPrototype3D game = root.AddComponent<CarPrototype3D>();
            Invoke(game, "LoadFixedLevels");

            int campaignAndChallengeCount = VerifyLevelList(game, "levels", "Level");
            int demoCount = VerifyLevelList(game, "demoLevels", "Demo");
            Debug.Log(
                $"Parking-capacity principle passed for all {campaignAndChallengeCount} " +
                $"campaign/challenge levels and all {demoCount} demo levels. Police cars were excluded.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static int VerifyLevelList(CarPrototype3D game, string fieldName, string levelKind)
    {
        IList levels = (IList)GetField(game, fieldName);
        var violations = new List<string>();

        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            object level = levels[levelIndex];
            Type levelType = level.GetType();
            int boardNumber = (int)levelType.GetField("boardNumber").GetValue(level);
            int capacity = (int)levelType.GetField("matchTarget").GetValue(level);
            Array specifications = (Array)levelType.GetField("customSpecifications").GetValue(level);
            if (specifications == null)
            {
                object source = levelType.GetField("source").GetValue(level);
                MethodInfo builder = typeof(CarPrototype3D).GetMethod(
                    "BuildPrototypePieceSpecs",
                    BindingFlags.Static | BindingFlags.NonPublic);
                specifications = (Array)builder.Invoke(null, new[] { source });
            }

            var colorCounts = new SortedDictionary<string, int>();
            foreach (object specification in specifications)
            {
                object color = specification.GetType().GetField("color").GetValue(specification);
                string colorName = color.ToString();
                if (colorName == "Trash") continue;

                colorCounts[colorName] = colorCounts.TryGetValue(colorName, out int count)
                    ? count + 1
                    : 1;
            }

            if (capacity <= 0 || colorCounts.Count == 0)
            {
                violations.Add($"{levelKind} {boardNumber}: invalid capacity/content");
                continue;
            }

            foreach (KeyValuePair<string, int> colorCount in colorCounts)
            {
                if (colorCount.Value != capacity)
                {
                    violations.Add(
                        $"{levelKind} {boardNumber}: capacity {capacity}, " +
                        $"{colorCount.Key} has {colorCount.Value} cars");
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Parking-capacity principle failed. Police cars are excluded:\n" +
                string.Join("\n", violations));
        }

        return levels.Count;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException($"Missing field {fieldName}.");
        return field.GetValue(target);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException($"Missing method {methodName}.");
        method.Invoke(target, null);
    }
}
