# Utility dialogs UI update

## Implemented

Existing prefab GUIDs are retained for:

- `Assets/Prefab/LevelSelect/LevelSelectPanel.prefab`
- `Assets/Prefab/PopConfirm/PopConfirmPanel.prefab` (`UiConfirmDialog`)
- `Assets/Prefab/Setting/SettingsPanel.prefab`
- `Assets/Prefab/Setting/AboutPanel.prefab`
- `Assets/Prefab/Common/ItemTooltip.prefab`
- `Assets/Prefab/Common/UiToastPanel.prefab`

The cream/chocolate palette, golden primary action and pale green secondary action reuse the confirmed Home resources. New shared audio, gift, info and switch PNGs are in `Assets/Res/image/common/`; six PNGs total 7,687 bytes on disk (not a build-size measurement). No old art was deleted for this update.

Level selection retains real table data, unlocking, stars, progress positioning, and confirmation before launching a battle. The loop library's internal temporary pool does not distinguish prefab types, so the new optional `LevelSelectThemeCell` presents either a chapter or a level within one recyclable template. Legacy prefabs still use the existing fallback.

Settings retains native `UnityEngine.UI.InputField` and `WechatInputFieldEnabler`; sound preferences and gift-code business rules are unchanged. About retains the existing attribution text, including authors and license. Tooltip keeps click-outside dismissal and now scrolls long descriptions. Confirmation supports both two-button confirmation and centered one-button alerts. Toast retains automatic fade/float and does not block input or pause the game.

Modal surfaces clamp to the available canvas and scroll their content. Tooltip placement converts world coordinates through the canvas camera before clamping to the canvas. Toast resets the full-screen anchors applied by UIManager so only the small surface is drawn.

## Backpack

The approved V2 design is now implemented in `BackPackPanel.prefab`. See `Tools/UI/backpack-update.md` for resources, category configuration, validation and rollback details. Actual renders are in `ArtOutput/UI/Backpack/overview.png`; the original V1/V2 drafts are preserved.

Four columns, item names and quantities, selected-item emphasis, existing ItemTooltip interaction, and category-specific empty states. Tabs are All, Currency and Materials (current fragments), driven by a configurable category catalog. The strip scrolls horizontally as more item types are introduced. No new use/combine/capacity actions.

## Validation and preview

- `HomeUIEditorPreview.Begin("dialogs")` starts an isolated preview using in-memory profile storage.
- `HomeUIEditorPreview.Finish()` restores the original scene setup and Game View size.
- `ArtOutput/UI/Dialogs/validation-report.json`: 30 checks passed.
- Actual renders: `ArtOutput/UI/Dialogs/overview.png` plus individual screenshots.
- Tested 1080 x 1920, short 1080 x 1440, long 1080 x 2400, and 360 x 640 Game View rendering.
- Checks cover level states and mixed-cell recycling, confirmation callbacks and pause lock, single-button alerts, audio switches, native gift input/daily state, invalid-code nonmutation, attribution, tooltip scrolling/edge clamping/dismissal, toast dimensions/raycast/timing, and persistent-save/audio-preference preservation.
- Editor validation does not confirm WeChat device keyboard, platform ad behavior, or final build/package size.

## Rebuild

1. Run `Tools/UI/prepare-dialog-assets.cjs` with the bundled Node runtime.
2. Refresh Unity and wait for compilation.
3. Run `DialogsUIBuilder.Apply()` or menu `工具/Home UI/Apply Utility Dialogs`.
4. To regenerate review boards, run `Tools/UI/render-dialogs-proof.cjs` and `Tools/UI/render-backpack-design.cjs`.

Original utility prefabs and UI scripts were copied before changes to `Library/UIBackups/Dialogs-20261018-211045/`. This is a local safety copy, not a replacement for version control.
