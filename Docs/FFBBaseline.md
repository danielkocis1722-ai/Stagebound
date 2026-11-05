# FFB diagnosis — baseline pending

The physical rolling baseline requested before tuning has not yet been performed.
Only telemetry was added; all force calculations, tuning defaults, input settings,
motor polarity and vehicle physics remain unchanged.

## Capture in Unity Play Mode

1. Select AE86, use the G29, enable FFB and set strength to 25%.
2. With Game view focused, press F8 to begin recording.
3. Roll at 30 km/h, hold full-right steering, then press F9 as you loosen/release
   the physical wheel. F9 is an operator marker, not automatic release detection.
4. Press F10 after observing the return. Recording also saves after 120 seconds
   or when the component is disabled. Files go to this project's `Logs/FFB`.
5. Repeat with approximately +180 degrees at 10, 30, 60 and 100 km/h, then
   normal corners, understeer, drift and transitions. Repeat for R32 and RX-7
   after AE86 tuning is validated. Use a separate recording for each trial.

The CSV includes both actual collider steering angles, individual estimated tire
forces and trail moments, wheel torque components, normalized filtered output,
accepted motor percentage, master, invert, driving validity and release marker.
Rows are recorded after the motor-output update. Tire state is from the latest
physics tick. Tire forces are estimates, not measured WheelCollider lateral forces.
The companion summary gives start/final angle, peak SAT, peak accepted motor
percentage and time to half the starting angle. Examine the CSV for invalid
driving intervals and speed changes before interpreting a return result.

## Findings and requested report status

1. Likely weakness: large-slip pneumatic trail collapses, leaving only 12 mm
   mechanical trail, followed by a 14 Nm divisor and another 0.45 multiplier.
   This is a code diagnosis, not yet a measured cause of failed physical return.
2. Old default output at 25% master: theoretical +/-11.25% before integer
   rounding (maximum command +/-11%). A steady 1 Nm total wheel torque gives
   0.80%; 4 Nm gives 3.21%. Actual normal-driving range remains unmeasured.
3. New range: unchanged pending baseline.
4. Mechanical moment: estimated lateral force times 0.012 m trail, speed faded.
5. Pneumatic moment: estimated lateral force times 0.055 m trail times the
   slip curve, speed faded. The curve reaches zero at 25 degrees slip.
6. Slip falloff was already separate: mechanical trail does not use that curve.
7. Caster: no explicit caster projection in the existing implementation.
8. Low-speed fade: SmoothStep from 3 to 20 km/h of contact-plane speed.
9. Damping: 0.08 Nm per rad/s, opposing filtered physical wheel angular velocity.
10. Friction: up to 0.04 Nm, opposing physical wheel movement.
11. Normalization: divide total wheel torque by 14 Nm, apply vehicle strength,
    clamp and filter, multiply by output cap and master. Native API accepts
    signed integer percentage (-100 to +100); percentage is not calibrated Nm.
12. Default cap: 0.45 multiplicative gain; final safety limit +/-60%.
13. Invert: unchanged; hardware polarity still requires the +/-15% pulse and
    rolling sign test. Mathematically, forward travel with right steer yields
    negative slip, positive Fy, and a leftward aligning moment.
14–19. Hands-off return, corner feel, understeer, AE86 countersteer, R32/RX-7
    behavior and oscillation are all pending physical validation.
20. Changed files: SteeringForceFeedback.cs, LogitechWheelInputInspector.cs,
    and this document. No commit or push.

Validation: dotnet build could not run because no .NET SDK is installed.
Unity compilation and physical recording are still required.
