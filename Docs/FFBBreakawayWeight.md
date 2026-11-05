# Steering breakaway resistance

1. **Previous static friction:** no separate static/breakaway envelope. Moving
   friction was `-tanh(wheelRate/0.3)*(0.04 + parkingBlend*0.18)` Nm. At 50 km/h
   and 0.06 rad/s this was only about 0.0079 Nm, effectively lost in motor-command
   integer rounding. Parking tire scrub was separate and remains so.
2. **New static envelope:** 1.2 Nm for steering-system friction at low movement
   speed, before master/output scaling. Exact rest still produces zero torque.
   This approximates breakaway once movement is detected; the G29 input supplies
   position, not driver torque, so true force-balanced stiction before movement
   is not inferred or claimed.
3. **Moving friction:** 0.16 Nm, plus the existing 0.18 Nm parking-only supplement.
   Existing load-based tire scrub is unchanged. There is no slip-based lightening
   of the steering-system friction at 100 km/h.
4. **Breakaway transition:** characteristic speed 0.35 rad/s (~20 degrees/s).
   Smooth direction reversal uses tanh(rate/0.025 rad/s); no sign() discontinuity,
   angle test, centre notch or steering deadzone is used. Developer tuning fields
   are exposed in the existing per-vehicle FFB tuning object.
5. **Damping:** unchanged, including existing general and centre-approach damping.
6. **Stribeck-like formula:**

   `direction = -tanh(wheelRate / zeroVelocityScale)`

   `breakaway = direction * max(0, staticFriction-movingFriction) * exp(-(wheelRate/breakawayVelocity)^2)`

   `coulomb = direction * (movingFriction + parkingBlend*parkingFriction)`

   `total steering friction = breakaway + coulomb`

   At 50 km/h and 0.06 rad/s, the total is approximately -1.15 Nm for rightward
   movement, versus -0.0079 Nm before. At 60–75% master this corresponds to about
   -7% to -9% steady motor demand from friction alone. At 2 rad/s the breakaway
   excess is negligible and only 0.16 Nm moving friction remains. At rest both
   parts are zero. The formula is identical at -5, 0 and +5 degrees because it
   does not consume wheel angle.

7. **0 km/h centred feel:** awaiting physical validation. Parking supplement
   and tire scrub remain active, in addition to the new breakaway envelope.
8. **10 km/h small corrections:** awaiting physical validation.
9. **50 km/h centred feel:** awaiting physical validation.
10. **One-finger-light feel resolved:** not yet established on hardware.
11. **Self-centering:** SAT/caster formulas, sign and strength unchanged. New
    friction opposes any movement, including self-return; weak near-neutral SAT
    may settle sooner. This tradeoff must be checked physically.
12. **Drift countersteer:** strong SAT can exceed the bounded friction envelope,
    and breakaway excess rapidly falls with wheel speed. Physical countersteer
    preservation remains unverified for this revision, not guaranteed by maths.
13. **Notch/jitter:** no angle-dependent notch is introduced and the friction
    law has a continuous zero crossing. Actual sensor noise, integer motor
    commands and the existing output filter can still affect feel; inspect the
    recorded rate/torque traces for jitter during the physical test.
14. **Files changed:**
    - Assets/Scripts/SteeringForceFeedback.cs
    - Assets/Editor/LogitechWheelInputInspector.cs
    - Assets/Editor/SteeringForceFeedbackValidation.cs
    - Docs/FFBBreakawayWeight.md

The inspector separately displays breakaway excess, moving/Coulomb friction,
general damping, centre damping, SAT, scrub and final motor output. CSV recording
adds `breakaway_nm` and `coulomb_nm`; existing `friction_nm` is their sum, not a
third torque. No advanced controls were added to the normal player settings UI.

Runtime/editor compilation passed. New Unity checks cover symmetric motion
opposition, zero-speed continuity, Stribeck reduction with speed, high-road-speed
weight retention, bounded friction and representative strong-SAT headroom.
Their execution remains pending; earlier PASS logs do not validate these new
checks. Reload scripts after ending the driving session, then use Stagebound >
Validate FFB torque signs if needed. `Logs/FFB/breakaway-validation.csv` records
the checked friction curve. Tests do not activate the hardware.

Restart Play Mode for updated defaults. Retain the working Invert and use
60–75% master. Check stationary first motion, 10 km/h ±10-degree corrections,
50 km/h small corrections, normal return and finally drift countersteer.
F8/F9/F10 telemetry recording remains available. No new hardware test was claimed.

Vehicle handling, yaw tuning, tire grip, camera, steering/input, caster geometry,
H-shifter and Controls UI were not changed. No commit or push.
