using UnityEngine;

// Small, stateless rear-axle helpers. No forces, yaw correction or torque creation.
public static class RearTraction
{
    public static float TargetLeftShare(bool leftGrounded, bool rightGrounded,
        float leftSlip, float rightSlip, float strength)
    {
        float bias = 0f;
        if (leftGrounded != rightGrounded)
            bias = leftGrounded ? 1f : -1f;
        else if (leftGrounded)
        {
            float left = Mathf.Abs(leftSlip), right = Mathf.Abs(rightSlip);
            float difference = right - left;
            float confidence = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.03f, 0.2f, Mathf.Abs(difference)));
            bias = difference / (left + right + 0.2f) * confidence;
        }
        // Strength 0.6 bounds the split to 20:80 .. 80:20, retaining both wheels.
        return 0.5f + 0.5f * Mathf.Clamp01(strength) * bias;
    }

    public static float Follow(float current, float target, float response, float deltaTime) =>
        Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, response) * Mathf.Max(0f, deltaTime)));

    public static float CombinedSlip(float forwardSlip, float throttle, float contactLoad,
        float referenceLoad, float start, float full, float minimum)
    {
        float slip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, Mathf.Max(start + 0.01f, full),
            Mathf.Abs(forwardSlip)));
        float power = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.85f, throttle));
        float load = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0f, 0.1f, contactLoad / Mathf.Max(1f, referenceLoad)));
        float floor = Mathf.Clamp(minimum, 0.85f, 1f);
        return Mathf.Sqrt(1f - (1f - floor * floor) * slip * power * load);
    }

    public static float PowerGrip(float forwardSlip, float contactLoad, float referenceLoad,
        float throttle, float peakSlip, float maxReduction)
    {
        // Retain the original signed model for established drift/handbrake feel.
        // Entry uses CombinedSlip with magnitude and explicit brake/clutch gates.
        float slip = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(peakSlip * 0.25f, peakSlip * 2f, -forwardSlip));
        float power = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.85f, throttle));
        float load = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0.15f, 1f, contactLoad / Mathf.Max(1f, referenceLoad)));
        return 1f - Mathf.Clamp(maxReduction, 0f, 0.12f) * slip * power * load;
    }
}
