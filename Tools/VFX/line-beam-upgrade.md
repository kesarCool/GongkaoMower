# 气贯长虹表现升级

2026-10-21，工程 `G:/Demo/GongkaoMower`。本轮完成气贯长虹接入；其余 11 个主动技能的静态审查与后续优先级见 [skill-visual-audit.md](skill-visual-audit.md)。

## 实际变化

- 普通射线由红色细线改为青蓝束体、青白束芯、较暗外缘与收尖束头；附有少量沿束流动的金色短划。
- 原冷却的最后约 100 毫秒出现凝气，跟随移动中的角色；命中时出现短促星芒，发射点有小幅脉冲。
- 配置为 0.08 秒的旧表现对应约 0.24 秒的新收束过程。视觉凝气位于原冷却内，伤害仍在原释放时刻即时结算。
- 满级普通三束与每第五次七色齐发保持原规则；七色沿用分色辨识并统一束芯和轮廓。
- 已释放的光束固定在世界坐标；暂停冻结生命周期且不重复上传相同网格，卸下技能立即清理。

## 实现与资源

- `Assets/Script/Game/Combat/Visuals/LineBeamPresentation.cs`：一个可复用动态 Mesh，组合光束、凝气和命中反馈。上限 8 束、12 个命中反馈点；共用一个支持顶点色和透明度的材质。
- `Assets/Script/Game/Combat/Skills/Implementations/SkillLineBeam2D.cs`：把视觉接到真实释放与命中位置，不变更伤害和方向计算。
- `Assets/Script/Game/Player/Components/PlayerSkills.cs`：创建及复用表现组件；`enhancedBeamVisuals` 为新版开关。
- `Assets/Prefab/Player/Player.prefab`：启用 `enhancedBeamVisuals`。关闭该开关可回看旧版细线表现。

新增的是代码和对应元数据，没有新增贴图、序列帧、字体或材质资产。技能定义脚本及 `SkillDef_LineBeam.asset` 与本轮开始时的备份内容一致。最终微信包体增量须以构建结果为准。

本轮开始前的相关文件备份位于 `Library/UIBackups/LineBeam-20261021-005336`。其他功能的已有工作未纳入本轮回退范围。

## 验证与预览

使用真实 Game 场景、Player、enemy001 碰撞体和技能配表，停止自动刷怪后创建固定目标；仅预览存档使用内存存储。

- 旧表现 15 项检查、新表现 28 项检查通过，含原冷却、范围内连续穿透、范围外无伤、单目标索敌、三束／第五发七束、暂停和移动、卸下清理、旧新版切换、视觉 API 不消耗战斗随机数、存档保持及运行无报错。
- 重复释放验证对象层级与当前材质复用；这不等同于全局分配或 GPU 性能测量。
- 静态图冻结真实施放帧；动图仅录制一次已进入原冷却末尾的真实施放，保留新一轮的伤害飘字。录制前让压力检查留下的旧飘字自然回收。

预览文件：

- `ArtOutput/UI/LineBeamAfter/comparison.png`
- `ArtOutput/UI/LineBeamAfter/runtime-cast.gif` 和 `runtime-cast.mp4`
- `ArtOutput/UI/LineBeamBefore/`：旧版对应画面与报告。
- `ArtOutput/UI/LineBeamAfter/validation-report.json`、`motion-validation.json`：新版验证结果。

验证入口为 `HomeUIEditorPreview.Begin("beam")`；`SessionState` 的 `LineBeamBaseline` 为 true 时使用旧版，否则使用新版。`HomeUIEditorPreview.Finish()` 退出预览并恢复原场景。素材拼接脚本为 `Tools/UI/render-line-beam-proof.cjs`，它只使用通过验证的实际截图与帧时间。

尚未验证微信真机帧时、GPU 过绘、最终构建体积及多技能叠加的完整战斗可读性。
