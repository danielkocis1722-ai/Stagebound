# Engine response and acceleration feel experiment

This pass keeps max engine torque at 360 Nm, idle at 900 RPM and redline at 7500.
It changes low/mid torque delivery, moderate-surplus clutch-kick mapping, and
camera/engine-audio feedback. Ackermann/steering, tire curves, combined-slip,
suspension, handbrake, gearbox ratios, race/HUD/respawn/pause and effects are
unchanged. No visual-body pitch system, new clips, or extra physics forces were
added. No commit or push.

## Exact engine curve and delivery

The existing serialized AnimationCurve already multiplied engine torque; it was
not replaced with another engine simulation. Its new keys are:

| RPM | Multiplier | Nominal engine torque |
| --- | --- | --- |
| 900 | 0.60 | 216 Nm |
| 2000 | 0.84 | 302.4 Nm |
| 3000 | 0.95 | 342 Nm |
| 4000 | 1.00 | 360 Nm |
| 5000 | 1.00 | 360 Nm |
| 6000 | 0.90 | 324 Nm |
| 7500 | 0.65 | 234 Nm before the limiter |

Keys use zero incoming/outgoing slopes, making smooth monotonic transitions and
a flat 4000–5000 plateau. Coordinates match the CURRENT idle/redline settings.
The existing 7100–7500 soft limiter still reduces actual torque to zero at redline.
The requested stronger low/mid band is not accompanied by a high-RPM increase:
the complete 6000–7500 curve is unchanged. The 0.65 redline key is deliberately
retained instead of raising it to the suggested 0.75–0.85.

At settled full throttle in the current scene:

| RPM | Old engine torque | New engine torque |
| --- | --- | --- |
| 2000 | 253.3 | 302.4 |
| 2500 | 270.0 | 322.2 |
| 3000 | 284.1 | 342.0 |
| 3500 | 315.0 | 351.0 |
| 4000 | 345.9 | 360.0 |
| 4500 | 360.0 | 360.0 |
| 6500 | 300.7 | 300.7 |

Normal drive torque = maxEngineTorque * curve(EngineRPM) * EngineThrottle * limiter.
The existing clutch capacity and final engagement, gearing, efficiency and LSD
then determine the delivered rear torques. At 2500 RPM in second, total rear axle
torque rises from 3863.7 to 4610.7 Nm with the clutch fully engaged.

## Throttle and clutch tuning

| Inspector value on CarController | Default | Purpose |
| --- | --- | --- |
| Torque Curve | keys above | RPM torque distribution |
| Max Engine Torque | 360 Nm, unchanged | Peak rating |
| Engine Throttle Response | 24/s | Short exponential motor-throttle filter |
| Clutch Kick Mismatch Exponent | 0.65 | Stronger moderate-RPM-surplus mapping |
| Clutch Kick Midrange Fade Start RPM | 4500 | Begin returning to old mapping |
| Clutch Kick Midrange Fade End RPM | 6000 | Old mapping fully restored |
| Max Clutch Torque | 500 Nm, unchanged | Total engine-side transmitted capacity |
| Clutch Kick Strength | 0.65, unchanged | Maximum extra torque scaling |
| Clutch Kick Max RPM Difference | 3000, unchanged | Surplus normalization |
| Clutch Kick Duration / Response | 0.22 s / 18/s, unchanged | Pulse window/rise |
| Clutch Kick Engine Inertia | 0.35 kg m², unchanged | RPM debit from transferred energy |

Gamepad motor throttle was previously unfiltered. EngineThrottle now follows the
existing ThrottleInput with alpha = 1 - exp(-24 * fixedDeltaTime), reaching over
90% in five 0.02 s steps. Keyboard input smoothing remains 2.5/s attack and 3/s
release; it feeds the same short motor filter. Raw ThrottleInput remains available
for free revving, existing systems and immediate audio load response. Neutral,
shift and handbrake torque cuts still take priority.

For the clutch, n is the clamped positive RPM surplus divided by 3000, limited by
the captured release surplus. New response = Lerp(n, pow(n,0.65), midrangeWeight).
The weight is 1 up to 4500 RPM and smoothly falls to 0 at 6000 RPM. High-band
mapping, maximum pulse scaling, capacity and RPM-energy budget are unchanged.
No kick is generated from matched/negative RPM surplus, and no yaw force is used.

In integrated fixed-wheel release tests, delivered extra engine-side peak torque
at 3500 RPM (drivetrain 3250) rose 9.03 -> 21.56 Nm. At 4500 RPM (drivetrain 3900)
it rose 21.68 -> 33.49 Nm. These isolate the mapping change using the new engine
curve in both cases. They are coupling checks, not measured drift initiation.
With the current gearing/wheel radius, second gear is already about 3240 drivetrain
RPM at 50 km/h and 4540 at 70 km/h. Releasing at 3000 RPM at those speeds cannot
produce a positive energy kick; the engine must first rev above drivetrain RPM.

## Camera tuning on Main Camera > FollowCamera

| Inspector value | Default |
| --- | --- |
| Acceleration Feedback Enabled | true |
| Acceleration Pullback | 0.50 m (previous cap 0.35) |
| Acceleration Lowering | 0.05 m |
| Acceleration FOV Bonus | 3 degrees |
| Full Feedback Acceleration | 6 m/s² |
| Acceleration Response / Recovery | 10/s / 5/s |
| Torque Kick Pullback | 0.06 m |
| Torque Kick FOV Bonus | 1.25 degrees |
| Torque Kick Threshold | 150 Nm rear-axle torque rise per physics step |
| Torque Kick Duration | 0.18 s |
| Torque Kick Cooldown | 0.40 s |
| Braking Push | 0.30 m scene value, unchanged |

CarController measures change in Rigidbody velocity per physics step projected
onto horizontal vehicle forward, filters it at 14/s and exposes LongitudinalAcceleration.
Samples above 35 m/s² total acceleration or with more than 2 m predicted-position
error are rejected as discontinuities; the feedback then resets. The filtered
telemetry is bounded to +/-20 m/s². It does not alter vehicle motion.

Camera acceleration amount is a smoothed 0–1 value over 0.3–6 m/s² positive
longitudinal acceleration. It adds distance and FOV, and subtracts a little height.
Original chase/drift directions, offsets, look smoothing and speed-based 62–73
degree FOV remain. Speed FOV is smoothed separately before adding these bonuses,
so a short pulse is not swallowed by the slower speed-FOV response.

The torque hit needs a sufficient rear-axle torque rise plus rising throttle or
clutch re-engagement. It uses a short sine envelope and cooldown, rather than
continuous torque noise or speed alone. Braking/handbrake/reverse suppress it;
pause freezes it. Disabling the feedback toggle clears it. There is no shake,
constant vibration, velocity change, or visual-body rotation. All offsets affect
only the camera.

## Engine audio tuning on CarRoot > VehicleAudio

| Inspector value | Default |
| --- | --- |
| Volume Response | 18/s (previously 7/s) |
| Volume Recovery Response | 7/s |
| RPM Volume Weight | 0.15 |
| Idle / Loaded Volume | 0.22 / 1.00 scene values, unchanged |
| Min / Max Pitch | 0.7 / 2.2, unchanged |
| Pitch Response | 5/s, unchanged |

Load = throttle + 0.15 * normalizedRPM * (1 - throttle). Volume interpolates from
idle to loaded volume using that value, with faster attack and original recovery.
Full throttle requests full loaded volume even at idle RPM; small RPM contribution
keeps high-rev coasting audible. Pitch remains RPM-driven and smoothed. The same
engine layer/clip/source setup is retained, and tire-screech processing is unchanged.

## Debug, validation and scope

DrivetrainDebug adds normalized RPM, curve multiplier, filtered engine throttle,
calculated engine torque (after limiter, before kick/clutch), transmitted engine-
side torque and longitudinal acceleration. Existing final rear torques, raw
throttle, clutch/kick, speed, slip and geometry diagnostics remain. Enable Capture
Telemetry on CarRoot; no new GameObject assignment is needed.

Runtime/editor assemblies compile with warnings treated as errors. Unity-native
checks passed for the full high-RPM curve, low/mid torque, filter response, torque
caps, clutch disconnection/free revving, moderate release coupling, unchanged high-
band kick mapping, longitudinal acceleration and discontinuity detection, bounded
camera pulse/recovery/pause/no steady retrigger, camera base parameters, and audio
load response/retained clip and pitch. Temporary tests/results live under ignored
Library/EngineFeelValidation. Driving, listening and camera feel still require
manual Play Mode acceptance; no subjective road-test claim is made.

Files changed in this pass:

- Assets/Scripts/CarController.cs
- Assets/Scripts/ClutchKickCoupling.cs
- Assets/Scripts/FollowCamera.cs
- Assets/Scripts/VehicleAudio.cs
- Assets/Scripts/DrivetrainDebug.cs
- Assets/Scripts/AccelerationCameraFeedback.cs and .meta (new)
- Assets/Scenes/SampleScene.unity (engine, FollowCamera and VehicleAudio settings)
- Docs/EngineResponseAndFeel.md (new)
