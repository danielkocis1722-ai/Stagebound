using UnityEngine;
using UnityEngine.InputSystem;

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

    public float maxEngineTorque = 280f;

    public float idleRPM = 900f;
    public float redlineRPM = 7500f;

    public float engineRevUpSpeed = 9000f;
    public float engineRevDownSpeed = 5000f;

    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(900f, 0.50f),
        new Keyframe(2500f, 0.75f),
        new Keyframe(4500f, 1.00f),
        new Keyframe(6000f, 0.90f),
        new Keyframe(7500f, 0.60f)
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
    // 0.55 = zostane 55 % normálneho sideways stiffness.
    [Range(0.1f, 1f)]
    public float handbrakeRearGrip = 0.55f;

    // Ako rýchlo zadok stratí grip po stlačení X.
    public float handbrakeGripDropSpeed = 12f;

    // Ako rýchlo sa grip vráti po pustení.
    // Nižšia hodnota = menej snapu späť.
    public float handbrakeGripRecoverySpeed = 3.5f;


    // =========================================================
    // STEERING
    // =========================================================

    [Header("Steering")]

    public float maxSteerAngle = 34f;

    public float steeringSpeed = 95f;
    public float steeringReturnSpeed = 75f;


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

    public float steeringDeadzone = 0.08f;

    // Jemnejší stred analog sticku.
    // 1 = linear
    // 1.7 = jemné korekcie, plný stick stále = plný lock
    public float steeringExponent = 1.7f;


    // =========================================================
    // CAR PHYSICS
    // =========================================================

    [Header("Car Physics")]

    public Vector3 centerOfMass =
        new Vector3(0f, -0.55f, 0f);


    // =========================================================
    // RUNTIME TELEMETRY
    // =========================================================

    [Header("Runtime Telemetry")]

    public float SpeedKmh;
    public float EngineRPM;

    [Range(0f, 1f)]
    public float ClutchEngagement;

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

    private float currentKeyboardSteering;
    private float currentKeyboardThrottle;

    private float shiftTimer;

    private float rearLeftNormalStiffness;
    private float rearRightNormalStiffness;

    private float currentHandbrakeGripMultiplier = 1f;


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.centerOfMass = centerOfMass;

        EngineRPM = idleRPM;
        ClutchEngagement = 1f;


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

        float rawSteering =
            Input.GetAxisRaw("Horizontal");


        float rawThrottle =
            Input.GetKey(KeyCode.W)
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
            Input.GetKey(KeyCode.S)
            ? 1f
            : 0f;


        // SPACE = handbrake
        HandbrakeInput =
            Input.GetKey(KeyCode.Space)
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


        float normalizedInput =
            (absoluteInput - steeringDeadzone) /
            (1f - steeringDeadzone);


        float curvedInput =
            Mathf.Pow(
                normalizedInput,
                steeringExponent
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

        if (Input.GetKeyDown(KeyCode.E))
        {
            ShiftUp();
        }


        if (Input.GetKeyDown(KeyCode.Q))
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
            targetClutch > ClutchEngagement
            ? clutchEngageSpeed
            : clutchDisengageSpeed;


        ClutchEngagement =
            Mathf.MoveTowards(
                ClutchEngagement,
                targetClutch,
                clutchSpeed *
                Time.fixedDeltaTime
            );
    }


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
            shiftTimer > 0f
            ? 0f
            : ThrottleInput;


        float freeRevTarget =
            Mathf.Lerp(
                idleRPM,
                redlineRPM,
                effectiveThrottle
            );


        if (currentGear == 0)
        {
            MoveEngineRPMTowards(
                freeRevTarget
            );

            return;
        }


        float gearRatio =
            GetCurrentGearRatio();


        float wheelLinkedRPM =
            RearWheelRPM *
            Mathf.Abs(gearRatio) *
            finalDrive;


        wheelLinkedRPM =
            Mathf.Max(
                wheelLinkedRPM,
                idleRPM
            );


        float targetRPM =
            Mathf.Lerp(
                freeRevTarget,
                wheelLinkedRPM,
                ClutchEngagement
            );


        targetRPM =
            Mathf.Clamp(
                targetRPM,
                idleRPM,
                redlineRPM
            );


        MoveEngineRPMTowards(
            targetRPM
        );
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
        // Neutral / shifting
        if (
            currentGear == 0 ||
            shiftTimer > 0f
        )
        {
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
            SetRearMotorTorque(0f);
            return;
        }


        float gearRatio =
            GetCurrentGearRatio();


        float torqueMultiplier =
            torqueCurve.Evaluate(
                EngineRPM
            );


        float engineTorque =
            maxEngineTorque *
            torqueMultiplier *
            ThrottleInput;


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


        // =====================================================
        // DRIVETRAIN
        // =====================================================

        float wheelTorque =
            engineTorque *
            Mathf.Abs(gearRatio) *
            finalDrive *
            drivetrainEfficiency *
            ClutchEngagement;


        if (currentGear == -1)
        {
            wheelTorque *= -1f;
        }


        float torquePerWheel =
            wheelTorque *
            0.5f;


        SetRearMotorTorque(
            torquePerWheel
        );
    }


    void SetRearMotorTorque(float torque)
    {
        backLeft.motorTorque =
            torque;

        backRight.motorTorque =
            torque;
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
            Mathf.Abs(SteeringInput) > 0.01f
            ? steeringSpeed
            : steeringReturnSpeed;


        currentSteerAngle =
            Mathf.MoveTowards(
                currentSteerAngle,
                targetSteerAngle,
                rate *
                Time.fixedDeltaTime
            );


        frontLeft.steerAngle =
            currentSteerAngle;

        frontRight.steerAngle =
            currentSteerAngle;


        SteeringAngle =
            currentSteerAngle;
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
            normalBrake +
            rearHandbrake;

        backRight.brakeTorque =
            normalBrake +
            rearHandbrake;


        // =====================================================
        // HANDBRAKE REAR GRIP
        // =====================================================

        float targetGripMultiplier =
            HandbrakeInput > 0.05f
            ? handbrakeRearGrip
            : 1f;


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


        SetSidewaysStiffness(
            backLeft,
            rearLeftNormalStiffness *
            currentHandbrakeGripMultiplier
        );


        SetSidewaysStiffness(
            backRight,
            rearRightNormalStiffness *
            currentHandbrakeGripMultiplier
        );
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