# Real-scale AE86 gearing pass

> Historical report. The experimental branch now uses the full metre-scale migration documented in [RealScaleMigration.md](RealScaleMigration.md). Values and instructions below describe the earlier state.

Only `experiment/real-world-scale`, in `Stagebound-real-scale`. Changes are left
uncommitted for manual testing. Nothing is merged or pushed. The pre-existing
ProjectSettings.asset working-tree modification is preserved.

## Current physical inputs

Measured driven-wheel mean radius: **0.296700015 m** (individual rear radii
0.29670003 / 0.29670000). Engine idle/redline: **900 / 7,500 RPM**. Peak engine
torque remains 360 Nm. The unchanged curve multipliers are 0.60 at 900 RPM,
0.84 at 2,000, 0.95 at 3,000, 1.00 at 4,000–5,000, 0.90 at 6,000 and 0.65 at
7,500, with the existing interpolation. The soft limiter begins at 7,100 RPM.

The rear differential remains LimitedSlip, strength 0.6, response 5/s. Efficiency
remains 0.9 and shift duration 0.2 s. Neither influences the kinematic gear-speed
formula; efficiency affects delivered torque. Mass remains 1,100 kg and is not
used to derive the ratios.

## Calculation and selection

For a coupled drivetrain and rolling tire, without wheelspin:

```
wheel RPM = engine RPM / (gear ratio * final drive)
speed km/h = wheel RPM * (2 * pi * wheel radius) * 60 / 1000
RPM after shift = RPM before shift * next gear ratio / current gear ratio
```

The oversized AE86 used 0.6500 m wheels. The real-scale radius is 45.6462% of
that, a 54.3538% reduction. At 100 km/h the rolling wheel RPM rises from about
408.09 to 894.03: **2.1908 times higher**. Retaining every original ratio therefore
cut each theoretical redline road speed to 45.6462% of its old oversized value.

Restoring the oversized car's exact speeds by final drive alone would require
`5.3 * 0.296700015 / 0.65 = 2.419246`. That would make first gear roughly 82.6
km/h and fifth about 289 km/h, unsuitable for the requested starting targets.
Instead final drive is **3.55**, retaining first and fifth ratios and lengthening
those gears by 49.30%. The three middle ratios are then adjusted for drift use.

| Gear | Old ratio | Old redline km/h at FD 5.30 | New ratio | New redline km/h at FD 3.55 | Ideal RPM after upshift at 7,500 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 4.20 | 37.69 | 4.20 | 56.26 | 4,107 |
| 2 | 3.00 | 52.76 | 2.30 | 102.74 | 5,804 |
| 3 | 2.10 | 75.37 | 1.78 | 132.76 | 6,152 |
| 4 | 1.55 | 102.12 | 1.46 | 161.86 | 6,164 |
| 5 | 1.20 | 131.90 | 1.20 | 196.92 | — |

All ideal shift landings are in a useful part of the existing torque curve. The
larger first-to-second drop lands near the torque plateau; later gears have closer
spacing. This is physically calculated for the Stagebound engine, not a factory
T50 specification claim.

The first candidate used 2.50 / 1.85 for second/third (94.52 / 127.73 km/h).
Native tests showed that driven-wheel slip consumed its upper-speed headroom:
second spent too much time near the limiter at roughly 82–85 km/h. The final
2.30 / 1.78 ratios deliberately exceed the nominal starting targets slightly so
wheelspin does not immediately consume all available RPM at 90 / 120 km/h.
No tire grip, engine torque, clutch algorithm or limiter behavior was changed.

The old reverse ratio was 3.80. Changing final drive alone would lengthen reverse,
so reverse is compensated to **5.6732394**. Its total reduction remains 20.14,
equivalent to `3.8 * 5.3`; theoretical reverse redline speed stays about 41.65 km/h.

## Native validation and practical limits

The editor benchmark loads the real-scale scene/config and uses ordinary Input
System throttle, steering and shoulder-button shifts. Initial fixture velocity,
engine RPM and pose are arranged only before measurements; no ongoing body/RPM
correction, yaw assist or traction-control behavior is added to runtime code.
Extra flat runoff exists only in Play Mode. The saved scenes remain unchanged.

The 1→2→3→4→5 acceleration sweep and shift measurements are recorded in
`Library/Gearing/sweep.txt` and `acceleration.csv`. Theoretical shift landings
above assume no speed change or clutch slip; measured half-second post-shift RPM
can differ while the existing clutch synchronizes and the car accelerates.

| Gear | Test throttle | Time in gear | Speed at end | First sampled soft-limit RPM |
| --- | ---: | ---: | ---: | ---: |
| 1 | 22% | 11.92 s | 52.17 km/h | 11.60 s |
| 2 | 25% | 7.87 s | 85.47 | 7.32 s |
| 3 | 30% | 10.17 s | 115.88 | 8.93 s |
| 4 | 35% | 14.42 s | 145.65 | 11.70 s |
| 5 | 40% | 35.00 s | 180.42 | 23.78 s |

All five gears accelerate, all four normal controller upshifts pass, and none
immediately reaches the limiter in this measured moderate-throttle sweep.
Measured RPM 0.5 s after shifts was 4,864 / 5,365 / 5,693 / 5,791 respectively;
these shifts occurred around 7,200–7,287 RPM rather than exactly 7,500 RPM.
Times above use Unity simulation time, not assumed rendering-frame duration.
This is not an all-gears full-throttle traction/limiter guarantee. The fifth-gear
run stops after 35 seconds at 40% throttle, so 180.42 km/h is not a measured maximum.

Second gear is intended for roughly **50–90 km/h** drift work; third for roughly
**80–120 km/h**. Pure rolling soft-limit onset is now 97.26 / 125.68 km/h. Wheelspin
raises engine RPM above that implied by ground speed, so these are not guarantees
of limiter-free full-throttle drift at every angle/speed.

The native tests include 50/70/90 km/h second-gear and 80/100/120 km/h third-gear
fixtures, each at 35% and 65% throttle. Straight rolling conditioning reduces
actual entry speed slightly; actual speeds are logged rather than mislabelled
as exact target speeds. A second test starts each fixture at a 20-degree body
slip angle and then uses controller inputs only. DriftTelemetry confirms actual
drift in every entered-drift case. The fixed inputs subsequently straighten the
car: measured drifting lasts about 0.2–0.55 s, **not a validated long sustained
drift section or a human controller driving test**.

Representative four-second entered-drift results with the final ratios:

| Gear / target / throttle | Actual road-speed span | Engine RPM span | Seconds above 7,100 / 7,400 RPM |
| --- | --- | --- | --- |
| 2 / 50 / 35% | 47.36–70.90 km/h | 3,604–5,996 | 0 / 0 |
| 2 / 70 / 35% | 65.11–83.39 | 5,269–7,099 | 0 / 0 |
| 2 / 90 / 35% | 82.72–89.16 | 6,626–7,435 | 2.67 / 0.38 |
| 2 / 90 / 65% | 83.17–90.38 | 6,641–7,500 | 3.60 / 1.23 |
| 3 / 80 / 35% | 74.25–90.03 | 4,575–5,812 | 0 / 0 |
| 3 / 100 / 35% | 92.26–103.98 | 5,638–6,624 | 0 / 0 |
| 3 / 120 / 35% | 109.75–115.95 | 6,647–7,231 | 0.70 / 0 |
| 3 / 120 / 65% | 110.65–119.74 | 6,701–7,424 | 3.42 / 0.30 |

The old gearing hit 7,500 RPM repeatedly in nearly every corresponding speed
probe, often slowing the car below the requested speed. The new second and
third provide substantially more useful range at moderate throttle. **Limiter
activity remains at upper speeds/high throttle**, including wheelspin and clutch
transients. No claim is made that the existing limiter/clutch behavior is fully
resolved by ratios alone, or that 196.92 km/h is a measured top speed under load.

## Preserved setup and files

Service brakes remain 650 Nm per-wheel base, 67% front share: 871 Nm front /
429 Nm rear at full pedal. Rear handbrake remains 2,400 Nm. Vehicle scale, mass,
COM, inertia, steering, suspension, tire curves, differential, yaw governor,
camera, effects, engine characteristics and runtime physics code are unchanged.

Changed intentionally in this pass:

- `Assets/Experiments/RealScale/AE86_RealScale_VehicleConfig.asset`: forward ratios,
  final drive and compensation preserving the existing reverse reduction.
- `Assets/Editor/RealScaleExperiment.cs`: authoring reproduces those ratios.
- `Assets/Editor/ValidateRealScale.cs`: explicitly invoked gearing measurements;
  no test-driver behavior is shipped in the game.
- `Docs/RealScaleGearing.md`: calculations, results and limitations.
- `Docs/RealWorldScaleExperiment.md`: link marking its earlier gearing as historical.

The config comparison against `Library/Gearing/before.asset` must differ only
inside the transmission section. Other protected vehicle/runtime/scene/model files
are checked against the pre-pass manifest. Native logs and initial candidate
results are retained under ignored `Library/Gearing`.

Final checks: 12 acceleration/shift assertions and 26 entered-drift assertions
passed. The final native Unity runs compiled without C# errors or warnings.
`git diff --check` passes. Only the experimental config's transmission block
differs from its pre-pass copy; all other protected runtime, vehicle, scene and
model files are byte-identical. The pre-existing ProjectSettings.asset bytes are
also unchanged. No files are staged, no commit is created, and HEAD stays at
bc078a5a26c2b1376d98954a19bf315530887513.
