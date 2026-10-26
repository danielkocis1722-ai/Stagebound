# Vehicle audiovisual feedback

The saved `Assets/Scenes/SampleScene.unity` has four new components on **CarRoot**.
They only read vehicle state. No suspension, friction, forces, steering, transmission,
race, HUD, respawn or pause code is changed.

## Already wired

- **SkidmarkSystem** uses the supplied `Skidmarks.mat`. A fixed-size mesh ring buffer
  holds 2048 segments across all wheels; old segments are overwritten rather than
  growing the scene. Marks live in world space and never follow the car. Contact
  gaps and long jumps break trails so respawning does not draw a line across the map.
- **WheelEffects** has all four WheelColliders assigned from CarController. It reads
  grounded contact points plus absolute sideways/forward slip once per physics step.
  Mark opacity rises smoothly from slip 0.20 to 0.85, above 5 km/h road speed.
  Separate smoke thresholds default to 0.30–1.00. Wheel surface speed also contributes
  to smoke so stationary wheelspin can smoke while very slow driving does not mark.
- One starter **Tire Smoke** child ParticleSystem is created per wheel in Play Mode.
  These use world simulation, a generated soft radial texture, lifetime fade/expansion,
  and the supplied `TireSmoke.mat`. No smoke prefab or texture is required.
  Replace an individual wheel's Smoke reference with a custom ParticleSystem if desired.
  Its continuous emission is owned by WheelEffects; bursts and distance emission are
  disabled so the effect cannot smoke during normal grip driving.
- **VehicleAudio** creates looping Engine Layer 1 and Tire Screech child AudioSources.
  Doppler is off so RPM pitch is not distorted by the chase camera. Pitch and volume
  use frame-rate-independent smoothing. Engine layers have separate clips, gains and
  pitch multipliers; add array entries for future layers. Tire screech uses the strongest
  grounded wheel slip and its own thresholds/fade settings.
- Audio pauses/unpauses its own sources when the existing pause system pauses time.
  Smoke uses scaled time. No global AudioListener setting or pause-menu changes are made.

## Assets and references to supply

The current SampleScene already has its engine and screech clips assigned.
For replacing these recordings:

1. On **CarRoot > VehicleAudio > Engine Layers > Element 0 > Clip**, assign a seamless
   looping engine recording. Additional engine layers are optional. The source field
   may be left empty, or point to an existing dedicated AudioSource on the car.
2. Assign a seamless tire-screech clip to **VehicleAudio > Screech Clip**.
   Both systems remain silent if their clips are removed.
3. **BrakeLights is intentionally disabled in the current prototype.** The imported
   `lights` renderer spans nearly the full vehicle rather than a verified rear-only
   region, and a former material override referenced a missing asset. The normal body
   palette has been restored without emission. Do not assign this combined renderer
   or the shared Body/Body 1 material to brake effects.
   Separate only the rear lamp faces in Blender, export them as dedicated geometry,
   and give them their own URP/Lit material. Then assign **Dedicated Rear Light Material**
   to that material, and **Left Rear / Right Rear > Renderer**, and each light's
   **Material Index**. Use dedicated tail-light geometry/material slots, or materials with
   a proper emission mask. A shader with `_EmissionColor` is required (for example URP/Lit).
   The component rejects slots that do not match the explicit dedicated material.
   The AE86 importer currently remaps a single Body material; do not assign an unmasked
   whole-body slot unless you want the entire car to emit. Supply separate light meshes
   or an emission-masked material if the model has no separate light slots.

BrakeLights creates private copies of only the assigned material slots to enable emission,
then changes emission through per-slot MaterialPropertyBlocks. Shared imported materials
are never edited, and original materials/property overrides are restored on removal.
Normal/braking colors, intensities and response are configurable. Only BrakeInput controls
the lights; the handbrake alone does not light them.

## Tuning and verification

- Tune each effect's slip thresholds independently. A higher full-slip value gives a
  longer ramp from faint marks/smoke/screech to maximum intensity.
- Engine Min Pitch / Max Pitch default to 0.7 / 2.2. Match these to the recording's base RPM.
- Normal grip: no marks/smoke and silent screech. Cornering/drift: rear contact trails,
  smoke and a smoothly fading screech. Brake emission remains disabled until dedicated
  rear-light geometry/material is supplied; the rest of the car retains its normal palette.
- Test R recovery for no connecting skid line, and ESC pause for frozen smoke and paused
  audio. Old world-space marks are intentionally retained when the car respawns.
- Skidmarks are for static road surfaces; moving platforms would require marks attached
  to their surface. The starter shader targets this project's Universal Render Pipeline.
