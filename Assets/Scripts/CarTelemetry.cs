using UnityEngine;
using TMPro;

public class CarTelemetry : MonoBehaviour
{
    public Rigidbody rb;

    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    public TMP_Text telemetryText;

    public float SpeedKmh { get; private set; }
    public float SlipAngle { get; private set; }
    public float YawRate { get; private set; }

    void Update()
    {
        if (rb == null)
            return;

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        SlipAngle = Mathf.Atan2(
            localVelocity.x,
            Mathf.Abs(localVelocity.z)
        ) * Mathf.Rad2Deg;

        YawRate = rb.angularVelocity.y * Mathf.Rad2Deg;

        float flSlip = GetSideSlip(frontLeft);
        float frSlip = GetSideSlip(frontRight);
        float rlSlip = GetSideSlip(rearLeft);
        float rrSlip = GetSideSlip(rearRight);

        float steer =
            (frontLeft.steerAngle + frontRight.steerAngle) * 0.5f;

        if (telemetryText != null)
        {
            telemetryText.text =
                $"Speed: {SpeedKmh:0} km/h\n" +
                $"Steer: {steer:0.0}°\n" +
                $"Slip Angle: {SlipAngle:0.0}°\n" +
                $"Yaw Rate: {YawRate:0.0}°/s\n\n" +

                $"FL Slip: {flSlip:0.00}\n" +
                $"FR Slip: {frSlip:0.00}\n" +
                $"RL Slip: {rlSlip:0.00}\n" +
                $"RR Slip: {rrSlip:0.00}";
        }
    }

    float GetSideSlip(WheelCollider wheel)
    {
        if (wheel == null)
            return 0f;

        if (wheel.GetGroundHit(out WheelHit hit))
            return hit.sidewaysSlip;

        return 0f;
    }
}