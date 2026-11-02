# 石破天惊：卡通爆炸序列帧 V1

> 历史记录：此版本已于 2026-10-25 被 [共享爆炸库 V2](explosion-library-v2.md) 替代。V1 运行资产与预览源码已移出 Assets 归档，原始生成图和本页预览证据保留；当前重建与预览入口以 V2 说明为准。

日期：2026-10-25。完成原创素材生成、Unity 接入及 Editor 运行验证。

## 最终表现与接入范围

石破天惊落地后播放橙金色亮核、圆润火团、少量碎石与烟团，再迅速消散。主体动画 0.44 秒，0.48 秒回池。特效大小随实际 AOE 半径缩放；排序 -5，位于当前地图地面/装饰之上、文字怪和角色之下。

本轮只修改 `SkillDef_ThrowGrenade.asset` 的爆炸 Prefab 引用。手雷伤害、冷却、飞行、索敌、碰撞、暴击及升级数值未修改；连环爆破和导弹继续引用原资源。

`CombatVfxSpawner` 增加带可选范围信息的重载；原三参数入口保留。`PooledCombatVfx` 增加默认值为 0 的 `referenceRadius`，只有显式声明参考半径的新特效按范围缩放，回池恢复原缩放，旧 Prefab 保持原尺寸。普通、弹射及余爆调用点传入各自已有的伤害半径，没有修改伤害结算。新爆炸参考半径为 1.2。

## 正式资产

| 类型 | 工程相对路径 |
| --- | --- |
| 图集 | `Assets/Res/Textures/VFX/sheet_grenade_explosion_v1.png` |
| 材质 | `Assets/Res/Materials/VFX/mat_grenade_explosion_v1.mat` |
| Prefab | `Assets/Res/Prefabs/Effects/Impact/fx_grenade_explosion_v1.prefab` |
| 技能绑定 | `Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_ThrowGrenade.asset` |
| 生成原图及修正版 | `ArtOutput/VFX/GrenadeExplosionV1/` |
| 完整初始/修正提示词 | [prompts.md](../../ArtOutput/VFX/GrenadeExplosionV1/prompts.md) |

使用内置 `image_gen`，`transparent_background=true`；修正版引用初版图集调整构图。没有使用外部 API/CLI，没有使用网友的 PNG 作为运行资产。初版与修正版均保留，正式选用修正版。

源文件实际为 1254×1254 RGBA，835,455 字节；Unity 导入后为 1024×1024，4×4 图集。不把提示词要求的输出尺寸当作实际文件尺寸。使用真实 alpha、Clamp、Bilinear、关闭 mipmap 与 Read/Write；WebGL 沿用现有火焰图集的 ETC2_RGBA8Crunched 策略。材质使用工程已确认存在的 `Sprites/Default`，共用一个材质。

这些是源文件和导入设置，不是微信最终包体增量，也不是设备显存测量。

## 运行验证

通过 [validation-report.json](../../ArtOutput/UI/GrenadeExplosionV1/validation-report.json) 的 22 项检查。使用真实 Game 场景、玩家、技能定义、enemy001 碰撞体和实际伤害入口；固定目标、关闭自动刷怪、使用现有内存存档夹具。

- 原一级冷却 3 秒、伤害 2、半径 1.2；到冷却前不发射，真实投掷不立即扣血，落地后范围内目标各扣原伤害，范围外目标不受伤。
- 实际落点生成新特效，弹体和特效分别回池。
- 大小随半径变化；重复借还同一实例无缩放累乘，播放时间重置，材质复用。
- 特效生成不消耗战斗的全局随机状态；旧火焰 Prefab 保持原尺寸。
- 暂停后粒子时间维持 0.07031339 秒，回收计时也冻结。检查在暂停切换生效一帧后采样，避免将切换帧已缓存的 deltaTime 当作持续推进。初轮这项采样时机问题与其余通过记录保留在 `ArtOutput/VFX/GrenadeExplosionV1/initial-validation.json`；调整夹具后完整复跑通过。
- 一帧 40 次请求接受 8 次，符合现有限流；存活数量受限，结束后归还并注销；退出战斗清理已借出的特效。
- 玩家持久存档保持，测试期间未捕获运行错误。

编译通过。控制台原有 `AudioService` 场景关闭清理错误在本轮首次停止原运行场景时、修改代码之前已经观察到；本轮未修改该服务，也没有清空控制台。这里的“测试期间无运行错误”不代表整个工程控制台没有历史错误。

## 预览证据

- [游戏内动图](../../ArtOutput/UI/GrenadeExplosionV1/runtime-cast.gif)
- [游戏内视频](../../ArtOutput/UI/GrenadeExplosionV1/runtime-cast.mp4)
- [前后对比](../../ArtOutput/UI/GrenadeExplosionV1/comparison.png)
- [爆炸静态图](../../ArtOutput/UI/GrenadeExplosionV1/impact-1080x1920.png)
- [尾烟静态图](../../ArtOutput/UI/GrenadeExplosionV1/tail-1080x1920.png)

动图/视频由 34 张真实运行截图按记录的时间间隔合成，其中 10 帧存在爆炸实例；记录一次实际发射、飞行、命中和消散，没有生成或补绘战斗帧。静态对比在真实伤害发生后，将两套粒子明确采样到 0.075 秒；尾烟图采样到 0.29 秒。静态图用于检查轮廓和透明度，不代表自然播放中恰好抓取到该时刻。

检查覆盖当前浅色沙地与固定目标，不等于全地图、多技能完整战斗或微信真机验收。尚未测量微信真机帧时、GPU 过绘、发布构建增量。

## 复现与维护

`GrenadeExplosionV1Builder.Build()` 重建本版本材质、Prefab 和技能引用；Unity 菜单为“工具/VFX/Build Grenade Explosion V1”。先退出 Play Mode。

`HomeUIEditorPreview.Begin("grenade-explosion")` 启动隔离运行验证；结束后调用 `HomeUIEditorPreview.Finish()` 恢复原编辑器场景和 Game View 尺寸。生成预览媒体：

```powershell
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' 'Tools\VFX\render-grenade-explosion-proof.cjs'
```

该脚本只在验证报告通过后组合真实截图，使用本机现有 sharp 与 ffmpeg；它不修改源图或战斗资产。移植机器时需调整脚本中的依赖路径。

本轮修改前的相关文件备份位于 `Library/VFXBackups/GrenadeExplosion-20261025/`，用于逐文件核对。工程已有大量其他未提交工作；回退时仅处理本轮相关改动，不能整体覆盖项目。
