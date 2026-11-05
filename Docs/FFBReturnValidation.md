# Steering return revision — physical acceptance pending

This supersedes the baseline-only status in FFBBaseline.md. User evidence:
approximately 14.9 Nm SAT and 34% command did not unwind the physical wheel;
at roughly 7 km/h, inversion produced approximately -1% command from 0.15 Nm.
Neither observation contains the measured raw-command physical direction or
per-tire state needed to establish a root cause. No CSV was present at inspection.

## Findings and exact sign paths

1. A 34% accepted command can still fail if its physical direction is wrong,
   if the contact-state torque no longer aligns toward centre, or if other device
   effects/mechanical resistance dominate. These are hypotheses, not established
   causes. Missing explicit caster geometry was confirmed in code, but adding it
   is not proof that the observed failure is fixed. Gain was not increased.
2. Old path: relative GetPointVelocity(contact) projected onto WheelHit.forwardDir
   and cross(normal, forward); alpha=atan2(vRight,max(0.4,abs(vForward)));
   Fy=clamp(-corneringStiffnessPerLoad*Fz*alpha,+/-frictionLimit);
   M=-Fy*(mechanicalTrail+pneumaticTrail*slipCurve)*speedFade;
   sum front moments / steering ratio * aligningStrength;
   add movement damping/friction; /14 Nm * vehicleStrength; clamp/filter;
   *maximumOutput*master; optionally negate through Invert; round to SDK percent.
3. Revised path preserves the contact calculation and output mapping, but replaces
   the moment with the three separately exposed components below. For straight
   forward travel and right steer: vRight negative, alpha negative, Fy positive,
   pneumatic and mechanical moments negative, geometric caster moment negative,
   rack and wheel SAT negative (LEFT). Left steer mirrors every sign. During a
   bend the target is contact travel direction, which need not be body centre.
4. Raw positive/negative SDK physical directions remain UNKNOWN until observed.
   The wrapper passes the signed integer to LogiPlayConstantForce unchanged.
   Inspector confirmation records the observed positive direction for this
   component/session and sets the existing Invert setting: + means physical
   right => invert off; + means physical left => invert on. There is no second
   inversion. Existing Invert remains editable. Reconfirm after a new session.
   “Actual motor direction” means the mapped accepted command, not measured
   motor torque or proof of wheel motion. Do not treat it as hardware sensing.

Unity documents forwardDir as the direction the wheel points:
https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/wheelhit
No extra steer rotation is applied to that already-steered direction.

## Component formulas (per tire, before rack ratio)

5. Pneumatic: -Fy * pneumaticTrail * slipCurve(abs(alpha)) * cos(caster) * fade.
6. Mechanical trail: -Fy * mechanicalTrail * cos(caster) * fade.
7. Caster jacking approximation: -Fz * mechanicalTrail * sin(caster) * sin(steer)
   * fade. This is the negative derivative of the simplified potential energy
   Fz*trail*sin(caster)*(1-cos(steer)); it assumes a restoring jacking geometry.
   It is not a complete suspension/kingpin/scrub-radius model or measured car
   geometry. Default caster is an exposed, provisional 6 degrees; trail stays
   12 mm. Both are in each vehicle's existing feedback tuning. Caster is a small
   centre-biased contribution; the force-derived trail terms remain dominant.
8. Only pneumatic torque uses the slip curve. All components use the existing
   SmoothStep rolling fade from 3 to 20 km/h. Grounded load is WheelHit.force.
9. Damping remains -0.08*filteredPhysicalWheelAngularVelocity Nm. At a held angle
   it decays to zero; it does not depend on angle. A just-stopped wheel retains a
   brief velocity-filter transient. No new position-based damping was added.
10. Friction remains -0.04*clamp(angularVelocity/0.3,-1,1) Nm, zero at rest.
    Output normalization, master, 0.45 default multiplier and 60% hard ceiling
    are unchanged. “Filtered torque” is the low-pass raw Nm diagnostic before
    normalization/clipping; filtered normalized output shows the actual path.

## Required physical validation

11–13. Both +150 and -150 degree hands-off results and actual return command
percentages are PENDING. Observe the raw +15% pulse on a stationary car, confirm
its direction in the wheel inspector, and re-enable FFB (the existing pulse test
disarms it). Keep the same master strength. At straight forward 30 km/h use F8
to record, F9 exactly at release, F10 to save; repeat the opposite direction in
a separate recording. The CSV and summary go to Logs/FFB. Summary checkpoints
are 0.25, 0.5 and 1.0 seconds, with actual sample times. Missing or over-100ms-late
checkpoints are NaN, not fabricated successes. F9 is an operator assertion of
release, not a hands-off sensor. CSV driving validity must be checked.

Expected/actual direction and YES/NO are shown in the developer inspector.
Validity is N/A outside near-straight forward rolling at 25–35 km/h or near
centre, UNKNOWN without an observed polarity. No motor direction is inferred
from the command sign alone. A YES only verifies sign, not physical return.

14. Normal-corner unwinding remains UNTESTED.
15. Drift has NOT been tested or tuned; wait until both straight-return tests
    pass physically, then validate normal cornering before drift.
16. This revision changes SteeringForceFeedback.cs, LogitechWheelInputInspector.cs,
    adds SteeringForceFeedbackValidation.cs and its Unity .meta, and adds this
    report. No handling/input/settings implementation changed. No commit/push.

## Software validation

Runtime and editor sources compiled using Unity's bundled Roslyn compiler and
the project's generated reference/define response files, with outputs isolated
in Logs/FFB/compile. Synthetic Unity checks cover mirrored sign paths, pneumatic
collapse with surviving mechanical trail, zero-speed fade, unloaded wheels and
contact-direction torque overcoming the geometric centre bias. They run after
editor reload or via Stagebound > Validate FFB torque signs and write
Logs/FFB/model-validation.txt. Unity executed these checks successfully at
2026-11-04 20:31:05 UTC. With 3000 N load per tire and the +150-degree equivalent,
each tire produced slip -13.333 degrees, Fy +3300 N, mechanical -39.383 Nm,
pneumatic -23.221 Nm and caster -0.868 Nm. These are synthetic inputs, not the
user's car telemetry. Physical self-return remains unverified.
