# Boss arena: single stone tile V3

Retired 2026-10-31: the active arena now uses the existing shared `barrel.png`. The unused stone texture was deleted, and the wall prefab was renamed to `boss_arena_barrel.prefab` with its GUID preserved. Use `BossArenaBarrelBuilder.Apply()` / `Tools/Battle Art/Apply Boss Arena Barrels` for the current setup. See `Doc/boss-arena-barrel.md`. The sections below record the historical stone version.

Implemented 2026-10-23 in the existing branch. Uses one cool gray stone image repeated along the arena perimeter.

## Assets and integration

- Texture: `Assets/Res/image/Game/arena/boss_stone_tile_v3.png`, 64×64 RGBA, source PNG 9,569 bytes (9.35 KiB).
- Import: single Sprite, FullRect, center pivot, 128 PPU, alpha, bilinear, clamp, no mipmaps, no CPU readback; default platform max size 64.
- Prefab: `Assets/Prefab/Battle/boss_arena_stone_v3.prefab`.
- `Assets/Prefab/Battle/GameLayer.prefab` → BossArenaLock.wallPrefab now points to the V3 prefab.
- Repeat pitch: 0.5 world units. A 16×20 arena uses 144 complete stone quads. Horizontal walls own the four corners; vertical walls skip the corner endpoints.
- Four merged meshes render all stones. No per-stone GameObjects, animation, materials or colliders. The original four BoxCollider2D / static Rigidbody2D / WallMarker roots and boss event path remain in use.
- Existing directional wall mode and old V1/V2 assets are retained. Old assets were not deleted; this is not a measured reduction in the final WeChat build.

## Reproduce

Mechanical export: `Tools/VFX/prepare-stone-tile-v3.cjs`, using the generated source and recorded prompt in `ArtOutput/World/BossArenaStoneV3/source/`.

Unity menu: `Tools/Battle Art/Apply Single Stone Tile V3` via `Assets/Editor/BossArenaStoneV3Builder.cs`. Exit Play mode before applying. The builder imports/configures the Sprite and writes the V3 prefab/reference.

Preview validation: set `BossWallBaseline=false`, `BossWallPreviewPrefix=BossStoneV3` in Unity SessionState, then run `HomeUIEditorPreview.Begin("boss-arena")`. Finish with `HomeUIEditorPreview.Finish()` and erase the preview prefix afterward. The preview uses in-memory profile storage and restores the original scene setup.

## Verified

Unity 2021.3.22f1c1 compilation succeeded. Actual Game scene validation passed **21/21** checks, including:

- Normal enemy damage does not lock; real boss damage creates four walls and retains original boss repositioning.
- Original 0.4-unit collision thickness, static blockers and physics queries are preserved.
- Four meshes share the same tile/material; all 144 quads retain full sprite size and UVs.
- Each corner has exactly one full stone; no quad overlaps; repeated Configure adds nothing.
- Walls remain fixed when the player moves. Boss death removes the walls; later locks rebuild them; disabling cleans them.
- Profile data is preserved and no runtime error occurs during the validation interval.

Proof and the full JSON report: `ArtOutput/World/BossArenaStoneV3/`. The overview screenshot uses a temporary zoom-out to show the perimeter; the gameplay screenshot uses the original camera zoom. Visual review confirmed corners and cool gray stone contrast against the current map.

After the preview, the editor was restored to `Assets/Scenes/Boot.unity`, not playing and with no dirty scene. WeChat device performance and final build size were not tested in this task.

Task backup directory: `Library/UIBackups/StoneTileV3-20261023-015512`. The earlier pre-V3 component is also preserved under `Library/UIBackups/FenceV2-20261022-002225/Assets/Script/Game/Battle/BossArenaWallVisual.cs`; prefer it if reverting the component. Revert only the intended prefab reference/component because the branch contains other ongoing work.
