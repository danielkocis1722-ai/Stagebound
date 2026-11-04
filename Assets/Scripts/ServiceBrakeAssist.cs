using System;
using UnityEngine;

// Per-wheel service-brake pressure only. Never changes grip, motor or handbrake torque.
public sealed class ServiceBrakeAssist
{
    [Serializable]
    public class Settings
    {
        public bool enabled = true;
        [Tooltip("Fraction of rolling speed lost before reducing pressure; independent of WheelHit.forwardSlip units.")]
        [Range(.05f, .5f)] public float targetSlip = .36f;
        [Min(0f)] public float minimumSpeedKmh = 8f;
        [Min(.1f)] public float releaseRate = 20f;
        [Min(.1f)] public float recoveryRate = 5f;
        public Settings Copy() => (Settings)MemberwiseClone();
    }

    float pressure = 1f;

    public float Apply(WheelCollider wheel, Rigidbody body, float requestedTorque,
        Settings settings, bool bypass, float dt)
    {
        if (settings == null || !settings.enabled || bypass || requestedTorque <= 0f ||
            !wheel.GetGroundHit(out var hit))
        {
            pressure = 1f;
            return requestedTorque;
        }

        Vector3 velocity = body.GetPointVelocity(hit.point);
        var groundBody = hit.collider != null ? hit.collider.attachedRigidbody : null;
        if (groundBody != null && groundBody != body)
            velocity -= groundBody.GetPointVelocity(hit.point);
        float groundSpeed = Vector3.Dot(velocity, hit.forwardDir);
        float speed = Mathf.Abs(groundSpeed);
        if (speed * 3.6f < Mathf.Max(1f, settings.minimumSpeedKmh))
        {
            pressure = 1f;
            return requestedTorque;
        }

        float rollingSpeed = wheel.rpm * (2f * Mathf.PI / 60f) * wheel.radius;
        float slip = Mathf.Clamp01((speed - rollingSpeed * Mathf.Sign(groundSpeed)) / speed);
        float target = Mathf.Clamp(settings.targetSlip, .05f, .5f);
        if (slip > target)
            pressure = Mathf.MoveTowards(pressure, 0f,
                settings.releaseRate * Mathf.InverseLerp(target, target + .2f, slip) * dt);
        else if (slip < target * .75f)
            pressure = Mathf.MoveTowards(pressure, 1f, settings.recoveryRate * dt);
        return requestedTorque * pressure;
    }
}
