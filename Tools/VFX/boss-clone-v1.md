# Boss 分身 V1

2026-10-28。第二批按“分身 → 验收 → 召唤 → 验收 → 复活”逐项实施。本轮只交付分身：修正真实技能继承与克隆身份，增加字形重影、统一透明度、本体 / 分身标识、出生和到期反馈。召唤、复活的视觉代码保持原样。

## 当前表现

- 本体蓄力 0.35 秒，文字向两侧产生短重影，脚下聚拢环提示分身释放。重影直接使用当前 TMP 的网格、UV 和共享字体材质，按实际字体 atlas 分组，不生成字体或位图。
- 本体为实心金色印记与单菱形；分身为朱红断续印记与双空心菱形。印记跟随真实文字中心，出生扩散环属于实际生成的分身，不预画尚未确定的随机落点。
- 分身的 TMP 文字与 Sprite 装饰统一使用 0.64 透明度，出生 0.42 秒渐入；到期前 0.32 秒轻淡并散出细碎线，仍保留可读轮廓和原始碰撞。
- 分身自身死亡或自然到期复用旧 Boss 小消散。本体死亡、回池、卸载时直接清理所属实例，避免退场重新产生装饰对象。
- 只调整分身本体的文字 / 装饰透明度；继承技能的预警网格和真实伤害弹体保持完整、清晰，不套用本体半透明或玩家装饰限流。

## 保持的配表与数值

| 项目 | 当前实际配置 |
| --- | --- |
| 分身间隔 / 首次等待 | 10 秒 / 间隔 × 0.6 |
| 蓄力 | 0.35 秒 |
| 每次生成 | 2 个 |
| 最大血量 | 本体 MaxHp × 0.35，向上取整 |
| 存活时间 | 15 秒，使用缩放时间；暂停不推进 |
| 随机出生 | 原环形分散：2.5–4.5 世界单位、±15° 抖动 |
| 伤害、移动、碰撞 | 保留克隆复制后的原值；不缩放物理根 |
| 两轮重叠 | 间隔 10 秒、存活 15 秒，允许原有的两轮重叠窗口 |

保留原随机抽样公式与抽样时机，不增加随机调用；没有新增地图边界重采样或改变出生距离。新增表现组件不创建 Texture 或 Material；克隆实体继续使用 Instantiate / Destroy，未将克隆塞进本体对象池。

## 必要的机制修正

旧版 `Instantiate` 后立即删模块，但新对象的 `BossBrain.Start` 随后重建全套技能，白名单失效。旧版真实对照已复现，且旧克隆复制了本体 `PooledObject.sourcePrefabId`。

现在在首次 Start 前设置 `CloneMarker` 的继承策略，`BossBrain.AddModule` 在模块 Init 及被动监听注册之前检查策略；后续 `OnEnemyDataReady` 重建继续使用同一策略。空串代表无主动技能；`*` 继承本体有的允许技能；大小写兼容；`clone`、`revive` 始终排除，不创建对应监听。

同时清空克隆复制的本体池身份、移除最终 Boss 标记。克隆死亡奖励计数为 0，避免增加杀敌进度；胜利计数仍由既有 CloneMarker / Boss 标记过滤。真正的本体对象保持原奖励逻辑。

模块重建先清理旧监听与所属克隆，避免重复 Start / 数据初始化积累监听。主身真死、禁用、回池和退场清理实例；主身复活假死在全部 OnDied 监听执行后再判断，避免监听顺序误删分身。本轮调用旧 ReviveModule 验证兼容性，未改其表现。

直接禁用已配置分身也清理其对象，不留下停止计时的不可见实体。实际到期和伤害死亡不进入本体池，不会把 CloneMarker 带到下一次真正的 Boss 生成。

## 资源与制作入口

本轮 **新增 PNG 0 字节、Texture 0、材质 0**。复用 `mat_boss_blade_solid_v1`、旧 8 帧 Boss 小消散和实际 TMP 的共享字体材质。每个参与角色新增一个身份网格；本体蓄力每个实际字体 atlas 使用两个重影网格，复用缓冲，结束隐藏、对象销毁时释放网格。

- [分身配置](Library/Boss/boss_clone_v1.json)：Editor 构建器读取，运行时不加载 JSON。
- Unity 菜单「工具/VFX/Build Boss Clone V1」维护四个兼容 Boss prefab 的 `BossCloneVisual` 子级，保留原物理、字体、技能参数和其他表现。
- 「工具/VFX/检查Boss分身配置」检查共享引用、视觉参数和旧刀气 / 冲刺 / 小刀入口。
- 「工具/VFX/实战验收/Boss分身」运行真实 Game 场景夹具；「结束并恢复」还原场景、窗口尺寸和验收开关。

普通一次性库仍为 10 项，独立 Boss 库增加分身后为 4 个配置。PNG 字节、运行时网格、最终微信上传包和真机帧时是不同指标；本轮未测量微信设备性能或最终包体增量。

## 验收证据

旧版 23 项为问题复现与视觉基线；新版 **90 项全部通过**，检查真实 CloneModule 生成、真实 Start、五组实际表继承（含本体多攻击技能的 Boss）、空白 / 通配 / 混合策略、真实死亡、复活假死、回池、暂停和禁用。没有用手动放置 CloneMarker 代替真实分身。

17 秒原时序回放包含两轮生成：峰值 4 个分身，第一轮按原 15 秒到期。分身的实际刀气保留 24 个预警方向和 24 发清晰弹体。

- [原始基线](../../ArtOutput/UI/BossCloneV1/Before/validation-report.json) / [分身实战](../../ArtOutput/UI/BossCloneV1/After/validation-report.json)
- [新版回放](../../ArtOutput/UI/BossCloneV1/after.mp4) / [局部预览](../../ArtOutput/UI/BossCloneV1/after-closeup.mp4) / [前后对比](../../ArtOutput/UI/BossCloneV1/comparison.png) / [时间戳导出记录](../../ArtOutput/UI/BossCloneV1/proof.json)
- [冲刺与小刀回归](../../ArtOutput/UI/BossCloneV1/RegressionDashKnife/validation-report.json)：79 项通过。
- [配置检查](../../ArtOutput/VFX/BossCloneV1/Audit/configuration.json) / [资源统计](../../ArtOutput/VFX/BossCloneV1/Audit/resources.json)
- [完整性检查](../../ArtOutput/VFX/BossCloneV1/Audit/integrity.json) / [重复构建](../../ArtOutput/VFX/BossCloneV1/Audit/rebuild.json) / [工具回归](../../ArtOutput/VFX/BossCloneV1/Audit/toolkit-report.json)：44 项工具检查通过，8 个本轮 prefab / meta 文件和 14 个共享文件重复构建一致。

分身先单独交付观感验收，下一项为召唤。当前结果不包含召唤、复活改版，也不代表其他已记录的冲刺伤害 / 小刀追踪问题已解决。
