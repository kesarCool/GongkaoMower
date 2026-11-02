# 飞刃长度与命中落点修订 V1

2026-10-28。用户反馈「飞刀观感长了点，还有所有的命中反馈，都离文字怪有一段距离」。本轮缩短刀光剑影的飞刃，并统一处理文字命中的视觉锚点。**五类技能共 311 项实战、93 项 Editor 字形、41 项导入预览工具检查全部通过。**

## 飞刃长度

只把三种飞刃 Sprite 的 X 缩短 20%，Y 厚度、图集、材质、拖尾和碰撞体保持原参数。

| 变体 | 上一版本地缩放 | 本轮本地缩放 |
| --- | --- | --- |
| 普通 / 暴击分裂 | (0.88, 1.188, 1) | (0.704, 1.188, 1) |
| 满级 / 环绕 | (1, 1.45, 1) | (0.8, 1.45, 1) |

继续共用 [512×256 图集](../../Assets/Res/Textures/VFX/sheet_sword_presentation_v1.png)，PNG 仍为 **37,493 字节（36.6 KiB）**，本轮不新增贴图。源 PNG 字节与微信最终构建增量分别统计，后者尚未测量。

## 命中锚点与跟随

原命中点由 Collider 的最近点或投射物根坐标决定。文字怪的字号、旋转、子物体偏移与受击位移独立于 Collider，因而画面中的文字可能与命中点分离；持续移动也会让固定世界点上的切痕落在字块后方。

`EnemyWordLabel.TryGetHitAnchor` 从 TMP 当前可见字符的几何四边形中取距离来袭点最近的位置，向该字符内部收 **18%**。旋转、缩放和子物体偏移通过文字 Transform 转换；空格不作为可命中字形。文本或布局变化时刷新 TMP 几何。找不到可见字形时，`CombatHitPlacement.Resolve` 使用原 Collider 最近点回退；视觉中心接口使用字形范围中心，无字形时回退目标根坐标。

符箓与飞刃的短时命中烙印保存文字局部锚点，播放期间跟随字块移动和受击变换。暂停时冻结跟随和动画时间；失效目标仍可解除绑定。文字组件禁用和启用都会更新版本，敌人死亡、回池，以及同帧回池后重新借出时，旧烙印停止跟随。特效自身借出和归还时也清理绑定，避免复用后追随上一次目标。

| 表现入口 | 本轮视觉处理 | 保持的玩法坐标 |
| --- | --- | --- |
| 普通、满级、环绕及分裂飞刃；普通和满级符箓 | 字符内侧命中点，短时跟随文字 | 原 Collider、伤害、速度、发射数量、穿透、环绕与分裂规则 |
| 雷霆万钧 | 特效中心对齐目标可见字形中心 | 原 AOE 中心、伤害与技能事件坐标 |
| 导弹 | 碰撞伤害前捕获字形接触点，用于爆炸特效 | 原弹体位置作为 AOE 中心，原伤害流程 |
| 射线 | 冲击点使用字形接触点 | 原射线检测、伤害与事件坐标 |
| 手雷、弹射雷 | 保留真实落地爆点 | 原落点与 AOE 中心 |
| 随机余爆 | 保留随机爆点，不吸附某只怪 | 原随机点与对应 AOE 中心 |

本轮不移动敌人的碰撞体，也不修改寻敌和伤害判定。文字明显偏离根坐标时，雷击或导弹的视觉中心与原 AOE 中心可能不同；这是本轮保留玩法坐标的明确取舍。地面爆炸和余爆不能统一吸附到文字上。

## 验证与证据

[Editor 字形检查](../../ArtOutput/VFX/HitFeedbackAlignmentV1/anchor-checks.json) **93 项通过**。使用真实 enemy001 / enemy003 的临时实例，覆盖各方向来袭、旋转与非等比缩放、长词、字符空隙、空白文本、视觉中心、Collider 回退、随机状态保持、生命周期版本失效，以及临时对象清理和场景 / 预制体保留。该检查不替代实战中的移动跟随与池复用验收。

| 实战夹具 | 本轮报告 | 当前结论 |
| --- | --- | --- |
| 刀光剑影 | [Sword/validation-report.json](../../ArtOutput/UI/HitFeedbackAlignmentV1/Sword/validation-report.json) | 105 项通过；含实际碰撞贴字、六种飞刃 / 符箓跟随、暂停、同帧复用和保留对象的致死复活检查 |
| 天师符令 | [Talisman/validation-report.json](../../ArtOutput/UI/HitFeedbackAlignmentV1/Talisman/validation-report.json) | 50 项通过 |
| 雷霆万钧 | [Lightning/validation-report.json](../../ArtOutput/UI/HitFeedbackAlignmentV1/Lightning/validation-report.json) | 72 项通过；视觉字形中心与原 AOE 分别核对 |
| 爆炸与导弹 | [Explosions/validation-report.json](../../ArtOutput/UI/HitFeedbackAlignmentV1/Explosions/validation-report.json) | 56 项通过；导弹有 / 无表现伤害对照及地面爆点回归 |
| 射线 | [Beam/validation-report.json](../../ArtOutput/UI/HitFeedbackAlignmentV1/Beam/validation-report.json) | 28 项通过 |

已导出并检查 [完整回放](../../ArtOutput/UI/HitFeedbackAlignmentV1/sword-after.mp4)、[近景](../../ArtOutput/UI/HitFeedbackAlignmentV1/sword-closeup.mp4) 与 [前后对照](../../ArtOutput/UI/HitFeedbackAlignmentV1/comparison.png)。连续录像按实际时间戳导出，未慢放；静态飞行图使用 0.25 倍时间采样后冻结，以避免编辑器帧率波动跨过碰撞时刻。前后静态图来自各自真实碰撞用例，并非同步逐帧对比。

[完整性审计](../../ArtOutput/VFX/HitFeedbackAlignmentV1/integrity.json) 18 项通过，核对雷击 / 导弹 AOE 代码、射线唯一视觉调用、三个投射物的物理 / 玩法组件和 GUID，以及图集字节保持；[配置库](../../ArtOutput/VFX/HitFeedbackAlignmentV1/library-audit.json) 9 项与 [工具回归](../../ArtOutput/VFX/HitFeedbackAlignmentV1/toolkit-report.json) 41 项通过，测试资源已清理。上一版刀光剑影的 64 项报告保留为历史。微信真机帧时、内存与最终构建增量仍待测量。

## 维护入口

- 飞刃比例由 [投射物配置](Library/Projectiles/sword_presentation_v1.json) 和 `SwordPresentationV1Builder` 同步；Unity 菜单「工具/VFX/Build Sword Presentation V1」重建。
- Unity 菜单「工具/VFX/检查命中反馈字形锚点」执行本轮 Editor 检查。
- 运行时锚点入口为 `EnemyWordLabel` / `CombatHitPlacement`，命中入口为 `ProjectileHitFeedback`，跟随和清理由 `PooledCombatVfx` 承担。
- [刀光剑影初版制作与历史证据](sword-presentation-v1.md)保留原美术、打包和初版实战记录；本轮结论以此修订及独立报告为准。
