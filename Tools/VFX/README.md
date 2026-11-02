# 《文字割草机》技能视觉制作手册

版本 1.9，2026-10-29。适用于 Unity 2021.3 Built-in 2D / 微信小游戏。爆炸、天师符令、雷霆万钧、罡气护体、刀光剑影与 Boss 第一批表现已验证这套生产路径，本目录作为后续制作的统一入口。[飞刃长度与命中落点修订](hit-feedback-alignment-v1.md)通过 311 项实战、93 项字形与 41 项工具检查；[Boss 刀气爆发](boss-blade-burst-v1.md)历史样本通过 89 项实战；[冲刺与小刀](boss-dash-knife-v1.md)通过 79 项实战、44 项工具检查。

**Boss 制作入口：**[工作流](boss-production-workflow.md) → [产出总目录](boss-deliverables.md) → [Boss 任务单](Templates/boss-skill-brief.md)与[提示词 / 执行指令](boss-production-prompts.md)。7 个技能配置和 1 个公共头标的代码、菜单、资源与证据已统一索引；地裂未配表、独立 resist 待制作、Spine 与真机待验收分别保留状态。目录可用 `node Tools/VFX/audit-boss-workflow.cjs --write-index` 更新和核对。

第二批首项[分身 V1](boss-clone-v1.md)已完成 90 项真实生成 / 继承 / 生命周期检查、79 项冲刺小刀回归及 44 项工具检查。本体字形重影、分身统一半透明和金色实印 / 朱红断续印复用原资源，0 新 PNG / Texture / 材质。第二批按分身、召唤、复活逐项验收。

[Boss 头顶标识 V1](boss-head-crest-v1.md)将两把旧装饰刀替换为紧凑冠徽，分身使用成对空心标记；复用材质，并给后续 Spine 主体保留头部节点绑定入口。

第二批[召唤 V1](boss-summon-v1.md)增加旋转敕令符环和真实出生门印，复用材质；保留 0.45 秒蓄力、12 / 15 秒间隔、关卡数量和属性。每个门印在真实成功生成后的最终点出现，无额外出生等待或随机抽样。

[复活 V1](boss-revive-v1.md)使用同一批实际字形完成散开、余烬等待和回聚，恢复弹动只影响视觉，保护弧对应真实复活盾。保留配表时间、比例和次数，修正旧动态护盾缓存漏接，0 新 PNG / Texture / Material。

| 内容 | 入口 | 用法 |
| --- | --- | --- |
| Boss 专用工作流 | [工作流](boss-production-workflow.md) / [产出目录](boss-deliverables.md) | 机制基线、视觉路线、构建、真实技能验收、原速交付与持续复用 |
| 角色多方向动作提案 | [整身方向帧评估](player-frame-reference-review-v1.md)、[历史分件方案](player-directional-actions-plan-v1.md) | 保留原图评估与未通过验收的分件方案；已实施的弹仔单角色版本见下方 V1 |
| 弹仔五向整身瞄准 V1（已停用） | [实验记录与恢复检查](player-gunner-aim-v1.md) | 观感未通过，已恢复原版单贴图及左右镜像；五向素材保留为历史实验 |
| 技能视觉说明书 | [visual-spec.md](visual-spec.md) | 制作前确定事件、风格、时序、锚点、预算与验收 |
| 提示词模板 | [prompt-templates.md](prompt-templates.md) | 先填需求，再组合生成、修图、选帧提示词 |
| 导入预览工具 | Unity 菜单 **工具 → VFX → 技能特效工作台**；[工具说明](toolkit-guide.md) | 导入配方与透明 PNG，逐帧、变速、叠加与文字对照 |
| 特效配置库 | [Library/README.md](Library/README.md) / [Recipes](Library/Recipes) / [Persistent](Library/Persistent) / [Projectiles](Library/Projectiles) / [Boss](Library/Boss) | 10 个一次性效果 + 罡气护体常驻配置 + 刀光剑影投射物配置 + 7 个 Boss 技能配置和 1 个公共外观配置，保存实际参数和证据路径 |
| 新效果任务单 | [Templates/visual-brief.md](Templates/visual-brief.md) | 每个任务复制一份，记录决策和最终验收 |
| 新效果配方 | [Templates/hit-eight-frame.example.json](Templates/hit-eight-frame.example.json) | 从 4×4 原稿选 8 帧，缩小打包成一层池化命中效果 |

## 每次制作的顺序

Boss 全流程优先使用[专用工作流](boss-production-workflow.md)。下列通用顺序主要对应一次性图集效果；纯网格、字形或资源复用路线可以直接进入实现，不要求生成新图片。

1. **填写说明书**：复制任务单到 `ArtOutput/VFX/<效果名>/brief.md`，填写真实技能事件、主色、轮廓、视觉半径、命中锚点、时长、频率、层数及 PNG 预算。先查库中是否已有可复用的轮廓和材质。
2. **生成少量原稿**：使用实际战斗截图作为风格参考，记录完整提示词、参考输入、工具、日期、选用与淘汰理由。原稿保存在 `ArtOutput/VFX/<效果名>/Sources/`。透明背景由生成工具的参数启用，同时写入提示词。
3. **逐帧筛选**：检查动作连续性、中心漂移、裁边、尾帧、浅底对比与透明边缘。先修正美术问题，再选帧缩小；代码打包只处理取样、尺寸和排版。
4. **导入草稿**：复制 JSON 模板或在工作台导出已有变体，填写新 id 和最终参数。选择配方与 PNG 导入；复用现有图集时不选 PNG。工具不覆盖旧 id、不自动绑定技能。
5. **在工作台预览**：检查浅沙、草地、深底、实际地图截图，以及 1 / 4 / 8 个实例。暂停、拖动时间、逐帧查看；核对世界尺寸、落点与文字可读性。预览速度只用于观察，不保存到资源。
6. **接入真实事件并验收**：通过 `CombatVfxSpawner.TryPlayPooled` 沿用现有池化与限流，只改表现引用。已有样本可使用实战验收菜单；新增技能需补对应的实际命中验证。输出截图、实际时序录像和报告。
7. **登记与交付**：新导入默认标为 `draft-needs-combat-validation`。完成真实事件、复用、暂停和退场检查后，登记报告并标记 Editor 已验收；微信真机与包体另记结论。更新任务单与配方，运行配置库检查。

## 当前约定

现有 8 个预制体采用上一轮确认的 **1.5 倍时长**，见 [时长记录](vfx-duration-1.5x.md)。配置库直接保存最终秒数；导入时不再乘 1.5。原有专用生成器仍使用 `CombatVfxTimingProfile` 从基础参数重建，新配方导入器直接按配方生成。

配置库与工具都属于制作资料 / Editor 代码，不增加运行时配置加载，也不新增通用动画播放器。普通一次性效果使用粒子图集、`PooledCombatVfx` 与现有技能事件；Boss 全流程使用专用表现组件、网格 / 字形和共享弹体资源。常驻光环、任意长度光束、多方向角色动作按各自入口制作。

文字命中以 TMP 可见字形为视觉锚点，向字符内部收 18%；符箓与飞刃的短时烙印跟随文字，暂停、死亡和池复用均检查绑定有效性。雷击、导弹、射线只修正视觉点，保持原伤害与事件坐标；手雷落地、弹射雷和随机余爆保持真实爆点。具体实现、边界与本轮验收状态见[命中落点修订](hit-feedback-alignment-v1.md)。

## 已有样本与历史资料

- [共享爆炸库 V2](explosion-library-v2.md)：四款爆炸 + 复用帧段的回响。
- [天师符令 V1](talisman-presentation-v1.md)：符纸、柔边拖尾与真实命中入口。
- [普通落雷 V1](lightning-presentation-v1.md) / [专用红色突破 V2](lightning-breakthrough-v2.md)：两层图集、独立突破美术。
- [罡气护体 V3](field-generator-v3.md)：当前使用；内外圈反向旋转，突破加强带宽、金白长弧和六枚能量棱角，无新增贴图。[V2](field-generator-v2.md) / [V1](field-generator-v1.md) 保留历史记录。
- [刀光剑影 V1](sword-presentation-v1.md)：青锋 / 金青双刃、柔边剑气与交错切痕，共用 36.6 KiB 图集；初版 64 项实战检查属于历史记录。命中保存最终 0.27 秒，不额外乘 1.5。
- [飞刃长度与命中落点修订 V1](hit-feedback-alignment-v1.md)：飞刃只缩短 X 20%，厚度和贴图不变；统一字形锚点及短时跟随。五类技能 311 项实战检查通过。
- [Boss 刀气爆发 V1](boss-blade-burst-v1.md)：按真实 8 / 12 / 16 向蓄力，橙红短刀气、柔边尾迹与小消散；共用 28,653 字节图集，89 项实战检查通过。危险预警与伤害弹体不受玩家装饰限流裁剪。
- [Boss 冲刺与小刀 V1](boss-dash-knife-v1.md)：冲刺路径覆盖身体包络，原时机锁向，文字后沿速度线；小刀缩短并补出手闪、柔边尾迹和消散。旧版 40、新版 79 项通过，0 新 PNG / Texture / 材质；保留小刀直飞和冲刺原单次 OverlapCircle 限制，未宣称 30 点冲刺技能伤害已命中。
- [1.5 倍时长实际回放](../../ArtOutput/UI/Timing15/runtime-1.5x.mp4)：178 项既有运行检查通过。
- [原分享流程接入分析](frame-animation-adoption-analysis.md)：保留最初调研，具体实现与参数以本手册、配置库和当前资源为准。
- [Boss 技能清单与优化方案 V1](boss-skills-optimization-plan-v1.md)：8 类已实现模块、6 类当前已配技能及全部 Boss 组合；刀气爆发、冲刺、小刀、分身、召唤与复活已完成；地裂能力与预览已完成但未配表，免伤护盾模块仍待制作。

旧文档中的初版时长和资源数字属于当次记录，不作为新配方默认值。源 PNG 字节、ETC2 块数据、Editor 内存读数与最终微信上传包分别记录，不能混用。


[Boss 地裂 V1](boss-zone-v1.md)已加入独立构建、配置检查和实战预览入口。使用世界空间危险圆、每处 0.7 秒独立预警与真实命中帧的裂纹 / 碎石反馈，残痕 0.9 秒；复用现有实体材质，网格和缓冲仅首次施放创建。正式 Monster / LevelWave 尚未启用 zone，本次不变更技能组合；默认最后爆发由约 0.7 秒延后至 1.26 秒，属于明确的预警平衡调整。
