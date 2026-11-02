# Home navigation entrances

- The entire existing `IllustratedMap/ChapterBadge` (chapter title and progress count) is a Button. `HomeHubController.openLevelSelectButton` points to it. It keeps the cream artwork and has an 80 ms warm pressed tint.
- The `LocateCurrent` shortcut is removed from `RoadmapView.prefab`; the separate `homeButton/ButtonChapterLevel` shortcut is inactive. The internal `HomeChapterMapView.LocateCurrent()` API remains available.
- `homeButton/ButtonBackpack` stays in the existing button group, with anchor `(1, 0.24)`, pivot `(1, 1)`, and position `(-40, 0)`. It still opens `BackPackPanel`.
- `LevelSelectLoopScrollDriver` cancels old scroll animation and velocity, then fills the pooled list at the latest unlocked stage, keeping one preceding row when possible. If the final row is visible, it refills from the end to settle the bottom bound before the opening animation. It does not animate through previous chapters.
- Opening the selector still uses progression (latest unlocked stage), even when replaying an older stage. Unlock checks and entry confirmation are unchanged.

Apply only the entrance changes with `HomeRoadmapUIBuilder.ApplyNavigation()` outside Play mode. The full roadmap builder includes the same layout. Prefab and scene GUIDs remain unchanged; no textures or fonts are added.

Run `HomeUIEditorPreview.Begin("home-navigation")` for the focused Editor check using an in-memory profile. Output: `ArtOutput/UI/HomeNavigation/`. It checks full-badge hit testing, pressed feedback, backpack placement at 1080×1920/2400/1440, simulated WeChat menu exclusion, first-frame stage visibility, position stability and pooled-panel reopening at early and late progress. Call `HomeUIEditorPreview.Finish()` afterward to restore the prior scene setup.
