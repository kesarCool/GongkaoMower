# Independent autumn ground palette

Updated 2026-10-27. Chapter two now has its own ground palette, replacing the shared green-gray square pavement and green lawn with staggered warm sandstone paving and red-brown autumn earth. The approved decoration objects and all eight existing silhouettes are preserved.

## Assets and generation

- Active maps: `Assets/Resources/Map/TileMap2_1.prefab` through `TileMap2_8.prefab`; their GUIDs are unchanged.
- New terrain sprites: `Assets/Res/Map/Styles/AutumnAcademy/Terrain/Textures/`.
- New Tile assets: `Assets/Res/Map/Styles/AutumnAcademy/Terrain/Tiles/`.
- Editor source materials: `Assets/Res/Map/Styles/AutumnAcademy/Terrain/Source/` (not runtime map dependencies).
- Original shared palette: `Assets/Res/Map/Styles/SharedCartoon/`, moved with GUIDs preserved.
- Preserved bamboo maps: `Assets/Resources/Map/ZlMap1.prefab` through `ZlMap8.prefab`, moved/renamed with GUIDs preserved. They now participate in builds.

Canonical workflow and directory conventions: [map production manual](../Tools/Skills/word-mower-maps/references/production-manual.md).

The two base materials were created with the built-in imagegen tool. Full-resolution originals and the exact prompts are saved outside Assets in `ArtOutput/World/AutumnAcademyTerrain/source/`, including `generation-prompts.json`. `Tools/Maps/prepare-autumn-terrain.cjs` crops and downsamples them to periodic 128px source macros. Each macro supplies four adjoining 64px quadrants, so material phase remains continuous across tile boundaries.

`AutumnAcademyTerrainBuilder` creates the needed interiors, straight edges, outside corners and inside corners with deterministic eight-neighbor masks. The current eight maps use **56** sprites and Tile assets. All ground sprites use **64 pixels per unit**, so each still covers exactly one existing grid cell. Explicit WebGL DXT1 Crunch compression is enabled; mipmaps and Read/Write are disabled. No new runtime script, font, material or SpriteAtlas is introduced.

## Editor commands

- `Tools > Maps > Chapter Two > Apply Autumn Terrain Only`: updates only Ground in existing chapter-two maps and keeps their decoration edits.
- `Tools > Maps > Chapter Two > Generate Autumn Academy (2-1 to 2-8)`: rebuilds all map contents using the new ground palette. As before, full regeneration rewrites decoration layout; use the terrain-only command to preserve manual scenery changes.
- `Tools > Maps > Map Library > Generate Bamboo Academy`: generates `Resources/Map/ZlMap1..8` with the original shared palette, protecting active autumn maps.

Both terrain-only application and full generation back up their target prefabs under `Library/MapBackups/`. The original files from this change are in `Library/MapBackups/AutumnTerrain-20261027/`.

## Verification

`ArtOutput/World/AutumnAcademyTerrain/` contains:

- actual Unity full-map and 9:16 phone renders, plus `overview.png` and `phone-overview.png`;
- `before-after.png`, comparing the original and updated 2-3 ground;
- `tile-palette.png`, showing all 56 produced sprites;
- `build-report.json`, verifying all eight maps load, have 900 cells, retain their shape/GUID/decoration, use only the new ground palette, have matching neighboring edge masks and do not depend on the editor source macros;
- `preservation-check.json`, verifying **only the Tilemap YAML component changed** in each prefab. Every other serialized document and prefab meta is unchanged. All 239 original shared-art files, 16 bamboo prefab/meta files and 16 first-chapter prefab/meta files match their pre-task hashes;
- `runtime-dependency-check.json`, confirming no old shared ground Tile assets remain as dependencies.

The 56 PNG source images total **485,728 bytes**. Their measured imported texture storage totals **81,072 bytes (about 79 KiB)**. This is not a measured final WeChat package delta; no new WeChat build was run.

Preview assembly: `node Tools/Maps/compose-autumn-academy.cjs G:/Demo/GongkaoMower/ArtOutput/World/AutumnAcademyTerrain`, followed by `node Tools/Maps/compose-autumn-terrain-proof.cjs`. Preservation can be rechecked with `node Tools/Maps/verify-autumn-terrain.cjs`.
