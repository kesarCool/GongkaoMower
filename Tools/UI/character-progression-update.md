# Character progression UI update

Implemented after the Home roadmap, using the approved V5 character design.

## Entry points

- Existing panel: `Assets/Prefab/CharacterSelect/CharacterSelectionPanel.prefab`
- New card: `Assets/Prefab/CharacterSelect/CharacterCard.prefab`
- New attribute row: `Assets/Prefab/CharacterSelect/CharacterStatRow.prefab`
- Builder: `Assets/Editor/CharacterProgressionUIBuilder.cs`
- Automated preview: `HomeUIEditorPreview.Begin("character")`; finish with `HomeUIEditorPreview.Finish()`.

The panel's cellPrefab and attrRowPrefab point to the new dedicated assets. The old row/card resources remain available. The original panel GUID and catalog references are preserved.

## Resources and layout

- Character-specific sprites: `Assets/Res/image/Home/character/`
- Shared assets: `Assets/Res/image/common/`
- Import manifest: `Tools/UI/character-assets.json`

The selected tab has the folder-shaped background, checkmark and connecting line. The unselected tab is plain text with a transparent hit area. Cards retain thin borders. Selection changes the gold card background; the equipped checkmark follows the equipped profile separately. Locked cards remain clickable and show a dedicated condition field.

All nine attributes remain. Stat rows and skill descriptions use independent scroll containers. The skill name and body are separate; both promotion stages show their actual requirements from HeroUpgradeData. Theme colors also replace the old white inline rich-text colors.

At the 1080x1920 design size the layout follows the source coordinates. Extra height shifts the entire hero header by E/8 and tabs/paper by E/4; the paper gains E/2. All content is anchored inside its paper. The character strip and fixed action stay above navigation. Short screens scroll the page body above the fixed action, keeping text readable.

## Functional preservation and fixes

Upgrade, cooldown, promotion requirements, fragment cost, locked-card hints/unlock flow, switching tabs and existing data calculations remain. Equipping retains the existing SelectedCharacterContext.Set call, which persists through PlayerProfileService.SetEquippedCharacter. Start uses the selected map level through HomeHubController.TryResolveStartLevel.

## Verification

Results: `ArtOutput/UI/CharacterProgression/validation-report.json`; real Unity screenshots are in the same directory. Checks cover the two tabs, five cards, nine attributes, child prefab bindings, upgrade/double-click handling, selection versus equipped state, actual equip persistence, locked interaction, promotion eligibility/cost, long descriptions, page revisit and screen ratios.

The preview operates on an in-memory save and verifies the persistent profile is unchanged. It does not replace the player's existing save. WeChat device rendering, touch gestures and final package size are not verified by this Editor preview.
