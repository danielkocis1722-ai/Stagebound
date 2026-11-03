# Full real-world-scale migration — 2026-11-02

> Subsequent power delivery and clutch-load tuning is documented in
> [PowerDeliveryTuning.md](PowerDeliveryTuning.md). The migration measurements below
> describe the pre-tuning engine curves; geometry and gearbox values are retained.

Worktree: `C:\Users\dandy\Documents\Projects\Stagebound-real-scale`, branch
`experiment/real-world-scale`. All work remains uncommitted. No merge or push.
The presentation worktree is protected separately by a before/after file manifest.

## Convention and scope

One Unity unit is one metre. Rigidbody velocity is m/s; displayed km/h is velocity
magnitude × 3.6. Mass is kg, torque Nm, and inertia kg m². The existing shared
CarController, VehicleConfig, StageDefinition, race flow and scoring architecture
remain authoritative. No new gameplay feature, ABS, countersteer, scripted recovery,
transition delay, or yaw torque was added.

The AE86 experiment's measured visual/wheel geometry, suspension, brakes and latest
gearing were copied into the actual AE86 assets, preserving their original GUIDs
and vehicle ID. R32 and RX-7 were measured independently and uniformly sized by
model length. Runtime has no legacy spatial conversion. Historical reports describe
superseded geometry; the authoring baseline was 1/2.192, used only to migrate assets.

## Vehicles

Dimensions below are transformed **vertex envelopes**, including mirrors and other
protrusions, rather than manufacturer body specifications. In particular, AE86's
source envelope is unusually tall; no independent-axis distortion was applied.

| Metres / kg | AE86 | Skyline R32 | RX-7 FD |
| --- | ---: | ---: | ---: |
| Length | 4.200000 | 4.545000 | 4.285000 |
| Width | 1.828579 | 1.875966 | 1.893306 |
| Height | 1.927765 | 1.335235 | 1.225163 |
| Wheelbase | 2.371815 | 2.626781 | 2.438360 |
| Front track | 1.414525 | 1.517305 | 1.399161 |
| Rear track | 1.433471 | 1.517305 | 1.399161 |
| Front radius | 0.296700 | 0.331617 | 0.321729 / 0.321730 |
| Rear radius | 0.296700 | 0.331617 | 0.321778 |
| Mass | 1100 | 1450 | 1250 |
| COM local X | -0.000196 | 0 | 0 |
| COM local Y | 0.057436 | 0.100836 | 0.114216 |
| COM local Z | 0.386817 | 0.473516 | 0.315910 |

COM height was authored at approximately 0.5 m above the nominal tire bottoms;
the local Y numbers are relative to the rig origin, not road height. Front axle
weight shares are about 53%, 56%, and 50%. CarRoot and all WheelColliders have
unit scale, identity wheel-local rotation, and zero WheelCollider centre offset.
Visual import scales remain uniform: 0.22562091, 1.7705736, and 96.71598. The RX-7
source import is tiny; a large visual import multiplier is expected.

| Car / wheel | WheelCollider mount local X/Y/Z (m) |
| --- | --- |
| AE86 FL | -0.704974, -0.079556, 1.501886 |
| AE86 FR | 0.709551, -0.079556, 1.501255 |
| AE86 RL | -0.719416, -0.102172, -0.869929 |
| AE86 RR | 0.714055, -0.102172, -0.870560 |
| R32 FL / FR | ±0.758653, -0.012546, 1.629300 |
| R32 RL / RR | ±0.758653, -0.012546, -0.997481 |
| RX-7 FL / FR | ±0.699581, -0.009030, 1.535090 |
| RX-7 RL / RR | ±0.699581, -0.009031, -0.903270 |

The mounts sit 0.055 m above measured visual tire centres to accommodate the
tested suspension rest position. The AE86's existing tire-centre hub wrappers
preserve mesh world poses while fixing source pivot offsets. The other models'
wheel pivots already coincide with their measured tire centres. Native tests
compare the animated hubs against WheelCollider.GetWorldPose for every selection.

### Inertia and suspension

The fitted chassis boxes give sensible automatic inertia close to a uniform-box
estimate, so automatic inertia is retained. Selecting a config explicitly resets
inertia and sprung masses after applying geometry; it adds no recurring helper.
Existing angular damping (0.225 / 0.28 / 0.24) and anti-roll forces are retained.

| Car | Chassis box X/Y/Z (m) | Uniform-box inertia X/Y/Z | Unity automatic = chosen X/Y/Z |
| --- | --- | --- | --- |
| AE86 | 1.65 / 0.70 / 3.70 | 1299.833 / 1504.479 / 294.479 | 1307.163 / 1510.057 / 296.231 |
| R32 | 1.72 / 0.70 / 3.9996 | 1992.155 / 2290.420 / 416.682 | 2030.493 / 2326.481 / 418.958 |
| RX-7 | 1.75 / 0.70 / 3.7708 | 1532.180 / 1800.149 / 370.052 | 1534.181 / 1800.149 / 372.052 |

Tensor rotations are approximately 0.148°, 0.275°, and 0° around X. The small
differences from the simple estimate include collider offset relative to COM.

| Car | Front/rear spring N/m | Front/rear damper N s/m | Wheel mass kg | Measured sprung mass per front/rear wheel kg |
| --- | --- | --- | ---: | --- |
| AE86 | 40000 / 36000 | 3500 / 3200 | 20 | 291.5 / 258.5 |
| R32 | 52727 / 47455 | 4614 / 4218 | 25 | 406 / 319 |
| RX-7 | 45455 / 40909 | 3977 / 3636 | 22 | 312.5 / 312.5 |

All use 0.18 m travel, target position 0.30, force application distance 0.22 m,
and wheel damping 0.5. Native flat-pad compression is about 29.2–29.4%, with four
grounded wheels. Spring/damper scaling here follows **mass**, preserving a sporty
road-car baseline; it is not a geometric mass conversion. No visual Y oscillation
was added. Existing small visual angular motion remains.

## Engine and gearbox

Engine torque curves, idle/redline (900/7500 RPM), rev response, clutch model,
differential and tire curves are preserved. Peak torque remains 360 / 460 / 500 Nm.
R32 keeps its greater mass/inertia and slower steering response; RX-7 retains its
stronger power and more responsive steering. Geometry and mass preserve distinct
initial personalities; human driving approval is still required.

For rolling tires: `km/h = RPM × 2πr × 0.06 / (gear × finalDrive)`.
The formula uses actual driven-wheel radius, with no mass term or hidden scale.

| Gear | AE86 ratio / redline km/h | R32 ratio / redline km/h | RX-7 ratio / redline km/h |
| --- | --- | --- | --- |
| 1 | 4.200000 / 56.264 | 3.877692 / 62 | 3.698392 / 60 |
| 2 | 2.300000 / 102.743 | 2.226082 / 108 | 2.054662 / 108 |
| 3 | 1.780000 / 132.758 | 1.658048 / 145 | 1.530369 / 145 |
| 4 | 1.460000 / 161.856 | 1.299551 / 185 | 1.232797 / 180 |
| 5 | 1.200000 / 196.925 | 1.068520 / 225 | 1.008652 / 220 |
| Final drive | 3.55 | 3.90 | 4.10 |
| Reverse ratio | 5.673239 | 5.724212 | 5.283417 |
| Reverse redline km/h | 41.65 | 42 | 42 |

The fifth-gear figures are **kinematic estimates**, not measured power-limited
top speeds. A moderate-throttle native sweep reached 180.42 / 207.96 / 204.80 km/h;
these are fixture endpoints, not full-throttle maximum-speed claims. All twelve
normal controller upshifts passed. Measured RPM 0.5 s after each upshift:
AE86 4835 / 5486 / 5677 / 5827; R32 4961 / 5325 / 5618 / 5989;
RX-7 4697 / 5124 / 5647 / 5842. Clutch synchronization and acceleration explain
differences from ideal instantaneous ratio-only RPM drops.

Second/third gear slip fixtures cover 50/70/90 and 80/100/120 km/h target entries.
They confirm useful gearing range without changing tire grip. They **do not
validate a long sustained drift**: several fixed-input cases straighten or spin
and lose speed. Upper-speed second-gear limiter exposure remains, particularly
RX-7 at the 90 km/h entry (1.6 of 4 s above 7100 RPM at 35% throttle). The earlier
AE86 gearing report also records high-throttle limiter limits. Do not interpret
low limiter time in a spun/slowed fixture as successful sustained drift handling.

## Steering, brakes and handling

The central steering limit is 40° through 30 km/h, smoothly reducing to the
existing 30° high-speed limit at 80 km/h. Existing input exponent, steering rate
and Ackermann behavior remain; actual inner/outer wheel angles differ.

| Native measurement | AE86 | R32 | RX-7 |
| --- | ---: | ---: | ---: |
| CG-path full-lock radius at ~10 km/h (m) | 3.897 | 4.316 | 3.966 |
| 100 km/h stop distance (m) | 48.721 | 49.388 | 49.150 |
| Stop time (s) | 3.550 | 3.600 | 3.583 |
| Prolonged-lock wheel-sample fraction | 0% | 0% | 0% |
| Mean absolute forward slip | 0.2149 | 0.2133 | 0.2145 |
| Service base torque Nm | 650 | 957.654 | 801.007 |
| Front service share | 67% | 68% | 65% |
| Full front torque per wheel Nm | 871 | 1302.409 | 1041.309 |
| Full rear torque per wheel Nm | 429 | 612.898 | 560.705 |
| Rear handbrake per wheel Nm | 2400 | 3163.636 | 2727.273 |

Stopping threshold is 0.1 m/s (0.36 km/h), measured from an arranged 100 km/h
rolling fixture with normal full service-brake input. No velocity correction is
applied during stopping. Lock samples mean RPM below 10 or |forwardSlip| > 1
while vehicle speed exceeds 10 km/h. Per-wheel RPM/slip traces are retained with
the validation evidence. Service and handbrake paths remain independent. No ABS
was needed for these straight-line results; mixed-surface/trail braking is unapproved.

At target entries 30/60/100/125 km/h, a half-stick, 0.5 s neutral-coast steering
probe kept all four wheels grounded on the flat pad. Actual end speeds were about
28/56/94–95/118–119 km/h. AE86 yaw rates were 33.4/42.4/39.3/39.5 deg/s;
R32 30.3/38.5/35.7/36.0; RX-7 32.6/41.2/38.1/38.4. These transient samples are
not steady-state corner limits or an approval of abrupt high-speed input.

The reverse fixture starts at -35 km/h, coasts 0.5 s, steers fully 1.2 s, pulses
rear handbrake 0.3 s, selects first with throttle/opposite steering 1.5 s, then
holds straight throttle 3.5 s. Governor-off final headings were 208.1° / 230.6° /
195.7°, with forward exit speeds 55.7 / 59.4 / 58.8 km/h. Forward motor torque
signs passed. R32 does not finish close to a clean 180° in this fixed sequence.
No scripted pose, velocity rotation or yaw torque operates during recovery.

Governor ON/OFF aggressive transition tests use +0.55 stick, -0.55, +0.35, then
neutral with 65% throttle near 100 km/h. All cars spin in both variants. Peak yaw:
AE86 122.0/144.0, R32 119.3/142.4, RX-7 118.3/138.0 deg/s. The governor limits
peak yaw but does not rescue this maneuver. It is retained as the existing
optional component, **disabled in all three authoritative configs** to match the
accepted AE86 experimental baseline. Evidence does not establish it as universally
unnecessary; the A/B traces support further human comparison without new assists.

DriftTelemetry retains 28 km/h minimum speed, 20 km/h minimum forward speed,
10° entry / 65° maximum angle, and 0.15 rear sideways / 0.25 forward-slip minima.
Scoring retains its 150 deg/s yaw protection, spin rules, angle/speed rewards,
combo, transition bonus and Style Credit. Only the spatial repeated-area cell
changes from 20 old units to 9.124087 m; its algorithm and time budget remain.

## Tracks, presentation and records

All four playable scenes have metre geometry, matching static collision meshes,
converted route guides, gates, start/finish and checkpoint respawns. Relative
positions, route heading and authored road pitch are preserved. The scene car's
pose uses the authoritative spawn. Spawn clearance is recomputed from the lowest
fully extended wheel footprints across all three cars plus 4 cm safety margin,
instead of retaining a 1.25 m drop. Typical flat-road root height is ~0.62 m;
root height is not the gap beneath the tires.

| Stage | Legacy coordinate factor | Static colliders | Checkpoints | Gate width (m) | Record version |
| --- | ---: | ---: | ---: | ---: | --- |
| South Loop | 0.4562044 | 4 | 1 | 10.803 / 11.095 | 1 → 2 |
| Track Comple Circuit | 0.4562044 | 64 | 4 | 7.527 | 1 → 2 |
| Cherry Mountain | 0.4562044 | 7 | 9 | 10.675 | 2 → 3 |
| Drift Tracks | 0.4562044 | 10 | 6 | 9.489 | 2 → 3 |

Track Comple's independently inspected original scale 6 becomes 2.7372264 at
the imported visual root; its ~15-old-unit asphalt corresponds to ~6.84 m, while
road-plus-curb collision cross sections are typically 8.6 m. Cherry's inside-line
pavement is ~6.93 m; its broader supported surface includes shoulders. Drift's
pavement is ~8.3 m; sampled collision width median is 8.2 m at 0.1 m sampling.
South Loop's original 0.8/0.9/0.9 source proportions are preserved uniformly at
the scene level; collision transforms are baked rather than inheriting those
nonuniform scales. Native material-submesh raycasts distinguish Tarmac/Stripe
from the broad sand/concrete collision surface: representative South Loop pavement
is 8.6–8.7 m wide at the two gate stations.

The source models, UVs, Cherry recovered textures/materials and Drift's 14 materials
remain intact. Track Comple's visible jump connector remains aligned with its
collision; the route layout and existing collision structure are preserved.
All static collider and trigger world scales are one. Visual imports may keep
their source scale beneath clean scene containers.

Chase camera slow/fast distance is 3.55/4.2 m, height 1.3/1.6 m, look height
0.4 m and look-ahead 1.1/2.8 m. Low-speed proximity reduction is 0.25 m,
acceleration pullback 0.25 m, acceleration lowering 0.025 m, drivetrain hit 0.03 m,
brake push cap 0.12 m and drift side offset 0.18 m. FOV behavior stays 60–73°
before existing acceleration feedback. Renders are reviewed alongside the accepted
AE86 experiment; final camera preference remains a human driving check.

Skid width is 0.20 m, minimum segment length 0.06 m, and ground offset 0.007 m.
Starter smoke size is 0.16–0.30 m, cone radius 0.055 m, speed 0.16–0.41 m/s and
contact offset 0.025 m. Wheel hit positions drive placement. Lamps stay attached
to uniformly scaled body geometry; AE86's existing disabled lamps remain disabled.
Engine RPM/shift audio algorithms and clips are preserved; spatial min/max
distances become 2.3/30 m to suit the closer camera. No auditory listening
approval is claimed.

Stage IDs and vehicle IDs stay stable. New record versions and disabled legacy
imports isolate real-scale records; no old user keys are deleted. Timer,
checkpoint order, continuous circuit loop, point-to-point finish, respawn,
restart, results and Style Credit behavior use their existing implementation.

## Validation status and reproduction

Native physics measurements passed for all three vehicles. Full asset and
vehicle/stage integration results, cleanup status and final diff verification
are recorded in the evidence alongside this report.

The final audit compares each static MeshCollider vertex against the transformed
visual source. All 57 mesh pairs agree within 0.00013 m. This catches visual-only
hierarchy mistakes that road-contact tests cannot detect. Visual review exposed
and corrected an initial Cherry prefab normalization error; imported prefab
internals are now preserved, with the conversion applied at their import root.

The AE86-only test/comparison scenes, catalogs, duplicate config/definition,
temporary visual prefab and lab overlay are removed after their authoritative
references were migrated. Old one-time vehicle/Track Comple setup generators and
AE86-only test helpers are retired. Their modified local copies were archived
under ignored `Library/FullScale/ArchivedExperiment`; git retains committed history.
Build Settings launches MainMenu followed by the four playable stages. No second
physics controller or permanent test scene is shipped.

Editor entry points:

- `ValidateMetreAssets.Run`: enabled scenes, references, metre geometry, route support.
- `ValidateMetrePhysics.Run`: native flat-pad braking, turning, speed probes, J-turn,
  governor comparison, five-gear sweep and second/third gear slip fixtures.
- `ValidateVehicleFlow.RunTracks`: all 12 selections, actual stage spawn/contact,
  menus, controls, physical gate crossings, checkpoint/respawn/restart, records,
  scoring, light materials, effects/audio bindings and sampled road support.

Independent physics fixtures seed pose/velocity/RPM before measurements. Matrix
trigger sweeps and scoring excitation deliberately arrange motion to test those
systems, and are not driving demonstrations. No human completed twelve full laps.
Manual approval remains required for sustained drifting, drift transitions,
high-speed inputs, reverse recovery consistency, road-compliance endurance,
brake feel/trail braking, camera composition, real controller/clutch use and audio.

### Final results

| Stage | AE86 | R32 | RX-7 |
| --- | --- | --- | --- |
| South Loop | PASS | PASS | PASS |
| Track Comple Circuit | PASS | PASS | PASS |
| Cherry Mountain | PASS | PASS | PASS |
| Drift Tracks | PASS | PASS | PASS |

These are automated integration passes with the scope above, not subjective
driving approvals. The final matrix records 1,647 passing assertions, the native
physics run 90, and the final static audit 108. Route support covers 28,788 wheel
footprints across the three vehicles. Physical representative surface tests and
roadside impacts supplement those static probes; they cannot certify every
possible driving line or impact.

Unity **6000.6.7f1** compiled and completed the final native runs without C# errors
or warnings. No missing scripts, meshes, materials, shaders or dangling serialized
component references were found in enabled scenes. The selected visual prefabs
and forced RX-7 FBX reimport also passed. MainMenu remains the startup scene.
The HUD still reads the shared controller's real km/h, engine RPM and gear; timer,
score and Style Credit readers are preserved. Native vehicle renders omit the
screen-space overlay, so no screenshot-based HUD legibility claim is made.

Changed runtime files are `VehicleConfig.cs` (one-time inertia/sprung-mass reset),
`FollowCamera.cs` (metre defaults), `WheelEffects.cs`, `SkidmarkSystem.cs`,
`VehicleAudio.cs` (spatial dimensions), and `DriftScoreSystem.cs` (cell-size default).
The controller, telemetry algorithm, tire model, clutch, race flow, records code
and HUD algorithms remain unchanged. All three actual configs, AE86 visual,
four playable scenes, their authored collision meshes and stage definitions,
Build Settings, gate-authoring defaults and editor validation tools are migrated.
Generated mesh payloads and scene hierarchies dominate the diff; retired experiment
assets account for most deletions. The final tracked diff against experimental
HEAD covers 113 files, plus the new validation/editor/report assets listed below.

`git diff --check` passes. No changes are staged and HEAD remains
`bc078a5a26c2b1376d98954a19bf315530887513`. Main's 595 protected tracked files and
working-tree status match the initial manifest. The experiment's pre-existing
ProjectSettings/QualitySettings bytes are preserved. All 244 production source
model, texture, material and corresponding metadata files checked are unchanged;
removed experimental pad materials are intentionally excluded from that count.
No commit, push, merge, standalone player build, physical-controller test or human
driving/audio approval is claimed.

Evidence:

- [Exact changed/new file list](RealScaleValidation/files.txt)
- [Authored dimensions, wheel/config values and spawn/gate poses](RealScaleValidation/authored.txt)
- [Final geometry/reference/road audit](RealScaleValidation/assets.txt)
- [Native physics, gears and handling measurements](RealScaleValidation/physics.txt)
- [Final 12-combination integration log](RealScaleValidation/matrix.txt)
- [Main/settings/source preservation checks](RealScaleValidation/protection.txt)
- [AE86 braking trace](RealScaleValidation/ae86-braking.csv),
  [R32 braking trace](RealScaleValidation/skyline-r32-braking.csv),
  [RX-7 braking trace](RealScaleValidation/rx7-fd-braking.csv).

Final rendered captures and governor transition CSVs remain in ignored
`Library/FullScale/Matrix` and `Library/FullScale`. Use MainMenu in this worktree
to manually evaluate all cars/stages. Main is still the safe presentation version.
