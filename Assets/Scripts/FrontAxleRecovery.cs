using UnityEngine;

// Front tire authority only. No input, pose, velocity, torque or rear-tire writes.
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(CarController), typeof(Rigidbody))]
public class FrontAxleRecovery : MonoBehaviour
{
    [Header("Genuine drift gates")]
    [Min(1f)] public float minimumSpeedKmh = 28f;
    [Min(0f)] public float minimumForwardSpeedKmh = 15f;
    [Range(5f, 30f)] public float minimumDriftAngle = 15f;
    [Min(0f)] public float minimumRearSidewaysSlip = .15f;
    [Min(0f)] public float minimumRearForwardSlip = .25f;
    [Range(0f, .5f)] public float minimumCountersteerInput = .08f;
    [Header("Front authority")]
    public AnimationCurve recoveryByAngle = new AnimationCurve(new Keyframe(15, 1),
        new Keyframe(20, 1.02f), new Keyframe(30, 1.08f), new Keyframe(40, 1.12f), new Keyframe(60, 1.12f));
    [Range(1f, 1.15f)] public float maximumRecoveryMultiplier = 1.02f;
    [Min(.1f)] public float blendResponse = 3f;
    [Tooltip("Small line corrections keep baseline front grip; stronger countersteer requests extra recovery bite.")]
    [Range(.08f, .6f)] public float recoveryGripInput = .25f;
    [Header("Deliberate lift and countersteer")]
    [Range(1f, 1.18f)] public float deliberateRecoveryMultiplier = 1.10f;
    [Min(0f)] public float recoveryStartAngle = 33f;
    [Min(1f)] public float recoveryFullAngle = 45f;
    [Range(0f, 1f)] public float recoveryThrottleCeiling = .2f;
    [Range(0f, 1f)] public float deliberateCountersteerInput = .65f;
    [Min(.1f)] public float recoveryBlendResponse = 6f;
    [SerializeField] float deliberateRecoveryWeight;
    public float DeliberateRecoveryWeight => deliberateRecoveryWeight;
    public bool RecoveryAttemptActive { get; private set; }
    [Header("Shared drift risk (front, rear recovery and yaw assist)")]
    [SerializeField] bool controlledDrift;
    [SerializeField] float overRotationWeight, openingAngleRate, outwardYawAcceleration;
    public bool IsControlledDrift => controlledDrift;
    public float OverRotationWeight => overRotationWeight;
    public float OpeningAngleRate => openingAngleRate;
    public float OutwardYawAcceleration => outwardYawAcceleration;

    // Body heading relative to travel: positive means recovery steering is left.
    // Existing CarController/DriftTelemetry use the opposite signed convention;
    // those APIs and their scoring meaning are deliberately unchanged.
    public float VehicleSlipAngle { get; private set; }
    public float PlayerSteeringInput => car != null ? car.SteeringInput : 0f;
    public float FrontLateralMultiplier { get; private set; } = 1f;
    public bool CountersteerRecoveryActive { get; private set; }
    public float FLSteerAngle => car != null ? car.frontLeft.steerAngle : 0f;
    public float FRSteerAngle => car != null ? car.frontRight.steerAngle : 0f;
    public float FLSidewaysSlip { get; private set; }
    public float FRSidewaysSlip { get; private set; }
    public float YawRate => body != null ? body.angularVelocity.y * Mathf.Rad2Deg : 0f;
    // Kept for existing telemetry: wheel response now comes only from player
    // steering/cross-center rates, with no recovery-dependent multiplier.
    public float SteeringResponseMultiplier => 1f;

    CarController car;
    Rigidbody body;
    float leftBaseStiffness, rightBaseStiffness;
    bool initialized;
    bool hasAngleSample;
    float previousAngle;
    float previousYaw;
    Vector3 previousPosition;

    void Awake()
    {
        if (initialized) return;
        car = GetComponent<CarController>(); body = GetComponent<Rigidbody>();
        if (car.frontLeft == null || car.frontRight == null) return;
        leftBaseStiffness = car.frontLeft.sidewaysFriction.stiffness;
        rightBaseStiffness = car.frontRight.sidewaysFriction.stiffness;
        initialized = true;
    }
    float RecoveryWeight()
    {
        if (!initialized) return 0f;
        Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        VehicleSlipAngle = velocity.sqrMagnitude > .01f ? Vector3.SignedAngle(velocity, forward, Vector3.up) : 0f;
        float angle = Mathf.Abs(VehicleSlipAngle);
        if (velocity.magnitude * 3.6f < minimumSpeedKmh || Vector3.Dot(velocity, forward) * 3.6f < minimumForwardSpeedKmh ||
            angle < minimumDriftAngle || angle >= 75f || car.SteeringInput * VehicleSlipAngle >= 0f ||
            Mathf.Abs(car.SteeringInput) < minimumCountersteerInput) return 0f;
        if (!car.frontLeft.isGrounded || !car.frontRight.isGrounded ||
            !car.backLeft.GetGroundHit(out WheelHit bl) || !car.backRight.GetGroundHit(out WheelHit br)) return 0f;
        bool sliding = Mathf.Max(Mathf.Abs(bl.sidewaysSlip), Mathf.Abs(br.sidewaysSlip)) >= minimumRearSidewaysSlip ||
            Mathf.Max(Mathf.Abs(bl.forwardSlip), Mathf.Abs(br.forwardSlip)) >= minimumRearForwardSlip;
        if (!sliding) return 0f;
        float input = Mathf.InverseLerp(minimumCountersteerInput, Mathf.Max(minimumCountersteerInput + .01f, .35f), Mathf.Abs(car.SteeringInput));
        float entry = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(minimumDriftAngle, minimumDriftAngle + 5f, angle));
        float spinFade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(60f, 75f, angle));
        return input * entry * spinFade;
    }
    void FixedUpdate()
    {
        if (!initialized) Awake();
        if (!initialized) return;
        float weight = RecoveryWeight();
        weight *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(recoveryGripInput,
            Mathf.Max(recoveryGripInput + .01f, .65f), Mathf.Abs(car.SteeringInput)));
        float dt = Mathf.Max(.001f, Time.fixedDeltaTime);
        float travelLimit = Mathf.Max(2f, body.linearVelocity.magnitude * dt * 3f + 1f);
        bool continuous = hasAngleSample && (body.position - previousPosition).sqrMagnitude <= travelLimit * travelLimit &&
            Mathf.Abs(Mathf.DeltaAngle(previousAngle, VehicleSlipAngle)) < 30f;
        float angleRate = continuous ? Mathf.DeltaAngle(previousAngle, VehicleSlipAngle) / dt : 0f;
        float yawGrowth = continuous ? (Mathf.Abs(YawRate) - Mathf.Abs(previousYaw)) / dt : 0f;
        float follow = 1f - Mathf.Exp(-6f * dt);
        if (!continuous) { openingAngleRate = outwardYawAcceleration = deliberateRecoveryWeight = 0f; controlledDrift = false; }
        openingAngleRate = Mathf.Lerp(openingAngleRate, angleRate * Mathf.Sign(VehicleSlipAngle), follow);
        outwardYawAcceleration = Mathf.Lerp(outwardYawAcceleration, yawGrowth, follow);
        previousAngle = VehicleSlipAngle; previousYaw = YawRate; previousPosition = body.position; hasAngleSample = true;
        float angle = Mathf.Abs(VehicleSlipAngle);
        bool moving = car.SpeedKmh >= minimumSpeedKmh && car.SignedLongitudinalSpeedKmh > minimumForwardSpeedKmh;
        bool grounded = car.frontLeft.isGrounded && car.frontRight.isGrounded && car.backLeft.isGrounded && car.backRight.isGrounded;
        if (!moving || !grounded || angle < 12f || angle > 33f || openingAngleRate > 18f || outwardYawAcceleration > 80f)
            controlledDrift = false;
        else if (angle >= 14.99f && angle <= 30.01f && Mathf.Abs(openingAngleRate) <= 10f && Mathf.Abs(outwardYawAcceleration) <= 30f)
            controlledDrift = true;
        // One risk assessment feeds every recovery mechanism. Stable angle alone
        // never requests recovery; real opening/growing yaw is required.
        overRotationWeight = moving && grounded && VehicleSlipAngle * YawRate > 0f && !controlledDrift
            ? Smooth(recoveryStartAngle, recoveryFullAngle, angle) * Smooth(30f, 90f, Mathf.Abs(YawRate)) *
                Smooth(3f, 18f, openingAngleRate) * Mathf.Lerp(.1f, 1f, Smooth(20f, 100f, outwardYawAcceleration)) : 0f;
        weight *= overRotationWeight;
        float recoveryTarget = 0f;
        bool deliberateInput = Mathf.Abs(car.SteeringInput) >= deliberateCountersteerInput &&
            car.SteeringInput * VehicleSlipAngle < 0f && car.SteeringInput * YawRate < 0f &&
            car.ThrottleInput < recoveryThrottleCeiling && car.HandbrakeInput <= .05f && car.BrakeInput <= .02f;
        if (deliberateInput && car.SignedLongitudinalSpeedKmh > minimumForwardSpeedKmh &&
            car.SpeedKmh >= minimumSpeedKmh && VehicleSlipAngle * YawRate > 0f &&
            Mathf.Abs(VehicleSlipAngle) < 75f && car.frontLeft.isGrounded && car.frontRight.isGrounded &&
            car.backLeft.isGrounded && car.backRight.isGrounded)
        {
            recoveryTarget = overRotationWeight *
                (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, recoveryThrottleCeiling, car.ThrottleInput))) *
                (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(60f, 75f, Mathf.Abs(VehicleSlipAngle))));
        }
        RecoveryAttemptActive = recoveryTarget > .001f;
        deliberateRecoveryWeight = Mathf.Lerp(deliberateRecoveryWeight, recoveryTarget,
            1f - Mathf.Exp(-recoveryBlendResponse * Time.fixedDeltaTime));
        // Do not strengthen the old turning direction while the wheel interpolation
        // is still crossing zero to reach the player's countersteer request.
        float actualSteer = (FLSteerAngle + FRSteerAngle) * .5f;
        if (actualSteer * VehicleSlipAngle >= 0f) weight = 0f;
        bool leftHit = car.frontLeft.GetGroundHit(out WheelHit fl);
        bool rightHit = car.frontRight.GetGroundHit(out WheelHit fr);
        // At excessive angle the tires can still drive an opening spin. Require
        // either a restoring front reaction or measured progress back toward the
        // travel direction; then the extra front authority supports that catch.
        float loadedSlip = fl.sidewaysSlip * fl.force + fr.sidewaysSlip * fr.force;
        bool trajectoryRecovering = angleRate * Mathf.Sign(VehicleSlipAngle) < -.5f;
        if (!leftHit || !rightHit || (!trajectoryRecovering && loadedSlip * VehicleSlipAngle <= 0f)) weight = 0f;
        float requested = Mathf.Clamp(recoveryByAngle != null ? recoveryByAngle.Evaluate(Mathf.Abs(VehicleSlipAngle)) : 1f,
            1f, Mathf.Clamp(maximumRecoveryMultiplier, 1f, 1.15f));
        float target = Mathf.Lerp(1f, requested, weight);
        // Emergency bite still waits for the actual front wheels to countersteer
        // and for a restoring front reaction, rather than boosting the old turn.
        if (actualSteer * VehicleSlipAngle < 0f && leftHit && rightHit &&
            (trajectoryRecovering || loadedSlip * VehicleSlipAngle > 0f))
            target = Mathf.Max(target, Mathf.Lerp(1f, Mathf.Clamp(deliberateRecoveryMultiplier, 1f, 1.18f), deliberateRecoveryWeight));
        FrontLateralMultiplier = Mathf.Lerp(FrontLateralMultiplier, target, 1f - Mathf.Exp(-blendResponse * Time.fixedDeltaTime));
        CountersteerRecoveryActive = target > 1.001f && (weight > 0f || deliberateRecoveryWeight > .001f);
        SetStiffness(car.frontLeft, leftBaseStiffness * FrontLateralMultiplier);
        SetStiffness(car.frontRight, rightBaseStiffness * FrontLateralMultiplier);
        FLSidewaysSlip = leftHit ? fl.sidewaysSlip : 0f;
        FRSidewaysSlip = rightHit ? fr.sidewaysSlip : 0f;
    }
    static void SetStiffness(WheelCollider wheel, float value)
    { if (wheel == null) return; var friction = wheel.sidewaysFriction; friction.stiffness = value; wheel.sidewaysFriction = friction; }
    static float Smooth(float start, float end, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, Mathf.Max(start + .01f, end), value));
    void OnDisable()
    {
        if (!initialized) return;
        SetStiffness(car.frontLeft, leftBaseStiffness); SetStiffness(car.frontRight, rightBaseStiffness);
        FrontLateralMultiplier = 1f; CountersteerRecoveryActive = false;
        hasAngleSample = false;
        deliberateRecoveryWeight = 0f; RecoveryAttemptActive = false;
        controlledDrift = false; overRotationWeight = openingAngleRate = outwardYawAcceleration = 0f;
    }
}
