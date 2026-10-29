# Clutch torque transfer rebuild

This replaces the previous envelope-based kick described in ManualClutch.md.
No commit or push was made. Existing drift telemetry/scoring, race/HUD/menu,
camera, audio, effects, steering, friction and suspension were preserved.

## What was wrong

The previous controller independently moved engine RPM toward:

```
lerp(freeRevTarget, max(idleRPM, DrivetrainRPM), clutchEngagement)
```

at up to 5000 RPM/second on the way down. This RPM reduction did not produce any
reaction torque at the wheels. A separate small kick then added torque:

```
targetExtra = 360 * .65 * mismatchWeight
extra = filteredTarget * remainingTime / .22
extra = min(extra, 500 - normalEngineTorque, availableFlywheelImpulse)
axle = (normalEngineTorque + extra) * engagement * gear * finalDrive * efficiency
```

`mismatchWeight` used the smaller of current/captured positive RPM surplus,
normalized by 3000 RPM, with a .65 exponent faded over 4500–6000 engine RPM.
Filtering used 18/second. Only the extra torque debited flywheel RPM.

The baseline did have some ordinary full-throttle wheelspin; the tests do not
support a claim of zero slip under every condition. However, in the 5000 RPM test
the kick added only 47 Nm engine-side. Between .04 and .12 seconds, RPM dropped
about 490: 400 RPM came from the independent RPM-follow path and only about 90
from kick reaction. That unpaired synchronization path was the fundamental bug.

## New calculation

There is one total engine-side clutch torque. Combustion is applied to the
flywheel prediction; it is not added to the clutch output again.

Let `e` be final engagement, `dt` the physics step, `Ie` flywheel inertia,
`Id` estimated engine-side drivetrain inertia, `Te` calculated engine torque,
`L` estimated road load, and `K` clutch coupling strength in Nm/RPM.

```
Ir = 1 / (1/Ie + 1/Id)
predictedDeltaOmega = omegaEngine - omegaDrivetrain + dt * (Te/Ie + L/Id)
follow = 1 - exp(-K * (60 / (2*pi)) * e * dt / Ir)
demandedClutchTorque = Ir / dt * predictedDeltaOmega * follow
Tc = clamp(demandedClutchTorque, -capacity, capacity)

newEngineRPM = EngineRPM + (Te - Tc) * dt / Ie * 60 / (2*pi)
axleTorque = Tc * abs(currentGearRatio) * finalDrive * efficiency
```

Reverse changes axle torque sign. The existing LSD receives this one axle torque
and distributes it without bypasses or extra multipliers. Signed clutch torque
can also spin up a slower engine from the drivetrain.

Fully disconnected/neutral/shifting/handbrake states retain free-rev behavior and
their existing zero-wheel-torque cuts. Idle/redline protection remains. The small
existing free-engine drag is applied to the uncoupled fraction only. Connected
RPM is never moved toward a wheel-linked target or snapped to drivetrain RPM.

### Inertia and road load

The controller reads wheel mass/radius and current ground-hit forward slip. A
solid-wheel inertia estimate is reflected through `gear * finalDrive`, including
efficiency. Gripping ground contacts add a chassis-inertia estimate; spinning or
airborne wheels lose that contribution. Once a release breaks traction, the
inertia estimate can fall immediately but does not rebuild during the release
window. Afterwards it recovers at 6/second, avoiding repeated artificial hits
from a single low-slip sample.

Road resistance is inferred from the previous applied torque and measured wheel
acceleration, using the reflected wheel inertia:

```
L = clamp(previousTc - wheelInertia * changeInDrivetrainOmega / dt,
          -maxClutchTorque, maxClutchTorque)
```

This prevents the solver from treating grounded wheels as unloaded flywheels.
History resets on neutral, shifts and the handbrake torque cut. These are read-only
load estimates: WheelCollider mass, friction, suspension and geometry are never
modified by the coupling calculation. It remains a lightweight prototype model,
not a full elastic driveshaft/tire solver.

### Release behavior and Inspector tuning

- **Manual Clutch Release Speed: 20 engagement units/second** (previously 8).
  Digital release reconnects in about .05 seconds instead of .125 seconds.
  The internal serialized field remains `manualClutchEngageSpeed` for compatibility.
- **Manual Clutch Press Speed: 20/second**, unchanged. The Inspector label now
  correctly describes the existing `manualClutchReleaseSpeed` field's function.
- **Clutch Coupling Strength: 1.2 Nm/RPM**.
- **Max Clutch Torque: 1400 Nm**, total engine-side capacity, not an additive kick.
- **Clutch Kick Strength: 1**, blending normal to rapid-release holding capacity.
- **Engine Inertia: .35 kg m²**, retained and migrated from `clutchKickEngineInertia`.

A fully depressed clutch arms one release. The measured engagement rate controls
capacity: rates <=1/second use normal engine-rated capacity; >=8/second allow full
configured kick capacity, with interpolation between them. Capacity is multiplied
by engagement. The higher capacity is permitted for .3 seconds; this is not a
torque envelope, and any torque still requires current shaft mismatch/engine
input/load. There is no redline-only activation threshold or artificial yaw force.

Removed old overlapping kick-duration/response/RPM-normalization/exponent/fade
parameters from the component and current scene. Keyboard Left Shift and
gamepad west/Square bindings are unchanged.

## Measured validation

Both baseline and final tests cloned the current scene car into isolated flat
ground physics, preserving current WheelCollider/friction/suspension/LSD setup.
Release starts at 55 km/h, gear 2, full throttle and 5000 RPM, after fully holding
the clutch. Steering input is .25. RPM is initialized at release for repeatability;
this is not a human road-performance test.

The actual current ratios (3 * 5.3) and .65 m wheel radius give about **3565–3574
drivetrain-equivalent RPM** at this speed, rather than the illustrative 3000 RPM.
No gearing or wheel radius was changed to match an illustrative number.

| Measurement | Baseline | Final fast release | Final slow release |
|---|---:|---:|---:|
| Release rate /s | 8 | 20 | .7 |
| Initial BL/BR forward slip | -.0010 / -.0010 | -.0010 / -.0010 | -.0010 / -.0010 |
| Peak total axle torque, first .5 s | 5592.6 Nm | 8013.6 Nm | 1875.2 Nm |
| Peak absolute BL/BR slip, first .5 s | .2412 / .3343 | .2951 / .2993 | .0753 / .0898 |

Final fast release first sample: 560 Nm engine-side clutch torque, 4006.8 Nm per
rear wheel, engine RPM 5000 -> 4889.6. Both rear slips exceed the unchanged .25
forward-friction extremum. Subsequent RPM follows measured wheel spin-up and
clutch reaction; it is deliberately not forced down to 3500 RPM. Ordinary throttle
already produced slip in the baseline, so increased axle torque and earlier
slip onset are more informative than claiming every later slip peak increased.

With .45 steering input, the same isolated scenario developed about 51 degrees
body slip over 1.8 seconds without countersteer: torque-induced oversteer, not a
fake yaw impulse. This is a diagnostic fixed-input case, not a drift stability claim.

At about 2521 drivetrain RPM (38.5 km/h with the unchanged gearing), releases at
3500 / 4500 / 6000 engine RPM produced BL peak slip .297 / .375 / .524 and BR
.303 / .433 / .556. This confirms useful below-redline response. At 55 km/h,
3500 engine RPM is below drivetrain RPM and correctly does not arm a positive kick.

An additional actual SampleScene Play Mode test used a temporary Input System
gamepad through the normal controller input path. Holding west/Square plus full
throttle revved freely with zero axle torque. Release from a controlled 5000 RPM
state produced **8013.6 Nm axle torque**, with BL/BR slip rising from about
-.0009 / -.0009 to **.2875 / .3015**. The test restored the original input device,
stopped Play Mode, and did not write race records or scene changes.

Checks also cover per-step engine reaction (no RPM-follow bypass), signed momentum
and energy bounds at 5/20/50 ms, total clutch cap, smooth RPM-mismatch response,
normal reverse/gear torque conversion, conserved LSD torque sums, and neutral,
shift and handbrake torque cuts. Clutch held plus throttle transmits zero torque.
Native runtime/editor compilation passes with warnings treated as errors.

Telemetry and test sources are archived under ignored
`Library/ClutchTransferValidation`, including baseline/final CSVs and Play Mode
results. Temporary validators are removed from Assets before delivery.

## Debugging and scope

On CarRoot / DrivetrainDebug, enable `captureTelemetry` for live Inspector fields.
Enable `logClutchTrace` for every-physics-step console samples: engine/drivetrain
RPM, RPM delta, pedal input, engagement/rate, engine torque, total coupling torque,
axle and individual wheel torques, both forward slips, speed, gear and kick status.
It captures telemetry even if the general capture toggle is off. Leave it off for
normal driving; the existing slower general log can miss a short clutch pulse.

Modified scripts: CarController.cs, ClutchKickCoupling.cs, DrivetrainDebug.cs.
SampleScene changes only CarRoot's CarController clutch fields. This document is
the only new deliverable file. No manual component or asset assignments are needed.
Source/scene comparisons confirm unchanged front/rear friction, combined-slip
logic, steering/Ackermann, suspension, handbrake, differential and other systems.
