# 竹林书院地图（保留方案）

2026-10-27 更新：当前第二章已改用秋日书院，见 `Doc/autumn-academy-battle-maps.md`。原竹林八图现为 `Assets/Resources/Map/ZlMap1.prefab`～`ZlMap8.prefab`，GUID和地图内容保留，仅文件名/根节点名改变；`TileMap2_1..8` 由秋日版本使用。共用美术统一在 `Assets/Res/Map/Styles/SharedCartoon/`。最新流程见[制作手册](../Tools/Skills/word-mower-maps/references/production-manual.md)。本页后续内容为历史记录，原路径不再作为当前接入依据。

竹林重新生成入口现为 `Tools > Maps > Map Library > Generate Bamboo Academy`，只更新 `ZlMap1..8`，不覆盖当前第二章秋日地图。

2026-10-24。第二章完成八张独立地图，统一使用既有风格 2 的青灰石板、草地、竹丛、石灯、石凳、灌木、岩石与路牌。沿用 30×30 格范围与平地移动规则，以地表轮廓和景物分布区分关卡。文件名采用第一章目前使用的下划线格式。

| 关卡 / levelId | 名称 | 轮廓 | Prefab / mapPath |
|---|---|---|---|
| 2-1 / 201 | 竹径迎门 | 上下收口、中央开阔的纵向庭院 | `TileMap2_1.prefab` / `Map/TileMap2_1` |
| 2-2 / 202 | 斜阶问学 | 从左下向右上逐层错开的斜向阶梯 | `TileMap2_2.prefab` / `Map/TileMap2_2` |
| 2-3 / 203 | 竹影回庭 | 顶部开口的 U 形回庭，中央竹影草地 | `TileMap2_3.prefab` / `Map/TileMap2_3` |
| 2-4 / 204 | 分径寻章 | 单入口向上展开、左右分叉 | `TileMap2_4.prefab` / `Map/TileMap2_4` |
| 2-5 / 205 | 九曲书径 | 外围折返后绕入中央的螺旋形书径 | `TileMap2_5.prefab` / `Map/TileMap2_5` |
| 2-6 / 206 | 双庭对语 | 横向并列的双庭，以中央短廊联通 | `TileMap2_6.prefab` / `Map/TileMap2_6` |
| 2-7 / 207 | 书山长阶 | 下窄上宽的三层平台和短连接道 | `TileMap2_7.prefab` / `Map/TileMap2_7` |
| 2-8 / 208 | 文昌论道 | 多层退台的宽阔论道场与两侧入口 | `TileMap2_8.prefab` / `Map/TileMap2_8` |

所有 Prefab 位于 `Assets/Resources/Map/`。本轮没有修改 ChapterLevel 配表，旧 `TileMap201` 等地图继续保留；正式启用时将关卡 `mapPath` 指向上表的新路径。

## 生成与适配

Unity 菜单：`Tools > Maps > Chapter Two > Generate Style 2 Maps (2-1 to 2-8)`。

生成器：`Assets/Editor/ChapterTwoMapGenerator.cs`。与第一章共用 Tilemap 拼装、透明摆件边界检查和真实相机渲染方法，地形边缘继续由 `CartoonTilePlan.Build` 自动计算。第二章布局数据独立于第一章。

菜单会重新写入这八张地图，已有目标 Prefab 会先备份到 `Library/MapBackups/ChapterTwo-时间戳/`。手动编辑后如需保留这些修改，不应直接重新生成。

`Ground` 保持 900 格完整地面、1 单位格距与 `[0,30]×[0,30]` 范围。地形和摆件使用 `Map` 图层，兼容现有小地图。地面排序 -100、景物排序 -50。石路和草地都可走，景物不新增阻挡；不改变刷怪、相机和 Boss 围场逻辑。出生点 `(15,15)` 周围的 5×5 区域留空。

竹丛宽度约 2～2.7 格，石灯 1 格，石凳 1.8 格；不用大摊位，保留清爽的庭院比例。

## 资源与包体

继续引用 `Assets/ArtPreview/CartoonMaps_32_db482fa081/`。原风格 2 只导入了矩形场地使用的直边与外角，因此从同一份原始交付补齐 `stone_on_grass_m127`、`m223`、`m247`、`m253` 四张内角 PNG 及对应 Tile 资产。

四张新增图片均为 **32×32**，源 PNG 合计 **7,704 字节**。八张地图共用 26 张纹理；没有为每关复制图片，没有新增字体、材质或运行时脚本。八张 Prefab 的 YAML 源文件合计 **2,318,324 字节（约 2.21 MiB）**。这些源文件大小不能直接视为微信小游戏包体增量，本轮没有执行微信构建。

预览 PNG、蓝图与报告在 Assets 外，不作为游戏贴图入包。旧 Resources 地图仍保留，后续包体清理应单独核对引用和构建报告。

## 预览和验证

`ArtOutput/World/ChapterTwoMaps/`：

- `overview.png`：八张实际 Prefab 的整图渲染。
- `phone-overview.png`：9:16 手机视野，正交相机 size 10，中心 `(15,15)`，无角色、怪物或 HUD。
- `TileMap2_N.png` 与 `TileMap2_N-phone.png`：每关独立渲染。
- `blueprints/TileMap2_N.txt`：顶行在上，`#` 为石路，`.` 为草地。
- `build-report.json`：资源加载、完整地面、石路连通、引用、图层、可见摆件边界、出生点留白与边界围墙校验，八张均通过。
- `regeneration-check.json`：重复生成时八张地图的 GUID 和 Prefab 文件内容均稳定。
- `silhouette-check.json`：任意两张地图最少有 **184/900 格（约 20.4%）** 的地表分布不同。

边界检查在隔离的 Editor Preview Scene 中调用实际 `BattleMapLoader.BuildPerimeterWalls`，确认四面边界和 WallMarker；不是完整战斗通关测试。本轮生成保护范围内 31 个既有地图 Prefab 的哈希保持一致，用户场景状态保持不变。

重新制作总览可运行 `node Tools/Maps/compose-chapter-one.cjs 2`（依赖 sharp）；不传章节参数时仍生成第一章总览。Unity 一键菜单会更新单图和 build-report，总览与两项专项检查报告是本次交付记录。
