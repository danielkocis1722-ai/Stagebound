# Neutral suspension response and controlled drifting

This pass balances suspension return rates and coordinates front/yaw/rear recovery around measured risk. It does not change base tire curves, steering geometry/input response, engine torque curve, clutch architecture, gearbox, score/credit, race/checkpoint, HUD/menu or audiovisual systems.

## Suspension equilibrium

The saved setup already had a sensible resting position. It was not necessary to keep increasing springs or shorten travel again.

| Setting | Before | After |
| --- | ---: | ---: |
| Spring FL/FR/BL/BR | 26000 | unchanged |
| Damper FL/FR | 5300 | 4250 |
| Damper BL/BR | 5300 | 5500 |
| Suspension distance | 0.28 m | unchanged |
| Target position | 0.38 | unchanged |
| Force application distance | 0.30 m | unchanged |
| COM Y | -0.30 m | unchanged |
| Rigidbody mass / linear / angular damping | 1100 / 0 / 0.225 | unchanged |
| Anti-roll front/rear | 2200 / 1700 | unchanged |
| Visual angular spring / damping | 70 / 24 | 70 / 18 |

Native flat-ground static compression, after settling:

| Wheel | Before | After |
| --- | ---: | ---: |
| FL | 38.998% | 38.624% |
| FR | 38.998% | 38.624% |
| BL | 37.968% | 38.149% |
| BR | 37.968% | 38.149% |

Unity reports supported masses of approximately 202.6 kg per front wheel and 347.4 kg per rear wheel. The estimate `critical damping = 2*sqrt(spring*supportedMass)` gives about 4590 front and 6011 rear. Uniform 5300 damping was above critical at the front (ratio about 1.15) and below it at the rear (about 0.88). New ratios are approximately 0.93/0.92. This is a per-wheel approximation, not an exact model of the coupled chassis; actual physics tests confirm the neutral position and travel use.

The adjustment addresses different front/rear return rates rather than changing equilibrium or hiding motion with Rigidbody drag/anti-roll. Normal corner, seeded drift and transition fixtures showed no grounded top-out or bottom-out before or after. There is no evidence in these fixtures that an end stop was the main cause. This does not rule out larger irregularities on the real track.

## Visual layer

No independent visual Y spring/displacement was found. The script rotates body renderer leaves around its pivot and leaves wheel meshes independent. Angular damping 24 was slower than necessary for spring 70; returning it toward the approximately 16.73 critical value makes pitch/roll return more promptly without adding a second vertical motion. Limits and strengths remain 2° roll, 1.2° pitch, 0.34/0.15 degrees per m/s².

One actual visual reset issue was corrected: an acceleration spike above the telemetry validity threshold previously incremented the same ResetVersion used for respawn, instantly resetting visual angular pose during a sharp contact impulse. Impact acceleration is still rejected, but it no longer resets the visual spring. Position discontinuities/initialization still reset it. Native checks verify impact versus teleport behavior. This changes visual/observer behavior only, not vehicle forces.

## Why recovery could feel too aggressive

The old ordinary front boost could operate in the usual drift-angle band. Deliberate recovery began at 25° and used yaw magnitude without requiring measured yaw/angle growth, and accelerated rear recovery from 1.8/sec toward 3.2/sec. Front, yaw and rear support could therefore overlap during a lift/countersteer correction. Not every fixture activated all of them: ordinary powered-hold traces already had zero front/yaw assistance, so those traces alone do not prove assist was responsible for every natural straightening event.

A larger bypass was found in rear recovery: only the power/combined-slip factor passed through the slow envelope. Released handbrake grip, which moves from 0.67 toward 1 at the existing 0.65/sec internal rate, was multiplied afterward and bypassed it. That could restore most of the rear lateral multiplier in about half a second after handbrake initiation.

## Shared controlled/risk state

FrontAxleRecovery now owns the assessment used by the front boost, rear return rate and yaw damping. It observes opening slip rate and yaw-magnitude acceleration, filtered at 6/sec.

- Controlled entry: approximately 15–30°, absolute filtered angle rate at most 10°/s and yaw acceleration at most 30°/s².
- Controlled exit uses hysteresis: below 12°, above 33°, opening rate above 18°/s or yaw acceleration above 80°/s², or loss of the motion/contact gates.
- Over-rotation risk ramps over 33–45°, requires outward yaw and increasing slip, and grows with yaw rate and positive yaw acceleration. Angle alone is insufficient.
- Deliberate recovery additionally requires countersteer at least 0.65, throttle below 0.20, forward motion and contact, with no handbrake/normal brake input.

Stable 20° and 30° drifts receive no front grip boost or yaw damping. Stable 35°/40° also receive no strong recovery merely because the angle is large. Growing angle and yaw near 40° receive support; actual strength depends on the measured rates and input, not just the angle.

Front ordinary boost cap changes from 1.06 to 1.02, deliberate cap from 1.16 to 1.10, with a 3/sec blend instead of 4/sec. Boost still waits for actual countersteer and a restoring front reaction/trajectory. Normal steering/Ackermann and the existing wheel-response multiplier are unchanged.

Yaw base coefficient decreases from 4.5 to 2.5/sec and deliberate coefficient from 5 to 3.2/sec. Both use the shared risk; the greater coefficient is applied, not their sum. Already-closing slip receives no yaw damping. The operation still removes yaw exponentially without targeting zero heading/slip, reversing yaw, writing player steering or changing linear velocity.

## Final rear-grip recovery

The recovery envelope now covers the actual final rear multiplier, including forward-drift handbrake release. Rates are:

| State | Before | After |
| --- | ---: | ---: |
| Ordinary return | 1.8/sec | 0.8/sec |
| Controlled drift | no separate rate | 0.6/sec |
| Strong deliberate recovery | up to 3.2/sec | up to 1.6/sec |

Recovery remains exponential toward the existing target. Grip-loss responses, base friction and power/combined-slip reduction amounts are unchanged. Handbrake application still uses the original locking torque, drop rate and 0.67 target. Its released forward-drift return is now smoothed instead of bypassing the envelope. Reapplying the handbrake cannot increase grip while an earlier release is still blending. Reverse handbrake release keeps its direct path while travelling backward; very low speed clears lingering recovery memory.

The handling drift latch still enters at 10° and remains down to 7°. It can now recognize a released handbrake slide before the internal handbrake multiplier has fully returned. Release blending persists past the latch's exit until restored, preventing a sudden final jump. DriftTelemetry.IsDrifting and all score/credit thresholds remain unchanged.

## Measurements and limits

Matched 70%-throttle seeded fixtures include an established slide and recent handbrake-grip state, rather than only rotating a gripping car into a sideways pose. At 60 km/h the time before slipping below 20° increases from approximately 0.76 to 3.40 seconds. With a small countersteer pulse, time below 10° moves from 2.48 to 5.70 seconds; with partial throttle reduction, from 2.38 to 5.10 seconds. These are deterministic test-driver comparisons, not a guarantee that constant throttle/steering will hold any desired angle.

The aggressive 80 km/h handbrake-seeded hold still spins under that driver's fixed 70% throttle/control policy. It requires a milder entry or appropriate recovery input. The pass improves time/control in the lower-speed fixtures without lowering base grip, changing engine power or making spins impossible; it does not claim continuous 20–30° holding across every 50–80 km/h input combination.

Native Play Mode profiles cover static equilibrium, drop, 50/70 km/h corners, transition, curb, seeded hold, partial lift and small correction. Normal profiles report zero grounded top/bottom-out samples. Initial drop airborne samples are separate from grounded end-stop counts. All profiles retain contact after initial settling in the tested conditions. State checks verify controlled 20°/30°, stable 35°/40°, growing over-rotation, front/yaw bounds, released-handbrake smoothing, unchanged handbrake locking/application, reapplication without grip gain, and impact versus teleport resets.

## Debug and scope

SuspensionTelemetry now exposes per-wheel grounded state, normalized compression, extension/travel position in metres, supported mass, raw WheelHit.force, grounded/airborne samples, top/bottom counts, accumulated durations and longest continuous end-stop durations. End stops use a 2% threshold and count grounded samples only; airborne time is recorded separately. Use its Reset Suspension Statistics context action before reproducing a route. Raw WheelHit.force can be low/negative during sharp dynamics and is not silently clamped or used to invent extra tire grip.

DrivetrainDebug adds scoring-drift state, handling latch, controlled state, shared over-rotation risk and release-recovery state alongside the existing throttle, slip, yaw, front/rear multipliers and damping fields.

Runtime/editor compilation passes with warnings treated as errors. Tests run in an isolated project copy with separate PlayerPrefs. No test code/components were added to the real Assets/scene. Existing tests/results are under ignored Library/NeutralDriftValidation.

Changed this pass: CarController.cs (rear recovery only), FrontAxleRecovery.cs, DriftYawStability.cs, SuspensionTelemetry.cs, DrivetrainDebug.cs, VisualBodyRoll.cs (damping default), SampleScene.unity (existing damping/recovery settings), and this document. No new scene objects or manual references are required. Clutch/engine/gearbox code sections, base tire curves, scene transforms and all other protected systems are preserved. No commit or push was made.

Stop Play Mode and reload SampleScene before testing saved serialized changes in an editor that was already open.
