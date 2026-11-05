# Mechanical return tuning

User hardware evidence now confirms +150 degrees returns toward zero. This
revision preserves that sign path and model. Physical strength/return timing
of this tuning is not yet confirmed.

Changes:
- Effective mechanical trail: 12 -> 20 mm. This scales only force-derived
  mechanical trail and the existing caster jacking approximation by 1.667.
  This is an effective FFB tuning value, not a claim of measured car geometry.
- Mechanical/caster rolling SmoothStep: 2 -> 12 km/h, independently exposed.
- Pneumatic rolling SmoothStep remains 3 -> 20 km/h, with unchanged slip curve.
- Caster angle remains 6 degrees; damping 0.08 Nm/(rad/s), friction 0.04 Nm.
- Motor normalization, smoothing, master slider, inversion and caps unchanged.

Mechanical fade is zero at rest, 21.6% at 5 km/h, 89.6% at 10 km/h and full
at 12 km/h. The old shared fade was 3.8% at 5 km/h, 36.9% at 10 km/h and
79.0% at 15 km/h. This improves the initial rolling buildup without throttle
or brake multipliers. Load still comes from WheelHit.force.

The total signal still clips at the existing normalization ceiling. At 60%
master the steady command cannot exceed 27% with the default 0.45 output gain;
at 50% it rounds to at most 22%, and at 70% to 32%. Stronger mechanical torque
reaches that ceiling earlier; it cannot increase already-clipped commands.
This is deliberately not a global gain increase. At large slip this can mask
some pneumatic variation while clipped; inspect the recorded components as
well as motor output. Near aligned centre all restoring terms still go to zero.

Validation: runtime and editor compilation passed with Unity's bundled compiler.
The extended Unity check compares old/new steady torque and signed output at
0/5/10/15/20/30/60 km/h, checks unchanged pneumatic torque, stronger mechanical
return, monotonic buildup and zero aligned-centre torque, plus existing symmetry,
load, slip saturation and contact-direction checks. It writes
Logs/FFB/return-tuning-comparison.txt after editor reload or Stagebound > Validate
FFB torque signs. These are synthetic checks, not hardware timing measurements.

Next physical test: restart Play Mode to instantiate the updated defaults, keep
the known working Invert setting, and try 60% master. Record separate +150/-150
degree releases at 10, 15 and 30 km/h (F8 record, F9 release, F10 save). Check
0.25/0.5/1-second angles and overshoot. Confirm stationary behaviour and 50+ km/h
stability before cornering/drift. No drift-specific torque was added; drift and
transition performance remain untested with this tuning.

Changed this revision: Assets/Scripts/SteeringForceFeedback.cs,
Assets/Editor/SteeringForceFeedbackValidation.cs, Docs/FFBReturnTuning.md.
No handling/settings/input changes; no commit or push.
