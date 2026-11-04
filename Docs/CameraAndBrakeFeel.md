# Calmer camera and service braking — 2026-11-03

Current tuning is the latest follow-up at the end of this report; earlier measurements describe the first pass.

Scope: experimental real-scale project only. No commit or push.

Camera tuning applies to SampleScene, TrackCompleCircuit, CherryMountain and
DriftTracks, with matching FollowCamera defaults. FOV is now 60–66 degrees,
acceleration bonus 1 degree and drivetrain-hit bonus 0.3 degrees. The camera
pullback, vertical motion, braking push and drift lateral/orientation response
are reduced. Low-speed chase distance is slightly farther out; speed-dependent
changes are smaller. Actual vehicle speed and displayed speed are unchanged.

ServiceBrakeAssist is an independent pressure regulator per wheel, called only
for the normal service-brake contribution inside CarController.HandleBrakes.
It compares wheel peripheral speed to contact-point velocity in the wheel's
longitudinal direction (including a moving ground Rigidbody). It reduces pressure
above 30% braking slip and restores it below 22.5%, with a low-speed bypass below
8 km/h for a complete stop. This is deliberately an ABS-style driving aid, not
an increase in tire grip or a change to the drivetrain. WheelHit.forwardSlip and
all effects still report actual physical tire behavior.

Rear regulation is bypassed during handbrake use. Handbrake torque and engine
braking are added separately and remain unchanged. Pressure resets with brake
release, disabled assistance, loss of contact or low speed. Forward and reverse
use the same signed rolling-speed calculation.

Each vehicle's existing Handbrake/brakes config group now exposes serviceBrakeAssist
(enabled, targetSlip, minimumSpeedKmh, releaseRate, recoveryRate). Config ApplyTo
and CaptureFrom copy these settings without sharing a mutable instance. AE86,
R32 and RX-7 configs explicitly enable the aid. Existing brake torque and bias
remain unchanged. Disable enabled to compare with the previous brake behavior.

Files: Assets/Scripts/ServiceBrakeAssist.cs (+ meta), CarController.cs,
VehicleConfig.cs, FollowCamera.cs; the three Assets/Vehicles/Configs assets;
FollowCamera blocks only in the four gameplay scenes listed above; this document.

No mass, centre of mass, inertia, spring/damper, friction, steering, gearing,
power, drift grip/governor, HUD, race, records, respawn or pause tuning changed.
Existing Unity-generated ProjectSettings changes are not part of this pass.

Verification results: see ignored Library/BrakeCameraValidation. Native brake
checks run in a separate project/product namespace, leaving player records and
HUD preferences untouched. Subjective camera/handling feel still needs a drive.

## Final native measurements

Unity 6000.6.7f1 Play Mode, synthetic gamepad, independent flat-pad fixture,
neutral gear to isolate service brakes. No subjective driving claim.

100 km/h nominal full-trigger stops: AE86 48.721 m (same with aid off/on),
R32 49.388 m (same), RX-7 49.150 / 49.156 m. Nominal flat-pad service braking
already had no prolonged locks; the aid stays out of its way.

A separate stress case doubles service torque only in the test fixture:
AE86 54.562 -> 43.828 m, R32 55.146 -> 42.332 m, RX-7 53.886 -> 42.088 m.
Wheel samples with more than 70% lost rolling speed above 10 km/h fall from
about 96% to zero in all three stress cases. This doubled torque is NOT shipped.

Reverse stops from 40 km/h finish in 9.28–9.72 m with no prolonged locks.
60 km/h light-steering/full-brake probes stop in 17.89–18.03 m with no prolonged
locks. Full rear handbrake torque, zero front torque for handbrake-only, brake
release and per-car config/camera loading checks all pass. All 66 assertions pass.
Runtime compilation also passes with warnings treated as errors.

The first 18% target was rejected: it prevented lock but needlessly lengthened
nominal stops to about 62 m. The final 30% target preserves normal braking and
intervenes on actual near-lock behavior. Tire/friction curves remain unchanged.
## Small follow-up after positive driving feedback

User requested slightly more of the same effect. Reused the existing systems;
no additional runtime logic. Fast FOV 66 -> 65, acceleration FOV bonus 1 -> 0.8,
hit bonus 0.3 -> 0.25, acceleration pullback 0.12 -> 0.10 m, braking push
0.06 -> 0.05 m. Drift sensitivity 0.60 -> 0.55, velocity-direction weight
0.70 -> 0.65, side offset 0.10 -> 0.08 m. Slow distance 3.80 -> 3.85 m,
slow height 1.40 -> 1.42 m; FOV and acceleration response are slightly slower.
Applied to defaults and all four gameplay scene camera blocks.

Service pressure target 0.30 -> 0.28, release rate 18 -> 20, recovery rate
remains 3. Explicit settings updated in AE86, R32 and RX-7 configs.
Nominal 100 km/h stops: AE86 48.224 m, R32 49.396 m, RX-7 49.224 m.
All nominal, doubled-torque stress, reverse and mild corner-braking samples
have zero prolonged wheel-lock fraction. Stress stops remain about 44–46 m,
versus 53–55 m without assistance. Full handbrake/release checks pass.
66 native assertions pass; warnings-as-errors compilation passes.
Logs: Library/BrakeCameraValidation/followup-results.txt. No commit/push.
## Stronger service brakes — 2026-11-04

User requested materially shorter stopping distances, retaining the accepted
camera and handling. Changed only service-brake configuration and pressure tuning:
AE86 brakeTorque 650 -> 975 Nm, R32 957.6535 -> 1436.4803 Nm, RX-7
801.007 -> 1201.5105 Nm (all +50%). Existing front bias and handbrake torque
unchanged. ServiceBrakeAssist targetSlip .28 -> .36, recoveryRate 3 -> 5;
releaseRate remains 20. Matching defaults changed in ServiceBrakeAssist.cs.
Camera, mass, tire friction, suspension, engine and all other handling unchanged.

The previous target was conservative under increased brake pressure. A native
sweep compared 1.5x and 1.8x torque with .28/.32/.36 targets at recovery 5.
Chosen 1.5x/.36 retains a less aggressive pedal while using the tire's available
longitudinal force. Simply increasing torque at .28 delivered smaller gains.

Final native flat-pad 100 km/h -> 0.1 m/s full service stops in neutral:
AE86 38.422 m (previous matched sweep baseline 48.743), R32 39.960 m (49.396),
RX-7 38.893 m (48.703). About 19–21% shorter, with zero nominal prolonged-lock
fraction. A repeat has normal small variation (RX-7 sweep 38.342 m).
40 km/h reverse stops finish in 7.61–7.87 m. Mild-steering 60 km/h full-service
stops finish in 13.93–14.63 m. Full handbrake and complete brake-release checks
pass. The doubled-new-torque stress case has under 1% locked-wheel samples.
These are synthetic-input physical measurements, without driver reaction time;
actual roads/slopes and driver inputs affect the distance.

69 final assertions pass; runtime warnings-as-errors compilation passes.
Native sweep/final logs and harnesses: ignored Library/StrongerBrakes.
No commit/push. Existing unrelated Unity ProjectSettings changes preserved.