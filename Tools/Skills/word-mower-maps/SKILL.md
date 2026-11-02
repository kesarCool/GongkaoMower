---
name: word-mower-maps
description: Produce, reskin, organize, and validate tiled battle maps for the GongkaoMower Unity project (文字割草机), using shared cartoon terrain and per-stage layouts. Use for new chapter map sets, seasonal tile palettes, decoration changes, and resource organization in this project.
---

# 文字割草机地图制作

适用工程：`G:/Demo/GongkaoMower`。若工程被移动，从用户指定工作区定位 `Assets/Editor/MapResourcePaths.cs`。先读 [制作手册](references/production-manual.md)，再按本次任务选择流程；手册记录的是当前工具能力，不代表允许修改未请求的章节。

## 工程约定

- 所有导入地图美术归 `Assets/Res/Map/Styles/<EnglishStyleName>/`；共用集合为 `SharedCartoon`，秋日为 `AutumnAcademy`，旧围墙树素材为 `Common/GeneratedTiles`。路径以 `MapResourcePaths.cs` 为准，Node 对应 `Tools/Maps/map-paths.cjs`。
- 成品放 `Assets/Resources/Map/`。第一章为 `TileMap1_1..8`，第二章当前秋日为 `TileMap2_1..8`；竹林备用为 `ZlMap1..8`。竹林生成使用 `ChapterTwoMapGenerator.BambooMapPath`，`MapPath` 仍指当前秋日，不能混用。
- 移动和重命名已导入资产用 `AssetDatabase.MoveAsset`，保留 GUID；更新脚本和 JSON 中的字面路径。大原图、预览、备份分别放 Assets 外的 `ArtOutput`、`Library/MapBackups`，不复制每关贴图。
- 地图为 30×30、每格 1 单位，根 Grid；子节点 `Ground` 和 `Decoration`。Ground 完整900格，Map 层，地面排序 -100、装饰 -50、落叶 -90；无内部碰撞体。保留中央5×5出生留白、暂停页 Decoration 显隐和运行时四面围墙。
- 只改地面时使用地面迁移流程，不能整图重建覆盖已批准装饰。新季节新建独立瓦片，避免给 `SharedCartoon` 全局改色。新章节先做差异化轮廓，再共用同一套瓦片。

## 执行

1. 确认编辑器不是播放/编译状态，记录目标 prefab、meta 和非目标地图哈希；检查未保存场景。已有授权范围内直接完成，不增加例行确认。
2. 从手册选择已有生成器。新风格先做素材和单张样图，再扩展布局；当前没有“输入风格名称自动生成任意美术”的通用工具，新风格需要独立配置/生成器。
3. 修改编辑器脚本后刷新 Unity，检查编译错误，再运行相应生成/迁移方法。Unity 不可用时完成可做的文件工作，并明确哪些原生验证尚未执行。
4. 跑 `MapResourceOrganizer.Validate()`；新生成器另外验证连通性、出生区、装饰边界、邻接拼缝、四面墙与非目标地图保持。资源迁移的历史审计见手册，不把历史哈希当作后续内容修改的验收条件。
5. 导出实际 prefab 全图与9:16视野，检查镜头范围和文字怪可读性。报告实测导入纹理大小；只有重新构建才能声称微信包体变化。

交付：成品 prefab、共享瓦片/装饰、实际 Unity 预览、验证报告、改动和包体说明。不要把 AI 概念图当作已经实现的地图。
