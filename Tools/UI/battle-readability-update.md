# Battle readability polish

本轮针对 HUD 遮挡、小地图黑屏和单字库文字表现进行修正。

## Changes

- `GameLayer/HudTop/TopGradient` 的 Image Alpha 从 1 调为 **0.82**，保留原渐变形状。`BattleUIBuilder` 同步此值，重新构建不会恢复旧值。
- 1-1 黑屏原因：`TileMap101` 的 Ground 与装饰是 Default 层，小地图相机只渲染 Map 层。将这 15 个地形/装饰 Renderer 所在节点归入 Map 层，保留原 Tile、Sprite、位置、比例、碰撞数据及 Prefab GUID。`CartoonBattleMapGenerator` 创建地形和装饰时也写入 Map 层，避免后续一键生成再次发生。
- 根据奶油描边在地图上对比不足的反馈，文字怪改为亮字面配深棕描边（V3）。普通怪每波在 5 个浅亮色中选色，同波仍保留细微色相变化；Boss 使用独立珊瑚色，不继承普通怪的波次配色。原 seeded 随机模式和 Off 模式保留。
- 受击闪白改为顶点颜色反馈，避免更改共享材质影响整波怪物。字体、字号变化、旋转、击退、伤害和掉落逻辑保留。

## One-font styling

继续使用现有 `Fonts/msyh SDF`。不新增 FontAsset、TTF/OTF、字体贴图或备用字库。4 种共享 SDF 材质只在运行时创建，并引用同一份字库图集；通过字面扩张和描边加重小字，不另引入粗体字库。

| 用途 | 字色 | 描边 |
| --- | --- | --- |
| HUD 纸面文字 | `#493627` | 纸面内保持原样，直接覆盖地图的关卡/Boss 文本加细奶油描边 |
| 普通文字怪 | 米白 `#FFF5DD`、薄荷 `#A5F0DA`、冰蓝 `#B8DFFE`、浅金 `#FFDB7A`、桃色 `#FFC6A1` | 深棕 `#34271F`，SDF 宽度 0.32；字面扩张 0.16 |
| Boss 文字 | `#FF8B65` | 同上 |
| 普通伤害 | `#FFF5DD` | 深棕 `#493627`，SDF 宽度 0.24 |
| 暴击 | `#FFC452` | 同上 |
| 破防 | `#ED825B` | 同上 |
| 抵抗/免伤 | `#D7DFD7` | 同上 |
| 玩家血量数字 | `#FFF3D4` | 奶油描边，沿用当前血条位置与大小 |

数值是 TMP SDF 材质参数，不是固定像素描边。普通怪物保留轻微上下渐变，表中字色为渐变基准。主要参数位于 `BattleTextTheme.cs` 和 `WordMonsterWaveStyle.cs`。

V3 使用 `BattleReadabilityBuilder.ApplyWordContrast()` 仅更新 7 个文字怪样式，不重复修改地图、HUD 和伤害设置。运行验证输出在 `ArtOutput/UI/BattleReadabilityV3`，18 项检查通过，包括材质共享、字面与描边的对比、受击闪白隔离、复用恢复、原随机模式、两关小地图及存档隔离。已检查实际沙地、草地截图；尚未做微信设备验证。原 V2 图片保留作对照。

## Validation and screenshots

- `HomeUIEditorPreview.Begin("battle-readability")`：真实 Game 场景、真实 101/102 地图、内存存档，配合 Prefab 生成的代表性文字/伤害样本；检查小地图真实渲染像素、图层覆盖、单字体/同图集引用、共享材质、受击恢复和对象复用。
- 本轮 18 项检查通过：`ArtOutput/UI/BattleReadability/validation-report.json`。
- `before-level-1-1-1080x1920.png` 为修改前截图，`after-*` 为修改后 Unity Editor 运行截图；不代表微信真机截图。
- `BattleReadabilityBuilder.Apply()` 是针对现有预制体的局部更新，不重建整套面板。备份路径记录在 `Library/UnityMCPTools/readability-backup-path.txt`。
- 最终微信包体增量和设备显示尚未测量；本轮没有新增图像资源或字体库。
- 预制体审计中 `enemy003` 的原 Sprite 引用已在本轮备份中缺失（GUID `84ce6042cad94e244a9af991e19fe104`），此次保留，没有新增缺失脚本。它与本轮地图图层和文字材质修改无关。
