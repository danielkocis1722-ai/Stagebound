# Progressive anti-spin support

This experimental pass adds `DriftYawStability` to SampleScene / CarRoot. Every pre-existing runtime script and every pre-existing scene setting is preserved. No rear-grip multiplier, suspension/anti-roll change, throttle filter, steering change or transition-specific assist was added.

## Diagnosis

Tests use the actual current car cloned into isolated Unity native physics, on flat ground, with gear 2 at 60/70/80 km/h. Seeded 25–40 degree drifts and yaw rates allow paired comparisons with identical test-driver inputs. These are controlled reproductions, not recordings of a human's exact spin.

In the 70 km/h / 30-degree / 60-degree-per-second seeded drift, yaw grew to 70.5 degrees/sec at 30.6 degrees slip, 99.5 degrees/sec at 43.6 degrees slip, and 113.8 degrees/sec at 61.6 degrees slip. At 0.48 seconds the rear sideways slips were approximately -0.99/-1.26, while front sideways slips were -0.17/-0.62. This is an oversteer force imbalance with growing yaw: lateral sliding exceeds what the rear tires can arrest before front countersteer takes effect.

The existing rear grip multipliers remained 1.0 and the front recovery multiplier remained 1.0 throughout that initial event. Both rear wheels remained loaded; around 41 degrees slip the loads were approximately 4962/1531 N, with normalized compression 0.633/0.366. There was significant load transfer but no rear wheel going effectively unloaded in that interval. Removing the front recovery boost on the test clone did not prevent the spin; removing the scripted power-grip reduction produced the same trajectory. Returning only the clone to pre-weight-transfer suspension also spun. Therefore the evidence does not justify flattening the suspension or changing general grip. The main correction is to slow excessive yaw growth, rather than modify existing grip/recovery transitions.

The full-throttle step occurs at 0.60 seconds in these fixtures, after the original 70 km/h case has already exceeded 60 degrees at 0.48 seconds. It does not explain that initial snap. Engine torque response remains unchanged.

## Exact default formula

Let `S(lo, hi, x)` mean smoothstep of `clamp01((x-lo)/(hi-lo))`. Let:

- `a` = absolute DriftTelemetry.VehicleSlipAngle, degrees.
- `w` = world yaw angular velocity, degrees/sec.
- `u` = positive rate of increase of absolute slip, using signed DeltaAngle between samples, degrees/sec.
- `g` = exponentially filtered positive growth of yaw magnitude, degrees/sec², response 10/sec.
- `c` = 1.2 when player steering exceeds 0.08, opposes yaw and points toward recovery; otherwise 1.

The default damping coefficient, per second, is:

```
lambda = 4.5 * S(28,40,speedKmh)
             * S(25,42,a)
             * S(30,80,abs(w))
             * S(1,15,u)
             * (0.25 + 0.75*S(10,120,g))
             * c

newYawRadians = oldYawRadians * exp(-lambda * fixedDeltaTime)
```

Yaw acceleration compares the current pre-assist rate to the previous **post-assist** rate, so it captures new physics growth rather than counting this component's own damping as tire recovery. Opposite yaw signs clear that acceleration sample. Slip growth measures the actual travel/heading divergence; it is not inferred from yaw alone.

Intervention also requires forward speed above 15 km/h, rear sliding, both rear wheels and at least one front wheel grounded, continuous samples and outward yaw. DriftTelemetry's signed slip is travel relative to heading, so outward yaw means `slip * yaw < 0`. Stable large-angle drifts have no opening-angle weight. Recovering slides receive no damping. At 25 degrees or below, the angle weight is exactly zero. The angle weights at 30/35/40/42 degrees are approximately 0.209/0.631/0.962/1; these are multiplied by all other gates, not direct damping strengths.

Only the world-y component of Rigidbody.angularVelocity is reduced, without changing its sign. There is no target heading, velocity alignment, steering input or new torque source. Physical pitch/roll angular velocities and linear velocity are untouched. The assist has no force or tire writes. Teleports, disable/re-enable and pause discard stale history. All tuning fields and live slip/yaw/growth/damping diagnostics are exposed on CarRoot / DriftYawStability.

## Paired native physics results

For prompt recovery from 30 degrees / 35 degrees-per-second initial yaw, using the same countersteering diagnostic driver:

| Speed | Before peak slip / peak yaw | After peak slip / peak yaw |
| --- | --- | --- |
| 60 km/h | 37.9° / 71.2°/s | 34.9° / 62.9°/s |
| 70 km/h | 43.0° / 74.5°/s | 37.6° / 62.2°/s |
| 80 km/h | full spin / 129.3°/s | 39.8° / 61.2°/s |

All three assisted prompt-recovery cases catch below 10 degrees slip and 20 degrees/sec yaw within 0.84/1.00/1.20 seconds. In the more severe 70 km/h, 30-degree, 60-degree/sec case with 0.20-second delayed correction, peak slip changes from a full spin to 43.8 degrees, peak yaw from 140.0 to 81.0 degrees/sec, and catch occurs at 1.48 seconds. The mirrored case matches. Ordinary drifting in the 15–25 degree bin measured roughly 28–46 degrees/sec yaw, compared with about 100–114 degrees/sec approaching the reproduced spin.

It remains possible to spin: delayed severe correction at 80 km/h still exceeds 60 degrees (1.70 seconds instead of 0.44), and continued wrong-way input still enters the danger zone. No artificial angle/yaw cap or automatic save is applied.

Normal-corner and below-25-degree paired CSVs are byte-identical. The transition fixture retains the same 26.0-degree slip peak and 66.7-degree/sec yaw peak, with only negligible damping (0.004/sec) when briefly above 25 degrees. Physical suspension and the existing visual spring remain unchanged.

Native SampleScene Play Mode assertions verify scene wiring, preserved suspension/visual spring settings, zero low-angle/low-yaw/stable-angle/recovery intervention, progressive damping, countersteer bonus, left/right symmetry, yaw acceleration weighting, unchanged pitch/roll/linear motion and tire curves, pause, teleport and disabling. Runtime/editor assemblies compile with warnings treated as errors. Harnesses and CSVs are archived locally under ignored Library/YawStabilityValidation; no test scripts were added to the project's Assets. Native tests run in an isolated project copy with separate PlayerPrefs. One batch startup emitted a UnityEditor.Search index exception unrelated to project scripts; the gameplay assertions completed successfully.

Changed files: new Assets/Scripts/DriftYawStability.cs and .meta, SampleScene.unity (one CarRoot component only), and this document. No manual assignments are needed. No commit or push was made.
