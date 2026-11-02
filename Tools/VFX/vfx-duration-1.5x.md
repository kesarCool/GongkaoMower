# 新版战斗特效时长调整

2026-10-25。新版爆炸、普通落雷、红色突破落雷及符箓命中均已延长为原来的 **1.5 倍**，共 8 个预制体、10 个粒子层。

## 调整方式

这些效果在第 0 秒一次性发射粒子，序列帧按粒子生命周期播放。因此只改 Particle System 的 `Duration` 不够。本次同步将 `Main.duration`、`Main.startLifetime` 和 `PooledCombatVfx.lifetimeOverride` 乘以 1.5，让帧动画、透明度变化和回池时间一起延长。`simulationSpeed` 保持 1，发射延迟保持 0。

| 预制体 | 粒子播放时长（秒） | 回池时间（秒） |
| --- | --- | --- |
| fx_explosion_compact_v2 | 0.40 → 0.60 | 0.44 → 0.66 |
| fx_explosion_chain_v2 | 0.30 → 0.45 | 0.34 → 0.51 |
| fx_explosion_missile_v2 | 0.32 → 0.48 | 0.36 → 0.54 |
| fx_explosion_heavy_v2 | 0.48 → 0.72 | 0.52 → 0.78 |
| fx_explosion_echo_v2 | 0.22 → 0.33 | 0.26 → 0.39 |
| fx_talisman_hit_v1 | 0.18 → 0.27 | 0.22 → 0.33 |
| fx_lightning_v1 主雷 / 余电 | 0.16 / 0.30 → 0.24 / 0.45 | 0.34 → 0.51 |
| fx_lightning_breakthrough_v2 主雷 / 余电 | 0.18 / 0.32 → 0.27 / 0.48 | 0.36 → 0.54 |

同一个落雷预制体的两层同时开始，表中两层时长不相加。符纸飞行、拖尾、技能冷却、伤害、连击间隔、回响触发时机均保留原值。

## 重建与资源

四个现有生成器共用 [CombatVfxTimingProfile.cs](../../Assets/Editor/CombatVfxTimingProfile.cs) 的 `DurationScale = 1.5f`。生成器保留基础参数，在构建时应用倍率，重复构建不会再次累乘。当前 8 个预制体已直接更新，无需手动重建。修改这个常量后，需要重新生成对应预制体才能生效；它不是运行时调速开关。

未新增贴图或材质，纹理大小不变。持续时间变长会增加同时存活的特效数量，现有每帧 8 个、活动 32 个的限制保持原值。相同落雷叠加样本中，观测到雷电粒子系统组件峰值由 4 增至 6、存活粒子峰值由 3 增至 5；这是本次回放的观测值，不代表理论上限或设备帧率。

## 验证与回放

运行验证共 **178 项通过**：爆炸 56 项、符箓 50 项、落雷与突破 72 项。覆盖实际碰撞伤害、等级与突破切换、暂停、对象池回收复用和场景清理。测试使用内存存档，PlayerPrefs 保留原值。原生快照确认所有目标时长精确为原值的 1.5 倍。

完整性检查将新旧预制体逐一比对，除时长字段外内容一致；贴图帧曲线、透明度曲线、材质、尺寸、变换及粒子数量限制保持原值。

- [实际新时长回放 MP4](../../ArtOutput/UI/Timing15/runtime-1.5x.mp4) / [GIF](../../ArtOutput/UI/Timing15/runtime-1.5x.gif)：由本次运行重新录制，未将旧视频慢放。
- [原生参数核对](../../ArtOutput/VFX/Timing15/Audit/after.json) / [预制体完整性检查](../../ArtOutput/VFX/Timing15/Audit/integrity.json)。
- [爆炸运行报告](../../ArtOutput/UI/Timing15/Explosions/validation-report.json)、[符箓运行报告](../../ArtOutput/UI/Timing15/Talisman/validation-report.json)、[落雷运行报告](../../ArtOutput/UI/Timing15/Lightning/validation-report.json)。

未执行微信真机帧率或完整上传包测量。先前版本文档及报告保留其原始参数，本页记录当前生效时长。
