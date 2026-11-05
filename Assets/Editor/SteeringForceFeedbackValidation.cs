using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Pure FFB model checks. Never creates a vehicle or touches the Logitech SDK.
public static class SteeringForceFeedbackValidation
{
    [InitializeOnLoadMethod]
    private static void Schedule() => EditorApplication.delayCall += Run;

    [MenuItem("Stagebound/Validate FFB torque signs")]
    public static void Run()
    {
        try
        {
            var tuning = new SteeringForceFeedback.Tuning();
            var neutral = new SteeringForceFeedback.TireSample { normalLoad = 3000f };
            float Centre(float angle, float rate, float speed = 30f, float sat = 0f) =>
                SteeringForceFeedback.EstimateCenterDamping(angle, rate, speed, neutral, neutral, sat, false, tuning);
            float approach = Centre(2f, -4f);
            Require(approach > 0f && Mathf.Abs(approach + Centre(-2f, 4f)) < .0001f, "centre damper is symmetric and opposes approach");
            Require(Centre(150f, -4f) == 0f && Centre(5f, -4f) == 0f, "ordinary return outside centre window untouched");
            Require(Centre(2f, 4f) == 0f && Centre(2f, 0f) == 0f && Centre(2f, -.05f) == 0f, "no brake on outward travel, held angle or tiny movement");
            Require(Centre(0f, -4f) > 0f && Centre(-.5f, -4f) > 0f && Centre(-2f, -4f) == 0f, "soft brief crossing tail, no latched centre brake");
            Require(Centre(2f, -4f, 0f) == 0f && Centre(2f, -4f, 2f) == 0f && Centre(2f, -4f, 130f) < approach, "stationary and high-speed gates");
            Require(Centre(2f, -4f, 30f, -2f) == 0f && Centre(2f, -4f, 30f, 2f) == 0f, "meaningful SAT bypasses centre brake in both directions");
            var sliding = neutral; sliding.slipAngleDegrees = 6f;
            Require(SteeringForceFeedback.EstimateCenterDamping(2f, -4f, 30f, sliding, neutral, 0f, false, tuning) == 0f, "one slipping front tire vetoes centre damper even if SAT cancels");
            Require(SteeringForceFeedback.EstimateCenterDamping(2f, -4f, 30f, default, neutral, 0f, false, tuning) == 0f, "missing front contact bypasses damper");
            Require(Mathf.Abs(Centre(0f, -15f)) <= tuning.centerDampingLimitNm, "centre torque bounded");
            var centreTrace = new System.Text.StringBuilder("angle_deg,rate_rad_s,sat_nm,center_damping_nm\n");
            foreach (float angle in new[] { 6f, 5f, 4f, 2f, 1f, 0f, -.5f, -1f, -2f })
                centreTrace.AppendLine(FormattableString.Invariant($"{angle},-4,0,{Centre(angle, -4f):F4}"));
            Directory.CreateDirectory("Logs/FFB");
            File.WriteAllText("Logs/FFB/centre-damping-validation.csv", centreTrace.ToString());
            float delta = (150f / 11.25f) * Mathf.Deg2Rad;
            var right = SteeringForceFeedback.EstimateTire(8.333f * Mathf.Cos(delta), -8.333f * Mathf.Sin(delta), 3000f, 0f, 0f, .4f, tuning, delta * Mathf.Rad2Deg);
            var left = SteeringForceFeedback.EstimateTire(8.333f * Mathf.Cos(delta), 8.333f * Mathf.Sin(delta), 3000f, 0f, 0f, .4f, tuning, -delta * Mathf.Rad2Deg);
            Require(right.slipAngleDegrees < 0 && right.lateralForce > 0 && right.mechanicalMoment < 0 && right.pneumaticMoment < 0 && right.casterMoment < 0, "right steer must produce left torque at every moment stage");
            Require(left.aligningMoment > 0 && Mathf.Abs(left.aligningMoment + right.aligningMoment) < .001f, "left/right symmetry");
            var saturated = SteeringForceFeedback.EstimateTire(5f, -5f, 3000f, 0f, 0f, .4f, tuning, 40f);
            Require(Mathf.Abs(saturated.pneumaticMoment) < .001f && saturated.mechanicalMoment < 0f, "mechanical trail survives pneumatic collapse");
            var stopped = SteeringForceFeedback.EstimateTire(0f, 0f, 3000f, 0f, 0f, .4f, tuning, 40f);
            Require(stopped.aligningMoment == 0f, "stationary rolling fade");
            var unloaded = SteeringForceFeedback.EstimateTire(8f, -2f, 0f, 0f, 0f, .4f, tuning, 15f);
            Require(unloaded.aligningMoment == 0f, "unloaded wheel has no aligning torque");
            var following = SteeringForceFeedback.EstimateTire(8f, 2f, 3000f, 0f, 0f, .4f, tuning, 15f);
            Require(following.aligningMoment > 0f, "contact direction can overcome geometric centre bias");
            Directory.CreateDirectory("Logs/FFB");
            var scale = typeof(SteeringForceFeedback).GetMethod("ScaleMotorOutput", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            int Output(float demand, float strength, bool invert = false, float cap = .6f) => (int)scale.Invoke(null, new object[] { demand, cap, strength, invert });
            Require(Output(1f, 1f) == 60 && Output(-1f, 1f) == -60 && Output(100f, 1f, false, 1f) == 60, "hard safety ceiling");
            Require(Output(.4f, .5f) == 20 && Output(.4f, 1f) == 40 && Output(.4f, .5f, true) == -20 && Output(1f, 0f) == 0, "master and sole inversion");
            Require(Output(float.NaN, 1f) == 0, "nonfinite output rejected");
            var parked = new SteeringForceFeedback.TireSample { normalLoad = 3000f };
            float scrub = SteeringForceFeedback.EstimateScrub(parked, 1f, 11.25f, tuning);
            Require(scrub < -1f && Mathf.Abs(scrub + SteeringForceFeedback.EstimateScrub(parked, -1f, 11.25f, tuning)) < .001f, "parking scrub opposes motion symmetrically");
            Require(SteeringForceFeedback.EstimateScrub(parked, 0f, 11.25f, tuning) == 0f && SteeringForceFeedback.EstimateFriction(0f, 0f, tuning) == 0f, "no parking holding or centre force at rest");
            Require(SteeringForceFeedback.EstimateScrub(default, 1f, 11.25f, tuning) == 0f, "airborne scrub zero");
            parked.normalLoad = 1500f;
            Require(Mathf.Abs(SteeringForceFeedback.EstimateScrub(parked, 1f, 11.25f, tuning) - scrub * .5f) < .001f, "scrub follows actual load");
            parked.longitudinalVelocity = 20f / 3.6f;
            Require(SteeringForceFeedback.EstimateScrub(parked, 1f, 11.25f, tuning) == 0f, "parking scrub absent at road speed");
            Require(Mathf.Abs(SteeringForceFeedback.EstimateFriction(2f, 20f, tuning)) <= tuning.frictionNm + .001f, "fast steering drops to moving friction");
            float slowFriction = SteeringForceFeedback.EstimateFriction(.06f, 50f, tuning);
            Require(slowFriction < -1f && Mathf.Abs(slowFriction + SteeringForceFeedback.EstimateFriction(-.06f, 50f, tuning)) < .0001f, "noticeable symmetric first-motion resistance");
            Require(Mathf.Abs(slowFriction) > Mathf.Abs(SteeringForceFeedback.EstimateFriction(.7f, 50f, tuning)), "Stribeck resistance decreases once moving");
            Require(Mathf.Abs(SteeringForceFeedback.EstimateFriction(.000001f, 50f, tuning)) < .001f, "smooth zero-speed limit");
            Require(Mathf.Approximately(slowFriction, SteeringForceFeedback.EstimateFriction(.06f, 100f, tuning)), "high-speed weight does not disappear with zero tire slip");
            var frictionTrace = new System.Text.StringBuilder("wheel_rate_rad_s,breakaway_nm,coulomb_nm,total_friction_nm\n");
            foreach (float rate in new[] { -15f, -2f, -.35f, -.06f, -.001f, 0f, .001f, .06f, .35f, 2f, 15f })
            {
                SteeringForceFeedback.EstimateFrictionComponents(rate, 50f, tuning, out float stiction, out float moving);
                Require((stiction + moving) * rate <= 0f, "friction dissipates motion");
                Require(Mathf.Abs(stiction + moving) <= tuning.staticSteeringFrictionNm, "rolling friction bounded below strong SAT");
                Require(5f + stiction + moving > 0f, "representative strong SAT exceeds friction");
                frictionTrace.AppendLine(FormattableString.Invariant($"{rate},{stiction:F6},{moving:F6},{stiction + moving:F6}"));
            }
            File.WriteAllText("Logs/FFB/breakaway-validation.csv", frictionTrace.ToString());
            var old = new SteeringForceFeedback.Tuning { mechanicalTrailMetres = .020f, mechanicalFadeStartKmh = 2f, mechanicalFadeEndKmh = 12f };
            var comparison = new System.Text.StringBuilder("Synthetic: 3000 N/tire, +150 deg, ratio 11.25, 60% master. Steady commands, no damping.\nkmh,old_wheel_nm,new_wheel_nm,old_motor_pct,new_motor_pct\n");
            float previousTorque = 0f;
            foreach (float speed in new[] { 0f, 2f, 5f, 10f, 15f, 20f, 30f, 60f })
            {
                float v = speed / 3.6f;
                var before = SteeringForceFeedback.EstimateTire(v * Mathf.Cos(delta), -v * Mathf.Sin(delta), 3000f, 0f, 0f, .4f, old, delta * Mathf.Rad2Deg);
                var after = SteeringForceFeedback.EstimateTire(v * Mathf.Cos(delta), -v * Mathf.Sin(delta), 3000f, 0f, 0f, .4f, tuning, delta * Mathf.Rad2Deg);
                Require(Mathf.Abs(before.pneumaticMoment - after.pneumaticMoment) < .001f, "pneumatic feel unchanged");
                Require(Mathf.Abs(after.aligningMoment) >= previousTorque - .001f, "progressive rolling buildup");
                previousTorque = Mathf.Abs(after.aligningMoment);
                if (speed > 0f) Require(after.aligningMoment < before.aligningMoment, "stronger left return without sign change");
                float oldNm = 2f * before.aligningMoment / 11.25f;
                float newNm = 2f * after.aligningMoment / 11.25f;
                comparison.AppendLine(FormattableString.Invariant($"{speed},{oldNm:F3},{newNm:F3},{Mathf.RoundToInt(Mathf.Clamp(oldNm / 14f, -1f, 1f) * .45f * .6f * 100f)},{Output(Mathf.Clamp(newNm / tuning.referenceTorqueNm, -1f, 1f), .6f)}"));
            }
            var centre = SteeringForceFeedback.EstimateTire(8.333f, 0f, 3000f, 0f, 0f, .4f, tuning, 0f);
            Require(centre.aligningMoment == 0f, "zero return at aligned centre");
            File.WriteAllText("Logs/FFB/return-tuning-comparison.txt", comparison.ToString());
            File.WriteAllText("Logs/FFB/model-validation.txt", $"PASS {DateTime.UtcNow:O}\nRight +150deg equivalent: slip {right.slipAngleDegrees:F3}, Fy {right.lateralForce:F3}, mechanical {right.mechanicalMoment:F3}, pneumatic {right.pneumaticMoment:F3}, caster {right.casterMoment:F3} Nm.\nSynthetic model checks only; physical return NOT verified.");
            Debug.Log("FFB model sign checks passed. Physical hands-off validation remains required.");
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs/FFB");
            File.WriteAllText("Logs/FFB/model-validation.txt", $"FAIL {DateTime.UtcNow:O}\n{error}");
            Debug.LogException(error);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FFB validation failed: " + message);
    }
}
