# Backpack UI implementation

Implemented from approved `ArtOutput/UI/BackpackDesignV2/overview.png`.

## Result

- Updated `Assets/Prefab/BackPack/BackPackPanel.prefab` in place; its GUID and Home entry point are retained.
- Tabs: All / Currency / Materials. Gold and Diamond use ItemTable.Type 1; existing character fragments use Type 2.
- Four-column pooled grid, names, explicit quantities (including one), selected-card emphasis, small grade-color marks and category-specific empty states. Empty placeholder rows are removed.
- Clicking an item uses the existing ItemTooltip and retains its table description, with the current quantity appended. Dismissal, changing category and closing the panel clear selection; changing category resets the grid to the top.
- Wallet balances are read through PlayerProfileService.GetItemCount rather than stale item arrays. Legacy fragment-only storage remains supported, and duplicate storage entries are not counted twice.
- UI code does not redeem, consume, merge or write inventory data. No save schema or generated table files changed.
- Shared `ItemCell` and its reward/loot prefab remain unchanged.

## Adding item categories

Edit `Assets/ScriptableObject/UI/BackpackCategories.asset` in the Inspector:

1. Add an entry with a unique English `id`, display `label`, optional `heading` and `icon`.
2. Set `itemTypes` to the matching values from ItemTable.Type. Keep `allItems` false for ordinary categories.
3. Add/configure the corresponding items in the existing item table pipeline.

Existing Materials can include additional material types by extending its itemTypes list; adjust its heading when it includes more than character fragments. Categories are not inferred from Chinese names or item-ID ranges. Unknown types still appear under All before a dedicated category is configured. Tabs scroll horizontally as the configured list grows, and longer labels get a wider tab rather than shrinking the type.

The default All tab is first in the catalog. Opening the backpack selects the first configured tab. The grid sorts fragments first (equipped hero first), then currencies, then other types.

## Resources and sizing

- New English-named resources are under `Assets/Res/image/Home/backpack/`: normal/selected card surfaces, tintable icon surface, and empty-state glow.
- Four new PNGs total 10,409 bytes on disk; this is not a final build-size measurement. Shared buttons, close icon, currency symbols and character portraits are reused.
- Design canvas 1080 x 1920; centered modal 920 x 1040; cards 194 x 250; grid spacing 20 x 22.
- The modal clamps to the canvas. Items scroll vertically, the category strip scrolls horizontally, and empty-state visuals shrink on short viewports.
- New label characters are added to the existing dynamic Chinese font in memory before display. No font source or baked atlas is duplicated or replaced.

## Verification

- `ArtOutput/UI/Backpack/validation-report.json`: 35 checks passed in Unity Editor using an in-memory profile.
- Actual render board: `ArtOutput/UI/Backpack/overview.png`.
- Tooltip and empty-state board: `ArtOutput/UI/Backpack/interaction-states.png`.
- Screenshots also cover 1080 x 2400, short 1080 x 960, and 360 x 640 Game View sizes.
- Checks cover Home entry, filtering, selected tabs/cards, icon/count rendering, currency reconciliation, fragment fallback/deduplication, glyph coverage, tooltip dismissal, category/inventory empty states, future category configuration, tab overflow, a 36-item pooled grid, switching back to its first row, reopening, and persistent-save preservation.
- WeChat device touch behavior and final package/build size are not verified by Editor checks.

## Rebuild and rollback

Run `Tools/UI/prepare-backpack-assets.cjs`, refresh/compile Unity, then run `BackpackUIBuilder.Apply()` or menu `工具/Home UI/Apply Backpack`. Existing category configuration is preserved by rebuilds.

`HomeUIEditorPreview.Begin("backpack")` runs the isolated preview; `HomeUIEditorPreview.Finish()` restores the scene setup and Game View size. `Tools/UI/render-backpack-proof.cjs` composes the saved screenshots.

Original backpack prefab and relevant scripts were backed up locally under `Library/UIBackups/Backpack-20261018-215158/`. Old art and the shared reward ItemCell were not deleted. Work remains on the existing branch, uncommitted.
