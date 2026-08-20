using System;
using UnityEngine;

/// <summary>
/// Persistent retry-heart bank for the 3D car game. Missing hearts recover one
/// at a time every ten real-world minutes, including while the app is closed.
/// </summary>
public static class CarPrototypeHeartBank
{
    public const int MaximumHearts = 5;
    public const int RecoveryMinutes = 10;

    private const string HeartCountKey = "CarPrototype3D.RetryHeartCount";
    private const string NextHeartTicksKey = "CarPrototype3D.NextRetryHeartUtcTicks";
    private static readonly long RecoveryTicks = TimeSpan.FromMinutes(RecoveryMinutes).Ticks;

    public static int AvailableHearts
    {
        get
        {
            Reconcile(out int hearts, out _);
            return hearts;
        }
    }

    public static double SecondsUntilNextHeart
    {
        get
        {
            Reconcile(out int hearts, out long nextHeartTicks);
            if (hearts >= MaximumHearts || nextHeartTicks <= 0) return 0d;

            long remainingTicks = Math.Max(0L, nextHeartTicks - DateTime.UtcNow.Ticks);
            return TimeSpan.FromTicks(remainingTicks).TotalSeconds;
        }
    }

    public static bool TryConsumeHeart()
    {
        Reconcile(out int hearts, out long nextHeartTicks);
        if (hearts <= 0) return false;

        long nowTicks = DateTime.UtcNow.Ticks;
        hearts--;
        if (hearts < MaximumHearts && nextHeartTicks <= 0)
            nextHeartTicks = nowTicks + RecoveryTicks;

        Save(hearts, nextHeartTicks);
        return true;
    }

    private static void Reconcile(out int hearts, out long nextHeartTicks)
    {
        hearts = Mathf.Clamp(PlayerPrefs.GetInt(HeartCountKey, MaximumHearts), 0, MaximumHearts);
        nextHeartTicks = ReadNextHeartTicks();
        long nowTicks = DateTime.UtcNow.Ticks;
        bool changed = false;

        if (hearts >= MaximumHearts)
        {
            if (nextHeartTicks != 0)
            {
                nextHeartTicks = 0;
                changed = true;
            }
        }
        else
        {
            if (nextHeartTicks <= 0 || nextHeartTicks > nowTicks + RecoveryTicks)
            {
                nextHeartTicks = nowTicks + RecoveryTicks;
                changed = true;
            }

            if (nowTicks >= nextHeartTicks)
            {
                long elapsedIntervals = 1L + (nowTicks - nextHeartTicks) / RecoveryTicks;
                int recoveredHearts = Mathf.Min(
                    MaximumHearts - hearts,
                    elapsedIntervals > int.MaxValue ? int.MaxValue : (int)elapsedIntervals);
                hearts += recoveredHearts;
                nextHeartTicks = hearts >= MaximumHearts
                    ? 0L
                    : nextHeartTicks + recoveredHearts * RecoveryTicks;
                changed = true;
            }
        }

        if (changed)
            Save(hearts, nextHeartTicks);
    }

    private static long ReadNextHeartTicks()
    {
        string value = PlayerPrefs.GetString(NextHeartTicksKey, string.Empty);
        return long.TryParse(value, out long ticks) ? ticks : 0L;
    }

    private static void Save(int hearts, long nextHeartTicks)
    {
        PlayerPrefs.SetInt(HeartCountKey, Mathf.Clamp(hearts, 0, MaximumHearts));
        PlayerPrefs.SetString(NextHeartTicksKey, nextHeartTicks.ToString());
        PlayerPrefs.Save();
    }
}
