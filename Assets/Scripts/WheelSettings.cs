using System;
using UnityEngine;

// One persisted profile, independent of scenes and transient DirectInput device slots.
public static class WheelSettings
{
    public const string Profile = "LogitechG29";
    private const string Prefix = "Stagebound.Controls." + Profile + ".v1.";
    public enum Binding { Gear1, Gear2, Gear3, Gear4, Gear5, Gear6, Reverse, ShiftUp, ShiftDown, Handbrake, Respawn, Pause }
    private static readonly int[] Defaults = { -1, -1, -1, -1, -1, -1, -1, 4, 5, 0, 3, 9 };
    [Serializable]
    private class LegacyButtons
    {
        public int shiftUpButton = 4, shiftDownButton = 5, handbrakeButton = 0, respawnButton = 3, pauseButton = 9;
    }
    private static int[] bindings;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { bindings = null; Changed = null; }

    private static void Load()
    {
        if (bindings != null) return;
        bindings = (int[])Defaults.Clone();
        // Preserve a previously measured complete shifter profile, never guess gears.
        string[] legacy = PlayerPrefs.GetString("Stagebound.G29.ShifterButtons.v1", "").Split(',');
        var legacyButtons = new LegacyButtons();
        string calibration = PlayerPrefs.GetString("Stagebound.G29.MeasuredCalibration.v1", "");
        if (!string.IsNullOrEmpty(calibration))
        {
            try { JsonUtility.FromJsonOverwrite(calibration, legacyButtons); }
            catch (ArgumentException) { /* Leave the known built-in button defaults. */ }
        }
        int[] measured = { legacyButtons.shiftUpButton, legacyButtons.shiftDownButton, legacyButtons.handbrakeButton,
            legacyButtons.respawnButton, legacyButtons.pauseButton };
        uint used = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            int fallback = i >= 7 ? measured[i - 7] : Defaults[i];
            if (i < 7 && legacy.Length == 7 && int.TryParse(legacy[i], out int old)) fallback = old;
            int value = PlayerPrefs.GetInt(Prefix + (Binding)i, fallback);
            if (value < -1 || value >= 32 || (value >= 0 && (used & (1u << value)) != 0)) value = -1;
            bindings[i] = value;
            if (value >= 0) used |= 1u << value;
        }
    }

    public static int Get(Binding binding) { Load(); return bindings[(int)binding]; }
    public static int Conflict(Binding binding, int button)
    {
        Load();
        for (int i = 0; i < bindings.Length; i++)
            if (i != (int)binding && bindings[i] == button && button >= 0) return i;
        return -1;
    }

    public static void Set(Binding binding, int button, bool replace = false)
    {
        if (button < -1 || button >= 32) throw new ArgumentOutOfRangeException(nameof(button));
        int conflict = Conflict(binding, button);
        if (conflict >= 0 && !replace) throw new InvalidOperationException("Button is already assigned.");
        if (conflict >= 0) bindings[conflict] = -1;
        bindings[(int)binding] = button;
        SaveBindings();
    }

    private static void SaveBindings()
    {
        for (int i = 0; i < bindings.Length; i++) PlayerPrefs.SetInt(Prefix + (Binding)i, bindings[i]);
        Commit();
    }

    public static void ResetBindings() { bindings = (int[])Defaults.Clone(); SaveBindings(); }
    public static string Label(Binding b) => (int)b < 6 ? "Gear " + ((int)b + 1) :
        b == Binding.ShiftUp ? "Shift Up" : b == Binding.ShiftDown ? "Shift Down" : b.ToString();
    public static string Display(Binding b) => Get(b) < 0 ? "UNBOUND" : "Button " + Get(b);
    public static bool Enabled { get => PlayerPrefs.GetInt(Prefix + "FFBEnabled", 0) != 0; set => SetBool("FFBEnabled", value); }
    public static bool Invert { get => PlayerPrefs.GetInt(Prefix + "FFBInvert", 0) != 0; set => SetBool("FFBInvert", value); }
    public static bool UseShifter
    {
        get => PlayerPrefs.GetInt(Prefix + "UseHShifter", PlayerPrefs.GetInt("Stagebound.G29.UseHShifter", 0)) != 0;
        set => SetBool("UseHShifter", value);
    }
    public static float Strength
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "FFBStrength", PlayerPrefs.GetFloat("Stagebound.G29.FFBStrength", .25f)));
        set { PlayerPrefs.SetFloat(Prefix + "FFBStrength", float.IsFinite(value) ? Mathf.Clamp01(value) : 0f); Commit(); }
    }
    private static void SetBool(string key, bool value) { if (PlayerPrefs.HasKey(Prefix + key) && PlayerPrefs.GetInt(Prefix + key) == (value ? 1 : 0)) return; PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0); Commit(); }
    private static void Commit() { PlayerPrefs.Save(); Changed?.Invoke(); }
}
