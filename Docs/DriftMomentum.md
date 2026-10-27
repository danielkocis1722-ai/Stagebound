# Drift momentum experiment

No commit or push. Production handling changes are limited to the sideways
sliding region of the two rear WheelColliders in SampleScene. Entry code,
steering/Ackermann, clutch kick, handbrake, suspension, engine torque curve,
front tires, camera, audio and effects are preserved.

## Diagnosis from native physics

Tests copied the current car into an isolated Unity preview physics scene with
flat ground. Initial speed was 85 km/h, second gear, initial body slip 28 degrees.
A test-only virtual driver controlled steering to hold approximately 25–30 degrees
using actual tire forces. Only the initial pose/velocity/yaw rate were assigned;
no body forces, velocity changes or pose corrections were applied during the run.
The virtual driver is not included in production scripts or the scene.

The old setup already avoided an immediate 85-to-40 collapse in this controlled
line. At 65% throttle it nevertheless lost speed continuously, from roughly 83
to 68 km/h during the stable portion of the eight-second run. At 75–80% throttle
it retained momentum substantially better. The user's more severe road-case loss
was not reproduced, so this is a focused improvement rather than proof that every
drift line is fixed.

During the baseline 65% run, with 7.5 seconds sampled at 20–35 degrees:

- Body slip was approximately 26–29 degrees.
- Speed was 68.3–83.0 km/h and engine RPM 5288–6035.
- Engine torque was 210–229 Nm, total rear torque 3012–3281 Nm.
- Rear forwardSlip medians were approximately BL 0.14 / BR 0.29.
- Rear sidewaysSlip medians were approximately BL -0.48 / BR -0.52.
- Front sidewaysSlip medians were approximately FL -0.22 / FR -0.27.
- Rear lateral multipliers were 1.0 and combined-slip entry weight was 0.
- The limiter factor remained 1; the engine was not dropping into a weak low-RPM band.
- Rigidbody linear damping was 0, angular damping 0.225.
- Speed-direction acceleration was approximately -0.56 m/s² median despite
  positive body-forward acceleration during the curved drift.

The data and controlled curve comparisons point to post-peak rear lateral
scrubbing work as the useful tuning target. Damping and combined slip were not
the observed speed sink. Front tires were below peak in this window; reducing
their sliding value had no measurable benefit. Raising rear forward asymptote
value alone also produced almost no change, so it was not retained. These are
comparative observations, not a direct readout of per-tire friction force.

## Exact retained change: both rear sideways curves

| Property | Before | After |
| --- | --- | --- |
| Extremum slip | 0.28 | 0.28 |
| Extremum value | 1.00 | 1.00 |
| Asymptote slip | 1.00 | 0.80 |
| Asymptote value | 0.94 | 0.88 |
| Stiffness | 1.20 | 1.20 |

Peak grip is retained. The post-peak curve reaches a slightly lower sliding
plateau sooner, reducing sliding resistance without lowering global stiffness.
There is no torque boost, artificial lateral force or direct velocity adjustment.
Tune these existing WheelCollider Inspector fields to compare the old and new
sliding region; no extra handling mode/component was added.

Rear forward friction is unchanged: extremum 0.25/1, asymptote 1/0.88,
stiffness 0.90. Front sideways friction is unchanged: extremum 0.30/1,
asymptote 0.80/0.95, stiffness 1.26. Front forward friction is also unchanged.
The 360 Nm peak engine rating, low/mid torque curve and high-RPM roll-off are
unchanged. Combined slip is retained exactly, including its 8–18 degree handoff;
no further softening was needed because its entry contribution was already zero
throughout the established-drift samples.

## Comparison and throttle authority

All cases started at 85 km/h. These speeds are at the end of eight simulated
seconds, using the same steering policy and unchanged drivetrain:

| Throttle | Before | After |
| --- | --- | --- |
| 65% | 68.3 km/h | 69.1 km/h |
| 75% | 78.7 km/h | 80.6 km/h |
| 80% | 80.9 km/h | 83.4 km/h |
| 100% | 84.1 km/h | 87.8 km/h |

The 65–100% tests each supplied 7.5 seconds within the 20–35 degree stable window
after the initial half second. Final 75% RPM was sampled across 6224–6422 RPM,
and 80% across 6441–6620. At full throttle the limiter occasionally acted, but
speed still increased. That limiter and engine curve were not modified.

Expected starting range for holding this second-gear line is about 75–80%
throttle. 65% still loses speed gradually; this pass does not claim that 65% holds
every angle/radius. At 40% the test car left the drift early, so its final speed
cannot be compared as though it had maintained the same slip angle throughout.
Use more throttle for momentum/wider line, less to shed speed/end the drift.
Actual road grade, steering and drift angle will alter the required percentage.

## Debug and validation

DrivetrainDebug now records speedAcceleration (change of speed magnitude per
physics step), linear/angular damping, and the rear asymptote values. Existing
body-forward acceleration, speed/RPM, torque, slips, grip multipliers and entry
weight remain. Capture resets cleanly when disabled, and discontinuity samples
are excluded. This is telemetry only. Positive body-forward acceleration during
a turn is not sufficient evidence that total speed is increasing; speedAcceleration
is the appropriate field for net momentum loss/gain.

Native tests validated the final serialized tire values and retained peak,
forward, steering, engine and damping settings. Controlled A/B variants isolated
rear forward, rear sideways and front sideways changes. Runtime/editor compilation
passed with warnings treated as errors. Manual Play Mode testing is still needed
for corner entry, catches, long left/right transitions and road-specific feel.
No claim is made that virtual-driver balance proves unchanged subjective handling.

Files changed in this pass:

- Assets/Scenes/SampleScene.unity (BL/BR sideways asymptote slip and value only)
- Assets/Scripts/DrivetrainDebug.cs (telemetry only)
- Docs/DriftMomentum.md (new)

Temporary test source, baseline/variant/final reports and full per-step CSVs are
kept under ignored Library/DriftMomentumValidation. The previous experimental
work remains uncommitted as well.
