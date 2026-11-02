# Character equip button

`CharacterSelectionPanel.prefab` now uses a teal button with a person and circular swap arrows for `EquipButton` / 换将上阵. Its label uses `#315B51`; the normal background uses `#94CFBC` with a `#40695E` border. Pressed and disabled states have their own sprites. `StartButton` / 开始游戏 retains its gold background and swords icon.

Only the equip button's sprites, label color and icon aspect preservation were changed in the existing prefab. Positions, dimensions, serialized panel references and click logic are preserved. `CharacterProgressionUIBuilder.StyleEquipButton` also applies the same styling during future page rebuilds.

- Assets: `Assets/Res/image/Home/character/character_equip_{normal,pressed,disabled}_9slice.png` and `icon_character_swap.png`.
- Generator: `Tools/UI/prepare-character-equip-assets.cjs`; import borders are in `Tools/UI/character-assets.json`.
- Four source images total 9,534 bytes. No new font or atlas.
- Original prefab, builder and manifest: `Library/UIBackups/CharacterEquipButton-20261026/`.
- Unity preview: `ArtOutput/UI/CharacterEquipButton/`; direct checks cover button visibility, all state sprites, pointer raycast, icon separation and the real equip action switching back to the unchanged Start button. The preview uses an in-memory player profile.
- All 26 existing character-page checks pass, including phone, short-screen and long-screen layouts; persistent player data is preserved. The older tier-visual assertion now waits for the existing 1.05-second promotion animation before checking the final state.
