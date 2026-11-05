using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class LogitechWheelInput : MonoBehaviour
{
    public enum Axis { X, Y, Z, Rx, Ry, Rz, Slider0, Slider1 }

    [Serializable]
    public class Pedal
    {
        public Axis axis;
        public int released = 32767;
        public int pressed = -32768;
        [Range(0f, .2f)] public float deadzone = .025f;

        public Pedal(Axis axis) => this.axis = axis;

        public float Normalize(int raw)
        {
            if (released == pressed)
                return 0f;
            float value = Mathf.InverseLerp(released, pressed, raw);
            return Mathf.InverseLerp(deadzone, 1f - deadzone, value);
        }
    }

    [Header("DirectInput calibration")]
    public Axis steeringAxis = Axis.X;
    public int steeringLeft = -32768;
    public int steeringCentre;
    public int steeringRight = 32767;
    public Pedal throttle = new Pedal(Axis.Y);
    public Pedal brake = new Pedal(Axis.Rz);
    public Pedal clutch = new Pedal(Axis.Slider0);
    [Tooltip("Confirm this only after matching the vehicle's range in G HUB when the SDK cannot read the range.")]
    public bool confirmManualRange;
    public static bool ConfirmConfiguredRange { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRangeConfirmation() => ConfirmConfiguredRange = false;

    [Header("Saved wheel bindings (edit in Settings > Wheel)")]
    [HideInInspector] public int shiftUpButton = 4;
    [HideInInspector] public int shiftDownButton = 5;
    [HideInInspector] public int handbrakeButton = 0;
    [HideInInspector] public int respawnButton = 3;
    [HideInInspector] public int pauseButton = 9;
    [HideInInspector] public bool useHShifter;
    [HideInInspector]
    public int[] shifterButtons = Array.Empty<int>();

    public bool WheelConnected { get; private set; }
    public bool Ready { get; private set; }
    public bool HasLiveSample { get; private set; }
    public const double MaxCalibrationSampleAgeMs = 750;
    public double CalibrationSampleAgeMs => hasCalibrationSample
        ? (Time.realtimeSinceStartupAsDouble - calibrationSampleTime) * 1000
        : double.PositiveInfinity;
    public bool CalibrationInProgress { get; private set; }
    public bool CanStartCalibration => Application.isPlaying && isActiveAndEnabled &&
        WheelConnected && !CalibrationInProgress;
    public bool HasMeasuredCalibration { get; private set; }
    public bool HasActivity { get; private set; }
    public string WheelName { get; private set; } = "";
    public string Status { get; private set; }
    public bool RangeVerified => backend.Range > 0;
    public bool CanDrive => WheelConnected && !CalibrationInProgress && !InputSuppressed && Ready;
    public int ReportedRange => backend.Range;
    public int RawSteering { get; private set; }
    public int RawThrottle { get; private set; }
    public int RawBrake { get; private set; }
    public int RawClutch { get; private set; }
    public string RawAxes => FormatRawAxes(lastSample);
    public string CalibrationRawAxes => hasCalibrationSample ? FormatRawAxes(calibrationSample) : "No sample";
    public uint RawButtons => lastSample.Buttons;
    public uint RawPov => lastSample.Pov;
    public int ShifterMode => backend.ShifterMode;
    public uint? LearnedNeutralButtons { get; private set; }
    public bool MotorSupported => backend.ForceAvailable;
    public bool HasShifterMapping => IsValidShifterMapping(shifterButtons);

    private static bool IsValidShifterMapping(int[] mapping)
    {
        if (mapping == null || (mapping.Length != 6 && mapping.Length != 7))
            return false;

        uint used = 0;
        foreach (int button in mapping)
        {
            if (button < 0 || button >= 32 || (used & (1u << button)) != 0)
                return false;
            used |= 1u << button;
        }
        return true;
    }
    public uint Buttons { get; private set; }
    public float SteeringInput { get; private set; }
    public float ThrottleInput { get; private set; }
    public float BrakeInput { get; private set; }
    public float ClutchInput { get; private set; }
    public float SteeringDegrees { get; private set; }
    public float RoadWheelTarget { get; private set; }
    public bool Handbrake => Ready && !InputSuppressed && HasButton(Buttons, handbrakeButton);
    public bool ShiftUpPressed => Ready && !InputSuppressed && HasButton(pressedButtons, shiftUpButton);
    public bool ShiftDownPressed => Ready && !InputSuppressed && HasButton(pressedButtons, shiftDownButton);
    public bool RespawnPressed => Ready && !InputSuppressed && HasButton(pressedButtons, respawnButton);
    public bool PausePressed => Ready && !InputSuppressed && HasButton(pressedButtons, pauseButton);
    public VehicleConfig.WheelSteering Settings { get; private set; }

    private readonly LogitechWheelBackend backend = new LogitechWheelBackend();
    private const string CentreKey = "Stagebound.G29.SteeringCentre";
    private const string CalibrationKey = "Stagebound.G29.MeasuredCalibration.v1";
    private CarController car;
    private uint pressedButtons;
    private Vector4 activityAnchor;
    private LogitechWheelBackend.Sample lastSample;
    private LogitechWheelBackend.Sample calibrationSample;
    private bool hasCalibrationSample;
    private double calibrationSampleTime;

#if UNITY_EDITOR
    public string CalibrationPrompt { get; set; }
    private GUIStyle calibrationPromptStyle;

    private void OnGUI()
    {
        if (!CalibrationInProgress || string.IsNullOrEmpty(CalibrationPrompt))
            return;
        calibrationPromptStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleCenter };
        float width = Mathf.Min(640f, Screen.width);
        GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height - 90f, width, 78f),
            CalibrationPrompt, calibrationPromptStyle);
    }
#endif

    private void Awake()
    {
        car = GetComponent<CarController>();
        var rig = GetComponent<VehicleRigLoader>();
        Settings = rig != null ? rig.CurrentDefinition?.config?.wheelSteering : null;
        Settings ??= new VehicleConfig.WheelSteering();
        Settings.feedback ??= new SteeringForceFeedback.Tuning();
        steeringCentre = PlayerPrefs.GetInt(CentreKey, steeringCentre);
        string profile = PlayerPrefs.GetString(CalibrationKey, "");
        if (!string.IsNullOrEmpty(profile))
        {
            try
            {
                JsonUtility.FromJsonOverwrite(profile, this);
                HasMeasuredCalibration = true;
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("G29 calibration could not be read. Run calibration again.", this);
            }
            confirmManualRange = false;
        }
        ApplyBindings();
        WheelSettings.Changed += ApplyBindings;
        if (car != null && !TryGetComponent<SteeringForceFeedback>(out _))
            gameObject.AddComponent<SteeringForceFeedback>();
    }

    private void ApplyBindings()
    {
        shiftUpButton = WheelSettings.Get(WheelSettings.Binding.ShiftUp);
        shiftDownButton = WheelSettings.Get(WheelSettings.Binding.ShiftDown);
        handbrakeButton = WheelSettings.Get(WheelSettings.Binding.Handbrake);
        respawnButton = WheelSettings.Get(WheelSettings.Binding.Respawn);
        pauseButton = WheelSettings.Get(WheelSettings.Binding.Pause);
        useHShifter = WheelSettings.UseShifter;
        shifterButtons = new int[7];
        for (int i = 0; i < 7; i++) shifterButtons[i] = WheelSettings.Get((WheelSettings.Binding)i);
    }

    public bool BindingCaptureActive { get; private set; }
    private uint capturePrevious, releaseGuard;
    private int captureEndFrame = -1;
    public bool InputSuppressed => BindingCaptureActive || Time.frameCount <= captureEndFrame || releaseGuard != 0;

    public bool BeginBindingCapture()
    {
        if (!WheelConnected || !HasLiveSample || CalibrationInProgress) return false;
        capturePrevious = RawButtons;
        BindingCaptureActive = true;
        StopForce();
        return true;
    }

    public bool TryCaptureButton(out int button)
    {
        button = -1;
        if (!BindingCaptureActive || !HasLiveSample) return false;
        uint rising = RawButtons & ~capturePrevious;
        capturePrevious = RawButtons;
        // Ignore ambiguous simultaneous activations; release and try again.
        if (rising == 0 || (rising & (rising - 1)) != 0) return false;
        for (int i = 0; i < 32; i++)
            if ((rising & (1u << i)) != 0) { button = i; return true; }
        return false;
    }

    public void EndBindingCapture()
    {
        if (!BindingCaptureActive) return;
        BindingCaptureActive = false;
        captureEndFrame = Time.frameCount + 1;
        // Held action buttons must be released; a latched gear may stay selected.
        releaseGuard = RawButtons;
        foreach (int gearButton in shifterButtons)
            if (gearButton >= 0) releaseGuard &= ~(1u << gearButton);
        pressedButtons = 0;
    }

    private void Update()
    {
        if (!Application.isFocused)
            return;
        int requestedRange = Mathf.Clamp(Mathf.RoundToInt(Settings.rotationDegrees), 40, 900);
        bool valid = backend.Poll(requestedRange, Time.unscaledTime, out var sample);
        // A failed acquisition cannot tell us whether a previously detected wheel was unplugged.
        if (backend.DeviceScanAvailable)
        {
            WheelName = backend.Name;
            WheelConnected = !string.IsNullOrEmpty(WheelName);
            if (!WheelConnected)
                hasCalibrationSample = false;
        }
        Status = backend.Status;
        if (!valid || backend.CombinedPedals)
        {
            HasLiveSample = false;
            if (backend.CombinedPedals)
            {
                hasCalibrationSample = false;
                Status = "Turn off combined pedals in G HUB";
            }
            ResetInput();
            return;
        }

        ApplySample(sample, requestedRange);
    }

    private void ApplySample(LogitechWheelBackend.Sample sample, int requestedRange)
    {
        lastSample = sample;
        releaseGuard &= sample.Buttons;
        RawSteering = Read(sample, steeringAxis);
        RawThrottle = Read(sample, throttle.axis);
        RawBrake = Read(sample, brake.axis);
        RawClutch = Read(sample, clutch.axis);
        HasLiveSample = sample.X != 0 || sample.Y != 0 || sample.Z != 0 || sample.Rx != 0 ||
            sample.Ry != 0 || sample.Rz != 0 || sample.Slider0 != 0 || sample.Slider1 != 0;
        if (!HasLiveSample)
        {
            Status = "Waiting for a valid DirectInput sample";
            ResetInput();
            return;
        }
        if (WheelConnected)
        {
            calibrationSample = sample;
            calibrationSampleTime = Time.realtimeSinceStartupAsDouble;
            hasCalibrationSample = true;
        }
        if (CalibrationInProgress)
        {
            ResetInput();
            return;
        }
        if (throttle.released == throttle.pressed || brake.released == brake.pressed ||
            clutch.released == clutch.pressed || steeringCentre <= Mathf.Min(steeringLeft, steeringRight) ||
            steeringCentre >= Mathf.Max(steeringLeft, steeringRight))
        {
            Status = "Check steering and pedal calibration endpoints";
            ResetInput();
            return;
        }

        float gas = throttle.Normalize(RawThrottle);
        float stop = brake.Normalize(RawBrake);
        float clutchPedal = clutch.Normalize(RawClutch);
        if (!Ready)
        {
            // Require an observed released state after connection or loss of acquisition.
            if (Mathf.Max(gas, Mathf.Max(stop, clutchPedal)) > .08f)
            {
                Status = "Release pedals or check their axis calibration";
                return;
            }
            Ready = true;
            Buttons = sample.Buttons;
            activityAnchor = new Vector4(NormalizeSteering(), gas, stop, clutchPedal);
        }

        Status = HasMeasuredCalibration ? "G29 ready with measured profile" : "G29 ready with built-in profile";

        SteeringInput = NormalizeSteering();
        ThrottleInput = gas;
        BrakeInput = stop;
        ClutchInput = clutchPedal;
        SteeringDegrees = SteeringInput * (RangeVerified ? backend.Range : requestedRange) * .5f;
        RoadWheelTarget = Mathf.Clamp(SteeringDegrees / Mathf.Max(1f, Settings.steeringRatio), -(car != null ? car.maxSteerAngle : 40f), car != null ? car.maxSteerAngle : 40f);
        pressedButtons = sample.Buttons & ~Buttons;
        uint changedButtons = sample.Buttons ^ Buttons;
        Buttons = sample.Buttons;

        var input = new Vector4(SteeringInput, gas, stop, clutchPedal);
        var change = input - activityAnchor;
        bool steeringMoved = Mathf.Abs(change.x) >= .035f && Mathf.Abs(input.x) >= .045f;
        bool pedalMoved = MeaningfulPedal(input.y, change.y) || MeaningfulPedal(input.z, change.z) || MeaningfulPedal(input.w, change.w);
        HasActivity = !InputSuppressed && (steeringMoved || pedalMoved || ShiftUpPressed || ShiftDownPressed ||
            HasButton(pressedButtons, handbrakeButton) || ShifterMoved(changedButtons));
        if (Mathf.Abs(change.x) >= .035f) activityAnchor.x = input.x;
        if (Mathf.Abs(change.y) >= .04f) activityAnchor.y = input.y;
        if (Mathf.Abs(change.z) >= .04f) activityAnchor.z = input.z;
        if (Mathf.Abs(change.w) >= .04f) activityAnchor.w = input.w;
    }

    private static bool MeaningfulPedal(float value, float change) => value >= .08f && Mathf.Abs(change) >= .04f;
    private static bool HasButton(uint buttons, int index) => index >= 0 && index < 32 && (buttons & (1u << index)) != 0;

    private float NormalizeSteering() => (steeringRight > steeringCentre ? RawSteering >= steeringCentre : RawSteering <= steeringCentre)
        ? Mathf.InverseLerp(steeringCentre, steeringRight, RawSteering)
        : -Mathf.InverseLerp(steeringCentre, steeringLeft, RawSteering);

    public bool TryGetSelectedGear(out int gear)
        => ReadShifterGear(Buttons, out gear);

    public bool TryGetRawShifterGear(out int gear)
        => DecodeShifterGear(RawButtons, out gear);

    private bool ReadShifterGear(uint buttons, out int gear)
    {
        if (!useHShifter || InputSuppressed)
        {
            gear = 0;
            return false;
        }
        return DecodeShifterGear(buttons, out gear);
    }

    private bool DecodeShifterGear(uint buttons, out int gear)
    {
        gear = 0;
        if (shifterButtons == null || shifterButtons.Length != 7)
            return false;
        for (int i = 0; i < shifterButtons.Length; i++)
        {
            if (!HasButton(buttons, shifterButtons[i]))
                continue;
            if (gear != 0)
            {
                gear = 0;
                return true; // Ambiguous positions safely select neutral.
            }
            gear = i == shifterButtons.Length - 1 ? -1 : i + 1;
        }
        if (car != null && gear > car.gearRatios.Length) gear = 0;
        return true;
    }

    private bool ShifterMoved(uint changedButtons)
    {
        if (!useHShifter || shifterButtons == null)
            return false;
        foreach (int button in shifterButtons)
            if (HasButton(changedButtons, button))
                return true;
        return false;
    }

    private static int Read(LogitechWheelBackend.Sample sample, Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return sample.X;
            case Axis.Y: return sample.Y;
            case Axis.Z: return sample.Z;
            case Axis.Rx: return sample.Rx;
            case Axis.Ry: return sample.Ry;
            case Axis.Rz: return sample.Rz;
            case Axis.Slider0: return sample.Slider0;
            case Axis.Slider1: return sample.Slider1;
            default: return 0;
        }
    }

    [ContextMenu("Capture steering centre while stopped")]
    private void CaptureCentre()
    {
        if (!Application.isPlaying || !Ready || (car == null || car.SpeedKmh > .5f) || Mathf.Abs(SteeringInput) > .1f)
            return;
        steeringCentre = RawSteering;
        if (HasMeasuredCalibration)
            SaveMeasuredCalibration();
        else
        {
            PlayerPrefs.SetInt(CentreKey, steeringCentre);
            PlayerPrefs.Save();
        }
    }

    public void SetCalibrationInProgress(bool active)
    {
        CalibrationInProgress = active;
        SteeringForceFeedback.MotorArmed = false;
        ResetInput();
    }

    public bool TryGetCalibrationSample(out int[] axes, out uint buttons, out uint pov)
    {
        axes = null;
        buttons = pov = 0;
        if (!WheelConnected || !isActiveAndEnabled || CalibrationSampleAgeMs > MaxCalibrationSampleAgeMs)
            return false;

        axes = new int[8];
        for (int i = 0; i < axes.Length; i++)
            axes[i] = Read(calibrationSample, (Axis)i);
        buttons = calibrationSample.Buttons;
        pov = calibrationSample.Pov;
        return true;
    }

    private static string FormatRawAxes(LogitechWheelBackend.Sample sample) =>
        $"X {sample.X}  Y {sample.Y}  Z {sample.Z}  Rx {sample.Rx}  Ry {sample.Ry}  Rz {sample.Rz}  Slider0 {sample.Slider0}  Slider1 {sample.Slider1}";

    public void SaveMeasuredCalibration()
    {
        HasMeasuredCalibration = true;
        // Optional developer calibration writes through the same authoritative binding store.
        int up = shiftUpButton, down = shiftDownButton;
        WheelSettings.Set(WheelSettings.Binding.ShiftUp, up, true);
        WheelSettings.Set(WheelSettings.Binding.ShiftDown, down, true);
        PlayerPrefs.SetString(CalibrationKey, JsonUtility.ToJson(this));
        PlayerPrefs.Save();
    }

    public void SaveShifterMapping(int[] mapping, uint neutralButtons)
    {
        if (mapping == null || mapping.Length != 7 || !IsValidShifterMapping(mapping))
            throw new ArgumentException("A learned G29 map needs seven distinct gear buttons.", nameof(mapping));
        for (int i = 0; i < 7; i++) WheelSettings.Set((WheelSettings.Binding)i, mapping[i], true);
        LearnedNeutralButtons = neutralButtons;
        WheelSettings.UseShifter = true;
    }

    public void SaveShifterMode() => WheelSettings.UseShifter = useHShifter;

    internal bool CanSendForce => CanDrive && backend.ForceAvailable;
    internal bool ConstantForceActive => backend.ConstantForceActive;
    internal bool SendForce(int percent) => backend.SendForce(percent);
    internal void StopForce() => backend.StopForce();

    private void ResetInput()
    {
        StopForce();
        Ready = HasActivity = false;
        releaseGuard = 0;
        Buttons = pressedButtons = 0;
        SteeringInput = ThrottleInput = BrakeInput = ClutchInput = SteeringDegrees = RoadWheelTarget = 0f;
    }

    private void Close(bool retainDetection = false)
    {
        ResetInput();
        backend.Dispose();
        HasLiveSample = false;
        if (!retainDetection)
        {
            hasCalibrationSample = false;
            WheelConnected = false;
            WheelName = "";
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            Close(retainDetection: true);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Close(retainDetection: true);
    }

    private void OnDisable() { EndBindingCapture(); Close(); }
    private void OnDestroy() => WheelSettings.Changed -= ApplyBindings;
    private void OnApplicationQuit() => Close();
}
