using UnityEngine;

public class DriftStability : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Drift Detection")]
    public float minimumDriftSpeed = 20f;
    public float driftStartAngle = 8f;
    public float fullDriftAngle = 30f;

    [Header("Rear Grip")]
    public float normalRearStiffness = 1.20f;
    public float driftRearStiffness = 1.05f;

    // Ako rýchlo pneumatika pustí grip
    public float gripReleaseSpeed = 2.5f;

    // Ako pomaly sa grip po drifte vráti
    // Nižšia hodnota = menej snapu
    public float gripRecoverySpeed = 0.8f;

    [Header("Spin Protection")]
    public float maxDriftYawRate = 80f;
    public float yawDamping = 3f;

    [Header("Telemetry")]
    public float slipAngle;
    public float yawRate;
    public float driftAmount;

    private float currentRearStiffness;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        currentRearStiffness = normalRearStiffness;
    }

    void FixedUpdate()
    {
        float speedKmh =
            rb.linearVelocity.magnitude * 3.6f;

        CalculateSlipAngle(speedKmh);
        UpdateRearGrip(speedKmh);
        StabilizeYaw(speedKmh);
    }

    void CalculateSlipAngle(float speedKmh)
    {
        if (speedKmh < 2f)
        {
            slipAngle = 0f;
            yawRate = 0f;
            driftAmount = 0f;
            return;
        }

        Vector3 flatVelocity =
            Vector3.ProjectOnPlane(
                rb.linearVelocity,
                Vector3.up
            );

        if (flatVelocity.sqrMagnitude < 0.01f)
        {
            slipAngle = 0f;
            return;
        }

        slipAngle = Vector3.SignedAngle(
            transform.forward,
            flatVelocity.normalized,
            Vector3.up
        );

        yawRate =
            rb.angularVelocity.y * Mathf.Rad2Deg;

        if (speedKmh >= minimumDriftSpeed)
        {
            driftAmount = Mathf.InverseLerp(
                driftStartAngle,
                fullDriftAngle,
                Mathf.Abs(slipAngle)
            );
        }
        else
        {
            driftAmount = 0f;
        }
    }

    void UpdateRearGrip(float speedKmh)
    {
        float targetStiffness = Mathf.Lerp(
            normalRearStiffness,
            driftRearStiffness,
            driftAmount
        );

        // Grip pustíme relatívne rýchlo,
        // ale naspäť ho vraciame pomaly.
        float responseSpeed =
            targetStiffness < currentRearStiffness
            ? gripReleaseSpeed
            : gripRecoverySpeed;

        currentRearStiffness =
            Mathf.MoveTowards(
                currentRearStiffness,
                targetStiffness,
                responseSpeed * Time.fixedDeltaTime
            );

        SetRearStiffness(
            rearLeft,
            currentRearStiffness
        );

        SetRearStiffness(
            rearRight,
            currentRearStiffness
        );
    }

    void StabilizeYaw(float speedKmh)
    {
        if (
            speedKmh < minimumDriftSpeed ||
            driftAmount <= 0f
        )
            return;

        float maxYawRadians =
            maxDriftYawRate * Mathf.Deg2Rad;

        float currentYaw =
            rb.angularVelocity.y;

        float excessYaw =
            Mathf.Abs(currentYaw) - maxYawRadians;

        // Normálny drift nechávame úplne na fyzike.
        // Zasiahneme až keď auto smeruje do spinu.
        if (excessYaw > 0f)
        {
            float correction =
                -Mathf.Sign(currentYaw) *
                excessYaw *
                yawDamping *
                driftAmount;

            rb.AddTorque(
                Vector3.up * correction,
                ForceMode.Acceleration
            );
        }
    }

    void SetRearStiffness(
        WheelCollider wheel,
        float stiffness
    )
    {
        if (wheel == null)
            return;

        WheelFrictionCurve friction =
            wheel.sidewaysFriction;

        friction.stiffness = stiffness;

        wheel.sidewaysFriction = friction;
    }
}