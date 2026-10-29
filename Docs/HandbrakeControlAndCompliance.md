# Handbrake control and road compliance

## Current follow-up: smaller body motion and a more progressive rear slide

The latest follow-up changes only SampleScene, VisualBodyRoll defaults and these notes. Physical suspension travel is 0.25 m instead of 0.28 m. Added visual roll/pitch limits are halved to 1 degree / 0.6 degrees; strengths are 0.18 / 0.08 instead of 0.34 / 0.15. The visual spring/damping pair is 90/19 instead of 70/18, so this smaller visual movement settles promptly. There is still no added vertical body translation. Physical springs, dampers, anti-roll, COM and mass are preserved from the previous pass below.

Rear sideways-friction stiffness changes from 1.20 to 1.16 (3.3% less peak authority). Asymptote value changes from 0.88 to 1.00, flattening the force curve after breakaway rather than increasing its drop. Front tires, forward friction, rear-grip recovery, handbrake locking, steering and yaw governor are unchanged. Larger reductions were rejected because they reintroduced high-speed handbrake spins.

Matched short-entry fixtures at 50/60/70/80 km/h starting speed peak at approximately 25.8/26.3/26.7/27.0 degrees; established-slide peaks are 26.4/28.8/31.3/33.9 degrees. The longer 80 km/h handbrake fixture peaks around 44.6 degrees without a spin. Left–right fixtures also remain below 34 degrees. These use a diagnostic steering controller, not human stick input. An open-loop case that holds steering/throttle without countersteer still spins at 60 km/h and above; the change is not spin-proof.

The shortened-travel road fixtures retain contact without grounded end stops. The 60 km/h ripple RMS acceleration is approximately 7.06 -> 7.18 m/s², so shortening travel did not materially improve that measure. The 60 mm ramp peak acceleration decreases from approximately 16.85 to 13.69 m/s²; drop settling remains around 0.72 seconds. The visible reduction comes principally from the smaller added roll/pitch, not a claim that all physical vertical motion is halved. In native Play Mode the transition fixture's total roll peak decreases from about 2.22 to 1.42 degrees.

Current validation is under ignored Library/RearSlideValidation. The earlier pass below is retained as history; its 0.28 m travel and rear tire curve are superseded by this section.

This pass supersedes the tuning in ProgressiveDriftEntry.md and NeutralDriftAndSuspension.md. It targets DualSense/gamepad handbrake entries and removes overlapping steering/yaw response paths instead of adding another component.

## Previous pass

- Front damping: 4250 -> 2800; rear damping: 5500 -> 3600. Springs remain 26000, travel 0.28 m, target position 0.38. Mass, COM, anti-roll strengths and visual body motion are unchanged.
- AntiRollBar now applies forces only as a pair when both wheels have contact, with bounded travel measurements that account for collider center. Previously, an airborne wheel was assigned full extension and the remaining wheel could receive an unopposed upward force.
- Handbrake rear lateral multiplier: 0.67 -> 0.80. Drop rate: 1.1 -> 0.65/sec. The existing wheel-locking brake torque remains 15000; reducing the extra artificial lateral loss keeps more authority during and after the lock. Rear recovery rates remain 0.8 normally, 0.6 during a controlled drift, up to 1.6 for deliberate recovery.
- Opposite steering input now uses the existing return-speed setting until the wheels cross center. Return speed: 75 -> 150 degrees/sec; turn-in remains 95. Removed the recovery-dependent steering speed multiplier. Full lock, Ackermann geometry and gamepad input curves remain unchanged.
- DriftYawStability now has one opening-yaw governor, replacing the separate late-angle recovery and entry damping paths. It acts only on grounded, forward, sliding, outward-opening rotation. Weight ramps over 6–14 degrees slip, 10–35 degrees/sec opening, and 45–95 degrees/sec yaw. Maximum response is 6/sec. It removes only some yaw above 45 degrees/sec, never normal rotation below that rate, never linear velocity/pitch/roll, and never targets a heading or writes player steering. Stable or closing slides receive no correction.

FrontAxleRecovery retains its existing bounded grip/risk assessment, but no longer changes steering speed. The old disabled DriftStability component remains disabled. No new runtime components were added. The modified runtime files are CarController.cs, FrontAxleRecovery.cs, DriftYawStability.cs and AntiRollBar.cs; SampleScene stores the corresponding settings.

## Comparative fixtures

Small-road-ripple testing uses a smooth mesh with 25 mm amplitude and 2 m wavelength, plus a 60 mm rising ramp and a 150 mm chassis-drop test. The initial damping sweep reduced 60 km/h ripple acceleration RMS from 10.03 to approximately 7 m/s² and peak ramp acceleration from 22.89 to approximately 17 m/s². The softer damping takes longer to settle after a drop (approximately 0.7 seconds versus 0.5), rather than locking suspension movement to remove bounce. No tested ripple/ramp reached a travel end stop or lost wheel contact. These fixtures do not reproduce every real-track impact.

Matched candidate fixtures use second gear, 70% throttle and the same diagnostic steering controller, not human stick input. Short handbrake entry starts from zero slip with a 0.24-second hold; long entry holds for 0.6 seconds. At starting speeds 50/60/70/80 km/h, short-entry peaks are approximately 25.2/25.6/26.0/26.4 degrees. Long entry peaks are approximately 24.5/25.1/26.9/40.9 degrees. Left–right–left transition fixtures peak below 30 degrees at these starting speeds. Established-slide fixtures remain within 20–30 degrees throughout the four-second measurement window, without yaw assistance in the settled slide. Speed changes during these fixtures: starting speed is not a fixed-speed guarantee.

This is a wider control window, not a guarantee against spins. Long handbrake holds, late countersteer, hard curbs and abrupt mesh steps remain meaningful inputs. Unity WheelColliders use a single ray per wheel, so sharp steps can still produce a pop instead of a rolling contact; this pass retains WheelColliders and does not redesign the road collision geometry. See Unity's [Wheel collider introduction](https://docs.unity.com/en-us/engine/6000.7/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/introduction).

Final checks passed in isolated native Play Mode, including static sag, drop, corners, transitions, curb contact, stable-angle/closing-angle exclusions, steering cross-center speed, excess-yaw bounds, handbrake locking/release and visual impact/teleport behavior. Runtime and editor compilation passed with warnings treated as errors.

The final 60 km/h ripple RMS acceleration is 10.03 -> 7.06 m/s²; ramp peak acceleration is 22.89 -> 16.84 m/s². No grounded end stops or airborne samples occurred in those road-compliance fixtures. The one-front-wheel-airborne check measured zero net vertical anti-roll force; the old algorithm would add approximately 866 N upward in that fixture. Flat-road sag remains approximately 38.6% front / 38.3% rear. These measurements are not a hands-on DualSense driving evaluation.

No engine, clutch, gearbox, base tire curves, scoring, race, HUD, menu, audio, smoke or skidmark changes. No commit or push. Validation artifacts are kept in ignored Library/ControlResetValidation. Stop Play Mode and reload SampleScene before testing saved scene values in an editor that was already open.
