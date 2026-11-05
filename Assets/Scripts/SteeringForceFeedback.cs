using System;
using UnityEngine;

// The motor is the only output. Tire estimates never feed back into vehicle physics.
[DefaultExecutionOrder(170)]
[DisallowMultipleComponent]
public sealed class SteeringForceFeedback : MonoBehaviour
{
    [Serializable]
    public class Tuning
    {
        [Range(0f, 2f)] public float vehicleStrength = 1f;
        [Min(0f)] public float aligningStrength = 1f;
        [Tooltip("Effective steering-axis mechanical trail for FFB; does not change vehicle handling.")]
        [Min(0f)] public float mechanicalTrailMetres = .035f;
        [Tooltip("Positive caster tilts the steering axis rearward. FFB geometry only.")]
        [Range(0f, 15f)] public float casterAngleDegrees = 6f;
        [Min(0f)] public float pneumaticTrailMetres = .055f;
        public AnimationCurve pneumaticTrailBySlipDegrees = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(4f, .9f), new Keyframe(8f, .42f),
            new Keyframe(15f, .08f), new Keyframe(25f, 0f));
        [Tooltip("Cornering stiffness in newtons per radian per newton of normal load.")]
        [Min(0f)] public float corneringStiffnessPerLoad = 9f;
        [Min(0f)] public float frictionCoefficient = 1.1f;
        [Min(0f)] public float dampingNmPerRadianPerSecond = .094f;
        [Tooltip("Moving steering-system Coulomb friction, separate from parking tire scrub.")]
        [Min(0f)] public float frictionNm = .20f;
        [Tooltip("Low-velocity friction envelope; smooth sign makes torque zero at exact rest.")]
        [Min(0f)] public float staticSteeringFrictionNm = 1.53f;
        [Min(.01f)] public float breakawayVelocityRadPerSecond = .35f;
        [Tooltip("Smooth zero-velocity transition to avoid a discontinuous friction sign.")]
        [Min(.001f)] public float frictionZeroVelocityRadPerSecond = .025f;
        [Header("Neutral-tire centre settling (physical wheel degrees)")]
        [Min(.1f)] public float centerWindowDegrees = 5f;
        [Min(0f)] public float centerDampingNmPerRadianPerSecond = .25f;
        [Min(0f)] public float centerDampingLimitNm = 1.5f;
        [Min(.1f)] public float centerSlipReleaseDegrees = 3f;
        [Min(.1f)] public float centerSatReleaseNm = 1.5f;
        [Tooltip("Effective contact-patch torsional friction radius, not wheel radius.")]
        [Min(0f)] public float parkingScrubRadiusMetres = .010f;
        [Min(0f)] public float parkingScrubCoefficient = .625f;
        [Min(0f)] public float parkingFrictionNm = .225f;
        [Min(.001f)] public float scrubVelocityScale = .025f;
        [Min(0f)] public float scrubFadeStartKmh = 3f;
        [Min(0f)] public float scrubFadeEndKmh = 15f;
        [Min(0f)] public float lowSpeedFadeStartKmh = 3f;
        [Min(0f)] public float lowSpeedFadeEndKmh = 20f;
        [Tooltip("Mechanical/caster rolling fade, independent of pneumatic trail speed fade.")]
        [Min(0f)] public float mechanicalFadeStartKmh = .5f;
        [Min(0f)] public float mechanicalFadeEndKmh = 12f;
        [Tooltip("Simulated wheel Nm mapped to full normalized demand, not measured G29 motor Nm.")]
        [Min(.1f)] public float referenceTorqueNm = 10f;
        [Tooltip("Final motor ceiling after master strength, not a multiplicative gain.")]
        [Range(0f, .6f)] public float maximumOutput = .6f;
        [Min(.1f)] public float smoothing = 14f;
    }

    public bool invertMotor { get => WheelSettings.Invert; set => WheelSettings.Invert = value; }

    public static bool MotorArmed { get => WheelSettings.Enabled; set => WheelSettings.Enabled = value; }


    [Serializable]
    public struct TireSample
    {
        public float slipAngleDegrees, normalLoad, sidewaysSlip, forwardSlip;
        public float lateralForce, aligningMoment;
        public float mechanicalMoment, pneumaticMoment;
        public float casterMoment, longitudinalVelocity, lateralVelocity;
    }

    public static float MasterStrength
    {
        get => WheelSettings.Strength;
        set => WheelSettings.Strength = value;
    }

    public TireSample FrontLeft { get; private set; }
    public TireSample FrontRight { get; private set; }
    public float AligningTorque { get; private set; }
    public float DampingTorque { get; private set; }
    public float FrictionTorque { get; private set; }
    public float BreakawayTorque { get; private set; }
    public float CoulombTorque { get; private set; }
    public float ScrubTorque { get; private set; }
    public float CenterDampingTorque { get; private set; }
    public float WheelAngularVelocity { get; private set; }
    public float FinalNormalizedDemand { get; private set; }
    public float FilteredOutput { get; private set; }
    public int MotorPercent { get; private set; }
    public float MechanicalTorque { get; private set; }
    public float PneumaticTorque { get; private set; }
    public float CasterTorque { get; private set; }
    public float RawWheelTorque => AligningTorque + DampingTorque + FrictionTorque + ScrubTorque + CenterDampingTorque;
    public float FilteredTorque { get; private set; }

    private CarController car;
    private Rigidbody body;
    private LogitechWheelInput wheel;
    private float lastPhysicsTime = float.NegativeInfinity;
    private float previousDegrees, angularSpeed;
    private bool hasAngle;

#if UNITY_EDITOR
    public string MotorTestStatus { get; private set; } = "Ready for a stationary motor test.";
    public string PeakDrivingOutput { get; private set; } = "No accepted driving output yet";
    private int peakDrivingPercent;
    public string RecordingStatus { get; private set; } = "F8: record baseline; F9: mark hands-off; F10: save.";
    private System.Text.StringBuilder recording;
    private float recordingStart, releaseTime, releaseAngle, halfReturnTime, peakTorque, peakMotor;
    private bool released;
    // Observation of raw SDK pulses, independent of the player's Invert setting.
    public int ObservedPositiveCommandDirection { get; private set; }
    public void ConfirmPositivePulseDirection(int direction)
    {
        ObservedPositiveCommandDirection = direction < 0 ? -1 : 1;
        // The sole physics-to-SDK sign mapping remains the existing Invert setting.
        invertMotor = ObservedPositiveCommandDirection < 0;
    }
    private static string Direction(float value) => Mathf.Abs(value) < .001f ? "ZERO" : value < 0f ? "LEFT" : "RIGHT";
    public string ExpectedRestoringDirection => wheel == null ? "UNKNOWN" : Direction(-wheel.SteeringDegrees);
    public string ActualMotorDirection => ObservedPositiveCommandDirection == 0 ? "UNKNOWN (observe raw pulse)" : Direction(MotorPercent * ObservedPositiveCommandDirection);
    public string RestoringDirectionValid
    {
        get
        {
            if (!IsDriving || car.SpeedKmh < 25f || car.SpeedKmh > 35f ||
                Mathf.Abs(wheel.SteeringDegrees) < 10f ||
                Mathf.Abs(Vector3.Dot(body.linearVelocity, car.transform.right)) > .5f ||
                Vector3.Dot(body.linearVelocity, car.transform.forward) < 0f || body.angularVelocity.magnitude > .2f)
                return "N/A (requires straight forward rolling at 30 km/h)";
            if (ObservedPositiveCommandDirection == 0) return "UNKNOWN";
            return MotorPercent * ObservedPositiveCommandDirection * wheel.SteeringDegrees < 0f ? "YES" : "NO";
        }
    }
    private readonly float[] returnTimes = { .25f, .5f, 1f };
    private readonly float[] returnAngles = { float.NaN, float.NaN, float.NaN };
    private readonly float[] sampledReturnTimes = { float.NaN, float.NaN, float.NaN };
    private float releaseSpeed, releaseTorque;
    private int releaseCommand;

    private void RecordTelemetry()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && Application.isFocused)
        {
            if (keyboard.f8Key.wasPressedThisFrame && recording == null)
            {
                recording = new System.Text.StringBuilder("seconds,speed_kmh,wheel_deg,fl_road_deg,fr_road_deg,fl_slip_deg,fr_slip_deg,fl_fz_n,fr_fz_n,fl_fy_n,fr_fy_n,fl_mechanical_nm,fr_mechanical_nm,fl_pneumatic_nm,fr_pneumatic_nm,wheel_sat_nm,damping_nm,friction_nm,total_wheel_nm,filtered_normalized,accepted_motor_percent,master,invert,driving,release_marker\n");
                recording.Length--; // Extend the single CSV header, preserving column alignment.
                recording.Append(",fl_vlong,fl_vlat,fr_vlong,fr_vlat,fl_caster_nm,fr_caster_nm,combined_mechanical_wheel_nm,combined_pneumatic_wheel_nm,combined_caster_wheel_nm,filtered_torque_nm,positive_sdk_physical_sign,parking_scrub_nm,wheel_rate_rad_s,center_damping_nm,final_normalized_demand,breakaway_nm,coulomb_nm\n");
                recordingStart = Time.unscaledTime;
                releaseAngle = releaseSpeed = releaseTorque = releaseTime = 0f;
                releaseCommand = 0;
                for (int i = 0; i < returnAngles.Length; i++) returnAngles[i] = sampledReturnTimes[i] = float.NaN;
                released = false;
                peakTorque = peakMotor = 0f;
                halfReturnTime = -1f;
                RecordingStatus = "Recording baseline. F9 at release; F10 saves.";
            }
            if (keyboard.f9Key.wasPressedThisFrame && recording != null && !released && IsDriving)
            {
                released = true;
                releaseTime = Time.unscaledTime;
                releaseAngle = wheel.SteeringDegrees;
                releaseSpeed = car.SpeedKmh;
                releaseTorque = RawWheelTorque;
                releaseCommand = MotorPercent;
                peakTorque = peakMotor = 0f;
            }
            if (keyboard.f10Key.wasPressedThisFrame) SaveRecording();
        }
        if (recording == null || wheel == null || car == null) return;
        float now = Time.unscaledTime;
        float angle = wheel.SteeringDegrees;
        if (released && IsDriving)
        {
            for (int i = 0; i < returnTimes.Length; i++)
                if (float.IsNaN(returnAngles[i]) && now - releaseTime >= returnTimes[i] && now - releaseTime <= returnTimes[i] + .1f)
                {
                    returnAngles[i] = angle;
                    sampledReturnTimes[i] = now - releaseTime;
                }
            peakTorque = Mathf.Max(peakTorque, Mathf.Abs(AligningTorque));
            peakMotor = Mathf.Max(peakMotor, Mathf.Abs(MotorPercent));
            if (halfReturnTime < 0f && Mathf.Abs(releaseAngle) >= 10f && Mathf.Abs(angle) <= Mathf.Abs(releaseAngle) * .5f)
                halfReturnTime = now - releaseTime;
        }
        float[] row = { now - recordingStart, car.SpeedKmh, angle,
            car.frontLeft != null ? car.frontLeft.steerAngle : 0f,
            car.frontRight != null ? car.frontRight.steerAngle : 0f,
            FrontLeft.slipAngleDegrees, FrontRight.slipAngleDegrees,
            FrontLeft.normalLoad, FrontRight.normalLoad, FrontLeft.lateralForce, FrontRight.lateralForce,
            FrontLeft.mechanicalMoment, FrontRight.mechanicalMoment, FrontLeft.pneumaticMoment, FrontRight.pneumaticMoment,
            AligningTorque, DampingTorque, FrictionTorque, RawWheelTorque,
            FilteredOutput, MotorPercent, MasterStrength, invertMotor ? 1f : 0f, IsDriving ? 1f : 0f, released ? 1f : 0f,
            FrontLeft.longitudinalVelocity, FrontLeft.lateralVelocity, FrontRight.longitudinalVelocity, FrontRight.lateralVelocity,
            FrontLeft.casterMoment, FrontRight.casterMoment, MechanicalTorque, PneumaticTorque, CasterTorque, FilteredTorque, ObservedPositiveCommandDirection, ScrubTorque, WheelAngularVelocity, CenterDampingTorque, FinalNormalizedDemand, BreakawayTorque, CoulombTorque };
        for (int i = 0; i < row.Length; i++)
        {
            if (i > 0) recording.Append(',');
            recording.Append(row[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        recording.AppendLine();
        if (now - recordingStart >= 120f) SaveRecording();
    }

    private void SaveRecording()
    {
        if (recording == null) return;
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/FFB"));
        string path = System.IO.Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(path, recording.ToString());
            string summary = $"Vehicle: {gameObject.name}; release marked: {released}; starting angle: {releaseAngle:F1} deg; " +
                $"peak SAT: {peakTorque:F3} Nm; peak accepted motor: {peakMotor:F0}%; " +
                $"time to half angle: {(halfReturnTime < 0f ? "not observed" : halfReturnTime.ToString("F3") + " s")}; " +
                $"final angle: {(wheel != null ? wheel.SteeringDegrees : 0f):F1} deg.\n" +
                "Operator release marker only; inspect CSV driving validity and speed. This does not certify hands-off or tire force accuracy.";
            System.IO.File.WriteAllText(path + ".txt", summary);
            string checkpoints = $"\nRelease: {releaseTime:F3}s, {releaseSpeed:F2} km/h, {releaseTorque:F3} Nm, {releaseCommand}% command. Positive SDK physical sign: {ObservedPositiveCommandDirection} (0 unknown).";
            for (int i = 0; i < returnTimes.Length; i++)
                checkpoints += $"\nAt {returnTimes[i]:F2}s: {returnAngles[i]:F2} deg (sample time {sampledReturnTimes[i]:F3}s; NaN means not observed).";
            System.IO.File.AppendAllText(path + ".txt", checkpoints);
            RecordingStatus = "Saved " + path;
            Debug.Log(RecordingStatus + "\n" + summary, this);
        }
        catch (System.IO.IOException error) { RecordingStatus = "Recording save failed: " + error.Message; }
        catch (UnauthorizedAccessException error) { RecordingStatus = "Recording save failed: " + error.Message; }
        recording = null;
    }

    public void ResetDrivingOutputPeak()
    {
        peakDrivingPercent = 0;
        PeakDrivingOutput = "No accepted driving output yet";
    }

    private int testCommand;
    private double testDeadline;
    private bool testPlaying;
    private bool testSeenActive;

    public void QueueMotorTest(int direction)
    {
        if (!MotorArmed || wheel == null || !wheel.WheelConnected || wheel.CalibrationInProgress ||
            body == null || body.linearVelocity.sqrMagnitude > .02f)
        {
            MotorTestStatus = "Arm the connected G29 and stop the car before testing.";
            return;
        }
        wheel.StopForce();
        testCommand = direction < 0 ? -15 : 15;
        testPlaying = false;
        testSeenActive = false;
        testDeadline = Time.realtimeSinceStartupAsDouble + 5;
        MotorTestStatus = "Focus the Game view within five seconds for a 400 ms pulse.";
    }

    public void StopMotorTest() => FinishMotorTest("Stopped (0%).");

    private void FinishMotorTest(string status)
    {
        if (wheel != null)
            wheel.StopForce();
        testCommand = 0;
        testPlaying = false;
        MotorPercent = 0;
        MotorArmed = false;
        MotorTestStatus = status;
    }

    private bool ProcessMotorTest()
    {
        if (testCommand == 0)
            return false;
        double now = Time.realtimeSinceStartupAsDouble;
        bool available = Application.isFocused && wheel.CanSendForce && wheel.HasLiveSample;
        if (!MotorArmed || PauseMenu.IsPaused || wheel.InputSuppressed || !wheel.isActiveAndEnabled || !wheel.WheelConnected || wheel.CalibrationInProgress ||
            body.linearVelocity.sqrMagnitude > .02f || (testPlaying && !available))
            FinishMotorTest("Motor test cancelled. Output stopped.");
        else if (now >= testDeadline)
        {
            FinishMotorTest(testPlaying
                ? $"Pulse stopped. LogiPlayConstantForce accepted. LogiIsPlaying during pulse: {testSeenActive}."
                : "Timed out waiting for live G29 input.");
            Debug.Log($"G29 motor test: {MotorTestStatus}", this);
        }
        else if (testPlaying)
            testSeenActive |= wheel.ConstantForceActive;
        else if (Application.isFocused && wheel.HasLiveSample && !wheel.MotorSupported)
            FinishMotorTest("SDK does not report force feedback support for this device.");
        else if (available && !testPlaying)
        {
            bool accepted = wheel.SendForce(testCommand);
            bool active = wheel.ConstantForceActive;
            testSeenActive = active;
            MotorTestStatus = $"LogiPlayConstantForce({testCommand}%) = {accepted}. LogiIsPlaying = {active}.";
            Debug.Log($"G29 motor test: {MotorTestStatus}", this);
            if (!accepted)
                FinishMotorTest(MotorTestStatus);
            else
            {
                MotorPercent = testCommand;
                testPlaying = true;
                testDeadline = now + .4;
            }
        }
        return true;
    }
#endif

    private void Awake()
    {
        car = GetComponent<CarController>();
        body = GetComponent<Rigidbody>();
        wheel = GetComponent<LogitechWheelInput>();
        WheelSettings.Changed += ApplyUserSettings;
        ApplyUserSettings();
    }

    private void ApplyUserSettings()
    {
        if (!MotorArmed || MasterStrength <= 0f) Clear();
    }

    private void OnDestroy() => WheelSettings.Changed -= ApplyUserSettings;

    private bool IsDriving => car != null && body != null && wheel != null && wheel.isActiveAndEnabled &&
        wheel.CanDrive && car.isActiveAndEnabled && car.ActiveDrivingSource == CarController.DrivingSource.Wheel &&
        Application.isFocused && !PauseMenu.IsPaused && Time.timeScale > 0f;

    private void FixedUpdate()
    {
        if (!IsDriving)
            return;
        var settings = wheel.Settings.feedback;
        FrontLeft = ReadTire(car.frontLeft, settings);
        FrontRight = ReadTire(car.frontRight, settings);
        AligningTorque = (FrontLeft.aligningMoment + FrontRight.aligningMoment) *
            settings.aligningStrength / Mathf.Max(1f, wheel.Settings.steeringRatio);
        float ratio = settings.aligningStrength / Mathf.Max(1f, wheel.Settings.steeringRatio);
        MechanicalTorque = (FrontLeft.mechanicalMoment + FrontRight.mechanicalMoment) * ratio;
        PneumaticTorque = (FrontLeft.pneumaticMoment + FrontRight.pneumaticMoment) * ratio;
        CasterTorque = (FrontLeft.casterMoment + FrontRight.casterMoment) * ratio;
        lastPhysicsTime = Time.unscaledTime;
    }

    private TireSample ReadTire(WheelCollider tire, Tuning settings)
    {
        if (tire == null || !tire.GetGroundHit(out var hit))
            return default;
        Vector3 forward = Vector3.ProjectOnPlane(hit.forwardDir, hit.normal).normalized;
        Vector3 right = Vector3.Cross(hit.normal, forward).normalized;
        Vector3 velocity = body.GetPointVelocity(hit.point);
        Rigidbody groundBody = hit.collider != null ? hit.collider.attachedRigidbody : null;
        if (groundBody != null && groundBody != body)
            velocity -= groundBody.GetPointVelocity(hit.point);
        return EstimateTire(Vector3.Dot(velocity, forward), Vector3.Dot(velocity, right),
            hit.force, hit.sidewaysSlip, hit.forwardSlip, tire.forwardFriction.extremumSlip, settings, tire.steerAngle);
    }

    public static TireSample EstimateTire(float longitudinal, float lateral, float normalLoad,
        float sidewaysSlip, float forwardSlip, float longitudinalPeak, Tuning settings, float steerDegrees = 0f)
    {
        float alpha = Mathf.Atan2(lateral, Mathf.Max(.4f, Mathf.Abs(longitudinal)));
        float angleDegrees = alpha * Mathf.Rad2Deg;
        float load = Mathf.Max(0f, normalLoad);
        float brakingUse = .85f * Mathf.InverseLerp(.05f, Mathf.Max(.1f, longitudinalPeak), Mathf.Abs(forwardSlip));
        float limit = settings.frictionCoefficient * load * Mathf.Sqrt(1f - brakingUse * brakingUse);
        float force = Mathf.Clamp(-settings.corneringStiffnessPerLoad * load * alpha, -limit, limit);
        float trailFactor = settings.pneumaticTrailBySlipDegrees != null
            ? Mathf.Clamp01(settings.pneumaticTrailBySlipDegrees.Evaluate(Mathf.Abs(angleDegrees))) : 0f;
        float speedKmh = Mathf.Sqrt(longitudinal * longitudinal + lateral * lateral) * 3.6f;
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.lowSpeedFadeStartKmh,
            Mathf.Max(settings.lowSpeedFadeStartKmh + 1f, settings.lowSpeedFadeEndKmh), speedKmh));
        float mechanicalFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.mechanicalFadeStartKmh,
            Mathf.Max(settings.mechanicalFadeStartKmh + 1f, settings.mechanicalFadeEndKmh), speedKmh));
        float caster = Mathf.Clamp(settings.casterAngleDegrees, 0f, 15f) * Mathf.Deg2Rad;
        float steer = steerDegrees * Mathf.Deg2Rad;
        // Positive torque means steering right. Project contact moments onto the
        // rearward-tilted axis. Mechanical trail never uses pneumatic slip falloff.
        float mechanical = -force * settings.mechanicalTrailMetres * Mathf.Cos(caster) * mechanicalFade;
        float pneumatic = -force * settings.pneumaticTrailMetres * trailFactor * Mathf.Cos(caster) * fade;
        // Reduced jacking model: h(delta)=trail*sin(caster)*(1-cos(delta));
        // torque=-d(Fz*h)/d(delta). Rolling fade suppresses stationary return.
        // This is a geometry approximation, not a measured suspension model.
        float jacking = -load * settings.mechanicalTrailMetres * Mathf.Sin(caster) * Mathf.Sin(steer) * mechanicalFade;
        return new TireSample
        {
            slipAngleDegrees = angleDegrees,
            normalLoad = load,
            sidewaysSlip = sidewaysSlip,
            forwardSlip = forwardSlip,
            lateralForce = force,
            longitudinalVelocity = longitudinal,
            lateralVelocity = lateral,
            mechanicalMoment = mechanical,
            pneumaticMoment = pneumatic,
            casterMoment = jacking,
            // Patch sits behind the steering axis. Rightward travel gives leftward Fy
            // and a positive (right-turn) moment, reducing the slip angle.
            aligningMoment = mechanical + pneumatic + jacking
        };
    }

    private void LateUpdate()
    {
        ProcessOutput();
#if UNITY_EDITOR
        RecordTelemetry();
#endif
    }

    // Coulomb-like motion resistance with a smooth zero crossing, not a position
    // spring or a static holding-force solver. Equivalent road rate = wheel rate / ratio.
    public static float ParkingBlend(float speedKmh, Tuning settings) => 1f - Mathf.SmoothStep(0f, 1f,
        Mathf.InverseLerp(settings.scrubFadeStartKmh, Mathf.Max(settings.scrubFadeStartKmh + 1f, settings.scrubFadeEndKmh), Mathf.Abs(speedKmh)));

    public static float EstimateScrub(TireSample tire, float wheelRate, float steeringRatio, Tuning settings)
    {
        float ratio = Mathf.Max(1f, steeringRatio);
        float roadRate = wheelRate / ratio;
        float motion = (float)Math.Tanh(roadRate / Mathf.Max(.001f, settings.scrubVelocityScale));
        float contactSpeed = new Vector2(tire.longitudinalVelocity, tire.lateralVelocity).magnitude * 3.6f;
        return -Mathf.Max(0f, tire.normalLoad) * settings.parkingScrubCoefficient * settings.parkingScrubRadiusMetres *
            motion * ParkingBlend(contactSpeed, settings) / ratio;
    }

    public static void EstimateFrictionComponents(float wheelRate, float speedKmh, Tuning settings,
        out float breakaway, out float coulomb)
    {
        float direction = -(float)Math.Tanh(wheelRate / Mathf.Max(.001f, settings.frictionZeroVelocityRadPerSecond));
        float scaledRate = wheelRate / Mathf.Max(.01f, settings.breakawayVelocityRadPerSecond);
        // Regularized Stribeck envelope: higher resistance at first motion, then
        // lower running friction. No angle, heading, slip or drift-state input.
        breakaway = direction * Mathf.Max(0f, settings.staticSteeringFrictionNm - settings.frictionNm) * Mathf.Exp(-scaledRate * scaledRate);
        coulomb = direction * (settings.frictionNm + settings.parkingFrictionNm * ParkingBlend(speedKmh, settings));
    }

    public static float EstimateFriction(float wheelRate, float speedKmh, Tuning settings)
    {
        EstimateFrictionComponents(wheelRate, speedKmh, settings, out float breakaway, out float coulomb);
        return breakaway + coulomb;
    }

    private void ProcessOutput()
    {
#if UNITY_EDITOR
        if (ProcessMotorTest())
            return;
#endif
        if (!IsDriving || Time.unscaledTime - lastPhysicsTime > .15f)
        {
            Clear();
            return;
        }

        var settings = wheel.Settings.feedback;
        float dt = Mathf.Max(.001f, Time.unscaledDeltaTime);
        float degrees = wheel.SteeringDegrees;
        float rate = hasAngle ? Mathf.Clamp((degrees - previousDegrees) * Mathf.Deg2Rad / dt, -15f, 15f) : 0f;
        bool crossedCenter = hasAngle && degrees * previousDegrees <= 0f && Mathf.Abs(degrees - previousDegrees) > .001f;
        previousDegrees = degrees;
        hasAngle = true;
        angularSpeed = Mathf.Lerp(angularSpeed, rate, 1f - Mathf.Exp(-18f * dt));
        WheelAngularVelocity = rate;
        CenterDampingTorque = EstimateCenterDamping(degrees, rate, car.SpeedKmh, FrontLeft, FrontRight, AligningTorque, crossedCenter, settings);
        DampingTorque = -angularSpeed * settings.dampingNmPerRadianPerSecond;
        EstimateFrictionComponents(rate, car.SpeedKmh, settings, out float breakaway, out float coulomb);
        BreakawayTorque = breakaway;
        CoulombTorque = coulomb;
        FrictionTorque = breakaway + coulomb;
        ScrubTorque = EstimateScrub(FrontLeft, rate, wheel.Settings.steeringRatio, settings) +
            EstimateScrub(FrontRight, rate, wheel.Settings.steeringRatio, settings);
        float baseTorque = RawWheelTorque - CenterDampingTorque;
        float normalized = baseTorque /
            Mathf.Max(.1f, settings.referenceTorqueNm) * settings.vehicleStrength;
        if (!float.IsFinite(normalized))
            normalized = 0f;
        FilteredOutput = Mathf.Lerp(FilteredOutput, Mathf.Clamp(normalized, -1f, 1f),
            1f - Mathf.Exp(-settings.smoothing * dt));
        FilteredTorque = Mathf.Lerp(FilteredTorque, baseTorque, 1f - Mathf.Exp(-settings.smoothing * dt));

        // The brief centre crossing needs prompt dissipation, not another 70 ms
        // tail through the SAT filter. The usual master, inversion and hard cap still apply.
        FinalNormalizedDemand = FilteredOutput + CenterDampingTorque / Mathf.Max(.1f, settings.referenceTorqueNm) * settings.vehicleStrength;
        int command = ScaleMotorOutput(FinalNormalizedDemand, settings.maximumOutput, MasterStrength, invertMotor);
        if (!MotorArmed || !wheel.CanSendForce || command == 0)
        {
            wheel.StopForce();
            MotorPercent = 0;
            return;
        }
        MotorPercent = wheel.SendForce(command) ? command : 0;
#if UNITY_EDITOR
        if (Mathf.Abs(MotorPercent) > peakDrivingPercent)
        {
            peakDrivingPercent = Mathf.Abs(MotorPercent);
            PeakDrivingOutput = $"{MotorPercent}% at {car.SpeedKmh:F0} km/h, SAT {AligningTorque:F2} N·m";
        }
#endif
    }


    public static float EstimateCenterDamping(float degrees, float rate, float speedKmh,
        TireSample left, TireSample right, float satNm, bool crossedCenter, Tuning settings)
    {
        // No contact => no special centre behaviour. Use maximum per-tire slip,
        // never axle-average slip: opposite front slips must not cancel the drift veto.
        if (left.normalLoad <= 0f || right.normalLoad <= 0f) return 0f;
        float angle = Mathf.Abs(degrees);
        float window = Mathf.Max(.1f, settings.centerWindowDegrees);
        float approach = degrees * rate < 0f ? 1f : 0f;
        // A soft 1-degree crossing tail dissipates residual momentum without a
        // latched brake. It cannot persist if tire demand becomes meaningful.
        float crossing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(angle / Mathf.Min(1f, window)));
        if (crossedCenter && angle < window) approach = 1f;
        float envelope = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(angle / window))) * Mathf.Max(approach, crossing);
        float slip = Mathf.Max(Mathf.Abs(left.slipAngleDegrees), Mathf.Abs(right.slipAngleDegrees));
        float tireNeutral = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, Mathf.Max(.6f, settings.centerSlipReleaseDegrees), slip));
        float satNeutral = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, Mathf.Max(.3f, settings.centerSatReleaseNm), Mathf.Abs(satNm)));
        float rolling = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 8f, Mathf.Abs(speedKmh)));
        float highSpeed = Mathf.Lerp(1f, .4f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 130f, Mathf.Abs(speedKmh))));
        float moving = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.1f, .8f, Mathf.Abs(rate)));
        return -Mathf.Clamp(rate * settings.centerDampingNmPerRadianPerSecond, -settings.centerDampingLimitNm, settings.centerDampingLimitNm) *
            envelope * tireNeutral * satNeutral * rolling * highSpeed * moving;
    }

    private static int ScaleMotorOutput(float filtered, float cap, float strength, bool inverted)
    {
        float demand = filtered * Mathf.Clamp01(strength);
        float ceiling = Mathf.Clamp(cap, 0f, .6f);
        float percent = Mathf.Clamp(demand, -ceiling, ceiling) * (inverted ? -100f : 100f);
        return float.IsFinite(percent) ? Mathf.RoundToInt(Mathf.Clamp(percent, -60f, 60f)) : 0;
    }

    private void Clear()
    {
        if (wheel != null)
            wheel.StopForce();
        FrontLeft = FrontRight = default;
        AligningTorque = DampingTorque = FrictionTorque = FilteredOutput = angularSpeed = 0f;
        MechanicalTorque = PneumaticTorque = CasterTorque = FilteredTorque = 0f;
        ScrubTorque = 0f;
        BreakawayTorque = CoulombTorque = 0f;
        CenterDampingTorque = WheelAngularVelocity = FinalNormalizedDemand = 0f;
        MotorPercent = 0;
        hasAngle = false;
        lastPhysicsTime = float.NegativeInfinity;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        SaveRecording();
        if (testCommand != 0)
            StopMotorTest();
#endif
        Clear();
    }
    private void OnApplicationQuit() => Clear();
}
