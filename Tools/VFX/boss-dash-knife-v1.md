# Boss 冲刺与小刀 V1

2026-10-28。冲刺与小刀已接入 Boss 朱红、深红轮廓与少量暖白亮芯：冲刺增加身体包络路径、方向箭头、后沿速度线和终点环；小刀增加紧凑刀身、出手小闪、短尾和消散。旧版基线 **40 / 40**、新版 **79 / 79** 项 Editor 实战检查通过，工具回归 **44 / 44**，本轮新增 PNG、Texture、材质均为 0。

## 小刀

原来的 `homingKnife` 名称和表中预留转向参数保留。当前正式 `HomingBullet` 的 `turnRate=0`，本轮继续直飞，不恢复追踪、不增加锁定延迟。技能立即从 Boss 真实位置生成弹体；文字前沿的出手小闪仅用于视觉提示。

| 内容 | 本轮配置 |
| --- | --- |
| 真实技能间隔 | 按表保留 0.5 / 1 s；首次等待仍由原模块间隔系数决定 |
| 速度 / 寿命 | 4 / 5 s |
| 伤害 | 仍由原 `ResolveDamage` 和 `PlayerHealth.TakeDamage` 处理，未改表或伤害倍率 |
| 飞行方向 | 保留原出手时玩家方向；`turnRate=0` |
| 真实发射起点 | `boss.position`，无新偏移或新延迟 |
| 弹体碰撞 | 保留原半径 0.35 Trigger、根 scale、Rigidbody、wallMask、目标标签和全部 HomingBullet 序列化字段 |
| 新独立弹体 | `KnifeBulletV1.prefab`，新注册 key `KnifeBullet`；缺失时模块回退 `HomingBullet` |
| 刀身 | 复用刀气 Sprite，视觉子级 scale X=0.58、Y=0.72；根与物理不缩放 |
| 可见刀身长度 | 约 0.517 世界单位；刀气爆发约 0.802，小刀更短、更紧凑 |
| 拖尾 | 0.08 s、宽 0.075，暖白—朱红—深红渐隐；1 个 TrailRenderer |
| 出手闪 | 可见字形朝发射方向的前沿，0.12 s，单 MeshRenderer；即时播放，1 个动态 Mesh 重复使用 |
| 命中玩家 / 到期 | 复用既有 Boss 8 帧小消散，不在回池事件中额外生成消散 |
| 排序 | 出手闪 5，拖尾 9，刀身 10 |

`BossKnifePresentation.Flash(direction)` 从 `EnemyWordLabel.TryGetHitAnchor` 取得发射方向前沿可见字形；该查询只决定闪光位置。实际弹体仍在 Boss 中心生成，碰撞位置与目标计算保持原实现。出手闪在缩放时间下播放，暂停不推进；禁用、死亡、回池与复用清理几何。拖尾由 `BossBladeProjectilePresentation` 在启用、回池、取出和禁用时清空，避免跨屏连线。

只有挂可选表现组件的新弹体会播放小消散。`HomingBullet` 原有寻敌、转向、速度、伤害与寿命逻辑保留；到期回池后立即退出本次 FixedUpdate，避免继续写入已回池对象的速度或朝向。

小刀 Collider 仍为 `isTrigger=true`。原 `BossBullet` 只在 `OnCollisionEnter2D` 使用 wallMask，墙体触发事件不会按该分支回收弹体；本轮未改变穿墙或墙体碰撞机制。保留了实际产生 Collision 回调时的可选消散 hook，不把 `wallMask=-1` 当作已验证的小刀撞墙消散。冲刺使用独立的 Rigidbody Cast 截断路径，分别验收。

## 冲刺

沿用原 0.3 s 蓄力、按表 15 / 20 速度、8 距离和 0.2 s 收招。蓄力时朱红胶囊形路径跟随玩家，按原时机在蓄力末尾锁定方向；冲刺时保留整段锁定路线，将填充、边框和箭头淡化，并从文字可见后沿播放三条短速度线，终点以 0.2 s 双细环收尾。

| 内容 | 本轮配置 |
| --- | --- |
| 身体包络 | `max(1.2, Collider世界半径)`；当前 Boss 半径 1.5，完整路径宽 3，含端帽 |
| 路径样式 | 低透明朱红填充、0.055 深红边线与方向箭头 |
| 冲刺阶段 | 保留整段锁定路线；填充 alpha 0.025、边框 / 箭头 alpha 0.34 |
| 速度线 | 通过 `TryGetHitAnchor` 找文字可见后沿，再向后偏移 0.25；中央线长 1.25、两侧线长 0.85 |
| 材质 / 渲染 | 复用无贴图实体材质；单动态 Mesh、单 MeshRenderer |
| 锁向 | 跟随玩家至 0.3 s 蓄力结束；原技能方向计算时机保留 |
| 预判障碍 | 使用与原技能相同的 4-contact Cast 和墙体过滤；拥挤碰撞情况下远墙预判仍有原查询上限 |
| 配置 / Builder | [boss_dash_v1.json](Library/Boss/boss_dash_v1.json) / `BossDashV1Builder.Build()`、`Audit()` |
| 组件 | `Assets/Script/Game/Enemies/Boss/BossDashPresentation.cs` |

路径同时表达冲刺路线与 Boss 身体包络，避免细线给出过窄安全区。原 `CheckDashHit` 使用半径 1.2 的单次 `OverlapCircle`，可能先返回自身或其他碰撞体。本轮在保留真实 Boss / 玩家 Collider、关闭 AI 接触伤害的隔离观察中，15 / 20 速度的基线与新版技能额外 hpDelta 均为 0；新版最接近玩家距离分别约 1.696 / 1.599，查询可返回自身。该项通过表示结果与基线一致，**没有证明 30 点冲刺技能伤害已实际命中，也未修复原单次查询机制**。小刀则已在真实 BossBullet 玩家碰撞中验证原 20 点伤害。

死亡、禁用、中断与回池会清理阶段几何、旧拖尾和本次冲刺的忙状态，并清空排队的移动与速度。`Pool.Get` 复用时以 Transform 的新生成位置同步 Rigidbody，再覆盖旧移动目标，避免把旧租用位置带到新实例位置。正常收招通过独立的 `CompleteRecovery` 完成，不清空正常流程中的移动速度。阶段 token 用于阻止旧协程在取消或复用后继续移动、释放忙状态或干扰新阶段。

## 共享资源与边界

本轮冲刺与小刀**新增 PNG 0 字节、Texture 0 个、材质 0 个**。复用 `sheet_boss_blade_burst_v1.png`（512×256、28,653 字节）、3 个既有材质和 `fx_boss_blade_dissolve_v1.prefab`。冲刺只使用已有实体材质；不在运行时创建 Texture，不克隆字体材质，不把 Boss 弹体接入玩家装饰限流。短消散继续由现有装饰 VFX 池控制。

| 资源 | 入口 |
| --- | --- |
| 小刀全流程配置 | [boss_knife_v1.json](Library/Boss/boss_knife_v1.json) |
| 冲刺全流程配置 | [boss_dash_v1.json](Library/Boss/boss_dash_v1.json) |
| 图集与原始提示词 | [Boss 刀气制作说明](boss-blade-burst-v1.md) / [提示词](../../ArtOutput/VFX/BossBladeBurstV1/prompts.md) |
| 小刀 Builder | `Assets/Editor/BossKnifeV1Builder.cs` |
| 出手表现 | `Assets/Script/Game/Enemies/Boss/BossKnifePresentation.cs` |
| 配置审查 | `ArtOutput/VFX/BossDashKnifeV1/Audit/knife-configuration.json` |
| 资源报告 | `ArtOutput/VFX/BossDashKnifeV1/Audit/knife-resources.json` |
| 变更前备份 | `ArtOutput/VFX/BossDashKnifeV1/Baseline` |

构建与验收入口：

- Unity 菜单「工具/VFX/Build Boss Dash V1」读取冲刺配置，在 4 个兼容 Boss prefab 中维护 `BossDashVisual` 子级；「工具/VFX/检查Boss冲刺配置」核对视觉参数、共享材质、既有刀气及原小刀引用。
- 「工具/VFX/Build Boss Knife V1」读取小刀配置，创建独立弹体，在 4 个 Boss 中增补 key 与 `BossKnifeVisual`，保留其他资源库条目和已有刀气表现；「工具/VFX/检查Boss小刀配置」核对共享资源、原始物理和 Homing 字段、直飞状态、单刀身、短拖尾、出手闪和引用。
- 「工具/VFX/实战验收/Boss冲刺与小刀」运行真实 Game 场景验收夹具；使用「工具/VFX/实战验收/结束并恢复」结束并还原场景、开关与验收输出设置。配置由 Editor 构建器读取，运行时不加载 JSON。

## 统一验收与证据

旧版基线 40 项、新版 79 项全部通过。基线使用变更前 Boss prefab 和原 HomingBullet，沿用当前生命周期取消保护；它是视觉改版前后对照，不是旧工程完整快照回退。新版覆盖真实表构建、15 / 20 速度、0.3 s 跟随与原时机锁向、8 距离、0.2 s 收招、静态墙和实际地图边界截断、0.5 / 1 s 小刀、实际 20 点玩家碰撞、直飞与原寿命、暂停、中断 / 死亡 / 回池、跨屏尾迹、装饰限流饱和、随机状态、退场清理与存档保持。

中途回池后，同实例复用的 Busy、路径和几何均清空，首个物理步仍停在新的生成位置。8 向 / 16 向刀气与冲刺、小刀组合均保留真实时序回放；组合不是 CloneModule 真实生成、继承或初始化顺序验收。

冲刺配置 **9 / 9**、小刀配置 **10 / 10**、工具 **44 / 44** 项通过，完整性审查通过。重复构建比较 5 个本轮资产及其 meta 共 10 文件，并核对 12 个共享文件，合计 **22 文件逐字节一致**。原图集与 meta、3 份共享材质、BossBrain / BossBullet、Monster / LevelWave 及旧 HomingBullet 资源保持。本轮独立 Boss 库共 3 个配置；普通一次性库仍为 10 项。

- [旧版基线](../../ArtOutput/UI/BossDashKnifeV1/Before/validation-report.json) / [新版实战](../../ArtOutput/UI/BossDashKnifeV1/After/validation-report.json)
- [改版前回放](../../ArtOutput/UI/BossDashKnifeV1/before.mp4) / [新版实际时序回放](../../ArtOutput/UI/BossDashKnifeV1/after.mp4) / [局部回放](../../ArtOutput/UI/BossDashKnifeV1/after-closeup.mp4) / [前后对照](../../ArtOutput/UI/BossDashKnifeV1/comparison.png)
- [组合技能改版前](../../ArtOutput/UI/BossDashKnifeV1/combination-before.mp4) / [组合技能新版](../../ArtOutput/UI/BossDashKnifeV1/combination-after.mp4) / [回放导出记录](../../ArtOutput/UI/BossDashKnifeV1/proof.json)：按捕获时间编码，`retimed=false`。
- [冲刺配置](../../ArtOutput/VFX/BossDashKnifeV1/Audit/dash-configuration.json) / [冲刺资源](../../ArtOutput/VFX/BossDashKnifeV1/Audit/dash-resources.json)
- [小刀配置](../../ArtOutput/VFX/BossDashKnifeV1/Audit/knife-configuration.json) / [小刀资源](../../ArtOutput/VFX/BossDashKnifeV1/Audit/knife-resources.json)
- [完整性审查](../../ArtOutput/VFX/BossDashKnifeV1/Audit/integrity.json) / [重复构建检查](../../ArtOutput/VFX/BossDashKnifeV1/Audit/rebuild.json)
- [工具回归](../../ArtOutput/VFX/BossDashKnifeV1/Audit/toolkit-report.json)

源 PNG 字节数是共享源文件统计；最终微信上传包增量、真机帧时和设备内存需要另测。
