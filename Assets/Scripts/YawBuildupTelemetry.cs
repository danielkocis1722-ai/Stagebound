#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

// Explicit developer recording only; never changes input, handling or motor output.
[DefaultExecutionOrder(210)]
public sealed class YawBuildupTelemetry : MonoBehaviour
{
    CarController car;
    Rigidbody body;
    StringBuilder csv;
    float start, previousYaw;
    bool hasPrevious;
    Vector3 previousPosition;
    public void Begin()
    {
        car = GetComponent<CarController>(); body = GetComponent<Rigidbody>();
        start = Time.fixedTime; hasPrevious = false;
        csv = new StringBuilder("seconds,speed_kmh,steer_fl,steer_fr,throttle,brake,handbrake,sideslip_deg,yaw_deg_s,yaw_accel_deg_s2,continuous");
        foreach (string tire in new[] { "fl", "fr", "rl", "rr" })
            csv.Append($",{tire}_grounded,{tire}_side_slip,{tire}_forward_slip,{tire}_fz,{tire}_extremum_slip,{tire}_extremum_value,{tire}_asymptote_slip,{tire}_asymptote_value,{tire}_stiffness");
        csv.AppendLine(",rear_left_multiplier,rear_right_multiplier,front_recovery_multiplier,yaw_assist_enabled,inertia_x,inertia_y,inertia_z");
        Debug.Log("Yaw recording started. Stagebound > Stop live yaw recording to save.", this);
    }
    void FixedUpdate()
    {
        if (csv == null || car == null || body == null) return;
        float yaw = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
        bool continuous = hasPrevious && Vector3.Distance(previousPosition, body.position) < Mathf.Max(2f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f + 1f);
        float acceleration = continuous ? (yaw - previousYaw) / Time.fixedDeltaTime : 0f;
        previousYaw = yaw; previousPosition = body.position; hasPrevious = true;
        csv.Append(FormattableString.Invariant($"{Time.fixedTime-start:F3},{car.SpeedKmh:F3},{car.frontLeft.steerAngle:F3},{car.frontRight.steerAngle:F3},{car.ThrottleInput:F3},{car.BrakeInput:F3},{car.HandbrakeInput:F3},{car.VehicleSlipAngle:F3},{yaw:F3},{acceleration:F3},{(continuous ? 1 : 0)}"));
        foreach (var wheel in new[] { car.frontLeft, car.frontRight, car.backLeft, car.backRight })
        {
            bool grounded = wheel.GetGroundHit(out var hit); var f = wheel.sidewaysFriction;
            csv.Append(FormattableString.Invariant($",{(grounded ? 1 : 0)},{hit.sidewaysSlip:F4},{hit.forwardSlip:F4},{hit.force:F2},{f.extremumSlip:F4},{f.extremumValue:F4},{f.asymptoteSlip:F4},{f.asymptoteValue:F4},{f.stiffness:F4}"));
        }
        var recovery = car.GetComponent<FrontAxleRecovery>(); var governor = car.GetComponent<DriftYawStability>();
        csv.AppendLine(FormattableString.Invariant($",{car.RearLeftGripMultiplier:F4},{car.RearRightGripMultiplier:F4},{(recovery != null ? recovery.FrontLateralMultiplier : 1f):F4},{(governor != null && governor.enabled ? 1 : 0)},{body.inertiaTensor.x:F3},{body.inertiaTensor.y:F3},{body.inertiaTensor.z:F3}"));
        if (Time.fixedTime-start >= 120f) Save();
    }
    public void Save()
    {
        if (csv == null) return;
        Directory.CreateDirectory("Logs/YawBuildup");
        string path = "Logs/YawBuildup/live-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv";
        File.WriteAllText(path, csv.ToString()); csv = null;
        Debug.Log("Saved yaw trace: " + path, this);
    }
    void OnDisable() => Save();
}
#endif
