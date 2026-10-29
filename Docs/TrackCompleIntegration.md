# Three vehicles and two playable stages

Work performed directly in `C:\Users\dandy\Documents\Projects\Stagebound`. No commit or push. No external vehicle, track, texture, font or plugin assets were downloaded.

## 1. Detected car assets

`Assets/Models/Cars` contains `ToyotaAE86.fbx`, `ToyotaAE86-lights.fbx`, `Skyline_R32.fbx` and `RX7_FBX.fbx`. The existing three-car integration was retained, not rebuilt over the user's configs. The playable AE86 baseline uses the lights FBX.

## 2–3. Verified R32 and RX7 hierarchy mapping

| Wheel | R32 imported path | RX7 imported path |
|---|---|---|
| FL | `Circle.014` | `group9/group3` |
| FR | `Circle.020` | `group10/group3` |
| RL | `Circle.017` | `group9/group8` |
| RR | `Circle.023` | `group10/group8` |

R32 retains its `Plane*` body parts, `Plane.011` windows, `Plane.023` logos/trim and `Plane.024` interior/dummy. RX7 retains `group11` body/trim/windows/interior and all eleven renderers in each wheel group. Its authored 15-degree front steer pose is neutralized. Empty pivots supply common wheel axes while preserving each imported child mesh's required orientation. RX7's hierarchy-derived mesh names and prefab references are checked across a forced FBX reimport.

## 4–5. Scale, visual offsets and WheelColliders

These are Stagebound world units, following the existing oversized AE86/world convention; they are not literal metre measurements. Vehicle configs remain unchanged in this second-track pass.

| Vehicle | Uniform visual scale | Visual position offset | Rotation offset |
|---|---:|---|---|
| AE86 | 0.5 | `(0.115,-0.92,0.7624253)` | zero |
| R32 | 3.881245 | `(0,-0.851482,0.670030)` | zero |
| RX7 | 212.0095 | `(0,-0.853072,0.521016)` | zero |

RX7's unusually large scale corrects its approximately 0.0443-unit imported length. R32's imported length is approximately 2.567 units. New-car final lengths are approximately 9.963 and 9.393 world units.

| Vehicle | FL / FR local positions | RL / RR local positions | Wheel radii FL/FR/RL/RR | Wheelbase | Front/rear track |
|---|---|---|---|---:|---|
| AE86 | `(±1.605,-0.2,3.323)` | `(±1.605,-0.25,-1.938)` | `.65/.65/.65/.65` | 5.261 | 3.21 / 3.21 |
| R32 | `(±1.66303,-0.14807,3.57156)` | `(±1.66303,-0.14807,-2.18656)` | `.72693` each | 5.75812 | 3.32606 / 3.32606 |
| RX7 | `(±1.53354,-0.14036,3.36504)` | `(±1.53354,-0.14036,-1.98004)` | `.73325/.73576/.73477/.73477` | 5.34509 | 3.06708 / 3.06708 |

Negative X is left. The selected config applies its four individual collider positions, radii, suspension and friction before the common controller/effects initialize. No AE86 collider geometry is substituted for the other models.

## 6. Initial vehicle config character

| Value | AE86 | R32 | RX7 |
|---|---:|---:|---:|
| Mass, kg | 1100 | 1450 | 1250 |
| Maximum engine torque, Nm | 360 | 460 | 500 |
| Steering speed | 95 | 82 | 105 |
| Steering return | 150 | 130 | 155 |
| Final drive | 5.3 | 4.1 | 4.3 |
| Engine inertia | .35 | .44 | .34 |
| LSD strength | .6 | .75 | .7 |
| Angular damping | .225 | .28 | .24 |
| Front/rear anti-roll | 2200/1700 | 3200/2400 | 2500/1900 |

These are editable prototype values, not factory specification claims. R32 is explicitly labelled **RWD (DRIFT SETUP)** because it uses the common RWD drivetrain. Its greater mass/inertia, longer wheelbase and slower steering support a planted initial character. RX7 has more torque, faster steering and more power breakaway. AE86's captured baseline remains untouched. Shared controller, assist, drivetrain and drift physics code was not rewritten for either stage.

## 7–8. Materials and brake lights

The existing 24 R32 and 33 RX7 URP Lit materials remain in `Assets/Vehicles/Materials`. R32 uses sensible paint/glass/rubber/metal/interior/light approximations; its outer rear lenses are transparent so they do not hide the emitting lamps. RX7 uses recovered source material colors/metalness/roughness/transmission and all four supplied images: tire-sidewall, door-handle, rear-reflector and plate textures. Complex Blender/Maya shaders are approximated; material slots are preserved.

- AE86: its pre-existing disabled/unassigned brake-light state is retained.
- R32: `Plane.020`, slot 0, dedicated `rear lights` material.
- RX7: `group11/polySurface534`, slot 0, dedicated `aiStandardSurface12` material on the upper red lamp circles. Textured lower reflectors remain non-emissive.

Play Mode checks require brake emission on the allowed slot and no emission on other materials. Track materials were already usable URP Lit palette materials in the actual import; the connector reuses the existing road material and UVs. No new track texture or shader package was introduced.

## 9. Car Select and flow

**Main Menu -> PLAY -> Car Select -> CONTINUE -> Stage Select -> START -> selected race scene.**

The three-car browser retains rotating visual-only previews, vehicle name, drivetrain, config mass/torque and editable display-only bars. AE86/R32/RX7 bars are 5/8/7/5, 7/7/8/8 and 8/8/9/7. Mouse buttons/arrows, keyboard, gamepad shoulders/Cross/Circle and Enter/Escape use the existing UI architecture. Stage Back returns to Car Select; Car Back returns to Main Menu.

The stage list now has **South Loop** and **Track Comple Circuit**, plus the disabled Map 02 placeholder. It shows selected-car and overall **BEST FINAL**, **BEST RAW**, **BEST DRIFT**, Style Credit cap/rate, checkpoint count and difficulty. Selection uses stable IDs in PlayerPrefs, never persistent array indices. The same selected vehicle visual/config is loaded into one common CarRoot per active race scene.

## 10–12. Actual tracks and second scene

The complete track directory contains two real models:

- `Assets/Models/Tracks/SouthLoop.glb`: the existing `SampleScene.unity` track instance references this GLB's GUID.
- **`Assets/Models/Tracks/track comple.fbx`**: the additional model, previously unused by a playable stage.

The FBX has a root named from `track comple` and roughly 250 named mesh objects, including roads, a bridge, S-bends, jump ramps, curbs, tire walls, fences, terrain, buildings, parking and decorative vegetation. Its metadata does not identify a real-world venue, so **Track Comple Circuit** is a descriptive name derived from the actual filename, rather than an invented venue or generic Stage 02 identity.

Created **`Assets/Scenes/TrackCompleCircuit.unity`**. Its gameplay services/CarRoot are copied from the working race setup. The South Loop model, its flat test-floor object and its two race triggers are removed only from this new scene. The original South Loop scene, spawn and triggers remain unchanged.

The imported FBX bounds are about **44.745 x 10.661 x 44.805**. The track root uses uniform **scale 6**, position **`(0,-3.6,0)`**, identity rotation. This produces a roughly 268.5 x 268.8-unit environment, approximately 15-unit asphalt width and 19-unit road-plus-curb width. That fits the approximately 4.2-unit widest car without borrowing South Loop's transforms.

The supplied layout has an intentional open jump gap between `springboard` and `springboard.001`. To make a continuous first playable circuit, a visible elevated deck was generated from a copy of the existing `road` mesh. It spans raw X **-2.075..6.8763**, at raw Z **6.3963**, and matches the **actual asphalt** tip heights of both ramps. Its collider uses the same geometry as its renderer. Original source meshes/files remain untouched. This is an explicit stage-specific geometry repair; there is no invisible road over the gap. Ramps remain a technical feature of the route.

## 13. Road and terrain collision

Sixteen separate static non-convex road MeshColliders:

`bridge`, `crooked road`, `road`, `road.001`, `road.002`, `road.003`, `road 90`, `road 90 fix`, `road 90.001`, `road 90.002`, `road 180`, `road 180.001`, `road 180.002`, `springboard`, `springboard.001`, and `Jump Connector`.

`Earth` and `parking` receive their own static environment MeshColliders. There was no dedicated collider-mesh set in the FBX. Collision meshes are baked from each inspected source transform into world-space geometry under unit-scale collision objects. Mirrored winding is corrected when needed. Static track colliders have no Rigidbody, no negative scale and no convex cooking. No giant environment BoxCollider or one-mesh collider over the entire track was used.

Original road meshes include their beveled curbs and, on ramps/bridge, side structures. Ordinary curb rise is approximately .23 world units. Decals/grid markings do not receive colliders, preventing overlapping driving surfaces. The connector's endpoint heights use asphalt centerline vertices, not taller guardrail renderer bounds.

## 14. Walls/barriers/solids

There are **64 static track/environment colliders total**: 16 road meshes, 21 terrain/solid meshes and 27 boxes.

Simple world-aligned boxes are used for the 12 `fence*` objects, two `barrier*` objects, `house`, `garages`, two `tower*` buildings, the decorative parked `police car`, and eight `flower bed*` objects. Thin box dimensions have a .18-unit minimum thickness. They are fitted to individual visible objects, not to the full scene.

The complex solid mesh group comprises ten `bunch of wheels*` tire-wall pieces, two `wheel fence*` pieces, four `stone*` objects, `stack of wheels`, `tribune`, and `finish board`, plus `Earth` and `parking`. The sign uses exact mesh collision so its open space does not become an invisible wall across the start lane. Curved tire-wall shapes also use exact geometry rather than large boxes cutting into corners.

Vegetation clusters, flags, lamps, grid/parking decals and other small decorative meshes remain non-collidable. The parked police-car model is static scenery, not another playable car/controller.

## 15–17. Spawn, Start/Finish and checkpoints

Authoritative **SpawnPoint**: **`(-0.5646, 1.2619, 48)`**, yaw **180 degrees**, on the existing starting straight, facing -Z. It is 1.25 units above the measured road surface and approximately 20 units upstream of Start/Finish. The new scene's RespawnManager references this transform for initial spawn, Restart Stage and pre-checkpoint recovery.

All race gates use the existing RaceTrigger and BoxCollider architecture, with positive unit transform scale, `isTrigger=true`, box size **`(16.5,6,1.5)`** and the existing Scene-view colored box/label gizmos. Gate centers are 2.5 units above these road-surface coordinates:

| Gate | Surface anchor | Heading | Placement purpose |
|---|---|---:|---|
| Start/Finish | `(-.565,-.025,27.670)` | 180 | Existing finish marking on main straight |
| CP1 | `(-.565,-.069,-72)` | 180 | Lower straight before bottom hairpin |
| CP2 | `(-81.120,-.069,12)` | 0 | West S-bend, requiring the lower route |
| CP3 | `(18,6.483,38.378)` | 90 | Elevated crossing after the west bend/ramp |
| CP4 | `(29.810,.080,66)` | 0 | Upper return after the east hairpin |

The order is main straight southbound -> bottom hairpin -> west S-bend -> elevated crossing eastbound -> east hairpin/inner corner -> upper hairpin -> finish southbound. Four checkpoints prevent an immediate finish or skipping the main sectors. Each has a road-projected respawn point nine world units downstream, with the correct heading. The crossing checkpoint is vertically separated from the main straight underneath it.

The route guide contains **430 real surface samples**, approximately **785.66 world units** long. Empty guide markers are authoring/validation aids; they do not drive the player or replace race trigger logic.

## 18–19. Stage definition and records

Created `Assets/Stages/TrackComple/TrackComple.asset`:

- Stable ID: **`track-comple`**
- Name: **Track Comple Circuit**
- Scene: **`Assets/Scenes/TrackCompleCircuit.unity`**
- Checkpoints: **4**, record version **1**
- Credit conversion: **2000 points/second**, cap **8 seconds**
- Difficulty: **TECHNICAL**, availability enabled, optional preview unassigned
- Legacy scene-record import disabled

StageCatalog includes both real stages, and the new scene is enabled after the two existing build scenes. The stage name/scene/metadata are supplied by the definition; no track-specific scene-loading behavior was added to menu code.

Overall records use `Stagebound.Stage.track-comple.4.v1.style2`, with per-car suffixes such as `.car.ae86`. South Loop retains `Stagebound.Stage.south-loop.1.v1.style2`. Legacy raw keys also contain different scene paths. New-track records therefore do not share South Loop's record namespace. The validation process uses reserved temporary stage IDs, restores selections/migration preferences, and does not deliberately overwrite real best times.

## 20. Validation scope

The matrix runs all three cars on both stages in Unity Play Mode. It checks selected visual/config, one active car, all four wheel positions/radii, settled suspension/road contact, camera binding, acceleration, wheel spin/steering, gearshift, clutch/handbrake, audio/effects bindings and safe lamp emission. Controlled velocity creates actual WheelCollider slip to verify telemetry, score/credit, smoke, screech gain and skidmark segments.

Physical gate tests move the real car Rigidbody/BoxCollider through the real triggers under controlled poses: start begins timing; finish without checkpoints is rejected; an out-of-order final checkpoint is rejected; all ordered checkpoints register; R uses the recorded checkpoint; finish snapshots results and begins the next continuous run. Pause, restart, main-menu return and remembered vehicle/stage selection are checked.

For Track Comple, every route sample is checked with all four selected-car wheel footprints: **1720 support probes per car**, **5160 across the three cars**. Height-aware road queries distinguish the elevated crossing from the straight below. Representative physical suspension poses cover every road section; a controlled lateral push verifies a visible fence blocks each car. These are automated collision/support and race-flow tests, not claims that a human drove six complete laps or that final subjective handling has been tuned.

The final run passed **790 assertions** and exited cleanly. Detailed results, setup measurements and renders are in ignored **`Library/TrackIntegration`**, with the Unity matrix log at `Library/TrackMatrix.log`. A readable collider/placement export is also saved as `Docs/TrackCompleMeasurements.txt`. Tests use the installed/project Unity **6000.6.7f1**. No standalone build or physical DualSense test was performed.

## 21–22. Files and manual work

New scene, stage definition, supporting baked collider/deck mesh assets and metadata live under `Assets/Scenes/TrackCompleCircuit.unity` and `Assets/Stages/TrackComple`. Added explicit authoring utility `Assets/Editor/TrackCompleSetup.cs`. Updated StageCatalog, EditorBuildSettings, StageSelectPanel record labels, the editor validation matrix and the vehicle authoring utility's obsolete one-stage assertion. Documentation and the full Git/file manifest are updated.

The prior car assets/configs and shared driving, camera, timer, checkpoint, drift, effects and respawn runtime implementations remain in use. No imported models were moved. Existing user settings/package/version changes present at the beginning of this pass are preserved. The cumulative changed/new file list is **`Docs/VehicleIntegrationFiles.txt`**.

No manual Inspector/Blender assignment is required for the saved stage. Open MainMenu, select any car, select Track Comple Circuit and Start. Remaining optional work is a physical controller road test, subjective handling/camera polish, custom venue naming/art and refinement of the source ramps/props. Source jump ramps remain steep; the stage is intentionally technical.

`TrackCompleSetup.Run` is an explicit rebuild utility, not an import hook. It replaces the generated new scene/meshes/metadata, so preserve manual changes before rebuilding. It never saves over South Loop. `ValidateVehicleFlow.Run` should be launched in a dedicated editor process and exits after restoring its temporary input/preferences state. Nothing has been committed or pushed.
