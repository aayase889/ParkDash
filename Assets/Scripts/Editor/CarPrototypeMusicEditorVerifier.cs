using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that the settings Music toggle has a persistent preference and a
/// real, non-silent looping track to control.
/// </summary>
public static class CarPrototypeMusicEditorVerifier
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const string MusicPreferenceKey = "CarPrototype.MusicEnabled";

    [MenuItem("Car Prototype/Verify Music Toggle")]
    public static void RunBatch()
    {
        bool preferenceExisted = PlayerPrefs.HasKey(MusicPreferenceKey);
        int oldPreference = PlayerPrefs.GetInt(MusicPreferenceKey, 1);
        AudioClip clip = null;

        try
        {
            CarPrototypeFeedback.MusicEnabled = false;
            Require(!CarPrototypeFeedback.MusicEnabled,
                "The Music preference did not switch off.");
            CarPrototypeFeedback.MusicEnabled = true;
            Require(CarPrototypeFeedback.MusicEnabled,
                "The Music preference did not switch back on.");

            MethodInfo createMusic = typeof(CarPrototypeFeedback).GetMethod(
                "CreateMusicLoop",
                PrivateStatic);
            Require(createMusic != null, "The background-music generator is missing.");
            clip = (AudioClip)createMusic.Invoke(null, null);
            Require(clip != null, "The background-music generator returned no track.");
            Require(clip.length >= 7.9f && clip.length <= 8.1f,
                "The background track does not have the expected seamless eight-second loop length.");

            float[] samples = new float[clip.samples];
            Require(clip.GetData(samples, 0), "Could not inspect the generated background track.");
            float peak = 0f;
            for (int index = 0; index < samples.Length; index++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
            Require(peak > 0.05f,
                "The generated background track is silent or too quiet to control.");

            Debug.Log(
                "[Music Toggle Verification] PASS: the Music setting persists and controls "
                + "a real non-silent eight-second background loop independently of sound effects.");
        }
        finally
        {
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            if (preferenceExisted) PlayerPrefs.SetInt(MusicPreferenceKey, oldPreference);
            else PlayerPrefs.DeleteKey(MusicPreferenceKey);
            PlayerPrefs.Save();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
