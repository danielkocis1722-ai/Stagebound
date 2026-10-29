using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class CarController : MonoBehaviour
{
    // =========================================================
    // WHEELS
    // =========================================================

    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider backLeft;
    public WheelCollider backRight;

    [Header("Visual Wheels")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform backLeftWheel;
    public Transform backRightWheel;


    // =========================================================
    // ENGINE
    // =========================================================

    [Header("Engine")]

    public float maxEngineTorque = 360f;

    public float idleRPM = 900f;
    public float redlineRPM = 7500f;

    public float engineRevUpSpeed = 9000f;
    public float engineRevDownSpeed = 5000f;
    [Min(0.1f)] public float engineThrottleResponse = 24f;

    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(900f, 0.60f),
        new Keyframe(2000f, 0.84f),
        new Keyframe(3000f, 0.95f),
        new Keyframe(4000f, 1.00f),
        new Keyframe(5000f, 1.00f),
        new Keyframe(6000f, 0.90f),
        new Keyframe(7500f, 0.65f)
    );


    // =========================================================
    // TRANSMISSION
    // =========================================================

    [Header("Transmission")]

    public float[] gearRatios =
    {
        4.20f,
        3.00f,
        2.10f,
        1.55f,
        1.20f
    };

    public float reverseRatio = 3.80f;
    public float finalDrive = 5.30f;

    [Range(0f, 1f)]
    public float drivetrainEfficiency = 0.90f;

    public float shiftDuration = 0.20f;

    public enum DifferentialMode { Open, LimitedSlip }

    [Header("Rear Differential")]
    public DifferentialMode differentialMode = DifferentialMode.LimitedSlip;
    [Range(0f, 1f)] public float lsdStrength = 0.6f;
    [Min(0.01f)] public float differentialResponse = 5f;

    // -1 = R
    //  0 = N
    //  1-5 = gear
    public int currentGear = 1;


    // =========================================================
    // CLUTCH
    // =========================================================

    [Header("Clutch")]

    [Range(0f, 1f)]
    public float launchClutchMin = 0.55f;

    public float launchClutchSpeed = 15f;

    public float clutchEngageSpeed = 4f;
    public float clutchDisengageSpeed = 12f;

    [Header("Manual Clutch")]
    [Tooltip("Engagement units/sec when releasing the pedal/button.")]
    [InspectorName("Manual Clutch Release Speed")]
    [Min(0.01f)] public float manualClutchEngageSpeed = 20f;
    [Tooltip("Disengagement units/sec when pressing the pedal/button.")]
    [InspectorName("Manual Clutch Press Speed")]
    [Min(0.01f)] public float manualClutchReleaseSpeed = 20f;
    [Tooltip("Total transmitted torque cap at the engine side, before gear/final-drive multiplication.")]
    [Min(0f)] public float maxClutchTorque = 1400f;
    [Tooltip("Nm per RPM of mismatch, before timestep-stable clutch relaxation.")]
    [Min(0f)] public float clutchCouplingStrength = 1.2f;
    [Range(0f, 1f)] public float clutchKickStrength = 1f;
    [FormerlySerializedAs("clutchKickEngineInertia")]
    [Tooltip("Effective flywheel inertia (kg m²). All clutch torque loads this engine state.")]
    [Min(0.05f)] public float engineInertia = 0.35f;

    public float EngineThrottle { get; private set; }
    public float NormalizedEngineRPM => Mathf.InverseLerp(idleRPM, redlineRPM, EngineRPM);
    public float TorqueCurveMultiplier { get; private set; }
    public float CalculatedEngineTorque { get; private set; }
    public float TransmittedEngineTorque { get; private set; }
    public float LongitudinalAcceleration { get; private set; }
    public bool DynamicsSampleValid { get; private set; }


    // =========================================================
    // BRAKES
    // =========================================================

    [Header("Brakes")]

    public float brakeTorque = 8000f;

    // Zadná ručná brzda
    public float handbrakeTorque = 15000f;


    // =========================================================
    // HANDBRAKE GRIP
    // =========================================================

    [Header("Handbrake Grip")]

    // Koľko rear gripu zostane počas handbrake.
    // Fraction of normal sideways stiffness retained at full handbrake.
    [Range(0.1f, 1f)]
    public float handbrakeRearGrip = 0.8f;

    // Ako rýchlo zadok stratí grip po stlačení X.
    public float handbrakeGripDropSpeed = .65f;

    // Ako rýchlo sa grip vráti po pustení.
    // Nižšia hodnota = menej snapu späť.
    public float handbrakeGripRecoverySpeed = 0.65f;

    [Header("Power Slip Grip")]
    [Tooltip("Maximum lateral stiffness reduction under measured driven-wheel slip and load. Set to zero to disable.")]
    [Range(0f, 0.12f)] public float powerSlipGripReduction = 0.08f;
    [Min(0.01f)] public float powerSlipGripResponse = 4f;

    [Header("Powered drift grip recovery")]
    [Tooltip("Return of the final non-handbrake rear grip multiplier, per second. Grip loss keeps the existing power/combined-slip response.")]
    [Min(.1f)] public float rearGripRecoveryResponse = .8f;
    [Min(.1f)] public float countersteerGripRecoveryResponse = 1.6f;
    [Min(.1f)] public float controlledDriftGripRecoveryResponse = .6f;
    [Min(0f)] public float driftGripEnterAngle = 10f;
    [Min(0f)] public float driftGripExitAngle = 7f;
    [SerializeField] bool driftGripHeld;
    [SerializeField] float currentRearGripRecovery;
    public bool DriftGripHeld => driftGripHeld;
    public float CurrentRearGripRecovery => currentRearGripRecovery;
    public bool DriftReleaseRecoveryActive => driftReleaseRecovery;

    [Header("Transient Powered Breakaway")]
    public bool enableCombinedSlip = true;
    [Min(0f)] public float combinedSlipStart = 0.10f;
    [Min(0.01f)] public float combinedSlipFull = 0.22f;
    [Range(0.85f, 1f)] public float minimumCombinedSlipMultiplier = 0.85f;
    [Min(0.01f)] public float combinedSlipResponse = 8f;
    [Tooltip("Blend back to the original drift-hold grip model over this body slip angle range.")]
    [Min(0f)] public float breakawayHandoffStartAngle = 8f;
    [Min(0.01f)] public float breakawayHandoffFullAngle = 18f;

    public float RearLeftCombinedSlipMultiplier { get; private set; } = 1f;
    public float RearRightCombinedSlipMultiplier { get; private set; } = 1f;
    public float CombinedSlipEntryWeight { get; private set; }
    public float VehicleSlipAngle { get; private set; }
    public float YawRate => rb != null ? rb.angularVelocity.y * Mathf.Rad2Deg : 0f;

    public float RearLeftGripMultiplier { get; private set; } = 1f;
    public float RearRightGripMultiplier { get; private set; } = 1f;
    public float HandbrakeGripMultiplier => currentHandbrakeGripMultiplier;
    public float RearLeftTorqueShare => rearLeftTorqueShare;
    public float DeliveredAxleTorque => backLeft.motorTorque + backRight.motorTorque;
    public float EngineBrakeAxleTorque { get; private set; }
    public float LimiterFactor { get; private set; } = 1f;
    public bool IsShifting => shiftTimer > 0f;


    // =========================================================
    // STEERING
    // =========================================================

    [Header("Steering")]

    [Tooltip("Maximum inside-front-wheel angle. Full input retains this lock at every speed.")]
    public float maxSteerAngle = 30f;
    public bool useAckermannSteering = true;
    public float SteeringWheelbase { get; private set; }
    public float SteeringFrontTrack { get; private set; }

    public float steeringSpeed = 95f;
    [Tooltip("Wheel angle return/cross-center speed, including opposite player input. Degrees/sec.")]
    public float steeringReturnSpeed = 150f;


    // =========================================================
    // KEYBOARD SMOOTHING
    // =========================================================

    [Header("Keyboard Input Smoothing")]

    public float steeringInputSpeed = 2.0f;
    public float steeringInputReturnSpeed = 2.5f;

    public float throttleInputSpeed = 2.5f;
    public float throttleInputReturnSpeed = 3.0f;


    // =========================================================
    // CONTROLLER
    // =========================================================

    [Header("Controller")]

    [Range(0f, 0.3f)]
    public float steeringDeadzone = 0.06f;

    // Jemnejší stred analog sticku.
    // 1 = linear
    // 1.7 = jemné korekcie, plný stick stále = plný lock
    [FormerlySerializedAs("steeringExponent")]
    [Range(1f, 3f)]
    public float lowSpeedSteeringExponent = 1.4f;

    [Range(1f, 3f)]
    public float highSpeedSteeringExponent = 2.1f;

    [Min(1f)]
    public float fullSteeringCurveSpeed = 130f;


    // =========================================================
    // CAR PHYSICS
    // =========================================================

    [Header("Car Physics")]

    public Vector3 centerOfMass =
        new Vector3(0f, -0.35f, 0f);


    // =========================================================
    // RUNTIME TELEMETRY
    // =========================================================

    [Header("Runtime Telemetry")]

    public float SpeedKmh;
    public float EngineRPM;

    [FormerlySerializedAs("ClutchEngagement")]
    [SerializeField, Range(0f, 1f)] private float finalClutchEngagement;
    public float ClutchEngagement => finalClutchEngagement;
    [Range(0f, 1f)] public float ClutchInput;
    public float AutomaticClutchEngagement => automaticClutchEngagement;
    public float ManualClutchEngagement => manualClutchEngagement;
    public float DrivetrainRPM { get; private set; }
    // Signed in engine/selected-gear coordinates, separate from RPM magnitude.
    public float SignedDrivetrainRPM { get; private set; }
    public float SignedRearWheelRPM => (backLeft.rpm + backRight.rpm) * .5f;
    public int GearDirection => currentGear == 0 ? 0 : currentGear > 0 ? 1 : -1;
    public float SignedLongitudinalSpeedKmh => rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f : 0f;
    public float ClutchRPMDifference => EngineRPM - SignedDrivetrainRPM;
    public bool ClutchKickActive => clutchKick.Active && ClutchKickTorque > 0.001f;
    public float ClutchKickTorque { get; private set; }
    public float ClutchCouplingTorque { get; private set; }
    public float ClutchEngagementRate { get; private set; }

    [Range(0f, 1f)]
    public float HandbrakeInput;

    public float RearWheelRPM;
    public float RearForwardSlip;

    [Range(-1f, 1f)]
    public float SteeringInput;

    [Range(0f, 1f)]
    public float ThrottleInput;

    [Range(0f, 1f)]
    public float BrakeInput;

    public float SteeringAngle;

    public bool IsBraking;


    // =========================================================
    // GEAR TEXT
    // =========================================================

    public string CurrentGearText
    {
        get
        {
            if (currentGear == -1)
                return "R";

            if (currentGear == 0)
                return "N";

            return currentGear.ToString();
        }
    }


    // =========================================================
    // PRIVATE
    // =========================================================

    private Rigidbody rb;

    private float currentSteerAngle;
    private FrontAxleRecovery frontAxleRecovery;

    private float currentKeyboardSteering;
    private float currentKeyboardThrottle;

    private float shiftTimer;

    private float rearLeftNormalStiffness;
    private float rearRightNormalStiffness;

    private float currentHandbrakeGripMultiplier = 1f;
    private float rearLeftTorqueShare = 0.5f;
    private float leftPowerGrip = 1f, rightPowerGrip = 1f;
    float leftRecoveredGrip = 1f, rightRecoveredGrip = 1f;
    bool driftReleaseRecovery;
    private float automaticClutchEngagement = 1f, manualClutchEngagement = 1f;
    private bool clutchKickArmed;
    private float previousClutchInput;
    private int clutchKickGear;
    private float reflectedDrivetrainInertia;
    private float previousDriveRPM, previousDriveTorque, previousDriveRatio;
    private bool hasDriveSample;
    private readonly ClutchKickCoupling clutchKick = new ClutchKickCoupling();
    private Vector3 previousBodyVelocity, previousBodyPosition;


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        frontAxleRecovery = GetComponent<FrontAxleRecovery>();

        rb.centerOfMass = centerOfMass;

        EngineRPM = idleRPM;
        automaticClutchEngagement = manualClutchEngagement = finalClutchEngagement = 1f;
        clutchKick.Cancel();
        ClutchCouplingTorque = ClutchKickTorque = ClutchEngagementRate = 0f;
        reflectedDrivetrainInertia = 0f;
        hasDriveSample = false;
        EngineThrottle = 0f;
        previousBodyVelocity = rb.linearVelocity;
        previousBodyPosition = rb.position;
        LongitudinalAcceleration = 0f;
        DynamicsSampleValid = false;


        // Zapamätáme si normálny rear sideways grip
        rearLeftNormalStiffness =
            backLeft.sidewaysFriction.stiffness;

        rearRightNormalStiffness =
            backRight.sidewaysFriction.stiffness;


        // Stabilnejší WheelCollider simulation
        if (frontLeft != null)
        {
            frontLeft.ConfigureVehicleSubsteps(
                5f,
                12,
                15
            );
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        HandleGearInput();
    }


    // =========================================================
    // FIXED UPDATE
    // =========================================================

    void FixedUpdate()
    {
        ReadInput();

        UpdateEngineThrottle();
        UpdateLongitudinalAcceleration();

        UpdateSpeed();

        UpdateClutch();

        UpdateEngineRPM();

        HandleSteering();

        HandleMotor();

        HandleBrakes();

        UpdateWheelTelemetry();

        UpdateVisualWheels();


        if (shiftTimer > 0f)
        {
            shiftTimer -= Time.fixedDeltaTime;
        }
    }


    // =========================================================
    // INPUT
    // =========================================================

    void ReadInput()
    {
        Gamepad gamepad = Gamepad.current;
        // Square / west face button; keyboard remains usable with a pad connected.
        ClutchInput = (gamepad != null && gamepad.buttonWest.isPressed) ||
            (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ? 1f : 0f;


        // =====================================================
        // CONTROLLER / DUALSENSE
        // =====================================================

        if (gamepad != null)
        {
            float steer =
                gamepad.leftStick.x.ReadValue();


            SteeringInput =
                ApplySteeringCurve(steer);


            // R2 = throttle
            ThrottleInput =
                gamepad.rightTrigger.ReadValue();


            // L2 = brake
            BrakeInput =
                gamepad.leftTrigger.ReadValue();


            // X / Cross = handbrake
            HandbrakeInput =
                gamepad.buttonSouth.isPressed
                ? 1f
                : 0f;


            IsBraking =
                BrakeInput > 0.01f;


            return;
        }


        // =====================================================
        // KEYBOARD
        // =====================================================

        Keyboard keyboard = Keyboard.current;

        // Preserve the Horizontal axis bindings: A/D and left/right arrows.
        float rawSteering = keyboard == null ? 0f :
            ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f) -
            ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1f : 0f);


        float rawThrottle =
            keyboard != null && keyboard.wKey.isPressed
            ? 1f
            : 0f;


        // -----------------------------
        // STEERING SMOOTHING
        // -----------------------------

        float steerChangeSpeed =
            Mathf.Abs(rawSteering) > 0.01f
            ? steeringInputSpeed
            : steeringInputReturnSpeed;


        currentKeyboardSteering =
            Mathf.MoveTowards(
                currentKeyboardSteering,
                rawSteering,
                steerChangeSpeed *
                Time.fixedDeltaTime
            );


        SteeringInput =
            currentKeyboardSteering;


        // -----------------------------
        // THROTTLE SMOOTHING
        // -----------------------------

        float throttleChangeSpeed =
            rawThrottle > currentKeyboardThrottle
            ? throttleInputSpeed
            : throttleInputReturnSpeed;


        currentKeyboardThrottle =
            Mathf.MoveTowards(
                currentKeyboardThrottle,
                rawThrottle,
                throttleChangeSpeed *
                Time.fixedDeltaTime
            );


        ThrottleInput =
            currentKeyboardThrottle;


        // S = normal brake
        BrakeInput =
            keyboard != null && keyboard.sKey.isPressed
            ? 1f
            : 0f;


        // SPACE = handbrake
        HandbrakeInput =
            keyboard != null && keyboard.spaceKey.isPressed
            ? 1f
            : 0f;


        IsBraking =
            BrakeInput > 0.01f;
    }


    // =========================================================
    // CONTROLLER STEERING CURVE
    // =========================================================

    float ApplySteeringCurve(float input)
    {
        float absoluteInput =
            Mathf.Abs(input);


        if (absoluteInput <= steeringDeadzone)
        {
            return 0f;
        }


        float normalizedInput = Mathf.Clamp01(
            (absoluteInput - steeringDeadzone) /
            (1f - steeringDeadzone));

        // Shape precision near center progressively; never scale maximum lock.
        float speedBlend = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(SpeedKmh / Mathf.Max(1f, fullSteeringCurveSpeed)));
        float exponent = Mathf.Lerp(lowSpeedSteeringExponent,
            highSpeedSteeringExponent, speedBlend);


        float curvedInput =
            Mathf.Pow(
                normalizedInput,
                exponent
            );


        return Mathf.Sign(input) *
               curvedInput;
    }


    // =========================================================
    // GEARS
    // =========================================================

    void HandleGearInput()
    {
        if (shiftTimer > 0f)
            return;


        Gamepad gamepad =
            Gamepad.current;


        // =====================================================
        // CONTROLLER
        // =====================================================

        if (gamepad != null)
        {
            // R1 = shift up
            if (gamepad.rightShoulder.wasPressedThisFrame)
            {
                ShiftUp();
            }


            // L1 = shift down
            if (gamepad.leftShoulder.wasPressedThisFrame)
            {
                ShiftDown();
            }


            return;
        }


        // =====================================================
        // KEYBOARD
        // =====================================================

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            ShiftUp();
        }


        if (keyboard.qKey.wasPressedThisFrame)
        {
            ShiftDown();
        }
    }


    void ShiftUp()
    {
        if (currentGear == -1)
        {
            currentGear = 0;
        }
        else if (currentGear == 0)
        {
            currentGear = 1;
        }
        else if (currentGear < gearRatios.Length)
        {
            currentGear++;
        }
        else
        {
            return;
        }


        StartShift();
    }


    void ShiftDown()
    {
        if (currentGear > 1)
        {
            currentGear--;
        }
        else if (currentGear == 1)
        {
            currentGear = 0;
        }
        else if (currentGear == 0)
        {
            currentGear = -1;
        }
        else
        {
            return;
        }


        StartShift();
    }


    void StartShift()
    {
        shiftTimer =
            shiftDuration;
    }


    // =========================================================
    // SPEED
    // =========================================================

    void UpdateSpeed()
    {
        SpeedKmh =
            rb.linearVelocity.magnitude *
            3.6f;
    }

    void UpdateLongitudinalAcceleration()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 change = rb.linearVelocity - previousBodyVelocity;
        Vector3 positionError = rb.position - previousBodyPosition - previousBodyVelocity * dt;
        DynamicsSampleValid = change.magnitude <= 35f * dt && positionError.sqrMagnitude <= 4f;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float acceleration = DynamicsSampleValid ? Vector3.Dot(change / Mathf.Max(dt, .001f), forward) : 0f;
        LongitudinalAcceleration = DynamicsSampleValid
            ? Mathf.Lerp(LongitudinalAcceleration, Mathf.Clamp(acceleration, -20f, 20f), 1f - Mathf.Exp(-14f * dt)) : 0f;
        previousBodyVelocity = rb.linearVelocity;
        previousBodyPosition = rb.position;
    }

    void UpdateEngineThrottle()
    {
        EngineThrottle = Mathf.Lerp(EngineThrottle, ThrottleInput,
            1f - Mathf.Exp(-Mathf.Max(0.1f, engineThrottleResponse) * Time.fixedDeltaTime));
    }


    // =========================================================
    // CLUTCH
    // =========================================================

    void UpdateClutch()
    {
        float targetClutch;


        if (
            currentGear == 0 ||
            shiftTimer > 0f
        )
        {
            targetClutch = 0f;
        }

        else if (
            ThrottleInput > 0.05f &&
            SpeedKmh < launchClutchSpeed
        )
        {
            float launchFactor =
                Mathf.InverseLerp(
                    0f,
                    launchClutchSpeed,
                    SpeedKmh
                );


            targetClutch =
                Mathf.Lerp(
                    launchClutchMin,
                    1f,
                    launchFactor
                );
        }

        else
        {
            targetClutch = 1f;
        }


        float clutchSpeed =
            targetClutch > automaticClutchEngagement
            ? clutchEngageSpeed
            : clutchDisengageSpeed;


        automaticClutchEngagement =
            Mathf.MoveTowards(
                automaticClutchEngagement,
                targetClutch,
                clutchSpeed *
                Time.fixedDeltaTime
            );

        float manualTarget = 1f - Mathf.Clamp01(ClutchInput);
        float manualRate = manualTarget > manualClutchEngagement
            ? manualClutchEngageSpeed : manualClutchReleaseSpeed;
        manualClutchEngagement = Mathf.MoveTowards(manualClutchEngagement,
            manualTarget, Mathf.Max(0f, manualRate) * Time.fixedDeltaTime);
        float previousEngagement = finalClutchEngagement;
        finalClutchEngagement = automaticClutchEngagement * manualClutchEngagement;
        ClutchEngagementRate = (finalClutchEngagement - previousEngagement) / Time.fixedDeltaTime;
        if (ClutchInput > previousClutchInput + .01f) clutchKick.Cancel();

        // Require an actual disconnection, then a release. Holding or repeatedly
        // tapping a partly engaged clutch cannot continuously retrigger torque.
        if (ClutchInput > 0.5f && manualClutchEngagement <= 0.01f)
            clutchKickArmed = true;
        if (previousClutchInput > 0.5f && ClutchInput <= 0.5f)
        {
            if (clutchKickArmed && CanClutchKick() && ClutchRPMDifference > 0f)
            {
                clutchKick.Begin(ClutchEngagementRate);
                clutchKickGear = currentGear;
            }
            clutchKickArmed = false;
        }
        previousClutchInput = ClutchInput;
    }

    bool CanClutchKick() => currentGear > 0 && !IsShifting && SpeedKmh >= launchClutchSpeed &&
        ThrottleInput > 0.05f && HandbrakeInput <= 0.05f && BrakeInput <= 0.02f;


    // =========================================================
    // ENGINE RPM
    // =========================================================

    void UpdateEngineRPM()
    {
        RearWheelRPM =
            (
                Mathf.Abs(backLeft.rpm) +
                Mathf.Abs(backRight.rpm)
            ) * 0.5f;


        float effectiveThrottle =
            shiftTimer > 0f && ClutchInput <= 0.05f
            ? 0f
            : ThrottleInput;


        float freeRevTarget =
            Mathf.Lerp(
                idleRPM,
                redlineRPM,
                effectiveThrottle
            );


        SignedDrivetrainRPM = SignedRearWheelRPM * GearDirection * Mathf.Abs(GetCurrentGearRatio()) * finalDrive;
        DrivetrainRPM = Mathf.Abs(SignedDrivetrainRPM);

        // Only a disconnected engine uses the existing free-rev target. Once
        // connected, engine torque and clutch reaction in HandleMotor change RPM;
        // there is no wheel-linked RPM target or synchronization bypass.
        if (currentGear == 0 || shiftTimer > 0f || HandbrakeInput > .05f || ClutchEngagement <= .001f)
            MoveEngineRPMTowards(freeRevTarget);
    }


    void MoveEngineRPMTowards(float targetRPM)
    {
        float rpmSpeed =
            targetRPM > EngineRPM
            ? engineRevUpSpeed
            : engineRevDownSpeed;


        EngineRPM =
            Mathf.MoveTowards(
                EngineRPM,
                targetRPM,
                rpmSpeed *
                Time.fixedDeltaTime
            );


        EngineRPM =
            Mathf.Clamp(
                EngineRPM,
                idleRPM,
                redlineRPM
            );
    }


    // =========================================================
    // ENGINE TORQUE
    // =========================================================

    void HandleMotor()
    {
        UpdateDifferential();
        LimiterFactor = 1f;
        ClutchKickTorque = 0f;
        ClutchCouplingTorque = 0f;
        TorqueCurveMultiplier = Mathf.Max(0f, torqueCurve.Evaluate(EngineRPM));
        CalculatedEngineTorque = TransmittedEngineTorque = 0f;
        EngineBrakeAxleTorque = 0f;
        // Neutral / shifting
        if (
            currentGear == 0 ||
            shiftTimer > 0f
        )
        {
            clutchKick.Cancel();
            hasDriveSample = false;
            SetRearMotorTorque(0f);
            return;
        }


        // =====================================================
        // HANDBRAKE
        //
        // Toto je DÔLEŽITÉ:
        // počas handbrake motor nebojuje proti zadným brzdám.
        // =====================================================

        if (HandbrakeInput > 0.05f)
        {
            clutchKick.Cancel();
            hasDriveSample = false;
            SetRearMotorTorque(0f);
            return;
        }


        float gearRatio =
            GetCurrentGearRatio();


        float torqueMultiplier =
            TorqueCurveMultiplier;


        float engineTorque =
            maxEngineTorque *
            torqueMultiplier *
            EngineThrottle;


        // =====================================================
        // SOFT LIMITER
        // =====================================================

        float limiterStart =
            redlineRPM - 400f;


        float limiterFactor =
            1f;


        if (EngineRPM > limiterStart)
        {
            limiterFactor =
                Mathf.InverseLerp(
                    redlineRPM,
                    limiterStart,
                    EngineRPM
                );
        }


        engineTorque *=
            limiterFactor;
        LimiterFactor = limiterFactor;
        CalculatedEngineTorque = engineTorque;

        if (!CanClutchKick() || currentGear != clutchKickGear || ClutchInput > 0.5f)
            clutchKick.Cancel();
        // Preserve the normal idle floor, but never turn an opposing shaft into
        // forward overrun by taking its absolute RPM before clutch coupling.
        float clutchShaftRPM = SignedDrivetrainRPM < 0f ? SignedDrivetrainRPM : Mathf.Max(idleRPM, SignedDrivetrainRPM);
        float transmitted = clutchKick.Step(EngineRPM, clutchShaftRPM, engineTorque,
            maxEngineTorque, maxClutchTorque, clutchCouplingStrength, clutchKickStrength,
            engineInertia, ClutchEngagement, Time.fixedDeltaTime, ReflectedRearWheelInertia(gearRatio),
            EstimateDrivetrainLoad(gearRatio));
        previousDriveRPM = SignedDrivetrainRPM; previousDriveTorque = transmitted;
        previousDriveRatio = gearRatio * GearDirection; hasDriveSample = true;
        ClutchCouplingTorque = TransmittedEngineTorque = transmitted;
        ClutchKickTorque = clutchKick.Active ? Mathf.Max(0f, clutchKick.SynchronizingTorque) : 0f;
        if (ClutchEngagement > .001f)
        {
            float deltaRPM = (engineTorque - transmitted) * Time.fixedDeltaTime /
                Mathf.Max(.05f, engineInertia) * (60f / (2f * Mathf.PI));
            // Keep no-throttle free-engine drag on the uncoupled fraction only;
            // do not add a new steady engine-braking force to the baseline car.
            float freeDrag = (1f - EngineThrottle) * (1f - ClutchEngagement) *
                engineRevDownSpeed * Time.fixedDeltaTime;
            EngineRPM = Mathf.Clamp(EngineRPM + deltaRPM - freeDrag, idleRPM, redlineRPM);
        }


        // =====================================================
        // DRIVETRAIN
        // =====================================================

        float torqueScale = Mathf.Abs(gearRatio) * finalDrive * drivetrainEfficiency;
        // Negative clutch torque is passive wheel-to-engine overrun, not reverse
        // engine power. Wheel brakes oppose rotation without driving through zero.
        EngineBrakeAxleTorque = Mathf.Max(0f, -transmitted) * torqueScale;
        float wheelTorque = Mathf.Max(0f, transmitted) * GearDirection * torqueScale;


        SetRearMotorTorque(
            wheelTorque
        );
    }


    void SetRearMotorTorque(float axleTorque)
    {
        backLeft.motorTorque = axleTorque * rearLeftTorqueShare;
        backRight.motorTorque = axleTorque * (1f - rearLeftTorqueShare);
    }

    float ReflectedRearWheelInertia(float gearRatio)
    {
        // Read-only inertia estimate: gripping tires also accelerate the chassis;
        // freely spinning tires mostly accelerate the wheels. Account for that
        // transition instead of applying an engine-only synchronization impulse.
        float axleInertia = .5f * (backLeft.mass * backLeft.radius * backLeft.radius +
            backRight.mass * backRight.radius * backRight.radius);
        float radiusSquared = .5f * (backLeft.radius * backLeft.radius + backRight.radius * backRight.radius);
        float grip = .5f * (LongitudinalCoupling(backLeft) + LongitudinalCoupling(backRight));
        axleInertia += rb.mass * radiusSquared * grip * grip;
        float ratio = Mathf.Abs(gearRatio * finalDrive);
        float target = axleInertia / Mathf.Max(.001f, ratio * ratio * Mathf.Max(.01f, drivetrainEfficiency));
        // Once a release breaks traction, do not reinterpret a single low-slip
        // sample as a suddenly rigid chassis load and fire a second torque hit.
        // Rebuild the load smoothly after the release interval has finished.
        if (reflectedDrivetrainInertia <= 0f || target < reflectedDrivetrainInertia)
            reflectedDrivetrainInertia = target;
        else if (!clutchKick.Active)
            reflectedDrivetrainInertia = Mathf.Lerp(reflectedDrivetrainInertia, target,
                1f - Mathf.Exp(-6f * Time.fixedDeltaTime));
        return reflectedDrivetrainInertia;
    }

    static float LongitudinalCoupling(WheelCollider wheel) => wheel.GetGroundHit(out WheelHit hit)
        ? 1f - Mathf.Clamp01(Mathf.Abs(hit.forwardSlip) / Mathf.Max(.01f, wheel.forwardFriction.extremumSlip))
        : 0f;

    float EstimateDrivetrainLoad(float gearRatio)
    {
        if (!hasDriveSample || !Mathf.Approximately(previousDriveRatio, gearRatio * GearDirection)) return 0f;
        // Infer tire/road resistance from the last applied torque and the actual
        // wheel acceleration, rather than assuming an unloaded spinning wheel.
        float inertia = .5f * (backLeft.mass * backLeft.radius * backLeft.radius +
            backRight.mass * backRight.radius * backRight.radius);
        float ratio = Mathf.Abs(gearRatio * finalDrive);
        inertia /= Mathf.Max(.001f, ratio * ratio * Mathf.Max(.01f, drivetrainEfficiency));
        float acceleration = (SignedDrivetrainRPM - previousDriveRPM) * (2f * Mathf.PI / 60f) / Time.fixedDeltaTime;
        return Mathf.Clamp(previousDriveTorque - inertia * acceleration, -maxClutchTorque, maxClutchTorque);
    }

    void UpdateDifferential()
    {
        bool leftGrounded = backLeft.GetGroundHit(out WheelHit leftHit);
        bool rightGrounded = backRight.GetGroundHit(out WheelHit rightHit);
        float target = differentialMode == DifferentialMode.Open ? 0.5f :
            RearTraction.TargetLeftShare(leftGrounded, rightGrounded,
                leftHit.forwardSlip, rightHit.forwardSlip, lsdStrength);
        rearLeftTorqueShare = differentialMode == DifferentialMode.Open ? 0.5f :
            RearTraction.Follow(rearLeftTorqueShare, target, differentialResponse, Time.fixedDeltaTime);
    }


    float GetCurrentGearRatio()
    {
        if (currentGear == -1)
        {
            return reverseRatio;
        }


        if (currentGear <= 0)
        {
            return 0f;
        }


        int index =
            Mathf.Clamp(
                currentGear - 1,
                0,
                gearRatios.Length - 1
            );


        return gearRatios[index];
    }


    // =========================================================
    // STEERING
    // =========================================================

    void HandleSteering()
    {
        float targetSteerAngle =
            SteeringInput *
            maxSteerAngle;


        float rate =
            Mathf.Abs(SteeringInput) > 0.01f && SteeringInput * currentSteerAngle >= 0f
            ? steeringSpeed
            : steeringReturnSpeed;

        // Opposite input crosses center at the configured return rate.


        currentSteerAngle =
            Mathf.MoveTowards(
                currentSteerAngle,
                targetSteerAngle,
                rate *
                Time.fixedDeltaTime
            );


        UpdateSteeringGeometry();
        float leftAngle = currentSteerAngle, rightAngle = currentSteerAngle;
        if (useAckermannSteering)
            AckermannSteering.Calculate(currentSteerAngle, SteeringWheelbase, SteeringFrontTrack,
                out leftAngle, out rightAngle);
        frontLeft.steerAngle = leftAngle;
        frontRight.steerAngle = rightAngle;


        SteeringAngle =
            currentSteerAngle;
    }

    void UpdateSteeringGeometry()
    {
        Vector3 fl = frontLeft.transform.TransformPoint(frontLeft.center);
        Vector3 fr = frontRight.transform.TransformPoint(frontRight.center);
        Vector3 rear = (backLeft.transform.TransformPoint(backLeft.center) +
            backRight.transform.TransformPoint(backRight.center)) * 0.5f;
        SteeringWheelbase = Mathf.Abs(Vector3.Dot((fl + fr) * 0.5f - rear, transform.forward));
        SteeringFrontTrack = Mathf.Abs(Vector3.Dot(fr - fl, transform.right));
    }


    // =========================================================
    // BRAKES + HANDBRAKE
    // =========================================================

    void HandleBrakes()
    {
        float normalBrake =
            BrakeInput *
            brakeTorque;


        float rearHandbrake =
            HandbrakeInput *
            handbrakeTorque;


        // =====================================================
        // NORMAL BRAKE
        // =====================================================

        frontLeft.brakeTorque =
            normalBrake;

        frontRight.brakeTorque =
            normalBrake;


        // =====================================================
        // REAR + HANDBRAKE
        // =====================================================

        backLeft.brakeTorque =
            normalBrake + rearHandbrake + EngineBrakeTorque(backLeft, rearLeftTorqueShare);

        backRight.brakeTorque =
            normalBrake + rearHandbrake + EngineBrakeTorque(backRight, 1f - rearLeftTorqueShare);


        // =====================================================
        // HANDBRAKE REAR GRIP
        // =====================================================

        UpdateSlipAngle();
        UpdateDriftGripState();
        float targetGripMultiplier = Mathf.Lerp(1f, handbrakeRearGrip, HandbrakeInput);


        float gripChangeSpeed =
            targetGripMultiplier <
            currentHandbrakeGripMultiplier

            ? handbrakeGripDropSpeed
            : handbrakeGripRecoverySpeed;


        currentHandbrakeGripMultiplier =
            Mathf.MoveTowards(
                currentHandbrakeGripMultiplier,
                targetGripMultiplier,
                gripChangeSpeed *
                Time.fixedDeltaTime
            );


        // Keep handbrake grip independent of powered-drift recovery.
        leftPowerGrip = UpdatePowerGrip(backLeft, leftPowerGrip);
        rightPowerGrip = UpdatePowerGrip(backRight, rightPowerGrip);
        float driftHandoff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(breakawayHandoffStartAngle,
            Mathf.Max(breakawayHandoffStartAngle + 0.01f, breakawayHandoffFullAngle), Mathf.Abs(VehicleSlipAngle)));
        float speedGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 30f, SpeedKmh));
        float steeringGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.16f, Mathf.Abs(SteeringInput)));
        CombinedSlipEntryWeight = enableCombinedSlip && currentGear > 0 && !IsShifting &&
            ClutchEngagement > 0.01f && BrakeInput <= 0.02f && HandbrakeInput <= 0.05f
            ? speedGate * steeringGate * (1f - driftHandoff) : 0f;
        RearLeftCombinedSlipMultiplier = UpdateCombinedSlip(backLeft, RearLeftCombinedSlipMultiplier);
        RearRightCombinedSlipMultiplier = UpdateCombinedSlip(backRight, RearRightCombinedSlipMultiplier);
        float leftTarget = currentHandbrakeGripMultiplier * Mathf.Lerp(leftPowerGrip, RearLeftCombinedSlipMultiplier, CombinedSlipEntryWeight);
        float rightTarget = currentHandbrakeGripMultiplier * Mathf.Lerp(rightPowerGrip, RearRightCombinedSlipMultiplier, CombinedSlipEntryWeight);
        if (HandbrakeInput > .05f)
        {
            leftTarget = Mathf.Min(leftTarget, leftRecoveredGrip);
            rightTarget = Mathf.Min(rightTarget, rightRecoveredGrip);
        }
        // Smooth the actual final multiplier, including released handbrake grip.
        // Applying the handbrake still follows the original drop/locking path.
        if (HandbrakeInput > .05f || SpeedKmh < 1f || SignedLongitudinalSpeedKmh < -2f)
            driftReleaseRecovery = false;
        else if (DriftGripHeld && currentHandbrakeGripMultiplier < .999f)
            driftReleaseRecovery = true;
        if (currentHandbrakeGripMultiplier >= .999f && leftRecoveredGrip >= .999f && rightRecoveredGrip >= .999f)
            driftReleaseRecovery = false;
        bool normalGrip = HandbrakeInput <= .05f && (currentHandbrakeGripMultiplier >= .999f || driftReleaseRecovery);
        if (SpeedKmh < 1f && HandbrakeInput <= .05f)
        { leftRecoveredGrip = leftTarget; rightRecoveredGrip = rightTarget; }
        float oldAverage = (leftRecoveredGrip + rightRecoveredGrip) * .5f;
        leftRecoveredGrip = RecoverRearGrip(leftRecoveredGrip, leftTarget, normalGrip);
        rightRecoveredGrip = RecoverRearGrip(rightRecoveredGrip, rightTarget, normalGrip);
        currentRearGripRecovery = normalGrip ? Mathf.Max(0f,
            ((leftRecoveredGrip + rightRecoveredGrip) * .5f - oldAverage) / Time.fixedDeltaTime) : 0f;
        RearLeftGripMultiplier = leftRecoveredGrip;
        RearRightGripMultiplier = rightRecoveredGrip;
        SetSidewaysStiffness(backLeft, rearLeftNormalStiffness * RearLeftGripMultiplier);
        SetSidewaysStiffness(backRight, rearRightNormalStiffness * RearRightGripMultiplier);
    }

    void UpdateSlipAngle()
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        VehicleSlipAngle = horizontal.sqrMagnitude > 1f
            ? Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), horizontal, Vector3.up) : 0f;
    }

    float RecoverRearGrip(float current, float target, bool smoothRecovery)
    {
        float deliberateRecovery = frontAxleRecovery != null && frontAxleRecovery.isActiveAndEnabled
            ? frontAxleRecovery.DeliberateRecoveryWeight : 0f;
        float ordinaryResponse = frontAxleRecovery != null && frontAxleRecovery.isActiveAndEnabled && frontAxleRecovery.IsControlledDrift
            ? controlledDriftGripRecoveryResponse : rearGripRecoveryResponse;
        float response = Mathf.Lerp(ordinaryResponse, countersteerGripRecoveryResponse, deliberateRecovery);
        return !smoothRecovery || target <= current ? target :
            RearTraction.Follow(current, target, response, Time.fixedDeltaTime);
    }

    float EngineBrakeTorque(WheelCollider wheel, float share) =>
        wheel.rpm * GearDirection > 0f ? EngineBrakeAxleTorque * share : 0f;

    void UpdateDriftGripState()
    {
        // Handling-only hysteresis; DriftTelemetry/score detection is unchanged.
        float threshold = DriftGripHeld ? Mathf.Min(driftGripExitAngle, driftGripEnterAngle) : driftGripEnterAngle;
        driftGripHeld = SpeedKmh >= 28f && currentGear > 0 && HandbrakeInput <= .05f &&
            BrakeInput <= .02f &&
            Vector3.Dot(rb.linearVelocity, transform.forward) > 4f &&
            backLeft.isGrounded && backRight.isGrounded && Mathf.Abs(VehicleSlipAngle) >= threshold;
    }

    float UpdateCombinedSlip(WheelCollider wheel, float current)
    {
        float target = 1f;
        if (enableCombinedSlip && currentGear > 0 && !IsShifting && ClutchEngagement > 0.01f &&
            BrakeInput <= 0.02f && HandbrakeInput <= 0.05f && wheel.GetGroundHit(out WheelHit hit))
            target = RearTraction.CombinedSlip(hit.forwardSlip, ThrottleInput, hit.force,
                rb.mass * Physics.gravity.magnitude * 0.25f, combinedSlipStart, combinedSlipFull, minimumCombinedSlipMultiplier);
        return RearTraction.Follow(current, target, combinedSlipResponse, Time.fixedDeltaTime);
    }

    float UpdatePowerGrip(WheelCollider wheel, float current)
    {
        float target = 1f;
        if (currentGear > 0 && !IsShifting && HandbrakeInput <= 0.05f &&
            BrakeInput <= 0.02f && wheel.GetGroundHit(out WheelHit hit))
        {
            float referenceLoad = rb.mass * Physics.gravity.magnitude * 0.25f;
            // Powered WheelHit slip can have either sign. Use magnitude only in
            // the latched drift, retaining the existing normal-driving model.
            float slip = DriftGripHeld && currentHandbrakeGripMultiplier >= .999f ? -Mathf.Abs(hit.forwardSlip) : hit.forwardSlip;
            target = RearTraction.PowerGrip(slip, hit.force, referenceLoad,
                ThrottleInput, wheel.forwardFriction.extremumSlip, powerSlipGripReduction);
        }
        return RearTraction.Follow(current, target, powerSlipGripResponse, Time.fixedDeltaTime);
    }


    void SetSidewaysStiffness(
        WheelCollider wheel,
        float stiffness
    )
    {
        WheelFrictionCurve friction =
            wheel.sidewaysFriction;


        friction.stiffness =
            stiffness;


        wheel.sidewaysFriction =
            friction;
    }


    // =========================================================
    // WHEEL SLIP TELEMETRY
    // =========================================================

    void UpdateWheelTelemetry()
    {
        float leftSlip =
            GetForwardSlip(backLeft);


        float rightSlip =
            GetForwardSlip(backRight);


        RearForwardSlip =
            (
                Mathf.Abs(leftSlip) +
                Mathf.Abs(rightSlip)
            ) * 0.5f;
    }


    float GetForwardSlip(
        WheelCollider wheel
    )
    {
        if (wheel == null)
            return 0f;


        if (
            wheel.GetGroundHit(
                out WheelHit hit
            )
        )
        {
            return hit.forwardSlip;
        }


        return 0f;
    }


    // =========================================================
    // VISUAL WHEELS
    // =========================================================

    void UpdateVisualWheels()
    {
        UpdateWheel(
            frontLeft,
            frontLeftWheel
        );

        UpdateWheel(
            frontRight,
            frontRightWheel
        );

        UpdateWheel(
            backLeft,
            backLeftWheel
        );

        UpdateWheel(
            backRight,
            backRightWheel
        );
    }


    void UpdateWheel(
        WheelCollider collider,
        Transform wheel
    )
    {
        if (
            collider == null ||
            wheel == null
        )
        {
            return;
        }


        collider.GetWorldPose(
            out Vector3 position,
            out Quaternion rotation
        );


        wheel.position =
            position;

        wheel.rotation =
            rotation;
    }
}
