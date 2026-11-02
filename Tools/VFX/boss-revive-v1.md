# Boss 复活 V1 · 余烬重生

2026-10-29。第二批第三项，在分身、召唤独立交付后改版复活。保留当前配表比例、等待、次数和保护时长，将散字、等待、聚字、恢复和护盾衔接成一条完整动效。

## 当前表现

1. 致命伤发生时，快照当前字体的真实可见字形，字形向两侧散开并轻旋，脚下朱红断环扩散。
2. 原等待期内保留暖金余烬核心、反向短弧与细碎能量线。散出的同一批字形悬停，再沿原路径回聚；不是生成另一套字或等待结束后整块重现。
3. 等待进度 46% 开始回聚，96% 回到原字形位置。原 1.5 秒恢复血量时，副本完成 0.35 秒轻弹并交还原文字，只缩放视觉字形，不缩放物理根节点或碰撞体。
4. 真实 0.5 秒复活护盾生效时，显示金橙护弧；实际抵抗事件使护弧短暂提亮。盾失效后闭合护弧立即结束，仅留下 0.16 秒散线。

字形直接复用 TMP 字符四边形、UV、UV2、顶点渐变和当前共享字体材质，最多 32 个可见字符，按实际字体材质合批。当前 Boss 名称远低于上限。没有新增逐字 TMP / GameObject 或字体贴图；空字 / 非活跃 TMP 使用核心兜底。

## 保持的机制与必要修正

当前 4 个 Boss 配置使用 revive：32021 / 33021 为 50%、1.5 秒、2 次；32081 为 50%、1.5 秒、1 次；32082 为 100%、1.5 秒、1 次。保留原初始一帧、等待循环、0.35 秒恢复交接，以及之后 0.5 秒保护窗口。

保留旧血量公式：按**当次 MaxHp × 配表比例向上取整**并写回 MaxHp / Hp。因此两次半血原逻辑为 2000 → 1000 → 500；本轮没有改为每次按初始最大血量恢复。

旧版实际探针已复现：第一次致命伤把护盾缓存为空，之后动态挂盾不刷新，物理伤害仍掉血。新版在安装 / 清理复活盾时定点失效缓存，不给普通受击热路径增加重复查询；真实物理、能量、爆炸探针单独核对。复活盾使用一个可复用、到期禁用的小组件；默认 `ResistShield.Setup` 的旧护盾技能视觉入口保留，复活路径不再生成它的临时环形 Texture / Sprite 或根节点脉冲。

复活状态下不允许新攻击或 AI 移动，技能冷却继续按原逻辑推进；已经释放的弹体 / 召唤批次保留原行为。未释放的刀气 / 冲刺 / 出手视觉取消，分身仍按原假死策略存活。恢复交接前半段沿用原可受伤窗口；此时再次致命会消耗下一次剩余复活或真正死亡，旧协程不能随后装盾、复活或污染新池租约。

禁用、回池、数据重建、真实死亡、场景退出取消所属协程与渲染，清理死亡拦截标记和旧保护。视觉根独立于被隐藏的 Boss 渲染树，按组件所有权复用 / 销毁，因此可在身体隐藏期间显示核心，而不恢复碰撞或攻击。

## 资源与入口

新增 PNG 0 字节、Texture 0、Material 0。复用 `mat_boss_blade_solid_v1` 和当前字体材质；首次复活才创建一个核心网格及每个实际字体材质一个字形网格，后续复活复用，随所有者销毁。每字不创建临时对象；不再生成旧 TextFrag、DeathFrag、GlowDot、ReviveFlash、ReviveText、ShieldRing。

- [配置](Library/Boss/boss_revive_v1.json)，仅 Editor 读取。
- 字数上限、散开位移倍率、材质和排序由构建器应用；三段归一化进度、恢复和盾时长记录 V1 固定节奏并校验一致。改变这些固定节奏需同步更新代码、版本和实战证据。
- [字形与核心表现](../../Assets/Script/Game/Enemies/Boss/BossRevivePresentation.cs)、[复活模块](../../Assets/Script/Game/Enemies/Boss/Skills/ReviveModule.cs)、[护盾](../../Assets/Script/Game/Enemies/Components/ResistShield.cs)。
- `工具/VFX/Build Boss Revive V1` 维护四个 Boss 的 `BossReviveVisual`；`检查Boss复活配置`检查共享引用、字数上限和既有技能入口。
- `工具/VFX/实战验收/Boss复活`运行真实 Game、致命伤、表驱动模块与伤害探针；`结束并恢复`还原场景、窗口尺寸和验收开关。

## 验收入口

- [旧版报告](../../ArtOutput/UI/BossReviveV1/Before/validation-report.json) / [旧机制与漏盾观察](../../ArtOutput/UI/BossReviveV1/Before/mechanics-observation.json)：49 项通过，包含旧漏盾问题复现。
- [新版报告](../../ArtOutput/UI/BossReviveV1/After/validation-report.json) / [新机制观察](../../ArtOutput/UI/BossReviveV1/After/mechanics-observation.json)。
- [配置检查](../../ArtOutput/VFX/BossReviveV1/Audit/configuration.json) / [资源说明](../../ArtOutput/VFX/BossReviveV1/Audit/resources.json)。

新版 88 / 88 项实战通过：四组真实表配置、六次复活与最终死亡、三类实际伤害探针、保护结束后再受伤、字形回到原位、物理根保持、暂停、不同阶段回池 / 禁用 / 重建、恢复窗口再次致命、旧攻击取消和恢复后再出手、网格 / 纹理 / 组件复用、32 字上限、非活动文字兜底、两位真实施法者与装饰限流饱和。

分身与头标 100 / 100 项回归通过，包括主身假死时保留同一批真实分身、后注册的复活监听，以及复活后继续正常活动。

召唤 70 / 70 项回归、导入预览工具 44 / 44 项通过；复活配置 9 / 9 项通过。四个 Boss prefab 重复构建逐字节一致，所有原组件字段和既有技能资源保持，无缺失脚本。

- [新版实际回放](../../ArtOutput/UI/BossReviveV1/after.mp4) / [局部预览](../../ArtOutput/UI/BossReviveV1/after-closeup.mp4) / [静态前后对比](../../ArtOutput/UI/BossReviveV1/comparison.png) / [时间戳导出记录](../../ArtOutput/UI/BossReviveV1/proof.json)。
- [分身 / 头标回归](../../ArtOutput/UI/BossReviveV1/RegressionClone/validation-report.json)。
- [召唤回归](../../ArtOutput/UI/BossReviveV1/RegressionSummon/validation-report.json) / [工具检查](../../ArtOutput/VFX/BossReviveV1/Audit/toolkit-report.json) / [重复构建](../../ArtOutput/VFX/BossReviveV1/Audit/rebuild.json) / [静态完整性](../../ArtOutput/VFX/BossReviveV1/Audit/integrity.json)。

Editor 验收不替代微信真机帧时、内存和最终上传包测量。后续 Spine 主体需要替换字形快照源与身体锚点，当前聚字实现不等于角色骨骼动画已经接入。


## 2026-10-29 散字距离微调（当前）

`scatterDistanceMultiplier=1.6`：字形中心散开位移扩大 60%，只改变平移距离。原散开完成 23%、回聚开始 46%、结束 96% 的进度、旋转、字形缩放与透明度曲线保持；等待、恢复、护盾、血量与次数不变，因此在相同时间内收回。字段由当前专用构建器写入四份兼容 Prefab，零新增贴图和材质。

[本轮实战](../../ArtOutput/UI/BossReviveScatterV1/After/validation-report.json)额外核对实际字形中心位移的 1.6 倍比例和原 96% 进度归位；[原速预览](../../ArtOutput/UI/BossReviveScatterV1/after-closeup.mp4)、[前后对比](../../ArtOutput/UI/BossReviveScatterV1/comparison.png)、[完整性审查](../../ArtOutput/VFX/BossReviveScatterV1/Audit/integrity.json)。前版对照读取上方 V1/After 归档，本轮独立输出；上方 88 项与旧录像保留为初版历史证据。
