# Sporty suspension: vertical settling

This pass changes only the four WheelCollider suspension settings in SampleScene and the visual-body damping value. Every pre-existing runtime script except that one default value in VisualBodyRoll is hash-identical to its starting state. Tire curves, combined slip, drift and countersteer assists, engine/clutch/gearbox/handbrake, scoring, telemetry, race/UI and effects systems are unchanged.

## Inspection and final values

| Setting | Before | Final |
| --- | ---: | ---: |
| Spring, all four wheels | 21000 | 26000 (+23.8%) |
| Damper, all four wheels | 3900 | 5300 (+35.9%) |
| Suspension distance | 0.32 m | 0.28 m |
| Target position | 0.50 | 0.38 |
| Force application distance | 0.30 m | unchanged |
| Rigidbody mass | 1100 | unchanged |
| COM Y | -0.30 m | unchanged |
| Rigidbody linear/angular damping | 0 / 0.225 | unchanged |
| Front/rear anti-roll | 2200 / 1700 | unchanged |
| Visual body spring | 70 | unchanged |
| Visual body damping | 18 | 24 |
| Visual roll/pitch strength | 0.34 / 0.15 | unchanged |
| Additional visual roll/pitch limits | 2° / 1.2° | unchanged |

The spring increase limits displacement; the larger damper increase controls return motion. A simple heave estimate gives damping ratio approximately 0.99 for the new four-spring/four-damper combination and 1100 kg mass, versus approximately 0.81 before. This estimate ignores axle load distribution and pitch/roll coupling, so native simulation was used to select the values. A 28000/6000 candidate was also tested; it did not settle faster and was not retained.

No extra Rigidbody drag or anti-roll stiffness was used to hide the vertical response. No mount, spawn, model or other scene transforms were moved. The target-position change follows measured sag rather than blindly retaining 0.45–0.55: the previous 0.50 target used approximately 50% of travel at rest. The final target gives approximately 39% front and 38% rear, within the requested 25–40% sag range. It leaves useful travel in both directions. The settled chassis height rises by approximately 13 mm due to the combined travel/target change.

## Visual body inspection

VisualBodyRoll contains an angular spring only. Its runtime update rotates the body pivot and updates the two unsprung axle meshes. It has no separate vertical/Y-offset spring and does not add a second vertical chassis bounce. Body points can move vertically as a consequence of intended pitch/roll around the pivot; that is not duplicate heave.

Only its damping default and the corresponding scene value change, from 18 to 24. The spring integration, pivot, wheel hierarchy and wheel-pose updates are unchanged. Roll, pitch and inertia remain; the visual contribution is more damped. No camera or handling smoothing was changed.

## Physics comparisons

The current car and all current handling scripts were used in paired isolated native WheelCollider fixtures. Profiles covered a 15 cm standing drop, regulated 50/60/70 km/h corners, left/right drift transition, a 12 cm one-side curb, braking and acceleration.

- Static normalized compression: before FL/FR 0.5059, BL/BR 0.5008; after FL/FR 0.3900, BL/BR 0.3797.
- Drop rebound below settled height: approximately 2.77 mm before, 0.21 mm after. The final test has no rebound larger than 1 mm.
- Drop settling within 5 mm: approximately 0.40 seconds before, 0.38 seconds after.
- Peak apparent roll in the 60 km/h corner: 2.02° before, 2.12° after; weight-transfer feedback remains visible.
- Peak apparent roll in the transition: 2.79° before, 2.70° after.
- The baseline controlled flat-ground fixtures did not reproduce prolonged repeated hopping. The measured improvement is a smaller rebound and tighter displacement response; this is not a claim that every irregularity of the real track has been reproduced.

Final saved-scene configuration was also checked in native Play Mode using the default physics scene in an isolated project copy with separate PlayerPrefs. It passed the drop/standing, 50 km/h corner, 70 km/h corner, left/right transition and curb profiles. All wheels retained contact after the initial drop impact/settling period, and visual angular limits were respected.

Native resting compression was FL/FR 0.38998 and BL/BR 0.37968. The native drop remained within 5 mm after approximately 0.40 seconds, with only 0.32 mm undershoot below resting height. Thus the result is a tiny controlled rebound, not literally zero motion. Final-second height ranges were approximately 0.014 mm in the 50 km/h corner, 0.605 mm in the 70 km/h corner, 1.21 mm during the transition's final settling portion, and 0.006 mm after the curb. WheelHit.force can report low/negative instantaneous values during the high-damping dynamic profiles; contact and height measurements are not presented as a guarantee of uniform tire loading.

## Verification and changed files

Runtime and editor assemblies compile with warnings treated as errors. Scene comparison permits only spring, damper, travel, target position and visual damping differences. All drift/tire, assist, steering, drivetrain and other runtime source hashes are preserved. Test sources/results remain under ignored Library/TightSuspensionValidation and in the isolated test copy; no validation scripts/components were added to the real Assets or scene.

Changed files: Assets/Scenes/SampleScene.unity; Assets/Scripts/VisualBodyRoll.cs (one damping default); this document. Scene objects affected: FL_WheelCollider, FR_WheelCollider, BL_WheelCollider, BR_WheelCollider and CarRoot's existing VisualBodyRoll settings. No new objects, assets or manual assignments are required.

No commit or push was made. Stop Play Mode and reload SampleScene before testing the saved tuning in an editor that was already open while the files changed.
