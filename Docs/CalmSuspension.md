# Calmer suspension and weight transfer

This pass only changes suspension/weight-transfer settings in SampleScene and the matching visual-body defaults. CarController, tire curves, power/combined slip, front recovery, yaw assist, steering, drivetrain, handbrake, scoring, race/UI flow and effects scripts/settings are otherwise unchanged.

## Final tuning

| Setting | Before this pass | Final |
| --- | ---: | ---: |
| All four wheel springs | 19000 | 21000 (+10.5%) |
| All four wheel dampers | 3100 | 3900 (+25.8%) |
| Suspension travel | 0.35 m | 0.32 m |
| Suspension target | 0.50 | 0.50 |
| COM Y | -0.25 m | -0.30 m |
| Front anti-roll | 1800 | 2200 |
| Rear anti-roll | 1300 | 1700 |
| Wheel force-application distance | 0.12 m | 0.30 m |
| Visual roll strength | 0.40°/(m/s²) | 0.34°/(m/s²) |
| Visual roll limit | 3° | 2° (-33.3%) |
| Visual pitch strength | 0.18°/(m/s²) | 0.15°/(m/s²) |
| Visual pitch limit | 1.5° | 1.2° |
| Visual body spring | 70 | 70 |
| Visual body damping | 16.5 | 18 |

Mass remains 1100, angular damping 0.225, wheel radii 0.65 and all mount positions are unchanged. No Rigidbody constraints, artificial roll limits, tire-grip adjustments, heading correction or steering limits were added.

The larger damper change calms rebound while the modest spring/travel changes retain visible wheel motion. Lower COM and moderate anti-roll settings reduce roll. Increasing anti-roll alone worsened the reported inside-front loads in the comparison; it was not accepted as the whole solution. Raising the force-application point shortened the roll lever and improved those readings without changing the tire-friction curves. Unity defines forceAppPointDistance as the application point for suspension and tire forces, measured from the resting wheel base ([WheelCollider reference](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/wheelcollider)). This is an additional suspension setting worth checking when comparing the final setup to the previous version.

The visual body still uses the same damped angular spring and inertia. A damping value of 18 is slightly above the approximate critical value of 16.73 for spring 70. Maximum additional roll is reduced by one third; pitch is reduced by one fifth. Renderer hierarchy, wheel-pose updates and axle-beam following are unchanged. Telemetry still exposes local lateral/longitudinal acceleration, all four suspension compressions and loads, grounded count and physical/apparent roll/pitch.

## Paired physics comparisons

Tests used the current vehicle and unchanged handling scripts, cloned onto flat ground in isolated native physics. Corner speed is regulated near 30/60/80 km/h; the drift/transition fixtures use the same diagnostic steering controller and 70% throttle in both configurations. These are controlled comparisons, not recordings of the exact curb event in the supplied screenshot.

| Test | Before peak apparent roll | Final peak apparent roll | Reduction |
| --- | ---: | ---: | ---: |
| 30 km/h normal corner | 1.215° | 0.858° | 29.4% |
| 60 km/h corner | 3.025° | 2.023° | 33.1% |
| 80 km/h harder corner | 4.671° | 2.908° | 37.7% |
| 60 km/h seeded drift | 4.276° | 2.788° | 34.8% |
| 80 km/h seeded drift | 4.192° | 2.742° | 34.6% |
| 60 km/h left/right transition | 4.276° | 2.788° | 34.8% |

The goal was a relative amplitude reduction, not adding enough artificial lean to force every test to a particular minimum angle. These mild/controlled flat-ground fixtures remain below the requested 4–5° hard-corner reference. Large curb impacts or already-developed chassis tilt can still exceed the visual contribution limit, since the Rigidbody is deliberately not angle-clamped.

In a 15 cm vertical drop test, rebound below settled ride height decreases from 8.49 mm to 2.71 mm. Height remains within 5 mm after approximately 0.40 seconds versus 0.68 before. There is only one measured rebound larger than 1 mm in either run; the new one is much smaller. Settled root height changes by approximately 15 mm from the shorter suspension travel; no scene transforms were repositioned.

## Native Play Mode check

The final saved SampleScene was also loaded in native Play Mode in an isolated project copy with separate PlayerPrefs. The checker confirmed all final serialized settings, initialized body pivot and independent wheel meshes. Current-car clones then ran on a default-physics flat-ground fixture with real FixedUpdate scheduling. The following are the actual final results:

| Profile | Peak physical / apparent roll | Minimum FL/FR/BL/BR reported loads (N) | Contact loss |
| --- | --- | --- | --- |
| 30 km/h corner | 0.242° / 0.727° | 726 / 1405 / 2244 / 2925 | 0/230 |
| 60 km/h corner | 0.575° / 1.714° | 231 / 1735 / 1815 / 3226 | 0/230 |
| 80 km/h corner | 0.909° / 2.661° | -331 / 1222 / 1410 / 3256 | 0/230 |
| 60 km/h drift | 0.838° / 2.413° | 2040 / -264 / 4003 / 1576 | 0/230 |
| 60 km/h transition | 0.842° / 2.330° | -175 / -265 / 1783 / 1624 | 0/230 |

Ordinary 30 and 60 km/h corner checks assert positive load readings and continuous contact on all four wheels. The final 60 km/h inside-front minimum is approximately 19% of its static reported load. Harder tests still report low/negative raw WheelHit.force values at an inside front wheel, despite continuous ray contact; this pass reduces their severity, not eliminates every unloading indication. In the comparable live drift, the minimum improves from approximately -872 N to -264 N, while inside-rear minimum improves from approximately 982 N to 1576 N. Compression imbalance also decreases. Do not interpret a true GetGroundHit result alone as proof of meaningful load.

All five final Play Mode profiles passed their contact and visual-bound checks. Apparent angles differ somewhat from the manually stepped fixtures because the visual spring advances in LateUpdate while physics advances in FixedUpdate. Damping still passes through neutral with inertia; no angle snap or roll clamp was added to the chassis. Diagnostic experiments with other simulation settings were confined to the test copy; shipped vehicle substep settings remain unchanged.

## Verification and files

Runtime and editor assemblies compile with warnings treated as errors. Hash checks preserve every pre-existing runtime script except the five default numeric values in VisualBodyRoll; the actual changes there are roll/pitch strength, damping and roll/pitch limits (five fields). Scene diffs preserve all tire-friction blocks, vehicle/race object transforms and non-suspension handling settings. No validation scripts were added under the project's Assets; test sources, logs and CSVs are local under ignored Library/CalmSuspensionValidation. The copied editor cache can emit an unrelated UnityEditor.Search startup exception; no project compiler diagnostics were introduced.

Changed files: Assets/Scenes/SampleScene.unity, Assets/Scripts/VisualBodyRoll.cs and this document. Scene objects affected: CarRoot's existing COM, AntiRollBar and VisualBodyRoll settings; FL_WheelCollider, FR_WheelCollider, BL_WheelCollider and BR_WheelCollider suspension settings. No new objects or manual assignments are needed. No commit or push was made.

Stop Play Mode and reload SampleScene before evaluating the saved serialized tuning in an editor that was already open while these files changed.
