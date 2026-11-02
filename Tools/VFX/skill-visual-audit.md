# Active skill visual audit

本轮审查覆盖当前 12 个主动技能的实现、定义资产与引用的 Prefab。**这是静态代码和资源审查，不是逐项实机验收**；各技能的实际屏幕大小、叠加遮挡、设备性能仍需对应的运行验证。

**本轮只落实「气贯长虹」的表现升级。** 其余项目均为后续建议，不代表已经修改、接入或验收。伤害、冷却、索敌、穿透、羁绊与突破规则不属于本清单的表现调整范围。

## Priority

| 优先级 | 技能 | 当前表现依据 | 后续表现建议 |
| --- | --- | --- | --- |
| P0，本轮 | 气贯长虹 | 原实现为纯色两点 LineRenderer；Player Prefab 的宽度与显示时长均为 0.08，满级每 5 次释放触发 7 色射线 | 用亮芯、束体、低透明外缘、束头和有限命中星芒形成贯穿感；让光束有快速出现与消散层次，不为视觉蓄力延迟原伤害结算 |
| P1 | 刀光剑影 | 普通飞刀为单 Sprite；满级飞刀、环绕爆发弹有短 Trail，支持暴击分裂 | 给普通飞刀补短刀芒，出手小弧光与命中斩痕；突破通过轮廓、轨迹、节奏强化，不仅增加粒子数量 |
| P1 | 百步穿杨 | 普通弹与满级散射弹均为单 Sprite，无 Trail | 短枪口闪光、极短弹道残影、金属命中火星；高频弹复用轻量反馈，不给每发弹增加完整粒子系统 |
| P1 | 天师符令 | 普通符纸单 Sprite；满级及风暴符有 Trail，突破两波追踪 | 符纸翻动、方向变化、少量符纹残迹、命中短印记；用轮廓与色调区分飞刀和子弹 |
| P1 | 石破天惊 | 手雷抛物线与自转，落地使用共享火焰粒子爆炸 | 小落点阴影、落地重音、爆炸亮核与扩散环，少量碎屑；视觉覆盖应对应实际伤害范围 |
| P1 | 连环爆破 | 熊猫手雷弹跳追敌，复用普通手雷同一火焰爆炸；突破带延迟余爆 | 弹跳压缩回弹、短连接轨迹、主爆与余爆分层，让玩家读出连环过程 |
| P1 | 罡气护体 | 程序化六边形渐变底盘与 3 个扩散圆环；普通亮黄、突破紫红 | 减少常驻底盘的抢眼程度，强调边界与命中脉冲，避免持续遮住文字怪 |
| P1 | 追风逐日 | 导弹 Sprite 与 Trail、多目标连发；命中用单 Sprite 放大的 Missilefx，该 Prefab 的 minAlpha 为 1 | 区分齐射与爆炸轮廓；给爆炸合理消散，避免停留后突然消失 |
| P2 | 弹无虚发 | 普通与满级弹均为单 Sprite，自动直线发射 | 复用百步穿杨的短弹道和命中火星，保持简洁的基础攻击身份 |
| P2 | 回风舞柳 | 刀片公转与自转，每刀一条短 Trail；冷却时整刀直接隐藏 | 刀刃扫光、命中短弧、冷却进出淡化；已有基础较完整，无需堆叠粒子 |
| P2 | 穿云破雾 | 导弹 Sprite 与 Trail、追踪后使用共享火焰爆炸 | 发射尾焰、转弯尾迹收细、落点爆炸层次，保留清楚的追踪轨迹 |
| P2 | 雷霆万钧 | 逐道落雷，间隔 0.18 秒；当前落雷 Prefab 包含 9 个粒子系统 | 优先精简层数，强化落点、闪击、余电的节奏，不在已有复杂特效上继续追加大面积覆盖 |

以上优先级综合可见性、角色常用攻击、现有表现完成度与地图中文字可读性提出，不代表对效果好坏做过设备对比测量。

## Source references

以下路径均相对工程根目录 `G:/Demo/GongkaoMower`。

### Beam

- `Assets/Script/Game/Combat/Skills/Implementations/SkillLineBeam2D.cs`：原来的射线颜色、即刻 Raycast 伤害、显示时长和关闭过程。
- `Assets/Script/Game/Player/Components/PlayerSkills.cs`：`EnsureBeamVisual` 与 `ConfigureBeamLineRenderer`，原来按需创建 8 个束线对象，七色变体使用其中 7 根。
- `Assets/Prefab/Player/Player.prefab`：原配置 `beamVisualWidth: 0.08`、`beamVisualDuration: 0.08`。
- `Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LineBeam.asset`：气贯长虹各等级参数与七色变体参数。

上述是升级前基线描述；当前实现可能已由本轮气贯长虹升级替换，具体交付和验证见同目录对应的升级说明。

### Auto projectiles

- `Assets/Script/Game/Combat/Skills/Implementations/SkillAutoProjectile.cs`：普通自动发射、散射、满级 Prefab 与环绕爆发。
- `Assets/Script/Game/Combat/Skills/Implementations/SkillAutoProjectileSword.cs`、`SkillAutoProjectilePistol.cs`、`SkillAutoProjectileTalisman.cs`：飞刀、枪弹、符箓突破差异。
- `Assets/Script/Game/Combat/Projectiles/PlayerBullet.cs`：通用命中与回收入口，基类未添加独立命中 VFX。
- `Assets/Prefab/Combat/playerBullet/`：普通飞刀、普通枪弹、普通符纸无 Trail；满级飞刀、满级符纸与两种爆发弹含 Trail。

### Grenades

- `Assets/Script/Game/Combat/Skills/Implementations/SkillThrowGrenade.cs`。
- `Assets/Script/Game/Combat/Projectiles/GrenadeProjectile.cs`、`BouncingGrenade.cs`：抛物线、自转、池化爆炸、弹跳和延迟余爆。
- `Assets/Prefab/Combat/Grenade.prefab`、`PandaGrenade.prefab`：本体为单 Sprite。
- `Assets/Res/Prefabs/Effects/Impact/fx_impact_fire_plus.prefab`：两类手雷及穿云破雾共享的火焰爆炸，当前为 1 个粒子系统。

### Field and orbit

- `Assets/Script/Game/Combat/Skills/Implementations/SkillFieldGenerator.cs`：实际运行的程序化底盘和 3 个脉冲圆环。
- `Assets/Script/Game/Combat/Skills/Definitions/FieldGeneratorSkillDefinition.cs`：定义虽然保留 `fieldVisualPrefab` 字段，但创建运行时技能时没有使用它，**只修改那份粒子 Prefab 不会改变当前护体效果**。
- `Assets/Script/Game/Combat/Skills/Implementations/SkillOrbitingBlades.cs`：刀片数量、轨道、冷却与显隐。
- `Assets/Script/Game/Combat/Visuals/OrbitingBladeVisual.cs`：自转、拖尾与池化重置。
- `Assets/Prefab/Combat/OrbitingBlade.prefab`、`OrbitingBladeMax.prefab`：各为 1 Sprite 与 1 Trail，无额外粒子系统。

### Missiles and lightning

- `Assets/Script/Game/Combat/Skills/Implementations/SkillHomingMissile.cs`、`Assets/Script/Game/Combat/Projectiles/HomingMissileBullet.cs`：追踪、齐射、命中范围爆炸。
- `Assets/Prefab/Combat/playerBullet/MissileBasicBullet.prefab`、`MissileBasicMaxBullet.prefab`、`HomingMissileBullet.prefab`、`HomingMissileMaxBullet.prefab`：均包含 Sprite 与 Trail。
- `Assets/Prefab/Combat/Missilefx.prefab`：追风逐日使用的 Sprite 爆发，带 `BurstVfx`，当前 `minAlpha: 1`。
- `Assets/Script/Game/Combat/Skills/Implementations/SkillLightningStrike.cs`：逐道释放，通过统一 VFX 入口播放。
- `Assets/Res/Prefabs/Effects/fx_lightning.prefab`：当前包含 9 个粒子系统。

## Shared resource and performance direction

- 优先共享亮点、短划、柔光、扩散环等少量小资源，以颜色、轮廓、时序区分技能，避免每个技能各做一套大序列帧。
- 低透明外缘只提供衬托，亮芯和命中点承担辨识；持续场和大面积爆炸要给文字留出阅读空间。
- 限制同时存在的命中闪点和残迹；战斗暂停、技能卸下、角色重建、场景卸载均应清理表现，不能残留束线或亮点。
- 共用材质，优先沿用现有池化入口 `CombatVfxSpawner`、`GameObjectPool` 和 `SpawnLimiter`，避免为每次命中分配材质。
- 追踪导弹当前仍在 `SkillHomingMissile.FireSalvoRound` 直接 Instantiate，本轮未改；后续提高齐射密度时应一起检查池化与分配。
- 新表现不新增字体，不让装饰覆盖文字怪，不通过延迟表现修改原伤害、冷却或索敌规则。

## Verification boundaries

本清单只确认上述代码路径与资源结构。未统计真实渲染批次、GPU 过绘、移动端帧时、最终微信构建增量，也未据此断言某个技能存在性能瓶颈。后续每项升级均应保存实际运行前后画面，并在多技能叠加场景下验证可读性。
