# 回风舞柳表现微调 V1

保留突破前 `blade.png` 与突破后 `blade2.png` 的原图、大小及公转/自转节奏。普通版采用浅金风丝，突破版采用银白到淡紫的月弧，呼应两张原图的金属与宝石配色。

## 调整

| 项目 | 普通 blade | 突破 blade2 |
| --- | --- | --- |
| 尾迹宽度 | 0.22 → 0.125 | 0.22 → 0.19 |
| 尾迹保留时间 | 0.14 → 0.135 秒 | 0.14 → 0.155 秒 |
| 颜色 | 象牙白、香槟金、低透明橄榄金 | 银白、淡紫、低透明灰紫 |
| 层级 | 刀片下方一层 | 原来与刀片同层，改为刀片下方一层 |

两档共用一份新材质，复用现有 `GongkaoMower/TalismanTrailSoft` Shader，使边缘柔化、尾端平滑收细。每刀仍为一个 SpriteRenderer 和一个 TrailRenderer，无新增粒子系统、图片或运行时材质实例。

`OrbitingBladeVisual` 在冷却隐藏、对象池回收时清空尾迹，重新启用后等到 LateUpdate 定位完成再恢复发射，避免跨位置连线。突破预制体的赋值移到 `ApplyRuntimeStats` 重建之前，保证绑定刷新时当次就使用 blade2。

技能数量、伤害、范围、碰撞、冷却和持续时间配表未修改。

## 验证和预览

- `ArtOutput/VFX/OrbitingBladePolishV1/regular-runtime.png`：真实技能 Lv.4 运行截图。
- `ArtOutput/VFX/OrbitingBladePolishV1/breakthrough-runtime.png`：真实技能 Lv.5 运行截图。
- `ArtOutput/VFX/OrbitingBladePolishV1/validation.json`：13 项 Play Mode 检查全部通过。覆盖 Lv.1 / Lv.4 / 突破的刀片资源与数量、原始参数、材质共用、层级、冷却显隐、移动后首次渲染无旧位置样本、暂停、卸下及池化复用。
- `ArtOutput/VFX/OrbitingBladePolishV1/Baseline/`：本轮修改前的两份预制体和两份代码备份。

截图来自临时空场景中的真实技能与真实尾迹，并非正式地图战斗截图。未进行多技能战场叠加、伤害碰撞回归、微信真机性能或包体测量。

编辑器菜单：`工具/VFX/回风舞柳/应用微调 V1` 可重复应用同一组表现参数。运行验证组件为 `Assets/ArtPreview/Skills/OrbitingBladePolishValidation.cs`，仅编辑器编译，只有 SessionState 的 `OrbitingBladePolish.Validate` 标记为 true 时才启动；在临时空场景进入 Play Mode 执行，不读写玩家存档。
