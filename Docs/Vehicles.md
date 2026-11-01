# Vehicles, Car Select and Stage Select

Current integration: 29 October 2026. Work was performed directly in `C:\Users\dandy\Documents\Projects\Stagebound`. Nothing was committed or pushed.

## Detected content

Cars already in the project:

- `Assets/Models/Cars/ToyotaAE86.fbx`
- `Assets/Models/Cars/ToyotaAE86-lights.fbx` (the existing playable baseline)
- `Assets/Models/Cars/Skyline_R32.fbx`
- `Assets/Models/Cars/RX7_FBX.fbx`
- Existing RX7 images in `Assets/Models/Cars/texture`: `bridgestone_dueler_hp_sport.jpg`, `hand_poolL.jpg`, `LightBackRx7.jpg`, `rx_plate.jpg`.

Both `Assets/Models/Tracks/SouthLoop.glb` and `Assets/Models/Tracks/track comple.fbx` exist. South Loop uses the GLB in SampleScene; Track Comple Circuit uses the FBX in its own new scene. Both stages are available; Map 02 remains disabled. See TrackCompleIntegration.md for the current two-stage setup, 22-item report and six-combination validation. No external model, texture, track, font or plugin assets were downloaded. Existing user package, quality, player and version changes present before the second-track pass were preserved.

## Shared architecture and baseline

`VehicleRigLoader` still configures one existing CarRoot, Rigidbody, CarController, chassis collider and four WheelColliders before other systems cache their state. A selected visual prefab supplies body/wheel/light bindings. The original scene visual is retained for AE86 and deactivated when another visual is instantiated. No additional playable rigs are placed in the scene.

The AE86 config, prefab, race scene, controller, camera, assists, effects, audio, timer, checkpoints, scoring and respawn scripts were retained. Only AE86's four display bars changed to the requested 5/8/7/5 values. Physics never reads those bars.

The authoritative initial/restart pose remains `Race Start-Finish/Start Respawn`, at `(6.4, 1, 4.8)`. South Loop still has one required checkpoint, an eight-second maximum Style Credit and 2000 points per credited second.

## Imported hierarchy mapping

The mappings were inspected in Unity after import, including renderer bounds and material slots. FBX collection names were not assumed.

| Wheel | Skyline R32 source path | RX7 source path |
|---|---|---|
| FL | `Circle.014` | `group9/group3` |
| FR | `Circle.020` | `group10/group3` |
| RL | `Circle.017` | `group9/group8` |
| RR | `Circle.023` | `group10/group8` |

R32's wheel meshes each include tire, rim and brake material slots. Its body is the collection of `Plane*` renderers; `Plane.011` contains window slots, `Plane.023` contains logos/trim and `Plane.024` contains dummy/interior/plastic/exhaust parts. Those renderers and their material-slot order remain present.

Each RX7 wheel group has eleven renderers: `te`, the named Tire7 mesh, Cylinder020, Support3 and the smaller bolt/rim pieces. They move together under four newly aligned empty pivots. The authored 15-degree front-wheel steering pose was removed about each actual hub; source camber and mirrored geometry were retained. `group11` contains the body, windows, light clusters, trim and black interior/dummy (`pCube10`). Window pieces include `polySurface10`, `polySurface180`, `polySurface514` and `polySurface521`; rear-lamp glass is `polySurface576`.

All four wheel pivots have common controller axes, so the unchanged CarController writes WheelCollider world rotation/position to the pivots while the imported meshes retain their required child transforms.

The RX7 FBX repeats wheel mesh names. `RX7ImportPostprocessor.cs` gives its imported mesh objects unique hierarchy-derived names without moving or modifying the FBX. A forced reimport was tested against the prefab's mesh GUID/file ID/vertex-count signature. Unity still emits source duplicate-name import warnings before postprocessing; the tested final prefab references remain intact. If the FBX hierarchy itself is changed later, rebuild and revalidate the visual.

## Scale and alignment

The existing world uses approximately 2.192083 Unity units per nominal metre, inferred from AE86's existing 5.261-unit wheelbase versus a 2.4 m reference. New models are normalized to reasonable nominal lengths within this established world convention. Their actual imported geometry determines scale, wheel hubs, radius and offsets. Their axle midpoint uses the shared rig's existing 0.6925 local Z reference. No AE86 visual offset was copied onto them.

| Vehicle | Imported bounds before scale (X/Y/Z) | Visual Scale (uniform) | Visual Position Offset | Rotation |
|---|---|---|---|---|
| AE86 | Existing prefab retained | 0.5 | `(0.115, -0.92, 0.7624253)` | `(0,0,0)` |
| R32 | `(1.070412, 0.754126, 2.566965)` | 3.881245 | `(0, -0.851482, 0.670030)` | `(0,0,0)` |
| RX7 | `(0.019811, 0.012811, 0.044305)` | 212.0095 | `(0, -0.853072, 0.521016)` | `(0,0,0)` |

Resulting new-car bounds are about `(4.15453, 2.92695, 9.96302)` for R32 and `(4.20007, 2.71606, 9.39308)` for RX7, in this world's units. The RX7 scale is large because the imported model is only about 0.0443 units long.

## Individual WheelCollider geometry

Positions are local to each wheel's existing parent in the common rig; all new wheel rotations are identity, scales one and centers zero. Suspension and friction remain independently editable for every wheel.

| Vehicle | FL | FR | RL | RR |
|---|---|---|---|---|
| AE86 | `(-1.605,-0.2,3.323)` | `(1.605,-0.2,3.323)` | `(-1.605,-0.25,-1.938)` | `(1.605,-0.25,-1.938)` |
| R32 | `(-1.66303,-0.14807,3.57156)` | `(1.66303,-0.14807,3.57156)` | `(-1.66303,-0.14807,-2.18656)` | `(1.66303,-0.14807,-2.18656)` |
| RX7 | `(-1.53354,-0.14036,3.36504)` | `(1.53354,-0.14036,3.36504)` | `(-1.53354,-0.14036,-1.98004)` | `(1.53354,-0.14036,-1.98004)` |

| Vehicle | Radius FL/FR/RL/RR | Wheelbase | Front track | Rear track |
|---|---|---|---|---|
| AE86 | `0.65 / 0.65 / 0.65 / 0.65` | 5.261 | 3.21 | 3.21 |
| R32 | `0.72693 / 0.72693 / 0.72693 / 0.72693` | 5.75812 | 3.32606 | 3.32606 |
| RX7 | `0.73325 / 0.73576 / 0.73477 / 0.73477` | 5.34509 | 3.06708 | 3.06708 |

New radii use measured wheel-assembly bounds, including retained camber. New chassis boxes are fitted to each sprung body and do not touch the road ahead of the tires. Four-wheel road contact is checked in Play Mode for every selection.

## Editable initial tuning

These are Stagebound prototype values, not claims of factory specifications. R32 uses the shared RWD drivetrain as an explicitly labelled drift setup; no AWD system was invented.

| Value | AE86 | R32 | RX7 |
|---|---:|---:|---:|
| Mass, kg | 1100 | 1450 | 1250 |
| Maximum engine torque, Nm | 360 | 460 | 500 |
| Steering speed, degrees/s | 95 | 82 | 105 |
| Steering return speed | 150 | 130 | 155 |
| Final drive | 5.3 | 4.1 | 4.3 |
| Engine inertia | 0.35 | 0.44 | 0.34 |
| LSD strength | 0.6 | 0.75 | 0.7 |
| Angular damping | 0.225 | 0.28 | 0.24 |
| Front/rear anti-roll | 2200/1700 | 3200/2400 | 2500/1900 |
| Power-slip grip reduction | 0.08 | 0.07 | 0.10 |
| Controlled-drift grip recovery | 0.6 | 0.5 | 0.55 |
| Display bars: power/handling/drift/stability | 5/8/7/5 | 7/7/8/8 | 8/8/9/7 |

New spring/damper values scale with each car's mass relative to the captured baseline. Existing gear ratios, engine curve/redline, clutch/handbrake behavior, tire-curve shapes and assist architecture are reused. R32's longer geometry, mass/inertia, damping and slower steering give it a planted initial character; RX7 has faster steering and more torque/power breakaway. Final subjective handling tuning remains a driving task.

## URP materials and brake lights

Created 24 R32 and 33 RX7 URP Lit material assets under `Assets/Vehicles/Materials/SkylineR32` and `Assets/Vehicles/Materials/RX7`. Every renderer retains its original number and order of slots. See the file manifest for individual material names.

R32 uses simple blue/teal paint, dark rubber/plastic/interior, metallic rims/brakes/chrome, tinted transparent glass, light lenses and retained logo slots. Two renderer-specific `RearLens_0/1` materials make the `Plane.016` outer covers transparent so the red lamps behind them remain visible. Blender procedural shading is approximated, not reproduced node for node.

RX7 uses source Maya base colors, metalness, roughness and transmission where applicable; these were recovered from the existing FBX into `Docs/RX7SourceMaterials.json`. Its four supplied images are explicitly rebound to the original material relationships: tire_side1/tire-sidewall image, hand_door/hand_poolL, light_back/LightBackRx7 and aiStandardSurface55/rx_plate. Reflective window materials are approximated with tinted transparent glass. Complex carbon-fiber/procedural/refraction behavior is simplified to URP Lit.

Brake-light assignments:

- AE86: existing disabled/unassigned baseline state retained.
- R32: `Plane.020`, slot 0, dedicated `rear lights` material. It covers both rear lamp banks; only one BrakeLights lamp binding is needed.
- RX7: `group11/polySurface534`, slot 0, dedicated `aiStandardSurface12` material, verified as the upper red lamp circles. The textured lower `polySurface557` reflectors stay non-emissive.

The existing BrakeLights system clones only the allowed lamp slot. Brake-pedal emission and all other material slots remaining dark are checked in Play Mode. No body-paint slot is allowed to emit.

## Menu and records

Flow: **Main Menu -> PLAY -> Car Select -> CONTINUE -> Stage Select -> START -> South Loop race**.

The separate Main Menu CAR SELECT shortcut opens the same flow. Stage Back returns to Car Select; Car Back returns to Main Menu. Both screens restore focus and keep the existing dark/mint/TMP styling.

Car Select has all three real vehicles, editable display bars, drivetrain, config mass/torque and the existing slowly rotating visual-only 3D preview. Mouse arrows/buttons, Left/Right, L1/R1, Enter/Cross and Escape/Circle are supported. Left/Right browse from the footer as well as the header. Up/Down move focus among Continue/Back/browser controls.

Stage Select includes South Loop and Track Comple Circuit and reuses StageCatalog/StageDefinition. It shows description, selected-car and overall final/raw records, best drift, Style Credit cap/rate and checkpoint count. Optional preview and difficulty metadata are supported by StageDefinition. Selecting a row updates details/focus; START performs scene loading. The disabled Map 02 cannot start. South Loop's existing definition, route, records and Style Credit rules are reused.

`Stagebound.SelectedVehicleId` and `Stagebound.SelectedStageId` store stable IDs, never array indices. IDs are `ae86`, `skyline-r32`, `rx7-fd` and stage IDs `south-loop` / `track-comple`. Race startup resolves the selected definition and applies all four collider geometries before the common controller/effects initialize. Existing overall/per-car record code is unchanged.

## Validation and limits

Tested with Unity **6000.6.7f1**, now also the project version at the start of the second-track pass. User settings/package/version changes already present were preserved. No standalone player build or physical DualSense test was performed.

The explicit editor checks validate imported references, catalog uniqueness, actual South Loop checkpoint metadata, single rig, authoritative spawn and mesh-reference survival across a forced RX7 reimport. Play Mode checks cover each selected vehicle's config, four wheel positions/radii, suspension poses, four-wheel road contact, camera, acceleration/RPM, wheel rotation, steering, gearshift, handbrake and clutch. Controlled velocity on the real road produces actual WheelCollider slip and verifies telemetry, drift score/credit, smoke, screech gain and skidmark segments.

Menu tests exercise pointer-button confirmation, synthetic gamepad shoulders/Cross/Circle, keyboard Enter/Escape, disabled-stage gating, Back navigation, stage loading, results/continued runs, pause/restart/menu return and saved selection. AE86's config is compared before/after the runs. Test result records and selected preferences are restored; user records are not deliberately overwritten. Screens and all three car renders are inspected separately from assertions.

Detailed evidence lives in ignored `Library/VehicleIntegration`: `inspection.txt`, `setup.txt`, `playmode.txt`, menu/model/race/braking PNGs and Unity logs. The editor may log its Search indexing exception and source duplicate-mesh-name warnings; these are separate from the passing gameplay assertions. Audio gain/clip behavior is verified programmatically; no claim is made of an auditory listening test or final subjective handling quality.

No Blender or Inspector assignment is required to try the saved setup. Open `Assets/Scenes/MainMenu.unity`, press Play and follow the flow. Optional later work is final handling tuning, more detailed shaders/interior presentation and a physical controller road test. The AE86's pre-existing disabled brake lights remain a known baseline limitation.

## Files and repeatability

Modified runtime scripts: `MainMenu.cs`, `CarSelectPanel.cs`, `CarSelectNavigation.cs`, `StageDefinition.cs`, `StageSelectPanel.cs`. Modified assets: `AE86_VehicleDefinition.asset` (display bars only) and `VehicleCatalog.asset`.

Created: R32/RX7 visual prefabs, configs, definitions and 57 URP materials with Unity metadata. New editor utilities: `IntegrateVehicles.cs`, `RX7ImportPostprocessor.cs`, `ValidateVehicleFlow.cs`. Added source material metadata and updated this document, `Stages.md` and `MenuAndRaceFlow.md`. The exhaustive Git/file list is `Docs/VehicleIntegrationFiles.txt`.

`IntegrateVehicles.Run` is an explicit one-time preparation/rebuild command. It does not run on normal imports, but deliberately rebuilding replaces the two generated configs with the recorded initial tuning. Do not run it over later manual tuning without saving those changes. `ValidateVehicleFlow.Run` is the explicit Play Mode verification entry point and exits its editor process when complete. It should be run in a dedicated editor process, not in an editor session with unsaved work.

## Current runtime layout

RaceHUD is attached directly in both race scenes. The Speedometer bootstrap,
hidden CarTelemetry overlay, inactive DriftStability implementation and optional
DrivetrainDebug logger have been removed. SuspensionTelemetry retains the body
motion and contact count used by VisualBodyRoll; experiment-only wheel statistics
are gone. StageRecords owns record parsing shared by the timer and stage menu.
Earlier handling documents record historical experiments and may describe debug
fields that are no longer present. Active handling settings and config values
remain unchanged.
