# Chapter two: autumn academy battle maps

Chapter two's eight battle maps now use autumn academy scenery to match the existing Home illustration: red maples, golden ginkgo, blue-gray academy gates, stone study tables, autumn shrubs and sparse fallen leaves. Updated 2026-10-27: ground now uses a separate 64px autumn sandstone/earth palette, described in `Doc/autumn-academy-terrain.md`. All eight distinct ground layouts are retained, with 30×30 cells and the central 5×5 spawning area clear. Scenery is visual decoration and adds no internal movement blockers.

Active resources remain `Assets/Resources/Map/TileMap2_1.prefab` through `TileMap2_8.prefab`. Existing table paths keep working; no level IDs, combat rules or fallback logic change. All new props, including leaf decals, are under `Decoration`, so the existing pause-menu decoration switch controls them.

Updated 2026-10-27: the original bamboo maps are `Assets/Resources/Map/ZlMap1.prefab` through `ZlMap8.prefab`. Asset GUIDs and map contents are preserved, apart from the renamed root. These reusable maps now participate in builds. Shared bamboo/terrain art lives in `Assets/Res/Map/Styles/SharedCartoon/`; original preview renders are retained. See the [map production manual](../Tools/Skills/word-mower-maps/references/production-manual.md).

Decoration images are in `Assets/Res/Map/Styles/AutumnAcademy/Textures/`: six shared sprites at 128 or 256 pixels; PNG source total 120,380 bytes. They use explicit WebGL DXT5 Crunch compression, with Read/Write and mipmaps disabled. The separate ground set is in `Assets/Res/Map/Styles/AutumnAcademy/Terrain/`. No runtime scripts, fonts, atlases or per-level image copies are added. Imported texture storage is reported by the Unity generators; final WeChat package size still requires a new build.

The artwork was made with the built-in imagegen tool, using the current Home autumn academy and existing battle sprites as references. The exact prompt and full-resolution transparent atlas are retained under `ArtOutput/World/AutumnAcademy/source/`; they are outside Assets. `Tools/Maps/prepare-autumn-academy.cjs` mechanically crops and downsamples that atlas into the six game sprites.

## One-click generation

- `Tools > Maps > Chapter Two > Generate Autumn Academy (2-1 to 2-8)` rebuilds the active autumn maps and renders previews. Existing active maps are backed up under `Library/MapBackups/AutumnAcademy-<timestamp>/` first.
- `Tools > Maps > Chapter Two > Apply Autumn Terrain Only` updates only the existing Ground palette while preserving decoration edits.
- `Tools > Maps > Map Library > Generate Bamboo Academy` rebuilds the preserved bamboo maps in the library, without replacing the autumn maps.
- `Tools/Maps/compose-autumn-academy.cjs` assembles full-map and phone-view contact sheets from actual Unity camera renders.

Map labels: 枫庭迎门、银杏斜阶、秋影回庭、丹枫寻章、落叶书径、双庭听秋、书山秋阶、文昌秋试.

## Verification outputs

`ArtOutput/World/AutumnAcademy/` contains actual map renders, `overview.png`, `phone-overview.png`, ground blueprints, the asset preparation record, bamboo byte/GUID preservation results and `build-report.json`. The generator verifies all 900 ground cells, distinct connected stone silhouettes, references, Resources loading, Map layers, four perimeter walls, clear spawning area, decoration bounds, absence of bamboo, unchanged first-chapter maps and unchanged user scene state.

Original code and map backups from before this task are in `Library/MapBackups/AutumnAcademy-20261026/`.

Initial scenery generation and checks passed for all eight maps. Imported decoration texture storage totals 93,894 bytes (about 92 KiB). The later independent ground palette replaces shared terrain dependencies; common stone lamps, benches, signs and rocks are still reused. All eight archived bamboo prefabs and their GUIDs match their pre-task originals byte for byte. No other project assets reference those archived prefab GUIDs.
