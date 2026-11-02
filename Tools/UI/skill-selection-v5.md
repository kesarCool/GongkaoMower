# Skill selection and energy HUD V5

2026-10-30. Incremental edits to existing prefabs, preserving their GUIDs and the existing single TMP font atlas.

## Appearance

- Main cards show five 40px stars in a centered dedicated footer. Owned stars are steady, the proposed next star pulses with unscaled time, and a five-star target uses red filled stars. Normal levels use the existing gold star art. The middle Lv summary is hidden; gain values and descriptions retain their original data formatting and scrolling. Refinement enlarged the stars from 34px without changing either pulse frequency.
- Each star now has a persistent empty backing and a separately centered filled overlay. Only the next filled overlay fades from transparent to opaque; neither layer changes size. Owned filled stars stay opaque, and future stars show only the empty backing. `SkillSelectionV5Builder.ApplyLayeredStars()` updates only CardView.
- Equipped active/passive slots retain their original Lv badges; no stars in the top HUD. Main card footers retain five-star previews. Bond icons remain icon-only.
- Matched equipped and bond icons share the same unscaled pulse clock. Both use centered (0.5, 0.5) anchors and pivot, scale from 1 to 1.22, and lift by up to 5px; a white moving stripe is masked to the icon silhouette. Equipped icons are restored to 84px. The previous frame-only early exit is removed.
- The energy HUD has a dark track, twenty slanted green segments, the existing green energy orb, an explicit energy caption, current/required count and a separate level badge. Energy pickups ease the fill and pulse the orb; a level change briefly fills/flashes the bar and then returns to actual remaining energy. Boss HP still replaces the entire energy display.

## Input

- A fresh pointer down compresses the card face over approximately 70 ms. Release on that card starts confirmation; moving out or dragging cancels. Detail scrolling remains on its existing ScrollRect.
- Confirmation releases the pressed face, briefly lifts it, makes the target star steady, highlights its frame and dims the other cards. The skill callback runs once after 0.42 seconds of unscaled feedback. Refresh and all cards remain locked throughout.
- Opening input protection is checked BEFORE animation/locking: normal 0.3 seconds, Boss 1.2 seconds. The old callback-time rejection could leave all cards disabled; it is removed. Share-return request locks are retained.
- Rebinding or closing cancels pending animation. Reopening restores opacity, transforms and locks.

## Rebuild and verify

- `SkillSelectionV5Builder.Apply()` updates the existing CardView, CardSelectionPanel, two V4 slot prefabs and GameLayer. This is an incremental builder; avoid applying the older full V4 builder by itself afterward.
- `HomeUIEditorPreview.Begin("skill-feedback")` runs the actual Game scene and real prefabs with an in-memory profile. `HomeUIEditorPreview.Finish()` exits and restores the editor scene setup.
- Output: `ArtOutput/UI/SkillSelectionV5/After/`. Reference video thumbnails are retained in the sibling `Reference/` folder.
- New graphics are UGUI geometry; stars and the orb reuse existing sprites. No new raster images, font atlas, material assets or gameplay balancing changes.
- WeChat touch feel and device performance require a device run; editor validation does not establish those results.
