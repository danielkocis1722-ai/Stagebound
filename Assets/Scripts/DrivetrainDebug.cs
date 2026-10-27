using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CarController))]
public class DrivetrainDebug : MonoBehaviour
{
    [Tooltip("Enable to inspect the live fields below. Does not draw over the race HUD.")]
    public bool captureTelemetry;
    public bool logToConsole;
    [Min(0.1f)] public float logInterval = 0.5f;
    [Min(0.01f)] public float frontReferenceStiffness = 1.2f;

    [Header("Live drivetrain (Play Mode, capture enabled)")]
    [SerializeField] float speedKmh, throttle, engineRPM;
    [SerializeField] float engineThrottle, normalizedRPM, torqueCurveMultiplier;
    [SerializeField] float calculatedEngineTorque, transmittedEngineTorque, longitudinalAcceleration;
    [SerializeField] float speedAcceleration, linearDamping, angularDamping;
    [SerializeField] float rearForwardAsymptoteValue, rearSidewaysAsymptoteSlip, rearSidewaysAsymptoteValue;
    [SerializeField] int gear;
    [SerializeField] float blMotorTorque, brMotorTorque, deliveredAxleTorque, leftTorqueShare;
    [SerializeField] bool blGrounded, brGrounded;
    [SerializeField] float blForwardSlip, brForwardSlip, blSidewaysSlip, brSidewaysSlip;
    [SerializeField] float blContactLoad, brContactLoad;
    [SerializeField] float flSidewaysSlip, frSidewaysSlip, flContactLoad, frContactLoad;
    [SerializeField] float flForwardSlip, frForwardSlip, flSteerAngle, frSteerAngle;
    [SerializeField] float steeringWheelbase, steeringFrontTrack;
    [SerializeField] bool flGrounded, frGrounded;
    [SerializeField] float flNormalUpDot, frNormalUpDot;
    [SerializeField] float frontLateralStiffnessMultiplier;
    [SerializeField] float blCombinedSlipMultiplier, brCombinedSlipMultiplier, combinedSlipEntryWeight;
    [SerializeField] float steeringInput, yawRate, vehicleSlipAngle, travelTurnRate, turnRadiusMeters;
    [SerializeField] float blRearFrictionMultiplier, brRearFrictionMultiplier;
    [SerializeField] float handbrakeInput, handbrakeGripMultiplier, clutchEngagement, limiterFactor;
    [SerializeField] float clutchInput, automaticClutchEngagement, manualClutchEngagement;
    [SerializeField] float drivetrainRPM, clutchRPMDifference, clutchKickTorque;
    [SerializeField] bool clutchKickActive;
    [SerializeField] bool shifting;
    CarController car;
    float nextLogTime;
    Rigidbody body;
    Vector3 previousTravelDirection;
    float previousSpeed;
    bool hasSpeedSample;

    void Awake()
    {
        car = GetComponent<CarController>();
        body = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!captureTelemetry || !car.enabled)
        { hasSpeedSample = false; previousTravelDirection = Vector3.zero; return; }
        speedKmh = car.SpeedKmh; throttle = car.ThrottleInput;
        engineRPM = car.EngineRPM; gear = car.currentGear;
        engineThrottle = car.EngineThrottle; normalizedRPM = car.NormalizedEngineRPM;
        torqueCurveMultiplier = car.TorqueCurveMultiplier; calculatedEngineTorque = car.CalculatedEngineTorque;
        transmittedEngineTorque = car.TransmittedEngineTorque; longitudinalAcceleration = car.LongitudinalAcceleration;
        float actualSpeed = body.linearVelocity.magnitude;
        speedAcceleration = hasSpeedSample && car.DynamicsSampleValid
            ? (actualSpeed - previousSpeed) / Time.fixedDeltaTime : 0f;
        previousSpeed = actualSpeed; hasSpeedSample = true;
        linearDamping = body.linearDamping; angularDamping = body.angularDamping;
        rearForwardAsymptoteValue = car.backLeft.forwardFriction.asymptoteValue;
        rearSidewaysAsymptoteSlip = car.backLeft.sidewaysFriction.asymptoteSlip;
        rearSidewaysAsymptoteValue = car.backLeft.sidewaysFriction.asymptoteValue;
        blMotorTorque = car.backLeft.motorTorque; brMotorTorque = car.backRight.motorTorque;
        deliveredAxleTorque = car.DeliveredAxleTorque; leftTorqueShare = car.RearLeftTorqueShare;
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
        flSidewaysSlip = flGrounded ? fl.sidewaysSlip : 0f; frSidewaysSlip = frGrounded ? fr.sidewaysSlip : 0f;
        flForwardSlip = flGrounded ? fl.forwardSlip : 0f; frForwardSlip = frGrounded ? fr.forwardSlip : 0f;
        flSteerAngle = car.frontLeft.steerAngle; frSteerAngle = car.frontRight.steerAngle;
        steeringWheelbase = car.SteeringWheelbase; steeringFrontTrack = car.SteeringFrontTrack;
        flContactLoad = flGrounded ? fl.force : 0f; frContactLoad = frGrounded ? fr.force : 0f;
        flNormalUpDot = flGrounded ? Vector3.Dot(fl.normal, Vector3.up) : 0f;
        frNormalUpDot = frGrounded ? Vector3.Dot(fr.normal, Vector3.up) : 0f;
        frontLateralStiffnessMultiplier = (car.frontLeft.sidewaysFriction.stiffness + car.frontRight.sidewaysFriction.stiffness) /
            Mathf.Max(.001f, 2f * frontReferenceStiffness);
        blCombinedSlipMultiplier = car.RearLeftCombinedSlipMultiplier;
        brCombinedSlipMultiplier = car.RearRightCombinedSlipMultiplier;
        combinedSlipEntryWeight = car.CombinedSlipEntryWeight;
        steeringInput = car.SteeringInput; yawRate = car.YawRate; vehicleSlipAngle = car.VehicleSlipAngle;
        Vector3 travel = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        travelTurnRate = travel.sqrMagnitude > 1f && previousTravelDirection.sqrMagnitude > .5f
            ? Vector3.SignedAngle(previousTravelDirection, travel, Vector3.up) / Time.fixedDeltaTime : 0f;
        previousTravelDirection = travel.sqrMagnitude > 1f ? travel.normalized : Vector3.zero;
        turnRadiusMeters = Mathf.Abs(travelTurnRate) > .1f
            ? travel.magnitude / (Mathf.Abs(travelTurnRate) * Mathf.Deg2Rad) : float.PositiveInfinity;
        blRearFrictionMultiplier = car.RearLeftGripMultiplier;
        brRearFrictionMultiplier = car.RearRightGripMultiplier;
        handbrakeInput = car.HandbrakeInput; handbrakeGripMultiplier = car.HandbrakeGripMultiplier;
        clutchEngagement = car.ClutchEngagement; limiterFactor = car.LimiterFactor;
        clutchInput = car.ClutchInput;
        automaticClutchEngagement = car.AutomaticClutchEngagement;
        manualClutchEngagement = car.ManualClutchEngagement;
        drivetrainRPM = car.DrivetrainRPM; clutchRPMDifference = car.ClutchRPMDifference;
        clutchKickActive = car.ClutchKickActive; clutchKickTorque = car.ClutchKickTorque;
        shifting = car.IsShifting;
        if (!logToConsole || Time.unscaledTime < nextLogTime) return;
        nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logInterval);
        Debug.Log($"Drivetrain: {speedKmh:F1} km/h throttle={throttle:F2} RPM={engineRPM:F0} gear={gear} " +
            $"engineThrottle={engineThrottle:F3} normalizedRPM={normalizedRPM:F3} curve={torqueCurveMultiplier:F3} " +
            $"engineTorque={calculatedEngineTorque:F1} transmitted={transmittedEngineTorque:F1} accel={longitudinalAcceleration:F2}m/s2 " +
            $"speedAccel={speedAcceleration:F2}m/s2 damping={linearDamping:F3}/{angularDamping:F3} " +
            $"rearAsymForward={rearForwardAsymptoteValue:F3} rearAsymSide={rearSidewaysAsymptoteSlip:F3}/{rearSidewaysAsymptoteValue:F3} " +
            $"torque BL/BR={blMotorTorque:F0}/{brMotorTorque:F0} axle={deliveredAxleTorque:F0} leftShare={leftTorqueShare:F3} " +
            $"grounded={blGrounded}/{brGrounded} forwardSlip={blForwardSlip:F3}/{brForwardSlip:F3} " +
            $"sidewaysSlip={blSidewaysSlip:F3}/{brSidewaysSlip:F3} load={blContactLoad:F0}/{brContactLoad:F0} " +
            $"frontSide={flSidewaysSlip:F3}/{frSidewaysSlip:F3} frontLoad={flContactLoad:F0}/{frContactLoad:F0} " +
            $"frontForward={flForwardSlip:F3}/{frForwardSlip:F3} frontSteer={flSteerAngle:F2}/{frSteerAngle:F2} " +
            $"wheelbase={steeringWheelbase:F3} frontTrack={steeringFrontTrack:F3} " +
            $"frontGround={flGrounded}/{frGrounded} normalUp={flNormalUpDot:F2}/{frNormalUpDot:F2} frontStiffness={frontLateralStiffnessMultiplier:F3} " +
            $"combined={blCombinedSlipMultiplier:F3}/{brCombinedSlipMultiplier:F3} entryWeight={combinedSlipEntryWeight:F3} " +
            $"steer={steeringInput:F3} yaw={yawRate:F1} bodySlip={vehicleSlipAngle:F1} travelTurn={travelTurnRate:F1} radius={turnRadiusMeters:F1} " +
            $"grip={blRearFrictionMultiplier:F3}/{brRearFrictionMultiplier:F3} " +
            $"HB={handbrakeInput:F2} HBgrip={handbrakeGripMultiplier:F3} clutch={clutchEngagement:F2} " +
            $"clutchInput={clutchInput:F2} auto/manual={automaticClutchEngagement:F2}/{manualClutchEngagement:F2} " +
            $"driveRPM={drivetrainRPM:F0} RPMdifference={clutchRPMDifference:F0} " +
            $"kick={clutchKickActive} kickTorque={clutchKickTorque:F1} " +
            $"limiter={limiterFactor:F2} shifting={shifting}", this);
    }
}
