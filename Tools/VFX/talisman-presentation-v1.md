# 天师符令表现 V1

当前命中效果的播放与回收时间已延长为原来的 1.5 倍，最新参数与验证见 [时长调整记录](vfx-duration-1.5x.md)。下文保留原版本记录。

2026-10-25。已接入工程，新增命中反馈、重绘 fulu 符纸，并替换满级与突破符令的旧拖尾。技能数值、发射规则和碰撞范围保持原值。

## 表现与拖尾改进方案

旧 fulu.jpg 只有 18×56，浅黄符面与沙地图接近。新版采用朱红边框、深色轮廓、金色符面与少量青色点缀，保持原有细长纸符身份。图片透明区计入 Sprite 尺寸，纸符可见轮廓略放大，碰撞器不扩大。

命中表现为金色亮点 → 朱红符印张开 → 少量青色与红色碎光消散，8 个关键帧、0.18 秒，0.22 秒归还对象池。普通、满级、突破符令以及角色特质的环绕符箓共用该效果；命中点取目标碰撞器的最近点，在伤害调用前记录，避免击杀回收移动目标后产生位置错误。符印置于怪物文字下层，保留文字可读性。

满级旧 TrailRenderer 的起点宽度约 0.175，宽度曲线在生命周期约前三分之一就降到零；透明度又从零开始，中间才最亮，加上 0.1 的顶点间距及零圆角，容易出现断促硬片，正常飞行时也不明显。

| 参数 | 新满级拖尾 | 新突破拖尾 |
| --- | --- | --- |
| 外层 | 青蓝柔边，宽 0.115，保留 0.12 秒 | 同色同宽，保留 0.10 秒 |
| 亮芯 | 金色向朱红过渡，宽 0.035，保留 0.10 秒 | 同色同宽，保留 0.085 秒 |
| 几何 | 顶点间距 0.035，转角 2，端帽 3，宽度持续收至零 | 相同 |
| 柔边 | 共用简单透明 Shader，横向透明度衰减 | 相同 |
| 复用 | 借出、回收、禁用均 Clear，避免跨位置拉线 | 相同 |

该方案已做成可直接评审的版本。普通符令和环绕符箓保留简洁符纸；满级、突破才使用双层拖尾。拖尾不采样贴图、不需要 Bloom；两层 TrailRenderer 会增加几何和绘制开销，并非零成本。如微信低端设备压力较大，优先去掉窄亮芯，保留柔边外层；这只改变表现，不需要改发射逻辑。尚未用真机数据判定是否需要降级。

## 资源预算

| 项目 | 实测 |
| --- | --- |
| 符纸与命中共用图集 | 512×256，PNG 43,988 字节，约 43 KiB |
| 命中动画 | 8 帧，每格 64×64，实际图案位于约 48×48 内 |
| WebGL 导入 | ETC2_RGBA8Crunched，质量 65，无 mipmap，无 CPU 可读副本 |
| ETC2 展开块数据 | 131,072 字节，128 KiB |
| Editor 纹理内存 API 读数 | 147,745 字节，包含 Unity 对象开销，不能视为纯 GPU 用量 |
| 共用材质 | 2 个：符纸/命中共用材质、拖尾共用材质 |
| 拖尾新增贴图 | 0 |
| 新增图集、两个材质、Shader、命中 Prefab 的源文件总量 | 165,218 字节，不含 meta、代码、已有 Prefab 差量 |

相比原来仅 946 字节的 fulu.jpg，这次是有控制的资源增加。图集阈值为 96 KiB PNG；使用现有 CombatVfx 限流（每帧最多 8、同时最多 32）。高清生成原稿保留在 Assets 外的 ArtOutput 中。旧 fulu.jpg 已无序列化 GUID 引用，暂留作回退；fulu2.png 未改动。源 PNG、YAML 源文件大小及 ETC2 纹理占用均不等于最终微信压缩包大小，本轮未构建微信包或进行真机验收。

## 接入范围

- `Assets/Prefab/Combat/playerBullet/BulletStraightTalisman.prefab`
- `Assets/Prefab/Combat/playerBullet/BulletStraightTalismanMax.prefab`
- `Assets/Prefab/Combat/playerBullet/BurstBulletTailman.prefab`（既有文件名）
- `Assets/Resources/Traits/TalismanOrbit.prefab`

`ProjectileHitFeedback` 为可选表现组件。PlayerBullet 与 TalismanOrbiter 在有效命中时调用；其他子弹未配置此组件，不播放符印。表现不消耗战斗全局随机状态，没有增加 Update、伤害、爆炸范围或停顿机制。

## 验证与预览

使用真实 Game 场景与技能配置、内存存档夹具。旧版 32 项、新版最终 50 项检查通过，运行期间无异常；编辑器已退出验证并恢复 Boot 场景。

- 普通 1 发总伤害 1，满级 3 发总伤害约 4.74，突破样本伤害 3；发射数量、冷却与命中前不提前扣血均符合原配置。
- 环绕符箓真实接触分支保持原伤害，新增命中符印并正常消失。
- 暂停冻结粒子及回收计时，恢复后回池；密集命中遵守已有上限；退场清理；复用跳转不连接旧位置。
- 4 个 Prefab 原有 Rigidbody2D、CircleCollider2D 与玩法组件的序列化内容逐块对比未变，技能定义文件与操作前备份逐字节一致。
- 浅沙与草地夹具均采样。静帧飞行来自真实发射后冻结；命中静帧将已触发粒子采样至 0.055 秒。动态回放保留实际采集帧间时长，未将动作慢放。

预览位于 `ArtOutput/UI/TalismanPresentationV1/comparison.png`、`after.gif`、`after.mp4`；原始 Before/After 截图及各自报告位于 `ArtOutput/UI/TalismanBefore` 和 `ArtOutput/UI/TalismanAfter`。资源审计位于 `ArtOutput/VFX/TalismanPresentationV1/Audit`。

复现：Unity 中执行 `TalismanPresentationV1Builder.Build()`；将 SessionState `TalismanPresentationBaseline` 设为 false，执行 `HomeUIEditorPreview.Begin("talisman")`，完成后 `HomeUIEditorPreview.Finish()`。旧版验证只能在恢复旧 Prefab 的隔离副本中运行，不能直接将 Baseline 标志当作资源回滚。`Tools/VFX/render-talisman-proof.cjs` 与 `audit-talisman-assets.cjs` 分别导出预览和审计。

## 美术来源

使用内置 image_gen，透明背景。原始符纸以既有 fulu.jpg 为编辑参考，命中图为新生成。最终运行图集由 Unity 工具选帧、缩小和打包。完整最终提示词保存在 [prompts.md](../../ArtOutput/VFX/TalismanPresentationV1/prompts.md)，原稿位于同目录的 `Sources/paper.png`、`Sources/hit.png`。
