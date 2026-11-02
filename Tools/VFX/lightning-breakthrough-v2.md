# 雷霆万钧：专用红色突破 V2

当前播放与回收时间已延长为原来的 1.5 倍，最新参数与验证见 [时长调整记录](vfx-duration-1.5x.md)。下文保留原版本记录。

2026-10-25。按反馈重新设计突破效果，替换红蓝叠加方案。新图以朱红、绯红为主调，搭配窄暖白亮芯与深红边缘；主雷、分叉、余电由同一组新美术构成，使用标准透明材质。普通状态仍为原蓝色落雷。

## 设计与精度

突破使用两组专用透明序列：主雷接触后闪击、断裂；落点星芒展开为中空电弧，再分散消失。两组各 8 帧。主雷持续 0.18 秒、落点余电 0.32 秒、0.36 秒归还对象池。

运行图集 1024×512，8 列×2 行，每格 128×256；上一版格子为 64×128。主雷取样区域为 108×224、落点为 108×108，格内留透明边距。画面使用新绘制的分叉结构，取消旧版同一路径的蓝红宽度叠加和通道改色 Shader。

每道雷只有两个单粒子系统，两个粒子共用一张图集、一个 Sprites/Default 材质。文字保持在特效前层，无全屏闪光、Bloom 或持续伤害。视觉沿用实际 strikeRadius 缩放；落点电弧属于表现，不新增判定。

## 资源预算

| 项目 | 实测 |
| --- | --- |
| 新专用 PNG | 256,422 字节，约 250.4 KiB；上限 256 KiB |
| 分辨率 | 1024×512，无 mipmap、无 CPU 可读副本 |
| WebGL 导入 | ETC2_RGBA8Crunched，质量 70 |
| 新纹理 ETC2 块数据 | 524,288 字节，512 KiB |
| Editor 纹理 API 内存读数 | 618,988 字节，含 Unity 对象等开销 |
| 运行表现 | 2 个粒子系统、最多 2 个粒子、1 个材质 |

与上一版共用普通图集的方案相比，明确增加了这一张纹理；普通图集仍为 54.4 KiB PNG、128 KiB ETC2 块数据。如果普通、突破两张纹理同时驻留，合计 ETC2 块数据为 640 KiB，不含其他对象和压缩数据开销。这些数字都不是最终微信包体。

新运行资产：

```text
Assets/Res/Textures/VFX/sheet_lightning_breakthrough_v2.png
Assets/Res/Materials/VFX/mat_lightning_breakthrough_v2.mat
Assets/Res/Prefabs/Effects/Impact/fx_lightning_breakthrough_v2.prefab
```

高清原稿保存在 Assets 外的 `ArtOutput/VFX/LightningBreakthroughV2/Sources`。旧红蓝突破的 Prefab、材质、改色 Shader 和构建器已移至同目录 `Retired/Assets`，避免旧构建菜单重新覆盖新版。归档源文件 SHA256 保持一致；归档过程的元数据处理错误已修复，使用 Unity 编辑器缓存中的原 GUID 重建标准导入元数据，旧 Prefab→材质→Shader 引用已核对。详情见 `Audit/retirement.json`。

## 接入与验证

本轮不修改技能运行代码或升级规则，只将技能定义的 maxLevelPrefab 绑定到 V2。原有羁绊被动解锁的第五级、角色 Legend stage≥2 都自动选择新突破效果。普通一级至四级、Rare stage 1 的表现规则保持原状。

72 项真实 Game 场景运行检查通过，验证期间无运行异常：

- 通过实际升级 API 验证羁绊门槛、第五级切换、Legend、数值刷新、实例重建与资源缺失回退。
- 普通单击伤害 2.5；满级仍为 5 道、总伤害 13.75；逐道 0.18 秒、冷却、索敌和 AOE 内外保持原规则，重复碰撞器不增加伤害。
- 主雷与落点对齐；半径缩放、复用尺寸恢复、暂停冻结、恢复回池、密集命中限流、限流后仍造成伤害、场景退出清理均通过。
- 单独突破五连击和雷电＋手雷爆炸＋符令叠加回放都记录到真实伤害、五道雷及可见效果；两个回放的雷电部分采样峰值均为 4 个 ParticleSystem 组件、3 个存活粒子。计数不是并发上限或真机帧率基准。
- 普通蓝色图集 SHA256 不变，定义文件除突破引用外逐字段保持一致，普通与突破 Prefab 均无 Missing Script。

本轮未进行微信导出、真机帧率或最终包体测量。

## 预览与复现

`ArtOutput/UI/LightningBreakthroughV2/comparison.png` 对比上一版红蓝突破和新专用红色突破。两边均是实际第五级技能命中、粒子采样至 0.035 秒的局部裁切。`breakthrough.mp4`、`breakthrough.gif` 包含单独五连击与三技能叠加回放，保留实际采集帧间时长。

原始帧、时间戳、闪击/余电截图、草地截图与验证报告也在该目录。资源和配置审计在 `ArtOutput/VFX/LightningBreakthroughV2/Audit`。此前 V1 预览和原稿保留，可回看。

Unity 菜单 `工具/VFX/Build Red Lightning Breakthrough V2` 重建图集并绑定突破资源。验证时设 SessionState `LightningBreakthroughV2Validation=true`、`LightningPresentationBaseline=false`，执行 `HomeUIEditorPreview.Begin("lightning")`，完成后执行 `Finish()` 并关闭验证标志。编辑器已退出验证，恢复 Boot 场景且无场景未保存修改。

预览导出：`Tools/VFX/render-lightning-breakthrough-proof.cjs V2`。资源审计：`Tools/VFX/audit-lightning-breakthrough-v2.cjs`。

美术使用内置 image_gen 生成真正透明的原稿，主雷经一轮边距与轮廓修正后选用。Unity 仅分格取样、缩小与组合。完整初稿、最终编辑和落点提示词见 [prompts.md](../../ArtOutput/VFX/LightningBreakthroughV2/prompts.md)。
