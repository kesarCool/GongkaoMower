# 技能视觉提示词模板

Boss 全流程使用[专用制作指令](boss-production-prompts.md)和[Boss 任务单](Templates/boss-skill-brief.md)，涵盖机制分析、网格 / 字形实现、新美术和验收收尾；具体阶段见[工作流](boss-production-workflow.md)。本页继续提供单张 Sprite、一次性图集与修图的通用美术模板。

先填写 [视觉任务单](Templates/visual-brief.md)，再替换以下 `{变量}`。变量未填完整不要直接生成。效果设计、参数和验收由本项目决定，参考资料只用于学习流程。历史完整提示词保留在各样本的 `ArtOutput/VFX/.../prompts.md`。

## 变量清单

| 变量 | 填写示例 / 含义 |
| --- | --- |
| skill / event | 天师符令 / 有效命中当帧 |
| subject / silhouette | 朱红符印裂开 / 集中、外围稀疏 |
| palette / edge | 金色小亮芯、朱红主体 / 深红清晰轮廓 |
| columns / rows / frames | 原稿 4 / 4 / 16，正式选 8 帧；不要混成一个规格 |
| anchor | 单格中心；或落雷接触点 x50%、y88%（从顶部计） |
| progression | 接触 → 冲击展开 → 分裂 → 消散，明确每阶段帧号 |
| safe_margin | 各侧 12%–15%，落雷上下均需留空 |
| runtime_cell | 正式运行时每格 64×64 / 128×128 / 128×256 |
| constraints | 主体不能盖住文字、不出现第二次爆发、无长蓄力等 |

## 新生成：一次性命中 / 爆炸 / 电弧

生成工具中显式启用透明背景，并真正附上当前战斗截图作为参考输入；仅写“参考截图”不会自动提供图片。

```text
Create ONE production animation sprite sheet for {skill}, triggered at {event}, for the bright 2D cartoon game 文字割草机. Use the supplied gameplay screenshot for visual style and camera reference.

Subject: {subject}. Distinct silhouette: {silhouette}. Dominant palette: {palette}; clearly defined darker contour: {edge}; small bright core, sparse open spaces around the main shape. Bold clean hand-painted cartoon masses readable at {runtime_cell} pixels per runtime cell. Keep word enemies readable on pale-yellow sand and green grass.

Exactly {columns} columns by {rows} rows of equal cells, {frames} chronological frames in LEFT-TO-RIGHT, TOP-TO-BOTTOM order. ONE continuous evolving event, not different concept icons. Fixed camera, fixed scale registration and consistent anchor: {anchor}. Keep all artwork and every fragment inside its own cell, with at least {safe_margin} fully transparent margins on all four sides. Never cross a cell boundary or crop the effect.

Frame progression: {progression}. The first impact frame must react immediately to the existing damage event. No added windup unless specified as a separate pre-cast asset. Last frame nearly empty, no sudden full-size reappearance.

True clean RGBA transparency. No opaque matte, drawn checkerboard, grid lines, labels, letters, UI, scene, characters or unrelated objects. No photorealism, diffuse pale haze, giant white disk, fine noisy grain, or full-screen bloom. Additional constraints: {constraints}.
```

## 已验证题材的填写方向

| 题材 | silhouette / palette | progression 示例 |
| --- | --- | --- |
| 手雷爆炸 | 圆润火团或断环；橙红主体、深棕灰烟、小金芯 | 1–2 接触，3–6 冲击，7–11 分裂烟团，12–15 稀疏余烬，16 近空 |
| 连环爆破 | 尖锐红橙星芒、小暗红碎石 | 迅速展开并留出空隙；回响复用同一帧段，时长和大小另设 |
| 导弹命中 | 集中穿刺星芒、橙火与钢蓝灰烟 | 固定命中点，不把飞行导弹或弹道画进命中图 |
| 符箓命中 | 小金芯、朱红碎符印、少量碎光 | 小亮点 → 符印张开 → 稀疏碎片消散；不生成大面积法阵 |
| 普通落雷 | 细白青芯、紫蓝锐角分叉 | 主雷立即触地，沿同一路径收细断裂；落点余电独立生成 |
| 红色突破落雷 | 朱红 / 绯红、细暖白芯、深酒红边缘 | 专用分叉轮廓，电弧逐渐断开；不含蓝、青、紫主色，不做宽火焰片 |

落雷拆成主雷和落点两组时，分别生成、分别写锚点；最终可共用一张图集和一个材质。生成原稿不必与最终长方形图集规格相同，打包时保留主体比例。

## 修图：只修正明确缺陷

必须把待编辑图作为实际输入。提示词之外同样保留透明背景参数。

```text
Edit the supplied animation sheet. Preserve the exact {columns} by {rows} cell layout, chronological order, camera, registration, anchor, palette family and the same evolving action.
Fix only these observed defects: {defects_with_frame_numbers}.
Required corrections: {specific_corrections}.
Keep all artwork within each cell with {safe_margin} empty transparent margins. Preserve true RGBA alpha. Do not add new subjects, extra bursts, backgrounds, labels or frame borders. Preserve every already-approved aspect: {approved_aspects}.
```

适用修正例：某帧裁边、浅底发白、主雷像火焰、中心漂移、尾帧重新爆发。先列出具体帧号和现象，再限定修正范围；不要连续叠加“更精致更震撼”造成风格漂移。

## 单 Sprite / 飞行符纸

```text
Create ONE transparent replacement sprite of {subject}, using the supplied existing asset as an identity reference and the gameplay image as the style reference. Preserve its recognizable shape and {identity_details}. Improve contrast on pale sand with {palette_and_contour}. Clean simplified cartoon shapes, readable at {runtime_display_size}. Fixed {orientation}, anchor {anchor}, generous transparent margins. No animation grid, trail, impact effect, scene, labels or checkerboard. True RGBA alpha.
```

拖尾、弹道、碰撞和方向交给工程控制。单 Sprite、飞行拖尾、命中图集分别设计与验收。

## 每轮保留的记录

保存完整替换后的提示词、工具与参数、参考图路径、所有原始输出、最终选图、修正理由及源文件哈希。AI 不保证输出尺寸、透明度、帧布局或连续性，导入检查与逐帧验收仍然必需。
