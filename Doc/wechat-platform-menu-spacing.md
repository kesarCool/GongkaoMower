# WeChat capsule spacing

The Home header and battle HUD now reserve space for the actual WeChat top-right menu capsule. Previously, those layouts used only `Screen.safeArea`, which does not describe the capsule rectangle.

## Geometry source

`PlatformMenuExclusion` reads the installed WeChat SDK's `WX.GetMenuButtonBoundingClientRect()` and `WX.GetWindowInfo()` in non-Editor WebGL/WeChat builds. SDK bounds use top-left CSS/window coordinates and are normalized by `windowWidth`/`windowHeight` before conversion to Unity canvas units. This avoids confusing CSS pixels with the Unity render resolution.

The result is cached and refreshed once per second, including while gameplay is paused. Resolution changes invalidate the cache. A conservative upper-right rectangle is used while the SDK/window values are unavailable; the actual bounds replace it after a successful query.

Editor and ordinary desktop builds keep their existing layout unless the Editor-only capsule override is set for validation. The capsule shown in validation captures is a simulated overlay and is not added to game prefabs.

## Layout changes

- Home: the shared header grows only as much as needed to put the content area and chapter badge below the capsule, with a 24-design-unit gap. The three header controls can shrink together if the capsule is unusually wide. Header background coverage follows the adjusted height.
- Battle: the pause button is positioned below the capsule with the same gap. The wave/kills/energy rows move by the matching amount. The timer shifts left if necessary, and the level label yields the required horizontal space.
- The battle top gradient continues from the original safe top edge after the HUD moves; it does not leave a new strip above the HUD. Its opacity is unchanged.
- Minimap placement, pause actions and the existing page/button bindings are retained.

All changes are runtime layout code. There are no new production art assets or replacement image files, and existing scene/prefab geometry remains the baseline.

## Validation

`HomeUIEditorPreview.Begin("platform-menu")` runs isolated validation with the actual Home and Game scenes and an in-memory player profile.

It checks 1080 × 1920, 1080 × 2400 and 360 × 640 viewports with both a standard capsule (87 × 32 CSS pixels) and a wider/lower capsule (112 × 40 CSS pixels). Checks cover visible clickable entrances, currency/header overlap, battle information rows, pause raycast/click behavior, desktop baseline restoration and persistent-save preservation.

Reports and screenshots are under `ArtOutput/UI/PlatformMenuSpacing/`. This is a simulated geometry check, not a WeChat device run. A fresh mini-game build is needed to validate the native SDK bounds on the phone.

The final run passed 79 checks with no runtime errors. The real pause-button callback opened the existing pause page, and the persistent player save was preserved. The simulated capsule is cleared when validation ends.

The non-Editor WebGL/WeChat code path in `PlatformMenuExclusion.cs` is also compiled separately against the installed SDK signatures. That check is not a full WebGL or WeChat build; the SDK reference assemblies produce existing netstandard version-unification warnings in this isolated compilation.

Before changes, the modified existing layout/preview scripts were copied to `Library/UIBackups/PlatformMenuSpacing-20261025/`.
