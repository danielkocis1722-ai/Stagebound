# Moderate steering-weight increment

Values-only continuation of the physically improved steering-weight model.

| Parameter | Previous | New | Increase |
|---|---:|---:|---:|
| Static/breakaway friction envelope | 1.20 Nm | 1.53 Nm | 27.5% |
| Moving/Coulomb friction | 0.16 Nm | 0.20 Nm | 25% |
| Parking scrub coefficient | 0.50 | 0.625 | 25% |
| Parking-only mechanical friction supplement | 0.18 Nm | 0.225 Nm | 25% |
| General velocity damping | 0.080 Nm/(rad/s) | 0.094 Nm/(rad/s) | 17.5% |

Scrub radius stays 0.010 m. Therefore scrub torque increases exactly 25% for
the same load, wheel motion and contact speed. Its speed fade is unchanged.
The static value is the total low-velocity steering-friction envelope, not an
additional torque on top of moving friction. The separate breakaway excess
changes from 1.04 to 1.33 Nm (27.9%); the smooth transition law is unchanged.
At exactly zero wheel velocity these friction terms still produce zero torque.

SAT and caster formulas, sign, strength and geometry are unchanged. Centre
settling retains its 5-degree window, 0.25 Nm/(rad/s) coefficient, 1.5 Nm limit
and tire-state bypass. No global FFB multiplier, output-cap change, new spring,
input adjustment or architecture change was made.

Calculated example at 50 km/h and 0.06 rad/s wheel motion: steering friction
increases from approximately 1.151 to 1.467 Nm (27.4%). At 60–75% master this
is roughly 9–11% steady motor demand from friction alone, before other forces.
These are model values, not measured G29 shaft torques or subjective results.

Runtime compilation passed with Unity's bundled compiler. The existing
regression suite is unchanged; this pass does not claim a new execution of it.
No serialized overrides of these fields were found in vehicle assets/scenes.
Restart Play Mode to instantiate the updated defaults.

Physical validation at 0/10/50 km/h, corner release, drift and fast transitions
is pending. Preserving SAT and centre-settling code does not prove that their
physical behaviour is identical after adding resistance. Confirm that return
and countersteer remain quick and that centre crossing does not hunt.

Recommended starting player strength: 60%, retaining the working Invert setting;
then try 65–75% to taste. No player preferences were changed. If countersteer
feels sluggish, reduce general damping toward the previous 0.080 first, keeping
the newly requested static weight. 100% usability remains a physical check.

Files changed: Assets/Scripts/SteeringForceFeedback.cs (five default values),
Docs/FFBWeightIncrement.md. No vehicle handling, tire grip, yaw tuning, camera,
Controls UI, H-shifter, input, centre-settling or self-centering code changes.
No commit or push.
