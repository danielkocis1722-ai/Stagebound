# Vehicles and Car Select

## Architecture

There is still one physics CarRoot and one CarController in SampleScene. VehicleRigLoader runs at execution order -1000, resolves the selected VehicleDefinition, applies its VehicleConfig once, then binds its VehicleVisualSetup. It completes before grip caches, respawn, effects Awake and controller Start. It does not move CarRoot, create a second rig, or own race state.

- VehicleConfig: grouped engine, transmission, steering, differential, clutch, brakes/handbrake, rear-grip, front-recovery, yaw, visual-motion, chassis and individual-wheel tuning. Each wheel stores exact local pose/scale, center, radius, mass, spring/damper/travel/target, force point and both friction curves.
- VehicleDefinition: stable ID, name/manufacturer, drivetrain label, description, availability, config, visual prefab, optional preview image/first engine-loop override and four display-only stats. The shared controller currently implements its existing RWD drivetrain; the label does not implement AWD/FWD.
- VehicleCatalog: list of definitions and a playable default. IDs must be non-empty and unique.
- VehicleVisualSetup: explicit body root, four wheel pivots, optional unsprung axle beams, optional brake-light renderers/material slots and a verified light-only material. There are no wheel-name searches.
- VehicleSelection: saves only Stagebound.SelectedVehicleId in PlayerPrefs. Changing the catalog order does not change the selected car. Missing, incomplete or unavailable IDs fall back to the catalog's playable default.

VehicleConfig copies arrays and animation curves into runtime fields. A car cannot alter the shared asset by changing its gears or curves while playing. Applying a config does not run every frame. Inspect VehicleRigLoader.CurrentDefinition / CurrentVisual to see the active entry.

## AE86 baseline and assets

- Assets/Vehicles/Configs/AE86_VehicleConfig.asset
- Assets/Vehicles/Definitions/AE86_VehicleDefinition.asset (ID ae86)
- Assets/Vehicles/Definitions/Vehicle02_VehicleDefinition.asset (ID vehicle02; disabled development entry)
- Assets/Vehicles/VehicleCatalog.asset
- Assets/Vehicles/Visuals/AE86_Visual.prefab

AE86 tuning was captured from the current saved SampleScene, including its latest 0.25 m travel, 2800/3600 dampers, 1.16 rear lateral stiffness, flat 1.0 rear asymptote and visual motion settings. No factory specifications or new handling values were substituted. Individual collider positions, visual offset/scale, torque curve, drivetrain and assist values are copied exactly.

The existing ToyotaAE86-lights scene instance is retained for AE86, with a VehicleVisualSetup added as a prefab-instance override. The visual-only prefab is a reusable copy of that same model and its current material/mesh references. It contains no CarController, colliders or Rigidbody. Future selections instantiate their own visual under the existing CarRoot and deactivate the original visual.

SampleScene's loader references the catalog, AE86 definition, existing visual and chassis BoxCollider. MainMenu references the same catalog. All current assignments are supplied. The existing FollowCamera still follows CarRoot; no camera handling offsets were added.

## Menu and preview

CAR SELECT is a separate Main Menu option below PLAY. Its SELECT button saves the stable vehicle ID and returns to the main buttons; BACK cancels browsing. PLAY opens the map list using the saved car. The map list's START button loads that map with the selected vehicle. See Stages.md for map metadata and overall/per-car records.

Mouse buttons, keyboard arrows/Enter/Escape and gamepad D-pad/left-stick, Cross/Circle use the existing InputSystemUIInputModule/EventSystem. Focus starts on SELECT; left/right on the footer move between SELECT and BACK without changing cars. Up focuses the Car Browser header, where left/right browse cars; down returns to the buttons. L1/R1 switch cars while retaining focus. The disabled Vehicle 02 entry can be browsed but cannot save a selection. Catalog or content errors are displayed as an unavailable selection.

The preview renders only the visual prefab to a small texture with its own camera/light and slow rotation. It uses layer 30 inside MainMenu; reserve that layer for menu previews if adding other menu geometry. It has no AudioListener or running vehicle physics. A preview image can be used when a visual is not ready. Missing preview content shows a simple placeholder. Graphics-less builds/tests use that placeholder safely. Preview cameras/textures are disabled with the panel and destroyed on scene exit.

Display stats are Inspector-editable bars only and never feed physics.

## Importing the next vehicle (no normal controller-code changes)

1. Import your model/FBX and prepare its materials.
2. Create a visual-only prefab. Remove physics, controllers and gameplay scripts; the shared CarRoot supplies those.
3. Add VehicleVisualSetup to the prefab root. Assign a body/visual root that contains all four wheel pivots.
4. Assign FL, FR, BL and BR explicitly. If an imported wheel's pivot, spin axis or rotation is unsuitable, create an aligned empty wheel pivot, parent the mesh underneath it, and adjust the mesh's local position/rotation there. Assign the empty pivot. CarController writes collider world poses to these pivots; it does not search names or infer axes.
5. Optional: bind separate axle-beam meshes to their wheel pairs through Unsprung Axles. Keep them outside the four rotating wheel pivots.
6. Optional: assign brake-light-only renderers and material indices. Supply the dedicated light-only material used by those slots. The current BrakeLights component safely ignores invalid/missing slots and never enables whole-body emission. Use the same dedicated light material for separate left/right renderers, or bind one shared light slot once. Keep normal emission zero.
7. Duplicate AE86_VehicleConfig (recommended) and rename it for the new car. Adjust grouped tuning, positive wheel radii and individual collider local positions to the new wheelbase/track. Configure chassis BoxCollider pose/size and positive scale. Keep the physics WheelColliders on CarRoot.
8. Set Visual Position Offset, Visual Rotation Offset and Visual Scale on the new config. Align the visual with the rig's collider geometry; changing visual scale does not automatically scale physics.
9. Create a VehicleDefinition via Assets > Create > Stagebound > Vehicles > Vehicle Definition. Give it a permanent unique ID, config and VehicleVisualSetup prefab reference. Set its name, description, stats and optional image/engine loop.
10. Add the definition to VehicleCatalog.vehicles. Set selectable only when model/setup and tuning are complete. Leave the default AE86 entry in place as fallback.
11. Test from MainMenu: CAR SELECT, browse/select, return to PLAY, choose a map and START. Then test driving, countersteer, handbrake, clutch/shifts, wheel poses, suspension, smoke/skidmarks, engine audio, brake lights, checkpoints, respawn and Restart Stage.

The development action Stagebound > Vehicles > Capture Selected Rig as Config can save a copy of a selected rig's serialized settings while stopped. It creates a new asset and does not retune the scene. The one-time baseline migration is not an automatic import hook and refuses to overwrite an existing catalog.

## Editing and limitations

Edit the VehicleConfig asset for persistent vehicle tuning. The selected config is authoritative at race startup, so scene CarController/WheelCollider tuning fields are overwritten once at initialization. Stop Play Mode before changing assets and reload SampleScene if Unity already has an older scene instance open.

The current AE86 brake-light component is disabled/unassigned in the baseline; that state is preserved. There are no newly required manual assignments for AE86 or menu flow. Future models require their own wheel/light bindings and assets as above. Vehicle 02 deliberately has no fake AE86 model, physics config or downloaded asset.

This task does not implement an unlock economy, new drivetrain architectures, or new car models. No race/HUD/scoring/pause/respawn/controller physics scripts were rewritten.

## Changed-file manifest

Created runtime scripts (under Assets/Scripts, with Unity metadata): VehicleConfig.cs, VehicleDefinition.cs, VehicleCatalog.cs, VehicleSelection.cs, VehicleVisualSetup.cs, VehicleRigLoader.cs, CarSelectPanel.cs, CarSelectNavigation.cs and VehiclePreview.cs.

Created editor script: Assets/Editor/VehicleSetupEditor.cs. Created the five vehicle assets listed above, their folders/metadata, and this document.

Modified: Assets/Scripts/MainMenu.cs, Assets/Scenes/MainMenu.unity and Assets/Scenes/SampleScene.unity. CarController, physics/assist implementations, FollowCamera, effects, race, scoring, HUD, pause and respawn scripts are unchanged by this feature.

## Validation

Compared the generated AE86 config against a fresh capture of the pre-migration scene: every serialized tuning, curve, geometry and alignment field matched. Matched isolated WheelCollider simulations for coasting, acceleration, handbrake and direction transitions produced zero trace difference in sampled position, velocity, yaw, RPM and rear-grip multiplier.

Additional checks cover an alternate in-memory definition applying different mass/torque/steering/geometry and swapping the visual without another rig, independent runtime curves/arrays, one-time initialization, invalid-ID/placeholder fallback and catalog reordering. The production placeholder is not a duplicate AE86.

Native Play Mode input tests cover pointer PLAY, keyboard navigation/Enter/Escape, gamepad shoulder/D-pad/Cross/Circle, disabled confirmation, selected-ID loading, one EventSystem, actual acceleration, wheel poses, configured audio and smoke/skidmarks, timer/checkpoint/split/results, DriftScore/Style Credit, checkpoint respawn, pause/Restart Stage, return to menu and selection restoration. A graphical run also renders and captures the model preview for visual inspection. These are automated Input System tests, not a physical DualSense road test.

Validation runs in an isolated project copy with a separate PlayerPrefs namespace. Test sources/results/screenshots stay under ignored Library/VehicleSelectionValidation; no test runner is added to the shipped Assets. No commit or push is made.
