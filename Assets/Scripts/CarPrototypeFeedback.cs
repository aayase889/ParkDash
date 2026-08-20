using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Small, self-contained feedback system for the 3D car prototype. Sound
/// effects are synthesized once at runtime, so the prototype does not need a
/// collection of external audio files. Settings persist between sessions.
/// </summary>
public sealed class CarPrototypeFeedback : MonoBehaviour
{
    private const string SoundPreferenceKey = "CarPrototype.SoundEnabled";
    private const string HapticsPreferenceKey = "CarPrototype.HapticsEnabled";
    private const string MusicPreferenceKey = "CarPrototype.MusicEnabled";
    private const int SampleRate = 22050;

    private static CarPrototypeFeedback instance;

    private AudioSource audioSource;
    private AudioSource musicSource;
    private AudioClip buttonClip;
    private AudioClip parkedClip;
    private AudioClip matchClip;
    private AudioClip errorClip;
    private AudioClip victoryClip;
    private AudioClip defeatClip;
    private AudioClip musicClip;
    private float lastHapticTime = -10f;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CarPrototype_PlayLightHaptic(float intensity);
#endif

    public static bool SoundEnabled
    {
        get => PlayerPrefs.GetInt(SoundPreferenceKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(SoundPreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (instance == null) return;
            if (!value)
            {
                if (instance.audioSource != null) instance.audioSource.Stop();
            }
        }
    }

    public static bool HapticsEnabled
    {
        get => PlayerPrefs.GetInt(HapticsPreferenceKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(HapticsPreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(MusicPreferenceKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(MusicPreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (instance == null) return;
            if (value) instance.StartMusic();
            else if (instance.musicSource != null) instance.musicSource.Stop();
        }
    }

    public static CarPrototypeFeedback EnsureExists()
    {
        if (instance != null) return instance;

        instance = FindFirstObjectByType<CarPrototypeFeedback>();
        if (instance != null) return instance;

        GameObject feedbackObject = new GameObject("Car Prototype Feedback");
        DontDestroyOnLoad(feedbackObject);
        return feedbackObject.AddComponent<CarPrototypeFeedback>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        musicSource.volume = 0.22f;

        buttonClip = CreateTone("UI Tap", 0.055f, time => 760f + time * 1400f, time => SineEnvelope(time, 0.055f) * 0.16f);
        parkedClip = CreateSequence("Car Parked", new[] { 330f, 523.25f }, 0.075f, 0.22f);
        matchClip = CreateSequence("Color Match", new[] { 523.25f, 659.25f, 783.99f }, 0.11f, 0.24f);
        errorClip = CreateTone("Wrong Move", 0.18f, time => 145f - time * 180f, time =>
        {
            float wave = Mathf.Sin(time * Mathf.PI * 2f * 18f) >= 0f ? 1f : -1f;
            return wave * SineEnvelope(time, 0.18f) * 0.16f;
        });
        victoryClip = CreateSequence("Level Complete", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.14f, 0.25f);
        defeatClip = CreateSequence("Level Lost", new[] { 392f, 311.13f, 233.08f }, 0.17f, 0.20f);
        musicClip = CreateMusicLoop();
        musicSource.clip = musicClip;
        if (MusicEnabled) StartMusic();
    }

    private void StartMusic()
    {
        if (musicSource == null || musicClip == null || musicSource.isPlaying) return;
        musicSource.clip = musicClip;
        musicSource.Play();
    }

    public static void ButtonTap()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.Play(feedback.buttonClip, 0.75f);
        feedback.PlaySoftHaptic(0.05f, 0.12f);
    }

    public static void CarMove()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.PlaySoftHaptic(0.09f, 0.10f);
    }

    public static void CarParked()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.Play(feedback.parkedClip, 0.95f);
        feedback.PlaySoftHaptic(0.07f, 0.16f);
    }

    public static void Match()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.Play(feedback.matchClip, 1f);
        feedback.PlaySoftHaptic(0.16f, 0.26f);
    }

    public static void Error()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.Play(feedback.errorClip, 0.9f);
        feedback.PlaySoftHaptic(0.14f, 0.22f);
    }

    public static void Victory()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.Play(feedback.victoryClip, 1f);
        feedback.PlaySoftHaptic(0.22f, 0.30f);
    }

    public static void Defeat()
    {
        CarPrototypeFeedback feedback = EnsureExists();
        feedback.Play(feedback.defeatClip, 1f);
        feedback.PlaySoftHaptic(0.22f, 0.26f);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (!SoundEnabled || clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    private void PlaySoftHaptic(float cooldown, float intensity)
    {
        if (!HapticsEnabled || Time.unscaledTime - lastHapticTime < cooldown) return;
        lastHapticTime = Time.unscaledTime;
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            CarPrototype_PlayLightHaptic(Mathf.Clamp01(intensity));
        }
        catch (EntryPointNotFoundException)
        {
            // Keep gameplay responsive if an incremental Xcode build still
            // contains an older target that predates the native haptic plugin.
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    private static AudioClip CreateTone(
        string name,
        float duration,
        Func<float, float> frequency,
        Func<float, float> amplitude)
    {
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        float phase = 0f;
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            phase += Mathf.Max(20f, frequency(time)) * Mathf.PI * 2f / SampleRate;
            samples[index] = Mathf.Sin(phase) * amplitude(time);
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateSequence(string name, float[] notes, float noteDuration, float volume)
    {
        float duration = notes.Length * noteDuration;
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            int noteIndex = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(time / noteDuration));
            float noteTime = time - noteIndex * noteDuration;
            float envelope = SineEnvelope(noteTime, noteDuration);
            float fundamental = Mathf.Sin(noteTime * Mathf.PI * 2f * notes[noteIndex]);
            float harmonic = Mathf.Sin(noteTime * Mathf.PI * 4f * notes[noteIndex]) * 0.25f;
            samples[index] = (fundamental + harmonic) * envelope * volume;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateMusicLoop()
    {
        const float beatDuration = 0.50f;
        const int beatsPerChord = 4;
        float[][] chords =
        {
            new[] { 261.63f, 329.63f, 392.00f },
            new[] { 220.00f, 261.63f, 329.63f },
            new[] { 174.61f, 220.00f, 261.63f },
            new[] { 196.00f, 246.94f, 293.66f }
        };
        float[] melody =
        {
            523.25f, 659.25f, 783.99f, 659.25f,
            440.00f, 523.25f, 659.25f, 523.25f,
            349.23f, 440.00f, 523.25f, 440.00f,
            392.00f, 493.88f, 587.33f, 493.88f
        };

        float duration = chords.Length * beatsPerChord * beatDuration;
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            int beat = Mathf.Min(melody.Length - 1, Mathf.FloorToInt(time / beatDuration));
            int chordIndex = Mathf.Min(chords.Length - 1, beat / beatsPerChord);
            float beatTime = time - beat * beatDuration;
            float chordTime = time - chordIndex * beatsPerChord * beatDuration;
            float beatEnvelope = SoftMusicEnvelope(beatTime, beatDuration);
            float chordEnvelope = SoftMusicEnvelope(
                chordTime,
                beatsPerChord * beatDuration);

            float pad = 0f;
            for (int note = 0; note < chords[chordIndex].Length; note++)
            {
                float frequency = chords[chordIndex][note];
                pad += Mathf.Sin(time * Mathf.PI * 2f * frequency) * 0.026f;
                pad += Mathf.Sin(time * Mathf.PI * 2f * frequency * 2f) * 0.004f;
            }

            float bassFrequency = chords[chordIndex][0] * 0.5f;
            float bass = Mathf.Sin(time * Mathf.PI * 2f * bassFrequency) * 0.045f;
            float lead = Mathf.Sin(time * Mathf.PI * 2f * melody[beat]) * beatEnvelope * 0.040f;
            float leadBell = Mathf.Sin(time * Mathf.PI * 4f * melody[beat]) * beatEnvelope * 0.008f;
            samples[index] = (pad + bass) * chordEnvelope + lead + leadBell;
        }

        AudioClip clip = AudioClip.Create(
            "Color Arrows Background Music",
            sampleCount,
            1,
            SampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float SoftMusicEnvelope(float time, float duration)
    {
        float attack = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(time / Mathf.Min(0.08f, duration * 0.15f)));
        float releaseStart = Mathf.Max(0f, duration - Mathf.Min(0.12f, duration * 0.20f));
        float release = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01((time - releaseStart) / Mathf.Max(0.001f, duration - releaseStart)));
        return attack * release;
    }

    private static float SineEnvelope(float time, float duration)
    {
        float normalized = Mathf.Clamp01(time / Mathf.Max(0.001f, duration));
        float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized / 0.12f));
        float release = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((normalized - 0.55f) / 0.45f));
        return attack * release;
    }
}
