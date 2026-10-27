using UnityEngine;
using UnityEngine.Audio;

// All current prototype audio is SFX. Without a mixer, apply master*SFX globally;
// music is explicitly a placeholder. A future mixer can route the three buses.
public static class StageboundAudioSettings
{
    const string Prefix = "Stagebound.Audio.";
    public static float Master { get; private set; } = 1f;
    public static float Music { get; private set; } = .7f;
    public static float Sfx { get; private set; } = 1f;
    static AudioMixer mixer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        mixer = null;
        Master = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Master.v1", 1f));
        Music = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Music.v1", .7f));
        Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "SFX.v1", 1f));
        Apply();
    }
    public static void UseMixer(AudioMixer value) { mixer = value; Apply(); }
    public static void SetMaster(float value) { Master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "Master.v1", Master); Apply(); }
    public static void SetMusic(float value) { Music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "Music.v1", Music); Apply(); }
    public static void SetSfx(float value) { Sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "SFX.v1", Sfx); Apply(); }
    public static void Save() => PlayerPrefs.Save();
    static void Apply()
    {
        if (mixer == null) { AudioListener.volume = Master * Sfx; return; }
        AudioListener.volume = 1f;
        mixer.SetFloat("MasterVolume", Decibels(Master));
        mixer.SetFloat("MusicVolume", Decibels(Music));
        mixer.SetFloat("SFXVolume", Decibels(Sfx));
    }
    static float Decibels(float value) => value <= .0001f ? -80f : 20f * Mathf.Log10(value);
}
