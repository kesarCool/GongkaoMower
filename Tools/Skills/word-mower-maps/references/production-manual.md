# 文字割草机 · 瓦片地图制作手册

更新：2026-10-27。工程 `G:/Demo/GongkaoMower`，Unity 2021.3.22f1c1。本文为当前制作规范；旧章节文档和 ArtOutput 历史报告中的路径以本文为准。

## 1. 资源放在哪里

```text
Assets/
  Res/Map/Styles/                   # 所有导入地图美术的统一根目录
    SharedCartoon/                  # 现有田园、竹林、滨水共用集合
      Textures/                     # 32px地面及共用装饰
      Tiles/                        # Tile .asset，引用上面的Sprite
      Prefabs/                      # 最初三张示例，非当前关卡入口
      BuildReport.json              # 原素材导入缓存，含路径
    AutumnAcademy/                  # 秋日独立素材，不修改竹林原色
      Textures/                     # 红枫、银杏、秋灌木、门、书桌、落叶
      Terrain/
        Source/                     # 两张128px材质母版，非地图运行时依赖
        Textures/                   # 自动生成64px地面和过渡片
        Tiles/                      # 与地面Sprite配套的Tile资产
    Common/
      GeneratedTiles/              # 当前仅common_tree，旧围墙仍在引用
      Palettes/                     # 原空调色板目录，保留扩展位置
    <NewStyleName>/                # 后续风格：EnglishStyleName
      Textures/                     # 装饰；图集也必须放本风格内
      Terrain/Source/
      Terrain/Textures/
      Terrain/Tiles/
  Resources/Map/
    TileMap1_1.prefab ... TileMap1_8.prefab   # 第一章美食篇
    TileMap2_1.prefab ... TileMap2_8.prefab   # 第二章秋日书院
    ZlMap1.prefab ... ZlMap8.prefab          # 竹林书院备用，尚未分配新章节
```

大原图放 `ArtOutput/World/<StyleName>/source/`；真实预览、蓝图和报告放 `ArtOutput/World/<StyleName>/`；覆盖前备份放 `Library/MapBackups/<Task>-<Timestamp>/`。Library 不进 Git，重要的长期备份应另行保存。

统一路径定义在 `Assets/Editor/MapResourcePaths.cs`，Node 工具用 `Tools/Maps/map-paths.cjs`。后续新增风格必须使用统一根目录；不要再生成到 ArtPreview 或在每关目录复制贴图。原素材交付文件保留在 `美术资源`，它不是 Unity 导入目录。

## 2. 选择哪条制作流程

| 需求 | 使用方式 | 会修改什么 |
|---|---|---|
| 已有风格增加关卡 | 扩展对应生成器的布局 Plan，再生成目标地图 | 指定 prefab，共用已有瓦片 |
| 新季节或新地貌 | 新建风格目录、地面母版和装饰；做独立配置/生成器 | 新资产和指定地图 |
| 仅换秋日地面，保留当前装饰 | `AutumnAcademyTerrainMigration.Apply()` | 只替换 Ground 的 Tile 引用 |
| 重新搭建整套秋日地图 | `AutumnAcademyMapGenerator.Generate()` | 重建八张地图的 Ground 和 Decoration |
| 重新生成备用竹林八图 | `ChapterTwoMapGenerator.Generate()` | 只写 ZlMap1～8，不覆盖秋日 |
| 仅整理目录/重命名 | `AssetDatabase.MoveAsset` | 位置/名称，保留 GUID，不重建美术 |

已有工具支持确定布局的一键生成；“设计新风格、画新瓦片、自动决定所有美术”尚未做成通用按钮，需要先完成风格配置。日常使用生成菜单即可，不需要手动一格格拼图。

## 3. 现有一键入口

先退出播放模式，确保编译成功。存在未保存场景修改时先处理，生成器不应擅自保存用户场景。

| Unity 菜单（Tools → Maps） | 方法 | 说明 |
|---|---|---|
| Chapter One → Generate Style 1 Maps (1-2 to 1-8) | `ChapterOneMapGenerator.Generate()` | 保留1-1，只生成1-2～1-8 |
| Chapter Two → Generate Autumn Academy (2-1 to 2-8) | `AutumnAcademyMapGenerator.Generate()` | 整图重建，覆盖当前秋日装饰 |
| Chapter Two → Apply Autumn Terrain Only | `AutumnAcademyTerrainMigration.Apply()` | 已批准装饰不变，换秋日瓦片 |
| Map Library → Generate Bamboo Academy | `ChapterTwoMapGenerator.Generate()` | 备用竹林输出 ZlMap1～8 |
| Resources → Validate Map Resources | `MapResourceOrganizer.Validate()` | 检查当前 Resources/Map 成品 |
| Resources → Organize Existing Map Resources | `MapResourceOrganizer.Organize()` | 本次旧目录迁移入口；已完成时只验证 |

最初的 `Tools → 地图 → 明快卡通 → 一键生成三张地图` 是原素材导入/样图工具，不是八关生成器。同一套32px原素材会复用 `SharedCartoon`；不同素材版本放 `Styles/Imported/CartoonMaps_<resolution>_<hash>/`，仍在统一根目录。`GenerateMapTiles.cs` 是旧占位色块工具，不用于制作正式秋日美术。

Unity MCP 可执行对应 C# 方法；没有 MCP 时用上述菜单，脚本编译失败先修编译再运行。不要同时启动两个 Unity 操作同一工程。自动化预览结束后恢复原场景和播放状态，不修改玩家存档。

## 4. 新的一章怎么做

1. 明确风格和输出名单，例如 `WinterTown`、`TileMap3_1..8`。只有用户要求接入时才修改关卡配置；不能按地图命名推断章节已接好。
2. 先画布局：每关一张30行、每行30格的蓝图，`.`为底地，`#`为主路。当前 Plan 用矩形叠加/挖空生成轮廓，不要求手绘每格。
3. 八关要有轮廓差异：入口庭院、长阶、回庭、分岔、回折、双院、阶梯、广场；同一章色系和材质保持一致。不要只移动两棵树就当新关卡。
4. 先验证单张：地面拼缝、角色/文字怪对比度、装饰比例和手机视野。通过后补齐八张。
5. 扩展本风格生成器或独立配置，复用 `ChapterOneMapGenerator.BuildThemedPrefab(...)`、`CartoonTilePlan.Build(...)` 和原生渲染工具。新地面 family 需要在 `CartoonTilePlan` 中接入，不能只改目录名就认为新风格已适配。
6. 生成前备份目标 prefab + meta，记录非目标地图哈希；生成后运行第7节验收。已有资产更新保留 GUID，必要时更新内容，不能删除 meta 后重建。

## 5. 瓦片与装饰的制作规格

### 地面

- 现有共用地面为32px，PPU32；秋日为64px，PPU64。两者都必须做到一张瓦片覆盖1×1世界单位，不能让分辨率变化改变地图大小。
- 秋日母版为128×128、2×2相位；`AutumnAcademyTerrainBuilder` 仅生成布局实际用到的瓦片。目前56张地面Sprite，八关共享。
- 八邻域 mask 决定直边、外角和内凹角。缺少相邻正交边的对角位要剔除；秋日相位按 `(x&1, y&1)` 选变体，不能用随机变体打乱连续纹理。
- 地面是已合成底色的不透明Sprite，无Alpha通道；Sprite Single、FullRect、Center Pivot、Bilinear、Clamp，关闭MipMap和Read/Write，Tile Collider=None。
- 当前秋日 WebGL 导入为 DXT1 Crunch，quality85。沿用已验证设置，最终纹理格式/大小以实际微信构建为准。
- 可复用的美术提示：俯视正交、明快卡通、低对比度细纹理、无建筑/文字/强光斑/透视、四边可平铺；秋日使用暖灰石路与土褐落叶地，禁止把旧绿草简单整体染黄。生成图只是材质输入，需处理边缘并验证真实铺设。

### 装饰

- 独立透明PNG，一套风格共享；树/门当前256px，小摆件128px。秋日Sprite PPU128，靠配置的世界宽度统一显示比例。
- 装饰Pivot通常 `(.5,.05)`，落叶 `(.5,.5)`；按源图实际透明边界检查占地，不只看整张PNG矩形。
- 秋日导入 DXT5 Crunch quality80；关闭MipMap和Read/Write。出现明显透明边缘压缩瑕疵时单独调整小类，不全局放大所有资源。
- 不在地面瓦片里画树、围墙或人物；装饰统一挂在 `Decoration`，暂停菜单开关才能全部生效。
- 树、门和大摊位靠地图边缘布置，中央5×5保持清晰。第一章 food_stall 已缩小，新增地图应复用它，不重新放大或复制原图。
- 秋冬元素符合季节：秋日以红枫、银杏、落叶为主；冬日避免柳条，腊梅可以使用。不能仅靠整张地图偏黄来表达季节。

当前素材准备工具：`Tools/Maps/prepare-autumn-academy.cjs` 与 `prepare-autumn-terrain.cjs`。它们会从现有大图重新生成秋日Sprite/母版，改变源文件；目录整理或只做预览时不要运行。新风格应创建自己的处理配置，不能覆盖秋日输入。

## 6. Prefab 与运行时约束

```text
<MapName> (Grid, Map layer, scale1)
  Ground (Tilemap + TilemapRenderer, Map layer)
  Decoration (Map layer, 默认开启)
    tree / shrub / lamp / leaves ... (SpriteRenderer, Map layer)
```

- 范围 `[0,30) × [0,30)`，Ground900格完整，无缺失Tile；排序地面-100、装饰-50、秋叶-90。
- 主路必须连通；出生点 `(15,15)` 周围5×5不被摆件可见区域覆盖。装饰可见边界保持在地图内。
- 当前装饰不作为障碍物：不要顺手加树碰撞或水域阻挡，这会影响刷怪/寻路。地图本身无内部 Collider；运行时根据Ground包围盒生成四面围墙。
- `BattleMapLoader` 根据 `ChapterLevel.mapPath` 加载。配置使用 `Map/TileMap2_1` 或 `Map/ZlMap1`，不带 Assets、Resources 前缀及 `.prefab` 后缀。
- 找不到资源仍回退 `Map/TileMap1_1`。该地图必须一直可加载。
- `Decoration` 的名字、层级入口与暂停页偏好设置保持兼容。编辑器预览默认可见；玩家自己的开关记录不被重置。
- Home章节路线图是独立UI图片，不会随战斗Tilemap自动更新，需要另行接入，不能把二者混为一套资源。

## 7. 验收与预览

日常入口：`Tools → Maps → Resources → Validate Map Resources`。当前已验证24张：第一章8张、秋日8张、备用竹林8张。该入口也会扫描后续放进同目录的新图。

通用验证检查：Resources加载、900格、30×30范围、内部无Collider、引用有效、Tile/Sprite依赖位于Styles、已知章节使用正确地面集、默认兜底地图可加载。输出 `ArtOutput/World/MapResourceOrganization/validation.json`。

通用检查不能代替美术验收。新生成器还需逐关验证：轮廓有差异且连通、出生区留白、装饰边界、邻接边一致、四面墙正确、非目标地图和场景未被改动。秋日地面迁移还要校验Decoration及地形轮廓保持。

预览必须用实际Prefab：`ChapterOneMapGenerator.Render(prefab, outputPath, phone, background)`；全图1024×1024，手机540×960、9:16、中心(15,15)、相机size10。秋日传入暖土色背景 `new Color(.74f,.51f,.37f)`。使用此方法无需进入播放模式，预览场景会被关闭。

拼总览的 Node 工具：

```powershell
# 在工程根目录执行；node不可用时用本机实际Node可执行文件
node Tools/Maps/compose-chapter-one.cjs 1
node Tools/Maps/compose-chapter-one.cjs bamboo
node Tools/Maps/compose-autumn-academy.cjs ArtOutput/World/AutumnAcademyTerrain
```

竹林新预览需先生成/渲染到 `ArtOutput/World/BambooAcademyLibrary/ZlMap1.png..ZlMap8.png` 及 `-phone.png`。第一章拼图沿用历史 `TileMap1-1.png` 文件名，但成品资源是 `TileMap1_1.prefab`。这些是预览名称，不影响加载。

两个历史核对脚本只用于对应迁移的保全审计：

```powershell
node Tools/Maps/verify-map-resource-organization.cjs
node Tools/Maps/verify-autumn-terrain.cjs
```

它们依赖 Library 内的迁移前备份。后续主动修改地图内容后，历史哈希不同是正常的；不要反复拿旧哈希验证新设计。日常使用 Unity 通用检查和本轮新报告。

## 8. 包体与交付

- 资源移动保留相同GUID，不会复制纹理；多张地图共享同一套Tile/Sprite，不能简单把“八张地图”算成“八倍贴图”。Prefab布局数据本身仍有体积。
- 按当前要求，ZlMap1～8已放入Resources，即使暂时不在关卡配表中，也会参与打包；共用纹理已在使用，但备用Prefab等仍可能增加包体。它们是备用内容，不再是包外备份。
- `Res`、`ArtPreview` 这些普通目录名称不决定是否入包：Resources地图或场景依赖到的资源会被包含。Source母版未被运行时引用，不要把它误做成Tile的Sprite引用。
- PNG源文件体积、Unity导入压缩大小、微信主包/分包大小必须分别记录。本次目录整理没有重新构建微信包，不给出未经构建的包体增量结论。
- 如需精确包体结果，重新构建 `Build/minigame` 后结合构建报告和 `Tools/Maps/audit-minigame-package.cjs` 核对资源分配；先看脚本参数与当前构建产物，勿使用过期输出当最新测量。
- 清理资源需结合运行时Resources路径、场景和Prefab依赖、生成器输入一起检查；不能因为“场景里没拖引用”就删Tile或备用地图。

交付包含：目标Prefab清单、统一目录的共享美术、实际全图及手机视野、原生验证报告、备份位置、已接入哪些关卡、是否重建微信包。不要交付每关一份全尺寸背景图来冒充瓦片方案。
