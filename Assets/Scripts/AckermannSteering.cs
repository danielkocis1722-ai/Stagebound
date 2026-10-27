using UnityEngine;

public static class AckermannSteering
{
    // Input is the inside-wheel angle: the configured lock is never exceeded.
    // With both axle lines intersecting at a common turn center,
    // cot(outside) - cot(inside) = track / wheelbase.
    public static void Calculate(float insideAngle, float wheelbase, float track,
        out float leftAngle, out float rightAngle)
    {
        leftAngle = rightAngle = insideAngle;
        float magnitude = Mathf.Abs(insideAngle);
        if (magnitude < 0.001f || wheelbase <= 0.001f || track <= 0.001f) return;
        float insideRadius = wheelbase / Mathf.Tan(Mathf.Min(magnitude, 80f) * Mathf.Deg2Rad);
        float outside = Mathf.Atan(wheelbase / (insideRadius + track)) * Mathf.Rad2Deg;
        if (insideAngle > 0f) leftAngle = outside; // Right turn: FR is inside.
        else rightAngle = -outside;              // Left turn: FL is inside.
    }
}
