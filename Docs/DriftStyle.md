# Time Attack + Drift Style

Implemented for the current South Loop route. Vehicle physics, handling, gearbox,
camera and effects are unchanged. This pass is intentionally uncommitted.

## Detection and telemetry

`DriftTelemetry` reads the current Rigidbody and BL/BR WheelCollider ground hits
after the car's physics update. It never writes physics state.

```
v = horizontal Rigidbody.linearVelocity
f = normalized horizontal vehicle forward
signed angle = SignedAngle(f, v, world up)
speed = length(v) * 3.6
forward speed = dot(v, f) * 3.6
```

A valid drift requires all of:

- Horizontal speed >= 28 km/h.
- Forward component >= 20 km/h (reject reverse and near-stationary rotation).
- Absolute angle >= 10 degrees and < 65 degrees.
- Both rear wheels grounded by default (configurable to either wheel).
- At least one grounded rear tire with `abs(sidewaysSlip) >= 0.15` OR
  `abs(forwardSlip) >= 0.25`.

Steering input is not used. Vertical speed is ignored. Positive signed angle
means velocity points to the vehicle's right; negative means left. Scoring uses
the absolute angle. Drift direction is +1/-1 during a valid drift, otherwise 0.
Current speed/angle/yaw and all four rear slip values remain observable even
outside a drift. Yaw is world-up angular velocity in degrees/second.

Duration, time-weighted average speed/angle, and peaks describe the current
uninterrupted detected drift; they reset on invalid detection. Scoring continuity
has its own transition grace and is deliberately independent of these statistics.

## Score and multipliers

Each valid physics sample adds:

```
100 points/second * angle curve * speed curve * combo * eligible delta time
```

Angle curve defaults (degrees -> multiplier):

| Angle | 0 | 10 | 15 | 25 | 35 | 45 | 55 | 60 | 180 |
|---|---|---|---|---|---|---|---|---|---|
| Multiplier | 0 | .25 | .6 | 1.5 | 2.3 | 3 | 1 | 0 | 0 |

The editable AnimationCurve interpolates between these keys. Angles below the
detection threshold earn nothing. The potential reward peaks at 45 degrees,
declines toward a spin, and reaches zero at 60. Output is clamped to 0..10.

Speed curve defaults (km/h -> multiplier):

| Speed | 0 | 25 | 50 | 80 | 100 | 140+ |
|---|---|---|---|---|---|---|
| Multiplier | 0 | .15 | 1 | 1.8 | 2.3 | 2.5 |

Output is clamped to 0..2.5; high speed cannot increase it without bound.

Combo is `clamp(1 + valid scoring seconds * .12, 1, 3)`. Grace time does not
increase the combo or score. A gap of up to .75 seconds can link drifts without
losing the combo. Longer grip recovery banks it. A sign reversal preserves the
combo and awards 50 points only after both the old direction and the new direction
have each supplied at least .35 seconds of valid drift. Repeated tiny sign taps
do not generate transition bonuses.

Low total/forward speed ends the combo immediately. Angle >= 65 degrees or
absolute yaw >= 150 degrees/second suppresses scoring immediately and ends the
combo after .2 seconds. Any other invalid state gets at most the .75 second grace.
Ending a combo banks all its points; existing stage score is never lost.

## Farming protection

In addition to the speed, forward motion, ground contact and spin gates, each
20x20 metre world-XZ cell has at most 3 eligible scoring seconds per run.
Exhausting a cell ends/banks the combo. Moving into unused ground allows scoring
again; revisiting a used cell does not refill it. Checkpoints and normal respawn
do not refill these budgets. A new run or Restart Stage does.

This is a prototype spatial progression guard, not a full racing-line validator.
It allows exploration of new areas, while the global eight-second credit cap
bounds the advantage even if the player finds a route around the local guard.

## Credit, results and records

`Assets/Stages/SouthLoop.asset` is the assigned StageDefinition:

- Stage ID `south-loop`, display name `South Loop`, record version 1.
- Points Per Second Credit: **2000**.
- Max Style Credit: **8 seconds**.
- Legacy scene-record import enabled for this upgraded route.

```
Drift Score = banked score + active combo score
Style Credit = clamp(Drift Score / 2000, 0, 8)
Final Adjusted Time = max(0, Raw Time - Style Credit)
```

The live clock remains raw elapsed time; credit is a separate readout. At finish
the active combo is banked and an immutable RunResult captures raw time, score,
credit, adjusted time, best, previous-best gap and completion timestamp. The
same crossing immediately starts a fresh run with zero score, combo, credit,
timer and checkpoint state. The previous result remains visible for the existing
five unscaled seconds and does not pause or intercept gameplay input.

Best adjusted time is the primary record. Best raw time and best drift score are
independent records, updated even if the adjusted best was not beaten. Raw
checkpoint splits from the best adjusted run are retained for live split
comparisons. A zero adjusted time remains a valid best result.

Current new record key: `Stagebound.Stage.south-loop.1.v1.style2`.
Legacy raw key: `Stagebound.Best.Assets/Scenes/SampleScene.unity.1.v1`.
The old record is read as a zero-credit baseline when no style record exists;
it is not deleted or overwritten by normal completion. No user records were
reset during implementation. `Stagebound > Reset Best Time` explicitly resets
both relevant record keys, leaving audio settings and HUD positions intact.

For future routes, create another StageDefinition with a unique stable ID and
assign it to that route's RaceTimer. Legacy import defaults off to avoid mixing
records when multiple routes share a scene. Increment the record version when
route/scoring changes should have separate records. RunResult exposes the
adjusted time for a future event aggregate; no championship is implemented.

## HUD and controls

The new compact Drift HUD sits on the right, away from the existing timing and
vehicle panels. It shows angle, speed, multiplier and active points prominently
during a combo; after banking it shows a two-second bank message then fades its
drift details. Style credit stays visible. It uses the existing dark/mint/TMP
style, transparent background, text outline and .65 scale.

The existing F2 edit mode also supports the third panel, including hover, overlay
pointer dragging, screen clamping and position persistence at
`Stagebound.HUD.Drift.v1`. Shift+F2 resets all panel positions, and T changes all
panel backgrounds. Existing timing/vehicle layout and preference keys are intact.
Enable `DriftScoreSystem.showDebug` on CarRoot for the optional live overlay.
It includes angle, drift status/direction, speed/forward speed/yaw, all rear slips,
each multiplier, active/banked points, credit and area-budget status.

Normal respawn banks/ends the active combo and clears telemetry continuity, but
preserves the running timer, checkpoints, stage score and anti-farming budgets.
Restart Stage resets all current run and result state and retains saved bests.
Pause prevents scoring and raw timing from advancing.

## Inspector tuning and wiring

- **CarRoot / DriftTelemetry:** minimum total/forward speed, angle range,
  sideways/forward rear slip thresholds, both-rear-grounded requirement.
- **CarRoot / DriftScoreSystem:** RaceTimer reference, base rate, angle/speed
  curves, speed cap, combo cap/growth, transition grace, qualifying direction
  duration, transition bonus, spin angle/grace, maximum yaw rate, cell size/time
  budget, debug toggle.
- **Canvas / RaceTimer:** StageDefinition and DriftScoreSystem references.
- **SouthLoop.asset:** stage ID/name, record version, legacy import,
  points-to-credit conversion, credit cap.
- **RaceHUD:** existing five-second result duration remains configurable.

SampleScene is fully wired. No manual Inspector assignments or new media assets
are required. The new panel and optional debug display are constructed at runtime.

## Files

Created: `DriftTelemetry.cs`, `DriftScoreSystem.cs`, `DriftHUD.cs`,
`StageDefinition.cs`, `Assets/Stages/SouthLoop.asset`, their Unity metadata,
the Stages folder metadata, and this document.

Modified: `RaceTimer.cs`, `RaceHUD.cs`, `RespawnManager.cs`,
`Assets/Editor/RaceRecordsMenu.cs`, and `Assets/Scenes/SampleScene.unity`.
Only the new component list, scoring components and timer references were changed
in the scene; no handling settings, checkpoint positions or vehicle models changed.

## Validation

Native Unity 6.6 compilation uses warnings as errors. Play Mode validation covers
actual rear WheelCollider ground contacts, deterministic velocity/slip samples,
invalid/low-speed/reverse/airborne cases, telemetry statistics, angle/speed reward,
combo grace/transitions/spin banking, spatial exhaustion/recovery, multiplier and
credit caps, pause and respawn, the 8420-point example, independent consecutive
runs, record reloads, best comparison, five-second result expiry, restart and zero
adjusted-time records. User PlayerPrefs are preserved; tests write isolated keys.
Full test output is retained under ignored `Library/DriftStyleValidation`.
Scoring balance still needs player testing with the current handling baseline.
