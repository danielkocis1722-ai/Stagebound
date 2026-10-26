# C7 — Handling V2

Handling-only changes relative to the working prototype before this pass:

| Setting | Before | C7 |
| --- | --- | --- |
| Max engine torque | 280 Nm | 360 Nm |
| Controller deadzone | 0.08 | 0.06 |
| Steering exponent | Fixed 1.7 | 1.4 at low speed, smoothly approaching 2.1 at 130 km/h |
| Rear forward asymptote value | 0.50 | 0.72 |
| Rear forward stiffness | 0.95 | 0.92 |

The steering curve remaps the input outside the deadzone into 0–1, then raises it
to a speed-dependent exponent. The blend uses SmoothStep over 0–130 km/h. The
original sign is restored afterward. Full stick always produces ±1, and the
configured maximum angle remains 34 degrees at every speed. Existing steering
angle smoothing and keyboard controls are unchanged. The old serialized
`steeringExponent` name migrates to `lowSpeedSteeringExponent` in other scenes.

Only rear longitudinal friction changed. Both rear wheels retain forward extremum
slip/value 0.40/1.00 and asymptote slip 0.80. Rear lateral stiffness stays 1.20;
front tire curves are unchanged. The aim is progressive power-on wheelspin rather
than permanently weak lateral grip. No automatic countersteer or yaw assist was added.
The old DriftStability component remains disabled.

The existing suspension already fits C7's target, so it is retained: mass 1100 kg,
COM Y −0.35, angular damping 0.225, travel 0.32 m, springs 20000, dampers 2800,
target position 0.50, force application distance 0.15. Active anti-roll stays
2500 front / 1800 rear. Handbrake stays rear-only at 15000 torque, with 0.55 grip
multiplier, drop speed 12, and recovery speed 3.5.

The commit also captures the existing working prototype scripts, scene references,
model, audio clips and effect assets that were not yet committed. Those systems were
not redesigned in this handling pass. Recovery scenes and Unity-generated folders
are excluded. Brake emission remains deliberately disabled until rear-only lamp
geometry is separated in Blender; see VehicleFeedback.md.

## Manual feel checks

- At 100–140 km/h compare 10–25% stick corrections with half stick. Full stick must
  still reach the full 34-degree target; the original steering slew rate still applies.
- In gear 2 at 50–80 km/h, steer into a corner, then progressively apply R2. Check
  whether power oversteer builds toward 15–30 degrees without the handbrake.
- At 70–100 km/h, hold and transition a 20–35 degree drift using countersteer and
  throttle. Confirm the existing handbrake remains the more aggressive initiation.
- If wheelspin is excessive, first lower torque toward 350 or raise rear forward
  stiffness toward 0.94. If initiation is still too difficult, compare 365 Nm / 0.90.
  Keep rear lateral stiffness and suspension unchanged while comparing these options.

These are tuning targets, not measured guarantees of a particular drift angle.

## Validation

- Runtime and editor assemblies compiled with Unity 6.6's bundled compiler and warnings treated as errors: zero errors or emitted warnings.
- Unity loaded SampleScene in a preview scene and confirmed serialized C7 values, one controller, valid race setup, retained gameplay/effect references, and disabled DriftStability.
- Steering tested at 0, 40, 60, 80, 100, 120, 130, 140 and 200 km/h: full lock, deadzone, symmetry, bounded output and monotonic response passed.
- At 20% stick, normalized steering is approximately 0.06953 at low speed and 0.01834 at 130 km/h.
- Compared with the task-start snapshot, only CarController.cs changed among scripts. Only the controller settings and two rear WheelCollider friction blocks changed in SampleScene.
- Actual DualSense driving feel and drift-angle targets still require manual Play Mode testing; numerical checks do not establish those subjective/dynamic targets.
- Temporary validation tooling and results remain under ignored Library/C7Validation, outside the commit.

## Complete committed file list

This includes the existing uncommitted working prototype baseline as well as the C7 handling changes above.
- `.gitignore`
- `Assets/Audio.meta`
- `Assets/Audio/Tires.meta`
- `Assets/Audio/Tires/tires_squal_loop.wav`
- `Assets/Audio/Tires/tires_squal_loop.wav.meta`
- `Assets/Audio/Vehicles.meta`
- `Assets/Audio/Vehicles/Engine.meta`
- `Assets/Audio/Vehicles/Engine/loop_0.wav`
- `Assets/Audio/Vehicles/Engine/loop_0.wav.meta`
- `Assets/Editor.meta`
- `Assets/Editor/RaceRecordsMenu.cs`
- `Assets/Editor/RaceRecordsMenu.cs.meta`
- `Assets/Editor/RaceSetupMenu.cs`
- `Assets/Editor/RaceSetupMenu.cs.meta`
- `Assets/Effects.meta`
- `Assets/Effects/VehicleFeedback.meta`
- `Assets/Effects/VehicleFeedback/Skidmarks.mat`
- `Assets/Effects/VehicleFeedback/Skidmarks.mat.meta`
- `Assets/Effects/VehicleFeedback/TireSmoke.mat`
- `Assets/Effects/VehicleFeedback/TireSmoke.mat.meta`
- `Assets/Effects/VehicleFeedback/VehicleFeedback.shader`
- `Assets/Effects/VehicleFeedback/VehicleFeedback.shader.meta`
- `Assets/Models/Cars/Materials/Body 1.mat`
- `Assets/Models/Cars/Materials/Body 1.mat.meta`
- `Assets/Models/Cars/ToyotaAE86-lights.fbx`
- `Assets/Models/Cars/ToyotaAE86-lights.fbx.meta`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scripts/AntiRollBar.cs`
- `Assets/Scripts/AntiRollBar.cs.meta`
- `Assets/Scripts/BrakeLights.cs`
- `Assets/Scripts/BrakeLights.cs.meta`
- `Assets/Scripts/CarController.cs`
- `Assets/Scripts/DraggableHUDPanel.cs`
- `Assets/Scripts/DraggableHUDPanel.cs.meta`
- `Assets/Scripts/PauseMenu.cs`
- `Assets/Scripts/PauseMenu.cs.meta`
- `Assets/Scripts/RaceHUD.cs`
- `Assets/Scripts/RaceHUD.cs.meta`
- `Assets/Scripts/RaceTimer.cs`
- `Assets/Scripts/RaceTimer.cs.meta`
- `Assets/Scripts/RaceTrigger.cs`
- `Assets/Scripts/RaceTrigger.cs.meta`
- `Assets/Scripts/RespawnManager.cs`
- `Assets/Scripts/RespawnManager.cs.meta`
- `Assets/Scripts/SkidmarkSystem.cs`
- `Assets/Scripts/SkidmarkSystem.cs.meta`
- `Assets/Scripts/Speedometer.cs`
- `Assets/Scripts/VehicleAudio.cs`
- `Assets/Scripts/VehicleAudio.cs.meta`
- `Assets/Scripts/WheelEffects.cs`
- `Assets/Scripts/WheelEffects.cs.meta`
- `Docs/C7Handling.md`
- `Docs/VehicleFeedback.md`
- `ProjectSettings/EditorSettings.asset`
