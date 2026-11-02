# 刀光剑影 V1

2026-10-28，初版已接入并通过 Editor 实战验收。同日根据「飞刀偏长、命中离文字有距离」追加修订，详见[飞刃长度与命中落点修订 V1](hit-feedback-alignment-v1.md)：本轮刀光剑影 105 项、五类技能共 311 项实战检查通过，另有 93 项字形检查。以下参数表已更新为本轮值，文末 64 项报告保留为初版历史证据。

原版普通、满级、环绕弹丸共用灰白刀片，普通没有拖尾，升级版只有短白尾迹。本版普通改为银白青锋、青蓝刃与深蓝轮廓；满级和环绕使用独立金青双刃，增加宽度与短剑气。命中播放八帧交错切痕，暴击分裂飞刃沿用普通外观。浅沙和草地均使用真实地图、文字怪与碰撞留证。

## 正式参数与资源

| 项目 | 普通 | 满级 / 环绕 |
| --- | --- | --- |
| Sprite | sword_qingfeng_v1 | sword_double_edge_v1 |
| 本地尺寸倍率 X / Y | 0.704 / 1.188 | 0.8 / 1.45 |
| 柔边拖尾时长 / 宽度 | 0.065 s / 0.065 | 0.095 s / 0.10 |
| 命中表现半径 | 0.34 | 0.38 |

命中是单层、单粒子八帧动画，播放 0.27 s，0.33 s 回池。排序为飞刃 -3、拖尾 -4、命中 -5，均在文字怪下方。拖尾复用既有 `GongkaoMower/TalismanTrailSoft` Shader，不增加拖尾贴图。池化发射和回收会清空尾迹，避免复用拉线。

本轮只将飞刃 X 缩短 20%，保持上一版 Y 厚度和贴图、拖尾、物理。符箓与飞刃的命中点改取可见字符内侧，并短时跟随字块；暂停、死亡和池复用的具体处理及跨技能修订见[命中落点记录](hit-feedback-alignment-v1.md)。

三种飞刃和命中共用一张 [512×256 图集](../../Assets/Res/Textures/VFX/sheet_sword_presentation_v1.png)，源 PNG **37,493 字节（36.6 KiB）**，低于 96 KiB 预算；两个共享材质，每发飞刃两个 Renderer。WebGL 为 ETC2_RGBA8Crunched，关闭 mipmap 和 CPU 可读副本，Clamp / Bilinear。无 mipmap 的 ETC2 RGBA8 块数据为 **128 KiB**；这与源 PNG 和最终微信上传包是不同指标。

原稿由内置 image_gen 生成；[源图](../../ArtOutput/VFX/SwordPresentationV1/Sources)与[完整提示词](../../ArtOutput/VFX/SwordPresentationV1/prompts.md)均已归档。构建器只做透明边界识别、等比采样、填充与排版。上排为八个带透明间隔的 64×64 命中格，下排为两个 256×128 飞刃 Sprite，中间一排留空。

## 制作与复用

- [投射物配置](Library/Projectiles/sword_presentation_v1.json)：调整飞刃、拖尾和表现半径；菜单「工具/VFX/Build Sword Presentation V1」同步现有资源，「检查刀光剑影配置」检查漂移和预算。
- [命中配方](Library/Recipes/sword_hit_v1.json)：工作台中查看切痕、时间轴、浅沙 / 草地 / 深底以及多实例；可导出新 id 复用图集的前八格。
- 「工具/VFX/实战验收/刀光剑影」运行真实技能夹具；结束后使用「结束并恢复」。输出到独立 Toolkit 时间目录。

投射物配置属于专用 Editor 构建器，不能交给一次性粒子导入器。改命中时间时同时修改投射物配置、命中配方，并重新构建、验收；不再自动乘 1.5。配置 JSON 不在运行时加载。

## 初版验收与历史证据

本轮修订前，原灰白素材版 **41 项**、青锋 / 金青双刃初版 **64 项**实战检查通过。涵盖普通 / 满级 / 草地、八剑环绕、Legend 十六剑、六发普攻与暴击三分裂、实际碰撞伤害、发射节奏、穿透、暂停恢复、拖尾复用、命中限流、随机状态、退场和存档保持。初版连续技能录像按实际采样时间导出，未慢放；其中同时活动的命中峰值为普通 2、满级 5、Legend 6。这些数据属于上一版夹具，不能作为缩短飞刃与命中锚点修订的验收结果。

- [初版实战报告：64 项](../../ArtOutput/UI/SwordPresentationV1/After/validation-report.json)
- [初版完整回放](../../ArtOutput/UI/SwordPresentationV1/after.mp4) / [八剑近景](../../ArtOutput/UI/SwordPresentationV1/burst-closeup.mp4) / [十六剑近景](../../ArtOutput/UI/SwordPresentationV1/legend-closeup.mp4)
- [初版前后对照](../../ArtOutput/UI/SwordPresentationV1/comparison.png)
- [资源统计](../../ArtOutput/VFX/SwordPresentationV1/Audit/resources.json) / [完整性检查](../../ArtOutput/VFX/SwordPresentationV1/Audit/integrity.json)
- [工具回归](../../ArtOutput/VFX/SwordPresentationV1/Audit/toolkit-report.json) / [重复构建检查](../../ArtOutput/VFX/SwordPresentationV1/Audit/rebuild.json)

初版完整性报告记录：技能定义、技能与投射物的七个核心文件与初版任务前快照逐字节一致，三个旧 Prefab 的原物理、玩法组件与 GUID 保持。本轮修订同样不改变伤害、速度、冷却、发射数量、环绕和分裂规则，另行调整命中表现的坐标与跟随代码；最终验收以[独立修订记录与报告](hit-feedback-alignment-v1.md)为准。微信真机帧时、内存与最终构建增量尚未测量。
