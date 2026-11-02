# Boss 地裂 V1 · 赤脉地裂

2026-10-29。第三批地裂能力改版，四份兼容 Boss Prefab 已加入独立表现组件；Monster / LevelWave 的技能分配保持原样，当前正式表没有 `zone`。实战验收在真实 Game 场景和真实 Boss 上临时注入该模块，不将测试技能写回关卡。

## 表现与判定

深红细边圈、低透明填充和向内收缩的橙金倒计时，依次标明危险区。判定当帧撤掉危险边界，裂纹向外展开，带少量碎石与土尘；残痕用 0.9 秒退去。主色沿用敌方朱红 / 橙金，深边保证浅地图上的对比，不增加全屏闪白。

地裂落点属于世界坐标，不跟随 Boss 移动、缩放或字形抖动。外圈外沿就是实际 `OverlapCircleAll` 半径，玩家碰撞体与圆相交即可命中；并非按玩家中心点判定。爆裂后的微弱扩散环与碎石只作反馈，不追加伤害。

修正旧 fallback 在 `SetParent(boss,false)` 后产生的落点偏移、贴图直径换算错误和逐圈创建纹理的问题。维持施法开始时锁定的起点 / 扇形方向、150° 扇区、原距离随机公式、默认 5 处 / 半径 1.5 / 伤害 30 / 冷却 10 秒 / 首次系数 0.5，以及整轮对同一 PlayerHealth 最多结算一次的规则。

**预警时长是本轮明确的玩法调整。** 旧实现把 0.7 秒均分给 5 圈，约 0.14 秒一处；现在每处实际显示满 0.7 秒，出现间隔仍为 `0.7 / count`。默认名义爆发时刻为 0.70 / 0.84 / 0.98 / 1.12 / 1.26 秒，替代旧 0.14 / 0.28 / 0.42 / 0.56 / 0.70 秒。逐帧运行允许略晚，帧卡顿也不能缩短已显示的单圈预警。随机公式与抽样次数不变，抽样的发生时间随新的预警排期变化。

没有对地图边缘做额外钳制，也不把危险圆吸附到墙内；圆可能跨越原地图 / 竞技场边界，视觉与实际查询始终同点同半径。后续若要保证全部落点在墙内，应单独调整选点规则并验收难度，不能只挪视觉。

## 生命周期与预算

死亡（包括复活假死）、禁用、回池和模块重建取消未命中的落点及全部残痕；已结算的伤害不会撤回。取消不解除正在进行的复活状态锁。暂停使用游戏缩放时间，圆圈和碎石一起冻结。缺少可用表现组件 / 材质时拒绝生成隐形地裂伤害。危险预警不走装饰限流。

新增 PNG / Texture / Material 均为 0。复用 `mat_boss_blade_solid_v1`，每个 Boss 首次使用地裂才创建一份可复用 Mesh 和一个 MeshRenderer，不逐圈创建 GameObject、Sprite、Texture；残痕结束后不再逐帧构建空网格。网格使用 16 位索引，单轮数量规范为 2～32；超出上限的配置截为 32，避免意外配置造成无界几何增长。没有新增通用播放器或运行时 JSON 读取。Editor 代码和制作资料均在制作侧；微信真机帧时、最终包体增量尚未测量。

## 制作与验收入口

- [配置](Library/Boss/boss_zone_v1.json)：`residueSeconds` / `sortingOrder` 由构建器写入组件；`warningSeconds=0.7` 与 `maxZonesPerCast=32` 是必须匹配代码的契约字段。`defaultParameters` 仅为配表参数示例，不自动分配技能；改 JSON 不会自动改变正式资源。
- `工具/VFX/Build Boss Zone V1` → `检查Boss地裂配置` → `实战验收/Boss地裂` → `实战验收/结束并恢复`。
- [旧版报告](../../ArtOutput/UI/BossZoneV1/Before/validation-report.json) / [新版报告](../../ArtOutput/UI/BossZoneV1/After/validation-report.json)。报告包含实际伤害、中断、暂停、回池、范围、限流和缺失材质检查。
- [实际时序录像](../../ArtOutput/UI/BossZoneV1/after.mp4) / [局部录像](../../ArtOutput/UI/BossZoneV1/after-closeup.mp4) / [对比图](../../ArtOutput/UI/BossZoneV1/comparison.png)。按捕获时间戳编码，未加速或慢放。
- [配置检查](../../ArtOutput/VFX/BossZoneV1/Audit/configuration.json) / [资源预算](../../ArtOutput/VFX/BossZoneV1/Audit/resources.json) / [完整性检查](../../ArtOutput/VFX/BossZoneV1/Audit/integrity.json)。
- 运行时代码：[技能](../../Assets/Script/Game/Enemies/Boss/Skills/ZoneModule.cs)、[表现](../../Assets/Script/Game/Enemies/Boss/BossZonePresentation.cs)；[构建器](../../Assets/Editor/BossZoneV1Builder.cs)、[验收工具](../../Assets/ArtPreview/Skills/BossZoneValidationRunner.cs)。

当前截图与录像基于测试单技能，不能据此声称地裂已配到某个正式 Boss 或微信真机验收完成。

当前验证：旧版 7 项、新版 29 项实战（含空闲网格检查）、[复活回归 88 项](../../ArtOutput/UI/BossZoneV1/RegressionRevive/validation-report.json)、导入预览工具 44 项、地裂配置 12 项均已通过。完整性检查保留 281 个共享资源与旧代码的哈希，并核对四份 Prefab 的原组件字段；重复构建四份 Prefab 逐字节一致。
