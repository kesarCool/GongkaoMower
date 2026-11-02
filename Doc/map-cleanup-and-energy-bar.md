# 地图资源清理、缺失兜底与能量条加深

2026-10-24，继续在现有分支上更新。

## 地图加载

- ChapterLevelCatalog.DefaultMapResourcesPath 改为 `Map/TileMap1_1`。
- 沿用 BattleMapLoader 的实际 Resources.Load 失败回退逻辑；空路径、未配置的关卡以及已删旧地图路径都使用 TileMap1_1。
- 有效地图路径继续加载其本身，不将旧名称静默映射到不同关卡。
- 第一章生成器的保留首图路径、地图可读性修复工具同步使用现存首图，避免继续依赖已删除的 TileMap101。
- 未改动关卡表、刷怪、战斗奖励或玩家存档。

## 清理范围与保护

先记录 Unity AssetDatabase 中 1,382 项资源与 302 项序列化资源的依赖，再对候选集合计算保留资源的外部入边和依赖闭包；执行前重新查询了所有保留资源的依赖。

本次共归档 **42 项旧资源及对应的 42 份 .meta**：

- Assets/mapRes/Prefab 中六张没有外部引用的旧试验地图 TileMap307～312。
- Assets/Res/Map 中两套旧调色板、20 个旧 Tile 资产，以及14张只供旧模板和调色板使用的贴图。

文件移出 Assets 后存于：

`ArtOutput/MapCleanup/ArchivedLegacyMaps-20261024-174413/`

归档中保持原始相对目录与 GUID，84 个文件逐个校验 SHA-256。资产本身共 3,800,134 字节，包含 .meta 共 3,854,638 字节。这是源文件体积，不能作为微信包体减少量；未重新打包。

以下资源明确保留：

- 当前 Resources/Map 中 TileMap1_1～1_8、TileMap2_1～2_8 全部16张地图及其完整依赖。
- ArtPreview/CartoonMaps_32_db482fa081 中三套风格和现有预览地图，包括后续可复用的第三套风格。
- Assets/mapRes/Image 中的原始图案、PNG 和文字布局源数据，以及制图工具代码。
- Assets/Res/Map/GeneratedTiles/common_tree.png：仍被 Assets/Prefab/Battle/wall.prefab 引用，未移动。
- 原版竹林书院、滨水古镇底图以及已确认的秋冬修订稿，仍保留在 Assets 外的 ArtOutput/UI 交付目录。

核对清单：ArtOutput/MapCleanup/cleanup-plan.json、archive-manifest.json、validation-report.json。

如需恢复，先退出播放模式，按 archive-manifest.json 的 originalPath 将归档文件连同对应 .meta 放回原路径，再刷新 Unity。恢复前应确认原路径为空，避免覆盖后续新资产。

## 能量条

只调整战斗 HUD 的 Energy/Fill 颜色：`#8DAE6E` → **`#4F8C38`**，不改变条高、填充比例、升级阈值或 Boss 血条。同步更新 BattleUIBuilder，重新应用 UI 时也会保留新色值。

改动前文件备份：Library/MapBackups/LegacyCleanup-20261024-165827/。

实际 Unity 预览渲染：ArtOutput/MapCleanup/energy-before-1080x1920.png 与 energy-after-1080x1920.png。这是隔离 Editor Preview Scene 的实际 HUD 渲染，不是完整战斗截图。

## 验证

Unity 已完成编译。29项校验全部通过：9种真实 Resources 加载/路径规范化场景、16张现存地图的可加载性/引用/哈希保持、能量条颜色、围墙共享贴图、未配置关卡兜底，以及保留资源未引用归档资产。未进行完整关卡通关或微信真机打包验证。
