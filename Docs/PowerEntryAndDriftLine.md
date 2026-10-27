# Powered entry and front drift line experiment

This pass preserves the prior rear tire curves, engine rating (360 Nm), differential,
manual clutch/kick calculation, steering curve, suspension and handbrake settings.
No chassis torque/force, countersteer assist, transform motion or velocity rotation
was added. The changes are an entry-only rear combined-slip allowance, modest
front sideways friction tuning, and optional diagnostics. No commit or push.

## What was measured

Unity-native WheelCollider tests used a copy of the current scene car, a separate
preview physics scene and a flat diagnostic surface. The real scene/track, race
state and PlayerPrefs were not changed by these tests. Starting speed was 60 km/h,
gear 2, with ramped throttle and steering. The following baseline values are signed
WheelHit.forwardSlip, not estimates from motor torque:

| Input | BL median / max | BR median / max |
| --- | --- | --- |
| 40% throttle | 0.0796 / 0.0821 | 0.0952 / 0.0953 |
| 60% throttle | 0.1108 / 0.1162 | 0.1322 / 0.1346 |
| 80% throttle | 0.1408 / 0.1492 | 0.1684 / 0.1740 |
| 100% throttle | 0.1749 / 0.1842 | 0.2165 / 0.2220 |
| Clutch-kick release window | 0.1803 / 0.2084 | 0.2558 / 0.2931 |

Rear slip was present, but the old PowerGrip helper used -forwardSlip. These
positive samples consequently requested no lateral reduction. Its old 8% cap and
load gate would also limit its effect for qualifying negative slip. This identifies
a concrete missed combined-slip response in the isolated test; it does not prove
the cause of every corner on the real road. The existing signed helper is retained
for established drift feel. The new entry calculation uses magnitude with explicit
brake/handbrake/clutch/gear gates, rather than changing the old helper globally.

At an initially imposed 25-degree body/velocity angle, front |sidewaysSlip| briefly
reached approximately 0.37, above its 0.30 peak. Most later samples fell below that
peak; continuous deep front saturation was not demonstrated. Both front wheels
reported grounded in those initial samples, but load transferred strongly: summed
front load was roughly 2.36–2.43 kN and one inside wheel briefly had very little
load. This is a short induced-slide transient, not a measured steady road drift.
It does not justify a suspension/COM/force-point redesign, so those are unchanged.

## Exact breakaway model

On each rear wheel:

```
S = SmoothStep01(InverseLerp(0.10, 0.22, abs(forwardSlip)))
P = SmoothStep01(InverseLerp(0.40, 0.85, throttle))
L = SmoothStep01(InverseLerp(0, 0.10, contactForce / (mass * |gravity| / 4)))
target = sqrt(1 - (1 - 0.85^2) * S * P * L)
filtered = Lerp(previous, target, 1 - exp(-8 * fixedDeltaTime))
```

Start 0.10 and full 0.22 are based on the measured slip range, rather than requiring
0.65 slip that these entry tests did not normally produce. At/below 40% throttle,
below start slip, or without contact load, the target is 1. The minimum is 0.85:
at most 15% reduction, without stacking another 15% on top of the old reduction.

The entry weight is speed SmoothStep over 25–30 km/h, multiplied by steering
magnitude SmoothStep over 0.04–0.16 (processed steering input), and by one minus
the body-slip-angle SmoothStep over 8–18 degrees. Meaningful steering is necessary
but never sufficient without powered tire slip. Forward gear, no shift, connected
clutch, no normal brake and no handbrake are also required for entry weight.

```
rear stiffness = originalRearStiffness * handbrakeMultiplier *
                 Lerp(originalPowerGrip, filteredCombinedSlip, entryWeight)
```

The original rear-grip state continues updating separately at its original rate.
At 18 degrees or more body slip the entry weight is exactly zero, restoring that
original calculation. With handbrake active the entry contribution is also zero;
the handbrake torque, target and rates are unchanged. As longitudinal slip falls,
the filtered target returns smoothly to 1. An Inspector enable toggle permits
comparison without changing tire curves. The same path handles ordinary throttle
and clutch-kick-generated tire slip; there is no separate kick rotation system.

## FL and FR sideways friction

| Property | Before | After |
| --- | --- | --- |
| Extremum slip | 0.30 | 0.30 |
| Extremum value | 1.00 | 1.00 |
| Asymptote slip | 0.80 | 0.80 |
| Asymptote value | 0.90 | 0.95 |
| Stiffness | 1.20 | 1.26 |

Stiffness rises 5%; the sliding value retains more force beyond the peak. Front
forward curves, suspension and force application distance (0.15) are unchanged.
COM remains Y -0.35. Both rear base sideways stiffness values remain 1.20 and all
rear serialized tire curves are unchanged from the preceding working version.

## Validation and limits

Runtime/editor compilation passed with warnings treated as errors. Native checks
passed for magnitude symmetry, bounded/monotonic combined slip, low-throttle/load,
steering/speed/brake gates, exact established-rear handoff and unchanged handbrake
grip composition. No torque/clutch tuning or other gameplay scripts changed.

The updated fixed-input test with 0.55 raw steering at full throttle progressed
through about 5, 8, 12 and 17 degrees of body slip at 0.8, 1.0, 1.2 and 1.4 s,
then about 24 degrees at 1.6 s. That test deliberately keeps steering into the
corner without countersteering; it eventually spins. It is evidence of a gradual
entry, not proof of a catchable or stable drift. The milder 0.40-stick test stayed
around 6 degrees. Updated slip peaks in that milder test were BL 0.184 / BR 0.236
under throttle and BL 0.208 / BR 0.300 after a kick. No extra clutch torque was needed.

Induced-slide checks observed roughly 20–24 degrees of body slip briefly and a
curving travel direction, but do not establish a sustained 25–30 degree road drift
or a verified drift radius. Test that manually on the actual track, including
throttle lift, countersteer changes, left/right transitions and 100–140 km/h grip.
The diagnostics and copied-car tests are useful evidence, not substitutes for feel.

## Debug and files

DrivetrainDebug now includes FL/FR slip, load, grounded flags and ground-normal
vertical alignment; front stiffness relative to 1.20; each rear combined-slip
multiplier and entry weight; steering, yaw rate and vehicle body slip angle. It
also computes travel-direction turn rate and approximate radius from changes in
horizontal velocity. Those are read-only measurements; velocity is never modified.
Existing wheel torque, rear slip, clutch and kick fields remain. Enable Capture
Telemetry on CarRoot; optional console logging contains the same fields.

Production files changed in this pass:

- Assets/Scripts/CarController.cs
- Assets/Scripts/RearTraction.cs
- Assets/Scripts/DrivetrainDebug.cs
- Assets/Scenes/SampleScene.unity (controller settings and FL/FR sideways curves)
- Docs/PowerEntryAndDriftLine.md

Temporary diagnostics, native reports and CSV traces are retained only under the
ignored Library/PowerEntryValidation directory. Prior experimental changes remain
uncommitted, and HUD/race/respawn/pause/audio/effect/track scripts were preserved.
