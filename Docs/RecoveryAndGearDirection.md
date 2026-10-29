# Deliberate drift recovery and drivetrain direction

This pass fixes torque direction while moving against the selected gear and adds bounded support for deliberate lift-and-countersteer recovery. Current suspension, COM, anti-roll, wheel settings, visual body roll, tire curves, normal powered-drift settings, engine torque curve, gearbox ratios and input bindings are preserved. Race, score, credit, HUD/menu and effects scripts are unchanged.

## Why first gear drove backward

The old engine-RPM path averaged absolute driven-wheel RPM. That positive value was also sent to the clutch. Backward wheel rotation in first gear was therefore interpreted as forward overrun. With engine RPM below that false positive shaft speed, the signed clutch solver returned negative torque, and HandleMotor sent it directly to the wheels in a forward gear. This is why the car could actively accelerate backward. The mirrored error also affected reverse selected during forward movement.

The baseline fixture reproduced first-gear axle torque of -7212 Nm while rolling backward at 20 km/h; it continued accelerating backward. Selecting reverse during forward movement produced +6525 Nm, also the wrong drive direction.

## New direction and clutch calculation

```
gearDirection = +1 for gears 1–5, -1 for R, 0 for N
signedRearRPM = (BL.rpm + BR.rpm) / 2
signedClutchShaftRPM = signedRearRPM * gearDirection * abs(gearRatio) * finalDrive
DrivetrainRPM = abs(signedClutchShaftRPM)
ClutchRPMDifference = EngineRPM - signedClutchShaftRPM
```

Engine RPM remains a positive magnitude. The existing idle floor is retained when the shaft is aligned/nonnegative; an opposing negative shaft is passed to the clutch with its sign intact. For backward wheels in first gear, the positive engine and negative shaft create a positive mismatch and a reaction opposing backward rotation. Reverse while moving forward works symmetrically. The load estimator now uses the signed shaft-speed derivative and invalidates history when the signed gear ratio changes.

The ClutchKickCoupling solver, engine inertia, clutch capacities, release rates and kick model are unchanged. The change is supplying the correct signed shaft speed and keeping wheel-to-engine overrun distinct from powered drive:

```
scale = abs(gearRatio) * finalDrive * drivetrainEfficiency
motorAxleTorque = gearDirection * max(0, clutchTorque) * scale
engineBrakeAxleTorque = max(0, -clutchTorque) * scale
```

Positive clutch transmission drives in the selected gear's direction. Negative clutch transmission is passive engine overrun: it contributes rear brake torque only to wheels turning along the selected gear, rather than negative motor torque that can power them through zero into the wrong direction. The engine still receives the same signed clutch reaction. Neutral/shifting and a disconnected clutch transmit no drive; handbrake still suppresses the motor as before. Engine braking does not set BrakeInput or activate brake lights.

## Recovery input detection

FrontAxleRecovery now recognizes a deliberate recovery attempt when all of these hold:

- Speed is at least 28 km/h, with signed forward speed above 15 km/h.
- Slip is significant; support ramps from 25° to 40° and fades between 60° and 75°.
- Yaw is outward relative to slip; its recovery weight ramps from 15°/s to 40°/s.
- Player countersteer magnitude is at least 0.4 and opposes both heading slip and yaw.
- Throttle is below 0.35, with full lift weight at 0–0.15 and a smooth fade up to 0.35.
- All four wheels are grounded, with no handbrake or normal brake input.

The demand is exponentially blended at 6/sec into DeliberateRecoveryWeight. It requires player intent; it is not a heading controller. A powered 30° drift does not enter this state. Reverse travel is excluded so the assist does not fight a J-turn.

Front temporary authority retains its existing normal cap of 1.06. The deliberate-recovery cap is 1.16, configurable up to 1.18. The emergency target waits for the actual wheel steering to oppose the slide and for a restoring front reaction or measured trajectory recovery; boosting tires still driving the old rotation would worsen a spin. Targets use the greater of normal/emergency support, not compounded multipliers, and the existing 4/sec blend. Small/moderate corrections remain around the previous 1.00–1.06 range. Serious native save tests observed approximately 1.124, within the 1.16 cap. Base tire curves and steering lock/geometry remain unchanged.

Rear grip still returns toward its original baseline, never above it. The final recovery envelope changes smoothly from 1.8/sec to at most 3.2/sec using DeliberateRecoveryWeight. Grip-loss responses remain unchanged. The existing handbrake grip/torque path still bypasses that envelope.

Yaw recovery only removes excessive outward yaw while slip is opening:

```
recoveryLambda = 5 * DeliberateRecoveryWeight
                  * smoothstep01((abs(yaw)-20)/(65-20))
                  * smoothstep01(openingSlipRate/10)
lambda = max(existingAntiSpinLambda, recoveryLambda)
newYaw = oldYaw * exp(-lambda * fixedDeltaTime)
```

Existing contact, speed, actual sliding and continuous-sample gates also apply. It does not add recovery torque to the original assist, target slip zero, reverse yaw or write steering/linear velocity. Pitch and roll angular velocity are untouched. Once the slip is closing, this extra damping stops. Input and front/rear tire forces determine the eventual line.

## Reverse handbrake and J-turn

The existing rear handbrake already applies positive brakeTorque regardless of wheel/vehicle direction and smoothly reduces rear lateral grip toward 0.67. There was no backward-velocity gate to remove, so its settings and code remain unchanged apart from adding the separately computed passive engine-brake contribution (zero while handbrake is active).

The drivetrain sign fix makes the post-turn first-gear handoff work. A native test started at -50 km/h, coasted in neutral, steered and applied the handbrake, then released it, selected first and used forward throttle while adjusting/unwinding steering. Rear wheel RPM reached zero under handbrake, body rotation passed 160° at about 5.28 seconds and forward longitudinal speed was approximately 14 km/h at the end. This is a recorded test-driver input sequence, not production automatic steering, gear selection or a 180° button. Suitable speed, timing and steering corrections are still needed.

## Validation

Paired isolated native-physics fixtures with the final scripts:

| Case | Before | After |
| --- | --- | --- |
| Backward 20 km/h, first gear + throttle | -7212 Nm initial drive; continued backward | +7212 Nm initial drive; crosses zero at 0.92 s |
| Forward 20 km/h, reverse + throttle | +6525 Nm initial drive; continued forward | -6525 Nm initial drive; reverses correctly |
| 70 km/h, 40° slip / 60°/s initial yaw, lift/countersteer | peak 54.7°; catch at 2.04 s | peak 48.1°; catch at 1.38 s |
| 70 km/h, 40° slip / 90°/s initial yaw, lift/countersteer | peak 91.9°; exceeds 70° at 1.0 s | peak 60.7°; catch at 2.12 s |

Catch means slip at most 20° and yaw below 25°/s, without first exceeding 70° slip. Mirrored recovery matches. Full throttle with wrong steering still reaches a full spin. Normal powered-drift comparisons remain effectively unchanged: maximum differences in the paired hold fixture were below 0.0001 km/h speed, 0.0003° slip and 0.001°/s yaw. Raw motor-torque traces differ where passive negative coupling is now represented as engine braking.

Native default-physics Play Mode also passed saved-scene preservation, both save cases, wrong-input spin, backward-plus-first, reverse from rest, reverse selected while moving forward, neutral, open-clutch/reconnection, reverse handbrake/J-turn and powered drift. All captured motor torques obey selected-gear direction (or zero); no sign violations occurred. The backward-first case crosses zero at 0.96 seconds. Correct recovery cases return below 20° without crossing the 70° spin threshold; wrong input still crosses it. Deliberate recovery damping is zero in the powered-hold and reverse/J-turn cases. These are scripted native tests on flat ground, not a human road-feel guarantee or a promise that any already-developed spin can be saved.

Signed clutch regressions include both positive and negative drivetrain speeds at 5/20/50 ms timesteps, checking momentum/energy balance, combustion/load accounting, open-clutch isolation, release capacities, mismatch response and torque caps. Runtime/editor compilation passes with warnings treated as errors. Test code lives only in the isolated copy and ignored Library/RecoveryDirectionValidation, with separate test PlayerPrefs; no validation components/scripts were added to the real scene or Assets.

## Debug and changed files

Enable CarRoot / DrivetrainDebug.captureTelemetry; optional logToConsole prints signed longitudinal speed, selected gear/direction, both rear wheel RPMs, signed shaft RPM and magnitude, engine RPM, clutch torque, motor torque/direction, passive engine braking, handbrake, deliberate-recovery state/weight, front multiplier and recovery yaw damping. Capture runs after the yaw assist so damping is current. FrontAxleRecovery and DriftYawStability also expose live recovery fields. Default logging stays off.

Changed: Assets/Scripts/CarController.cs, FrontAxleRecovery.cs, DriftYawStability.cs, DrivetrainDebug.cs; Assets/Scenes/SampleScene.unity (new settings on existing CarRoot components only); this document. No new scene objects or manual references are required. ClutchKickCoupling.cs, suspension/body-roll scripts and all other protected systems are hash-identical to this pass's starting state. Scene diffs preserve every prior setting, including all suspension values and transforms, adding only the eight new recovery configuration fields.

No commit or push was made. Stop Play Mode and reload SampleScene before testing the saved configuration in an editor that was already open.
