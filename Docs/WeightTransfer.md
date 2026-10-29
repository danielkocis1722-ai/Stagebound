# Suspension and body weight transfer

This pass changes SampleScene suspension settings and adds a small acceleration-driven visual spring. It does not change existing vehicle, drivetrain, steering, tire-friction, race, scoring, HUD or effects scripts.

## SampleScene settings

| Setting | Before | After |
| --- | ---: | ---: |
| Rigidbody mass | 1100 | 1100 |
| Center of mass Y | -0.35 | -0.25 |
| Angular damping | 0.225 | 0.225 |
| Suspension distance, all wheels | 0.32 | 0.35 |
| Spring, all wheels | 20000 | 18000 |
| Damper, all wheels | 2800 | 2600 |
| Target position, all wheels | 0.50 | 0.50 |
| Force application distance, all wheels | 0.15 | 0.12 |
| Front anti-roll | 2500 | 1800 |
| Rear anti-roll | 1800 | 1300 |

Center of mass remains the existing CarController scene setting applied in Start; no second physics/COM controller was added. Wheel radii, mount positions, friction curves, steering, combined slip, engine torque, clutch and handbrake settings are unchanged.

## Components and model wiring

CarRoot now has SuspensionTelemetry and VisualBodyRoll. All references are assigned in SampleScene.

SuspensionTelemetry exposes filtered local lateral/longitudinal acceleration, four normalized suspension compressions, four contact loads, grounded count and physical roll/pitch in the Inspector. Compression 0 means extended and 1 means compressed. It rejects initialization, large discontinuities and teleports so respawn does not produce a false acceleration kick.

VisualBodyRoll uses an angular spring with velocity/inertia, integrated in small time steps. Defaults:

- Roll strength: 0.4 degrees per m/s².
- Pitch strength: 0.18 degrees per m/s².
- Spring: 70; damping: 12.
- Additional visual roll limit: 3 degrees.
- Additional visual pitch limit: 1.5 degrees.

Positive debug roll means right side down; positive pitch means nose down. Calculated Body Roll/Pitch adds the small visual contribution to physical tilt for tuning. These are world-level tilt measurements, so track slope contributes to physical pitch.

At runtime only the ToyotaAE86-lights body renderer leaves are parented under a new `Sprung visual body` pivot at the center of mass. Existing renderer objects and brake-light references are retained. CarRoot, Rigidbody and WheelColliders are never rotated by this component. The four wheel roots remain outside the pivot and continue following the existing CarController GetWorldPose implementation. The model's Cylinder.011 and Cylinder.012 axle beams follow their corresponding wheel-pair centers independently, without inheriting body tilt or individual wheel spin. No imported model asset is edited.

Disabling VisualBodyRoll restores neutral visual motion; removing it restores the original body hierarchy. Pause freezes the spring. A detected respawn clears spring motion.

## Validation

Unity 6000.6.3f1 native physics tests used the current vehicle in an isolated flat-ground fixture. The baseline was captured before this pass. Native Play Mode checks also ran against a copy of the final SampleScene, with separate validation PlayerPrefs so race records and HUD preferences were not touched.

| Measurement | Before | After |
| --- | ---: | ---: |
| Physical corner roll peak | 0.94° | 1.57° |
| Physical transition roll range | -0.91° to +0.97° | -1.43° to +1.55° |
| Apparent corner roll with visual spring | n/a | 4.57° |
| Apparent transition range with visual spring | n/a | -4.43° to +4.55° |
| Brake dive above settled pitch | 0.49° | 0.69° |
| Acceleration nose rise from settled pitch | 0.55° | 0.61° |
| Corner maximum normalized compression | 0.585 | 0.633 |
| Corner maximum body slip | 7.6° | 8.1° |
| Transition maximum body slip | 8.3° | 8.4° |

Both configurations retained all four wheel contacts throughout the 200-sample corner and transition runs. Final suspension settled within 5 mm in 1.14 seconds; final one-second height variation rounded to 0.00000 m. Resting compression remained approximately 0.50 on every wheel, with 0.786 m body-collider clearance. The small body-slip differences show that suspension changes still affect dynamics even though friction is unchanged; subjective drift feel requires driving evaluation.

Play Mode assertions verified outside lean, inertial side changes, settling, braking dive, acceleration squat, pause, respawn reset, independent wheel meshes, unchanged Rigidbody pose from the visual layer and unchanged tire friction. Rendered controlled left/right spring poses were inspected. Live track motion was checked by telemetry and assertions; its offscreen captures were not usable for visual assessment.

Runtime and editor assemblies compiled with warnings treated as errors. Temporary validation scripts were moved out of Assets after testing; logs, source harnesses and measurements are under Library/WeightTransferValidation (local, not committed). All pre-existing runtime script hashes match their pre-pass snapshots. The scene diff contains only the listed suspension/COM/anti-roll settings and the two new components/model references.

No commit or push was made. Reload SampleScene if the open Unity editor prompts about external scene changes.
