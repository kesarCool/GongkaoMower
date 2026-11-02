# Login UI integration

Implemented the approved original-poster design in `Assets/Scenes/Login.unity`.

- Scene GUID and `LoginSceneController` button/version references are preserved.
- Login now routes all players to `Home`. The retired new-player quick-start branch and its serialized scene fields were removed; new players continue through the Home tutorial before entering `BattleLoading`.
- New art is under `Assets/Res/image/Home/login/`: `login_background.jpg`, `game_title.png`, `bottom_veil.png`.
- Start button normal/pressed/disabled sprites, swords icon, `msyh SDF` font and Home text material are reused. No new font or SpriteAtlas was introduced.
- The background covers the viewport proportionally. The top and bottom controls fit the safe area; short viewports scale those controls proportionally.
- The platform capsule is supplied by WeChat. The design-preview capsule was not added to Unity.
- The existing runtime version prefix is retained: `版本号：v` followed by `Application.version`.
- WeChat `MiniGameConfig.asset` now points its startup cover to the same new background. The startup cover has no interactive buttons; the title and button appear when the Unity login scene loads.

## Layout at 1080 × 1920

Coordinates use the upper-left origin, with no safe-area inset.

| Element | Rectangle / style |
| --- | --- |
| Background | Full bleed, proportional cover |
| Game title | x20, y212, 1040 × 250, transparent PNG |
| Version | x48, y86, 510 × 44, 27px, #76634C |
| Start button | x200, y1580, 680 × 136 |
| Button icon | Button-local x56, y32, 72 × 72 |
| Button label | 56px bold, #493627 |
| Health notice | Five rows, 29px spacing, 25px heading / 24px body, #76634C |

The version rectangle is wider than the design preview to retain the existing runtime prefix.

## Validation

`ArtOutput/UI/LoginUnity/validation-report.json` records checks using the real Login scene in the Unity Editor:

- 1080 × 1920, 1080 × 2400, 1080 × 1440 screenshots.
- Proportional background cover and foreground bounds for all three sizes.
- Simulated long-screen safe area with a 132px top inset and 72px bottom inset.
- Runtime version, assigned artwork, existing font, interactive start button, retained navigation configuration.
- Actual unmodified start-button callbacks load Home with both fresh and returning in-memory profiles.
- A fresh profile sees the new Home tutorial instead of automatically entering battle. No real save key is deleted to simulate a new player.
- An in-memory profile fixture protects player progress; persistent save data was unchanged and no runtime errors were reported.

Screenshots are actual Unity captures, not design renders. No WeChat device run or fresh mini-game build was performed for this integration.

## Maintenance and rollback

- `LoginUIBuilder.Apply()` / menu `工具/Home UI/Apply Login` reapplies this layout to the existing scene. Save other edited scenes and exit play mode first.
- `工具/Home UI/Preview Login` runs isolated checks and restores the prior scene/Game View setup afterward.
- `LoginScreenLayout` handles viewport and safe-area changes at runtime.
- The prior Login scene, scene meta and SDK configuration were copied to `Library/UIBackups/Login-20261024/` before modification.
- Old `Assets/Res/image/login2.png` is retained, not deleted. It is no longer referenced by the Login scene or the startup-cover configuration. Existing build output was not edited. On the next clean build, check the generated `images/` folder for stale old cover files before evaluating package size.
- After the compression pass, the background is 720 × 1280. The title keeps its original pixels with one transparent row added to both the top and bottom (1040 × 252), allowing block compression. The three source images total 259,547 bytes (about 253 KiB); this is disk-encoded source size, not a measured Unity/Brotli package increment.
- `Tools/UI/compress-login-assets.cjs` recreates the optimized source images from the preserved approved art. WebGL compression settings are serialized in the image `.meta` files; the UI builder preserves those settings.

## Compression validation (2026-10-24)

| Asset | Previous imported storage | Optimized imported storage | WebGL format |
| --- | ---: | ---: | --- |
| Background | 1,036,800 bytes | 146,557 bytes | DXT1Crunched |
| Title | 1,040,000 bytes | 31,378 bytes | DXT5Crunched |
| Total | 2,076,800 bytes | 177,935 bytes | 91.43% reduction |

Measurements use Unity's `TextureUtil.GetStorageMemorySizeLong`, not screenshot file size or a fresh mini-game package. GPU memory and Brotli output are different measurements; no claim is made that the complete package shrinks by the raw difference above. The previously uncompressed RGBA32 title now uses alpha-capable block compression after its dimensions were aligned to four pixels.

The actual Login scene was reloaded after texture import; all 20 validation checks passed again, and the new 1080 × 1920 screenshot was visually checked. Captures and before/after measurements are in `ArtOutput/UI/LoginUnity/compression/`. Original source art remains in `ArtOutput/UI/LoginOriginalPosterV1/`, and the pre-compression runtime images/import settings are backed up in `Library/UIBackups/LoginCompression-20261024/`.
