# 第一章美食篇：风格 1 地图资源

2026-10-27 目录更新：当前第一章为 `Assets/Resources/Map/TileMap1_1.prefab`～`TileMap1_8.prefab`；共用美术在 `Assets/Res/Map/Styles/SharedCartoon/`。最新制作规范见[制作手册](../Tools/Skills/word-mower-maps/references/production-manual.md)。下文为最初生成时的历史记录，`TileMap101` 和 ArtPreview 路径不再是当前接入路径。

## 摊位资源优化（最新）

`food_stall` 在第一章地图和引用它的两张 ArtPreview 样图中统一收至 **2.2 格宽**（原约 3.6～3.75 格）；相邻木箱收至 0.9 格并靠近摊位。Unity 导入纹理由 **256×256 降为 128×128**，保留原始 PNG 以便后续修改，没有新增贴图。多个摊位共用同一纹理，不按实例数量重复计入贴图包体。

本轮直接修改现有 Prefab 的摆件 Transform，保留地形、GUID、现有命名及其它手动调整。共更新 10 个地图 Prefab 中的 21 个摊位引用，包含第一章八图及两张使用该摊位的 ArtPreview 样图。当前工程内已被关闭的摆件继续保留其状态。

优化菜单：`Tools > Maps > Chapter One > Optimize Food Stall (128px, Compact)`。生成器的后续输出也使用小摊位，并会识别现有下划线命名，避免重建一套连字符命名的重复地图。

最新记录为 `ArtOutput/World/ChapterOneMaps/food-stall-optimization.json` 与 `food-stall-validation.json`，检查已通过：地形哈希不变、GUID 不变、原 PNG 不变、导入纹理 128×128、第一章摊位实际显示宽度 2.2、Resources 加载正常。旧 build-report、regeneration-check、perimeter-check 是首版生成时的历史记录，文件路径和尺寸以最新优化记录为准。最新整图与手机视野总览已经刷新。

优化前文件备份在 `Library/MapBackups/FoodStall-20261024-094622-853/`。纹理的 Editor 内存测量由 524,904 降到 131,688 字节，约减少 75%；这不等于最终微信包体节省量。

## 地图与配表路径

| 关卡 / levelId | 布局 | Prefab | mapPath（Resources 相对路径） |
|---|---|---|---|
| 1-1 / 101 | 田园食集：原有开阔场地，保留 | `TileMap101.prefab` | `Map/TileMap101` |
| 1-2 / 102 | 十字集市：四向道路与中央交叉场地 | `TileMap1_2.prefab` | `Map/TileMap1_2` |
| 1-3 / 103 | 双院连廊：左上、右下两个错位庭院 | `TileMap1_3.prefab` | `Map/TileMap1_3` |
| 1-4 / 104 | 果园环路：中央草坪与外环道路 | `TileMap1_4.prefab` | `Map/TileMap1_4` |
| 1-5 / 105 | 回折长街：横向三段回折，草带穿插 | `TileMap1_5.prefab` | `Map/TileMap1_5` |
| 1-6 / 106 | 麦田双道：两条纵向道路与横向联络场地 | `TileMap1_6.prefab` | `Map/TileMap1_6` |
| 1-7 / 107 | 双环食坊：上下两片草岛与双环轮廓 | `TileMap1_7.prefab` | `Map/TileMap1_7` |
| 1-8 / 108 | 宴集广场：四处绿地、四组摊位与开阔中心 | `TileMap1_8.prefab` | `Map/TileMap1_8` |

Prefab 均在 `Assets/Resources/Map/`。本轮没有修改 ChapterLevel 配表，也没有覆盖旧 `TileMap102` 至 `TileMap108`。需在正式启用时将相应关卡的 `mapPath` 改成上表路径；仅生成 Prefab 不会自动切换现有关卡。现有第一关没有重命名为 `TileMap1-1`，避免重复入包。

## 一键生成与资源结构

Unity 菜单：`Tools > Maps > Chapter One > Generate Style 1 Maps (1-2 to 1-8)`。

生成器：`Assets/Editor/ChapterOneMapGenerator.cs`。它复用 `CartoonTilePlan.Build` 计算草土过渡边缘，直接引用 `Assets/ArtPreview/CartoonMaps_32_db482fa081/Tiles/` 和 `Textures/` 中的现有资产，不复制图片、不创建新 Tile。摊位的导入分辨率由上述优化工具单独处理。旧 `mapRes` 的蓝图思路仍有利用价值，但本轮蓝图为这些新地图独立设计，避免直接复刻旧图。

重复生成会更新这七张地图，请在手改它们之后留意覆盖；同一版生成器重复生成的 Prefab 内容和 GUID 已验证稳定。现有第一关、三张风格样图和其它地图均不在生成范围。

全部地图使用相同的 30×30 格、1 单位格距和坐标范围 `[0,30]×[0,30]`。`Ground` 为 900 格完整地面，排序 -100；`Decoration` 为现有 Sprite 摆件，排序 -50。所有地形和摆件 Renderer 使用 `Map` 图层，使现有小地图相机可见。

这次差异是地表轮廓与摆件布局。草地、土路继续自由行走，摆件继续采用现有的视觉装饰规则；没有增加内部碰撞或改变刷怪、相机和 Boss 围场逻辑。出生点附近 `(15,15)` 的 5×5 区域留空，供角色与战斗操作使用。

## 预览与校验

输出在 `ArtOutput/World/ChapterOneMaps/`：

- `overview.png`：八张地图实际 Prefab 的整图渲染对照。
- `phone-overview.png`：9:16 手机视野对照，使用现有战斗相机的 orthographicSize 10，中心 `(15,15)`。不包含 HUD、角色和怪物。
- `TileMap1-N.png`、`TileMap1-N-phone.png`：每关单独渲染。
- `blueprints/TileMap1-N.txt`：地形蓝图，首行为顶部，`#` 为土路，`.` 为草地。
- `build-report.json`：完整地面、边缘 Tile、地形连通性、引用、图层、出生点留白、可见摆件边界、Resources 加载和原第一关哈希校验。
- `regeneration-check.json`：七张新 Prefab 的二次生成 GUID 与文件内容一致性，全部通过。
- `perimeter-check.json`：在隔离的 Editor Preview Scene 中调用真实 `BattleMapLoader.BuildPerimeterWalls`，八张图均生成正确的四面边界及 WallMarker。该项不是完整战斗通关测试。

本轮最终生成与上述校验通过；第一关文件哈希生成前后相同，用户当前 Boot 场景保持未修改。七张新地形的蓝图哈希各不相同。没有进行微信构建，也没有改动真实玩家进度。

一键菜单会更新单张地图渲染和 build-report；总览拼版可运行 `Tools/Maps/compose-chapter-one.cjs`（Node + sharp），二次生成与边界专项报告为本次交付的验证记录。

## 包体说明

`ArtPreview` 是普通文件夹名称，不是 Unity 的构建排除规则。Resources 下地图引用的 Tile、Sprite 和纹理会随依赖参与构建。

这七张地图的纹理依赖完全包含在现有 `TileMap101` 的 30 张纹理中，新增纹理依赖数为 **0**。没有新增字体、材质或运行时脚本。七张新 Prefab 的 YAML 初版源文件合计约 **2.14 MiB**；这只是源文件大小，不代表最终微信小游戏的包体增量，实际增量应以构建报告为准。

预览 PNG 和蓝图位于 Assets 外，不作为游戏素材入包。旧 Resources 地图暂时保留；即使关卡配表不再使用，Resources 内的旧资产仍可能占据包体空间，后续应结合引用和构建报告另行清理。
