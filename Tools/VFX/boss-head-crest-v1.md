# Boss 头顶标识 V1

2026-10-28。替换 Boss 字体上方悬空的两把旧小刀。`LeftHorn / RightHorn` 原来复用 `xd.png` 弹体图片，只是静态装饰，与真正的 `KnifeBullet` 发射无关。

现在本体显示一枚紧凑的金橙冠徽，深褐描边、朱红中心；分身显示一对朱红空心菱形，配合已有的半透明文字和脚底断续印。移除分身旧的固定高度头标，避免双重标识。徽记下沿距可见字形顶部约 0.13 世界单位，宽 0.86，不再把装饰放在左右相距 3 单位的固定位置。只有幅度 0.012 的缓慢上下浮动，随游戏暂停停止。

四个 Boss prefab 均已接入。[公共外观配置](Library/Boss/boss_head_crest_v1.json)由 Editor 构建器读取。构建菜单：`工具/VFX/Build Boss Head Crest V1`；配置检查：`工具/VFX/检查Boss头顶标识配置`。构建器只删除 `Visual/LeftHorn` 和 `Visual/RightHorn`，保留 `AbilityRoot/KnifeSpawnPoint`、投射物注册、碰撞和技能组件。

实现：[运行组件](../../Assets/Script/Game/Enemies/Boss/BossHeadCrestPresentation.cs)、[构建器](../../Assets/Editor/BossHeadCrestV1Builder.cs)。使用已存在的 `mat_boss_blade_solid_v1`，没有新增 PNG、Texture 或材质；每个 Boss 一个复用的小型运行网格，本体 54 顶点、分身 64 顶点。无每帧临时数组、粒子系统或 Trail；销毁时释放网格。

实战 100 / 100 项通过，沿用真实分身验收流程，覆盖出生、继承技能、死亡、假死、自然到期、暂停、回池和复用；增加不同字数、显式头部节点、文字隐藏、节点移动、复活隐藏与恢复、真实分身头标检查。配置 16 / 16 项通过，四个 prefab 重建一致，所有保留的原组件字段与技能数值保持。报告与截图：[本轮实战](../../ArtOutput/UI/BossHeadCrestV1/After/validation-report.json)、[配置检查](../../ArtOutput/VFX/BossHeadCrestV1/Audit/configuration.json)、[静态完整性](../../ArtOutput/VFX/BossHeadCrestV1/Audit/integrity.json)、[前后对比](../../ArtOutput/UI/BossHeadCrestV1/comparison.png)、[17 秒实际回放](../../ArtOutput/UI/BossHeadCrestV1/after-closeup.mp4)。微信真机性能和最终上传包未测量。

## 后续替换 Spine 主体

当前项目未发现 Spine runtime 或角色资产。本轮只给头标保留绑定入口，没有引入 SDK 或制作角色骨骼。

- `BossBrain`、`EnemyBase`、碰撞体和技能继续留在 Boss 根节点；可替换的角色表现放到 `Visual` 下，避免把攻击判定挂到骨骼形变上。
- 冠徽的 Inspector 已支持 `headAnchor` / `bodyRenderer`，也可调用 `BindBody(socket, renderer)`。优先跟随显式头部节点，其次使用绑定渲染器的包围盒；未绑定时才读取当前文字的实际字形范围。Spine 的头骨跟随节点可以作为这个 socket。
- 真正切换角色时，需要一并迁移现有刀气、冲刺、小刀的出手 / 命中 / 身体后沿锚点，以及分身的文字重影、整体透明和复活碎字；本轮头标可绑定不等于全部技能已经兼容 Spine。
- 首个样本先选一位 Boss，准备待机、移动、蓄力、出手、受击、死亡六组动作，分身复用同套资源。名字改为独立头顶名牌；技能预警保留当前危险色与真实发射时序。
- 先在真实关卡验证动作与事件、换向、排序、暂停、回池和分身，再测微信设备的图集占用、骨骼开销与帧率，确定共享图集和骨骼数量预算后推广。

第二批顺序仍为分身验收 → 召唤 → 复活；本轮是 Boss 公共外观修订，不启动后两项。
