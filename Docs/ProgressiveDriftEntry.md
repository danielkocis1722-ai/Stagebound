# Progressive drift entry

The previous recovery pass deliberately left stable slides alone, but its shared angle-based spin support starts at 33 degrees. Rapid outward rotation could build before reaching that gate. This pass adds an earlier rate-based path inside the existing DriftYawStability component.

Entry support requires grounded/sliding tires, forward travel, outward yaw and increasing slip magnitude. It ramps over 6–14 degrees of slip, 15–55 degrees/second of slip opening and 30–85 degrees/second of yaw. Maximum entry damping is 2.2/second; it fades out over 45–60 degrees as the existing spin recovery takes over. Entry and recovery use the larger damping request, never their sum.

There is no target drift angle or automatic steering. Stable slides and closing angles receive zero entry support. It removes only some outward yaw velocity, leaving linear velocity, pitch/roll, steering, suspension, tire grip and drivetrain untouched. Existing slow rear-grip recovery remains unchanged.

Inspector controls are on CarRoot's existing DriftYawStability component under Progressive drift entry. Entry Damping Rate = 0 disables this addition for comparison. No scene references or new components are required; existing components receive the new serialized-field defaults.

Only Assets/Scripts/DriftYawStability.cs and this note are changed by this pass. Validation artifacts stay under ignored Library/DriftEntryValidation.

Matched isolated WheelCollider tests compare entry support disabled/enabled at 50, 60, 70 and 80 km/h, second gear, 70% throttle. The entry fixture seeds 8 degrees of slip, rear-wheel spin and handbrake grip memory, holds turn-in briefly, then uses the same diagnostic steering policy in both runs. At 60 km/h, peak yaw falls from 148.45 to 79.28 degrees/second, and a spin becomes a peak angle of 34.31 degrees. At 70 km/h the spin becomes a large but recoverable 58.78-degree excursion. The aggressive 80 km/h entry still spins, although the established-slide 80 km/h fixture changes from spinning to a 38.47-degree peak. These are comparative test-driver results, not a guarantee that every entry will hold the desired angle.

In the 60 km/h established-slide fixture, time above 20 degrees remains approximately 3.4 seconds. No base grip or rear recovery settings were changed to obtain the result.

Runtime/editor compilation passed with warnings treated as errors. Isolated native Play Mode checks passed for stable 20/30/35/40-degree slides receiving no damping, early support during rapid opening below 25 degrees, no support while closing, independent disable, and the existing suspension/handbrake/recovery checks. CarController and SampleScene hashes are unchanged from the start of this pass. No commit or push was made.
