# 罡气护体 V2：视频参考力场

历史版本：2026-10-27 当前绑定已更新为 [V3 内外反转与突破加强](field-generator-v3.md)。以下记录保留 V2 当次实现与验收。

2026-10-26。已绑定实际技能。参考用户提供的 `QQ20261026-225222.mp4`：半透明覆盖区、持续向外传播的多圈六边形波纹，普通绿色，突破橙金。上一版的旋转火纹改为区域能量流动，角色、怪物文字和地面仍清晰可见。

## 表现与时序

三圈波纹错开三分之一周期，从小到大传播并在边缘淡出。覆盖区与较弱的圆形外沿表达真实半径，六边形亮带始终位于圆形范围内；力场固定朝向并跟随角色移动。普通 Lv1—4 使用绿色，Lv5 与既有正数 Legend 阶段使用橙金。

单圈传播时间为原视觉周期 × 1.6；例如 Lv1 为 2.24 秒，Lv5 为 1.12 秒。三圈交错，所以不会等一圈完全消失后才出现下一圈。这个倍率只用于视觉运动，伤害 Tick、DPS、半径、羁绊与突破条件不变。暂停冻结波纹，卸下或拥有者销毁时清理动态 Mesh。

## 资源与边界

| 项目 | 本版 |
| --- | --- |
| 专用贴图 / 图集 | 0，不生成运行时纹理 |
| 渲染层 | 半透明覆盖、六边形波纹、薄范围边，共 3 个 MeshRenderer |
| 材质 | 1 个共享的 Sprites/Default，无自定义 Shader |
| 共享 Mesh | 3 份，共 865 个顶点、1,344 个三角形 |
| Prefab + 材质 + Mesh 源资产 | 88,914 字节，约 86.8 KiB，不含 .meta / 脚本 |
| 实例额外 Mesh | 1 份 375 顶点波纹 Mesh，初始化后复用顶点 / 颜色数组 |
| 粒子 / 碰撞组件 | 0 / 0 |

普通与突破复用全部几何和材质，只切换颜色。实际资产字节和 Editor 内存 API 读数见 [资源记录](../../ArtOutput/VFX/FieldGeneratorV2/Audit/resources.json)。这不是微信包体或真机性能测量。V1 资产保留作历史对照，新力场的资源依赖中不含其贴图。

参考视频、抽帧和对照图保存在 Assets 外；视频中的白色竖线、交叉光束、足球、导弹等其他武器没有接入本技能。几何和透明渐变由工程代码实现，不直接抽取视频画面作游戏素材。

## 配置与工具

- [配置](Library/Persistent/field_generator_v2.json)：普通 / 突破颜色、区域透明度、亮带透明度、传播倍率与排序。
- Unity「工具 → VFX → Build Field Generator Surface V2」：重建并绑定本版；重复构建保留 GUID。
- Unity「工具 → VFX → 常驻特效 → 罡气护体预览」：自动读取当前技能引用，切换浅沙 / 草地 / 深底、普通 / 突破，调整半径、周期、强度及导出 PNG。
- Unity「工具 → VFX → 常驻特效 → 检查罡气护体配置」：核对资源与配置，包括两种颜色。
- Unity「工具 → VFX → 实战验收 → 罡气护体」：真实 Game 场景、隔离内存存档、独立输出目录；「结束并恢复」返回原场景。

已有的 Register FieldGenerator 菜单也调用 V2 构建器。V1 构建菜单与历史配置仍保留，主动重建 V1 会切回 V1；历史检查要求当时绑定 V1。

## 验收与证据

54 项运行检查通过，覆盖 AOE 内外、原周期伤害、数值保持、真实升级与羁绊门槛、突破及 Legend、透明度、波纹范围、跟随和角色缩放、随机数保持、暂停恢复、Mesh 复用 / 释放、缺失美术回退、卸下与退场、存档保持及无运行错误。通用导入 / 打包 / 预览工具的 38 项检查也通过。

另外 9 项力场预览检查覆盖采样 / 反向拖动、颜色切换复用、八实例与关闭后的 Mesh 清理。预览器会显式释放动态波纹 Mesh，避免 PreviewRenderUtility 不调用未唤醒脚本 OnDestroy 时残留资源；此修复后重新跑过实际技能验收。6 份资源重复构建字节一致且 GUID 保持。

- [同等级 V1 / V2 对照](../../ArtOutput/UI/FieldGeneratorV2/comparison.png)
- [实际动效 MP4](../../ArtOutput/UI/FieldGeneratorV2/after.mp4)：先 Lv4 绿色普通态，再 Lv5 橙金突破态；两段各约 3 秒，按采集时间导出。
- [运行报告](../../ArtOutput/UI/FieldGeneratorV2/After/validation-report.json) / [采集时序](../../ArtOutput/UI/FieldGeneratorV2/proof.json)
- [资源与配置审计](../../ArtOutput/VFX/FieldGeneratorV2/Audit) / [需求记录](../../ArtOutput/VFX/FieldGeneratorV2/brief.md)

导出：`Tools/VFX/render-field-generator-v2-proof.cjs`；源码 / 定义完整性复核：`Tools/VFX/audit-field-generator-v2-assets.cjs`。旧版画面来自上轮真实采集，本版画面来自当前真实运行；未慢放旧录像。
