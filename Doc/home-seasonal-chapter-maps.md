# Seasonal Home chapter maps

The approved `HomeChaptersV3` autumn academy and winter riverside town illustrations are used by chapters 2 and 3 in Home. Chapter 1 keeps its existing food map.

## Runtime integration

- `HomeChapterMapView` selects the serialized chapter theme when the selected level changes. Each theme supplies its background and route coordinates.
- The node positions, selected hero and scroll-to-selection calculation use the same theme coordinates. Coordinates stay in the 1080 × 1920 design space even though source images are downsampled.
- The existing `RoadmapView.prefab` carries all three themes; Home scene instances inherit that configuration.
- Progress, stars, replay selection and locked-level checks continue to use the current profile and level table.
- The current table has 8 food levels, 8 idiom levels and 5 third-chapter levels. The third chapter shows its five actual nodes and a `/ 5` denominator; remaining illustration waypoints are reserved for future table entries.
- Chapter titles read `ChapterLevel.mapName`: `烟火食集`, `秋日书院`, and `滨水古镇`. The selected-stage footer reads `ChapterLevel.stageName`, such as `1-1·田园食集`. Level selection, entry confirmation, battle HUD, and results share the same stage-name formatter, with a numeric level label when the stage name is empty.
- Planned landmarks are shown only on the first chapter, as before.

## Resource preparation

- `Assets/Res/image/Home/roadmap/map_autumn_academy_720x1280.jpg`
- `Assets/Res/image/Home/roadmap/map_winter_town_720x1280.jpg`

Only the optimized runtime images are imported. Approved high-resolution designs stay in `ArtOutput/UI/HomeChaptersV3/` outside Assets.

`Tools/UI/prepare-home-chapter-maps.cjs` downsamples the approved designs to 720 × 1280 JPEG at quality 80. `HomeChapterMapsUIBuilder.Apply()` imports the two sprites and applies explicit WebGL DXT1 Crunch compression at quality 70, with mipmaps and Read/Write disabled. Dimensions are divisible by four and alpha is disabled for the opaque backgrounds. No new atlas or font is introduced.

Chapter coordinates and asset paths are recorded in `Tools/UI/home-chapter-map-assets.json`. The builder can be reapplied through `工具/Home UI/Apply Seasonal Chapter Maps` after saving edited scenes and exiting play mode.

## Validation and backups

Use `工具/Home UI/Preview Seasonal Chapter Maps` for isolated checks with an in-memory player profile. It exercises chapter progress, theme changes, replay/locate behavior, hero/node positioning, hidden extra nodes, table-derived progress counts and multiple screen sizes.

Actual Unity captures, validation and texture size measurements are written to `ArtOutput/UI/HomeChapterMapsUnity/`. These measurements do not replace a fresh WeChat build or a device test.

The integration passed 51 checks. Actual Unity texture storage is 165,198 bytes for the autumn map and 160,181 bytes for the winter map, totaling 325,379 bytes (about 318 KiB). JPEG source files total 585,268 bytes (about 572 KiB). Only the two optimized images were added to Assets; no high-resolution duplicates were imported.

`Tools/UI/render-home-chapter-comparison.cjs` combines the actual chapter screenshots into `home-seasonal-comparison.png` for review. The Home scene file itself is unchanged; its existing roadmap prefab instance inherits the new theme references.

Before this integration, the roadmap prefab, Home scene and modified existing scripts were copied to `Library/UIBackups/HomeChapterMaps-20261024/`.
