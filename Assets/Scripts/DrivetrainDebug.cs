using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(CarController))]
public class DrivetrainDebug : MonoBehaviour
{
    [Tooltip("Enable to inspect the live fields below. Does not draw over the race HUD.")]
    public bool captureTelemetry;
    public bool logToConsole;
    [Tooltip("Log every physics sample for short clutch tests. Leave off during normal driving.")]
    public bool logClutchTrace;
    [Min(0.1f)]
    public float logInterval = 0.5f;
    [Min(0.01f)]
    public float frontReferenceStiffness = 1.2f;

    [Header("Live drivetrain (Play Mode, capture enabled)")]
    [SerializeField]
    private float speedKmh, throttle, engineRPM;
    [SerializeField]
    private float signedLongitudinalSpeedKmh, blWheelRPM, brWheelRPM, signedDrivetrainRPM;
    [SerializeField]
    private int gearDirection, finalMotorTorqueDirection;
    [SerializeField]
    private float engineBrakeAxleTorque;
    [SerializeField]
    private float recoveryYawDamping, deliberateRecoveryWeight, currentRearGripRecovery;
    [SerializeField]
    private bool deliberateRecoveryActive;
    [SerializeField]
    private bool isDrifting, controlledDrift, handlingDriftLatched, driftReleaseRecovery;
    [SerializeField]
    private float sharedOverRotationRisk;
    [SerializeField]
    private float engineThrottle, normalizedRPM, torqueCurveMultiplier;
    [SerializeField]
    private float calculatedEngineTorque, transmittedEngineTorque, longitudinalAcceleration;
    [SerializeField]
    private float speedAcceleration, linearDamping, angularDamping;
    [SerializeField]
    private float rearForwardAsymptoteValue, rearSidewaysAsymptoteSlip, rearSidewaysAsymptoteValue;
    [SerializeField]
    private int gear;
    [SerializeField]
    private float blMotorTorque, brMotorTorque, deliveredAxleTorque, leftTorqueShare;
    [SerializeField]
    private bool blGrounded, brGrounded;
    [SerializeField]
    private float blForwardSlip, brForwardSlip, blSidewaysSlip, brSidewaysSlip;
    [SerializeField]
    private float blContactLoad, brContactLoad;
    [SerializeField]
    private float flSidewaysSlip, frSidewaysSlip, flContactLoad, frContactLoad;
    [SerializeField]
    private float flForwardSlip, frForwardSlip, flSteerAngle, frSteerAngle;
    [SerializeField]
    private float steeringWheelbase, steeringFrontTrack;
    [SerializeField]
    private bool flGrounded, frGrounded;
    [SerializeField]
    private float flNormalUpDot, frNormalUpDot;
    [SerializeField]
    private float frontLateralStiffnessMultiplier;
    [SerializeField]
    private float frontRecoverySlipAngle, frontRecoveryMultiplier, recoverySteeringResponse;
    [SerializeField]
    private bool countersteerRecoveryActive;
    [SerializeField]
    private float blCombinedSlipMultiplier, brCombinedSlipMultiplier, combinedSlipEntryWeight;
    [SerializeField]
    private float steeringInput, yawRate, vehicleSlipAngle, travelTurnRate, turnRadiusMeters;
    [SerializeField]
    private float blRearFrictionMultiplier, brRearFrictionMultiplier;
    [SerializeField]
    private float handbrakeInput, handbrakeGripMultiplier, clutchEngagement, limiterFactor;
    [SerializeField]
    private float clutchInput, automaticClutchEngagement, manualClutchEngagement;
    [SerializeField]
    private float drivetrainRPM, clutchRPMDifference, clutchKickTorque;
    [SerializeField]
    private float clutchEngagementRate, clutchCouplingTorque;
    [SerializeField]
    private bool clutchKickActive;
    [SerializeField]
    private bool shifting;
    private CarController car;
    private float nextLogTime;
    private Rigidbody body;
    private FrontAxleRecovery frontRecovery;
    private DriftYawStability yawRecovery;
    private DriftTelemetry driftTelemetry;
    private Vector3 previousTravelDirection;
    private float previousSpeed;
    private bool hasSpeedSample;

    private void Awake()
    {
        car = GetComponent<CarController>();
        body = GetComponent<Rigidbody>();
        frontRecovery = GetComponent<FrontAxleRecovery>();
        yawRecovery = GetComponent<DriftYawStability>();
        driftTelemetry = GetComponent<DriftTelemetry>();
    }

    private void FixedUpdate()
    {
        if ((!captureTelemetry && !logClutchTrace) || !car.enabled)
        {
            hasSpeedSample = false;
            previousTravelDirection = Vector3.zero;
            return;
        }

        speedKmh = car.SpeedKmh;
        throttle = car.ThrottleInput;
        signedLongitudinalSpeedKmh = car.SignedLongitudinalSpeedKmh;
        blWheelRPM = car.backLeft.rpm;
        brWheelRPM = car.backRight.rpm;
        signedDrivetrainRPM = car.SignedDrivetrainRPM;
        gearDirection = car.GearDirection;
        engineBrakeAxleTorque = car.EngineBrakeAxleTorque;
        finalMotorTorqueDirection = car.DeliveredAxleTorque > .01f ? 1 : car.DeliveredAxleTorque < -.01f ? -1 : 0;
        recoveryYawDamping = yawRecovery != null ? yawRecovery.RecoveryYawDamping : 0f;
        deliberateRecoveryActive = frontRecovery != null && frontRecovery.RecoveryAttemptActive;
        deliberateRecoveryWeight = frontRecovery != null ? frontRecovery.DeliberateRecoveryWeight : 0f;
        currentRearGripRecovery = car.CurrentRearGripRecovery;
        isDrifting = driftTelemetry != null && driftTelemetry.IsDrifting;
        controlledDrift = frontRecovery != null && frontRecovery.IsControlledDrift;
        handlingDriftLatched = car.DriftGripHeld;
        driftReleaseRecovery = car.DriftReleaseRecoveryActive;
        sharedOverRotationRisk = frontRecovery != null ? frontRecovery.OverRotationWeight : 0f;
        engineRPM = car.EngineRPM;
        gear = car.currentGear;
        engineThrottle = car.EngineThrottle;
        normalizedRPM = car.NormalizedEngineRPM;
        torqueCurveMultiplier = car.TorqueCurveMultiplier;
        calculatedEngineTorque = car.CalculatedEngineTorque;
        transmittedEngineTorque = car.TransmittedEngineTorque;
        longitudinalAcceleration = car.LongitudinalAcceleration;
        float actualSpeed = body.linearVelocity.magnitude;
        speedAcceleration = hasSpeedSample && car.DynamicsSampleValid ? (actualSpeed - previousSpeed) / Time.fixedDeltaTime : 0f;
        previousSpeed = actualSpeed;
        hasSpeedSample = true;
        linearDamping = body.linearDamping;
        angularDamping = body.angularDamping;
        rearForwardAsymptoteValue = car.backLeft.forwardFriction.asymptoteValue;
        rearSidewaysAsymptoteSlip = car.backLeft.sidewaysFriction.asymptoteSlip;
        rearSidewaysAsymptoteValue = car.backLeft.sidewaysFriction.asymptoteValue;
        blMotorTorque = car.backLeft.motorTorque;
        brMotorTorque = car.backRight.motorTorque;
        deliveredAxleTorque = car.DeliveredAxleTorque;
        leftTorqueShare = car.RearLeftTorqueShare;
        blGrounded = car.backLeft.GetGroundHit(out WheelHit left);
        brGrounded = car.backRight.GetGroundHit(out WheelHit right);
        blForwardSlip = blGrounded ? left.forwardSlip : 0f;
        brForwardSlip = brGrounded ? right.forwardSlip : 0f;
        blSidewaysSlip = blGrounded ? left.sidewaysSlip : 0f;
        brSidewaysSlip = brGrounded ? right.sidewaysSlip : 0f;
        blContactLoad = blGrounded ? left.force : 0f;
        brContactLoad = brGrounded ? right.force : 0f;
        flGrounded = car.frontLeft.GetGroundHit(out WheelHit fl);
        frGrounded = car.frontRight.GetGroundHit(out WheelHit fr);
        flSidewaysSlip = flGrounded ? fl.sidewaysSlip : 0f;
        frSidewaysSlip = frGrounded ? fr.sidewaysSlip : 0f;
        flForwardSlip = flGrounded ? fl.forwardSlip : 0f;
        frForwardSlip = frGrounded ? fr.forwardSlip : 0f;
        flSteerAngle = car.frontLeft.steerAngle;
        frSteerAngle = car.frontRight.steerAngle;
        steeringWheelbase = car.SteeringWheelbase;
        steeringFrontTrack = car.SteeringFrontTrack;
        flContactLoad = flGrounded ? fl.force : 0f;
        frContactLoad = frGrounded ? fr.force : 0f;
        flNormalUpDot = flGrounded ? Vector3.Dot(fl.normal, Vector3.up) : 0f;
        frNormalUpDot = frGrounded ? Vector3.Dot(fr.normal, Vector3.up) : 0f;
        frontLateralStiffnessMultiplier = (car.frontLeft.sidewaysFriction.stiffness + car.frontRight.sidewaysFriction.stiffness) / Mathf.Max(.001f, 2f * frontReferenceStiffness);
        frontRecoverySlipAngle = frontRecovery != null ? frontRecovery.VehicleSlipAngle : -car.VehicleSlipAngle;
        frontRecoveryMultiplier = frontRecovery != null ? frontRecovery.FrontLateralMultiplier : 1f;
        recoverySteeringResponse = frontRecovery != null ? frontRecovery.SteeringResponseMultiplier : 1f;
        countersteerRecoveryActive = frontRecovery != null && frontRecovery.CountersteerRecoveryActive;
        blCombinedSlipMultiplier = car.RearLeftCombinedSlipMultiplier;
        brCombinedSlipMultiplier = car.RearRightCombinedSlipMultiplier;
        combinedSlipEntryWeight = car.CombinedSlipEntryWeight;
        steeringInput = car.SteeringInput;
        yawRate = car.YawRate;
        vehicleSlipAngle = car.VehicleSlipAngle;
        Vector3 travel = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        travelTurnRate = travel.sqrMagnitude > 1f && previousTravelDirection.sqrMagnitude > .5f ? Vector3.SignedAngle(previousTravelDirection, travel, Vector3.up) / Time.fixedDeltaTime : 0f;
        previousTravelDirection = travel.sqrMagnitude > 1f ? travel.normalized : Vector3.zero;
        turnRadiusMeters = Mathf.Abs(travelTurnRate) > .1f ? travel.magnitude / (Mathf.Abs(travelTurnRate) * Mathf.Deg2Rad) : float.PositiveInfinity;
        blRearFrictionMultiplier = car.RearLeftGripMultiplier;
        brRearFrictionMultiplier = car.RearRightGripMultiplier;
        handbrakeInput = car.HandbrakeInput;
        handbrakeGripMultiplier = car.HandbrakeGripMultiplier;
        clutchEngagement = car.ClutchEngagement;
        limiterFactor = car.LimiterFactor;
        clutchInput = car.ClutchInput;
        automaticClutchEngagement = car.AutomaticClutchEngagement;
        manualClutchEngagement = car.ManualClutchEngagement;
        drivetrainRPM = car.DrivetrainRPM;
        clutchRPMDifference = car.ClutchRPMDifference;
        clutchKickActive = car.ClutchKickActive;
        clutchKickTorque = car.ClutchKickTorque;
        clutchEngagementRate = car.ClutchEngagementRate;
        clutchCouplingTorque = car.ClutchCouplingTorque;
        shifting = car.IsShifting;
        if (logClutchTrace)
            Debug.Log($"Clutch: engineRPM={engineRPM:F1} driveRPM={drivetrainRPM:F1} deltaRPM={clutchRPMDifference:F1} " + $"input={clutchInput:F2} engagement={clutchEngagement:F3} rate={clutchEngagementRate:F2}/s " + $"engineTorque={calculatedEngineTorque:F1}Nm coupling={clutchCouplingTorque:F1}Nm " + $"axle={deliveredAxleTorque:F1}Nm BL/BR={blMotorTorque:F1}/{brMotorTorque:F1}Nm " + $"forwardSlip={blForwardSlip:F4}/{brForwardSlip:F4} speed={speedKmh:F1} gear={gear} kick={clutchKickActive}", this);
        if (!logToConsole || Time.unscaledTime < nextLogTime)
            return;
        nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logInterval);
        Debug.Log($"Drivetrain: {speedKmh:F1} km/h throttle={throttle:F2} RPM={engineRPM:F0} gear={gear} " + $"engineThrottle={engineThrottle:F3} normalizedRPM={normalizedRPM:F3} curve={torqueCurveMultiplier:F3} " + $"engineTorque={calculatedEngineTorque:F1} transmitted={transmittedEngineTorque:F1} accel={longitudinalAcceleration:F2}m/s2 " + $"speedAccel={speedAcceleration:F2}m/s2 damping={linearDamping:F3}/{angularDamping:F3} " + $"rearAsymForward={rearForwardAsymptoteValue:F3} rearAsymSide={rearSidewaysAsymptoteSlip:F3}/{rearSidewaysAsymptoteValue:F3} " + $"torque BL/BR={blMotorTorque:F0}/{brMotorTorque:F0} axle={deliveredAxleTorque:F0} leftShare={leftTorqueShare:F3} " + $"grounded={blGrounded}/{brGrounded} forwardSlip={blForwardSlip:F3}/{brForwardSlip:F3} " + $"sidewaysSlip={blSidewaysSlip:F3}/{brSidewaysSlip:F3} load={blContactLoad:F0}/{brContactLoad:F0} " + $"frontSide={flSidewaysSlip:F3}/{frSidewaysSlip:F3} frontLoad={flContactLoad:F0}/{frContactLoad:F0} " + $"frontForward={flForwardSlip:F3}/{frForwardSlip:F3} frontSteer={flSteerAngle:F2}/{frSteerAngle:F2} " + $"wheelbase={steeringWheelbase:F3} frontTrack={steeringFrontTrack:F3} " + $"frontGround={flGrounded}/{frGrounded} normalUp={flNormalUpDot:F2}/{frNormalUpDot:F2} frontStiffness={frontLateralStiffnessMultiplier:F3} " + $"recoveryAngle={frontRecoverySlipAngle:F1} frontRecovery={frontRecoveryMultiplier:F3} active={countersteerRecoveryActive} steerResponse={recoverySteeringResponse:F3} " + $"combined={blCombinedSlipMultiplier:F3}/{brCombinedSlipMultiplier:F3} entryWeight={combinedSlipEntryWeight:F3} " + $"steer={steeringInput:F3} yaw={yawRate:F1} bodySlip={vehicleSlipAngle:F1} travelTurn={travelTurnRate:F1} radius={turnRadiusMeters:F1} " + $"grip={blRearFrictionMultiplier:F3}/{brRearFrictionMultiplier:F3} " + $"HB={handbrakeInput:F2} HBgrip={handbrakeGripMultiplier:F3} clutch={clutchEngagement:F2} " + $"clutchInput={clutchInput:F2} auto/manual={automaticClutchEngagement:F2}/{manualClutchEngagement:F2} " + $"engagementRate={clutchEngagementRate:F2}/s couplingTorque={clutchCouplingTorque:F1}Nm " + $"driveRPM={drivetrainRPM:F0} RPMdifference={clutchRPMDifference:F0} " + $"kick={clutchKickActive} kickTorque={clutchKickTorque:F1} " + $"signedSpeed={signedLongitudinalSpeedKmh:F2} gearDirection={gearDirection} wheelRPM={blWheelRPM:F1}/{brWheelRPM:F1} " + $"signedDriveRPM={signedDrivetrainRPM:F1} motorDirection={finalMotorTorqueDirection} engineBrake={engineBrakeAxleTorque:F1}Nm " + $"deliberateRecovery={deliberateRecoveryActive} recoveryWeight={deliberateRecoveryWeight:F3} recoveryYawDamping={recoveryYawDamping:F3}/s rearRecovery={currentRearGripRecovery:F4}/s " + $"drifting={isDrifting} controlled={controlledDrift} handlingLatch={handlingDriftLatched} releaseRecovery={driftReleaseRecovery} risk={sharedOverRotationRisk:F3} " + $"limiter={limiterFactor:F2} shifting={shifting}", this);
    }
}
