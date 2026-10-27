using UnityEngine;

// Presentation-only state: never writes to the vehicle or its Rigidbody.
public sealed class AccelerationCameraFeedback
{
    public float AccelerationAmount { get; private set; }
    public float KickAmount { get; private set; }
    float previousTorque, previousThrottle, previousClutch;
    float kickElapsed, kickLength, cooldown;
    bool kicking;

    public void Reset(float torque, float throttle, float clutch)
    {
        AccelerationAmount = KickAmount = 0f;
        previousTorque = Mathf.Abs(torque); previousThrottle = throttle; previousClutch = clutch;
        kicking = false; cooldown = 0f;
    }

    public void Step(float acceleration, float torque, float throttle, float clutch,
        bool validSample, bool allowKick, float deltaTime, float fullAcceleration,
        float attack, float recovery, float kickThreshold, float duration, float kickCooldown)
    {
        if (deltaTime <= 0f) return;
        if (!validSample) { Reset(torque, throttle, clutch); return; }
        float target = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(.3f, Mathf.Max(.31f, fullAcceleration), acceleration));
        float response = target > AccelerationAmount ? attack : recovery;
        AccelerationAmount = Mathf.Lerp(AccelerationAmount, target,
            1f - Mathf.Exp(-Mathf.Max(0f, response) * deltaTime));

        cooldown = Mathf.Max(0f, cooldown - deltaTime);
        float magnitude = Mathf.Abs(torque);
        bool inputHit = throttle - previousThrottle > .02f || clutch - previousClutch > .05f;
        if (allowKick && inputHit && magnitude - previousTorque >= Mathf.Max(1f, kickThreshold) && cooldown <= 0f)
        {
            kicking = true; kickElapsed = 0f; kickLength = Mathf.Max(.01f, duration);
            cooldown = Mathf.Max(kickLength, kickCooldown);
        }
        if (kicking)
        {
            kickElapsed += deltaTime;
            KickAmount = Mathf.Sin(Mathf.PI * Mathf.Clamp01(kickElapsed / kickLength));
            if (kickElapsed >= kickLength) { kicking = false; KickAmount = 0f; }
        }
        else KickAmount = 0f;
        previousTorque = magnitude; previousThrottle = throttle; previousClutch = clutch;
    }
}
