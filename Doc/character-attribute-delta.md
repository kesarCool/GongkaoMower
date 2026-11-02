# Character upgrade delta display

The upgrade animation previously subtracted unrounded attribute values. Because hero growth is linear, the same raw increase (e.g. attack `+2.76`) appeared every level, even when the rounded panel values changed by 3 on one level and 2 on the next.

`CharacterGrowthValue.FormatDeltaFrom(before)` now rounds both endpoints with the same number format used by the panel, then subtracts them using decimal arithmetic. Integer stats, one-decimal speed/multipliers, and percentages retain their existing presentation. Examples: `43 -> 46` shows `+3`, `46 -> 48` shows `+2`, `6.0 -> 6.1` shows `+0.1`, and `30% -> 31%` shows `+1%`. Percent deltas represent percentage points. If both displayed values are equal, the row has no floating increment or green rolling animation.

The upgrade still commits the original full-precision stats and price. The animation keeps the operation's existing before/after snapshots, so the increment refers to the level just purchased. No attribute curves, assets, prefabs or save formats changed.

Regression entry: `HomeUIEditorPreview.Begin("attribute-delta")`. It checks numeric formatting and actual character upgrades from level 2 to 3 and 3 to 4, capturing both animations with an in-memory save. Results are under `ArtOutput/UI/CharacterAttributeDelta/`.

All 22 checks passed in Unity. Both real upgrades produced the expected row deltas, displayed-zero rows stayed quiet, final full-precision values settled correctly, no runtime errors were recorded during validation, and the persistent save was preserved.

Original source files are backed up in `Library/UIBackups/CharacterAttributeDelta-20261026/`.
