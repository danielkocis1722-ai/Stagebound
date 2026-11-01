using UnityEngine;

// One outward-yaw governor. No steering, grip, heading or linear-velocity writes.
[DefaultExecutionOrder(160)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CarController), typeof(Rigidbody), typeof(DriftTelemetry))]
public class DriftYawStability : MonoBehaviour
{
    [Header("Grounded, forward sliding only")]
    [Min(0f)]
    public float minimumSpeedKmh = 28f;
    [Min(0f)]
    public float fullSpeedKmh = 40f;
    [Min(0f)]
    public float minimumForwardSpeedKmh = 15f;

    [Header("Progressive opening support")]
    [Min(0f)]
    public float startAngle = 6f;
    [Min(1f)]
    public float fullAngle = 14f;
    [Tooltip("Outward yaw below this rate is never removed (degrees/sec). Not a hard cap.")]
    [Min(0f)]
    public float startYawRate = 45f;
    [Min(1f)]
    public float fullYawRate = 95f;
    [Min(0f)]
    public float startAngleGrowth = 10f;
    [Min(1f)]
    public float fullAngleGrowth = 35f;
    [Range(0f, 8f)]
    public float maximumDampingRate = 6f;

    private CarController car;
    private Rigidbody body;
    private DriftTelemetry telemetry;
    private Vector3 previousPosition;
    private float previousSlip;
    private bool hasSample;

    private void Awake()
    {
        car = GetComponent<CarController>();
        body = GetComponent<Rigidbody>();
        telemetry = GetComponent<DriftTelemetry>();
    }

    private void OnEnable() => hasSample = false;

    private void OnDisable() => hasSample = false;

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f || Time.timeScale <= 0f || body.isKinematic)
        {
            hasSample = false;
            return;
        }

        float slipAngle = telemetry.VehicleSlipAngle;
        float yawRate = body.angularVelocity.y * Mathf.Rad2Deg;
        float travelLimit = Mathf.Max(2f, body.linearVelocity.magnitude * dt * 3f + 1f);
        bool continuous = hasSample && (body.position - previousPosition).sqrMagnitude <= travelLimit * travelLimit && Mathf.Abs(Mathf.DeltaAngle(previousSlip, slipAngle)) < 30f;
        if (continuous)
        {
            float angleGrowth = Mathf.DeltaAngle(previousSlip, slipAngle) * Mathf.Sign(slipAngle) / dt;
            bool grounded = telemetry.RearGrounded && car.backLeft.isGrounded && car.backRight.isGrounded && (car.frontLeft.isGrounded || car.frontRight.isGrounded);
            bool openingYaw = slipAngle * yawRate < 0f;
            bool sliding = Mathf.Max(Mathf.Abs(telemetry.BLSidewaysSlip), Mathf.Abs(telemetry.BRSidewaysSlip)) >= .15f || Mathf.Max(Mathf.Abs(telemetry.BLForwardSlip), Mathf.Abs(telemetry.BRForwardSlip)) >= .25f;
            if (grounded && sliding && openingYaw && telemetry.ForwardSpeedKmh > minimumForwardSpeedKmh)
            {
                float appliedDampingRate = Mathf.Clamp(maximumDampingRate, 0f, 8f) * Weight(startAngle, fullAngle, Mathf.Abs(slipAngle)) * Weight(startYawRate, fullYawRate, Mathf.Abs(yawRate)) * Weight(startAngleGrowth, fullAngleGrowth, angleGrowth) * Weight(minimumSpeedKmh, fullSpeedKmh, telemetry.CurrentDriftSpeed);
                // Leave ordinary corner/drift rotation intact. Only remove a
                // portion of the excess rate, never pull toward a zero heading.
                float excess = Mathf.Max(0f, Mathf.Abs(yawRate) - Mathf.Max(0f, startYawRate));
                float removedYawRate = excess * (1f - Mathf.Exp(-appliedDampingRate * dt));
                body.angularVelocity -= Vector3.up * (Mathf.Sign(yawRate) * removedYawRate * Mathf.Deg2Rad);
            }
        }

        previousSlip = slipAngle;
        previousPosition = body.position;
        hasSample = true;
    }

    private static float Weight(float start, float full, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, Mathf.Max(start + .01f, full), value));
}
