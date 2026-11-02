# Battle loading transition

The authored page is `Assets/Prefab/Home/BattleLoadingPage.prefab`, instantiated below `BattleLoadingController` in `Assets/Scenes/BattleLoading.unity`. Use `BattleLoadingUIBuilder.Apply()` outside Play mode to rebuild it. The existing loading scene/controller GUIDs are preserved.

The page reuses the three Home chapter illustrations, equipped hero portrait, logo, common cream panels, progress sprites and the existing `msyh SDF` font/material. No new raster art, font, atlas, shader or external service is introduced. The character has a small idle bob, and the visible gold progress fill has a clipped moving highlight. Tips rotate while waiting. `BattleLoadingVeilFade` fades the shared opaque header strip into the map using vertex alpha.

The 1080×1920 design fits inside the device safe area, reserving space above it for the WeChat capsule when present. The background independently covers the whole screen with its aspect ratio preserved.

Progress uses continuous weighted phases, with a separate smooth visual value:

| Phase | Overall range |
| --- | --- |
| Existing package preparation callback | 0–28% |
| Table/font setup | 28–36% |
| Battle audio ready | 36–46% |
| `LoadSceneAsync` progress through 0.9 | 46–94% |
| Game activation and map/player ready | 94–100% |

The percentage represents this preparation sequence, not downloaded bytes. The existing `WeChatSubpackagePlaceholder` remains unchanged: Editor simulates its package step and WebGL relies on the existing runtime-managed package setup. Unknown audio progress remains at its current phase with an animated highlight; it does not fabricate incremental downloads. A retry explicitly resets a new attempt; phase changes within an attempt cannot move backward.

The controller and page persist across scene activation. The new Game UIManager pause lock holds gameplay while the page covers the first frames. Map and player are checked before reporting 100%; the page then fades out over 0.22 seconds and releases only its own pause lock. Input and the first-player tutorial wait for the page to finish. The controller and page are destroyed after the reveal. The background content check is hosted by the persistent AudioService so it can outlive the page.

The existing scene-entry confirmation and selected-level context remain in place. Rapid repeated Home start clicks share one launch. A missing destination or unavailable map keeps a readable error and retry button on the page. The loading scene creates an EventSystem when no Home system is present; it unloads with that scene, leaving Game's own system in control.

Run `HomeUIEditorPreview.Begin("battle-loading")` for an isolated in-memory-profile validation, then `HomeUIEditorPreview.Finish()` to restore the prior scene setup. Output is `ArtOutput/UI/BattleLoading/`: chapter/screen screenshots, retry state, the actual Game after reveal, `handoff-samples.csv` and the validation report. Checks include Home entry, duplicate-click protection, missing-scene retry, direct new-player entry, monotonic progress, opaque coverage during activation, gameplay pause/input protection and complete cleanup after fading. Device/network performance and build size require a fresh WeChat build; no such build is part of this change.

2026-10-31: all 30 focused Editor checks passed after the final visual update.
