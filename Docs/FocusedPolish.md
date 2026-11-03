# Focused track and driving polish — 2026-11-01

> Historical report. The experimental branch now uses the full metre-scale migration documented in [RealScaleMigration.md](RealScaleMigration.md). Values and instructions below describe the earlier state.

Working tree changes only. Nothing committed or pushed. This report supersedes
the scale/material findings in `NewTracksAndSteering.md`; the existing integration
and stage structure remain in place.

## Scale and source inspection

The current cars are intentionally retained as the world reference. Their visual
envelopes, measured from actual transformed vertices, are approximately AE86
4.05 m wide / 9.31 m long, R32 4.11 / 9.96 m, and RX-7 4.15 / 9.39 m.
Their wheelbases are 5.261, 5.758, and 5.345 m. The earlier bounding-box envelope
estimates were slightly wider (4.10 / 4.16 / 4.20 m).
These use the established approximately 2.192 world-units-per-nominal-metre
convention documented in Vehicles.md (and confirmed by the actual wheelbases).
They are oversized relative to real passenger cars. Scaling the new roads to
real-world lane widths without accounting for that would reproduce the mismatch.
No car transforms, wheel sizes, mass, COM, inertia, or suspension settings changed.

Cherry Mountain was enlarged uniformly by **1.8** relative to the previous scene;
Drift Tracks by **1.6**. The source FBX meshes are unchanged. ImportedTrack scale
and offset carry the visual change. The existing 7 Cherry and 10 Drift static
collision mesh copies were rebaked with the same multiplier, preserving unit,
positive collider transforms. No overlapping second road collider was added.

All route guide positions, gates and authored respawn points were repositioned.
Gate spans are now 23.4 m / 20.8 m. Gate height remains 5 m and thickness 1.5 m.
Spawn and checkpoint respawn clearance remains 1.25 world metres above the newly
scaled road surface, rather than multiplying the clearance along with the map.

Cherry's SpawnPoint is now (100.008, 361.018, -1094.075); Drift's is
(-58.702, 1.260, -148.689). Both keep the route-facing rotation and road pitch.
Cherry remains point-to-point with 9 checkpoints and distinct Start and Finish;
Drift remains a circuit with 6 checkpoints and a StartFinish gate.

The previous collider-based surface widths were approximately 9.5 m and 11–11.25 m.
The finer 0.125 m cross-section sampling after scaling reports medians 17.125 m
and 18.25 m. These surface widths must not be mistaken for asphalt-only widths;
textured cross-sections and lane views are inspected separately below.
At the established world convention these correspond to approximately 7.8 m
and 8.3 m nominal road widths, appropriate to a two-lane mountain road and a wider
drift circuit. They are not a claim of 4 m wide real passenger cars.

The recovered texture exposed a second issue: some Cherry guide points were
offset toward the verge by up to 3.002 m. All 1,119 guide points were centred
using asphalt atlas regions, excluding the stone/grass regions; gate and respawn
positions/rotations were then updated to that centred route.

| Representative usable pavement | Original world width | Corrected world width | Multiplier |
| --- | ---: | ---: | ---: |
| Cherry, inside the white edge lines | about 8.44 m | about 15.20 m | 1.8 |
| Drift, asphalt between curb boundaries | about 11.4 m | about 18.25 m | 1.6 |

Cherry's measurement uses 35 native collision ray hits and interpolated UVs on
the straight at route point 100. Image_7's edge-line pixels are rows 525–526 and
753–754; the inner pavement boundaries use rows 527 and 752. Their UV distance
divided by the measured 0.0144602 UV/world-metre cross-slope gives 15.195 m.
This excludes paved shoulders outside the lines and is not a mesh-bounds estimate.
The corrected Cherry lanes are about 7.60 world units each, approximately 3.47
nominal metres under the established car scale. Drift is intentionally wider.
All six car/track combinations were inspected in top-down rendered lane views:
each car fits within one lane with visible clearance. These are placement views,
not a claim of manual driving. Vertex comparisons confirmed all 17 colliders
match their visual source within 0.0003 m after scaling.

Stage IDs and per-car record separation are retained. Both changed stages advance
recordVersion from 1 to 2 so old, shorter-course times are not compared against
the enlarged routes; old saved keys are not deleted. Style Credit remains
2,000 points per second, capped at 8 seconds. South Loop and TrackComple geometry,
materials, scale, gates, records and spawns were left untouched.

## Cherry original materials

Source: `C:\Users\dandy\Downloads\cherry_mountain_-_super_drift_3d.glb`.
All eight images are embedded GLB BIN bufferViews, not external image paths.
Their bytes were extracted directly, with a byte-for-byte comparison against the
source GLB. No replacement images, download, Blender export, or addon was used.

| Recovered file | Original material |
| --- | --- |
| Image_0.png | 01_-_Default.099_4 |
| Image_1.png | 01_-_Default.099_1 |
| Image_2.png | 01_-_Default.099_2 |
| Image_3.jpg | 01_-_Default.099_3 |
| Image_4.png | 01_-_Default.099 |
| Image_5.png | 01_-_Default.099_0 |
| Image_6.jpg | Material.003_0 |
| Image_7.jpg | Material.003 |

Files live under `Assets/Stages/CherryMountain/Textures`. The eight existing URP
Lit materials retain their asset identities and importer remaps, with white base
colour tint and the original image assignment. The source has no normal, metallic,
roughness image, or emission texture. Metallic factor is zero; roughness defaults
to 1 for the first six materials and is 0.8011823 for the last two. URP smoothness
is therefore 0 and 0.1988177 respectively. Both original BLEND materials retain
alpha blending, and all eight source materials are double-sided.

No manual Blender action is required. South Loop's embedded materials and Drift
Tracks' 14 working URP materials were preserved.

## Steering and reverse investigation

The actual baseline maximum was **30 degrees on all three configs**, rather than
the approximate 34 degrees in the request. The new editable low-speed maximum
is **40 degrees up to 30 km/h**, smoothly interpolating to **30 degrees by 80 km/h**.
At 55 km/h the limit is 35 degrees. Speed is Rigidbody velocity magnitude, so
reverse travel receives the same limit as forward travel at the same speed.

The existing analog deadzone, 1.4-to-2.1 speed-dependent stick exponent, Ackermann
calculation and angle slew/return rates remain intact. At 80–120 km/h the target
wheel angles are mathematically identical to baseline for the same input and speed.
Visual wheels still obtain poses directly from WheelCollider.GetWorldPose.

The yaw governor was inspected and A/B tested enabled/disabled. It requires:
grounded tires, rear slip, signed forward speed above 15 km/h, increasing outward
slip angle, and outward yaw. Its weights ramp at 28–40 km/h speed, 6–14 degree
angle, 45–95 deg/s yaw and 10–35 deg/s angle growth. It only removes excess yaw
above 45 deg/s. It cannot activate in sustained backward travel or below 28 km/h.
It can oppose a player's intentional *forward opening drift* under those gates,
which is why it is retained for the existing good high-speed handling.

In the reproduced short-approach reverse sequence, removing the governor changed
rotation by less than about a degree. Front steering remained active, wheel RPM
was negative, and front forward/side slip and contact loads were sampled. The
rear handbrake produced zero RPM and approximately +0.98 forward slip, while
the rear tires retained substantial lateral grip. It consumed reverse momentum.

First gear plus throttle continues to deliver nonnegative forward motor torque
while wheels are moving backward. Braking backward motion toward zero before
accelerating forward is expected; there is no RPM-sign gear-direction lock.
The old automated J-turn test waited for 140 degrees of yaw before pulsing the
handbrake. That test did not establish success for the user's short sustained
sequence and is not used as evidence that this issue is solved.

The only physical reverse-related change in this pass is the requested larger
low-speed steering lock. No yaw governor deletion, friction multiplier, steering
assist, angular torque, transform rotation, or velocity redirection was introduced.
There is also a scale constraint: the measured wheelbases are 5.3–5.8 world metres,
while SpeedKmh is raw world velocity times 3.6. A displayed 30 km/h therefore
travels fewer car lengths per second than a real passenger car at 30 km/h. Under
the established 2.192 convention it is about 13.7 nominal km/h. The kinematic
turn rate v / wheelbase * tan(steer) is consequently lower. This is a measured
scale/speed mismatch that contributes to a slow-looking reversal, not proof that
it is the sole cause. Changing car scale, global speed readout, inertia or high-speed
handling to compensate was intentionally outside this focused pass.
The stock WheelCollider has separate forward and lateral friction curves; an
accurate combined-force model for locked rear tires would also affect the currently
good forward handbrake/drift balance. No such broad change was made speculatively.

**Reverse J-turn recovery remains a manual acceptance item, not a claimed fix.**
The quick steering/short-handbrake sequence still does not reliably produce a
180-degree reversal. Sustained full-steering approaches are reported separately
from that failure; successful forward acceleration alone is not a passed J-turn.

Automatic inertia tensors (principal kg m²) remain:
AE86 (1588.583, 1763.667, 418.917), R32 (11584.920, 11357.790, 3304.866),
RX-7 (8634.643, 8805.931, 2546.834). No theoretical box override was applied.

## Camera

Baseline: speed-dependent 62–73 degree FOV and 7.5–8.4 m follow distance, across
0–160 km/h, plus existing measured-acceleration feedback.

Now: stationary base FOV 60 degrees and distance 7.0 m. The extra 2-degree and
0.5 m proximity reductions smoothly vanish by 80 km/h, restoring the exact old
speed targets above that speed. At 100 km/h the cruise targets remain 68.875
degrees and 8.0625 m. Existing acceleration additions remain up to 3 degrees and
0.5 m, driven by the smoothed actual longitudinal change in Rigidbody velocity.
Their full-response threshold is 6 m/s²; no throttle-to-zoom mapping was added.

The existing brief drivetrain hit (0.06 m / 1.25 degrees maximum) now additionally
requires measured positive acceleration above 0.3 m/s², preventing a torque/input
hit from zooming a stationary, nonaccelerating car. Combined upper bounds are
77.25 degrees / 8.96 m if speed, acceleration and the transient all peak together;
ordinary speed-plus-acceleration maxima are 76 degrees / 8.9 m.

Acceleration attack/recovery rates remain 10 / 5 per second; the underlying
longitudinal sample is filtered at 14/s. Position, look and FOV sharpness remain
7 / 5 / 4 per second; existing drift look smoothing is retained. Braking offset
is now smoothed at the recovery rate rather than following each raw target step;
the existing 0.3 m scene cap remains, with no sudden zoom-forward command.

## Service brakes

Baseline: 8,000 Nm **per wheel**, equal front/rear, linear brake input. Native
100 km/h tests show essentially immediate zero wheel RPM and roughly -1 forward
slip. The front longitudinal curve falls from extremumValue 1 to asymptoteValue
0.5, making prolonged lock particularly costly. Torque tuning is sufficient to
improve braking; the tire curves were not changed.

| Car | New configured per-wheel base | Front share | Front / rear full service Nm |
| --- | ---: | ---: | ---: |
| AE86 | 1,600 | 52% | 1,664 / 1,536 |
| R32 | 2,250 | 55% | 2,475 / 2,025 |
| RX-7 | 2,000 | 55% | 2,200 / 1,800 |

The bias parameter is exposed per config. 50% preserves the old per-wheel torque
meaning. Input remains progressive and linear; rear handbrake torque remains
15,000 Nm and is added independently. Engine braking is also unchanged.
No ABS, tire grip increase, sideways-friction change, artificial load transfer,
spring/damper/COM alteration, engine power or clutch change was added.

The final repeatable native test starts at exactly 100 km/h after letting wheel
RPM settle near rolling speed, brakes in neutral on a flat surface, and ends
below 0.5 m/s (1.8 km/h). The old torque/bias is compared with the new settings
in the same run; no forces/velocity corrections are applied during braking.

| Car | Old distance | New distance | Old / new time | Old / new prolonged-lock samples |
| --- | ---: | ---: | ---: | ---: |
| AE86 | 50.63 m | 44.90 m | 3.60 / 3.18 s | 99.0% / 0% |
| R32 | 50.99 m | 47.06 m | 3.62 / 3.33 s | 98.7% / 0% |
| RX-7 | 50.56 m | 46.23 m | 3.58 / 3.32 s | 98.5% / 0% |

Lock metric: while vehicle speed exceeds 10 km/h, a grounded wheel sample has
either fewer than 10 absolute RPM or greater than 1 absolute forward slip.
Mean absolute slip fell from about 0.98 to 0.250 / 0.232 / 0.253. This establishes
reduced straight-line lock, not immunity to lock on unloaded tires in a turn.
An initial 55% front-bias AE86 tuning attempt still locked and worsened distance;
it was rejected, and the final 52% / 1,600 Nm tuning above was re-tested.

The final sustained reverse test uses roughly 33 km/h backward rolling speed,
2 seconds of full steering, 0.3 seconds of rear handbrake, then first gear and
throttle. Recovery input is tested both in the same direction and opposite for
2 seconds before returning to neutral. Maximum measured rotations were:

| Car | Same recovery steering | Opposite recovery steering |
| --- | ---: | ---: |
| AE86 | 138.29 degrees | 154.83 degrees |
| R32 | 122.13 degrees | 117.10 degrees |
| RX-7 | 129.03 degrees | 140.49 degrees |

All accelerate forward in their resulting heading; none of these figures is
presented as a clean 180-degree J-turn. The shorter 0.4-second approach / 0.2–0.7
second handbrake sweep remains inadequate and is retained as negative evidence.
The test only seeds initial velocity; steering, braking and recovery are ordinary
controller input into native wheel physics, with no heading/velocity intervention.

All three cars were also sampled at 30, 65 and 100 km/h with a half-stick step.
At the high-speed sample, baseline/new left/right wheel angles were:
AE86 5.434/5.767 degrees versus 5.434/5.767; R32 5.437/5.752 versus
5.437/5.752; RX-7 5.441/5.753 versus 5.441/5.754. Yaw rates were respectively
25.86/25.88, 15.83/15.87 and 16.46/16.50 deg/s. The small timing/solver variation
is not a handling retune. Low-speed steering gains are intentional.

## Changed files and limits

This pass changes CarController.cs, VehicleConfig.cs, FollowCamera.cs and the
three vehicle config assets. It updates only the two new scenes, their collision
mesh copies and StageDefinitions, Cherry's eight materials and eight recovered
texture assets, plus this report. Existing uncommitted stage-integration code and
assets from the preceding task are preserved. Temporary measurement/authoring
helpers and logs are retained under ignored Library after use, not shipped.
ValidateVehicleFlow.cs extends its barrier search reach from 10 to 25 m to test
the enlarged roads. Vehicles.md is updated with current tuning; the two earlier
investigation reports link here to distinguish historical results.

Final native regression: **12 combinations, 1,646 passing assertions**. The
matrix covers AE86/R32/RX-7 on South Loop, TrackComple, Cherry Mountain and Drift
Tracks: menu selection and persistence, vehicle configuration, four-wheel spawn
contact, physical ordered race triggers, stage/car record separation, scoring and
Style Credit, pause, restart, respawn, effects state and barrier impacts. New-track
wheel-footprint support includes 23,604 ray probes (13,428 Cherry / 10,176 Drift),
plus the existing TrackComple probes. Representative suspension stations pass
on each car. All five scenes have no missing script, mesh, material or shader
references in the editor audit. This is not a full-route curb-seam driving test.
After removing the temporary probes from Assets, the final Unity 6000.6.7f1 batch
compile exited 0 with no C# errors or C# warnings. Existing source import/package
diagnostics are not claimed to be absent. `git diff --check` passes. HEAD remains
5e80f4e1151965f667d3ba9ea6fa7102a9019e1e; no files are staged, committed or pushed.

Ignored evidence: `Library/Polish/before.txt` (initial torque sweep and governor
A/B), `after.txt` (final 100 km/h comparison and sustained reverse input tests),
`audit.txt` (world UVs, dimensions and collider alignment), six `*-lane.png`
placement renders, and `Library/TrackIntegration/playmode.txt` (final matrix).

Engine, clutch, differential, suspension, COM/inertia, power oversteer model,
forward drift stabilization, smoke/audio/skids/lights and scoring are unchanged.
Low/medium-speed steering and service braking intentionally change as described;
high-speed steering and unbraked drivetrain/drift calculations remain the same.
This is not a claim of human-verified identical driving feel.

Full-route controller driving, subjective chase-camera feel, transitions, drift
entry/hold and reverse recovery still need the user's manual test. No physical
gamepad/G29 driving or standalone-build test was performed by the agent.

Unity references: [WheelCollider](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/wheelcollider)
and [wheel friction](https://docs.unity.com/en-us/engine/6000.5/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/friction).
