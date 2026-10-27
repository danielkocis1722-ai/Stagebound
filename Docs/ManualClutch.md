# Manual clutch and clutch kick (uncommitted)

Controls: DualSense Square (Input System buttonWest), or keyboard Left Shift.
The keyboard clutch also works while a gamepad is connected. ClutchInput is 0
with the button released and 1 while held. R2/L2, left stick, R1/L1 and Cross,
and existing keyboard driving/shifting controls are unchanged.

## Engagement and engine RPM

The prior automatic clutch algorithm still computes launch/shift engagement using
the original values. A separate manual engagement moves toward 1 - ClutchInput.
The single final read-only ClutchEngagement is automatic * manual. Torque and
engine/wheel RPM coupling both use that same final value. Without manual input,
the manual factor stays 1 and the automatic algorithm behaves as before.

Held clutch disconnects within three 0.02 s steps (~0.06 s); releasing it fully
reconnects within seven (~0.14 s), provided the automatic clutch is engaged.
While disconnected, rear motor torque is zero and RPM follows the existing
free-rev target from idle to redline based on throttle. Wheel RPM no longer drives
engine RPM. Throttle lift brings RPM down at the existing rev-down rate. Manual
clutch also allows free revving during a shift, while the shift still cuts wheel
torque. There is no stall simulation or requirement to use the clutch for shifts.

## Bounded clutch kick

A kick arms only after a full manual disconnection. Releasing it in a forward
gear while moving at/above launchClutchSpeed, with throttle and without shifting,
normal braking or handbrake, can start a kick if engine RPM exceeds the current
drivetrain RPM (idle is the floor for the comparison).

DrivetrainRPM = mean absolute rear wheel RPM * absolute gear ratio * final drive.
The controller captures the positive RPM surplus at release. Each step uses the
smaller of the captured surplus and current surplus. The requested extra engine-
side torque is maxEngineTorque * clutchKickStrength * Clamp01(surplus / maxRPMDifference).
It follows that target exponentially with clutchKickResponse, then fades by the
remaining fraction of clutchKickDuration. Total normal-plus-extra engine torque
is capped by maxClutchTorque before final engagement, gearing and differential.

Delivered extra torque consumes engine RPM according to effective engine inertia:
RPM drop = extraTorque * finalEngagement * dt / inertia * 60 / (2*pi).
An additional limit prevents that debit from exceeding the remaining RPM surplus.
There is no body force, yaw torque, velocity assignment, friction change or
automatic steering associated with the kick. Extra torque passes through the
existing rear differential and tire physics. Pressing the clutch again, shifting,
neutral/reverse, braking, handbrake or throttle lift cancels a kick. Ordinary
automatic clutch changes never arm it, and holding release cannot retrigger it.

## New Inspector defaults on CarRoot > CarController

| Setting | Default | Meaning |
| --- | --- | --- |
| Manual Clutch Engage Speed | 8 /s | Reconnect when releasing button |
| Manual Clutch Release Speed | 20 /s | Disconnect when pressing button |
| Max Clutch Torque | 500 Nm | Total engine-side capacity, before gearing |
| Clutch Kick Strength | 0.65 | Fraction of engine rating at maximum mismatch |
| Clutch Kick Max RPM Difference | 3000 RPM | Mismatch normalization/saturation |
| Clutch Kick Duration | 0.22 s | Maximum release transient window |
| Clutch Kick Response | 18 /s | Extra torque rise response |
| Clutch Kick Engine Inertia | 0.35 kg m² | RPM debit from transferred kick torque |

The 360 Nm engine rating, launch clutch parameters, shift duration, ratios,
experimental rear tires/LSD/handbrake, steering and suspension are unchanged.

## Debug and testing

Enable CarRoot > DrivetrainDebug > Capture Telemetry. New values show clutch
input, automatic/manual/final engagement, drivetrain RPM, RPM difference, kick
active and delivered engine-side kick torque. Existing per-wheel/axle torque,
slip, load and limiter fields remain. Optional console logging includes all of
these. No normal HUD change or new GameObject assignment is required.

At about 60 km/h in second, hold Square/Left Shift with throttle. Confirm final
engagement falls to zero, motor torque becomes zero, and engine RPM rises without
engine-driven acceleration. Release around 5000–6500 RPM while cornering. Watch
the brief kick torque and rear slip; then catch with countersteer and throttle.
Also compare ordinary automatic shifts, launch/stopping without clutch, shifting
while holding clutch, neutral free revving, and the existing handbrake.

Unity-native preview-scene validation passed: 200-step compatibility with the old
automatic algorithm, complete disconnection, zero motor torque/free revving above
6500, RPM fall despite high wheel RPM, bounded kick and RPM debit, expiry/no
retrigger, matched-RPM and partial-tap rejection, reverse/brake/handbrake/shift
gates, and extreme mismatch caps. In a fixed-wheel scenario representing 60 km/h
in second, releasing at 6500 RPM peaked at 5685.2 Nm total axle torque versus
4867.9 Nm ordinary throttle torque, with 84.7 Nm peak delivered extra engine-side
torque. These are isolated coupling checks, not a measured drift or road test.
Runtime and editor compilation passed with warnings treated as errors. Driving
feel and actual drift initiation remain manual acceptance tests.

## Files changed in this pass

- Assets/Scripts/CarController.cs
- Assets/Scripts/DrivetrainDebug.cs
- Assets/Scenes/SampleScene.unity (only new clutch settings and telemetry serialization)
- Assets/Scripts/ClutchKickCoupling.cs (new)
- Assets/Scripts/ClutchKickCoupling.cs.meta (new)
- Docs/ManualClutch.md (new)

Temporary validation code/results stay under ignored Library/ManualClutchValidation.
The preceding handling experiment remains uncommitted as well. No commit or push
was performed, and no race, HUD, respawn, pause, audio or visual-effect scripts changed.
