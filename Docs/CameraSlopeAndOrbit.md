# Slope-aware chase camera and analog orbit — 2026-11-04

Scope: CURRENT Stagebound-real-scale project, camera only. No commit or push.
Earlier handling, stronger brakes and accepted camera tuning are preserved.

## Existing architecture and downhill cause

FollowCamera is the existing independent chase MonoBehaviour (execution order
200). It follows CarRoot/Rigidbody position, smoothing yaw between horizontal
car-forward and velocity directions for drift. It already owns distance/FOV
speed effects and measured acceleration, braking and drivetrain-hit feedback.

Both car-forward and velocity were explicitly projected onto Vector3.up's plane.
Position and look-ahead therefore had no vertical travel direction. The body could
point down a mountain while the camera continued using a horizontal chase frame.
There was no camera obstacle-avoidance system.

## Grounded slope, without body rotation or roll

FixedUpdate reads the four existing WheelCollider.GetGroundHit contacts. A new
normal estimate requires at least three valid grounded wheels, including both
axles. Normals facing less than .35 toward world-up are rejected. The average
normal is filtered using an exponential blend at GroundNormalSmoothing=3 /s.
It never writes wheel, Rigidbody or controller state.

LateUpdate projects the existing chase heading onto that filtered road plane,
extracts only its elevation, removes a 1-degree noise deadband, scales by .85 and
clamps to +/-25 degrees. SlopeSmoothing=2 /s and a 12 degree/s rate cap filter the
pitch again. The normal's time constant is about .33 s; pitch about .5 s, plus
the rate cap for large changes. Small suspension/body movement is not a source.
If reliable contacts are lost, hold the previous road for .6 s then very slowly
relax the normal toward level (.3 /s). Airborne velocity/body pitch never drives it.

The slope rotation affects the chase offset AND forward look target. The final
look direction is rebuilt with world-up each frame, keeping roll level even when
the road is banked. A flat road with no manual input retains the accepted chase
composition and all distance/FOV/feedback values.

## Right-stick input and orbit

Driving currently reads Input System Gamepad/Keyboard controls directly; it does
not use a PlayerInput driving map. The asset has a generic Player/Look binding
for rightStick as well as mouse/pointer delta. FollowCamera owns a separate
CameraLook Vector2 InputAction bound ONLY to <Gamepad>/rightStick. Enable/Dispose
follow the component lifecycle; no shared action map, UI binding or driving input
is changed. No mouse orbit was added.

Input System stick processing is retained. OrbitDeadzone=.12 is an additional
radial deadzone, editable in Inspector; zero uses only the project's deadzone.
Input magnitude scales angular velocity continuously.

| Setting | Current default |
| --- | --- |
| OrbitYawSpeed | 90 degrees/s at full stick |
| OrbitYawLimit | +/-140 degrees |
| OrbitPitchSpeed | 35 degrees/s at full stick |
| OrbitPitchMin / Max | -10 / +18 degrees |
| OrbitReturnDelay | .6 s |
| OrbitReturnSpeed | 3 /s exponential blend |
| InvertOrbitX / Y | false / false |

Right moves the camera to the car's right side; up raises its position. The
position rotates around CarRoot + the existing .4 m lookHeight pivot. Yaw/pitch
rotate the offset, rather than turning the camera in place; rotations preserve
the desired radius. Existing follow-position smoothing still applies. At large
orbit angles, look-ahead smoothly reduces toward the pivot to frame the car.
The radius/limited elevation stay outside the vehicle in the tested views.

During input and the hold delay, the camera's manual heading is independent of
automatic chase/drift yaw, which continues updating in the background. This
prevents drift yaw from cancelling the player's camera movement. After the delay,
manual angles and heading blend back toward the current automatic chase heading.
Road slope, speed distance, FOV, acceleration and braking feedback remain active.
Pause freezes orbit with game time; input is suppressed while the car is disabled.
Scene changes dispose the old action. No new roll response was introduced.

No existing collision system could be reused. This change does not add scenery
collision avoidance: a wall may still obstruct an orbital view on a narrow road.

## Files and Inspector tuning

Changed runtime file: Assets/Scripts/FollowCamera.cs.
Added camera settings only to the existing FollowCamera component in:
Assets/Scenes/SampleScene.unity, TrackCompleCircuit.unity,
CherryMountain.unity and DriftTracks.unity. This document is new.

No assignments are needed. On each scene's existing chase camera component, tune
Road slope / Right-stick orbit. First adjust slopeFollowStrength or
slopePitchLimit if hills feel too strong, groundNormalSmoothing/slopeSmoothing
if they feel noisy or delayed, and orbitYawSpeed/orbitReturnDelay for personal
control preference. Invert toggles and the additional deadzone are available.
Existing speed and acceleration settings were not retuned in this task.

Before/after SHA256 checks confirm all other runtime scripts (including
CarController, brakes, scoring, respawn, suspension) and all vehicle configs are
unchanged during this task. Scene diffs contain only the added camera fields.
Existing unrelated Unity settings/earlier changes are preserved.

## Validation

Warnings-as-errors runtime compilation succeeds. Native Unity 6000.6.7f1 Play
Mode runs in a separate test project/product namespace with synthetic gamepad
state, preserving the user's records and HUD settings.

Tests cover continuous half/full analog response, direction, yaw/pitch limits,
noise rejection, hold/delayed return, horizon stability, motion at 0/30/60/100 km/h,
sideways travel/drift with manual priority, speed FOV, pause, and action cleanup
across scene changes. Actual Cherry Mountain road meshes provide four grounded
contacts on a roughly 34.9-degree route segment, tested downhill and facing uphill.
Measured camera slope is about -24.3 / +25.0 degrees, capped at +/-25. Tests also
cover hill-to-flat smoothing and rejection of body pitch/roll. Camera renders were
reviewed for downhill/uphill road visibility and side/low orbital framing.

See ignored Library/CameraSlopeOrbit for final test results, harness, screenshots,
protected-file manifests and the previous FollowCamera source. This is automated
validation, not a claim of a human DualSense drive or complete obstacle coverage.

Final result: 47 assertions pass. An actual unconstrained Cherry downhill coast
moves 15.319 m; all 90 measured frames retain >=3 wheel contacts, final filtered
slope is -13.760 degrees as the route changes, and maximum per-frame slope change
is .2000 degrees (60 fps). The final hill-to-flat and body-tilt checks also pass.
