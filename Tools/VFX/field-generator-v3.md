# 罡气护体 V3：内外反转与突破加强

2026-10-27。已接入实际技能。继续参考用户提供的视频，补全内外圈反向转动，并让突破态通过轮廓、亮带和转速体现提升。

## 当前表现

普通绿色：保留半透明覆盖区，三圈六边形一边扩散一边交错反转；另有两条内弧与两条外弧反向转动，中心通透。内弧与第一 / 第三圈逆时针，外弧与第二圈顺时针。角度独立于扩散相位累计，波纹淡出重新生成时不会重置旋转。

橙金突破：波纹亮带宽度为普通态的 1.7 倍，弧段变长并出现更亮的金白核心；外缘增加六枚旋转能量棱角，覆盖区略微加强。旋转速度从 24°/秒提高至 34.8°/秒。升级切换时保持当前角度，六枚棱角只在突破态可见。

原扩散周期 × 1.6 继续保留。Lv1 单圈传播 2.24 秒，Lv5 为 1.12 秒；多圈交错，不产生等待空档。DPS、伤害 Tick、真实半径、五级羁绊门槛和正数 Legend 阶段规则均保持。完整 SkillFieldGenerator 源码与 V2 备份逐字对比一致。

## 资源控制

| 项目 | 本版 |
| --- | --- |
| 新增专用贴图 / 自定义 Shader | 0 / 0 |
| 渲染器 / 共享材质 | 3 个 MeshRenderer / 1 个既有材质 |
| 共享几何合计 | 1,179 个顶点、1,800 个三角形 |
| 动态网格 | 每实例初始化 1 份 689 顶点 Mesh；全部波纹、弧段、棱角合并 |
| Prefab + 材质 + Mesh 源文件 | 109,593 字节，约 107.0 KiB；预算 128 KiB |
| 复用资源 | V2 材质、覆盖 Mesh、圆形边界 Mesh |
| 本轮新增 Prefab + Mesh | 57,860 字节，约 56.5 KiB，不含 .meta / 脚本 |

逐 Tick 复用顶点与颜色数组，不增加对象或材质实例；三角函数按波纹 / 弧段采样点计算，五层柔边共用结果。暂停不推进角度或波纹，卸下 / 拥有者销毁时释放私有 Mesh，独立预览关闭时显式释放。源资产字节与 Editor 内存读数均不是微信包体或真机帧率；详细数据见 [资源记录](../../ArtOutput/VFX/FieldGeneratorV3/Audit/resources.json)。

## 配置与制作入口

- [V3 配置](Library/Persistent/field_generator_v3.json)：两种颜色、透明度、扩散倍率、基础旋转速度、突破转速 / 带宽 / 区域增强及资源预算。
- Unity「工具 → VFX → Build Field Generator Orbit V3」重建并绑定，保留既有 GUID。
- 「常驻特效 → 罡气护体预览」自动读取当前引用，可切换两种状态、背景、半径、强度、周期，连续播放、拖动时间或回到起点。旋转播放不在每个扩散周期跳回起始角度。
- 「常驻特效 → 检查罡气护体配置」自动检查当前旋转力场；另有「检查旋转力场 V3 配置」。
- 「实战验收 → 罡气护体」使用真实 Game 场景和隔离内存存档；「结束并恢复」返回原场景。

旧 Register FieldGenerator 菜单也重建 V3。V1 / V2 构建器与资产留作历史；主动执行旧版构建会切回该版。参考视频沿用 [V2 Reference](../../ArtOutput/VFX/FieldGeneratorV2/Reference)，不复制进 Assets，也不从中提取游戏贴图。

## 验收与证据

64 项实际战斗检查通过，新增覆盖内外反向旋转、突破六枚棱角 / 金白核心、提高转速时角度连续、增强几何不超真实范围、绿色普通态在草地的实际采样。其余覆盖原伤害、内外目标、等级门槛、Legend、随机数保持、跟随 / 缩放、暂停恢复、卸下、动态网格复用 / 释放、资源缺失回退、退场和存档保持。

9 项独立预览检查覆盖反向拖动、状态切换复用、八实例与关闭清理；通用导入 / 打包 / 隔离预览工具的 38 项检查也通过。重复构建的六份资产字节和 GUID 保持。

- [放大动态对照](../../ArtOutput/UI/FieldGeneratorV3/rotation-comparison.mp4)：左普通 Lv4，右突破 Lv5；裁切实际运行画面，保留采集时序。
- [V2 / V3 同等级截图对照](../../ArtOutput/UI/FieldGeneratorV3/comparison.png)
- [完整实际回放](../../ArtOutput/UI/FieldGeneratorV3/after.mp4)：普通 / 突破各约 4 秒。
- [运行报告](../../ArtOutput/UI/FieldGeneratorV3/After/validation-report.json) / [时序记录](../../ArtOutput/UI/FieldGeneratorV3/proof.json)
- [完整性与资源审计](../../ArtOutput/VFX/FieldGeneratorV3/Audit) / [本轮修改前备份](../../ArtOutput/VFX/FieldGeneratorV3/Baseline)

导出脚本 `Tools/VFX/render-field-generator-v3-proof.cjs`，完整性复核 `Tools/VFX/audit-field-generator-v3-assets.cjs`。本轮未测微信真机帧率或最终上传包大小。
