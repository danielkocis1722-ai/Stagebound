using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LogitechWheelInput))]
public sealed class LogitechWheelInputInspector : Editor
{
    private static readonly string[] WheelSteps =
    {
        "Centre the wheel, release all pedals and buttons, and put the shifter in neutral.",
        "Hold the wheel at full left lock.",
        "Hold the wheel at full right lock.",
        "Centre the wheel. Hold only the throttle fully down.",
        "Release the throttle. Hold only the brake fully down.",
        "Release the brake. Hold only the clutch fully down.",
        "Release the clutch. Hold the shift-up paddle.",
        "Release the up paddle. Hold the shift-down paddle."
    };

    private static LogitechWheelInput calibrationWheel;
    private static int step = -1;
    private static int[] neutral;
    private static uint neutralButtons;
    private static int steeringAxis, left, right, upPaddle, downPaddle;
    private static readonly int[] pedalAxes = new int[3];
    private static readonly int[] pedalEnds = new int[3];
    private static readonly int[] gearButtons = new int[7];
    private static bool includeShifter;
    private static bool shifterOnly;
    private static string captureMessage;
    private bool showAdvanced;

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    [InitializeOnLoadMethod]
    private static void RegisterSessionCleanup() => EditorApplication.update += CleanUpSession;

    public override void OnInspectorGUI()
    {
        var wheel = (LogitechWheelInput)target;
        EditorGUILayout.HelpBox("Player controls: Main Menu or Pause > Settings > Wheel. Saved in-game bindings are authoritative.", MessageType.Info);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode for optional developer diagnostics.", MessageType.Info);
            return;
        }

        var car = wheel.GetComponent<CarController>();
        var feedback = wheel.GetComponent<SteeringForceFeedback>();
        if (feedback != null)
            DrawForceFeedback(wheel, feedback);

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Wheel and pedal diagnostics", true);
        if (!showAdvanced)
            return;
        DrawShifter(wheel);
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Live G29", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Connection", wheel.WheelConnected ? wheel.WheelName : "G29 not detected");
        EditorGUILayout.LabelField("DirectInput sample", wheel.HasLiveSample ? "Live" : "Waiting for Game view input");
        EditorGUILayout.HelpBox(wheel.Status ?? "Waiting for wheel input", MessageType.Info);
        EditorGUILayout.LabelField("Driving source", car != null ? car.ActiveDrivingSource.ToString() : "Settings preview");
        EditorGUILayout.LabelField("Operating range", wheel.RangeVerified ? $"{wheel.ReportedRange}° (SDK)" : $"{wheel.Settings.rotationDegrees:0}° (configured fallback)");
        EditorGUILayout.LabelField("Raw axes", wheel.RawAxes, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Raw buttons / POV", $"0x{wheel.RawButtons:X8} / {wheel.RawPov}");
        EditorGUILayout.LabelField("Wheel → road angle", $"{wheel.SteeringInput:F3} / {wheel.SteeringDegrees:F1}° → {wheel.RoadWheelTarget:F1}°");
        EditorGUILayout.LabelField("Throttle / brake / clutch", $"{wheel.ThrottleInput:F2} / {wheel.BrakeInput:F2} / {wheel.ClutchInput:F2}");
        if (calibrationWheel != wheel || !shifterOnly)
            DrawCalibration(wheel);
        if (feedback != null)
        {
            DrawTire("Front left", feedback.FrontLeft);
            DrawTire("Front right", feedback.FrontRight);
        }
    }

    private static void DrawShifter(LogitechWheelInput wheel)
    {
        EditorGUILayout.LabelField("H-SHIFTER", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(calibrationWheel != null || !wheel.CanStartCalibration))
        {
            if (GUILayout.Button("Learn H-shifter"))
            {
                BeginCalibration(wheel, true);
                GUIUtility.ExitGUI();
            }
        }
        string position = wheel.TryGetRawShifterGear(out int gear) ? GearLabel(gear) :
            wheel.HasShifterMapping ? "Ambiguous gear buttons" : "Unlearned";
        EditorGUILayout.LabelField("Current position", position);
        var pressed = new System.Collections.Generic.List<int>();
        for (int index = 0; index < 32; index++)
            if ((wheel.RawButtons & (1u << index)) != 0)
                pressed.Add(index);
        EditorGUILayout.LabelField("Pressed DirectInput buttons", pressed.Count == 0 ? "None" : string.Join(", ", pressed));
        EditorGUILayout.LabelField("POV", wheel.RawPov.ToString());
        EditorGUILayout.LabelField("SDK shifter mode", wheel.ShifterMode == 1 ? "Gated" : wheel.ShifterMode == 0 ? "Sequential" : "Unknown");
        if (wheel.HasShifterMapping)
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || wheel.CalibrationInProgress))
            {
                bool enabled = EditorGUILayout.Toggle("Use H shifter", wheel.useHShifter);
                if (enabled != wheel.useHShifter)
                {
                    wheel.useHShifter = enabled;
                    wheel.SaveShifterMode();
                }
            }
        }
        EditorGUILayout.LabelField("N", wheel.LearnedNeutralButtons is uint mask
            ? $"No gear button (learned mask 0x{mask:X8})" : "No mapped gear button");
        for (int i = 0; i < 6; i++)
            EditorGUILayout.LabelField(GearLabel(i + 1), wheel.HasShifterMapping && i < wheel.shifterButtons.Length - 1
                ? $"Button {wheel.shifterButtons[i]}" : "Unlearned");
        EditorGUILayout.LabelField("R", wheel.HasShifterMapping ? $"Button {wheel.shifterButtons[wheel.shifterButtons.Length - 1]}" : "Unlearned");
        if (calibrationWheel == wheel && shifterOnly)
            DrawCalibration(wheel);
        else if (shifterOnly && !string.IsNullOrEmpty(captureMessage))
            EditorGUILayout.HelpBox(captureMessage, MessageType.Info);
    }

    private static string GearLabel(int gear) => gear == 0 ? "N" : gear == -1 ? "R" : gear.ToString();

    private static void DrawForceFeedback(LogitechWheelInput wheel, SteeringForceFeedback feedback)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("FORCE FEEDBACK", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!wheel.WheelConnected || wheel.CalibrationInProgress))
            SteeringForceFeedback.MotorArmed = EditorGUILayout.Toggle("Motor Armed", SteeringForceFeedback.MotorArmed);
        EditorGUI.BeginChangeCheck();
        float gain = EditorGUILayout.Slider("Master FFB", SteeringForceFeedback.MasterStrength, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
            SteeringForceFeedback.MasterStrength = gain;
        feedback.invertMotor = EditorGUILayout.Toggle("Invert Motor", feedback.invertMotor);
        EditorGUILayout.LabelField("SDK force capability", wheel.MotorSupported ? "Available" : "Waiting for SDK");
        using (new EditorGUI.DisabledScope(!SteeringForceFeedback.MotorArmed || wheel.CalibrationInProgress))
        {
            if (GUILayout.Button("Constant Force Test +15%")) feedback.QueueMotorTest(1);
            if (GUILayout.Button("Constant Force Test -15%")) feedback.QueueMotorTest(-1);
        }
        if (GUILayout.Button("Stop Motor")) feedback.StopMotorTest();
        EditorGUILayout.HelpBox(feedback.MotorTestStatus, MessageType.Info);
        EditorGUILayout.HelpBox("After observing the RAW +15% pulse, confirm its physical direction below. This sets the existing Invert option once; it does not add another inversion.", MessageType.Info);
        if (GUILayout.Button("Observed raw +15% turning LEFT")) feedback.ConfirmPositivePulseDirection(-1);
        if (GUILayout.Button("Observed raw +15% turning RIGHT")) feedback.ConfirmPositivePulseDirection(1);
        EditorGUILayout.LabelField("Expected restoring direction", feedback.ExpectedRestoringDirection);
        EditorGUILayout.LabelField("Actual motor direction (mapped command)", feedback.ActualMotorDirection);
        EditorGUILayout.LabelField("Restoring direction valid", feedback.RestoringDirectionValid);
        EditorGUILayout.LabelField("Mechanical / caster / pneumatic", $"{feedback.MechanicalTorque:F3} / {feedback.CasterTorque:F3} / {feedback.PneumaticTorque:F3} N·m");
        EditorGUILayout.LabelField("Raw / filtered wheel torque", $"{feedback.RawWheelTorque:F3} / {feedback.FilteredTorque:F3} N·m");
        EditorGUILayout.LabelField("SAT Torque", $"{feedback.AligningTorque:F2} N·m");
        EditorGUILayout.LabelField("Damping Torque", $"{feedback.DampingTorque:F2} N·m");
        EditorGUILayout.LabelField("Friction Torque", $"{feedback.FrictionTorque:F2} N·m");
        EditorGUILayout.LabelField("Static / breakaway excess", $"{feedback.BreakawayTorque:F3} N·m");
        EditorGUILayout.LabelField("Moving / Coulomb friction", $"{feedback.CoulombTorque:F3} N·m");
        EditorGUILayout.LabelField("Stationary Scrub Torque", $"{feedback.ScrubTorque:F2} N·m");
        EditorGUILayout.LabelField("Centre approach damping", $"{feedback.CenterDampingTorque:F3} N·m");
        EditorGUILayout.LabelField("Physical wheel velocity", $"{feedback.WheelAngularVelocity:F3} rad/s");
        EditorGUILayout.LabelField("Final Motor Output", $"{feedback.MotorPercent}%");
        EditorGUILayout.LabelField("Total wheel torque", $"{feedback.RawWheelTorque:F3} N·m");
        EditorGUILayout.HelpBox(feedback.RecordingStatus, MessageType.Info);
        EditorGUILayout.LabelField("Peak accepted driving output", feedback.PeakDrivingOutput);
        if (GUILayout.Button("Reset driving output peak"))
            feedback.ResetDrivingOutputPeak();
    }

    private static void DrawCalibration(LogitechWheelInput wheel)
    {
        EditorGUILayout.Space();
        if (calibrationWheel != wheel)
        {
            using (new EditorGUI.DisabledScope(calibrationWheel != null))
                includeShifter = EditorGUILayout.Toggle("Measure H-shifter too", includeShifter);
            using (new EditorGUI.DisabledScope(calibrationWheel != null || !wheel.CanStartCalibration))
            {
                if (GUILayout.Button("Start optional G29 calibration"))
                    BeginCalibration(wheel, false);
            }
            if (wheel.CanStartCalibration && !wheel.HasMeasuredCalibration)
                EditorGUILayout.HelpBox("The built-in G29 profile is available. Calibrate here if the controls need adjustment.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(CurrentInstruction + " Hold the position and click Capture Current Step.", MessageType.Info);
            double age = wheel.CalibrationSampleAgeMs;
            EditorGUILayout.LabelField("Latest valid sample age", double.IsPositiveInfinity(age) ? "No sample" : $"{age:F0} ms");
            EditorGUILayout.LabelField("Latest valid raw axes", wheel.CalibrationRawAxes, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Capture Current Step"))
            {
                CaptureCurrentStep(wheel);
                GUIUtility.ExitGUI();
            }
            if (calibrationWheel == wheel && GUILayout.Button("Restart Calibration"))
                BeginCalibration(wheel, shifterOnly);
            if (calibrationWheel == wheel && GUILayout.Button("Cancel Calibration"))
            {
                EndCalibration();
                captureMessage = "Calibration cancelled.";
            }
        }
        if (!string.IsNullOrEmpty(captureMessage))
            EditorGUILayout.HelpBox(captureMessage, MessageType.Info);
    }

    private static int ShifterStart => shifterOnly ? 0 : WheelSteps.Length;
    private static int TotalSteps => shifterOnly || includeShifter ? ShifterStart + 8 : WheelSteps.Length;

    private static string CurrentInstruction => step < ShifterStart ? WheelSteps[step] :
        step == ShifterStart ? "Put the H-shifter in neutral and release all buttons." :
        step == ShifterStart + 7 ? "Hold the H-shifter in reverse." :
        $"Hold the H-shifter in gear {step - ShifterStart}.";

    private static void BeginCalibration(LogitechWheelInput wheel, bool measureShifterOnly)
    {
        calibrationWheel = wheel;
        shifterOnly = measureShifterOnly;
        step = 0;
        captureMessage = null;
        wheel.SetCalibrationInProgress(true);
        RefreshPrompt();
    }

    private static void EndCalibration()
    {
        if (calibrationWheel != null)
        {
            calibrationWheel.CalibrationPrompt = null;
            calibrationWheel.SetCalibrationInProgress(false);
        }
        calibrationWheel = null;
        step = -1;
    }

    private static void RefreshPrompt()
    {
        calibrationWheel.CalibrationPrompt = $"G29 calibration {step + 1}/{TotalSteps}\n{CurrentInstruction}\n" +
            (captureMessage ?? "Hold the position and click Capture Current Step in the Inspector.");
    }

    private static void CleanUpSession()
    {
        if (!Application.isPlaying)
        {
            if (step >= 0)
                EndCalibration();
            captureMessage = null;
            return;
        }
        if (step >= 0 && (calibrationWheel == null || !calibrationWheel.isActiveAndEnabled))
            EndCalibration();

    }

    private static void CaptureCurrentStep(LogitechWheelInput wheel)
    {
        if (!wheel.TryGetCalibrationSample(out int[] raw, out uint buttons, out uint pov))
        {
            captureMessage = "Move the requested control, then click Capture Current Step again.";
            RefreshPrompt();
            return;
        }
        if (wheel.GetComponent<CarController>().SpeedKmh > .5f)
        {
            captureMessage = "Stop the car before capturing.";
            RefreshPrompt();
            return;
        }

        if (!CaptureStep(raw, buttons, out captureMessage))
        {
            RefreshPrompt();
            return;
        }

        Debug.Log($"G29 calibration step {step + 1} captured\n" +
            $"X = {raw[0]}, Y = {raw[1]}, Z = {raw[2]}, Rx = {raw[3]}, Ry = {raw[4]}, Rz = {raw[5]}, " +
            $"Slider0 = {raw[6]}, Slider1 = {raw[7]}, buttons 0x{buttons:X8}, POV {pov}", wheel);
        step++;
        if (step < TotalSteps)
        {
            RefreshPrompt();
            return;
        }

        if (shifterOnly)
        {
            wheel.SaveShifterMapping(gearButtons, neutralButtons);
            EndCalibration();
            captureMessage = "H-shifter map saved. Motor remains disarmed.";
            Debug.Log($"G29 H-shifter measured (1–6, R): {string.Join(", ", gearButtons)}; neutral mask 0x{neutralButtons:X8}", wheel);
            return;
        }

        wheel.steeringAxis = (LogitechWheelInput.Axis)steeringAxis;
        wheel.steeringLeft = left;
        wheel.steeringCentre = neutral[steeringAxis];
        wheel.steeringRight = right;
        var pedals = new[] { wheel.throttle, wheel.brake, wheel.clutch };
        for (int i = 0; i < pedals.Length; i++)
        {
            pedals[i].axis = (LogitechWheelInput.Axis)pedalAxes[i];
            pedals[i].released = neutral[pedalAxes[i]];
            pedals[i].pressed = pedalEnds[i];
        }
        wheel.shiftUpButton = upPaddle;
        wheel.shiftDownButton = downPaddle;
        wheel.useHShifter = includeShifter;
        if (includeShifter)
            wheel.SaveShifterMapping(gearButtons, neutralButtons);
        else if (wheel.HasShifterMapping)
            wheel.SaveShifterMode();
        SteeringForceFeedback.MasterStrength = Mathf.Min(SteeringForceFeedback.MasterStrength, .25f);
        wheel.SaveMeasuredCalibration();
        EndCalibration();
        captureMessage = "Measured calibration saved. Release all controls. Motor remains disarmed for the first sign check.";
        Debug.Log($"G29 measured calibration saved: {JsonUtility.ToJson(wheel)}", wheel);
    }

    private static bool CaptureStep(int[] raw, uint buttons, out string message)
    {
        message = null;
        if (step >= ShifterStart)
            return CaptureShifterStep(buttons, out message);
        if (step == 0)
        {
            neutral = raw;
            neutralButtons = buttons;
            return true;
        }

        if (step >= 1 && step <= 5)
        {
            int axis = DominantAxis(neutral, raw);
            if (axis < 0)
            {
                message = "Move only the requested control through its full travel and try again.";
                return false;
            }
            if (step == 1)
            {
                steeringAxis = axis;
                left = raw[axis];
            }
            else if (step == 2)
            {
                if (axis != steeringAxis || Math.Sign((long)left - neutral[axis]) == Math.Sign((long)raw[axis] - neutral[axis]))
                {
                    message = "Right lock must move the steering axis to the opposite side of centre.";
                    return false;
                }
                right = raw[axis];
            }
            else
            {
                int pedal = step - 3;
                if (axis == steeringAxis || Array.IndexOf(pedalAxes, axis, 0, pedal) >= 0)
                {
                    message = "Each pedal needs its own axis. Check that combined pedals are off.";
                    return false;
                }
                pedalAxes[pedal] = axis;
                pedalEnds[pedal] = raw[axis];
            }
            return true;
        }

        if (!TryReadButton(buttons, out int button))
        {
            message = "Hold only the requested paddle or gear position. Capture requires one new button bit.";
            return false;
        }
        if (step == 6)
            upPaddle = button;
        else if (step == 7 && button != upPaddle)
            downPaddle = button;
        else
        {
            message = "This button was already captured. Move to the requested paddle or gear and retry.";
            return false;
        }
        return true;
    }

    private static bool CaptureShifterStep(uint buttons, out string message)
    {
        message = null;
        int position = step - ShifterStart;
        if (position == 0)
        {
            neutralButtons = buttons;
            if (shifterOnly)
            {
                upPaddle = calibrationWheel.shiftUpButton;
                downPaddle = calibrationWheel.shiftDownButton;
            }
            return true;
        }
        if (!TryReadButton(buttons, out int button) || button == upPaddle || button == downPaddle ||
            Array.IndexOf(gearButtons, button, 0, position - 1) >= 0)
        {
            message = "Hold only the requested gear. Capture needs one distinct gear button.";
            return false;
        }
        gearButtons[position - 1] = button;
        return true;
    }

    private static bool TryReadButton(uint buttons, out int index)
    {
        uint changed = buttons & ~neutralButtons;
        index = 0;
        if (changed == 0 || (changed & (changed - 1)) != 0)
            return false;
        while ((changed & 1) == 0)
        {
            index++;
            changed >>= 1;
        }
        return true;
    }

    private static int DominantAxis(int[] baseline, int[] current)
    {
        int axis = -1;
        long largest = 0, runnerUp = 0;
        for (int i = 0; i < current.Length; i++)
        {
            long delta = Math.Abs((long)current[i] - baseline[i]);
            if (delta > largest)
            {
                runnerUp = largest;
                largest = delta;
                axis = i;
            }
            else if (delta > runnerUp)
                runnerUp = delta;
        }
        return largest >= 16 && largest > runnerUp * 2 ? axis : -1;
    }

    private static void DrawTire(string label, SteeringForceFeedback.TireSample tire)
    {
        EditorGUILayout.LabelField(label, $"{tire.slipAngleDegrees:F1}°  Fz {tire.normalLoad:F0} N  Fy {tire.lateralForce:F0} N");
        EditorGUILayout.LabelField("WheelHit side / forward slip", $"{tire.sidewaysSlip:F3} / {tire.forwardSlip:F3}");
        EditorGUILayout.LabelField("Mechanical / pneumatic moment", $"{tire.mechanicalMoment:F2} / {tire.pneumaticMoment:F2} N·m");
        EditorGUILayout.LabelField("Caster moment / contact velocity", $"{tire.casterMoment:F3} N·m / {tire.longitudinalVelocity:F2}, {tire.lateralVelocity:F2} m/s");
    }
}
