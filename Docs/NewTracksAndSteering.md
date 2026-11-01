# Imported stages and steering investigation

Historical integration report. The subsequent scale, original-texture recovery,
brake/steering polish and stricter reverse-test findings are in [FocusedPolish.md](FocusedPolish.md).

Work is uncommitted. No vehicle handling/configuration, steering, clutch,
suspension, camera tuning or Rigidbody inertia override was introduced.

## Track inventory

All source tracks are under `Assets/Models/Tracks`. No assets were downloaded.
There are no additional source track prefabs/glTF files in that directory.

| Source | Imported mesh objects | Stage | Type | Relative import scale |
| --- | ---: | --- | --- | ---: |
| SouthLoop.glb | 3 | Existing SampleScene / South Loop | Circuit | Existing 1 |
| track comple.fbx | 250 | Existing TrackCompleCircuit | Circuit | Existing 6 |
| Cherry mountain.fbx | 9 | New CherryMountain | Point to point | 0.03 |
| Drift Tracks.fbx | 18 | New DriftTracks | Circuit | 1 |

All four imports have UVs for their mesh vertices. The old Map02 catalog entry
has no real playable scene and remains unavailable.

Both new scenes retain the shared vehicle rig, camera, HUD and effects. Each has
an identity `TrackRoot` containing `ImportedTrack`. Cherry's original root scale
of approximately 100 is multiplied by 0.03 (the scene root therefore shows about
3), rather than replacing its source transform. Its child translation is
(-9.9283, 40.7000, -508.9714). Drift Tracks uses (-1.77813, 0.05767, -95.2423).
The original FBX meshes, pivots, orientation and source files are intact.

## Geometry, materials and race setup

| | Cherry Mountain | Drift Tracks |
| --- | --- | --- |
| Route | About 2,224 m timed descent; 2,279 m guide including approach/runoff | About 1,658 m complete circuit |
| Road width sampled | Median 9.5 m, minimum 9.25 m | Approximately 11–11.4 m |
| Static non-convex MeshColliders | 7 source parts: 3 road/wall sections and 4 roadside solids | 10 source parts: road plus curbs, fence, ground, concrete and solid props/posts |
| Spawn position | (55.403, 201.122, -607.745) | (-36.689, 1.256, -92.930) |
| Start gate center | (58.770, 196.616, -588.106) | (-16.683, 1.506, -92.837), combined Start/Finish |
| Finish gate center | (222.403, 1.629, 458.629) | Same line as start |
| Ordered checkpoints | 9 | 6 |
| Definition / stable ID | CherryMountain.asset / cherry-mountain | DriftTracks.asset / drift-tracks |

Gate boxes are 13 x 5 x 1.5 m. Spawn poses face the route, about 1.25 m above
the surface. Mountain guide and authored checkpoint poses follow the actual
slope. RespawnManager now honors an explicitly authored checkpoint rotation;
its upright fallback is unchanged. Existing authored flat checkpoint poses are
unaffected. Only half-car-length margins are omitted from the open-ended guide;
the road geometry is not shortened.
Route-guide objects are tagged EditorOnly so they are stripped from player
builds; race gates and authored spawn/checkpoint poses are independent of them.

Collision meshes bake source transforms into separate, unit-scale static
objects. No environment Rigidbody, huge combined collider or invisible filler
floor is used. Background tree cards, water and decorative checkpoint cards on
Drift Tracks have no blocking collision. Its low source curbs are retained;
their top is roughly 6 cm above the road.

SouthLoop.glb imports through the existing glTFast package with all seven
embedded textures on its existing URP-compatible glTF materials. No conversion
or destructive mesh edit was needed. Track Comple retains its palette texture.

Drift Tracks has fourteen remapped URP Lit materials using supplied textures.
The broken Track.tif, All Props.jpg and treewallb.tga references are mapped to
Track.png, All Props.jpeg and treewallb.png. Fence/tree/prop alpha masks are
retained through alpha-clipped materials.

Cherry references eight `textures/packed/Image_*` images that are neither
supplied nor embedded in its FBX. Eight neutral URP Lit materials replace the
missing-texture appearance. This makes the road readable; it does not restore
the artist's original terrain/road/sign appearance. The original images are
needed for that restoration. No textures were invented or downloaded.

Stage Select shows the stage type, car/overall records, drift score and Style
Credit cap. Each new definition keeps the current 2,000 points/second conversion
and 8-second cap. Stable stage IDs and checkpoint counts give independent overall
and per-car record keys. Existing stage IDs, records and assets are unchanged.
Point-to-point races stop timing at Finish after all checkpoints; existing
circuits still begin the next run at their Start/Finish line.

## Steering, transitions and inertia

The existing gamepad path shapes the stick/deadzone, then commands
`SteeringInput * maxSteerAngle`. `Mathf.MoveTowards` limits rack angle movement;
Ackermann geometry produces the two physical WheelCollider angles. Visual
wheels use `GetWorldPose`. Neutral stick therefore commands straight steering,
at the return rate, rather than releasing a freely rotating rack.

| Car | Steering / return rate (degrees/sec) | Automatic principal inertia (kg m²) |
| --- | --- | --- |
| AE86 | 95 / 150 | (1588.583, 1763.667, 418.917) |
| R32 | 82 / 130 | (11584.920, 11357.790, 3304.866) |
| RX-7 | 105 / 155 | (8634.643, 8805.931, 2546.834) |

All have a 0.06 steering deadzone. No steering or inertia values were changed.
Unity computes inertia from the colliders and COM. The AE86's 4 m chassis box
is shorter than its measured 5.261 m wheelbase. A uniform box of its entire
visual envelope gives about 9485 kg m² yaw inertia versus the current 1764;
this flags a scale/mass-distribution mismatch, but the visual envelope includes
nonuniform, mostly empty geometry and is not a calibrated mass model. Applying
that estimate would radically change the handling. It was calculated for review,
not applied. R32/RX-7 inertia is closer to their configured body dimensions.

Neutral-stick angle servoing, the configured return/cross-center rates, existing
angular damping and the pre-existing outward-yaw governor can contribute to a
mechanical transition feel. The governor already subtracts outward angular
velocity under its gates; this task neither added nor strengthened it. Suspension
and COM still provide physical weight transfer. No artificial straightening
delay, visual countersteer or new steering/yaw/grip assist was added.

A rack-inertia/aligning-torque model is feasible as a separate experiment, but
stock WheelCollider exposes an angle command and slip/friction data, not native
caster/trail rack dynamics or an exact tire aligning-moment output. Estimated
aligning torque would need a trail/tire-force model and controller torque/target
mapping. Existing public wheel references already expose actual angle, WheelHit
slip/load and vehicle motion for that work and future G29 FFB. No G29 driver or
FFB implementation was added, and current controller handling is unchanged.

References: [WheelCollider steerAngle](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/wheelcollider/steerangle),
[wheel physics/friction](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/class-wheel-collider),
[automatic Rigidbody inertia](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rigidbody/inertiatensor).

## Reverse recovery and validation

The baseline already separates signed drivetrain RPM from engine RPM magnitude.
Forward gear motor torque follows GearDirection, including when wheel RPM is
negative. Clutch slip resists the opposing shaft without setting wheel RPM or
vehicle velocity. Steering retains authority in reverse. Handbrake brakeTorque
does not depend on travel direction. No new drivetrain fix was necessary.

Native tests on all three cars passed backward-to-first/throttle, reverse
steering, sustained reverse handbrake, brief-pulse J-turn recovery and a backward
spin recovery fixture. First-gear motor torque never became negative. The brief
pulse test produced about 152–156 degrees of natural rotation and subsequent
forward travel; this is not a guarantee of an exact 180-degree turn for every
input sequence. Sustained handbrake stopped the car after about 39–47 degrees.
Test fixtures seed initial pose/velocity only; production recovery uses no new
rotation, velocity, yaw torque or steering override.

The existing Play Mode validator passed all twelve vehicle/stage combinations:
1,646 assertions, 23,604 new-track wheel-footprint probes, plus 5,160 existing
Track Comple probes. Coverage includes slope contact, native roadside impacts
with every car, menu navigation/persistence, start/checkpoint/finish ordering,
point-to-point completion, timer, scoring/credit, effects/audio state, camera,
respawn, restart and pause. All five scenes passed missing-script, mesh, material
and shader-reference inspection. These are synthetic-input native tests and
geometry checks, not manual full-route driving or a physical controller test.

Local evidence is under ignored `Library/ImportedTracks` and
`Library/TrackIntegration`; authoring/inspection code was removed from Assets.
The reusable existing ValidateVehicleFlow tool was extended, with no new test
project or runtime framework. User testing is still needed for road feel,
subjective camera clearance and the steep mountain descent. Cherry's original
images are the remaining asset-restoration requirement; Blender is not required
to drive either stage.

## Changed files

- Runtime: StageDefinition.cs, StageSelectPanel.cs, RaceTrigger.cs, RaceTimer.cs,
  RaceHUD.cs and RespawnManager.cs under Assets/Scripts.
- Existing editor validator: Assets/Editor/ValidateVehicleFlow.cs.
- Import material remaps: Cherry mountain.fbx.meta and Drift Tracks.fbx.meta.
- Catalog/build registration: Assets/Stages/StageCatalog.asset and
  ProjectSettings/EditorBuildSettings.asset.
- New scenes: Assets/Scenes/CherryMountain.unity and DriftTracks.unity.
- New stage definitions and supporting assets under Assets/Stages/CherryMountain
  and Assets/Stages/DriftTracks: two definitions, twenty-two materials, seventeen
  collision meshes and their Unity metadata.
- This report. Existing scenes, model binaries, car assets/configs and vehicle
  handling code are unchanged. Final Unity compilation passed with zero C# errors
  or warnings after the temporary authoring script was removed.
