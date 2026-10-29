using UnityEngine;

// Observes the current physics only. Never writes to the body or the wheels.
[DefaultExecutionOrder(150)]
[RequireComponent(typeof(CarController), typeof(Rigidbody))]
public class DriftTelemetry : MonoBehaviour
{
    [Header("Detection")]
    [Min(1f)] public float minimumSpeedKmh = 28f;
    [Min(1f)] public float minimumForwardSpeedKmh = 20f;
    [Range(0f, 60f)] public float minimumAngle = 10f;
    [Range(45f, 90f)] public float maximumAngle = 65f;
    [Min(0f)] public float minimumRearSidewaysSlip = .15f;
    [Min(0f)] public float minimumRearForwardSlip = .25f;
    public bool requireBothRearWheelsGrounded = true;

    public bool IsDrifting { get; private set; }
    public float VehicleSlipAngle { get; private set; }
    public int DriftDirection { get; private set; }
    public float DriftDuration { get; private set; }
    public float CurrentDriftSpeed { get; private set; }
    public float AverageDriftSpeed { get; private set; }
    public float PeakDriftSpeed { get; private set; }
    public float CurrentDriftAngle => Mathf.Abs(VehicleSlipAngle);
    public float AverageDriftAngle { get; private set; }
    public float PeakDriftAngle { get; private set; }
    public float CurrentYawRate { get; private set; }
    public float ForwardSpeedKmh { get; private set; }
    public float BLForwardSlip { get; private set; }
    public float BRForwardSlip { get; private set; }
    public float BLSidewaysSlip { get; private set; }
    public float BRSidewaysSlip { get; private set; }
    public bool RearGrounded { get; private set; }
    CarController car;
    Rigidbody body;

    void Awake() { car = GetComponent<CarController>(); body = GetComponent<Rigidbody>(); }
    void FixedUpdate()
    {
        WheelHit left = default, right = default;
        bool bl = car.backLeft != null && car.backLeft.GetGroundHit(out left);
        bool br = car.backRight != null && car.backRight.GetGroundHit(out right);
        EvaluateSample(body.linearVelocity, transform.forward, body.angularVelocity.y * Mathf.Rad2Deg,
            bl, left, br, right, Time.fixedDeltaTime);
    }

    // A single sample path also allows deterministic scoring/telemetry validation.
    public void EvaluateSample(Vector3 velocity, Vector3 forward, float yawRate,
        bool blGrounded, WheelHit bl, bool brGrounded, WheelHit br, float dt)
    {
        velocity.y = 0; forward.y = 0; forward.Normalize();
        CurrentDriftSpeed = velocity.magnitude * 3.6f;
        ForwardSpeedKmh = Vector3.Dot(velocity, forward) * 3.6f;
        VehicleSlipAngle = velocity.sqrMagnitude > .01f && forward.sqrMagnitude > .01f
            ? Vector3.SignedAngle(forward, velocity, Vector3.up) : 0f;
        CurrentYawRate = yawRate;
        BLForwardSlip = blGrounded ? bl.forwardSlip : 0f;
        BRForwardSlip = brGrounded ? br.forwardSlip : 0f;
        BLSidewaysSlip = blGrounded ? bl.sidewaysSlip : 0f;
        BRSidewaysSlip = brGrounded ? br.sidewaysSlip : 0f;
        RearGrounded = requireBothRearWheelsGrounded ? blGrounded && brGrounded : blGrounded || brGrounded;
        bool sliding = Mathf.Max(Mathf.Abs(BLSidewaysSlip), Mathf.Abs(BRSidewaysSlip)) >= minimumRearSidewaysSlip ||
            Mathf.Max(Mathf.Abs(BLForwardSlip), Mathf.Abs(BRForwardSlip)) >= minimumRearForwardSlip;
        IsDrifting = RearGrounded && sliding && CurrentDriftSpeed >= minimumSpeedKmh &&
            ForwardSpeedKmh >= minimumForwardSpeedKmh && CurrentDriftAngle >= minimumAngle &&
            CurrentDriftAngle < maximumAngle;
        DriftDirection = IsDrifting ? (VehicleSlipAngle > 0f ? 1 : -1) : 0;
        if (!IsDrifting) { ClearStatistics(); return; }
        float previous = DriftDuration;
        DriftDuration += Mathf.Max(0f, dt);
        if (DriftDuration <= 0f) return;
        AverageDriftSpeed = (AverageDriftSpeed * previous + CurrentDriftSpeed * dt) / DriftDuration;
        AverageDriftAngle = (AverageDriftAngle * previous + CurrentDriftAngle * dt) / DriftDuration;
        PeakDriftSpeed = Mathf.Max(PeakDriftSpeed, CurrentDriftSpeed);
        PeakDriftAngle = Mathf.Max(PeakDriftAngle, CurrentDriftAngle);
    }
    void ClearStatistics()
    { DriftDuration = AverageDriftSpeed = PeakDriftSpeed = AverageDriftAngle = PeakDriftAngle = 0f; }
    public void ResetDrift() { IsDrifting = false; DriftDirection = 0; ClearStatistics(); }
}
