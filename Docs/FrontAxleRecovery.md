# Front axle authority and countersteer recovery

Front-only pass on the current SampleScene and current clutch/handling baseline.
No commit or push. Rear friction/combined slip, clutch/engine, handbrake,
suspension, Ackermann geometry, scoring and all game-flow/presentation systems
are unchanged by this pass.

## Inspection and final friction values

Real WheelHit samples from isolated physics using the current scene car:

- Ordinary cornering: peak absolute FL/FR sideways slip about .169/.180,
  below the .30 extremum-slip point.
- Samples at 20–30 degrees body slip: mean absolute FL/FR about .363/.410,
  beyond that peak point.
- Excessive-oversteer traces reached the .80 asymptote region and peaks above
  1.2. The previous sliding region still retained .95 of the peak curve value;
  it was not zero-grip.
- Strong correction took about .28 seconds to reach the requested wheel angle
  in the delayed-countersteer fixture, so wheel interpolation contributes lag.

Both front WheelColliders now use:

| Sideways friction | Before | Final |
|---|---:|---:|
| Stiffness at normal grip | 1.26 | 1.26 |
| Extremum slip | .30 | .30 |
| Extremum value | 1.00 | 1.00 |
| Asymptote slip | .80 | .80 |
| Asymptote value | .95 | .98 |

The sliding-curve strength rises about 3.2%, with the normal peak unchanged.
Global stiffness increases of 5% and 6.35% were tested but raised yaw peaks in
the recovery comparisons. They were not retained. Additional stiffness is
conditional on the player's recovery instead.

## FrontAxleRecovery component

Installed once on CarRoot; automatically reads its existing CarController,
Rigidbody and front/rear WheelColliders. It writes only the two front sideways
stiffness values, always relative to the captured baseline, never cumulatively.

Detection uses horizontal body velocity and projected forward direction:

```
recovery angle = SignedAngle(horizontal velocity, vehicle forward, world up)
```

Positive means the body points right of its travel direction, so recovery
steering is left. This is the opposite signed convention to the existing
CarController.VehicleSlipAngle and DriftTelemetry.VehicleSlipAngle. Those
existing APIs and drift-scoring direction calculations are unchanged. The new
debug field `frontRecoverySlipAngle` explicitly identifies the recovery convention.

Recovery requires:

- Horizontal speed >=28 km/h and forward component >=15 km/h.
- Body slip >=15 degrees, fading out between 60 and 75 degrees.
- Both front and rear wheels grounded.
- A rear tire with absolute sideways slip >=.15 or forward slip >=.25.
- Player steering opposite the recovery angle, above .08 input; input strength
  blends to full influence at .35.
- Actual front steering has crossed into the requested recovery direction.
- A restoring measured front-slip reaction, or body/travel angular error already
  shrinking faster than .5 degrees/second. This avoids amplifying an opening
  spin merely because the player has requested opposite lock.

The last check uses load-weighted front sideways slip and measured angle change.
It does not add a yaw force, rotate velocity, change the transform or steer for
the player. A correction that has not physically begun catching may not receive
the full grip boost yet.

Default editable angle curve:

| Body slip | Target front multiplier |
|---|---:|
| 15 degrees | 1.00 |
| 20 degrees | 1.02 |
| 30 degrees | 1.08 |
| 40–60 degrees | 1.12 |

Target is blended with detection/input weights and approached exponentially at
6/second. Default cap is 1.12; the configurable cap has an absolute safety limit
of 1.15. At the default cap, front stiffness is 1.4112 rather than 1.26. The boost
fades smoothly to baseline when the conditions end. Disabling the component
immediately restores both captured baseline stiffnesses.

## Steering response

Normal steering remains 95 degrees/second, with the existing 75-degree return
rate, input curves, keyboard smoothing, deadzones, 30-degree inside lock and
Ackermann geometry unchanged.

For genuine drift with correctly opposed player input, the wheel response rate
can increase up to 20% (114 degrees/second). This is configurable from 1.0 to 1.2.
It speeds movement toward the player's existing requested angle; it does not
change that requested angle or write player input. Setting it to 1 restores the
original response while retaining the optional grip support.

## Validation and limits

Current-car physics comparisons at 70 km/h, gear 2, include normal cornering,
25-degree drift, excessive oversteer, throttle lift, mirrored slides and paired
old/new recovery trials. Preview scenes use the same body, wheels, rear curves,
clutch and suspension. Test-only old-config clones restore just the old front
curve and disable the recovery component.

The correction fixtures include both fixed input and a diagnostic driver that
countersteers, damps yaw and unwinds the steering. This diagnostic driver exists
only in the archived validation script, not in game code.

- Delayed strong correction reaches the requested angle in .24 seconds versus
  .28 before. A paired 40-degree driver correction reaches it in .28 versus .32.
- At 25 degrees with power and the same diagnostic driver, peak absolute yaw
  decreases from about 42.1 to 37.7 degrees/second. Both settle to roughly
  .4 degrees body slip, with the conservative catch metric at .54 seconds.
- At 40 degrees with throttle lift and steering unwind, both configurations
  catch in about .94 seconds and settle near .6 degrees. Front saturation samples
  fall from 12 to 10. This fixture did not demonstrate a faster overall catch;
  the measured improvement is wheel response and added conditional authority.
- Holding full lock without unwinding, or waiting until an excessive spin has
  developed, can still spin the car. The system is deliberately bounded and
  is not an automatic straighten/recovery controller.

Actual SampleScene Play Mode also checks the normal Input System path with a
temporary gamepad: no boost at rest, no boost steering into the slide, activation
during grounded countersteered drift, 20/30/40 curve values, absolute/noncompounding
stiffness, both multiplier caps and baseline restoration when disabled. The
25-degree road diagnostic ended around -1.9 degrees body slip, with a maximum
observed multiplier about 1.010 and response 1.200. Target curve values are not
instantaneous guarantees: the real multiplier depends on input, contact state,
force/progress gates and blend time.

Play Mode stops after the diagnostic and the temporary input device is removed.
Race triggers are disabled only in the runtime test copy, preventing record writes.
Runtime/editor compilation uses warnings as errors. Sources, results and CSVs
are archived under ignored `Library/FrontRecoveryValidation`; temporary
validators are removed from Assets before delivery. Player driving assessment
is still needed for feel and preferred recovery strength.

## Inspector and changed files

CarRoot / FrontAxleRecovery exposes speed/forward-speed/angle gates, rear-slip
thresholds, minimum countersteer input, recovery curve/cap, blend response and
recovery steering response. No manual references or new assets need assignment.

Enable CarRoot / DrivetrainDebug.captureTelemetry for Inspector values; its
existing logToConsole option prints steering/slip/yaw plus the new recovery angle,
front multiplier, active flag and response multiplier. The ordinary HUD stays
unchanged and debug capture remains optional.

Created: Assets/Scripts/FrontAxleRecovery.cs, its metadata and this document.
Modified: CarController.cs (component lookup and steering-rate hook),
DrivetrainDebug.cs (recovery fields), SampleScene.unity (CarRoot component and
only the FL/FR sideways asymptote values). No other script or scene block was
changed by this pass.
