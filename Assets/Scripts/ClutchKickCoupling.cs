using UnityEngine;

// One engine-side clutch torque, with equal reaction on the engine flywheel.
// The controller multiplies this torque through the existing gearbox and LSD once.
public sealed class ClutchKickCoupling
{
    const float RPMPerRadian = 60f / (2f * Mathf.PI);
    float releaseRate, releaseRemaining;
    public bool Active => releaseRemaining > 0f && releaseRate > 1f;
    public float SynchronizingTorque { get; private set; }

    public void Begin(float engagementRate)
    { releaseRate = Mathf.Max(0f, engagementRate); releaseRemaining = .3f; }
    public void Cancel() { releaseRate = releaseRemaining = 0f; SynchronizingTorque = 0f; }

    public float Step(float engineRPM, float drivetrainRPM, float engineTorque, float engineTorqueRating,
        float maxClutchTorque, float couplingStrength, float kickStrength, float engineInertia,
        float engagement, float deltaTime, float reflectedWheelInertia, float drivetrainLoadTorque = 0f)
    {
        SynchronizingTorque = 0f;
        if (engagement <= 0f || deltaTime <= 0f) return 0f;
        engagement = Mathf.Clamp01(engagement);
        float difference = engineRPM - drivetrainRPM;
        // Gentle reconnection retains normal holding capacity. Quick manual release
        // can use higher capacity during the release interval. Torque still
        // requires a current RPM mismatch; there is no timed torque envelope.
        float rapidRelease = Active ? Mathf.InverseLerp(1f, 8f, releaseRate) : 0f;
        releaseRemaining = Mathf.Max(0f, releaseRemaining - deltaTime);
        float capacity = Mathf.Lerp(Mathf.Min(engineTorqueRating, maxClutchTorque), maxClutchTorque,
            rapidRelease * Mathf.Clamp01(kickStrength)) * engagement;
        float engine = Mathf.Max(.05f, engineInertia);
        float wheels = Mathf.Max(.001f, reflectedWheelInertia);
        float inertia = 1f / (1f / engine + 1f / wheels);
        // Reduced inertia accounts for BOTH flywheels: spinning the rear wheels
        // changes drivetrain RPM too. Engine-only bounds can overshoot and reverse
        // the relative speed, creating an oscillating torque/braking pulse.
        float follow = 1f - Mathf.Exp(-Mathf.Max(0f, couplingStrength) * engagement *
            deltaTime * RPMPerRadian / inertia);
        // Include this step's combustion impulse before solving the clutch.
        // Engine torque is NOT separately added to the resulting clutch torque.
        float predictedDifference = difference + (engineTorque / engine + drivetrainLoadTorque / wheels) *
            deltaTime * RPMPerRadian;
        float demanded = predictedDifference / RPMPerRadian * inertia / deltaTime * follow;
        float transmitted = Mathf.Clamp(demanded, -Mathf.Max(0f, capacity), Mathf.Max(0f, capacity));
        SynchronizingTorque = transmitted - engineTorque * engagement;
        return transmitted;
    }
}
