# Real-scale power delivery — 2026-11-02

Only `C:\Users\dandy\Documents\Projects\Stagebound-real-scale`, on
`experiment/real-world-scale`. Main is unchanged. Nothing staged, committed,
merged or pushed. The real-scale migration is retained.

## Diagnosis and changes

**Wheelspin dominates the reported 40 km/h / third-gear condition. The gearbox
is not physically short enough to explain limiter RPM at that road speed.**
Zero-slip third-gear engine RPM at 40 km/h is 2260 for AE86 and 2069 for R32/RX-7.
Native slide probes instead measured rear wheel speeds equivalent to 83–106 km/h
at 60% throttle, and 126–138 km/h at full throttle, while the bodies travelled
about 38–43 km/h. Per-wheel forward slip confirms loss of longitudinal traction.

The exact claim that *moderate throttle permanently hits the limiter at 40 km/h*
was not reproduced in every fixture: the established-slide 60% cases averaged
roughly 4400–5500 RPM, while full-throttle cases held about 7130–7150 RPM. Other
second-gear corner fixtures did show long limiter exposure at higher throttle.
The evidence supports excessive wheelspin, rather than globally short gearing.

Two changes were selected after separate native comparisons:

1. **Progressive engine torque curves**, with less low/mid-RPM torque, the same
   peak Nm rating, and stronger natural falloff above 6000 RPM. Gear ratios, final
   drives, efficiency, redline, limiter algorithm and throttle input mapping stay
   unchanged. No tire grip or stability assist compensates for power delivery.
2. **Correct the clutch's reflected-load calculation.** It previously added a
   slip-dependent chassis inertia while also inferring the tire/chassis reaction
   through the observed drivetrain-load term. At metre-scale wheel inertia this
   produced alternating maximum drive and overrun pulses. The clutch now reflects
   physical rotating wheel inertia; the existing measured-load term accounts for
   road/chassis reaction. No Rigidbody tensor, wheel mass or engine flywheel
   inertia value changes. A redundant state/smoothing path and grip lookup are
   removed. This is a powertrain numerical correction, not traction control.

The clutch correction alone removed the torque oscillation but barely changed
established-slide wheelspin; the curves were therefore tested separately on that
corrected foundation. This prevents attributing the whole improvement to one fix.

For an AE86 third-gear straight probe's final 2.02 seconds at 60% throttle, the
original clutch output alternated from **-275.6 to +360 Nm**, with **100 sign
changes**. With the physical-wheel inertia calculation and the **original curve**,
it delivered **46.6–52.4 Nm**, with **zero sign changes**. This is an illustrative
acceleration fixture, not a matched constant-speed test: mean speeds were 122.0
and 127.2 km/h. Smoother overrun is part of this correction; service-brake tuning
and its implementation remain unchanged.

`EngineOutputTorque` is a read-only diagnostic of the actual post-throttle,
post-limiter combustion torque before clutch/gearing. It introduces no new loop,
force or assist. CSVs also record actual final motor torque per rear wheel.

## Current geometry and unchanged gearbox

All dimensions remain in metres; velocity is m/s and displayed km/h is ×3.6.
The following transmission values are **old = new for this pass**.

| Vehicle | Rear wheel radius m | Ratios 1–5 | Final drive | Reverse ratio |
| --- | ---: | --- | ---: | ---: |
| AE86 | 0.296700 | 4.20, 2.30, 1.78, 1.46, 1.20 | 3.55 | 5.673239 |
| R32 | 0.331617 | 3.877692, 2.226082, 1.658048, 1.299551, 1.068520 | 3.90 | 5.724212 |
| RX-7 | 0.321778 | 3.698392, 2.054662, 1.530369, 1.232797, 1.008652 | 4.10 | 5.283417 |

All retain 7500 RPM redline, 900 RPM idle and 0.9 drivetrain efficiency.

For zero slip: `km/h = engineRPM × 2πr × 0.06 / (gearRatio × finalDrive)`.

| Gear | AE86 redline km/h | R32 redline km/h | RX-7 redline km/h |
| --- | ---: | ---: | ---: |
| 1 | 56.264 | 62 | 60 |
| 2 | 102.743 | 108 | 108 |
| 3 | 132.758 | 145 | 145 |
| 4 | 161.856 | 185 | 180 |
| 5 | 196.925 | 225 | 220 |

These kinematic speeds do not guarantee enough engine power to reach them under
load. The revised high-RPM tail can reduce attainable top speed. Fifth-gear top
speed and subjective fourth/fifth-gear pull need manual evaluation; ratios were
not lengthened to disguise wheelspin.

## Torque curves and retained power character

Multipliers below multiply each car's unchanged peak torque rating. The original
seven keys used zero-slope Hermite interpolation; the new nine keys do likewise.
Original 6500/7000 values below are interpolated, not original authored keys.

| RPM | Original, all cars | AE86 new | R32 new | RX-7 new |
| --- | ---: | ---: | ---: | ---: |
| 900 | 0.60 | 0.30 | 0.25 | 0.25 |
| 2000 | 0.84 | 0.55 | 0.50 | 0.48 |
| 3000 | 0.95 | 0.78 | 0.75 | 0.72 |
| 4000 | 1.00 | 0.95 | 0.95 | 0.94 |
| 5000 | 1.00 | 1.00 | 1.00 | 1.00 |
| 6000 | 0.90 | 0.90 | 0.90 | 0.90 |
| 6500 | 0.8352 | 0.65 | 0.65 | 0.62 |
| 7000 | 0.7148 | 0.32 | 0.32 | 0.30 |
| 7500 | 0.65 | 0.12 | 0.12 | 0.10 |

| Vehicle | Peak torque, old → new Nm | Calculated peak power, old → new kW | New power-peak RPM |
| --- | --- | --- | ---: |
| AE86 | 360 → 360 | 207.695 → 204.013 | 6026 |
| R32 | 460 → 460 | 265.388 → 260.683 | 6026 |
| RX-7 | 500 → 500 | 288.466 → 283.284 | 6023 |

Calculated peak power decreases about **1.8%**, while the torque build and
near-redline tail change substantially. This is not a claim that every RPM retains
its old power. High RPM above roughly 6500 is deliberately softer. RX-7 retains
the greatest power, R32 retains its stronger/heavier identity, and AE86 remains
the lightest. Their chassis/steering personalities are not retuned.

### Wheel torque and legacy-scale comparison

At a 1.0 curve multiplier/full throttle, approximate total driven-axle torque is
`peakEngineNm × gear × finalDrive × 0.9`. **These peak values are unchanged in this
pass**; the curve controls when and how readily they are delivered.

| Gear | AE86 peak axle Nm | R32 peak axle Nm | RX-7 peak axle Nm |
| --- | ---: | ---: | ---: |
| 1 | 4830.84 | 6260.92 | 6823.53 |
| 2 | 2645.46 | 3594.23 | 3790.85 |
| 3 | 2047.36 | 2677.08 | 2823.53 |
| 4 | 1679.29 | 2098.25 | 2274.51 |
| 5 | 1380.24 | 1725.23 | 1860.96 |

The LSD splits axle torque; it need not divide it equally. Clutch transients can
temporarily exchange flywheel energy, so instantaneous motor torque is measured
separately rather than assumed equal to the steady engine-torque formula.

| Vehicle, third gear | Historical oversized axle Nm | Current metre-scale axle Nm | Historical/current radius m | Torque/radius increase |
| --- | ---: | ---: | --- | ---: |
| AE86 | 3606.12 | 2047.36 | 0.6500 / 0.2967 | 24.38% |
| R32 | 3564.54 | 2677.08 | 0.7269 / 0.3316 | 64.63% |
| RX-7 | 4063.50 | 2823.53 | 0.7333 / 0.3218 | 58.34% |

Despite lower axle torque after the earlier gearbox migration, smaller tire lever
arms increase the corresponding torque/radius force. The historical column refers
to the old oversized geometry's Unity-unit lever arms, not a second runtime metre
convention. The real-scale wheelbases and body tensors also differ from that
historical setup, but remain unchanged in this tuning pass.

## The 40 km/h / third-gear condition

This matched input sequence seeds an independent rolling slide near 40 km/h,
uses full throttle for two seconds to establish wheelspin, then holds the stated
pedal. The table selects only **drifting samples at 35–50 km/h after 2.5 seconds**.
It shows actual measured speeds, not an assumption that the fixture holds 40.
Different sample durations are recorded in the evidence.

| Car / throttle | Body km/h old → new | Engine RPM old → new | RL/RR RPM old → new | Equivalent mean wheel km/h old → new | RL/RR forward slip old → new |
| --- | --- | --- | --- | --- | --- |
| AE86 60% | 41.09 → 40.82 | 4683 → 4092 | 984/495 → 818/476 | 82.71 → 72.36 | .653/.260 → .560/.231 |
| AE86 100% | 38.37 → 38.44 | 7130 → 6562 | 1699/556 → 1539/536 | 126.12 → 116.06 | .836/.404 → .819/.377 |
| R32 60% | 39.29 → no matching drift | 4425 → n/a | 913/448 → n/a | 85.06 → n/a | .661/.286 → n/a |
| R32 100% | 38.10 → 38.93 | 7128 → 5198 | 1662/535 → 1085/515 | 137.35 → 100.01 | .843/.422 → .718/.396 |
| RX-7 60% | 42.80 → 41.34 | 5487 → 3762 | 1222/523 → 765/430 | 105.79 → 72.45 | .749/.350 → .670/.294 |
| RX-7 100% | 40.37 → 40.77 | 7152 → 6567 | 1697/578 → 1531/557 | 137.97 → 126.66 | .841/.453 → .818/.422 |

For R32 at 60%, the established slide decays below 35 km/h rather than maintaining
the old high-spin state. Its first post-settle sample is **34.98 km/h, 2007 engine
RPM, rear RPM 295/322, wheel-equivalent speeds 36.88/40.30 km/h, forward slips
.128/.067, sideways slips .205/.178, 138.0 Nm engine torque and 405.3/431.2 Nm
rear motor torque**. DriftTelemetry is false. This is a recovery/slowdown result,
not evidence of a sustained 40 km/h third-gear drift. Downshift for that speed.

For the qualified 60% samples, AE86 mean engine torque/motor torques change from
211.6 Nm / 480.1+761.8 Nm to 195.4 / 464.3+721.7. RX-7 changes from
287.0 / 647.0+1010.6 to 268.7 / 629.7+994.4. Wheelspin remains deliberately
available; it is reduced rather than forbidden. Full-throttle examples still
have much higher wheel speed than body speed. There is no slip clamp.

## Throttle modulation and useful drift gears

A second fixture starts at the selected road speed with a rolling drivetrain,
without the two-second full-throttle establishment phase. An **editor-only test
driver** uses ordinary steering input to attempt a long corner; it never writes
ongoing body pose/velocity, engine RPM, torque or grip. This tests sustained
physical drift rather than relying on a straight acceleration sweep. The first
second is excluded; the evaluation window is approximately nine seconds.

Below, each tuple is **mean actual km/h / mean engine RPM / drifting seconds**.
These are observed outcomes from seeded entry speeds, not speed-controlled laps.

| Car / gear / entry | 40% throttle | 60% throttle | 80% throttle | 100% throttle |
| --- | --- | --- | --- | --- |
| AE86, 2nd, 80 | 62.6 / 5520 / 8.38 | 70.2 / 6378 / 8.62 | 73.4 / 6708 / 8.80 | 74.5 / 6816 / 8.80 |
| R32, 2nd, 80 | 62.9 / 5337 / 8.30 | 71.5 / 6333 / 8.74 | 74.8 / 6687 / 8.78 | 75.9 / 6800 / 8.82 |
| RX-7, 2nd, 80 | 71.5 / 5965 / 8.54 | 77.2 / 6511 / 8.80 | 79.8 / 6743 / 8.84 | 80.8 / 6844 / 8.84 |
| AE86, 3rd, 75 | 43.8 / 2748 / 2.44 | 69.5 / 5260 / 8.76 | 76.2 / 6238 / 8.84 | 78.3 / 6588 / 8.96 |
| R32, 3rd, 75 | 36.6 / 2056 / 1.70 | 65.5 / 4359 / 8.40 | 76.0 / 6128 / 8.84 | 78.3 / 6489 / 9.02 |
| RX-7, 3rd, 75 | 46.1 / 2611 / 1.96 | 78.7 / 5426 / 8.80 | 85.0 / 6280 / 8.96 | 86.8 / 6594 / 9.02 |

All listed final windows have **zero seconds above 7100 RPM**. This does not
guarantee limiter-free driving for every input, clutch kick or surface. First-gear
full-throttle launch still reaches high RPM naturally. In the original-curve AE86
second-gear 80-entry / 80%-pedal corner, limiter exposure was 8.90 seconds; with
the new curve it is zero, while measured drift persists for 8.80 seconds.

At lower entries, 60% throttle in second from 50 km/h sustains about 8.4–8.8 s
of drift with mean speeds **52.9 / 50.2 / 60.9 km/h** (AE86/R32/RX-7). Third from
110 km/h at 60% sustains **8.68 / 8.62 / 8.72 s**, with actual mean speeds
**90.5 / 92.5 / 100.1 km/h**. The representative usable windows are therefore
roughly **45–80 km/h in second** and **70–110 km/h in third**, with R32 preferring
more pedal near the lower third-gear boundary. These are gameplay starting ranges,
not enforced limits. Forty-percent pedal can stabilize a higher-speed second-gear
drift or let a lower-RPM third-gear drift unwind; 80–100% creates more wheelspin.

Rear forward-slip means in the third/75-entry cases rise with pedal: AE86
.109/.266/.317/.332; R32 .080/.240/.362/.377; RX-7 .094/.266/.314/.330 for
40/60/80/100%. The extra pedal has a measurable effect without altering tire grip.
Subjective throttle precision still needs the user's manual approval.

## RPM at actual road speeds

Third-gear **60%-pedal straight acceleration** samples within ±2 km/h of each
listed speed, after the first 0.25 s. Each cell is old → new mean engine RPM.
These are transient, nonzero-slip readings; the theoretical zero-slip value is
included to distinguish gearing from wheelspin. Full traces retain per-wheel data.

| Car | 40 km/h | 60 km/h | 80 km/h | 100 km/h |
| --- | --- | --- | --- | --- |
| AE86 measured | 2658 → 2517 | 3943 → 3846 | 5297 → 5155 | 6549 → 6270 |
| AE86 zero-slip | 2260 | 3390 | 4519 | 5649 |
| R32 measured | 2476 → 2277 | 3639 → 3497 | 4824 → 4724 | 5982 → 5845 |
| R32 zero-slip | 2069 | 3103 | 4138 | 5172 |
| RX-7 measured | 2521 → 2295 | 3713 → 3553 | 4856 → 4789 | 6028 → 5901 |
| RX-7 zero-slip | 2069 | 3103 | 4138 | 5172 |

## Preservation and validation

Only these runtime/config files change in this pass:

- `Assets/Scripts/CarController.cs`: reflected powertrain inertia correction and
  read-only engine output torque diagnostic.
- The three existing `Assets/Vehicles/Configs/*_VehicleConfig.asset`: only the
  torque-curve blocks change. Their identities/GUIDs remain authoritative.

The new `Assets/Editor/ValidatePowerDelivery.cs` provides explicit native probes,
comparison fixtures and authoring/controls validation. It is excluded from player
builds. `Docs/RealScaleMigration.md` receives a pointer to this later tuning report.

Byte/section comparisons against the **start of this pass**, rather than git HEAD
which still contains the earlier uncommitted migration, verify unchanged steering,
brake torque/bias, suspension, Rigidbody mass/COM/tensor geometry, camera, forward
and sideways tire curves, DriftTelemetry, yaw governor, effects/audio and stages.
`ReadInput`, `UpdateClutch`, `UpdateEngineRPM`, `HandleSteering`, `HandleBrakes`
and visual wheel-follow methods are unchanged. The only inertia calculation changed
is the engine-side reflected-load estimate described above; chassis/engine/wheel
inertia parameters are not retuned. No speed conversion or fake recovery is added.

Native Unity **6000.6.7f1** completed:

- 96 original-powertrain straight/established-slide probes.
- 96 probes isolating the physical-wheel clutch calculation with original curves.
- 168 original/new-curve comparison cases across all cars, including second/third,
  40/60/80/100% throttle, rolling corner entries and established 40 km/h slides.
- **18 passing final controls assertions** against the actual saved configs:
  selected curve, full-throttle launch, controller upshift, manual clutch
  disconnect/reconnection/kick and effective service braking for each car.

Full-throttle six-second first-gear launch exit speeds are 54.8 / 59.7 / 58.6 km/h.
Manual release retains positive physical clutch kicks; peak extra engine-side kick
torques are 374.5 / 516.1 / 431.0 Nm in the test. Neutral 100 km/h service stops
remain **48.723 / 49.391 / 49.152 m**, effectively the preceding 48.721 / 49.388 /
49.150 m results. The stopping threshold is 0.1 m/s. These are native tests, not
human driving approvals or a guarantee for every trail-braking condition.

The comparison setup and candidate curves were measured before saving the new
configs. Final controls then verified the saved assets. No C# errors or warnings
occurred in the final compilation/run. `git diff --check` passes. Main's 595
protected tracked files and working-tree status remain unchanged. The earlier
migration's uncommitted work and pre-existing settings are preserved. No records
or user save keys are reset.

The test driver's exact steering strategy, arranged entry and flat surface limit
generalization. Some low-throttle cases straighten or slow down; those are
reported rather than counted as successful sustained drifts. Manual testing must
approve low-speed third-gear response, second/third throttle modulation, near-redline
falloff, lift-off/engine overrun feel and fourth/fifth-gear pull. No subjective
handling approval is claimed, and there is no merge or push.

## Evidence and reproduction

- [Comparison cases and summaries](PowerDeliveryValidation/comparison.csv)
- [40 km/h-region third-gear measurements](PowerDeliveryValidation/forty-third.csv)
- [RPM samples at matched road speeds](PowerDeliveryValidation/rpm-at-speed.csv)
- [Clutch pulse comparison](PowerDeliveryValidation/clutch-pulses.csv)
- [Original native probes](PowerDeliveryValidation/baseline-slides.txt)
- [Clutch-only probes](PowerDeliveryValidation/coupling.txt)
- [Curve comparison log](PowerDeliveryValidation/delivery-comparison.txt)
- [Calculated peak engine power](PowerDeliveryValidation/authored.txt)
- [Final saved-config controls/braking checks](PowerDeliveryValidation/final-controls.txt)
- [Preservation checks](PowerDeliveryValidation/protection.txt)

The evidence folder also contains original/final 60% and 100% 40-entry third-gear
CSVs for each car, including forward/sideways slip, both wheel RPMs, raw/smoothed
pedal, actual combustion torque, clutch torque and final per-wheel motor torque.
All complete traces and the pre-pass source/config snapshots remain under ignored
`Library/PowerDelivery`. `ValidatePowerDelivery.DeliveryComparison` reproduces the
original/new curve comparison on the corrected clutch; `ApplyAndValidate` authors
the documented candidate curves and runs the saved-config controls checks. Use
the experimental worktree's MainMenu for the manual power-delivery test.
