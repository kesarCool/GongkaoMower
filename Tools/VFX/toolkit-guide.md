# 导入预览工具与配方说明

Boss 制作统一入口：[工作流](boss-production-workflow.md)、[产出总目录](boss-deliverables.md)、[任务单](Templates/boss-skill-brief.md)与[提示词模板](boss-production-prompts.md)。完整技能使用专用构建 / 验证入口；一次性小消散继续使用普通工作台。目录检查命令：`node Tools/VFX/audit-boss-workflow.cjs --write-index`，仅核对资料和归档报告。

常驻护体场使用独立的 [V3 Persistent 配置](Library/Persistent/field_generator_v3.json) 与 [制作记录](field-generator-v3.md)。工作台「罡气护体（常驻预览）」及菜单「工具/VFX/常驻特效」读取当前技能引用，可调整预览半径、强度、周期与突破状态；菜单「实战验收/罡气护体」验证实际技能。一次性 JSON 导入器不处理常驻场。

刀光剑影使用 [Projectiles 配置](Library/Projectiles/sword_presentation_v1.json) 与专用构建器，菜单「工具/VFX/Build Sword Presentation V1」同步飞刃、短拖尾和命中引用，「检查刀光剑影配置」检查参数与预算。工作台中的「刀光剑影·交错切痕」可独立预览、导出变体；一次性导入器只处理命中配方，不能导入整个投射物配置。详见 [刀光剑影 V1](sword-presentation-v1.md)。

Boss 刀气爆发使用独立 [Boss 配置](Library/Boss/boss_blade_burst_v1.json)，菜单「工具/VFX/Build Boss Blade Burst V1」构建图集、材质、专用弹体和四个 Boss 的资源引用，「检查Boss刀气爆发配置」核对预算、原物理字段及预警引用。普通工作台只读取 [小消散配方](Library/Recipes/boss_blade_dissolve_v1.json)；全流程预警与真实伤害弹体须通过「实战验收/Boss刀气爆发」查看和检查。详见[制作记录](boss-blade-burst-v1.md)。

Boss [冲刺](Library/Boss/boss_dash_v1.json)和[小刀](Library/Boss/boss_knife_v1.json)也由独立构建器管理：菜单「工具/VFX/Build Boss Dash V1」「Build Boss Knife V1」，对应「检查Boss冲刺配置」「检查Boss小刀配置」。两者复用刀气图集 / 材质，0 新 PNG、Texture、材质；全流程使用「实战验收/Boss冲刺与小刀」，不能放入普通一次性导入器。Boss 独立库当前有 7 个技能配置和 1 个公共外观配置，普通库仍为 10 项。详见[制作记录](boss-dash-knife-v1.md)。

Unity 菜单：**工具 → VFX → 技能特效工作台**。代码位于 `Assets/Editor/SkillVfxToolkit/`，全部 Editor 专用；普通一次性效果使用原对象池和粒子系统；Boss 全流程使用独立 Presentation、共享弹体和网格 / 字形表现。

## 常用操作

**复用已有美术**：左侧选一个效果 → 导出变体配方 → 编辑 JSON 的 id、名称、用途和参数 → 选择该 JSON → PNG 留空 → 导入为新草稿。新 Prefab 位于 `Assets/Res/Prefabs/Effects/Generated/`，继续共享原贴图和材质。

**制作新图集**：复制 [8 帧模板](Templates/hit-eight-frame.example.json)，填写新 id、图集规格、选帧与预算 → 选择 JSON 与透明 PNG → 导入。默认模板将 4×4 原稿的 0、2、4、6、8、10、12、15 帧等比例缩小，加入每格 8px 边距，输出 512×64、8×1 格。打包器保留源单格宽高比，不会变形拉伸。

**直接导入已排好的图集**：设置 `packing: null`，PNG 宽高必须精确匹配配方。若从已有配方换成新原稿，需重新写明 packing；不要把旧的最终图集布局误当原稿布局。

**查看动效**：支持浅沙、草地、深色，以及可选的实际地图截图；截图可从文件读取，不必导入 Assets。可设置世界视野半高、真实范围缩放、单个 / 4 个 / 8 个叠加、预览速度、时间轴与前后帧。前后帧沿各层实际曲线查找纹理帧变化，非线性爆炸也可检查。文字是预览示意，真实阅读效果仍在 Game 场景验收。导出 PNG 为 512×512 的当前粒子视图与所选纯色底；不包含工作台 GUI 字样或地图截图。

**检查配置库**：菜单「工具/VFX/检查特效配置库」或工作台按钮，生成 `ArtOutput/VFX/Toolkit/library-audit.json`。比对 Prefab 与配方的层数、时长、尺寸、锚点、随机种子、帧曲线、alpha、回池、资源路径和纹理导入设置，同时检查 PNG 预算；按贴图路径去重汇总源字节。

## JSON 字段

| 字段 | 含义 |
| --- | --- |
| schemaVersion | 当前为 1；未知字段会报错，避免拼写错误静默失效 |
| id | 3–64 位小写英文、数字、下划线，以字母开头；导入要求唯一 |
| displayName / category / usage | 中文显示名、分类、真实技能事件与用途 |
| status / validationReport | 草稿或 Editor 已验收状态，以及报告路径；新导入会清空旧结论 |
| briefPath / promptPath / sourceDirectory | 需求、完整提示词与源图证据；新 PNG 原稿额外归档到 Toolkit/Sources |
| width / height / columns / rows | 正式图集尺寸与布局，宽高为 16–2048 内的 2 次幂，整数等分，最多 256 格 |
| pngBudgetKiB / compressionQuality | 最终 PNG 字节上限及 WebGL Crunch 质量；不是最终上传包预算 |
| texturePath / materialPath / prefabPath | 已有资源索引；复用时读取前两项，新图导入时按新 id 生成路径 |
| poolSeconds / referenceRadius / sortingOrder | 最终回池秒数、原尺寸对应的伤害半径（0 不缩放）、排序 |
| layers[].seconds | 最终粒子寿命与 Duration，不额外应用倍率 |
| sizeX / sizeY / size3D | 粒子世界尺寸；非 3D 尺寸模式要求 X=Y |
| offsetX / offsetY | 层的本地偏移；根节点位于实际命中点 |
| seed | 固定随机种子；不使用全局战斗随机状态 |
| startFrame | 归一化起始帧偏移，0–1；不是整数帧号 |
| frames | Lifetime 曲线键：time/value/inTangent/outTangent；value 为全图归一化帧位置 |
| alpha | time/alpha 键，覆盖 0–1，2–8 个；颜色保持白色以保留贴图本色 |
| packing | 可选的原稿分格与选帧映射；缺省 / null 表示直接使用正式图集 |
| packing.sourceFrames | 按目标格从左到右、从上到下排列的源帧索引；-1 留空，长度等于目标格数 |
| packing.paddingPixels | 目标每格额外透明边距；有原稿内边距时两者叠加 |

例：8×4 共 32 格，第二行第 0 帧为 `8/32`，末端采样使用 `15.999/32` 避免跳至下一行。两层落雷使用各自 `startFrame` 0 和 0.5，并在曲线中播放本层的 8 帧。

## 导入检查与输出

导入要求退出 Play Mode。检查配方、源图 PNG 签名、宽高、透明与可见像素、整数分格、最终字节预算、图集取样范围和回池余量。原稿上限为 4096×4096 / 16 MiB。普通透明图必须同时包含透明区和可见主体；工具不会自动抠黑底，也不能自动识别画进图里的棋盘格、中心漂移或艺术风格问题。

新资源路径固定在 `Assets/Res/{Textures/VFX,Materials/VFX,Prefabs/Effects}/Generated/` 对应目录；现有目标及 `.meta` 存在时拒绝导入，保留原 GUID。导入失败时撤回该次创建的资产。不会自动写任何技能定义或替换既有引用。

配方登记在 `Tools/VFX/Library/Recipes/<id>.json`，导入记录在 `ArtOutput/VFX/Toolkit/Imports/<id>.json`，新图原稿在 `ArtOutput/VFX/Toolkit/Sources/<id>/original.png`。正式图集仅保存选帧打包结果；配置内 packing 被清空，后续变体可直接复用正式图集。

## 真实技能验收

菜单「工具/VFX/实战验收/爆炸」「符箓」「落雷与突破」「罡气护体」「刀光剑影」「Boss刀气爆发」「Boss冲刺与小刀」「Boss分身」「Boss召唤」「Boss复活」「Boss地裂」调用已有真实 Game 场景夹具。运行前要求当前场景已保存，工具暂存场景与窗口尺寸，使用内存存档；通过菜单「结束并恢复」退出并还原。每次输出保存到独立的 `ArtOutput/UI/Toolkit/<日期时间>/<样本>/`，不会覆盖 Timing15 历史证据。验收开关退出后恢复原值。

## Boss 分身接入后验证（2026-10-28）

[分身 V1](boss-clone-v1.md)使用专用 Boss 配置和构建器，普通逐帧粒子导入器不负责生成克隆、技能白名单或 TMP 重影。[旧版 23 项](../../ArtOutput/UI/BossCloneV1/Before/validation-report.json)复现问题；[新版 90 项](../../ArtOutput/UI/BossCloneV1/After/validation-report.json)验证真实 CloneModule / Start、后续重建、继承与生命周期，并保留两轮 17 秒原时序回放。[旧冲刺小刀 79 项回归](../../ArtOutput/UI/BossCloneV1/RegressionDashKnife/validation-report.json)和[工具 44 项](../../ArtOutput/VFX/BossCloneV1/Audit/toolkit-report.json)通过。当前 Boss 库包含 7 个技能配置和 1 个[公共头顶标识配置](Library/Boss/boss_head_crest_v1.json)，普通一次性库 10 项；头标修订复用材质，新增贴图与材质均为 0。

[召唤 V1](boss-summon-v1.md)使用 `Build Boss Summon V1` / `检查Boss召唤配置` 和 `实战验收/Boss召唤`。它接入真实成功刷怪后的回调，门印在最终出生点播放；普通逐帧导入器不处理波次生成。源符环与全部门印合并为一个运行网格，0 新贴图或材质。

[复活 V1](boss-revive-v1.md)使用 `Build Boss Revive V1` / `检查Boss复活配置` 和 `实战验收/Boss复活`。快照并复用真实 TMP 四边形，核心与字形根独立于被隐藏的身体；还原原文字时不改变碰撞体。真实伤害探针核对保护弧和护盾寿命，普通粒子导入器不处理死亡拦截或字形重建。

这些入口只验证本来已接入的样本。新导入的草稿不会因为运行它们就自动获得验收，需实际绑定目标技能并添加相应事件、数值、生命周期和可读性检查。

菜单「工具/VFX/检查导入预览工具」运行确定性选帧 / 排版、透明度、无覆盖、导入、共享材质、反向拖动和场景隔离检查。临时正式资产会被清理，报告及输入 / 截图留在 `ArtOutput/VFX/Toolkit/Tests/`。这不改变已有三个样本的战斗验收结论，也不代表微信真机通过。

## Boss 冲刺与小刀接入后验证（2026-10-28）

- [旧版 40 项](../../ArtOutput/UI/BossDashKnifeV1/Before/validation-report.json)、[新版 79 项](../../ArtOutput/UI/BossDashKnifeV1/After/validation-report.json)实战检查通过；包括 15 / 20 速度、原锁向、8 距离、墙 / 地图边缘截断、真实小刀 20 点伤害、暂停、回池后新位置首个物理步、尾迹、装饰限流与组合回放。
- [冲刺配置 9 项](../../ArtOutput/VFX/BossDashKnifeV1/Audit/dash-configuration.json)、[小刀配置 10 项](../../ArtOutput/VFX/BossDashKnifeV1/Audit/knife-configuration.json)、[工具 44 项](../../ArtOutput/VFX/BossDashKnifeV1/Audit/toolkit-report.json)、[完整性检查](../../ArtOutput/VFX/BossDashKnifeV1/Audit/integrity.json)通过。普通一次性库维持 10 项，Boss 独立库为 3 个配置，本轮 0 新 PNG / Texture / 材质。
- [重复构建](../../ArtOutput/VFX/BossDashKnifeV1/Audit/rebuild.json)：5 个本轮资产及 meta 共 10 文件与 12 个共享文件，合计 22 文件逐字节一致。
- [单技能前后对照](../../ArtOutput/UI/BossDashKnifeV1/comparison.png)、[新版回放](../../ArtOutput/UI/BossDashKnifeV1/after.mp4)、[组合回放](../../ArtOutput/UI/BossDashKnifeV1/combination-after.mp4)和[导出记录](../../ArtOutput/UI/BossDashKnifeV1/proof.json)已归档，按实际捕获时间编码。
- 小刀保留直飞；冲刺原单次 OverlapCircle 保留，隔离技能伤害的基线与新版额外 hpDelta 均为 0。实战检查通过不表示 30 点冲刺技能伤害已实际命中，也不表示微信真机性能通过。

## Boss 刀气爆发接入后验证（2026-10-28）

- 普通配置库增至 10 项，新增小消散与敌方刀气共享图集；[本轮配置审查](../../ArtOutput/VFX/BossBladeBurstV1/Audit/library-audit.json)。Boss 全流程保存在独立 Boss 库，不作为第 11 个普通粒子配方。
- 导入、排版、预览与生产资源保护 44 / 44 项通过；[本轮工具报告](../../ArtOutput/VFX/BossBladeBurstV1/Audit/toolkit-report.json)。
- 新版刀气 89 项实战通过，包含收招期间中断与复用，确认同实例复用后 Busy=false、Phase=0、无旧几何；[报告](../../ArtOutput/UI/BossBladeBurstV1/After/validation-report.json)。保留旧版 46 项基线，手动 2 / 4 个 CloneMarker 压力样本不代表 CloneModule 继承已修复。
- 10 项正式资源及其元文件共 20 个文件重复构建逐字节一致；[检查](../../ArtOutput/VFX/BossBladeBurstV1/Audit/rebuild.json)。

## 刀光剑影接入后验证（2026-10-28）

- 配置库 9 项全部通过，按路径去重后 5 张图集，共 753,114 字节源 PNG；[本轮配置审计](../../ArtOutput/VFX/SwordPresentationV1/Audit/library-audit.json)。
- 导入、打包、预览与生产资产保护 41 项通过，临时资源已清理；[本轮工具报告](../../ArtOutput/VFX/SwordPresentationV1/Audit/toolkit-report.json)。
- 刀光剑影最终版 64 项真实技能检查通过；[报告](../../ArtOutput/UI/SwordPresentationV1/After/validation-report.json)。资源和元文件重复构建共 14 项逐字节一致；[检查](../../ArtOutput/VFX/SwordPresentationV1/Audit/rebuild.json)。

## 初次工具交付记录（2026-10-26）

- 配置库 8 项与当前资源一致，去重后 4 张贴图、715,621 字节源 PNG；[配置审计](../../ArtOutput/VFX/Toolkit/library-audit.json)。
- 导入、打包与隔离预览 38 项通过，测试资源已清理；[工具检查报告](../../ArtOutput/VFX/Toolkit/Tests/toolkit-report.json)。
- 新实战菜单成功执行符箓 50 项真实技能检查，报告进入独立时间目录；[本轮报告](../../ArtOutput/UI/Toolkit/20261026-001249-645/talisman/validation-report.json)。退出后已恢复 Boot 场景、原验收开关和输出目录，场景无未保存修改。
- 三个正式样本原有 178 项时长验证证据继续保留在 `ArtOutput/UI/Timing15/`；本轮未重新执行全部 178 项，也未执行微信构建或真机性能测试。


[Boss 地裂 V1](boss-zone-v1.md)已加入独立构建、配置检查和实战预览入口。使用世界空间危险圆、每处 0.7 秒独立预警与真实命中帧的裂纹 / 碎石反馈，残痕 0.9 秒；复用现有实体材质，网格和缓冲仅首次施放创建。正式 Monster / LevelWave 尚未启用 zone，本次不变更技能组合；默认最后爆发由约 0.7 秒延后至 1.26 秒，属于明确的预警平衡调整。
