# Controls and Wheel settings

Implemented in Stagebound-real-scale on 2026-11-04. No commit or push.

## Architecture and access

The existing AudioSettingsPanel is now the shared settings shell, retaining its
existing MainMenu and PauseMenu entry points and StageboundMenuUI visual language.
It builds Controls, Wheel and Audio tabs. No unrelated placeholder systems were
added. Controls lists the actual direct Keyboard/Gamepad inputs used by
CarController, RespawnManager and PauseMenu (these driving controls are not
generic Player action-map bindings).

WheelSettings owns the saved LogitechG29 profile. LogitechWheelInput reads the
existing SDK samples and applies that profile. SteeringForceFeedback consumes
only the player enable/gain/polarity values; its tire/SAT calculation and developer
tuning remain intact. No engine, clutch, ratios, steering, tire, suspension,
camera or other vehicle physics were modified.

In gameplay Settings reuses the car's wheel reader. Main Menu Settings creates
a temporary reader only when none exists; it uses the same backend and is
disabled/disposed when Settings closes. It has no vehicle and no FFB component.

## Bindings and capture

The Wheel tab offers all seven H-shifter positions plus Shift Up, Shift Down,
Handbrake, Respawn and Pause. Steering and pedals retain their existing axis
profile and optional measured calibration.

Capture records the current 32-bit DirectInput button mask. Only a newly active
single button is accepted; held buttons, releases, axes and POV are excluded.
Simultaneous new activations are rejected until a clean subsequent activation.
The same flow handles every bindable action. Duplicate use across any wheel
action offers REPLACE / CANCEL; replacement clears the previous assignment.

The modal disables background UI interaction and wheel gameplay actions.
Escape, Circle/Back, the Cancel button, focus loss, lost input, disconnect and
closing the panel end capture without changing the old mapping. Completion
suppresses input through the next frame and until held action buttons release.
Latched shifter gears may remain selected. PauseMenu gives the open settings
panel ownership of Escape, so cancelling never resumes the stage.

No separate neutral binding: no active mapped position selects N. Multiple
active gear positions resolve to N. A physical sixth position also resolves to
N in a five-speed car; a six-speed car uses the same binding automatically.
The existing gearbox receives the selected position, with its existing shift
timing and the physical clutch input unchanged. Paddles are used with H-shifter
mode off.

## Default / physical mapping report

No physical shifter positions were operated or detected during this work.
The final physical mapping must be established by the player in the new UI.
A saved older seven-position map is preserved if present; otherwise:

| Action | Fresh/reset binding (zero-based DirectInput) |
| --- | --- |
| Gear 1 | Unbound |
| Gear 2 | Unbound |
| Gear 3 | Unbound |
| Gear 4 | Unbound |
| Gear 5 | Unbound |
| Gear 6 | Unbound |
| Reverse | Unbound |
| Shift Up | Button 4 |
| Shift Down | Button 5 |
| Handbrake | Button 0 |
| Respawn | Button 3 |
| Pause | Button 9 |

Existing measured paddle/button assignments migrate from the previous
calibration JSON unless a new saved binding exists. Reset restores the table
above and does not reset FFB, audio or Use H-Shifter.

## Persistence

PlayerPrefs keys use the stable model profile prefix
Stagebound.Controls.LogitechG29.v1., followed by Gear1 through Gear6, Reverse,
ShiftUp, ShiftDown, Handbrake, Respawn, Pause, UseHShifter, FFBEnabled,
FFBStrength or FFBInvert. They do not depend on the SDK's transient device slot.
Changes are saved immediately. Legacy G29 shifter and strength keys are read
as migration fallbacks. New keys take precedence, including explicitly unbound
values, so Reset does not resurrect old mappings.

## Force feedback and safety

FFB ON enables normal output only under the existing driving gates: valid wheel,
active wheel driving source, running vehicle, focused game, unpaused gameplay
and fresh physics samples. OFF immediately clears the effect and output state.
Strength is a 0–100% multiplier on the final output after the existing filter
and developer output cap. Zero clears output immediately. Inversion changes only
motor polarity on the next output update, never steering input.

The SDK/backend and native x64 DLL were not rewritten. Existing force cleanup
on pause, focus loss, disconnect, component disable, scene unload and quit stays
in place. Advanced SAT/trail/stiffness/reference torque values remain outside
player settings. Steering range is read-only and labelled as configured when
the SDK cannot report it.

## Navigation and validation

New controls use Unity Selectables and the existing project UI action map for
mouse, keyboard and gamepad navigation. Generic Joystick bindings remain absent
from UI navigation, submit and cancel; the G29 axes cannot navigate these menus.
The meaningful driving-input thresholds were preserved.

ControlsSettingsValidation runs in a separate Unity 6000.6.7f1 batch Play Mode
session. It backs up/restores affected preferences and never polls the SDK or
sends physical force. It checks defaults and legacy paddle migration, duplicate
rejection/replacement, saved-value reload, reset isolation, baseline capture,
ambiguous edges, suppression/cancel, gears/reverse/neutral, five/six-speed limits,
immediate FFB clearing, 25/50/100% final-output scaling, motor inversion, UI action
bindings, tab construction and the disconnected state. Rendered Controls, Wheel,
Audio, disconnected and rebind pages were visually inspected.

Evidence: Logs/ControlsSettings-validation.log and Logs/ControlsSettings/*.png.
The connected status in these layout captures is a synthetic fixture, not
evidence of physical G29 input.

Run with Unity -batchmode -projectPath <project> -executeMethod
ControlsSettingsValidation.Run -logFile <log>. Do not add -quit: the validator
enters Play Mode, runs the checks and exits itself.

## Physical acceptance test still required

1. Open Main Menu → Settings → Wheel with the G29 connected; confirm real
   device/range/input status and mouse, keyboard and DualSense navigation.
2. Bind gears 1–6 and Reverse in the UI. Test baseline-held input, duplicate
   Replace/Cancel and Escape/Circle cancellation. Record the actual indices.
3. In a stage, enable H-shifter and verify 1–5, N and R with the real clutch.
   Verify sixth selects N in a five-speed vehicle. Disable H-shifter and test
   the paddles and rebound Handbrake, Respawn and Pause.
4. Open Pause → Settings; verify rebinding does not shift, respawn or resume.
5. Test FFB ON/OFF while driving and compare 25%, 50%, 100% and inversion.
   Confirm the motor releases on pause, unplug, focus loss and leaving the stage.
6. Exit Play Mode/restart the game and reconnect the G29. Confirm the actual
   mappings/settings remain and still operate correctly.

## Files changed by this task

- Assets/Scripts/AudioSettingsPanel.cs
- Assets/Scripts/WheelSettings.cs and Unity metadata (new)
- Assets/Scripts/LogitechWheelInput.cs
- Assets/Scripts/SteeringForceFeedback.cs
- Assets/Scripts/CarController.cs (only remove the all-seven-mapped gate)
- Assets/Scripts/PauseMenu.cs (settings owns Back/Cancel)
- Assets/Editor/LogitechWheelInputInspector.cs (player guidance/developer foldout)
- Assets/Editor/ControlsSettingsValidation.cs and Unity metadata (new)
- Docs/LogitechG29.md
- Docs/ControlsWheelSettings.md (new)

The working tree already contained other changes and untracked wheel integration
files before this task. Those unrelated changes were retained.
