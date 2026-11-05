# Progressive yaw buildup — measured inertia revision

## Diagnosis

The evidence supports low effective rotational inertia as a contributor to rapid
yaw buildup, not a confirmed single cause of every gameplay spin. The generated
inertia matches the compact collision boxes; no metre-scale units error was
found. A mass-distribution correction is justified as tuning beyond that uniform
box approximation, not as a claim of measured production-car inertia.

The rear sideways curves do **not** contain the suspected abrupt post-peak grip
collapse. All three cars use extremumSlip 0.28, extremumValue 1,
asymptoteSlip 0.8, asymptoteValue 1, stiffness 1.16 at baseline. These five values
are unchanged. The fronts use 0.3 / 1 / 0.8 / 0.98 / 1.26, also unchanged.
Do not interpret WheelCollider slip units as tire slip-angle degrees.

Dynamic rear-grip modifiers were inspected: combined-slip has a bounded floor,
power-slip reduction is bounded to 12%, and both upstream signals already follow
filtered targets. The final recovery helper applies decreasing targets directly,
so a mode handoff remains a possible contributor in gameplay. It was not changed
without a trace showing a step. FrontAxleRecovery already provides conditional
catch support; it is preserved. DriftYawStability is disabled in all three
configs, so there is no active yaw governor that is simply arriving late.
Neither force reversal nor the combined-slip handoff has been established as
the main cause. The live recorder now makes those states inspectable.

## Changes

After automatic inertia recomputation, scale only the principal inertia axis
closest to vehicle up. Preserve principal-axis orientation and the other two
principal inertia values. Recompute first on every config application so the
multiplier cannot accumulate. The multiplier is exposed per chassis and bounded
to 1–1.5. Mass, COM, wheel geometry, tire grip and angular damping are unchanged.

| Car | Measured baseline yaw inertia, kg m² | New | Multiplier |
|---|---:|---:|---:|
| AE86 | 1510.06 | 1812.07 | 1.20 |
| RX-7 | 1800.15 | 2250.19 | 1.25 |
| R32 | 2326.48 | 3024.43 | 1.30 |

No yaw-rate or drift-angle clamp was added. No stability controller was enabled.
AE86 retains the smallest inertia and smallest correction, RX-7 is intermediate,
and R32 has the greatest rotational resistance. Tire breakaway capacity is
unchanged; the slower **body response** is the intended improvement.

## Controlled native physics tests

72 completed preview-scene runs: three vehicles, three starting speeds,
baseline/new inertia, and four response strategies (no catch, catch at 8/20/40
degrees sideslip after 0.2 seconds reaction delay). Each fixture uses the actual
config chassis, COM, suspension and WheelCollider curves. It settles on a flat
pad and spins up the wheels before applying the test start speed.

The stimulus ramps actual road steering from 0 to 6 degrees over one second and
normalized fixture throttle 0.2 to 0.7 over two seconds. Each rear wheel receives
600 Nm times fixture throttle. The catch driver reduces throttle to 0.15 and
commands `clamp(0.65*sideslip - 0.1*yawRate, -25, 25)` degrees. This direct
synthetic driver is deliberately isolated; it does not ship as steering logic.

Important limits: the fixture omits the engine/clutch/differential torque model,
combined-slip grip modifiers, anti-roll, FrontAxleRecovery and human/input
response. Speed is initialized, not held constant. Results are controlled
comparisons, **not full-game acceptance tests**. The countersteer command itself
can produce large deceleration peaks; these must not be mistaken for outward
yaw acceleration. WheelHit reports slips and normal loads, not measured lateral
forces, so the logs do not claim to measure lateral-force reversal directly.

Unity preview physics is checked to be distinct from the default scene before
simulation. Every fixture is destroyed by closing its preview scene. No open
game scene is simulated or saved by this harness. See Unity's
[PhysicsScene.Simulate documentation](https://docs.unity3d.com/jp/current/ScriptReference/PhysicsScene.Simulate.html).

### No-catch results

Whole five-second peak absolute yaw acceleration, deg/s² (includes later spin
and slowing, so these values are not all measured at the starting speed):

| Car | 40 km/h old → new | 70 km/h old → new | 100 km/h old → new |
|---|---:|---:|---:|
| AE86 | 92.16 → 77.98 | 234.67 → 197.66 | 442.77 → 308.75 |
| RX-7 | 91.87 → 67.58 | 227.37 → 177.63 | 226.22 → 167.45 |
| R32 | 149.70 → 112.05 | 148.24 → 43.37 | 329.95 → 207.47 |

At 40 km/h peak yaw rate changed only slightly: AE86 33.07→32.71, RX-7
32.62→32.07, R32 29.55→28.97 deg/s. This stimulus did not reach drift-level
sideslip at 40 km/h; it is not evidence of successful low-speed drift recovery.

At 70 km/h, time to 20 degrees slip increased from 2.68→2.96 seconds for AE86
and 3.48→3.86 for RX-7. R32 did not reach 20 degrees in either variant.

At 100 km/h, time to 20 degrees increased from 1.66→1.78 seconds for AE86,
1.76→1.94 for RX-7, and 1.80→2.00 for R32. Filtering to the entry region
(absolute slip ≤20 degrees, actual speed ≥80% of starting speed, accelerating
in the direction of yaw), peak outward acceleration was:

| Car | Old | New |
|---|---:|---:|
| AE86 | 52.49 | 49.58 |
| RX-7 | 51.82 | 48.39 |
| R32 | 49.14 | 45.38 |

Thus early high-speed improvement is modest (~6–8%), not the larger whole-spin
peak reduction. Holding steer and throttle without catching can still spin AE86.

### Recovery and drift status

- **Mild catch:** at 70/100 km/h, every new-inertia 8-degree trigger returned
  essentially to zero sideslip within the five-second fixture.
- **Medium catch:** every triggered 20-degree case at 70/100 km/h returned near
  zero (largest residual 0.55 degrees). R32 did not trigger at 70 km/h.
- **Late mistake:** AE86 at 100 km/h with the 40-degree trigger still spun,
  finishing near 177 degrees sideslip. R32/RX-7 late catches retained substantial
  residual sideslip; no universal point-of-no-return claim is made.
- **Sustained intentional drift:** not established by these fixed-stimulus
  tests. Tire grip, engine torque and handbrake logic remain unchanged, but the
  new inertia also slows corrective yaw acceleration. Human testing must check
  that this tradeoff has not made catches or transitions sluggish.
- **Full-game 100 km/h acceptance:** pending driving validation. Do not present
  synthetic catches as proof that keyboard, gamepad and G29 gameplay all pass.

## Live gameplay recording

In Play Mode with a car loaded, select **Stagebound > Start live yaw recording**.
Return focus to the game, then run separate 40/70/100 km/h ramps and mild/medium/
late catches. Stop via **Stagebound > Stop live yaw recording**; recording also
saves on component disable or after 120 simulation seconds.

The editor-only component reads each fixed step: speed, actual FL/FR steer,
throttle/brake/handbrake, body sideslip, yaw rate/acceleration, four-wheel contacts,
lateral/longitudinal slip, normal loads, complete sideways friction curves,
rear grip multipliers, front recovery multiplier, governor enable state and
inertia tensor. Discontinuous position samples are flagged. No input/physics
values are written by the recorder.

Outputs: `Logs/YawBuildup/summary.txt`, 72 `*-catch*.csv` fixture traces,
`entry-metrics.csv`, and timestamped `live-*.csv` gameplay recordings.

## Validation and changed files

Runtime/editor compilation passed with Unity's bundled compiler. Native tests
completed, asserted four-wheel contact, and verified that the production inertia
helper scales yaw while preserving the other principal inertia values.

- Assets/Scripts/VehicleConfig.cs — chassis parameter and one-time inertia scaling.
- Assets/Vehicles/Configs/AE86_VehicleConfig.asset — multiplier 1.20.
- Assets/Vehicles/Configs/RX7_VehicleConfig.asset — multiplier 1.25.
- Assets/Vehicles/Configs/SkylineR32_VehicleConfig.asset — multiplier 1.30.
- Assets/Scripts/YawBuildupTelemetry.cs and .meta — editor-only read-only recorder.
- Assets/Editor/YawBuildupValidation.cs and .meta — isolated tests and recording menus.
- Docs/YawBuildup.md — this report.

No FFB, wheel settings, H-shifter, camera, steering-input, scoring or engine
changes were made in this task. Existing unrelated workspace edits were retained.
Nothing committed or pushed.
