# AE86 real-world-scale experiment

> Historical report. The experimental branch now uses the full metre-scale migration documented in [RealScaleMigration.md](RealScaleMigration.md). Values and instructions below describe the earlier state.

This report records the initial scale experiment. The subsequent drivetrain-only
ratio update and current gear speeds are in [RealScaleGearing.md](RealScaleGearing.md).

Experimental worktree only: `C:\Users\dandy\Documents\Projects\Stagebound-real-scale`
on `experiment/real-world-scale`. Do not merge this into the presentation version.

## Presentation protection

Main was inspected on branch `main`, HEAD `5e80f4e1151965f667d3ba9ea6fa7102a9019e1e`.
Its uncommitted changes were the intended four-stage integration, recovered Cherry
textures and track scaling, steering/brake/camera polish, reference validation and
supporting documentation. They were preserved using a temporary Git index and
`commit-tree`, without staging in main's real index or changing its branch/HEAD.

Presentation snapshot: **63d1b9e0ec0ca8434f2f4e86dc712558e85751f9**, branch
`presentation/2026-11-02`. The experimental branch starts from that exact snapshot.
Main deliberately retains its original dirty status; the snapshot is a separate
recoverable revision. Nothing was merged or pushed. A 705-file source/config/docs
SHA-256 manifest, branch/status and index hash were recorded before experimentation
and checked again afterward. No Unity batch command targets the main project.

The experiment has its own Library and product name `StageboundRealScale`, giving
it a separate PlayerPrefs namespace. Experimental records/settings therefore do
not overwrite the presentation game's saves. No shared Library junction is used.

## Open and compare

Open `Assets/Experiments/RealScale/AE86_RealScale_Test.unity` and enter Play Mode.
The scene is a copy of SampleScene's rig/camera/lighting, with its track replaced
by a flat 600 x 600 metre pad. Grid spacing is 10 metres; the two reference rings
have 5 and 10 metre radii. Paint has no collision. There is one flat floor collider
and an optional timing-line trigger, rather than rescaled production tracks.

Use the existing keyboard/gamepad driving, gear, clutch, handbrake, pause and
respawn controls. **F6** toggles the existing yaw governor. **F7** reloads the
matching presentation-geometry comparison scene or returns to the real-scale scene.
Reloading ensures each variant gets clean controller/suspension/effects caches.
The comparison scene contains only the original AE86; the experiment has its own
AE86-only definition/catalog. The original three-car catalog is untouched.

The lab overlay only displays measurements and switches scenes/governor state.
It never writes steering, forces, yaw torque, heading or velocity. There is no
J-turn button, automatic countersteer or scripted recovery in runtime code.

## Geometry and physical setup

| Quantity | Presentation AE86 | Experimental AE86 |
| --- | ---: | ---: |
| Complete visual vertex envelope, width | 4.052327 world units | 1.828579 m |
| Complete visual vertex envelope, length | 9.307648 | 4.200000 m |
| Complete visual envelope height | 4.272133 | 1.927765 m |
| Physics wheelbase | 5.261000 | 2.371815 m |
| Front / rear tire-centre track | physics 3.210 / 3.210 | 1.414525 / 1.433471 m |
| WheelCollider radius | 0.650000 | 0.296700 m |
| Rigidbody mass | 1,100 kg | 1,100 kg |
| COM, rig-local | (0, -0.300000, 0) | (-0.000196, 0.057436, 0.386817) m |
| Chassis box dimensions | (1.8, 0.5, 4.0) | (1.65, 0.70, 3.70) m |
| Chassis box local centre | (0, 0, 0) | (-0.000196, 0.097436, 0.315663) m |
| Automatic principal inertia, kg m² | (1588.583, 1763.667, 418.917) | (1307.163, 1510.057, 296.231) |
| Principal-axis rotation | identity | about 0.148 degrees about X |
| Angular damping | 0.225 | 0.225, retained rather than used as a new workaround |

The visual scale multiplier is **0.45124180**, uniform in all axes. The source
model's proportions cannot simultaneously produce 4.2 m length and exactly 1.65 m
overall width. The main body mesh is approximately 1.73 m wide; protrusions bring
the complete envelope to 1.83 m. These proportions are preserved rather than
stretching the mesh. The complete height includes the source's low underbody mesh
and protruding parts; it is not a roof-height claim or a chassis collider size.

The original wheel pivots were offset from their tire geometry centres. The new
visual prefab adds corrected hub parents while preserving child mesh world poses,
UVs, materials and uniform scale. Original prefab/model files are unchanged.
Collider positions come from those measured centres, not a blind coordinate division.

| Wheel | Tire centre at authored visual rest, rig-local | Suspension mount | Radius |
| --- | --- | --- | ---: |
| FL | (-0.704974, -0.134556, 1.501886) | (-0.704974, -0.079556, 1.501886) | 0.296700 |
| FR | (0.709551, -0.134556, 1.501255) | (0.709551, -0.079556, 1.501255) | 0.296700 |
| RL | (-0.719416, -0.157172, -0.869929) | (-0.719416, -0.102172, -0.869929) | 0.296700 |
| RR | (0.714055, -0.157172, -0.870560) | (0.714055, -0.102172, -0.870560) | 0.296700 |

Mass stays in the original approximately one-tonne identity. COM targets roughly
0.5 m ground height and 53% front static sprung mass, instead of copying the old
oversized-coordinate COM. The local COM Y is positive because the rig origin is
near the wheel/suspension plane; its sign alone does not indicate a high COM.

Inertia remains automatic. A uniform 1.65 x 0.70 x 3.70 m, 1,100 kg box estimate
`m/12 * (h²+l², w²+l², w²+h²)` is (1299.833, 1504.479, 294.479) kg m². The small
difference from Unity's automatic principal tensor comes from collider/COM offsets
and principal-axis rotation. No arbitrary explicit tensor or yaw damping was added.
Notably, the original AE86 collider was already much smaller than its visual body;
simply multiplying its old inertia by scale² would have been inappropriate.

## Suspension, steering and drivetrain

| Setting | Presentation front / rear | Real-scale front / rear |
| --- | ---: | ---: |
| Suspension travel | 0.25 / 0.25 m | 0.18 / 0.18 m |
| Spring | 26,000 / 26,000 N/m | 40,000 / 36,000 N/m |
| Damper | 2,800 / 3,600 N s/m | 3,500 / 3,200 N s/m |
| Target position | 0.38 / 0.38 | 0.30 / 0.30 |
| Wheel mass | 20 / 20 kg | 20 / 20 kg |
| Force application distance | 0.30 / 0.30 m | 0.22 / 0.22 m |

In native static tests all four wheels contacted the pad. Measured extension was
about 0.1271 m front / 0.1275 m rear, leaving about 0.0529 / 0.0525 m compression
from full extension (roughly 29% of total travel). Tire centres settled at 0.2946 m
above the pad, within about 2 mm of radius. This is a measured static pose; no
claim is made of a full bump-road or prolonged pogo/top-out endurance test.

Steering retains 40-degree inside-wheel lock through 30 km/h, smoothly reaching
30 degrees by 80 km/h. Ackermann uses the actual new wheelbase and front track.
The existing analog deadzone/exponents and 95/150 deg/s steer/return rates remain.
No countersteer is added; visual wheels use the physical collider poses.

Engine torque/curve, gear ratios, clutch coupling, differential and drivetrain
direction code are untouched. The smaller wheel radius naturally makes every gear
shorter; first gear now reaches approximately 37 km/h rather than about 78 km/h.
That is not a speed conversion or a reverse lock. All benchmark first-gear motor
torques were nonnegative while reversing, and rear wheel RPM crossed through zero.

Speed remains `Rigidbody.linearVelocity.magnitude * 3.6`: 10 m/s = 36 km/h and
27.78 m/s = 100.008 km/h. No hidden spatial/speed factor was introduced.

## Native comparison results

The test driver queues ordinary Input System controls into the native Play Mode
vehicle. Initial pose/velocity is set only when arranging an independent fixture;
there are no ongoing pose/velocity corrections, added forces or yaw interventions.
These are automated controller-input tests, not human controller driving.

At matched 10.00 km/h, full-lock CG-path radius was **8.278 m** for presentation
geometry and **3.897 m** for real scale, identical with the governor on/off.
These are CG path radii `speed / yawRate`, not outer-tire kerb radii. The speed
controller queues ordinary throttle only; it does not constrain vehicle motion.

For a half-stick step lasting 0.5 seconds from straight neutral coasting:

| Initial speed | Presentation yaw | Real-scale yaw, governor on/off |
| --- | ---: | ---: |
| 30 km/h | 16.287 deg/s | 33.565 deg/s |
| 65 km/h | 23.358 deg/s | 41.377 deg/s |
| 100 km/h | 25.809 deg/s | 38.894 deg/s |

These are transient samples, not a claim that the same input remains appropriate
for the smaller wheelbase. Existing steering inputs are substantially more potent.

### Reverse J-turn

All tests start backward at 35 km/h, coast for 0.5 seconds, apply full steering
for the specified duration, hold rear handbrake for 0.3 seconds, then select first
and throttle. Recovery steering lasts 1.5 seconds and then returns to neutral.
No test waits for a chosen heading before applying the handbrake.

| Full-steer approach | Recovery steering | Presentation max yaw | Real-scale max yaw, governor off |
| --- | --- | ---: | ---: |
| 0.4 s | same direction | 62.06 degrees | 110.87 degrees |
| 0.4 s | opposite | 37.46 | 93.76 |
| 0.8 s | same | 83.53 | 128.79 |
| 0.8 s | opposite | 63.21 | 123.57 |
| 1.2 s | same | 103.05 | 153.30 |
| 1.2 s | opposite | 87.10 | 151.26 |

The last real-scale case exits with heading 208.76 degrees, within 30 degrees of
the original backward travel direction, and reaches 36.84 km/h forward relative
to its new heading. Backward longitudinal motion crosses zero in about 0.82 s
after throttle. This is a meaningful improvement, **not a clean, universally
reliable 180-degree reversal**. The quicker approaches still do not complete it.
Governor-on results are effectively the same in reverse; disabling it was not
the source of the improvement. Rear handbrake RPM reaches zero as intended.

### Service braking

| Setting/result | Presentation | Real scale, governor on/off |
| --- | ---: | ---: |
| Configured per-wheel base torque | 1,600 Nm | 650 Nm |
| Front service share | 52% | 67% |
| Full front / rear service torque | 1,664 / 1,536 Nm | 871 / 429 Nm |
| Rear handbrake torque, separate | 15,000 Nm | 2,400 Nm |
| 100 km/h stopping distance | 44.352 m | 48.675 m |
| Stopping time | 3.167 s | 3.483 s |
| Prolonged-lock sample fraction | 0% | 0% |
| Mean absolute longitudinal slip | 0.250 | 0.204 |

Stop threshold is 0.5 m/s. Lock samples count wheel RPM below 10 or absolute
forward slip above 1 while speed exceeds 10 km/h. At 0.5 s into the real-scale
stop, front RPM was about 576 with slip -0.251; rear about 654 with slip -0.151.
Front loads rose to about 2,840 N per reported WheelHit and rear loads fell to
about 1,200 N. Braking is strong and controllable but not shorter than presentation.
Mass was not reduced to improve braking. Existing tire curves are unchanged and
no ABS was necessary for this first straight-line tune.

### Drift transitions and governor

An intentionally aggressive near-100 km/h input sequence uses +0.55 stick for one
second, -0.55 for one second, +0.35 for one second, then neutral, with 0.65 throttle.
Presentation uses second gear and real scale fourth, to put both near comparable
engine RPM after the wheel-radius change; this is not an isolated one-variable
experiment. The final powered fixtures spin up the engine consistently with the
rolling shaft, then run straight before the steering step, avoiding an artificial
neutral-idle-to-fourth engine-braking transient. Actual starting speeds are about
95.7 km/h presentation and 94.1 km/h real scale.

| Metric | Presentation | Real, governor ON | Real, governor OFF |
| --- | ---: | ---: | ---: |
| Peak yaw | 35.44 deg/s | 120.85 | 143.09 |
| Peak body slip | 11.60 degrees | 180.00 | 180.00 |
| Speed after 6 s | 105.88 km/h | 21.89 | 16.13 |

Real scale exposes much stronger yaw/oversteer for these inputs. Both real variants
spin; the existing governor reduces the peak but does not save the maneuver.
It has no measurable effect in the low-speed turn, neutral steering samples or
reverse cases. This does **not** prove that it is universally unnecessary. The
experimental default is OFF so the foundation can be evaluated, with F6 available
for comparison. Main's governor is unchanged. Existing front recovery and rear
grip models are retained; this is not presented as a new pure tire-model simulation.

The mild 0.25-stick / 0.35-throttle version stays settled: presentation peaks at
1.52 deg/s yaw / 0.58 degrees body slip, real scale at 3.12–3.15 deg/s / 1.04 degrees,
with essentially no governor difference and no active front recovery multiplier
(measured maximum 1.000). No smoke is expected at those small measured slips.
The direct-from-neutral stress fixture recorded peak physical pitch about 1.21
degrees and roll 2.45 degrees in real scale versus 0.78 / 0.66 presentation; these
body-motion figures are stress observations, not matched steady-state measurements.

Benchmark resets now settle neutral rack/throttle/idle state for two seconds
before each case, with an assertion. Early warm-state measurements are retained
only as `superseded-warm-state-*` logs; they are not the figures quoted here.

DriftTelemetry and scoring were not retuned. Speed/slip/yaw still come from actual
motion and wheel contacts. CSV traces record the timing and signs of the response;
peak metrics alone do not establish subjective transition quality.

## Camera, effects and limits

Experimental camera: base slow/fast distances 3.55 / 4.20 m, low-speed proximity
reduction 0.25 m, heights 1.30 / 1.60 m, look height 0.40 m and look-ahead 1.1 / 2.8 m.
Measured-acceleration pullback is 0.25 m, drivetrain-hit pullback 0.03 m and braking
offset cap 0.12 m. The camera is composed by inspecting renders, not by scaling the
camera Transform. Existing 60–73 degree base FOV range, actual-acceleration FOV
feedback and smoothing rates remain. Camera framing is an initial manual-test tune.

Skid widths are 0.20 m in the experimental scene. Smoke, skidmark and engine/tire
audio components retain their existing implementation and assets. The AE86's
pre-existing disabled brake lamps remain disabled; this is not a newly working
light system. No audio listening test is claimed. Additional native effect checks
confirmed smoke emission, skid intensity and screech gain under a separate actual
rear-wheel-lock coast at 50 km/h for all three variants. F6 toggling/restoration
also passes in all variants. The rendered real car is within roughly 10% of the
presentation car's static screen width; the camera has not simply inherited an
oversized follow distance.

The conclusion is **promising for dimensional consistency, low-speed turning and
reverse rotation, but not ready for a full-project migration**. High-speed input
mapping/drift balance needs manual evaluation and a focused tuning experiment.
R32, RX-7 and all production stages remain unchanged. Do not migrate them on the
basis of a single AE86 sandbox test, and do not use this branch for the presentation.

## Reproduction and files

Final verification: **78 full-comparison assertions + 33 refined assertions**,
all passing across presentation geometry, real scale/governor ON, and real scale/
governor OFF. All seven scenes passed missing-script/mesh/material/shader checks;
each test scene has only its flat pad and timing line as static colliders.
Final Unity 6000.6.7f1 compilation exited 0 with no C# errors or warnings.
The editor's pre-existing Search indexing exception can appear in batch startup
logs; it is not a Stagebound compile error or a failed physical assertion.
`git diff --check` passes. There is no standalone-build, real gamepad/G29,
human drift-feel or auditory listening-test claim.

Main's 705 protected files, index hash, branch/HEAD and working-tree status were
rechecked unchanged. R32/RX-7, original AE86 assets, runtime physics scripts and
production scenes have no experimental diff. The only existing files changed in
the experimental worktree are the build-scene list and separate product name.

Editor authoring: `RealScaleExperiment.Inspect`, `Build`, and `Audit` are explicit
batch entry points. Build writes the experiment's generated assets; do not rerun
it over manual experimental tuning without preserving that work first.
`ValidateRealScale.Run` measures the main comparison; `Refine` measures matched
turning speed, mild/harsh response and lab controls. Logs, screenshots and traces
live in ignored `Library/RealScale`. No runtime driving sequence was added.

Changes are confined to new `Assets/Experiments/RealScale` content (visual variant,
config, definitions, two catalogs, two scenes/stage assets, pad/paint materials and
lab overlay), two new Editor helpers, this report, experimental build-scene entries
and the separate product name. Original runtime physics scripts, vehicle assets,
visual prefabs and production scenes remain unmodified.

Physics references: [Unity WheelCollider](https://docs.unity3d.com/kr/6000.0/ScriptReference/WheelCollider.html),
[suspension](https://docs.unity3d.com/cn/2023.2/Manual/wheel-colliders-suspension.html),
[automatic inertia](https://docs.unity3d.com/ja/6000.0/ScriptReference/Rigidbody-inertiaTensor.html).
