# Boss 召唤 V1 · 敕令召唤

2026-10-29。第二批继分身之后，单独改版召唤表现。复活的旧美术保持，作为召唤中断兼容性验证对象。

## 表现与真实事件

Boss 蓄力仍为 0.45 秒，脚下朱红外环收拢，内圈反向旋转，六枚短符印环绕。释放后源符环在 0.36 秒内扩散淡出。每个成功出生的怪物，经过地图取点、竞技场约束、卡墙推出、属性和文字绑定之后，才在其实际世界出生位置出现 0.72 秒门印：深红边界、金橙内弧、短竖向裂隙与少量上升碎线。

怪物立即以原透明度和原碰撞生效，门印保持在出生位置，施法者和小怪后续移动不会拖走地面印。真实坐标在出生时才确定，本轮没有提前猜点或增加出场等待。该标记表达出生反馈，不是延迟生效的伤害圈。

## 配表与原行为

- 当前 13 个 Boss 配置使用 summon；冷却 12 / 15 秒，首次等待为间隔的 0.5 倍，读取预留波次 0。
- 真实关卡的预留怪物分别为第一章 10、第二章 12、第三章 15 个，原间隔 0.1 秒；攻、血、速、防均沿用 LevelWave。
- `SpawnerWaves` 原入口保留，新重载只增加成功生成后的可选回调。普通波次不传回调，不显示召唤门印，也不改变波次计数或 Boss 标记预算。
- 保留随机调用次数、顺序与时机；表现本身不调用 Unity Random，不提前抽取位置或挑词。
- 蓄力和门印动画使用缩放时间。释放后的波次继续沿用 `useRealtimeForWaves` 的原场景设置：为 false 时暂停刷怪，为 true 时已经释放的批次按真实时间调度。验收核对实际设置，未更改调度时钟。
- 蓄力中断、死亡、复活假死、禁用、回池及数据重建会取消未释放召唤和源符环。已经提交给 Spawner 的批次按旧规则继续，召唤物不随 Boss 回池而销毁；失效回调通过施法令牌过滤，避免给复用后的 Boss 添加旧门印。

现有目录缺失部分普通怪物 ID 时，Spawner 会使用原 `enemyPrefab` 回退，因此实例的 EnemyId 可能仍为 1001，但属性来自当前召唤表行、文字仍按请求的 Monster 生成。验收区分“表属性”和“当前目录 / 回退身份”，本轮不改目录、怪物类型或数值。

## 资源与制作入口

新增 PNG 0 字节，Texture 0，Material 0。复用现有 `mat_boss_blade_solid_v1`；每个 Boss 一个运行网格和 MeshRenderer，出生标记合批在同一网格内，不为每个门印创建 GameObject、ParticleSystem、Sprite 或材质。固定复用 32 个门印槽位；当前每批最多 15 只，超出槽位仅覆盖最旧装饰，不改变刷怪。网格在 Boss 销毁时释放，回池清空。

- [表现配置](Library/Boss/boss_summon_v1.json)，Editor 使用，运行时不读取 JSON。
- [运行表现](../../Assets/Script/Game/Enemies/Boss/BossSummonPresentation.cs)、[技能接入](../../Assets/Script/Game/Enemies/Boss/Skills/SummonModule.cs)、[真实刷怪回调](../../Assets/Script/Game/Spawning/SpawnerWaves.cs)。
- `工具/VFX/Build Boss Summon V1`：维护四个 Boss prefab 的 `BossSummonVisual`，保留已有技能、身份标记、物理和字体。
- `工具/VFX/检查Boss召唤配置`：共享材质、时长、排序和旧表现引用。
- `工具/VFX/实战验收/Boss召唤`：真实 Game、SummonModule 与 Spawner；`结束并恢复`还原场景、GameView 尺寸和验收开关。

## 验收资料

旧版 23 项检查记录真实表驱动召唤，区分回退实例身份与属性；出生记录在小怪第一次移动前采集，随后冻结移动供核对。

新版 70 / 70 项实战通过，覆盖三章 10 / 12 / 15 个真实出生、原 RNG 状态和精确世界位置、逐出生点门印、原暂停时钟、中断、真死、假死 / 复活、回池延迟回调、两种限流、多施法者、竞技场、普通四参数波次入口、刀气组合与重复施放网格复用。最终新版视频恢复真实 EnemyAI 移动；静态前后对比保留相同冻结夹具，避免把移动差异当作视觉改版。

100 / 100 项分身与头部节点回归、44 / 44 项导入预览工具检查通过；配置 9 / 9 项通过。四个 Boss prefab 重复构建逐字节一致，原组件字段、旧共享资源和非召唤技能源码保持；原普通波次四参数入口继续可用。

- [旧版报告](../../ArtOutput/UI/BossSummonV1/Before/validation-report.json) / [原随机与出生记录](../../ArtOutput/UI/BossSummonV1/Before/mechanics-observation.json)
- [新版报告](../../ArtOutput/UI/BossSummonV1/After/validation-report.json) / [新版出生记录](../../ArtOutput/UI/BossSummonV1/After/mechanics-observation.json)
- [配置检查](../../ArtOutput/VFX/BossSummonV1/Audit/configuration.json) / [资源说明](../../ArtOutput/VFX/BossSummonV1/Audit/resources.json)
- [新版实际回放](../../ArtOutput/UI/BossSummonV1/after.mp4) / [局部预览](../../ArtOutput/UI/BossSummonV1/after-closeup.mp4) / [静态对比](../../ArtOutput/UI/BossSummonV1/comparison.png) / [原时序导出记录](../../ArtOutput/UI/BossSummonV1/proof.json)
- [静态完整性](../../ArtOutput/VFX/BossSummonV1/Audit/integrity.json)：旧生成取点 / clamp / 推出 / 属性方法与原调度循环保持，四个 Boss 的原组件字段保持。
- [分身与头标回归](../../ArtOutput/UI/BossSummonV1/RegressionClone/validation-report.json) / [工具检查](../../ArtOutput/VFX/BossSummonV1/Audit/toolkit-report.json) / [重复构建](../../ArtOutput/VFX/BossSummonV1/Audit/rebuild.json) / [配置库审查](../../ArtOutput/VFX/BossSummonV1/Audit/library-audit.json)

微信真机帧时、内存和最终上传包尚未测量，Editor 实战不能替代设备验收。召唤单独交付观感验收后，再开始复活美术。
