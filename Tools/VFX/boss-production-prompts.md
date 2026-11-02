# Boss 制作提示词与执行指令

先填 [Boss 任务单](Templates/boss-skill-brief.md)，再替换 `{变量}`。这些模板描述本项目工作要求；复制时提供实际文件和截图。实现不需要新图片时，直接使用工程实现模板。

## 一次完整制作

```text
按 Tools/VFX/boss-production-workflow.md 制作 / 优化 Boss 技能「{名称}」，任务目录 {任务目录}，目标模块 {模块路径}，目标 Boss / 关卡 {ID}。
先查真实配表、触发时机、资源注册、伤害与锁向行为，保存修改前基线；填写 Boss 专用任务单。
视觉目标：{轮廓、朱红 / 橙金主次、节奏、锚点、参考路径}。
机制要求：保持 {原规则}；明确调整 {原值→新值、理由}。正式技能分配范围：{保持现有 / 指定新增}。
资源预算：{新增 PNG 字节、材质、每实例渲染器、预计并发}。先复用现有图集、实体网格与字体，确有需要再生成新美术。
完成独立表现组件、专用版本配置、构建 / 配置检查和真实技能验收。核对暂停、死亡 / 假死、回池、重建、旧回调和退场；按改动运行相关组合回归。
用实际时间戳输出完整与局部回放，记录机制变化、资源增量和限制。更新产出索引并运行目录检查。
本次交付范围：{单项或明确列出的任务}；观感验收与后续任务按本次约定进行。
```

## 先查机制与提出设计

```text
分析「{Boss技能}」的真实代码、表配置和当前 Prefab，不根据名称推断实际行为。
列出冷却、首次等待、锁向、发射 / 生成、命中 / 伤害、收招、死亡及回池的事件时间线，给出源码位置。
区分表现问题、真实机制缺陷和当前未配表能力。按「预警→释放→持续 / 飞行→反馈→消退」提出一版方案。
说明复用哪些已完成 Boss 资源、预计新增成本、需要明确记录的玩法调整，以及如何验证伤害、范围和取消。
将结果写入 {任务单路径}，并给出下一步制作的具体输入。
```

## 网格、字形及工程实现

```text
依据 {已填写任务单} 实现 Boss「{名称}」。模块管理真实事件、判定和生成；Presentation 管理阶段、锚点和可视几何。
世界危险范围固定在实际判定位置；身体反馈取可见字形 / 显式身体节点；弹体只缩放视觉子级；出生反馈取最终成功生成位置。
共享现有材质和图集，复用网格；字形按实际字体材质合批。表现不额外消耗玩法随机数，不逐圈 / 每字创建纹理、材质或临时对象。
明确暂停、取消、死亡 / 假死、禁用、回池、重建、销毁和旧回调所有权；危险预警与真实弹体保留完整。
给出专用构建与配置检查、真实事件和伤害探针，输出资源预算与未覆盖项。禁止凭视觉亮起就判定免伤、命中或继承已正确。
```

## 新生成美术：敌方单张图形

工具里启用真实透明背景，实际附上玩法截图与已有共享图集；需要编辑时附上目标图。只将正式选中的图形打包入 Assets。

```text
Create ONE original transparent production sprite for the enemy Boss skill {skill} in the bright top-down 2D cartoon game 文字割草机. Use the supplied gameplay screenshot and existing enemy VFX atlas as camera, palette and style references.
Subject: {subject}. Silhouette: {distinct compact readable form}. Direction: {right-facing or explicit orientation}; anchor: {position}; runtime display size: {size}.
Dominant vermilion and orange-gold, a clear dark burgundy contour, a very small warm pale core. Clean broad cartoon shapes, sparse fragments and negative space. Readable on pale sand and green grass while leaving word enemies legible.
Only draw {requested single subject}. Keep {safe margin} fully transparent around it. Do not include trajectories, cast circles or impact events unless explicitly requested as this asset.
True RGBA transparency, no matte, checkerboard, text, labels, scene, character or grid. No blue-red mixed main palette, diffuse white haze, full-screen bloom or fine noisy particles. Additional constraints: {constraints}.
```

## 新生成美术：一次性动画图集

```text
Create ONE original chronological animation sprite sheet for Boss {skill}, triggered at {actual event}, using the attached 文字割草机 gameplay and enemy VFX references.
Exactly {columns} columns × {rows} rows, {frames} equal cells in left-to-right, top-to-bottom order. One continuously evolving event: {frame-by-frame progression}. Fixed camera, scale and anchor {anchor}, with {transparent margin} inside every cell.
Compact {silhouette}, vermilion / orange-gold masses, dark burgundy edges and a tiny warm light core. Readable at {runtime cell size} on pale sand; preserve space around text enemies. Last frame nearly empty; no second burst unless specified.
True RGBA transparency. No background, matte, checkerboard, cell borders, labels or characters. No crop, frame crossing, center drift, pale haze or giant white disk. Constraints: {constraints}.
```

原稿布局与正式图集布局分别写明；时序、物理范围和技能生成由工程驱动。修图及原稿记录沿用[通用模板](prompt-templates.md)，按具体帧号列缺陷。

## 验收与收尾

```text
验证 Boss「{名称}」本次改动 {范围}。使用真实模块、真实 Prefab 和 Game 场景，按任务单核对实际伤害、数量、属性、锁向与范围。
未配表能力在隔离夹具临时注入并在报告注明；覆盖本次相关的生命周期和组合，保存原速帧时间戳与机制观察。
配置、资源预算、重复构建和共享文件完整性分别给证据。程序通过、观感确认、正式配表和真机状态分别记录，不将共用报告重复累加。
恢复原场景、窗口、测试开关、输出目录和后台设置。更新版本说明、配方、任务单与 Tools/VFX/boss-deliverables.json。
运行 node Tools/VFX/audit-boss-workflow.cjs --write-index，交付完整 / 局部回放、关键变化、资源增量及已知限制。
```
