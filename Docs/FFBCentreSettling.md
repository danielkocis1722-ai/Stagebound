# Centre overshoot revision

1. **Likely cause:** wheel momentum reaches centre while the previous model has
   only light general damping (0.08 Nm per rad/s). Its angular-rate filter and
   SAT/output filter also have lag. This is consistent with the reported
   overshoot, but no new hardware trace has established the relative contribution
   of wheel inertia, tire demand and filter lag. Nonzero opposite steering during
   a real slide can still be legitimate countersteer.
2. **Added logic:** a separate velocity-opposing centre damper, evaluated from
   measured physical wheel movement each render sample. It activates on inward
   travel near centre, fades smoothly with proximity, and has a soft one-degree
   tail immediately across centre. A sampled sign crossing is also recognized
   if it remains within the centre window. There is no latched brake, steering
   position write, input deadzone, spring or force toward a selected heading.
3. **Window:** configurable 5 physical wheel degrees. The proximity weight is
   `1-SmoothStep(0,1,abs(angle)/window)`. Away from centre the new term is zero.
   Outward travel outside the one-degree tail is untouched unless that sample
   has just crossed zero. The tail fades to zero by one degree of opposite lock.
4. **Strength:** coefficient 0.25 Nm/(rad/s), limited to 1.5 Nm before confidence,
   proximity and speed weights. Velocity activation fades from 0.1 to 0.8 rad/s;
   held or nearly stationary steering does not receive the new damping. It fades
   in over 2–8 km/h, is strongest at normal driving speeds and reduces smoothly
   to 40% over 70–130 km/h. Base damping, friction, scrub, SAT, caster, trail,
   master scaling, inversion and motor ceiling are unchanged.
5. **Countersteer exception:** maximum absolute individual front slip is used,
   not axle-average slip. Confidence fades from 0.5 to 3 degrees tire slip, and
   separately from 0.25 to 1.5 Nm absolute current SAT. Either strong tire slip
   or meaningful SAT completely bypasses the centre brake. Both front contacts
   must be loaded. Thus opposite tire slips cannot average out and incorrectly
   enable the brake, and strong tire demand of either sign is preserved. This is
   a conservative state-based exception, not a drift-angle or scoring trigger.

The new torque is added **after** the existing low-pass normalized base signal,
then passes through the same master strength, inversion and hard motor cap.
Filtering this brief damping pulse through the old roughly 70 ms output time
constant would delay it until after the crossing. It always opposes sampled
wheel velocity; there is no extra slow filter retaining the new brake after a
direction reversal. The existing base signal is otherwise unchanged.

No optional centre SAT deadband was added: basic restoring torque remains
intact. The new damping vanishes at low wheel velocity instead of creating a
magnetic centre or blocking precise steering. At extremely fast wheel rates a
5-degree crossing is brief; effectiveness must be assessed on hardware rather
than inferred from the torque formula.

6. **10 km/h ±90-degree release:** physical result pending.
7. **30 km/h ±150-degree release:** physical result pending.
8. **Settling at centre / ordinary corner release:** pending.
9. **Physical drift countersteer crossing:** pending. The software bypass is
   covered by regression checks, not by a new physical drift test.
10. **Remaining oscillation:** unknown until measured. No success claimed.
11. **Changed files:**
   - Assets/Scripts/SteeringForceFeedback.cs
   - Assets/Editor/LogitechWheelInputInspector.cs (developer telemetry only)
   - Assets/Editor/SteeringForceFeedbackValidation.cs
   - Docs/FFBCentreSettling.md

## Telemetry and validation

Live inspector adds physical wheel angular velocity (rad/s) and centre-damping
Nm. Existing F8/F9/F10 recording adds `wheel_rate_rad_s`, `center_damping_nm` and
`final_normalized_demand`. `wheel_sat_nm` and `damping_nm` remain separate from
the new damper. `total_wheel_nm` includes it; `filtered_torque_nm` and
`filtered_normalized` describe the filtered **base** signal. Final motor percent
includes the fast centre damper and all existing output controls.

Runtime and editor compilation passed using Unity's bundled compiler. New
regression cases cover left/right symmetry, motion opposition, no effect outside
the window, no stationary effect, speed fading, no low-velocity brake, the brief
crossing tail, strong-SAT/slip bypass, missing-contact bypass and the torque limit.
These new cases have not yet executed in Unity; the existing PASS log predates
this revision. Unity was in the gameplay view, so no forced reload or driving
interruption was performed. After stopping/restarting Play Mode, they run on
script reload, or via Stagebound > Validate FFB torque signs. They produce
Logs/FFB/centre-damping-validation.csv and update model-validation.txt.

Retain the current working strength and Invert settings. Record both directions
at 10 and 30 km/h, then normal corner release and drift centre crossing. Inspect
the new term around zero: it should oppose motion only while tire demand is
near neutral, and disappear when the front tires demand countersteer.

Vehicle dynamics, tire grip, steering/input logic, camera, H-shifter and player
FFB UI were not changed. No commit or push.
