# Conservative cleanup milestone

The working implementation was preserved first in commit
`4c9ffa6debe27a762e62109071372e744c595cd2`.

## Changes

- Formatted the 47 runtime and 7 editor scripts listed below with four-space
  indentation, consistent brace layout, member spacing and explicit private
  access where access was already implicitly private.
- Kept existing logical sections and initialization order. Added a few reference,
  configuration and runtime-state labels; removed decorative divider comments
  from CarController while preserving useful explanations.
- Made 31 Inspector-only settings private with `[SerializeField]`: six in
  BrakeLights, fourteen in DriftScoreSystem and eleven in WheelEffects.
  Names, types, defaults and existing attributes remain unchanged. Fields used by
  other systems or editor tools remain accessible.
- Removed the unreferenced editor coroutine `ValidateVehicleFlow.Frames` and
  its unused UnityEngine.SceneManagement import. Retained Unity callbacks,
  reflection helpers, legacy components, telemetry and useful editor tools.
- No utility consolidation, API replacement, hot-loop optimization or null-handling
  redesign was warranted. Existing shared time formatting remains in RaceTimer.
  Compilation under Unity 6000.6.7f1 found no Stagebound C# warnings.

## Behavior and reference verification

- A Roslyn comparison checked all 54 files in editor and player preprocessing
  modes against the preserved source, allowing only the changes described above.
  Runtime method bodies, expressions, field initializers and member order match.
- All 460 existing non-C# asset files retained their SHA-256 hashes. Scenes,
  prefabs, script metadata, models, audio, materials, vehicle configs and stage
  definitions were not edited. Packages and ProjectSettings were also untouched
  during cleanup, as were third-party/template scripts under TutorialInfo.
- Unity inspected MainMenu, SampleScene, TrackCompleCircuit and all three vehicle
  prefabs: 1,036 GameObjects, 2,136 components and 15,054 object-reference properties.
  No missing scripts or broken inspected references were found. Required CarRoot,
  WheelCollider, wheel visual, spawn, menu catalog and race/scoring bindings passed.
- All 100,993 serialized snapshot entries matched after excluding 211 transient
  `.m_FileID` children of object references whose stable parent GUID/fileID or
  hierarchy references matched. Active volume settings also matched.
- The raw GUID scan still reports the same 12 pre-existing unresolved entries in
  DefaultVolumeProfile.asset (4), PC_Renderer.asset (7) and TMP Settings.asset (1).
  These template/package records were preserved. This audit does not assert that
  every dormant project-wide reference was repaired; no new unresolved entries
  were introduced and none occur in the audited scenes or vehicle prefabs.
- Before and after cleanup, the existing Play Mode validator passed 790 assertions
  across AE86, Skyline R32 and RX-7 on both South Loop and Track Comple Circuit.
  Coverage includes Main Menu -> Car Select -> Stage Select -> Race, selection
  persistence, timer, physical checkpoints/start/finish, drift scoring, Style
  Credit, restart, respawn, pause, wheel visuals, smoke/skidmarks/brake lights and
  audio state. The second track passed 5,160 wheel-footprint support probes.
- These are editor Play Mode tests with synthetic input, not a physical gamepad
  road test, a manual full-lap handling assessment or a standalone build test.

Local evidence is under ignored `Library/CleanupValidation` (source snapshots,
before/after serialization, reference reports and Play Mode logs), with Unity logs
at `Library/CleanupBaseline.log`, `Library/CleanupAfter.log` and
`Library/CleanupFinalCompile.log`. The temporary audit harness was moved out of
Assets after use; the existing reusable ValidateVehicleFlow tool is retained.

## Script inventory

Runtime (`Assets/Scripts`, 47):

```text
AccelerationCameraFeedback.cs  AckermannSteering.cs  AntiRollBar.cs
AudioSettingsPanel.cs  BrakeLights.cs  CarController.cs
CarSelectNavigation.cs  CarSelectPanel.cs  CarTelemetry.cs
ClutchKickCoupling.cs  DraggableHUDPanel.cs  DriftHUD.cs
DriftScoreSystem.cs  DriftStability.cs  DriftTelemetry.cs
DriftYawStability.cs  DrivetrainDebug.cs  FollowCamera.cs
FrontAxleRecovery.cs  MainMenu.cs  MenuSelectionHighlight.cs
PauseMenu.cs  RaceHUD.cs  RaceTimer.cs  RaceTrigger.cs
RearTraction.cs  RespawnManager.cs  SkidmarkSystem.cs  Speedometer.cs
StageboundAudioSettings.cs  StageboundMenuUI.cs  StageCatalog.cs
StageDefinition.cs  StageRecords.cs  StageSelectNavigation.cs
StageSelectPanel.cs  SuspensionTelemetry.cs  VehicleAudio.cs
VehicleCatalog.cs  VehicleConfig.cs  VehicleDefinition.cs
VehiclePreview.cs  VehicleRigLoader.cs  VehicleSelection.cs
VehicleVisualSetup.cs  VisualBodyRoll.cs  WheelEffects.cs
```

Editor (`Assets/Editor`, 7):

```text
IntegrateVehicles.cs  RaceRecordsMenu.cs  RaceSetupMenu.cs
RX7ImportPostprocessor.cs  TrackCompleSetup.cs
ValidateVehicleFlow.cs  VehicleSetupEditor.cs
```
