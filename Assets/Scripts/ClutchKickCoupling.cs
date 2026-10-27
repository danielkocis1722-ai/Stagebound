using UnityEngine;

// Bounded engine-side transient. The controller supplies the final clutch value
// and applies the result through its existing gearbox and rear differential.
public sealed class ClutchKickCoupling
{
    float remaining, duration, torque, releaseRPMDifference;
    public bool Active => remaining > 0f;

    public void Begin(float seconds, float rpmDifference)
    {
        duration = Mathf.Max(0.01f, seconds);
        remaining = duration;
        torque = 0f;
        releaseRPMDifference = Mathf.Max(0f, rpmDifference);
    }

    public void Cancel() { remaining = torque = 0f; }

    public float Step(float rpmDifference, float normalEngineTorque, float engineTorqueRating,
        float maxClutchTorque, float strength, float maxRPMDifference, float response,
        float engineInertia, float engagement, float deltaTime,
        float mismatchExponent = 1f, float midrangeWeight = 0f)
    {
        if (!Active || rpmDifference <= 0f || strength <= 0f || deltaTime <= 0f)
        { Cancel(); return 0f; }
        float mismatch = Mathf.Clamp01(Mathf.Min(rpmDifference, releaseRPMDifference) /
            Mathf.Max(1f, maxRPMDifference));
        mismatch = Mathf.Lerp(mismatch, Mathf.Pow(mismatch, Mathf.Clamp(mismatchExponent, 0.5f, 1f)),
            Mathf.Clamp01(midrangeWeight));
        float envelope = Mathf.Clamp01(remaining / duration);
        float target = Mathf.Max(0f, engineTorqueRating) * Mathf.Clamp01(strength) * mismatch;
        torque = Mathf.Lerp(torque, target, 1f - Mathf.Exp(-Mathf.Max(0f, response) * deltaTime));
        remaining = Mathf.Max(0f, remaining - deltaTime);

        // Capacity limits the total engine-side torque, not an extra multiplier.
        float capacity = Mathf.Max(0f, maxClutchTorque - normalEngineTorque);
        // Do not debit more rotational speed than the remaining RPM surplus.
        float available = rpmDifference * (2f * Mathf.PI / 60f) * Mathf.Max(0.05f, engineInertia) /
            (deltaTime * Mathf.Max(0.001f, Mathf.Clamp01(engagement)));
        return Mathf.Min(torque * envelope, Mathf.Min(capacity, available));
    }
}
