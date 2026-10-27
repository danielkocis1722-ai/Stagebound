# Grip-to-drift experiment (uncommitted)

This pass follows C7. Engine torque stays at 360 Nm; steering, ratios, clutch,
suspension and anti-roll are unchanged. There is no added yaw force or auto-steer.
The old DriftStability component stays disabled.

## Rear tires, both BL and BR

| Property | C7 | Experiment |
| --- | --- | --- |
| Forward extremum slip | 0.40 | 0.25 |
| Forward extremum value | 1.00 | 1.00 |
| Forward asymptote slip | 0.80 | 1.00 |
| Forward asymptote value | 0.72 | 0.88 |
| Forward stiffness | 0.92 | 0.90 |
| Sideways extremum slip | 0.35 | 0.28 |
| Sideways extremum value | 1.00 | 1.00 |
| Sideways asymptote slip | 0.95 | 1.00 |
| Sideways asymptote value | 0.90 | 0.94 |
| Sideways stiffness | 1.20 | 1.20 |

The earlier peak and longer, shallower drop aim to reduce the distinction between
gripping and sliding. Forward peak-to-slide loss is now 12% rather than 28%.
Front curves and all wheel geometry remain unchanged.

Unity exposes separate [forward and sideways friction curves](https://docs.unity.com/en-us/engine/7000.0/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/friction).
Changing only forward grip does not explicitly couple it to lateral stiffness in
our controller. This experiment adds a small configurable allowance for that:
up to 8% lateral stiffness reduction per rear wheel, smoothly gated by measured
acceleration slip, throttle, and actual contact force. It never uses steering or yaw.

The power-slip blend uses SmoothStep over acceleration-slip magnitude 0.0625–0.50,
throttle 0.40–0.85, and contact load 0.15–1.00 times mass * gravity / 4. Its target
multiplier is 1 - 0.08 * slipGate * throttleGate * loadGate. Exponential response is
4/sec (time constant 0.25 s). No reduction is requested below 40% throttle, while
braking, handbraking, shifting, in neutral/reverse, or while airborne. Existing
reduction decays smoothly after those gates close. Set Power Slip Grip Reduction
to zero to compare curves/differential alone.

## Differential

Before: each rear wheel always received exactly 50% of the computed axle torque.
After: Open retains that exact behavior. LimitedSlip is the scene default, strength
0.60, response 5/sec (time constant 0.20 s). It smoothly biases drive torque toward
the grounded rear wheel with less absolute forward slip. Small slip differences
are smoothly suppressed to avoid chatter during normal cornering. A grounded wheel
receives more torque when its partner is airborne; both airborne return toward 50:50.
Maximum target split at strength 0.60 is 20:80–80:20. Equal slip means equal torque.
The two torques always sum to the original signed axle torque, including reverse.
This is an arcade torque-bias approximation, not a full mechanical LSD simulation.
There is no torque generation or chassis correction force.

## Handbrake and clutch

Handbrake target lateral multiplier: 0.55 -> 0.67.
Drop rate: 12 -> 1.1 multiplier units/sec; recovery: 3.5 -> 0.65 units/sec.
At the 0.02 s physics step, the old full drop completed in two steps (~0.04 s),
recovery in seven (~0.14 s). New full drop takes fifteen steps (~0.30 s), recovery
twenty-six (~0.52 s). The target also scales with handbrake input rather than a
binary >0.05 test. Brake torque remains 15000, rear-only; motor cut is preserved.
Any residual power-slip blend recovers while handbraking, so the effective wheel
multiplier can briefly be below the handbrake multiplier alone.

Clutch code and values are unchanged. At 50–80 km/h its normal target is 1;
partial launch engagement only applies below 15 km/h. Gear shifts still interrupt
torque for 0.20 s, followed by existing clutch recovery. Controller throttle is
direct; keyboard retains its prior smoothing. RPM coupling and soft limiter are
unchanged. This is code inspection and isolated validation, not evidence ruling
out a transient clutch/limiter issue during real driving.

## Testing in Unity

Select CarRoot > DrivetrainDebug and enable Capture Telemetry. Enable Log To
Console only when needed; interval defaults to 0.5 s. There is no main-HUD overlay.
Watch speed/throttle/RPM/gear, BL/BR motor torque and torque share, signed forward
and sideways slip, grounded state/contact load, both effective rear friction
multipliers, handbrake and its grip multiplier, clutch engagement, shifting and
limiter factor. Acceleration slip is negative in Unity.

At ~60 km/h in second, compare 40%, 70%, then 80–100% throttle in the same corner.
Check for progressive negative forward slip on both wheels and a smooth increase
in lateral slip. If one wheel spins excessively, inspect its load and the torque
bias. If total torque falls despite high throttle, inspect shifting, clutch and
limiter first. At 100–140 km/h check small corrections and normal grip. Compare
Open/LimitedSlip and power-slip reduction 0/0.08 independently before more tuning.
Test short and held handbrake presses, drift catches, transitions and throttle lift.

## Validation and scope

Unity preview-scene checks passed for serialized rear curves, one controller,
optional diagnostics, torque conservation, reverse/neutral/handbrake motor behavior,
LSD symmetry/bounds, load/slip/throttle gates, smoothing, handbrake drop/recovery,
and normal engaged-clutch state at 50, 60 and 80 km/h. Runtime and editor assemblies
compile with warnings treated as errors. These checks do not establish driving
feel, drift angle or high-speed stability; those remain manual acceptance tests.
Temporary validator and its results are kept only under ignored Library.

Changed: CarController.cs; SampleScene (controller settings, rear tire curves,
CarRoot debug component). Added: RearTraction.cs, DrivetrainDebug.cs and their
Unity metadata; this note. No race/HUD/respawn/pause/effect/audio scripts changed.
No commit or push was made.
