# Suspension settling and drift recovery refinement

This pass refines the current weight-transfer setup. It does not replace the tire curves, steering geometry, drivetrain, clutch, handbrake, scoring, race/UI flow or effects.

## Suspension

| Setting | Before | After |
| --- | ---: | ---: |
| All four wheel springs | 18000 | 19000 (+5.6%) |
| All four wheel dampers | 2600 | 3100 (+19.2%) |
| Suspension travel | 0.35 | unchanged |
| Suspension target | 0.50 | unchanged |
| Force application distance | 0.12 | unchanged |
| Mass / angular damping | 1100 / 0.225 | unchanged |
| COM Y | -0.25 | unchanged |
| Front / rear anti-roll | 1800 / 1300 | unchanged |
| Visual body spring | 70 | unchanged |
| Visual body damping | 12 | 16.5 |
| Additional visual roll / pitch limits | 3° / 1.5° | unchanged |

The visual spring damping ratio increases from approximately 0.72 to 0.99 (critical damping is about 16.73 at spring 70). The spring still integrates angular velocity with inertia; it does not snap or directly interpolate to an angle. Body and wheel hierarchy/wheel travel code are unchanged.

In the native 15 cm drop fixture, the first rebound below settled ride height decreases from 19.2 mm to 8.45 mm. The baseline has another 2.08 mm rebound; the new configuration has no second extremum above 1 mm. Both remain within 5 mm after about 0.7 seconds (baseline 0.64, new 0.68), so the improvement is less overshoot rather than a claim of faster initial return. Physical roll in the 60 km/h drift fixture remains 1.80° versus 1.86° before.

## Snap recovery diagnosis

The current car was compared at 50/60/70/80 km/h, gear 2, using actual WheelCollider physics and deterministic test-driver inputs. Strong countersteer and throttle lift start at 0.30 seconds. These are seeded flat-ground comparisons, not a recording of the user's exact road manoeuvre.

At 60 km/h the initial configuration closes the drift at a peak 61.7 degrees/sec. Yaw stabilization is zero throughout the recovery; disabling it produces identical results. Removing front recovery changes that rate only slightly. Thus the reproduced snap is mainly the physical tire/steering recovery, not a recentering torque from the yaw assist.

Two code paths make that response less forgiving:

1. The original power-grip helper uses only negative forward slip. Positive powered WheelHit samples therefore often leave the rear at full grip during the slide. In the original hold tests, the rear multiplier remains 1.0 at 60–80 km/h despite measured driven-wheel slip.
2. Power and combined-slip states blend internally at 4/sec and 8/sec, but the final steering/angle blend has no recovery envelope. Changing that blend can bypass those internal rates. The front boost can add a little extra bite while the player is only adjusting a drift line.

## Rear recovery and sustained drift

CarController uses magnitude of measured forward slip only while a handling drift is latched. It retains the existing maximum power-grip reduction of 0.08, load weighting, throttle weighting and power/combined-slip models. Normal-driving signed-slip behavior remains unchanged. No new constant rear-grip reduction or artificial drift torque was added.

The final non-handbrake rear multiplier now has an Inspector setting `rearGripRecoveryResponse = 1.8/sec`. Grip loss retains the previous internal 4/sec power-slip and 8/sec combined-slip responses. Any final multiplier increase follows:

```
newGrip = Lerp(currentGrip, targetGrip, 1 - exp(-1.8 * fixedDeltaTime))
```

This also covers recovery caused by changing steering/angle blend weights. Existing handbrake input, torque, grip drop/recovery settings and the handbrake multiplier path are preserved; while that path is active/recovering it bypasses the new envelope. `CurrentRearGripRecovery` exposes the actual positive multiplier change per second.

Handling-only hysteresis enters at 10° and remains latched down to 7°, with forward motion, speed, grounded rear wheels and normal powered-driving gates. `DriftGripHeld` is visible in Inspector. This does not alter DriftTelemetry.IsDrifting or any score/credit eligibility thresholds. After the latch ends, the final grip envelope still prevents an immediate jump back to full grip.

## Front recovery and anti-spin

- Front temporary boost cap: 1.12 to 1.06.
- Front boost blend response: 6/sec to 4/sec.
- Extra front grip now fades in between 0.25 and 0.65 countersteer input; small line adjustments keep baseline front grip.
- Existing front tire curves, Ackermann geometry and recovery wheel-response multiplier (up to 1.2) are preserved.
- Yaw assist angle ramp: 25–42° to 30–45°.
- Low-yaw-acceleration damping fraction: 0.25 to 0.10.
- Correct-countersteer damping bonus: 20% to 10%.
- Maximum danger-zone yaw damping coefficient stays 4.5/sec before input weighting.

The assist still requires an opening slip angle and outward yaw, so steady large-angle drifts and already-recovering slides have no yaw damping. The full 15–30° controlled-drift band is now free of this assist, even with temporarily growing yaw. No heading alignment or recentering torque was added.

## Measurements and practical limits

With the same diagnostic steering controller and 70% throttle, the new 60/70/80 km/h hold fixtures each remain in the 20–30° band for all 200 samples of the four-second observation window. They have zero yaw damping and baseline 1.0 front multiplier. The old 60–80 fixtures also pass with this controller; the improvement is a more consistent powered-slip response, less closing tendency and less assistance, not a claim that drifting was previously impossible. Final speeds after the seven-second runs are approximately 59/64/68 km/h. The diagnostic controller adjusts steering; no automatic steering exists in the game.

At 60 km/h, the partial-lift fixture's maximum angle-closing rate decreases from 13.0 to 8.5 degrees/sec. With strong countersteer and full lift it decreases from 61.7 to 57.0 degrees/sec; the 20° to 5° portion takes about 0.46 seconds versus 0.34 before. The car can still recover quickly under strong input.

The 50 km/h seeded hold is less consistent: at 70% throttle it spends much of the window slightly below 20°, and 75% throttle improves but does not fully eliminate that undershoot. It remains near the requested range with usable speed, but the automated fixture does not prove equally easy holding at every speed. Human testing should judge this experimental pass before further grip changes. No engine torque was increased to hide that limitation.

Native SampleScene Play Mode checks verify the final spring/damper/visual settings, unchanged travel/COM/anti-roll, controlled-drift yaw exclusion, symmetric progressive danger-zone support, grip-envelope formula, unchanged grip-loss response, handbrake bypass, handling hysteresis independent of scoring, pause and teleport guards. Runtime and editor assemblies compile with warnings treated as errors. All native tests run in an isolated project copy with separate PlayerPrefs. Test sources, CSVs and logs are local under ignored Library/HandlingRefinementValidation; no validation scripts were added to Assets. Unity's copied editor cache continues to emit a Search index startup exception unrelated to project scripts; gameplay assertions pass.

Files changed by this pass: Assets/Scripts/CarController.cs, FrontAxleRecovery.cs, DriftYawStability.cs, VisualBodyRoll.cs, Assets/Scenes/SampleScene.unity (only relevant existing CarRoot settings and four WheelCollider spring/damper values), and this document. No scene objects, imported assets or tire-friction curves were added or changed. No commit or push was made.
