# Home roadmap UI update

Implemented independently from the character page on the existing branch.

## Entry points

- Scene: `Assets/Scenes/Home.unity`
- Existing view: `Assets/Prefab/Home/RoadmapView.prefab`
- New node: `Assets/Prefab/Home/FoodMapNode.prefab`
- Shared navigation: `Assets/Prefab/Home/HomeTabBar.prefab`
- Reapply menu: `Tools` / `Home UI` / `Apply Roadmap` (localized menu prefix in Unity)
- Automated preview: `HomeUIEditorPreview.Begin("home")`; finish with `HomeUIEditorPreview.Finish()`.

## Resources

- Chapter art and node states: `Assets/Res/image/Home/roadmap/`
- Shared buttons, icons, navigation, gradient, font material: `Assets/Res/image/common/`
- Import manifest: `Tools/UI/home-assets.json`
- Approved source: the existing food chapter delivery under the art delivery directory.

Existing navigation textures were moved through AssetDatabase to preserve their GUIDs. Old unused resources were retained. Existing prefab and scene GUIDs were retained. No SpriteAtlas was added.

## Behavior

The food chapter uses eight nodes aligned with the illustrated road. Scroll position follows selected progress. Cleared, available, locked, selected and star states come from the existing profile. Clicking a node selects it; Start enters the selected unlocked level, including replay. Locate returns to latest progress.

The existing full level selector, vocabulary, exchange, character, achievement, settings and backpack entry points remain. The idiom chapter retains its data and selector access, temporarily using the same illustration because no separate chapter art was approved. Planning landmarks are visible only in the food chapter and do not open unfinished gameplay.

The map preserves aspect ratio and covers taller viewports. The Home shell applies Screen.safeArea once. The real WeChat system capsule is not drawn as game UI.

## Verification

Results: `ArtOutput/UI/HomeRoadmap/validation-report.json`; real Unity renders in the same directory. Coverage includes eight nodes, profile stars, selection, replay resolution, locked levels, new account, final food stage, idiom access, original modal entries and multiple screen ratios. The preview uses an in-memory ISaveStorage and checks the persistent profile is unchanged.

WeChat device safe areas, touch scrolling and final WebGL package size require a device/build check. There was no package rebuild in this UI update.

Original working files were copied before editing to `Library/UIBackups/HomeAndCharacter-20261018-164812`. This folder is a local rollback snapshot, not a distributable game asset.
