# 罡气护体 V1：清晰护体罡环

历史版本：当前实际绑定已改为 [V3 内外反转力场](field-generator-v3.md)；[V2 视频参考力场](field-generator-v2.md) 也保留历史记录。以下数字、验收与资产为 V1 当次记录；重建 V1 会主动切回历史表现。

2026-10-26。已替换实际运行的程序化淡色底盘与三圈脉冲，接通原先未使用的 `fieldVisualPrefab`。新版由稳定范围外缘、旋转罡气图和一圈向外扩散的脉冲组成。普通为琥珀金与深赭轮廓；突破为朱红金与深红轮廓。中心通透，置于怪物文字和角色下层。

## 当前实现

原实现通过运行时生成 256px 底盘和 128px 环纹理，叠加透明度后较淡，按 Sprite 默认 PPU 缩放也无法准确表达外沿。新版固定薄圈按世界半径绘制，罡气原稿按可见径向范围归一化后打包，带透明边距的 Sprite 不再直接当作有效直径。

持续的旋转和外扩脉冲由 `SkillFieldGenerator.Tick(deltaTime)` 驱动，沿用各级原脉冲周期。真实伤害发生时小幅增强外缘，使用局部计时，不调用战斗随机数。暂停不推进动画或结算；卸下立即隐藏并销毁，随拥有者销毁，不走一次性特效对象池。缺失美术时退化为两圈薄边界，仍正常伤害。

保留五级突破与原有 `ApplyLegendBreakthrough(stage > 0)` 规则；本次不改 Legend 门槛。所有 DPS、半径、Tick 间隔、羁绊门槛、暴击 / 穿透处理及查询代码保持原有行为。技能定义只修改 `fieldVisualPrefab`、`visualBaseDiameter`、`sortingOrder` 三个表现字段。伤害方法除增加表现通知外逐字对比一致。

原 `Game/Skills/Register FieldGenerator SkillDef` 菜单已改为调用同一表现构建器，避免旧脚本重新创建另一份定义、恢复旧特效或覆盖数值。当前 SkillCatalog 引用不变。

## 预算

| 项目 | 当前结果 |
| --- | --- |
| 运行图集 | 512×256，两个 256×256 Sprite，共用一张图 |
| 源 PNG | 131,926 字节，约 128.8 KiB；构建上限 192 KiB |
| WebGL | ETC2_RGBA8Crunched，质量 70，无 mipmap / CPU 可读副本 |
| ETC2 无 mipmap 块数据 | 131,072 字节，128 KiB |
| Editor 纹理 API 内存读数 | 175,467 字节，包含其他对象开销 |
| 常驻渲染器 | 原来 4 个 SpriteRenderer → 1 个 SpriteRenderer + 2 个 MeshRenderer |
| 材质 / 几何 | 2 个共享材质，2 个圈共用 1 个 Mesh；每个护体场 962 个三角形 |
| 粒子 / 碰撞 / 新玩法 | 0 / 0 / 0 |

没有运行时纹理绘制、材质实例化、每帧对象创建或额外 Update。新 Mesh 的三角形比原四张四边形更多，常驻透明层从 4 减为 3；不能据此直接推断真机帧率更快。旧版缓存 RGBA32 纹理像素约 320 KiB / 份，还保留 CPU 可读数据；与新 ETC2 块数据属于不同资源格式，不作为精确总内存或包体差额。

完整资源信息见 [resources.json](../../ArtOutput/VFX/FieldGeneratorV1/Audit/resources.json)。本轮未构建微信包或测量真机帧率。

## 配置、重建和预览

- [常驻配置](Library/Persistent/field_generator_v1.json) 由构建器实际读取：普通 / 突破旋转速度、PNG 预算、压缩质量和排序。固定图集规格与可见直径同时核对，避免误配。
- Unity「工具/VFX/Build Field Generator Presentation V1」重建并绑定；保留图集、Mesh、材质、Prefab 和定义的原 GUID。
- Unity「工具/VFX/常驻特效/罡气护体预览」查看普通 / 突破、半径、强度、脉冲周期、浅沙 / 草地 / 深底并导出 PNG。工作台也有入口。
- Unity「工具/VFX/常驻特效/检查罡气护体配置」检查当前绑定与配置，输出 [configuration.json](../../ArtOutput/VFX/FieldGeneratorV1/Audit/configuration.json)。
- Unity「工具/VFX/实战验收/罡气护体」运行现有实际场景夹具；新一轮输出独立时间目录。

这是常驻表现配置，放在 `Library/Persistent`；一次性 `Recipes/*.json` 的回池模型与导入器不用于它。

当前版本连续重建的 6 份资产字节一致、GUID 保持，见 [rebuild.json](../../ArtOutput/VFX/FieldGeneratorV1/Audit/rebuild.json)。改版前源代码与定义保存在 `ArtOutput/VFX/FieldGeneratorV1/Baseline/`，验收不依赖会被清理的 Library 备份。Before 是修改运行代码前采集的实际画面；Baseline 标志只用于已恢复原版代码 / 资源的隔离副本，不能作为回滚开关。

## 证据

原版 25 项、新版 53 项实战检查通过，覆盖四个场景采样、周期伤害、AOE 内外、真实升级门槛、突破 / Legend、世界半径、角色缩放、暂停恢复、卸下、重装、缺失资源回退和退场清理；均使用内存存档并确认 PlayerPrefs 保留原值。

扩展共用预览器后，原导入 / 打包 / 隔离预览工具 38 项回归检查继续通过；另外检查了受击增强后的 alpha 上限。见 [editor-tool-checks.json](../../ArtOutput/VFX/FieldGeneratorV1/Audit/editor-tool-checks.json)。源代码与定义完整性检查见 [integrity.json](../../ArtOutput/VFX/FieldGeneratorV1/Audit/integrity.json)，可通过 `Tools/VFX/audit-field-generator-assets.cjs` 复核。

实际三秒连续伤害回放记录的渲染器峰值由 4 降为 3。视频使用本次实际采集时间，未慢放旧录像。

- [原版 / 新版对照](../../ArtOutput/UI/FieldGeneratorV1/comparison.png)
- [新版 MP4](../../ArtOutput/UI/FieldGeneratorV1/after.mp4) / [GIF](../../ArtOutput/UI/FieldGeneratorV1/after.gif)
- [新版运行报告](../../ArtOutput/UI/FieldGeneratorV1/After/validation-report.json) / [原版报告](../../ArtOutput/UI/FieldGeneratorV1/Before/validation-report.json)

导出脚本：`Tools/VFX/render-field-generator-proof.cjs`。默认使用工程既有的 Node / sharp / ffmpeg 环境。

两张罡环原稿由内置 image_gen 生成，突破版以普通版原稿为编辑输入。源图和完整提示词：[Sources](../../ArtOutput/VFX/FieldGeneratorV1/Sources)、[prompts.md](../../ArtOutput/VFX/FieldGeneratorV1/prompts.md)。正式图集位于 `Assets/Res/Textures/VFX/sheet_field_generator_v1.png`；Unity 仅做等比例缩小、透明边距和图集打包，没有程序绘制替代原创图案。
