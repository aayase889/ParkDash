using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the rapid-tap traffic contract: independent lane segments may run
/// concurrently, while overlapping and crossing segments remain serialized.
/// </summary>
public static class ConcurrentCarMovementEditorVerifier
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Car Prototype/Verify Concurrent Car Movement")]
    public static void RunBatch()
    {
        Type prototypeType = typeof(CarPrototype3D);
        FieldInfo activeClaims = prototypeType.GetField("activeTrayTrafficClaims", PrivateInstance);
        Require(activeClaims != null, "The segment-level tray traffic claim list is missing.");
        Require(
            prototypeType.GetField("trayApproachOwner", PrivateInstance) == null,
            "The old whole-route tray owner lock is still present.");

        MethodInfo overlapMethod = prototypeType.GetMethod(
            "TrayTrafficCenterlinesOverlap",
            PrivateStatic);
        Require(overlapMethod != null, "The tray traffic overlap test is missing.");

        Vector3[] leftLane =
        {
            new Vector3(-1.2f, 0f, 3f),
            new Vector3(-1.2f, 0f, -3f)
        };
        Vector3[] rightLane =
        {
            new Vector3(1.2f, 0f, 3f),
            new Vector3(1.2f, 0f, -3f)
        };
        Require(
            !Overlaps(overlapMethod, leftLane, 0.42f, rightLane, 0.42f),
            "Separated parallel lanes were incorrectly serialized.");

        Vector3[] crossingLane =
        {
            new Vector3(-2f, 0f, 0f),
            new Vector3(2f, 0f, 0f)
        };
        Require(
            Overlaps(overlapMethod, leftLane, 0.42f, crossingLane, 0.42f),
            "Crossing routes were not protected from a collision.");

        Vector3[] followingLane =
        {
            new Vector3(-1.2f, 0f, 2f),
            new Vector3(-1.2f, 0f, -1f)
        };
        Require(
            Overlaps(overlapMethod, leftLane, 0.42f, followingLane, 0.42f),
            "Overlapping same-lane routes were not protected from a rear-end collision.");

        Type reservationType = prototypeType.GetNestedType(
            "TrayTrafficReservation",
            BindingFlags.NonPublic);
        Require(reservationType != null, "The per-car traffic reservation state is missing.");
        Require(
            reservationType.GetField("minimumHalfWidth", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "The dominant exchange cannot reserve the complete vehicle footprint.");
        MethodInfo continuousRoute = typeof(CarPuzzlePiece).GetMethod(
            "DriveContinuousTrayPath",
            PrivateInstance,
            null,
            new[]
            {
                typeof(System.Collections.Generic.List<Vector3>),
                typeof(float),
                typeof(float),
                typeof(CarMatchVfx),
                typeof(Func<Vector3[], bool>)
            },
            null);
        Require(
            continuousRoute != null,
            "The uninterrupted board-to-tray animation driver is missing.");
        MethodInfo arrivalSettle = typeof(CarPuzzlePiece).GetMethod(
            "PlayTrayArrivalSettle",
            PrivateInstance,
            null,
            new[] { typeof(float) },
            null);
        Require(
            arrivalSettle != null,
            "The non-blocking tray arrival settle animation is missing.");

        FieldInfo boardSpeed = prototypeType.GetField(
            "CarDriveSpeed",
            BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo outsideSpeed = prototypeType.GetField(
            "OutsideCarDriveSpeed",
            BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo matchAnticipation = prototypeType.GetField(
            "MatchAnticipationDuration",
            BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo cornerSpeedRatio = prototypeType.GetField(
            "ContinuousRouteCornerSpeedRatio",
            BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo slowdownFraction = prototypeType.GetField(
            "ContinuousRouteSlowdownFraction",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(
            boardSpeed != null
            && Mathf.Abs((float)boardSpeed.GetRawConstantValue() - 18f) < 0.001f,
            "Board exit movement is not using the approved balanced speed.");
        Require(
            outsideSpeed != null
            && Mathf.Abs((float)outsideSpeed.GetRawConstantValue() - 30f) < 0.001f,
            "Outside route movement is not using the approved balanced speed.");
        Require(
            matchAnticipation != null
            && (float)matchAnticipation.GetRawConstantValue() <= 0.05f,
            "A completed match still holds rapid queued input for too long.");
        Require(
            cornerSpeedRatio != null
            && (float)cornerSpeedRatio.GetRawConstantValue() >= 0.80f,
            "Rounded routes still lose too much speed at their corners.");
        Require(
            slowdownFraction != null
            && (float)slowdownFraction.GetRawConstantValue() <= 0.15f,
            "Tray arrival braking begins too early in the route.");

        MethodInfo pathBuilder = prototypeType.GetMethod(
            "BuildContinuousTrayPath",
            PrivateStatic);
        Require(pathBuilder != null, "The continuous rounded-path builder is missing.");
        Vector3 pathStart = new Vector3(-2f, 0f, 2f);
        var authoredWaypoints = new System.Collections.Generic.List<Vector3>
        {
            new Vector3(-2f, 0f, 0f),
            new Vector3(1.5f, 0f, 0f),
            new Vector3(1.5f, 0f, -2f)
        };
        var continuousPath = (System.Collections.Generic.List<Vector3>)pathBuilder.Invoke(
            null,
            new object[] { pathStart, authoredWaypoints, 0.70f });
        Require(
            continuousPath.Count > authoredWaypoints.Count + 1,
            "The continuous path did not sample its corners into a smooth curve.");
        Require(
            Vector3.Distance(continuousPath[0], pathStart) < 0.0001f
            && Vector3.Distance(
                continuousPath[continuousPath.Count - 1],
                authoredWaypoints[authoredWaypoints.Count - 1]) < 0.0001f,
            "The continuous path does not preserve its exact start and parking destination.");
        for (int index = 1; index < continuousPath.Count; index++)
        {
            Require(
                Vector3.Distance(continuousPath[index - 1], continuousPath[index]) > 0.001f,
                "The continuous path contains a zero-length frame-stalling segment.");
        }
        VerifyDistanceSampledSteering(continuousPath);
        MethodInfo trafficWindowBuilder = typeof(CarPuzzlePiece).GetMethod(
            "BuildContinuousTrafficWindow",
            PrivateStatic);
        Require(
            trafficWindowBuilder != null,
            "The rolling traffic look-ahead window is missing.");
        var trafficWindow = (Vector3[])trafficWindowBuilder.Invoke(
            null,
            new object[] { continuousPath, 0, continuousPath[0], 8 });
        Require(
            trafficWindow.Length == 9
            && Vector3.Distance(trafficWindow[0], continuousPath[0]) < 0.0001f
            && Vector3.Distance(trafficWindow[8], continuousPath[8]) < 0.0001f,
            "The rolling traffic reservation does not cover the expected short look-ahead window.");

        VerifyClaimScheduling(
            prototypeType,
            reservationType,
            activeClaims,
            leftLane,
            rightLane,
            crossingLane);
        VerifyResponsiveAnimationInputGate(prototypeType);
        VerifyDominantExchangeContract(prototypeType, overlapMethod);

        Debug.Log(
            "[Concurrent Car Movement Verification] PASS: rapid taps use a balanced uninterrupted "
            + "route on separated lanes; crossing and same-lane routes retain collision protection; "
            + "the dominant full-tray exchange accepts only an unambiguous N-1+1 pattern and "
            + "protects complete vehicle radii; distance-sampled steering preserves fast rounded "
            + "turns; the cosmetic tray settle no longer owns the road.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void VerifyResponsiveAnimationInputGate(Type prototypeType)
    {
        MethodInfo isBlocked = prototypeType.GetMethod(
            "IsVehicleInputBlockedByExclusiveAnimation",
            PrivateInstance);
        FieldInfo isAnimating = prototypeType.GetField("isAnimating", PrivateInstance);
        FieldInfo boxReveals = prototypeType.GetField(
            "boxRevealAnimationsInProgress",
            PrivateInstance);
        FieldInfo garageTransitions = prototypeType.GetField(
            "garageTransitionsInProgress",
            PrivateInstance);
        Require(
            isBlocked != null
            && isAnimating != null
            && boxReveals != null
            && garageTransitions != null,
            "The responsive gameplay-animation input gate is missing.");

        GameObject root = null;
        try
        {
            root = new GameObject("Responsive Animation Input Verification");
            CarPrototype3D prototype = root.AddComponent<CarPrototype3D>();
            isAnimating.SetValue(prototype, true);
            Require(
                (bool)isBlocked.Invoke(prototype, null),
                "An untracked exclusive sequence did not protect input.");

            boxReveals.SetValue(prototype, 1);
            Require(
                !(bool)isBlocked.Invoke(prototype, null),
                "A box reveal still blocks an unrelated valid tap.");

            boxReveals.SetValue(prototype, 0);
            garageTransitions.SetValue(prototype, 1);
            Require(
                !(bool)isBlocked.Invoke(prototype, null),
                "A garage transition still blocks an unrelated valid tap.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyDistanceSampledSteering(
        System.Collections.Generic.List<Vector3> path)
    {
        MethodInfo sampleMethod = typeof(CarPuzzlePiece).GetMethod(
            "SampleContinuousPathAtDistance",
            PrivateStatic,
            null,
            new[]
            {
                typeof(System.Collections.Generic.List<Vector3>),
                typeof(float[]),
                typeof(float),
                typeof(int).MakeByRefType()
            },
            null);
        Require(
            sampleMethod != null,
            "The frame-rate-independent distance sampler for smooth steering is missing.");

        var cumulativeDistance = new float[path.Count];
        for (int index = 1; index < path.Count; index++)
        {
            cumulativeDistance[index] = cumulativeDistance[index - 1]
                + Vector3.Distance(path[index - 1], path[index]);
        }

        float sampleDistance = cumulativeDistance[cumulativeDistance.Length - 1] * 0.50f;
        object[] arguments = { path, cumulativeDistance, sampleDistance, 0 };
        Vector3 sampled = (Vector3)sampleMethod.Invoke(null, arguments);
        int sampledSegment = (int)arguments[3];
        Require(
            sampledSegment >= 0
            && sampledSegment < path.Count - 1
            && Vector3.Distance(sampled, path[0]) > 0.10f
            && Vector3.Distance(sampled, path[path.Count - 1]) > 0.10f,
            "The distance sampler did not return a stable interior route position.");
    }

    private static void VerifyDominantExchangeContract(
        Type prototypeType,
        MethodInfo overlapMethod)
    {
        MethodInfo findOutlier = prototypeType.GetMethod(
            "TryFindDominantExchangeOutlier",
            PrivateStatic);
        Require(findOutlier != null, "The full-tray dominant exchange rule is missing.");

        Type colorType = prototypeType.GetNestedType("PieceColor", BindingFlags.NonPublic);
        FieldInfo colorField = typeof(CarPuzzlePiece).GetField(
            "<PieceColor>k__BackingField",
            PrivateInstance);
        Require(colorType != null && colorField != null, "The car-color test seam is missing.");

        GameObject root = null;
        try
        {
            root = new GameObject("Dominant Exchange Rule Verification");
            object purple = Enum.ToObject(colorType, 3);
            object yellow = Enum.ToObject(colorType, 4);
            object blue = Enum.ToObject(colorType, 2);
            var tray = new System.Collections.Generic.List<CarPuzzlePiece>();
            for (int index = 0; index < 3; index++)
            {
                CarPuzzlePiece car = CreatePiece(root.transform, $"Purple Tray Car {index + 1}");
                colorField.SetValue(car, purple);
                tray.Add(car);
            }
            CarPuzzlePiece yellowOutlier = CreatePiece(root.transform, "Yellow Limousine Outlier");
            colorField.SetValue(yellowOutlier, yellow);
            tray.Add(yellowOutlier);

            object[] validArguments = { tray, purple, 4, 4, null, -1 };
            Require(
                (bool)findOutlier.Invoke(null, validArguments)
                && validArguments[4] == yellowOutlier
                && (int)validArguments[5] == 3,
                "A full 3-purple + 1-yellow tray did not authorize the one-tap exchange.");

            colorField.SetValue(tray[1], blue);
            object[] ambiguousArguments = { tray, purple, 4, 4, null, -1 };
            Require(
                !(bool)findOutlier.Invoke(null, ambiguousArguments),
                "An ambiguous 2+1+1 tray incorrectly chose an outlier.");

            Vector3[] trayDeparture =
            {
                new Vector3(0f, 0f, -2f),
                new Vector3(0f, 0f, 0f),
                new Vector3(-2f, 0f, 0f)
            };
            Vector3[] incomingApproach =
            {
                new Vector3(1.45f, 0f, 2f),
                new Vector3(1.45f, 0f, -2f)
            };
            Require(
                Overlaps(
                    overlapMethod,
                    trayDeparture,
                    0.78f,
                    incomingApproach,
                    0.78f),
                "Full vehicle radii did not protect two visually separate centerlines from touching.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyClaimScheduling(
        Type prototypeType,
        Type reservationType,
        FieldInfo activeClaimsField,
        Vector3[] leftLane,
        Vector3[] rightLane,
        Vector3[] crossingLane)
    {
        GameObject root = null;
        try
        {
            root = new GameObject("Concurrent Car Movement Verification");
            CarPrototype3D prototype = root.AddComponent<CarPrototype3D>();
            CarPuzzlePiece firstCar = CreatePiece(root.transform, "First Rapid-Tap Car");
            CarPuzzlePiece secondCar = CreatePiece(root.transform, "Second Rapid-Tap Car");
            CarPuzzlePiece crossingCar = CreatePiece(root.transform, "Crossing Rapid-Tap Car");

            FieldInfo trayPiecesField = prototypeType.GetField("trayPieces", PrivateInstance);
            Require(trayPiecesField != null, "The logical tray reservation list is missing.");
            IList trayPieces = (IList)trayPiecesField.GetValue(prototype);
            trayPieces.Add(firstCar);
            trayPieces.Add(secondCar);
            trayPieces.Add(crossingCar);

            MethodInfo acquireClaim = prototypeType.GetMethod(
                "TryAcquireTrayTrafficClaim",
                PrivateInstance,
                null,
                new[] { typeof(CarPuzzlePiece), reservationType, typeof(Vector3[]) },
                null);
            Require(acquireClaim != null, "The segment claim acquisition method is missing.");

            object firstReservation = Activator.CreateInstance(reservationType, true);
            object secondReservation = Activator.CreateInstance(reservationType, true);
            object crossingReservation = Activator.CreateInstance(reservationType, true);
            Require(
                (bool)acquireClaim.Invoke(
                    prototype,
                    new[] { firstCar, firstReservation, leftLane }),
                "The first valid tap unexpectedly waited before claiming its route.");
            Require(
                (bool)acquireClaim.Invoke(
                    prototype,
                    new[] { secondCar, secondReservation, rightLane }),
                "A separated second lane did not start immediately.");
            IList activeClaims = (IList)activeClaimsField.GetValue(prototype);
            Require(
                activeClaims.Count == 2,
                "Two independent rapid taps were not allowed to move concurrently.");
            Require(
                !(bool)acquireClaim.Invoke(
                    prototype,
                    new[] { crossingCar, crossingReservation, crossingLane }),
                "A genuinely crossing route did not wait for collision clearance.");
            Require(
                activeClaims.Count == 2,
                "The waiting crossing route was registered before it was safe.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CarPuzzlePiece CreatePiece(Transform parent, string name)
    {
        GameObject pieceObject = new GameObject(name);
        pieceObject.transform.SetParent(parent, false);
        return pieceObject.AddComponent<CarPuzzlePiece>();
    }

    private static bool Overlaps(
        MethodInfo method,
        Vector3[] first,
        float firstHalfWidth,
        Vector3[] second,
        float secondHalfWidth)
    {
        return (bool)method.Invoke(
            null,
            new object[] { first, firstHalfWidth, second, secondHalfWidth });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
