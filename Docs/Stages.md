# Maps, menu flow and records

Main Menu now has PLAY, CAR SELECT, SETTINGS and QUIT. CAR SELECT keeps the rotating visual preview, saves a stable vehicle ID on SELECT and returns to Main Menu. PLAY opens SELECT MAP using that car; START saves the stable stage ID and loads the scene by its Build Settings index.

## Map content

Assets/Stages/StageCatalog.asset lists SouthLoop.asset and Map02.asset. South Loop points to Assets/Scenes/SampleScene.unity. Map 02 is a browsable, disabled development placeholder with no scene; it never starts a fake duplicate of South Loop.

StageDefinition retains its existing stage identity/style-credit fields and adds scene path, description, availability and expected checkpoint count for menu record lookup. No track, spawn, checkpoint, physics or HUD layout changes are needed for this menu pass.

To add your next map:

1. Prepare its scene, typically starting from a copy of the existing race scene/rig setup.
2. Create a separate StageDefinition with a permanent unique stageId and the map name/description.
3. Set scenePath to its project-relative scene asset path, including .unity. Add/enable that scene in Build Settings.
4. Assign this StageDefinition to that scene's RaceTimer.
5. Configure its Start/Finish, ordered checkpoints and authoritative start SpawnPoint. Set checkpointCount in the definition to match the actual required checkpoint count; RaceTimer still discovers/validates the actual scene triggers.
6. Add the definition to StageCatalog.stages and enable selectable. Keep unavailableLabel for incomplete entries. Change recordVersion when changing the route so incompatible records remain separate.

The current map is remembered under Stagebound.SelectedStageId. Invalid/unavailable saved IDs fall back to the first playable entry.

## Navigation

Car Select: focus starts on SELECT. Footer D-pad/arrow left/right move SELECT <-> BACK without changing the vehicle. Up focuses the Car Browser header, where left/right browse cars; down returns to the buttons. L1/R1 change cars independently without stealing footer focus. Enter/Cross confirms the focused control, Escape/Circle goes back. Clicking the on-screen arrows returns focus to the browser.

Map Select: D-pad/arrows navigate map rows and START/BACK. Focusing a row updates its details and records without loading anything. Right moves from a row to START (or BACK for a disabled map). START/BACK have normal left/right navigation. The map list scrolls for additional entries. Clicking a playable map row or pressing Cross/Enter on it starts that map directly. The START button also remains available. Confirming a disabled placeholder goes to BACK and never loads a scene.

Both panels use the existing EventSystem/InputSystemUIInputModule and preserve navigation focus after background clicks. The prior focus behavior is restored when a panel closes. No competing input module or EventSystem is added.

## Records

Map details show selected-car best adjusted time, fastest raw time and best drift score, alongside the existing overall adjusted/raw times. Stats are read directly from PlayerPrefs; there is no timer in the menu.

The overall key stays unchanged:

Stagebound.Stage.{stageId}.{checkpointCount}.v{recordVersion}.style2

Independent car records append:

.car.{escapedStableVehicleId}

RaceTimer adds one startup migration hook and one finish-record hook. Its timing, checkpoint order, split calculation, score/credit calculation, overall-record format and continuing-run behavior are unchanged. In-race HUD/result best-time behavior continues to use the existing overall record; the map screen distinguishes overall and selected-car records.

AE86 was the only playable car before per-car records. Its existing overall/style record (or supported legacy raw record) is shown in the menu and frozen into its own record once at race startup. The migration marker is the overall key plus .cars.v1.migrated. This happens before any new vehicle can replace the overall best, so a later car's run cannot become AE86's historical best. Unknown/new cars do not inherit it.

Per-car best adjusted times update only for better valid finishes; fastest raw time and best drift score are maintained independently, matching overall-record semantics. The key also contains stage identity, checkpoint count and record version, keeping routes and vehicles separate.

The development Reset Best Time action still resets only the active route's records. It now also removes that route's indexed per-car records and migration marker. The index lives at the overall key plus .cars.v1.index. Other maps, HUD positions, audio settings and selected IDs are preserved. PlayerPrefs.DeleteAll is never used.

## Files and scope

Created: StageCatalog.cs, StageRecords.cs, StageSelectPanel.cs, StageSelectNavigation.cs, StageCatalog.asset, Map02.asset and this document, with their Unity metadata.

Modified: MainMenu.cs, CarSelectPanel.cs, CarSelectNavigation.cs, StageDefinition.cs, RaceTimer.cs (record hooks only), MainMenu.unity, SouthLoop.asset and Docs/Vehicles.md.

No current car configuration, vehicle handling, suspension, race-scene layout, spawn/checkpoint placement or effect scripts are changed. No commits/pushes. Tests and screenshots are kept in ignored Library/StageMenuValidation, using a separate PlayerPrefs namespace.

Validation passes for gamepad/keyboard focus, carousel/footer separation, shoulder shortcuts retaining focus, independent car selection, map placeholder gating, START/BACK navigation, selected scene/car loading, one EventSystem/CarRoot, overall and per-car finish records, historical AE86 migration, slower-run rejection, cross-map/car isolation, scoped reset, malformed records, pause/restart/menu return and updated record display. Both panels were rendered for visual inspection. Runtime/editor compilation passes with warnings treated as errors. Stop Play Mode and reopen MainMenu before testing the saved scene changes.

