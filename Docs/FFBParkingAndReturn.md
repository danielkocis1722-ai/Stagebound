# Parking resistance and rolling return revision

This supersedes the earlier tuning report. All work is in Stagebound-real-scale.
The user physically confirmed the existing +150-degree return direction; that
sign path and the existing Invert setting are preserved. This revision's feel
has not yet been physically validated.

## Requested report

1. **Why 0–2 km/h was free:** tire slip SAT had almost no rolling fade, parking
   contact-patch torsional resistance was absent, and 0.04 Nm friction plus light
   damping was heavily attenuated by the 14 Nm divisor and 0.45 output multiplier.
2. **Parking scrub:** per grounded front tire, wheel-equivalent torque is
   `-Fz * muScrub * radius * tanh((wheelRate/ratio)/velocityScale) * blend / ratio`.
   Defaults: muScrub 0.5, effective contact torsion radius 0.010 m, road-rate
   smoothing scale 0.025 rad/s. Fz comes from WheelHit.force. Contact-plane speed
   comes from the existing relative contact velocity. No contact gives zero.
   The physical wheel rate divided by steering ratio estimates road steering
   movement; this resists physical motion even when collider steering hits lock.
   Radius is an effective contact-patch friction arm, not tire rolling radius.
3. **Mechanical friction:** smooth Coulomb-like resistance
   `-tanh(wheelRate/0.3) * (0.04 + 0.18*parkingBlend)` Nm. Both scrub and friction
   use measured physical wheel movement, not steering position. Both become
   zero at zero rate; no artificial static holding force or centre spring is
   added. The output filter may retain a short decaying tail after motion stops.
4. **Scrub fade:** `1-SmoothStep(3,15,abs(speedKmh))`; full at 0–3 km/h, 92.6% at
   5, 37.6% at 10 and zero at 15+. Scrub uses each tire's contact speed; rack
   friction uses car speed. Rolling mechanical fade starts at 0.5 km/h and is
   full at 12; pneumatic fade remains 3–20. Both rolling components vanish at 0.
5. **Previous mechanical/caster scale:** latest actual code was 20 mm trail,
   not the original 12 mm, with 6-degree caster and 2–12 km/h mechanical fade.
6. **New scale:** 35 mm effective trail, same 6-degree caster, 0.5–12 km/h fade.
   At full fade both mechanical and existing geometric caster moments are 1.75x
   the latest version. Mechanical is `-Fy*trail*cos(caster)`; caster approximation
   is `-Fz*trail*sin(caster)*sin(steer)`. These are existing formulas, not a new
   position spring. Their load dependence, rack ratio and working signs remain.
   Pneumatic slip falloff remains independent and unchanged. Geometry values
   are exposed development approximations, not measured production-car values.
7. **Output scaling:** old steady output was
   `clamp(T/14,-1,1) * 0.45 * master`.
   New steady output is `clamp(clamp(T/10,-1,1)*master,-0.60,+0.60)`.
   Vehicle gain and existing time filtering remain in their existing positions.
   Invert is applied once after the final clamp. 10 Nm is a simulated-torque
   normalization setting, not a claim about the G29's measured shaft torque.
   The per-config maximumOutput is now a ceiling after master rather than a
   multiplier; the code also enforces the 60% absolute ceiling. Existing custom
   config caps remain respected. No serialized overrides were found in assets.
   Damping remains 0.08 Nm/(rad/s); friction above 15 km/h remains at most 0.04 Nm.

8–14. **Physical validation matrix:** all results for this revision are pending:

| Speed | Required physical observation | Status |
|---|---|---|
| 0 | Resistance during 90/180/300-degree turns; no aggressive return when stopped | Pending |
| 2 | Clear motion resistance with small alignment | Pending |
| 5 | Resistance blending into return | Pending |
| 10 | Clear self-centering | Pending |
| 20 | Strong natural return | Pending |
| 30 | +150-degree release visibly unwinds within first few tenths | Pending |
| 60 | Strong stable return without violent oscillation | Pending |

15. **60–70% player strength sufficient:** not physically established. The
   synthetic 60%-master case below now reaches the 60% motor ceiling. At strong
   saturation 60/70/100% can share the same ceiling; the slider scales sub-cap
   forces and changes when clipping begins. It is not useful to raise master
   to improve a command already at the ceiling.
16. **Drift countersteer:** not tested; wait for normal steering matrix to pass.
   No drift-specific torque was added.
17. **Oscillation:** physical result unknown. Damping was not increased. The
   substantially stronger output requires checking centre crossing and settling.
18. **Files changed this revision:**
   - Assets/Scripts/SteeringForceFeedback.cs
   - Assets/Editor/LogitechWheelInputInspector.cs
   - Assets/Editor/SteeringForceFeedbackValidation.cs
   - Assets/Editor/ControlsSettingsValidation.cs (FFB scaling expectations only)
   - Docs/FFBParkingAndReturn.md

Vehicle handling, input, controls UI implementation and H-shifter are unchanged.
No commit or push.

## Verified software results

Runtime/editor compilation passed using Unity's bundled compiler. Unity executed
the expanded FFB regression checks successfully (Logs/FFB/model-validation.txt).
Checks cover return sign/symmetry, preserved pneumatic moments, independent
mechanical falloff, zero stationary alignment, zero unloaded scrub, scrub motion
opposition, scrub load scaling, high-speed scrub removal, zero resistance at rest,
master/invert mapping, nonfinite rejection and the hard output cap. The complete
controls/UI test harness was not rerun; its scaling assertions were updated.

Synthetic straight travel with 3000 N per tire, +150-degree wheel, ratio 11.25,
60% master, steady state before release (wheel rate zero):

| km/h | Old total SAT Nm | New total SAT Nm | Old motor % | New motor % |
|---|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 0 |
| 2 | 0 | -0.973 | 0 | -6 |
| 5 | -2.734 | -7.244 | -5 | -43 |
| 10 | -12.209 | -20.720 | -24 | -60 |
| 20 | -16.054 | -24.999 | -27 | -60 |
| 30 | -16.054 | -24.999 | -27 | -60 |
| 60 | -16.054 | -24.999 | -27 | -60 |

These signs are before the preserved user Invert mapping. These are synthetic
commands, not physical return measurements. At 0 km/h with the same load and
1 rad/s rightward wheel motion, the added scrub is about -2.66 Nm and friction
about -0.22 Nm. With settled damping, total resistance corresponds to roughly
-18% at 60% master. Stop moving and these new resistance terms go to zero.

Clipping is explicit: this high-slip example reaches the ceiling by 10 km/h.
Extra simulated torque beyond it cannot add physical strength and can mask
pneumatic changes until demand drops below the ceiling. Inspect component
telemetry and actual release recordings before interpreting feel.

Restart Play Mode to use updated defaults. Keep working Invert, start validation
at 60% master. Live inspector and CSV expose scrub separately, friction,
mechanical, caster, pneumatic, damping, raw/filtered total torque and final motor
percentage. F8 records, F9 marks release, F10 saves to Logs/FFB. Inspect the
0.25/0.5/1-second checkpoint angles; they are physical input samples, not targets.
