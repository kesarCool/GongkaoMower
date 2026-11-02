# Result return selects current progress

After winning level 108, victory progress unlocked level 201 but `SelectedLevelContext` still pointed at 108. Home intentionally honors a valid explicit selection so players can replay older levels. As a result, returning from the battle continued to show chapter 1.

`GameResultPanel.ReturnToHome()` now clears the completed battle selection when the result is a victory. Home then resolves the latest unlocked level from the saved profile, selecting chapter 2/201 after the first clear of 108 and chapter 3/301 after 208. Both the result Exit button and the new-character unlock return link use this method.

The selection is retained while the result panel is open, so Next continues to resolve relative to the completed battle. Failure exit retains the failed level. Retry, Next, pause quit and manual Home selection logic are unchanged. Selecting an old level in Home continues to survive tab/page refreshes; finishing a successful replay and returning Home returns to the player's current progression.

The shared return method releases the UI stack's pause locks before restoring `Time.timeScale` and loading Home. This prevents the departing result UI manager from restoring a paused/slow battle time scale during scene teardown.

## Validation

- The complete runtime assembly, including the regression runner, compiled offline against the current Unity project response file. Only existing unused-field warnings were emitted.
- `HomeUIEditorPreview.Begin("result-return")` drives actual Game result buttons and scene changes with an in-memory profile. It covers the 108/201 and 208/301 chapter boundaries, failure exit, ordinary level progression, replay return to later progress, final configured level, the new-character return link and Home manual selection retention.
- Runtime validation passed in Unity with actual result buttons and Game/Home scene transitions. The checks cover both chapter boundaries, failure, ordinary progression, replay, final-level completion and the unlock return link. Results and screenshots are saved under `ArtOutput/UI/ResultReturnProgress/`; the in-memory fixture preserved the persistent player save.

The existing result script and editor preview dispatcher were backed up in `Library/UIBackups/ResultReturnProgress-20261025/` before modification. No scene, prefab, image or level-table change is required.
