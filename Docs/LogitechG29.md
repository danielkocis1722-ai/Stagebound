# Logitech G29 on Windows

Stagebound-real-scale uses the official Logitech Steering Wheel SDK 8.75.30 for
DirectInput state and motor output. CarController owns the drivetrain and
Ackermann geometry. Meaningful steering, pedal or driving-button activity
selects wheel, gamepad or keyboard during play. The controller path uses the
existing steering curve, slew and speed-dependent lock. Right-stick camera
input remains independent of driving-source selection.

## Player controls (current)

Open **Main Menu → Settings** or **Pause → Settings**. Both use the same
Controls / Wheel / Audio shell and saved G29 profile. The Controls tab lists
the existing keyboard/gamepad bindings. Wheel contains live status, FFB on/off,
strength, polarity, H-shifter mode, and button rebinding.

Choose REBIND, release any already-held control, then select the requested
gear or press the requested button. Escape / Circle / Cancel dismisses capture.
Duplicate assignments require Replace or Cancel. Button indices are zero-based.
No gear numbers are guessed: bind 1–6 and Reverse yourself; an older learned
profile is migrated if present. Neutral needs no binding. Sixth selects neutral
in a five-speed vehicle. Reset restores the existing paddle/action defaults and
clears gear mappings without changing audio, FFB, or shifter mode.

FFB defaults to off on a fresh profile. The enabled state, strength and inversion
are now saved between launches. The former per-session Inspector arm workflow
below is only a developer diagnostic. Normal players use Settings → Wheel.
See [Controls/Wheel implementation and physical test checklist](ControlsWheelSettings.md).

## SDK installation and optional developer diagnostics

1. Download the Windows Steering Wheel SDK from the [Logitech G developer page](https://www-gaming.logitech.com/en-us/programs/developer-alliance).
2. Copy `Lib/GameEnginesWrapper/x64/LogitechSteeringWheelEnginesWrapper.dll` from
   the official ZIP into `Assets/Plugins/x86_64/` in Stagebound-real-scale. In
   Unity's Plugin Inspector select Windows x86-64 for Editor and Standalone.
   The project has its own C# bindings. G HUB provides the device driver.
   The local DLL and Unity metadata are ignored by Git. The bundled SDK EULA
   grants installation/use rights and reserves other rights to Logitech. Check
   distribution permission before shipping the wrapper with a player build.
3. Set 900 degrees and separate pedals in G HUB. Start Play with the Game view
   focused. CarRoot gains LogitechWheelInput and SteeringForceFeedback at
   runtime. Inspect LogitechWheelInput for the raw axes and button mask.
   Release the three pedals after connection to arm input.
   A detected G29 can drive with the built-in profile as soon as it reports
   valid released pedals. A saved measured profile takes precedence.
4. For optional calibration, stop and select **Start optional G29 calibration**.
   Tick **Measure H-shifter too** first if it is connected. The Game view
   displays the active step. Hold each requested position in the Game view,
   then click **Capture Current Step** in the Inspector. The
   sequence is centre with all controls released, left lock, right lock,
   throttle, brake, clutch, up paddle, down paddle. The optional shifter
   sequence is neutral, gears 1–6, then reverse. Move only the requested
   control at each capture. Each accepted sample is logged. The captured
   calibration is saved locally and used in place of the defaults. VehicleConfig
   stores the editable steering ratio and range.
   Start uses the SDK's last confirmed detection. Capture uses the latest
   valid sample from that wheel for up to 750 ms, including across a focus
   switch. The Inspector shows the sample age and raw axes.
5. The SDK requests the vehicle range when it can read a different current
   range. Steering uses the reported degrees, or the vehicle's configured
   range when the query fails. Match that fallback in G HUB.
6. Stop the car in neutral. In the live Inspector choose **Motor Armed**,
   then **Constant Force Test +15%** and return to Game view within five seconds. The pulse
   lasts at most 400 ms. Check both the physical direction and the SDK
   return/readback shown in the Inspector and logged in Console. Rearm and
   repeat with **Constant Force Test −15%**. **Stop Motor** cancels pending or running
   tests. Each test stops and disarms when it ends.
7. After confirming both directions, enable **Force Feedback** in Settings → Wheel or
   **Motor Armed** in the live Inspector to test normal force feedback.
   Reset the driving output peak before a corner. The Inspector retains the
   peak SDK-accepted command, speed and SAT after the Game view loses focus.
   During rolling, rightward front-contact velocity should pull the wheel
   right. A wheel steered right of straight travel should pull left. If the
   physical direction is reversed, disarm it and select **Invert Motor**
   on SteeringForceFeedback. The model sign and hardware polarity are
   separate checks.

A fresh profile starts with FFB off; subsequent sessions reload the saved setting. The persistent master slider
defaults to 25%. Effects clear on a source switch, pause, focus loss,
disconnect, stale physics sample, component disable, scene unload or quit.
The backend restores the prior range on close when the wheel is reachable.

## Steering and pedals

| Control | SDK sample defaults before measured calibration |
| --- | --- |
| Steering | DirectInput X, −32768 / 0 / +32767 |
| Wheel travel | ±450 degrees at 900 degrees total |
| AE86 / R32 / RX-7 | 11.25:1, independently editable |
| Road-wheel lock | Existing 40-degree inside-wheel limit at any speed |
| Throttle | Y, released +32767, pressed −32768 |
| Brake | Rz, released +32767, pressed −32768 |
| Clutch | Slider0, released +32767, pressed −32768 |
| Pedal deadzone | 2.5% at each endpoint, separately editable |
| Shift up / down | Buttons 4 / 5, typically right / left paddles |
| Handbrake / respawn / pause | Buttons 0 / 3 / 9 |

Button indices are zero-based. The live Inspector shows the raw button mask
and first POV value. Calibration finds the dominant axis from measured motion
and rejects ambiguous or duplicate pedal axes. It captures button indices from
changes relative to the neutral button state. For the optional shifter, an
active captured position selects the existing gearbox gear. With all captured
gear buttons released the gearbox selects neutral. Use **Settings → Wheel → REBIND**
for 1–6 and Reverse independently of wheel and pedal calibration. The observed
map is saved locally and reused in later scenes and sessions. Sixth selects
neutral when the vehicle has five gears. Turn off **Use H-Shifter** in Settings
to use the paddles. Separate pedal mode is required.

## Steering torque

Use Rigidbody.GetPointVelocity at each grounded front contact, relative to any
moving surface. Project it along the steered WheelHit.forwardDir on the road
plane and its perpendicular. Slip angle is atan2(lateral speed,
max(abs(longitudinal speed), 0.4 m/s)). WheelHit forward and side slip are
shown separately in Unity's units.

Estimate Fy as −9 × Fz × slip angle in radians, where Fz is normal load in
newtons. Cap force at μ × Fz with μ = 1.1. Forward slip consumes part of the
available lateral force using a friction ellipse approximation.

Each patch sits behind the steering axis. Tire moment is −Fy × effective
trail × speed fade. The mechanical trail is 0.012 m, and pneumatic trail is
0.055 m multiplied by an editable slip curve. Its factor falls from 100% at
zero slip through 90% at 4°, 42% at 8° and 8% at 15° to zero at 25°.
The speed fade goes from zero at 3 km/h to full at 20 km/h.

Sum both front moments and divide by the steering ratio. Add damping of
−0.08 N·m per rad/s of filtered wheel motion and smooth 0.04 N·m friction
opposing rotation. Divide by a 14 N·m reference, apply vehicle gain, clamp
to ±1 and smooth at 14/s. The configured cap defaults to 45%, with a hard
60% bound. Apply master gain and send the signed percentage through
LogiPlayConstantForce. At the default master and configured cap, the motor
command peaks at about 11%.
The torque goes to the physical motor. Actual wheel position supplies the
next steering input.

## Verification and files

- Windows enumerated the G HUB G29. A read-only test with the official x64
  wrapper identified SDK model 26 in slot 0. The hidden probe returned zero
  axes and did not report the operating range. The live pedal ranges,
  button assignments and motor polarity need the foreground check above.
- 108 automated Unity 6000.6.7f1 checks passed across AE86, R32 and RX-7.
  They cover a missing DLL, keyboard and gamepad ownership, simulated wheel
  axes, jitter rejection, buttons, disconnect fallback, settings toggles,
  and Ackermann wheel steering at 0/30/60/100 km/h.
- Native grounded contacts gave the expected sign in both directions,
  including the steered contact frame with an 8-degree front-wheel angle.
  At ±4° the estimated front axle moments were ±168 N·m for AE86,
  ±237 N·m for R32 and about ±176 N·m for RX-7 before steering-ratio division.
  At a matched 2500 N load, one tire moment fell from 96.6 N·m at 4° to
  33.0 N·m at 25°. Centered straight running gave zero aligning moment
  at 0/30/60/100 km/h.
- Physical countersteer, catching the wheel and oscillation require the
  G29 driving session. The automated tests kept the motor disarmed.
- Runtime compilation passed with warnings treated as errors for both
  the Windows path and the non-Windows guards.

Added LogitechWheelBackend.cs, LogitechWheelInput.cs, SteeringForceFeedback.cs
and the development-only LogitechWheelInputInspector.cs, with Unity metadata.
Updated CarController.cs, VehicleConfig.cs, AudioSettingsPanel.cs, PauseMenu.cs,
RespawnManager.cs and .gitignore. The ignored Library/G29Validation folder
contains the test harness and measurements.

## Local x64 installation

The original DLL in Assets/Plugins/x86_64 was PE32 machine 0x014c and matched
the SDK's x86 wrapper. It was replaced from the supplied Downloads SDK's
Lib/GameEnginesWrapper/x64 folder. The installed DLL is PE32+ machine 0x8664
and matches the source SHA-256. Its direct imports are KERNEL32, ADVAPI32,
VERSION and MSVCR120. The Windows x64 Visual C++ 2013 runtime was present.
Unity's plugin compatibility is set to Windows x64 Editor and Standalone.
A fresh batch Editor successfully imported the DLL and called the SDK entry
points. The foreground hardware connection and measured positions require
the live Game view procedure above.
