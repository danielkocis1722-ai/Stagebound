using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Run in a separate Unity batch process with -executeMethod ControlsSettingsValidation.Run.
// Synthetic logic checks are deliberately not described as physical G29 verification.
[InitializeOnLoad]
public static class ControlsSettingsValidation
{
    private const string Pending = "Stagebound.ControlsValidation.Pending";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static ControlsSettingsValidation() => EditorApplication.update += Tick;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Pending, false);
        int code = 0;
        try { Validate(); Debug.Log("CONTROLS VALIDATION PASSED (synthetic, no hardware test)"); }
        catch (Exception error) { Debug.LogException(error); code = 1; }
        EditorApplication.Exit(code);
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception("Controls validation failed: " + description);
        Debug.Log("PASS: " + description);
    }

    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static object Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    private static void Raw(LogitechWheelInput wheel, uint buttons)
    {
        var field = typeof(LogitechWheelInput).GetField("lastSample", Private);
        var sample = Activator.CreateInstance(field.FieldType);
        field.FieldType.GetField("Buttons").SetValue(sample, buttons);
        field.SetValue(wheel, sample);
    }

    private static int Gear(LogitechWheelInput wheel, uint buttons)
    {
        object[] args = { buttons, 0 };
        Check((bool)Invoke(wheel, "DecodeShifterGear", args), "shifter decode accepted");
        return (int)args[1];
    }

    private static void Validate()
    {
        const string prefix = "Stagebound.Controls.LogitechG29.v1.";
        var integerBackup = new Dictionary<string, int?>();
        foreach (WheelSettings.Binding binding in Enum.GetValues(typeof(WheelSettings.Binding)))
        {
            string key = prefix + binding;
            integerBackup[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        }
        foreach (string suffix in new[] { "FFBEnabled", "FFBInvert", "UseHShifter" })
        {
            string key = prefix + suffix;
            integerBackup[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        }
        string strengthKey = prefix + "FFBStrength";
        float? strengthBackup = PlayerPrefs.HasKey(strengthKey) ? PlayerPrefs.GetFloat(strengthKey) : (float?)null;
        const string calibrationKey = "Stagebound.G29.MeasuredCalibration.v1";
        string calibrationBackup = PlayerPrefs.HasKey(calibrationKey) ? PlayerPrefs.GetString(calibrationKey) : null;
        var root = new GameObject("Controls validation");
        try
        {
            WheelSettings.ResetBindings();
            PlayerPrefs.DeleteKey(prefix + "ShiftUp");
            PlayerPrefs.DeleteKey(prefix + "ShiftDown");
            PlayerPrefs.SetString(calibrationKey, "{\"shiftUpButton\":6,\"shiftDownButton\":7}");
            typeof(WheelSettings).GetField("bindings", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            Check(WheelSettings.Get(WheelSettings.Binding.ShiftUp) == 6 && WheelSettings.Get(WheelSettings.Binding.ShiftDown) == 7, "legacy measured paddle assignments preserved");
            if (calibrationBackup != null) PlayerPrefs.SetString(calibrationKey, calibrationBackup);
            else PlayerPrefs.DeleteKey(calibrationKey);
            WheelSettings.ResetBindings();
            Check(WheelSettings.Get(WheelSettings.Binding.ShiftUp) == 4 && WheelSettings.Get(WheelSettings.Binding.ShiftDown) == 5, "existing paddle defaults");
            Check(WheelSettings.Get(WheelSettings.Binding.Reverse) == -1, "no guessed reverse binding");
            WheelSettings.Set(WheelSettings.Binding.Gear1, 12);
            Check(WheelSettings.Conflict(WheelSettings.Binding.Gear2, 12) == 0, "duplicate detected");
            bool rejected = false;
            try { WheelSettings.Set(WheelSettings.Binding.Gear2, 12); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && WheelSettings.Get(WheelSettings.Binding.Gear1) == 12, "duplicate cannot silently overwrite");
            WheelSettings.Set(WheelSettings.Binding.Gear2, 12, true);
            Check(WheelSettings.Get(WheelSettings.Binding.Gear1) == -1 && WheelSettings.Get(WheelSettings.Binding.Gear2) == 12, "replacement clears old assignment");
            WheelSettings.UseShifter = true;
            WheelSettings.Strength = .5f;
            WheelSettings.Invert = true;
            WheelSettings.Enabled = false;
            typeof(WheelSettings).GetField("bindings", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            Check(WheelSettings.Get(WheelSettings.Binding.Gear2) == 12 && WheelSettings.UseShifter && WheelSettings.Invert && Mathf.Approximately(WheelSettings.Strength, .5f), "saved settings reload from PlayerPrefs");
            WheelSettings.ResetBindings();
            Check(WheelSettings.UseShifter && WheelSettings.Invert && Mathf.Approximately(WheelSettings.Strength, .5f), "reset bindings preserves other settings");

            var wheel = root.AddComponent<LogitechWheelInput>();
            wheel.enabled = false; // Never poll the SDK or send a motor command in this test.
            Set(wheel, "<WheelConnected>k__BackingField", true);
            Set(wheel, "<HasLiveSample>k__BackingField", true);
            Set(wheel, "<WheelName>k__BackingField", "Logitech G29 Driving Force Racing Wheel");
            Set(wheel, "<Ready>k__BackingField", true);
            Raw(wheel, 1u << 4);
            Check(wheel.BeginBindingCapture(), "capture begins with a valid baseline");
            Check(!wheel.TryCaptureButton(out _), "already held button is not captured");
            Raw(wheel, 0);
            Check(!wheel.TryCaptureButton(out _), "release is not an activation");
            Raw(wheel, (1u << 12) | (1u << 13));
            Check(!wheel.TryCaptureButton(out _), "simultaneous button noise is rejected");
            Raw(wheel, 0); wheel.TryCaptureButton(out _);
            Raw(wheel, 1u << 12);
            Check(wheel.TryCaptureButton(out int captured) && captured == 12, "new button edge is captured");
            Check(wheel.InputSuppressed && !wheel.PausePressed && !wheel.ShiftUpPressed, "wheel actions suppressed during capture");
            wheel.EndBindingCapture();
            Check(!wheel.BindingCaptureActive, "cancel/completion leaves listening mode");
            wheel.shifterButtons = new[] { 12, 13, 14, 15, 16, 17, 18 };
            Check(Gear(wheel, 0) == 0 && Gear(wheel, 1u << 18) == -1, "neutral and reverse decoding");
            for (int i = 0; i < 6; i++) Check(Gear(wheel, 1u << (12 + i)) == i + 1, "gear position " + (i + 1));
            Check(Gear(wheel, (1u << 12) | (1u << 13)) == 0, "ambiguous positions select neutral");

            // Validate vehicle gear availability without running vehicle physics.
            var car = root.AddComponent<CarController>();
            car.enabled = false;
            car.gearRatios = new float[5];
            Set(wheel, "car", car);
            Check(Gear(wheel, 1u << 17) == 0, "sixth position is neutral in five-speed vehicle");
            car.gearRatios = new float[6];
            Check(Gear(wheel, 1u << 17) == 6, "sixth position works with six-speed config");
            Set(wheel, "car", null);

            var feedback = root.AddComponent<SteeringForceFeedback>();
            feedback.enabled = false;
            WheelSettings.Enabled = true;
            Set(feedback, "<MotorPercent>k__BackingField", 20);
            Set(feedback, "<FilteredOutput>k__BackingField", .5f);
            WheelSettings.Enabled = false;
            Check(feedback.MotorPercent == 0 && feedback.FilteredOutput == 0, "FFB OFF clears output immediately");
            var scale = typeof(SteeringForceFeedback).GetMethod("ScaleMotorOutput", BindingFlags.Static | BindingFlags.NonPublic);
            Check((int)scale.Invoke(null, new object[] { .4f, .6f, .25f, false }) == 10 &&
                (int)scale.Invoke(null, new object[] { .4f, .6f, .5f, false }) == 20 &&
                (int)scale.Invoke(null, new object[] { .4f, .6f, 1f, false }) == 40, "25/50/100 percent scales unsaturated output");
            Check((int)scale.Invoke(null, new object[] { .4f, .6f, .5f, true }) == -20, "invert reverses only motor output");

            var canvas = StageboundMenuUI.Canvas(root.transform, "Settings validation canvas");
            var uiActions = InputSystem.actions.FindActionMap("UI");
            foreach (var binding in uiActions.bindings)
                Check(!binding.effectivePath.Contains("<Joystick>"), "UI excludes generic joystick: " + binding.effectivePath);
            var panel = AudioSettingsPanel.Create(canvas.transform, () => { });
            Set(panel, "wheel", wheel);
            panel.gameObject.SetActive(true);
            Directory.CreateDirectory("Logs/ControlsSettings");
            for (int page = 0; page < 3; page++)
            {
                Invoke(panel, "BuildPage", page);
                Canvas.ForceUpdateCanvases();
                Check(panel.GetComponentsInChildren<Button>().Length >= 4, "page " + page + " navigation");
                Capture(canvas, "Logs/ControlsSettings/page-" + page + ".png");
            }
            Invoke(panel, "BuildPage", 1);
            Set(wheel, "<WheelConnected>k__BackingField", false);
            Invoke(panel, "Refresh");
            Check(panel.GetComponentsInChildren<Slider>()[0].interactable == false, "disconnected wheel settings disabled");
            Capture(canvas, "Logs/ControlsSettings/disconnected.png");
            Set(wheel, "<WheelConnected>k__BackingField", true);
            Set(wheel, "<HasLiveSample>k__BackingField", true);
            Invoke(panel, "Refresh");
            Raw(wheel, 0);
            Invoke(panel, "BeginCapture", WheelSettings.Binding.Gear1, panel.GetComponentsInChildren<Button>()[0]);
            Capture(canvas, "Logs/ControlsSettings/rebind.png");
            Invoke(panel, "CancelCapture", false);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var entry in integerBackup)
                if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
            if (strengthBackup.HasValue) PlayerPrefs.SetFloat(strengthKey, strengthBackup.Value);
            else PlayerPrefs.DeleteKey(strengthKey);
            if (calibrationBackup != null) PlayerPrefs.SetString(calibrationKey, calibrationBackup);
            else PlayerPrefs.DeleteKey(calibrationKey);
            PlayerPrefs.Save();
        }
    }

    private static void Capture(Canvas canvas, string path)
    {
        var cameraObject = new GameObject("Validation camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(5, 10, 17, 255);
        var target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }
}
