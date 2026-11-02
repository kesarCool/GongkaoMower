# 特效配置库

2026-10-27 罡气护体当前使用 [V3 旋转力场配置](Persistent/field_generator_v3.json)：绿色普通 / 橙金突破、内外圈反向旋转；突破增加金白长弧与六枚能量棱角，无新增贴图。由专用构建器读取，使用「常驻特效/罡气护体预览」和「实战验收/罡气护体」。详见 [V3 制作记录](../field-generator-v3.md)；[V2](Persistent/field_generator_v2.json) / [V1](Persistent/field_generator_v1.json) 保留历史记录。常驻配置与下列一次性回池配方分别管理。

2026-10-28 新增 [刀光剑影投射物配置](Projectiles/sword_presentation_v1.json)：普通青锋、满级与环绕金青双刃、短柔边拖尾；命中也登记为下表的一次性配方。专用构建、预览和实战入口见 [制作记录](../sword-presentation-v1.md)。

同日完成 [飞刃长度与命中锚点修订](../hit-feedback-alignment-v1.md)：飞刃 X 缩短 20%，符箓 / 飞刃贴字并短时跟随，目标雷击、导弹和射线修正视觉落点；九个配方的报告索引已更新到本轮独立实战证据。

同日接入 [Boss 刀气爆发全流程配置](Boss/boss_blade_burst_v1.json)：蓄力方向刀影、独立敌方弹体和短消散共用 512×256 图集，28,653 字节，3 份共享材质，无新增拖尾纹理；新版 89 项实战检查通过。[制作记录](../boss-blade-burst-v1.md)说明当前参数与验收边界。全流程由 Boss 专用构建器读取，小消散单独登记为下表第 10 项。

同日完成 [Boss 冲刺](Boss/boss_dash_v1.json)与[小刀](Boss/boss_knife_v1.json)：路径包络、箭头、后沿速度线与紧凑短刃复用上述图集和材质，新增 PNG / Texture / 材质均为 0；旧版 40、新版 79 项实战、44 项工具检查通过。小刀保持 turnRate=0 直飞，冲刺原单次 OverlapCircle 命中限制保留；完整结果与证据见[制作记录](../boss-dash-knife-v1.md)。

第二批首项 [Boss 分身](Boss/boss_clone_v1.json)通过 90 项真实分身实战、79 项旧技能回归和 44 项工具检查，修正继承与本体池身份，增加字形重影、透明度和身份印记，0 新贴图或材质。当前独立 Boss 库为 **7 个技能配置和 1 个公共外观配置**，普通一次性库仍为 **10 项**。公共[头顶标识](Boss/boss_head_crest_v1.json)替换两把旧刀，后续[召唤配置](Boss/boss_summon_v1.json)增加旋转符环和真实出生点门印，[复活配置](Boss/boss_revive_v1.json)增加真实字形回聚、核心和保护弧，均复用已有资源。详见[分身记录](../boss-clone-v1.md)、[头标记录](../boss-head-crest-v1.md)、[召唤记录](../boss-summon-v1.md)与[复活记录](../boss-revive-v1.md)。

`Recipes/*.json` 为可读、可导出、可复用的制作配方。已录入当前 10 个一次性效果，均保存最终秒数；前 8 个为已批准的 1.5 倍结果，刀光剑影命中为 0.27 秒，Boss 刀气消散为 0.18 秒。配方保存在 Assets 外，Editor 工具读取它们，运行时不加载。

| 配方 id | 当前用途 | PNG 预算 | 共享关系 |
| --- | --- | --- | --- |
| explosion_compact_v2 | 石破天惊 | 512 KiB | 爆炸共用图集 |
| explosion_chain_v2 | 连环爆破 | 512 KiB | 爆炸共用图集 |
| explosion_missile_v2 | 穿云破雾 | 512 KiB | 爆炸共用图集 |
| explosion_heavy_v2 | 追风逐日 | 512 KiB | 爆炸共用图集 |
| explosion_echo_v2 | 熊猫手雷延迟回响 | 512 KiB | 复用连环爆破帧段，无新贴图 |
| talisman_hit_v1 | 普通、满级、突破与环绕符箓命中 | 96 KiB | 与符纸共用图集；拖尾另用既有无贴图材质 |
| lightning_v1 | 普通雷霆万钧 | 128 KiB | 主雷 + 余电共用图集 |
| lightning_breakthrough_v2 | 突破雷霆万钧 | 256 KiB | 红色专用主雷 + 落点图集 |
| sword_hit_v1 | 普通、满级、环绕与暴击分裂飞刃命中 | 96 KiB | 与青锋 / 金青双刃共用 512×256 图集；拖尾无贴图 |
| boss_blade_dissolve_v1 | Boss 刀气和小刀命中玩家 / 到期消散；不据当前 wallMask 声称撞墙消散 | 96 KiB | 与敌方刀气 / 小刀共用 512×256 图集；预警和拖尾不新增纹理 |

字段说明、导入方式与验收入口见 [工具说明](../toolkit-guide.md)。新效果优先在工作台「导出变体配方」，填写唯一新 id。PNG 留空时可复用该配方的正式贴图和材质；添加新美术时选择 PNG，工具建立独立的新资源。

`status=integrated-editor-verified` 表示该配方对应的已绑定资源通过所记录的 Editor 战斗报告；不表示微信真机通过。导入器总将新资源重置为草稿并清空旧报告。改 JSON 不会自动改已经绑定的 Prefab；库检查会报告参数漂移。需要更新已发布效果时，以新版本 id 导入、验收、换引用，再归档旧版。


[Boss 地裂 V1](../boss-zone-v1.md)已加入独立构建、配置检查和实战预览入口。使用世界空间危险圆、每处 0.7 秒独立预警与真实命中帧的裂纹 / 碎石反馈，残痕 0.9 秒；复用现有实体材质，网格和缓冲仅首次施放创建。正式 Monster / LevelWave 尚未启用 zone，本次不变更技能组合；默认最后爆发由约 0.7 秒延后至 1.26 秒，属于明确的预警平衡调整。
