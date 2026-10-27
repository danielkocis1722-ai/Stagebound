# Main Menu and continuous time attack

No commit or push. Vehicle handling/physics, camera, audio generation, effects and
respawn physics are unchanged. The existing HUD panel styling, sizes, opacity,
positions and F2 editing controls are retained.

## Scenes and menu

- Assets/Scenes/MainMenu.unity is the first enabled build scene.
- Assets/Scenes/SampleScene.unity remains the playable stage, second in the list.
- Both resolve for runtime SceneManager loading in the current Unity configuration.
- In the Editor, open MainMenu to test from the menu, or keep opening SampleScene
  directly when working on the car. No automatic Editor redirect was introduced.

The dedicated menu uses the existing TMP font and HUD palette: dark panels,
white/muted text and mint highlights. Mouse hover and keyboard/gamepad selection
show a small arrow/highlight, with a short color transition. No graphics assets
were downloaded. UI and its Input System EventSystem are built at runtime,
matching the existing prototype HUD approach.

PLAY restores Time.timeScale=1 and pause state, locks/hides the cursor, and loads
the stage with its enabled gameplay controller. SETTINGS opens the shared audio
panel. QUIT calls Application.Quit in builds; in the Editor it only exits Play Mode.
Returning through Pause > Main Menu clears pause, restores time and cursor state,
and loads the dedicated menu. The old placeholder is connected.

## Settings

Master and SFX sliders work immediately and persist separately in PlayerPrefs:

- Stagebound.Audio.Master.v1
- Stagebound.Audio.SFX.v1
- Stagebound.Audio.Music.v1 (reserved for future music)

There is currently no AudioMixer or music system. All current prototype audio is
SFX, so the no-mixer fallback applies Master*SFX through AudioListener.volume.
VehicleAudio, its clips, pitch/volume logic and sources were not modified.
The disabled MUSIC / COMING LATER row is explicitly a placeholder.

StageboundAudioSettings has an optional mixer path via MainMenu.audioMixer, with
exposed dB parameters MasterVolume, MusicVolume and SFXVolume. A future mixer
integration must also route sources to the corresponding mixer groups. No mixer
assignment is necessary for the current working fallback. The same settings panel
is used from the pause menu, and Back saves preferences and restores selection.

## Why the uncounted lap happened

The previous Start/Finish handler chose either StartRace or FinishRace. FinishRace
stopped timing, so the next crossing was only a new start. Everything driven
between those two crossings was uncounted. The popup used raceFinished directly,
which kept it visible throughout that stopped interval.

RaceTimer.CrossStartFinish now performs one of three operations:

1. Before the first run: begin timing and clear current checkpoint/split state.
2. During a run without all checkpoints: ignore the finish request and keep timing.
3. After all checkpoints: record the result/best, snapshot completed-run data,
   reset current time/checkpoints/splits and immediately begin the next run.

RaceTrigger calls this operation only on the first collider entry. The existing
HashSet of car colliders remains in place, so staying inside or partially exiting
the trigger cannot cause another race-line event. A successful initial/next start
also resets RespawnManager's checkpoint selection through its existing BeginStage
method. That does not teleport the car or modify its physics.

## Informational finish popup

RaceTimer.LastResult is an immutable completed-run snapshot containing the time,
best, gap/new-best flag and unscaled completion timestamp. RaceHUD reads that
snapshot independently of the live timer. A later run cannot overwrite the popup
with its current time/split data; a new finish replaces it with the newer result.

RaceHUD.resultDisplayDuration defaults to 5 seconds and is exposed in the existing
scene component. The popup automatically hides when that real-time interval ends,
including when the HUD has temporarily been disabled for pause. It does not stop
time, change controls/cursor, or receive pointer raycasts. The live HUD continues
showing the new run's timer and checkpoint count underneath it.

Best-time key format, JSON/split persistence and comparison rules are unchanged.
Only a better valid finish replaces the record. ResetTimer clears the transient
snapshot but retains the best. Restart Stage therefore returns to the existing
authoritative spawn, clears the popup/current run and waits for the next start
crossing. Normal respawn remains unchanged and does not reset the running timer.

## Verification

The full sequence ran in Unity Play Mode:

- Main Menu, settings and PLAY -> SampleScene, with Input System UI and raycaster.
- Master/SFX changes affected audio gain; music row was disabled as a placeholder.
- First crossing started Run 1; crossing without checkpoint did not finish.
- Checkpoint and split registered, then one line event finished Run 1 and started
  Run 2 immediately at zero current time/checkpoints.
- Multiple colliders and partial exit/re-entry did not create a second event.
- The previous result remained visible without raycast blocking, then expired
  after five real seconds while Run 2 kept timing.
- Run 2's slower finish retained the best, refreshed the result and started Run 3.
- Pause, shared settings, Restart Stage's authoritative pose/initial state, and
  Main Menu scene return restored time, controller, cursor and pause state.
- No duplicate EventSystem leaked across scene changes.
- The QUIT action safely exited Play Mode. Original editor scene/audio preferences
  were restored, and only a uniquely named temporary test record was removed.

The Main Menu/settings were inspected visually, and the Master slider was dragged
with the mouse successfully. Runtime/editor compilation passed with warnings
treated as errors. Temporary validation code/reports stay under ignored
Library/UIFlowValidation. The real race-record key was preserved; validation did
not reset HUD preferences, and the existing HUD load/repair behavior is unchanged.

## Files in this pass

Created:

- Assets/Scenes/MainMenu.unity and .meta
- Assets/Scripts/MainMenu.cs and .meta
- Assets/Scripts/StageboundMenuUI.cs and .meta
- Assets/Scripts/MenuSelectionHighlight.cs and .meta
- Assets/Scripts/AudioSettingsPanel.cs and .meta
- Assets/Scripts/StageboundAudioSettings.cs and .meta
- Docs/MenuAndRaceFlow.md

Modified:

- Assets/Scripts/RaceTimer.cs
- Assets/Scripts/RaceTrigger.cs
- Assets/Scripts/RaceHUD.cs
- Assets/Scripts/PauseMenu.cs
- Assets/Scenes/SampleScene.unity (only Result Display Duration on RaceHUD)
- ProjectSettings/EditorBuildSettings.asset

No manual Inspector assignment is required for the current prototype.
