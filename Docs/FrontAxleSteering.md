# Front axle / steering geometry experiment

No commit or push. This pass changes steering geometry and global lock only.
It preserves the existing rear tire curves, combined-slip entry and drift-hold
model, differential, engine torque, clutch/kick, handbrake, suspension, steering
input curve/slew rates, effects, audio, HUD and race systems.

## Measured original scenario

The requested scenario was reproduced using a copy of the CURRENT SampleScene
car, a separate Unity preview physics scene, flat ground, gear 2, initial speed
70 km/h, full right stick, full throttle and a manual clutch press/release.
WheelHit samples below are medians in the 0.70–1.10 s release window; speed in
that window was approximately 69 km/h. These are actual native physics samples,
not inferred slips or a human-driven road test.

| Telemetry | Original parallel 34° | Ackermann 30° |
| --- | --- | --- |
| FL sidewaysSlip | -0.63 | -0.45 |
| FR sidewaysSlip | -0.63 | -0.58 |
| BL sidewaysSlip | -0.21 | -0.28 |
| BR sidewaysSlip | -0.24 | -0.33 |
| FL forwardSlip | approximately 0 | approximately 0 |
| FR forwardSlip | approximately 0 | approximately 0.01 |
| BL forwardSlip | 0.18 | 0.18 |
| BR forwardSlip | 0.50 | 0.51 |
| Yaw rate | 34.56°/s | 51.61°/s |
| Body/velocity slip angle | -10.26° | -13.61° |
| Actual FL steerAngle | 34.00° | 23.12° |
| Actual FR steerAngle | 34.00° | 30.00° |

Front slip exceeded its 0.30 peak well before rear lateral slip exceeded its 0.28
peak. The rear wheels did have substantial forward slip, especially BR. Thus the
test supports front saturation and excessive outside-wheel steering as a cause;
it does not support another blind power increase. The original car still produced
yaw, so "zero yaw" is not claimed. Both front tires can still exceed peak slip at
full lock after this change; the improvement is reduced scrub and more rotation,
not physically unlimited cornering force.

## Geometry and full lock

The current collider centers give a wheelbase of 5.261 m and front track 3.210 m.
The controller derives these from all four WheelCollider centers in world space,
projected onto the vehicle's forward/right axes. No guessed geometry or wheel
position edits are used. Geometry is recalculated while steering so moving the
colliders updates it automatically.

The existing smoothed steering command is treated as the INSIDE wheel angle:

```
insideRadius = wheelbase / tan(abs(insideAngle))
outsideAngle = atan(wheelbase / (insideRadius + frontTrack))
```

Positive steering is right: FR inside, FL outside. Negative steering is left:
FL inside, FR outside. The mapping is symmetric, continuous about center and
approaches equal angles as steering approaches zero. Degenerate geometry safely
falls back to parallel angles. The configured maximum is never exceeded.

Global maxSteerAngle changes 34 -> 30 degrees. At full right input the result is
FL +23.120 / FR +30.000 degrees. Full left is FL -30.000 / FR -23.120 degrees.
Full stick always reaches the configured 30-degree INSIDE-wheel lock at every
speed, after the original slew rate. There is no speed-based angle cap. The old
SteeringAngle telemetry field now represents this inside-wheel command. A public
Use Ackermann Steering toggle permits comparison with parallel steering; the
scene default is enabled.

At 70 km/h, even the ideal low-speed turn circle for full 30-degree Ackermann lock
would demand roughly 3.6 g lateral acceleration. Normal tires cannot maintain
that circle at that speed. Medium input remains the correct way to request a
sustainable corner; full lock stays available for tight turns and countersteer.

## Front friction: intentionally unchanged in this pass

Both FL/FR sideways curves remain:

| Property | Before | After |
| --- | --- | --- |
| Extremum slip | 0.30 | 0.30 |
| Extremum value | 1.00 | 1.00 |
| Asymptote slip | 0.80 | 0.80 |
| Asymptote value | 0.95 | 0.95 |
| Stiffness | 1.26 | 1.26 |

The sliding value was already at the requested upper baseline. Geometry/lock
improved the measured result without increasing grip further. All rear friction
and combined-slip values are unchanged, as are COM, force points and suspension.

## Verification and limits

Native tests compared parallel 34, parallel 30, Ackermann 34 and Ackermann 30 with
the same tire/drivetrain configuration and identical zero-velocity/zero-wheel-
torque settling conditions. Sweeps verified left/right symmetry, common turn
center, valid bounded angles and full configured lock at 0, 40, 70, 100, 140 and
200 km/h. Runtime/editor assemblies compile with warnings treated as errors.

At 60 km/h initial speed with 55% raw stick and full throttle, the Ackermann-30
clutch-kick scenario reached about 20.6 degrees of body slip at 1.86 s / 76.4 km/h,
without handbrake. Actual steering then was only FL 8.25 / FR 9.04 degrees.
Normal throttle in the same corner also crossed 20 degrees. At 70 km/h initial
speed, the medium-stick clutch-kick scenario crossed 20 degrees near 83 km/h.
Not every medium-stick corner initiates a drift: the 45% test stayed near grip.

These fixed-input tests deliberately hold steering into the corner; they can
eventually spin because no driver catches the drift. They establish that rear
breakaway is possible without handbrake/full lock, not that sustained drift or
road handling feel is verified. The pulse was not consistently faster than normal
throttle in these tests. The unchanged clutch/kick should be evaluated manually
with different release RPM and countersteer timing before any further tuning.

DrivetrainDebug adds front forward slip, actual FL/FR steer angles, measured
wheelbase and track. Existing front/rear sideways slip, torque, kick, yaw, body
angle, load and combined-slip fields remain. Enable Capture Telemetry on CarRoot.
No manual component assignment is required.

## Files changed in this pass

- Assets/Scripts/CarController.cs
- Assets/Scripts/DrivetrainDebug.cs
- Assets/Scripts/AckermannSteering.cs and .meta (new)
- Assets/Scenes/SampleScene.unity (only maxSteerAngle and Ackermann enable)
- Docs/FrontAxleSteering.md (new)

Temporary validators, original/variant reports and full per-step CSV samples are
retained under ignored Library/FrontAxleValidation, outside the project Assets.
The preceding experimental work also remains uncommitted.
