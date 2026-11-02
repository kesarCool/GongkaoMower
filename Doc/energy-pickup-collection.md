# Energy pickup collection

`Assets/Script/Game/Combat/Pickups/EnergyPickup.cs` now uses scalar closing speed and a clamped `Vector2.MoveTowards` step instead of retaining a velocity vector. It runs in `LateUpdate`, after player movement. Pickup Z is preserved; attraction and collection use XY only.

While latched, each step includes the target's movement distance since the previous step plus the configured closing distance. By the triangle inequality, the remaining gap cannot grow merely because the player moves faster or changes direction. The final step clamps to the target and collects in the same frame. The periodic collector retains its existing interval and radius and still uses `ForceCollectBy`.

Touching a player collider collects immediately. The old outward bounce is removed, including its unused prefab settings. Child colliders resolve their owning `PlayerEnergy`. Each pool lease is guarded against duplicate collection; the pickup unregisters and returns to the pool before notifying energy/level-up subscribers. Pool reuse clears the target, timers, speed and collection guard.

Resting drops retain the existing lifetime and ground delay. Once attraction begins, a lifetime expiry no longer deletes an almost-collected pickup. Paused updates make no movement or progress. Existing orb artwork, drop value, suction radius, periodic collector settings and energy thresholds are unchanged. No textures, fonts, materials or runtime dependencies are added.

`HomeUIEditorPreview.Begin("energy-pickup")` runs the real battle scene with an in-memory profile. `EnergyPickupValidationRunner` uses the existing energy prefab and pool. Checks cover 15/30/60 fps, stationary and moving targets, rapid reversals, speeds up to 40 units/s, different Z values, paused/large frame steps, lifetime boundaries, duplicate child-collider callbacks, pool reuse, spawn-limiter accounting, real physics contact and an actual moving Rigidbody. Results are in `ArtOutput/UI/EnergyPickup/validation-report.json`. Finish with `HomeUIEditorPreview.Finish()` to restore the prior scene setup.

2026-10-31: 30 focused Editor checks passed. No WeChat-device frame timing or package build was performed for this change.
