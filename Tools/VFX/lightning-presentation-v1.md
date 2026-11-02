# 雷霆万钧表现 V1

当前普通与突破落雷的播放、回收时间已延长为原来的 1.5 倍，最新参数与验证见 [时长调整记录](vfx-duration-1.5x.md)。下文保留原版本记录。

后续已补齐并重制突破版：普通 Lv.1～4 保留本页效果，羁绊解锁的 Lv.5 与角色 Legend stage≥2 切换为专用红色图集的两层突破特效。最新设计、预算、72 项验证与预览见 [lightning-breakthrough-v2.md](lightning-breakthrough-v2.md)。下文保留首次两层样本的交付记录与测量。

2026-10-25。第三个原创序列帧试制样本已接入工程。新版将原有 9 个粒子系统精简为「闪击、落点余电」2 层，采用白青亮芯、饱和紫蓝轮廓，在浅黄色沙地上保持对比。效果置于怪物文字下层，避免亮斑盖住文字；不增加蓄力或延迟伤害。

## 已实现

- 闪击：0.16 秒、8 帧，第一帧即接通落点，随后断裂消失。
- 落点与余电：0.30 秒、8 帧，由接触星芒扩展成中空电环，再散成短弧；0.34 秒自动回池。
- 每层最多 1 个粒子，合计最多 2 个；共用一张 512×256 图集和一个 Sprites/Default 材质，不需要 Bloom、额外 Shader 或全屏闪光。
- 技能传入原有 strikeRadius，Prefab 以 1.1 为尺寸基准缩放，落点轮廓随实际 AOE 变化。余电是装饰性边缘，不新增持续伤害或新的判定。

运行资源：

```text
Assets/Res/Textures/VFX/sheet_lightning_v1.png
Assets/Res/Materials/VFX/mat_lightning_v1.mat
Assets/Res/Prefabs/Effects/Impact/fx_lightning_v1.prefab
```

## 玩法保持原规则

实际配置的等级 1～5 分别为 1、2、3、4、5 道落雷；轮内间隔 0.18 秒，轮间隔 3、2.95、2.9、2.85、2.85 秒，单次伤害 2.5、2.5、2.5、2.75、2.75，AOE 半径 1.1、1.15、1.2、1.25、1.35，索敌距离 10。

技能定义仅变更 strikeFxPrefab 引用，其他字段与操作前备份一致。运行技能脚本仅将原来的三参数 VFX 调用改为四参数、补传 strikeRadius；原索敌、随机选择、暴击、伤害计算、AOE 去重、冷却与逐道调度均保持原代码。

构建器保留定义文件的原有字段与换行。Unity 保存资产时可能补写继承字段的默认空值，因此构建后只把新的特效引用行应用到原定义文本，不顺带更新其他配置。

## 资源测量

| 项目 | 旧版选定依赖 | 新版 |
| --- | --- | --- |
| 粒子系统 | 9 | 2 |
| PNG 依赖数量 | 11 | 1 |
| PNG 源文件合计 | 514,348 字节 | 55,700 字节，54.4 KiB |
| 新图集 | — | 512×256，WebGL ETC2_RGBA8Crunched，质量 65 |
| 新纹理 ETC2 块数据 | — | 131,072 字节，128 KiB，无 mipmap |
| 新纹理 Editor API 内存读数 | — | 164,901 字节，包含对象等开销 |
| 新图集＋材质＋Prefab 源文件合计 | — | 294,099 字节，不含 meta 与代码 |

PNG 大小阈值为 128 KiB；禁用 mipmap 与 CPU 可读副本。高清生成原稿保留在 Assets 外，不作为运行纹理导入。

这些是选定依赖的源文件与纹理数据测量，不能当作全工程净减少量或最终微信包体。旧材质、贴图留在原资源库，尚未逐项裁剪，它们可能被其他效果共用。旧 `fx_lightning.prefab` 与 meta 已确认无引用，并归档到 `ArtOutput/VFX/LightningPresentationV1/Retired/Assets/Res/Prefabs/Effects`，SHA256 前后校验通过。保留该归档便于回退。

## 验证与性能范围

旧版 44 项检查通过；新版最终 58 项检查通过，验证期间无运行异常。使用真实 Game 场景、装备技能与内存存档夹具，不改用户存档。

- 普通首击伤害 2.5；满级 5 道总伤害 13.75，0.18 秒之前不触发后续雷击。
- AOE 内邻近目标受伤，范围外目标不受伤；目标拥有重复碰撞器也只计一次伤害。
- 冷却、10 单位索敌边界、无目标与卸下后不再释放均保持原行为。
- 命中点与真实选中目标一致，普通与满级特效尺寸按各自真实半径变化。
- 暂停冻结两层动画与回收计时；恢复后回池；复用时尺寸复原；场景退出无残留。
- 40 次同帧特效请求遵守现有每帧最多 8、同时最多 32 的限流；即使表现被限流，原技能仍然造成伤害。
- 单独满级回放与「落雷＋手雷爆炸＋满级符令」实际施放回放都记录到 5 道雷击、真实伤害与可见效果。叠加回放还记录到 3 次符令、1 次手雷施放。

| 同一满级验证夹具的雷电部分 | 旧版回放峰值 | 新版回放峰值 |
| --- | --- | --- |
| 激活效果上的 ParticleSystem 组件数 | 45 | 4 |
| 实际存活粒子数 | 57 | 3 |

两组计数在单技能、三技能叠加回放中一致。这些计数仅包含雷电，不包含其他技能。新版层数少、持续时间短，因此重叠数量明显减少；这是结构与存活数量证据，并非帧时、批次或过绘基准测试。本轮没有进行微信导出、真机帧率与最终包体验证。

验证夹具只把中心怪物保留为可随机索敌目标，其他物理敌人仍能被 AOE 伤害，以便稳定核对范围边界。三只文字怪距离很近，预览中的文字重叠来自夹具排布。

## 预览与复现

`ArtOutput/UI/LightningPresentationV1/` 内包含浅地图、草地对比图，以及旧版、新版 MP4/GIF 回放。回放包含满级落雷与三技能叠加两个片段，保留实际采集帧间时长，不慢放动作。静帧对已触发的真实粒子采样至 0.035 秒与 0.20 秒，分别检查闪击和余电；它们不是整轮同时播放的示意图。

原始截图、时间戳、运行报告分别保留在 `ArtOutput/UI/LightningBefore` 和 `ArtOutput/UI/LightningAfter`。资源审计、旧资源归档校验位于 `ArtOutput/VFX/LightningPresentationV1/Audit`。编辑器已退出验证并恢复 `Assets/Scenes/Boot.unity`，场景无未保存修改，后台运行设置按验证前记录还原。

Unity 中执行 `LightningPresentationV1Builder.Build()` 重建与绑定。新版验证将 SessionState `LightningPresentationBaseline` 设为 false，执行 `HomeUIEditorPreview.Begin("lightning")`，报告完成后执行 `HomeUIEditorPreview.Finish()`。旧版验证需在隔离副本中恢复归档旧 Prefab 及其定义引用；Baseline 标志只切换验收预期，不自动回滚资产。

`Tools/VFX/render-lightning-proof.cjs` 导出对比和回放，`audit-lightning-assets.cjs` 核对配置与依赖。原图由内置 image_gen 生成，透明背景；完整最终提示词见 [prompts.md](../../ArtOutput/VFX/LightningPresentationV1/prompts.md)，两组原稿在同目录 `Sources/bolt.png`、`Sources/ground.png`。Unity 打包工具仅按格取样、缩小与组合。
