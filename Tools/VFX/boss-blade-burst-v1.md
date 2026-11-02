# Boss 周身刀气爆发 V1

2026-10-28。已接入当前 Boss 刀气爆发，旧版基线 **46 / 46**、新版 **89 / 89** 项 Editor 实战检查通过。橙红短刀气、深红轮廓与少量暖白亮芯区分敌我；蓄力时按实际 8 / 12 / 16 个发射方向显示刀影和收束环，发射时播放短扩散环，飞行带短柔边尾迹，命中或到期播放小范围消散。

## 技能与接入

当前 Monster 表中 23 条 Boss 配置含 `bladeBurst`。新表现接入共享 `boss_common`，并同步兼容 `boss_30001`、`boss_30002`、`boss_30003` 的资源库。新 key `BladeBurstBullet` 使用独立弹体；原 `StraightBullet`、`HomingBullet` 引用及资源保留。其他七类 Boss 技能没有在此轮改版，小刀仍沿用原来的实际直飞行为。

| 项目 | 本轮保留的实际参数 |
| --- | --- |
| 蓄力 / 收招 | 0.5 s / 0.15 s |
| 发射数量 | 按表配置 8 / 12 / 16 个等角方向 |
| 速度 / 单次伤害 | 7 / 25 |
| 单发飞行行程 / 寿命 | 6 / `6 ÷ 7` s，约 0.857 s |
| 发射起点 | Boss 真实位置向对应方向偏移 0.6 |
| 技能间隔 / 首次等待 | 按表保留 8 或 4 s；首次等待为间隔 × 0.4 |
| 碰撞 | 原半径 0.4 的 Trigger、原 Rigidbody 字段与零 wallMask |

预警只表达方向与释放阶段，不把短刀影当作已飞出的伤害弹体。预警与弹体直接走 Boss 实际技能流程，玩家装饰 VFX 限流饱和时仍保留全部方向与伤害弹体。只有短消散进入既有 `CombatVfxSpawner`、`PooledCombatVfx` 和装饰限流。

蓄力期间禁用、回池或死亡会取消预警，并阻止旧协程延迟发射；恢复与复用不会携带旧刀影或尾迹。已实测发射后 0.15 s 收招期间中断和复用：同实例复用后 Busy=false、Phase=0、无旧几何。暂停冻结阶段、运动、寿命与几何，退场清理借出的弹体和预警。刀气朝向在 Launch 时初始化，避免新弹体第一帧沿用上一轮方向；未挂专用表现组件的旧弹体保持原接入。

## 资源与正式参数

| 内容 | 实际结果 |
| --- | --- |
| 共用图集 | 512×256，**28,653 字节（约 28.0 KiB）**，预算 96 KiB |
| WebGL | ETC2_RGBA8Crunched，质量 70；无 mipmap、无 CPU 可读副本，Clamp / Bilinear |
| ETC2 RGBA8 块数据 | 无 mipmap 为 **131,072 字节（128 KiB）** |
| 共享材质 | 3 个：图集 Sprite、无贴图实体网格、既有柔边拖尾 Shader |
| Boss 预警 | 单 MeshRenderer，两个 submesh 分别使用实体 / 刀气材质；运行时生成 Mesh |
| 每发刀气 | 1 个 SpriteRenderer + 1 个 TrailRenderer；子图缩放 0.9，根 scale 与物理保持 |
| 拖尾 | 0.055 s、宽度 0.085；暖白—橙红—暗红，末端透明，无新增拖尾纹理 |
| 小消散 | 单层单粒子、8 帧、0.18 s 播放、0.24 s 回池，参考半径 0.3、尺寸 0.65 |
| 排序 | 预警与消散 4，拖尾 9，刀气 10；实际文字与玩家可读性由战斗夹具检查 |

图集上排为 8 个 64×64 消散格，内容 56×56，每格 4 px 透明边距。左下 256×128 为朝右刀气，右下预留；打包只识别透明边界、等比采样和排版。原稿由内置 image_gen 生成，真实 alpha、[源图](../../ArtOutput/VFX/BossBladeBurstV1/Sources)和[完整提示词](../../ArtOutput/VFX/BossBladeBurstV1/prompts.md)已归档在 Assets 外。

源 PNG、ETC2 块数据、Editor 内存读数与最终微信上传包分别统计。微信真机帧时、内存和最终上传包增量尚未测量。

## 制作与复用入口

- [Boss 全流程配置](Library/Boss/boss_blade_burst_v1.json)：记录图集、共享材质、专用弹体、消散和预警引用；由 `BossBladeBurstV1Builder` 读取，运行时不加载 JSON。
- [单次消散配方](Library/Recipes/boss_blade_dissolve_v1.json)：作为第 10 个普通一次性配方，在技能特效工作台查看逐帧、背景、时间轴与多实例，或导出新 id 复用图集。
- Unity 菜单「工具/VFX/Build Boss Blade Burst V1」构建正式资源；「检查Boss刀气爆发配置」核对引用、物理字段、层数、时长、压缩与预算。
- 菜单「工具/VFX/实战验收/Boss刀气爆发」运行真实 Game 场景夹具；通过「结束并恢复」还原场景与验收状态。后续输出进入独立 Toolkit 时间目录。

一次性导入器只处理小消散，不能导入整个 Boss 全流程配置。改资源参数后需同步对应配方、重新构建和验收；0.18 s 为最终秒数，不额外乘 1.5。

## 实战证据与边界

新版 89 项覆盖真实表构建、8 / 12 / 16 向、浅沙 / 草地、原间隔和首次等待、蓄力无提前伤害、方向预警、真实玩家碰撞 25 点伤害、存活与回池、暂停恢复、禁用 / 回池 / 死亡取消、发射后收招中断复用、地图边缘、装饰限流饱和、尾迹清理、随机状态、场景退出和存档保持。

压力夹具手动放置本体与 2 / 4 个 `CloneMarker` 施法者，同步发射峰值分别为 **36 / 80 发**，逐个保留方向预警并在原寿命后回池。该结果只验证多施法者叠加，**没有修复或证明 CloneModule 的继承白名单、初始化顺序或实际分身生成流程**；也不是设备 FPS 测试。

- [新版实战报告：89 项](../../ArtOutput/UI/BossBladeBurstV1/After/validation-report.json) / [旧版基线：46 项](../../ArtOutput/UI/BossBladeBurstV1/Before/validation-report.json)
- [完整实际时序回放](../../ArtOutput/UI/BossBladeBurstV1/after.mp4) / [前后对照](../../ArtOutput/UI/BossBladeBurstV1/comparison.png) / [多施法者压力回放](../../ArtOutput/UI/BossBladeBurstV1/stress.mp4)
- [资源统计](../../ArtOutput/VFX/BossBladeBurstV1/Audit/resources.json) / [配置审查](../../ArtOutput/VFX/BossBladeBurstV1/Audit/configuration.json)
- [完整性审查](../../ArtOutput/VFX/BossBladeBurstV1/Audit/integrity.json)：原稿 alpha、图集格边界、保留资源与最终实战证据核对通过。
- [配置库审查](../../ArtOutput/VFX/BossBladeBurstV1/Audit/library-audit.json)：10 个普通配方通过；[工具回归](../../ArtOutput/VFX/BossBladeBurstV1/Audit/toolkit-report.json)：44 / 44 项通过。
- [重复构建检查](../../ArtOutput/VFX/BossBladeBurstV1/Audit/rebuild.json)：10 项正式资源及其元文件共 20 个文件逐字节一致。
- [Boss 整体清单与后续方案](boss-skills-optimization-plan-v1.md)
