using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression checks for the idle safe-move hint and the immediate blocker hint.
/// </summary>
public static class GameplayHintEditorVerifier
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Car Prototype/Verify Gameplay Hints")]
    public static void RunBatch()
    {
        Type gameType = typeof(CarPrototype3D);
        FieldInfo hintDelay = gameType.GetField("AutomaticSafeMoveHintDelay", PrivateStatic);
        Require(
            hintDelay != null
            && Mathf.Abs((float)hintDelay.GetRawConstantValue() - 30f) < 0.001f,
            "The automatic safe-move hint is not using the approved 30-second delay.");

        GameObject root = null;
        try
        {
            root = new GameObject("Gameplay Hint Verification");
            CarPrototype3D game = root.AddComponent<CarPrototype3D>();
            SetField(gameType, game, "activeBoardSize", 2);
            SetField(gameType, game, "activeRedTarget", 1);
            SetField(gameType, game, "trayCapacity", 1);

            CarPuzzlePiece safeCar = CreatePiece(
                root.transform,
                "Safe Red Car",
                0,
                0,
                "Red",
                "Up");
            CarPuzzlePiece blockedCar = CreatePiece(
                root.transform,
                "Blocked Red Car",
                1,
                0,
                "Red",
                "Up");

            IList boardPieces = GetList(gameType, game, "boardPieces");
            IList allPieces = GetList(gameType, game, "allPieces");
            boardPieces.Add(safeCar);
            boardPieces.Add(blockedCar);
            allPieces.Add(safeCar);
            allPieces.Add(blockedCar);

            MethodInfo findBlocker = gameType.GetMethod("FindFirstExitBlocker", PrivateInstance);
            Require(findBlocker != null, "The first-blocking-car lookup is missing.");
            Require(
                findBlocker.Invoke(game, new object[] { blockedCar }) == safeCar,
                "A blocked tap did not identify the car physically blocking its exit.");

            MethodInfo findSafeHint = gameType.GetMethod("TryFindSolverApprovedHintPiece", PrivateInstance);
            Require(findSafeHint != null, "The solver-backed safe-move hint lookup is missing.");
            object[] arguments = { null };
            Require(
                (bool)findSafeHint.Invoke(game, arguments),
                "The solver could not find a safe hint on a valid board.");
            Require(
                arguments[0] == safeCar,
                "The hint selected a blocked or unsafe car instead of the solver-approved car.");

            Debug.Log(
                "[Gameplay Hint Verification] PASS: the 30-second idle hint selects a "
                + "solver-approved car and blocked taps identify their first blocker.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CarPuzzlePiece CreatePiece(
        Transform parent,
        string objectName,
        int row,
        int col,
        string color,
        string direction)
    {
        GameObject pieceObject = new GameObject(objectName);
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();
        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod("Configure", PrivateInstance);
        Type colorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        Require(configure != null && colorType != null && directionType != null,
            "The car configuration API is missing.");
        configure.Invoke(piece, new object[]
        {
            row,
            col,
            Enum.Parse(colorType, color),
            Enum.Parse(directionType, direction),
            Vector3.zero,
            1f,
            1f,
            1,
            false,
            -1,
            -1,
            false,
            false
        });
        return piece;
    }

    private static IList GetList(Type type, object target, string name)
    {
        FieldInfo field = type.GetField(name, PrivateInstance);
        Require(field != null, $"Missing list: {name}");
        return (IList)field.GetValue(target);
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        FieldInfo field = type.GetField(name, PrivateInstance);
        Require(field != null, $"Missing field: {name}");
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
