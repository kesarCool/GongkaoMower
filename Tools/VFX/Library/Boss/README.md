# Boss 技能表现配置

后续制作从[Boss 工作流](../../boss-production-workflow.md)进入；[产出总目录](../../boss-deliverables.md)集中列出每项配置、运行代码、构建 / 实战菜单、录像和已知边界。新任务使用[Boss 任务单](../../Templates/boss-skill-brief.md)及[提示词模板](../../boss-production-prompts.md)。目录元数据位于库外的 `Tools/VFX/boss-deliverables.json`，通过 `node Tools/VFX/audit-boss-workflow.cjs --write-index` 更新和核对，不参与运行时或粒子导入。

当前独立库有 7 个技能配置和 1 个公共外观配置，均由 Editor 专用构建器读取，运行时不加载 JSON；不计入普通一次性库的 10 个配方。

| 配置 | 当前用途 | 构建与检查菜单 |
| --- | --- | --- |
| [boss_blade_burst_v1.json](boss_blade_burst_v1.json) | 刀气方向预警、径向弹体与小消散 | `Build Boss Blade Burst V1` / `检查Boss刀气爆发配置` |
| [boss_dash_v1.json](boss_dash_v1.json) | 跟随至原锁向时机的身体包络路线、箭头、后沿速度线与终点环 | `Build Boss Dash V1` / `检查Boss冲刺配置` |
| [boss_knife_v1.json](boss_knife_v1.json) | 紧凑朱红小刀、文字前沿出手闪、短柔边拖尾与共用消散 | `Build Boss Knife V1` / `检查Boss小刀配置` |
| [boss_clone_v1.json](boss_clone_v1.json) | 本体字形重影、统一半透明、金实印 / 红断续印及真实分身继承 / 生命周期 | `Build Boss Clone V1` / `检查Boss分身配置` |
| [boss_head_crest_v1.json](boss_head_crest_v1.json) | 替换旧头顶双刀的紧凑冠徽、分身空心标记与可选动画头部节点 | `Build Boss Head Crest V1` / `检查Boss头顶标识配置` |
| [boss_summon_v1.json](boss_summon_v1.json) | 原 0.45 秒蓄力符环、真实最终出生点门印和失效回调隔离 | `Build Boss Summon V1` / `检查Boss召唤配置` |
| [boss_zone_v1.json](boss_zone_v1.json) | 地裂独立预警、同半径爆裂和可取消残痕；当前正式表未配 | `Build Boss Zone V1` / `检查Boss地裂配置` |
| [boss_revive_v1.json](boss_revive_v1.json) | 实际字形散开 / 回聚、余烬核心、视觉恢复交接与真实护盾弧 | `Build Boss Revive V1` / `检查Boss复活配置` |

[地裂 V1](../../boss-zone-v1.md)新增世界空间独立预警、真实判定裂纹和短残痕；单圈预警明确调整为 0.7 秒，未修改正式表的 Boss 技能分配。复用实体材质，0 新贴图或材质。

[复活 V1](../../boss-revive-v1.md)复用当前字体四边形和共享实体材质，无新 PNG / Texture / Material；保护弧对应实际伤害减免，旧护盾缓存漏接单独复现并修正。88 项复活实战、100 项分身回归、70 项召唤回归及 44 项工具检查通过。

[召唤 V1](../../boss-summon-v1.md)按真实 Spawner 生成事件显示出生标记，复用同一实体材质，无新 PNG / Texture / 材质。70 项召唤实战、100 项分身 / 头标回归及 44 项工具检查通过，三章数量、属性、随机与落点对照保留；第二批三项保留各自的独立验收资料。

[头顶标识 V1](../../boss-head-crest-v1.md)通过 100 项真实分身及外观锚点检查、16 项配置检查。无新贴图或材质；本体使用金橙冠徽，分身使用成对朱红空心菱形。

分身为第二批首项，90 项真实生成与继承实战、79 项冲刺小刀回归及 44 项工具检查通过，0 新 PNG / Texture / 材质。[制作记录](../../boss-clone-v1.md)包含真实 Start、重建策略、克隆池身份、主身复活假死和自然到期的证据。第二批依次完成分身、召唤和复活的独立制作。

菜单前缀均为「工具/VFX」。冲刺与小刀通过「工具/VFX/实战验收/Boss冲刺与小刀」检查，旧版 40 / 40、新版 79 / 79 项实战及 44 / 44 项工具检查通过；冲刺配置 9 / 9、小刀配置 10 / 10，完整性审查通过。5 个本轮资产及 meta 共 10 文件、12 个共享文件合计 22 文件重复构建逐字节一致。[完整制作与证据](../../boss-dash-knife-v1.md)。

冲刺与小刀新增 PNG、Texture、材质均为 0。冲刺复用实体材质，小刀复用刀气 Sprite、柔边材质和消散。新 key `KnifeBullet` 对应独立 `KnifeBulletV1`，旧 `HomingBullet` 资源与其他注册保持。小刀继续 turnRate=0 直飞；Trigger 的墙碰撞机制未变。冲刺按表 15 / 20 速度、0.3 s 末锁向，原单次 OverlapCircle 未修复：隔离观察额外 hpDelta 与基线均为 0，不能视作 30 点技能伤害已命中。

## 刀气爆发历史接入记录

`boss_blade_burst_v1.json` 登记周身刀气爆发的共享图集、材质、专用投射物、消散效果和 Boss 接入。

2026-10-28，旧版基线 46 项、新版 89 项 Editor 实战检查通过。正式图集 512×256、28,653 字节，ETC2 RGBA8 无 mipmap 块数据为 128 KiB；三份共享材质、单 Mesh 两个 submesh 的方向预警，每发一张 Sprite 加一条短 Trail。完整参数、证据与后续复用见[制作记录](../../boss-blade-burst-v1.md)。

在 Unity 编辑模式执行 `工具/VFX/Build Boss Blade Burst V1` 重新构建，再执行 `工具/VFX/检查Boss刀气爆发配置`。构建读取 `ArtOutput/VFX/BossBladeBurstV1/Sources/` 的透明原稿，仅裁切、等比缩放与排版，不重绘素材。

该配置由编辑器使用，运行时不加载 JSON。它保留旧 `StraightBullet` 资源，为刀气爆发新建独立弹体，并给四个 Boss 预制体的资源库增添 `BladeBurstBullet`。预警网格在运行时生成，编辑器预制体不保存 Mesh 资源。重复构建复用既有组件和资源。

图集为 512×256：顶部八格 64×64 消散帧各留 4 px 透明边距；左下 256×128 保存朝右刀气，右下预留。三份共享材质中，只有刀气与消散共用材质绑定该图集，柔边拖尾与预警实体网格不增加贴图。

普通工作台只读取独立[小消散配方](../Recipes/boss_blade_dissolve_v1.json)，不能将整个 Boss 配置当作一次性粒子导入。小消散最终播放 .18 s、.24 s 回池；技能仍为 .5 s 蓄力、.15 s 收招，原发射数、速度、伤害与间隔保持。预警和伤害弹体绕开玩家装饰限流，小消散沿用现有池与限流。菜单「工具/VFX/实战验收/Boss刀气爆发」提供独立真实技能验收。

刀气历史中的 2 / 4 个手动 CloneMarker 样本仅用于压力验证；真实生成与继承问题已由后续分身 V1 单独复现、修复和验收，不能将旧压力报告当作该修复证据。微信真机与最终上传包尚未测量。

配置审查记录写入 `ArtOutput/VFX/BossBladeBurstV1/Audit/`。编辑器内存、源 PNG 和 ETC2 块数据分别记载；这些数值不能替代微信真机内存、帧率或最终上传包体测量。

冲刺与小刀本轮报告在 `ArtOutput/VFX/BossDashKnifeV1/Audit/`，真实前后回放与报告在 `ArtOutput/UI/BossDashKnifeV1/`。旧图集与 meta、3 份共享材质、BossBrain / BossBullet、Monster / LevelWave 与旧 HomingBullet 资源完整性保持。
