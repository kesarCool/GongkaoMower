# Boss 技能产出总目录

更新：2026-10-29。由 [产出索引](boss-deliverables.json) 和 `node Tools/VFX/audit-boss-workflow.cjs --write-index` 生成。编辑索引后重新生成本页。

制作从 [Boss 工作流](boss-production-workflow.md) 开始；复制 [任务单](Templates/boss-skill-brief.md)，使用 [提示词 / 执行指令](boss-production-prompts.md)。

本页核对的是归档证据及文件 / 菜单引用，没有重新运行 Unity 战斗。配表状态由制作记录维护；观感确认与微信真机结果分别记录。冲刺 / 小刀、分身 / 头标存在共用报告，检查数不相加为一个总验收数。

| 产出 | 接入范围 | 历史主报告 | 配置检查 | 原速预览 |
| --- | --- | --- | --- | --- |
| [刀气爆发](boss-blade-burst-v1.md) | 正式表已用 | [89 项](../../ArtOutput/UI/BossBladeBurstV1/After/validation-report.json) | [15 项](../../ArtOutput/VFX/BossBladeBurstV1/Audit/configuration.json) | [录像](../../ArtOutput/UI/BossBladeBurstV1/after-closeup.mp4) |
| [冲刺](boss-dash-knife-v1.md) | 正式表已用 | [79 项](../../ArtOutput/UI/BossDashKnifeV1/After/validation-report.json) | [9 项](../../ArtOutput/VFX/BossDashKnifeV1/Audit/dash-configuration.json) | [录像](../../ArtOutput/UI/BossDashKnifeV1/after-dash15-motion.mp4) |
| [小刀](boss-dash-knife-v1.md) | 正式表已用 | [79 项](../../ArtOutput/UI/BossDashKnifeV1/After/validation-report.json) | [10 项](../../ArtOutput/VFX/BossDashKnifeV1/Audit/knife-configuration.json) | [录像](../../ArtOutput/UI/BossDashKnifeV1/after-knife-motion.mp4) |
| [分身](boss-clone-v1.md) | 正式表已用 | [90 项](../../ArtOutput/UI/BossCloneV1/After/validation-report.json) | [10 项](../../ArtOutput/VFX/BossCloneV1/Audit/configuration.json) | [录像](../../ArtOutput/UI/BossCloneV1/after-closeup.mp4) |
| [召唤](boss-summon-v1.md) | 正式表已用 | [70 项](../../ArtOutput/UI/BossSummonV1/After/validation-report.json) | [9 项](../../ArtOutput/VFX/BossSummonV1/Audit/configuration.json) | [录像](../../ArtOutput/UI/BossSummonV1/after-closeup.mp4) |
| [复活](boss-revive-v1.md) | 正式表已用 | [90 项](../../ArtOutput/UI/BossReviveScatterV1/After/validation-report.json) | [9 项](../../ArtOutput/VFX/BossReviveV1/Audit/configuration.json) | [录像](../../ArtOutput/UI/BossReviveScatterV1/after-closeup.mp4) |
| [地裂](boss-zone-v1.md) | 能力完成，未配表 | [29 项](../../ArtOutput/UI/BossZoneV1/After/validation-report.json) | [12 项](../../ArtOutput/VFX/BossZoneV1/Audit/configuration.json) | [录像](../../ArtOutput/UI/BossZoneV1/after-closeup.mp4) |
| [公共头顶标识](boss-head-crest-v1.md) | 公共 Prefab 已接入 | [100 项](../../ArtOutput/UI/BossHeadCrestV1/After/validation-report.json) | [16 项](../../ArtOutput/VFX/BossHeadCrestV1/Audit/configuration.json) | [录像](../../ArtOutput/UI/BossHeadCrestV1/after-closeup.mp4) |

共 7 个技能配置和 1 个公共外观配置；其中 6 类技能已由正式表使用，地裂待配表。普通一次性库现为 10 项，Boss 小消散已计在其中。

## 共享正式资源

- [sheet_boss_blade_burst_v1.png](../../Assets/Res/Textures/VFX/sheet_boss_blade_burst_v1.png)：28,653 字节源 PNG，按路径只计一次。
- [mat_boss_blade_burst_v1.mat](../../Assets/Res/Materials/VFX/mat_boss_blade_burst_v1.mat)
- [mat_boss_blade_solid_v1.mat](../../Assets/Res/Materials/VFX/mat_boss_blade_solid_v1.mat)
- [mat_boss_blade_trail_v1.mat](../../Assets/Res/Materials/VFX/mat_boss_blade_trail_v1.mat)
- [fx_boss_blade_dissolve_v1.prefab](../../Assets/Res/Prefabs/Effects/Impact/fx_boss_blade_dissolve_v1.prefab)

字体快照使用实际 TMP 共享字体材质；原稿位于 Assets 外。源 PNG、压缩块数据、运行内存和最终微信包体分别计量。

## 各项工程和证据入口

### 刀气爆发 · boss_blade_burst_v1

按真实 8 / 12 / 16 向预警，短刀气、柔边拖尾与池化消散。共享 512×256 图集，源 PNG 28,653 字节。

- 接入：正式表已用；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_blade_burst_v1.json) / [制作说明](boss-blade-burst-v1.md) / [构建器](../../Assets/Editor/BossBladeBurstV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/BladeBurstModule.cs) / [表现组件1](../../Assets/Script/Game/Enemies/Boss/BossBladeBurstPresentation.cs) / [表现组件2](../../Assets/Script/Game/Enemies/Boss/BossBladeProjectilePresentation.cs)
- 构建：`工具/VFX/Build Boss Blade Burst V1`；检查：`工具/VFX/检查Boss刀气爆发配置`。
- 实战：`工具/VFX/实战验收/Boss刀气爆发`；phase：`boss-blade-burst`；[夹具](../../Assets/ArtPreview/Skills/BossBladeBurstValidationRunner.cs)。
- [归档实战 89 项](../../ArtOutput/UI/BossBladeBurstV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossBladeBurstV1/Audit/configuration.json) / [原速录像](../../ArtOutput/UI/BossBladeBurstV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossBladeBurstV1/comparison.png)
- 证据工具：[录像导出](render-boss-blade-burst-proof.cjs) / [该版本完整性检查](audit-boss-blade-burst.cjs)（使用固定历史目录，运行前核对输出范围）。
- 边界：手动 CloneMarker 压力样本不证明真实分身继承；使用后续分身报告。
- 边界：微信真机与最终包体增量未测量。

### 冲刺 · boss_dash_v1

身体包络路线、箭头、后沿速度线与终点环。复用实体材质，0 新贴图 / 材质。

- 接入：正式表已用；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_dash_v1.json) / [制作说明](boss-dash-knife-v1.md) / [构建器](../../Assets/Editor/BossDashV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/DashModule.cs) / [表现组件](../../Assets/Script/Game/Enemies/Boss/BossDashPresentation.cs)
- 构建：`工具/VFX/Build Boss Dash V1`；检查：`工具/VFX/检查Boss冲刺配置`。
- 实战：`工具/VFX/实战验收/Boss冲刺与小刀`；phase：`boss-dash-knife`；[夹具](../../Assets/ArtPreview/Skills/BossDashKnifeValidationRunner.cs)。
- [归档实战 79 项](../../ArtOutput/UI/BossDashKnifeV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossDashKnifeV1/Audit/dash-configuration.json) / [原速录像](../../ArtOutput/UI/BossDashKnifeV1/after-dash15-motion.mp4) / [对比图](../../ArtOutput/UI/BossDashKnifeV1/comparison.png)
- 证据工具：[录像导出](render-boss-dash-knife-proof.cjs) / [该版本完整性检查](audit-boss-dash-knife.cjs)（使用固定历史目录，运行前核对输出范围）。
- 后续归档回归：[79 项](../../ArtOutput/UI/BossCloneV1/RegressionDashKnife/validation-report.json)。
- 边界：保留原 0.3 秒末锁向、15 / 20 速度及 8 距离。
- 边界：原单次 OverlapCircle 可能返回自身；前后隔离额外 hpDelta 均为 0，不视作 30 点技能伤害已命中。

### 小刀 · boss_knife_v1

朱红短刃、文字前沿出手闪、短柔边尾迹。复用刀气图集和消散，0 新贴图 / 材质。

- 接入：正式表已用；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_knife_v1.json) / [制作说明](boss-dash-knife-v1.md) / [构建器](../../Assets/Editor/BossKnifeV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/HomingKnifeModule.cs) / [表现组件1](../../Assets/Script/Game/Enemies/Boss/BossKnifePresentation.cs) / [表现组件2](../../Assets/Script/Game/Enemies/Boss/BossBladeProjectilePresentation.cs)
- 构建：`工具/VFX/Build Boss Knife V1`；检查：`工具/VFX/检查Boss小刀配置`。
- 实战：`工具/VFX/实战验收/Boss冲刺与小刀`；phase：`boss-dash-knife`；[夹具](../../Assets/ArtPreview/Skills/BossDashKnifeValidationRunner.cs)。
- [归档实战 79 项](../../ArtOutput/UI/BossDashKnifeV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossDashKnifeV1/Audit/knife-configuration.json) / [原速录像](../../ArtOutput/UI/BossDashKnifeV1/after-knife-motion.mp4) / [对比图](../../ArtOutput/UI/BossDashKnifeV1/comparison.png)
- 证据工具：[录像导出](render-boss-dash-knife-proof.cjs) / [该版本完整性检查](audit-boss-dash-knife.cjs)（使用固定历史目录，运行前核对输出范围）。
- 后续归档回归：[79 项](../../ArtOutput/UI/BossCloneV1/RegressionDashKnife/validation-report.json)。
- 边界：实际 turnRate=0，仍为直飞。
- 边界：Trigger 墙体不走原 Collision 回收分支，未宣称撞墙消散。

### 分身 · boss_clone_v1

真实字形重影、角色统一半透明、主身实印与分身断续印；修正继承白名单与池身份。0 新贴图 / 材质。

- 接入：正式表已用；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_clone_v1.json) / [制作说明](boss-clone-v1.md) / [构建器](../../Assets/Editor/BossCloneV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/CloneModule.cs) / [表现组件](../../Assets/Script/Game/Enemies/Boss/BossClonePresentation.cs)
- 构建：`工具/VFX/Build Boss Clone V1`；检查：`工具/VFX/检查Boss分身配置`。
- 实战：`工具/VFX/实战验收/Boss分身`；phase：`boss-clone`；[夹具](../../Assets/ArtPreview/Skills/BossCloneValidationRunner.cs)。
- [归档实战 90 项](../../ArtOutput/UI/BossCloneV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossCloneV1/Audit/configuration.json) / [原速录像](../../ArtOutput/UI/BossCloneV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossCloneV1/comparison.png)
- 证据工具：[录像导出](render-boss-clone-proof.cjs) / [该版本完整性检查](audit-boss-clone.cjs)（使用固定历史目录，运行前核对输出范围）。
- 后续归档回归：[100 项](../../ArtOutput/UI/BossReviveV1/RegressionClone/validation-report.json)。
- 边界：攻击预警与弹体不能跟随角色一起降低透明度。
- 边界：必须用真实 CloneModule / Start / 重建检验继承，不能只手工放标记对象。

### 召唤 · boss_summon_v1

0.45 秒蓄力符环与真实成功出生点门印，保留预留波次数量、属性与时钟。0 新贴图 / 材质。

- 接入：正式表已用；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_summon_v1.json) / [制作说明](boss-summon-v1.md) / [构建器](../../Assets/Editor/BossSummonV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/SummonModule.cs) / [表现组件](../../Assets/Script/Game/Enemies/Boss/BossSummonPresentation.cs)
- 构建：`工具/VFX/Build Boss Summon V1`；检查：`工具/VFX/检查Boss召唤配置`。
- 实战：`工具/VFX/实战验收/Boss召唤`；phase：`boss-summon`；[夹具](../../Assets/ArtPreview/Skills/BossSummonValidationRunner.cs)。
- [归档实战 70 项](../../ArtOutput/UI/BossSummonV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossSummonV1/Audit/configuration.json) / [原速录像](../../ArtOutput/UI/BossSummonV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossSummonV1/comparison.png)
- 证据工具：[录像导出](render-boss-summon-proof.cjs) / [该版本完整性检查](audit-boss-summon.cjs)（使用固定历史目录，运行前核对输出范围）。
- 后续归档回归：[70 项](../../ArtOutput/UI/BossReviveV1/RegressionSummon/validation-report.json)。
- 边界：门印取最终受约束的真实生成位置；失败生成不出空门。
- 边界：已释放波次与施法者生命周期分开，旧回调不能污染新池租约。

### 复活 · boss_revive_v1

同一真实字形散开 / 回聚、余烬核心与真实保护弧；散开位移 1.6 倍，原回聚时间保持。修正动态盾缓存漏接，0 新贴图 / 材质。

- 接入：正式表已用；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_revive_v1.json) / [制作说明](boss-revive-v1.md) / [构建器](../../Assets/Editor/BossReviveV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/ReviveModule.cs) / [表现组件](../../Assets/Script/Game/Enemies/Boss/BossRevivePresentation.cs)
- 构建：`工具/VFX/Build Boss Revive V1`；检查：`工具/VFX/检查Boss复活配置`。
- 实战：`工具/VFX/实战验收/Boss复活`；phase：`boss-revive`；[夹具](../../Assets/ArtPreview/Skills/BossReviveValidationRunner.cs)。
- [归档实战 90 项](../../ArtOutput/UI/BossReviveScatterV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossReviveV1/Audit/configuration.json) / [原速录像](../../ArtOutput/UI/BossReviveScatterV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossReviveScatterV1/comparison.png)
- 证据工具：[录像导出](render-boss-revive-scatter-proof.cjs) / [该版本完整性检查](audit-boss-revive-scatter.cjs)（使用固定历史目录，运行前核对输出范围）。
- 后续归档回归：[88 项](../../ArtOutput/UI/BossZoneV1/RegressionRevive/validation-report.json)。
- 边界：保留按当次 MaxHp 递减的恢复公式、等待、次数与可受伤恢复窗口。
- 边界：当前是 TMP 字形快照，后续 Spine 主体需要新的身体表现适配。

### 地裂 · boss_zone_v1

世界空间朱红预警、真实判定裂纹与碎石，0.9 秒残痕；延迟创建并复用网格。0 新贴图 / 材质。

- 接入：能力完成，未配表；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_zone_v1.json) / [制作说明](boss-zone-v1.md) / [构建器](../../Assets/Editor/BossZoneV1Builder.cs) / [技能模块](../../Assets/Script/Game/Enemies/Boss/Skills/ZoneModule.cs) / [表现组件](../../Assets/Script/Game/Enemies/Boss/BossZonePresentation.cs)
- 构建：`工具/VFX/Build Boss Zone V1`；检查：`工具/VFX/检查Boss地裂配置`。
- 实战：`工具/VFX/实战验收/Boss地裂`；phase：`boss-zone`；[夹具](../../Assets/ArtPreview/Skills/BossZoneValidationRunner.cs)。
- [归档实战 29 项](../../ArtOutput/UI/BossZoneV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossZoneV1/Audit/configuration.json) / [原速录像](../../ArtOutput/UI/BossZoneV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossZoneV1/comparison.png)
- 证据工具：[录像导出](render-boss-zone-proof.cjs) / [该版本完整性检查](audit-boss-zone.cjs)（使用固定历史目录，运行前核对输出范围）。
- 边界：正式 Monster / LevelWave 未配 zone；验收使用临时注入，不代表已加入关卡。
- 边界：单圈预警约 0.14→0.7 秒，默认末击约 0.7→1.26 秒，是明确玩法调整。
- 边界：单轮 2～32 圈；不额外钳制跨越墙边的圆。

### 公共头顶标识 · boss_head_crest_v1

紧凑金橙冠徽、分身空心标记；替换旧双刀装饰，保留真实发射节点。0 新贴图 / 材质。

- 接入：公共 Prefab 已接入；配方记录状态：`integrated-editor-verified`。
- [配置](Library/Boss/boss_head_crest_v1.json) / [制作说明](boss-head-crest-v1.md) / [构建器](../../Assets/Editor/BossHeadCrestV1Builder.cs) / [表现组件](../../Assets/Script/Game/Enemies/Boss/BossHeadCrestPresentation.cs)
- 构建：`工具/VFX/Build Boss Head Crest V1`；检查：`工具/VFX/检查Boss头顶标识配置`。
- 实战：`工具/VFX/实战验收/Boss分身`；phase：`boss-clone`；[夹具](../../Assets/ArtPreview/Skills/BossCloneValidationRunner.cs)。
- [归档实战 100 项](../../ArtOutput/UI/BossHeadCrestV1/After/validation-report.json) / [配置报告](../../ArtOutput/VFX/BossHeadCrestV1/Audit/configuration.json) / [原速录像](../../ArtOutput/UI/BossHeadCrestV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossHeadCrestV1/comparison.png)
- 证据工具：[录像导出](render-boss-head-crest-proof.cjs) / [该版本完整性检查](audit-boss-head-crest.cjs)（使用固定历史目录，运行前核对输出范围）。
- 后续归档回归：[100 项](../../ArtOutput/UI/BossReviveV1/RegressionClone/validation-report.json)。
- 边界：沿用真实分身夹具进行头标检查，没有单独的 Boss头标 实战菜单。
- 边界：已有显式头部 / 身体绑定入口，不等于 Spine 角色已接入。

## 后续项

- **独立免伤护盾技能**（`not-redesigned-not-table-connected`）：复活保护弧与真实复活盾已完成；独立 resist 技能的持续壳体、缓存、减伤类型和耗次仍需单独改版验收。 [代码](../../Assets/Script/Game/Enemies/Boss/Skills/ResistModule.cs)
- **Spine Boss 主体**（`planned`）：头部绑定入口已保留；身体动画、出手、命中、拖尾、分身与复活表现适配尚未实现。
- **微信真机与发布包**（`not-measured`）：固定机型、场景和构建设置后再记录帧时、内存、过绘及包体增量。

## 当前目录核对

[静态检查报告](../../ArtOutput/VFX/BossWorkflow/Audit/catalog-check.json)检查目录完整性和历史报告字段；不替代重新编译、Prefab 构建、实战或真机验证。
