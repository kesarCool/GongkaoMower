# 雷霆万钧：红蓝突破表现 V1

此版已由专用红色贴图的 [V2 重制](lightning-breakthrough-v2.md) 替换，旧运行资源与构建器已归档到 V2 的 Retired 目录。下文仅保留历史设计和测量记录。

2026-10-25。补齐独立突破状态与特效切换。原来的 SkillLightningStrike 只有普通特效引用，满级与 Legend 都播放同一个蓝色落雷；本次增加突破 Prefab，保留蓝白主雷，强化轮廓并加入红色分叉与落点余电。

## 触发规则

- 普通 Lv.1～4：原蓝白落雷，2 个粒子系统。
- 原有羁绊被动解锁并升到配置上限 Lv.5：自动播放红蓝突破版。没有羁绊时仍由现有 PlayerSkills 阻止升到第五级，不把第四级的「当前可达满级」误判为突破。
- 角色 Legend stage≥2：使用现有 ApplyLegendBreakthrough 入口启用突破表现；Rare stage 1 不启用。等级数值刷新后保留 Legend 状态，重建新技能不继承旧实例状态。
- 突破 Prefab 未配置时回退普通特效，伤害照常结算。

表现状态由 SkillLightningStrike.IsBreakthroughVisual 查询，实际 Prefab 由 SelectedStrikeFxPrefab 选择。LightningStrikeSkillDefinition 将继承的 maxLevelPrefab 传给技能，按定义 maxLevel 判断等级门槛。没有修改 PlayerSkills 或角色升阶流程。

## 视觉与资源

| 层 | 变化 |
| --- | --- |
| 蓝白主雷 | 宽度由 1.65 加至 2.10，高度由 4.6 加至 4.9，保留亮芯；持续 0.18 秒 |
| 红色分叉 | 新增一层较宽的分叉电光，放在蓝白主雷后方，持续 0.22 秒 |
| 红色余电 | 原落点动画改用红色材质，持续 0.36 秒；保持原 AOE 尺寸基准 |

突破共 3 层，每层最多 1 个粒子，0.40 秒自动回池。文字仍位于特效前层，不增加全屏闪光或 Bloom。三层共用原来的 512×256、55,700 字节 PNG（54.4 KiB），图集 SHA256 与修改前一致，**新增贴图为 0**。

红色通过简单透明 Shader 将原图集的蓝紫 RGB 通道重映射成红色，并保留接近白色的亮部。蓝白主雷继续使用原材质；本轮未重新生成或编辑位图。

新增的一个 Shader、一个材质和一个突破 Prefab 的源文件共 357,220 字节，不含 meta、代码或原有文件差量；主要为粒子 Prefab 的 YAML 文本。这不是微信编译后的包体增量。相比普通版，突破多一个粒子系统和一个材质，会增加少量绘制工作；尚未进行微信真机或最终包体测量。

运行资产：

```text
Assets/Res/Prefabs/Effects/Impact/fx_lightning_breakthrough_v1.prefab
Assets/Res/Materials/VFX/mat_lightning_red_accent_v1.mat
Assets/Res/Materials/VFX/LightningRedAccent.shader
```

## 验证

72 项运行检查通过，验证期间无运行异常。使用真实 Game 场景、装备技能、原有升级 API 与内存存档夹具。

- 通过真实 TryLevelUp 从一级升到四级，确认未拥有被动时不能升五级；实际取得配置羁绊后升五级，确认生成突破 Prefab。
- 验证 Legend stage 1/2 区分、数值刷新、状态清除、新实例与未配置突破资源时的回退。
- 普通伤害 2.5、满级五道总伤害 13.75；0.18 秒连击间隔、冷却、索敌范围、AOE 内外、重复碰撞器去重保持原逻辑。
- 突破资源共用原图集、三层单粒子上限、半径缩放、回池复用、暂停恢复、密集命中限流与退场清理均通过。视觉被限流时仍造成原伤害。
- 单独突破五连击与雷电＋手雷＋符令同屏回放都记录到实际伤害及五道雷；采样峰值均为 6 个雷电 ParticleSystem 组件、4 个存活粒子。这是该回放的计数，并非并发上限或帧率测试。

配置审计确认仅增加 maxLevelPrefab 特效引用，原数值数组不变；技能的 ApplyRuntimeStats、冷却、索敌、逐道调度和伤害方法仅将特效生成调用改为选择当前表现。未新增伤害、扩大判定或改变成长数值。

## 预览与复现

`ArtOutput/UI/LightningBreakthroughV1/comparison.png` 为真实 Lv.4 普通与实际升级后 Lv.5 突破的对比。静帧对已经触发的粒子采样至 0.035 秒，余电采样至 0.20 秒。`breakthrough.mp4` 与 `breakthrough.gif` 包含单独五连击、三技能叠加回放，保留原采集帧间时长。

同目录保留原始截图、帧、时间戳与 validation-report.json。资源与 SHA256 审计在 `ArtOutput/VFX/LightningBreakthroughV1/Audit`。此前的普通版与旧资源对比记录未覆盖。

Unity 菜单 `工具/VFX/Build Lightning Breakthrough V1` 可重建并绑定突破资源。普通图集使用原 `LightningPresentationV1Builder`；普通构建器会保留已配置的 maxLevelPrefab。验证时将 SessionState `LightningBreakthroughValidation` 设为 true、`LightningPresentationBaseline` 设为 false，执行 `HomeUIEditorPreview.Begin("lightning")`，结束后执行 `HomeUIEditorPreview.Finish()` 并关闭突破验证标志。预览导出脚本为 `Tools/VFX/render-lightning-breakthrough-proof.cjs`。
