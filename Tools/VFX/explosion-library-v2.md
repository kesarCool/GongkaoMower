# 共享爆炸库 V2：四款风格、单图集与旧资源退役

当前播放与回收时间已统一延长为原来的 1.5 倍，最新参数与验证见 [时长调整记录](vfx-duration-1.5x.md)。下文保留原版本记录。

日期：2026-10-25。

## 最终接入

| 用途 | 轮廓与配色 | 主体时长 | Prefab |
| --- | --- | --- | --- |
| 石破天惊 | 普通圆润爆炸，深橙红外缘与棕灰烟团 | 0.40 秒 | `fx_explosion_compact_v2.prefab` |
| 连环爆破 | 红橙尖锐星芒，暗红碎石与烟卷 | 0.30 秒 | `fx_explosion_chain_v2.prefab` |
| 穿云破雾 | 橙色穿刺亮芯、钢蓝灰烟团 | 0.32 秒 | `fx_explosion_missile_v2.prefab` |
| 追风逐日 | 宽厚橙红爆炸、深棕烟尘与岩块 | 0.48 秒 | `fx_explosion_heavy_v2.prefab` |
| 熊猫手雷的延迟余爆 | 复用连环爆破帧段，更短、更小 | 0.22 秒 | `fx_explosion_echo_v2.prefab` |

Prefab 均位于 `Assets/Res/Prefabs/Effects/Impact/`。四种独立美术主体加一个余爆配置，共享同一张贴图和同一个材质；余爆没有新增图集。

颜色使用饱和橙红和较深的轮廓，峰值阶段保留较高不透明度，减少浅色背景上泛白、变淡的问题。延续排序 -5，位于当前地面和装饰之上、文字和角色之下。每次播放只发射一颗图集粒子，主体结束 0.04 秒后回池；大小按已有实际 AOE 半径缩放。

已替换四份技能定义、普通/熊猫手雷的默认爆炸与余爆引用、四个导弹弹体的默认爆炸引用。导弹播放入口新增传入已有 `explosionRadius`，使表现跟随实际范围。四份技能定义经逐字段文件比较，除 `explosionFxPrefab` 外完全相同，记录见 [definition-checks.json](../../ArtOutput/VFX/ExplosionLibraryV2/Audit/definition-checks.json)。本轮未调整战斗伤害、索敌、弹道、冷却、连发、暴击或突破数值。

## 资源规格与预算

- 正式图集：`Assets/Res/Textures/VFX/sheet_explosion_library_v2.png`。
- 正式材质：`Assets/Res/Materials/VFX/mat_explosion_library_v2.mat`，沿用已确认存在的 `Sprites/Default`。
- 1024×512，8 列×4 行；每款占一行，8 个关键帧，每格 128×128。
- 从各自 16 帧原图选取第 0、2、4、6、8、10、12、15 帧，缩小并打包；每格额外保留 12 像素透明边界，主体只使用中间 104×104 区域。
- 主体帧段：普通 0–7、连环 8–15、导弹 16–23、重型 24–31。结束采样值内缩，避免跳到下一款。
- WebGL 使用 ETC2_RGBA8Crunched，关闭 mipmap 和 Read/Write，Clamp / Bilinear；无新字体、渲染纹理或自定义 shader。
- 构建工具对 PNG 设置 512 KiB 的源文件预算上限，超出时停止。

| 统计口径 | 替换前 | V2 | 减少 |
| --- | ---: | ---: | ---: |
| 三张被替换 PNG 源文件合计 | 1,501,980 字节 | 359,511 字节，351.08 KiB | 76.06% |
| 本轮相关运行源资产，含 Prefab/材质，不含 meta 与工具源码 | 1,864,053 字节，9 个文件 | 955,015 字节，7 个文件 | 48.77% |
| ETC2 RGBA8 纹理块数据，无 mipmap，按尺寸计算 | 1,933,200 字节 | 524,288 字节，0.5 MiB | 72.88% |
| Unity Editor 纹理对象内存统计 | 2,247,649 字节 | 625,911 字节 | 72.15% |

替换前的三张 PNG 是旧火焰图集、旧 Missilefx 单帧贴图、上轮 V1 图集，未重复计算两个旧火焰 Prefab 共用的贴图。源资产总量比较只覆盖本轮退役与替换集合，不代表整个 Assets 目录。

**以上不同口径不能混用，也不等于最终微信上传包体或真机显存。** 最终发布包增量仍需相同设置下重新构建；本轮没有执行微信发布构建或上传。原始测量见 [before-textures.json](../../ArtOutput/VFX/ExplosionLibraryV2/Audit/before-textures.json)、[after-textures.json](../../ArtOutput/VFX/ExplosionLibraryV2/Audit/after-textures.json) 和 [runtime-source-budget.json](../../ArtOutput/VFX/ExplosionLibraryV2/Audit/runtime-source-budget.json)。

## 图像生成与打包

使用内置 `image_gen`：改造普通爆炸的对比度，另生成连环、导弹与重型三款，均请求真实透明背景。没有调用外部 API/CLI，没有复用网友图集作为正式资产。

高分辨率原图保存在 `ArtOutput/VFX/ExplosionLibraryV2/Sources/`；完整提示词见 [prompts.md](../../ArtOutput/VFX/ExplosionLibraryV2/prompts.md)。这些原图在 Assets 之外，供后续修正和重打包使用。运行时只导入精选关键帧合成的共享图集。

`ExplosionLibraryV2Builder` 仅进行选帧、缩小、留边、图集打包和 Unity 资产配置，不重新绘制美术。细节与动作连续性仍需结合实际播放检查，8 帧是当前体积与小尺寸表现之间的取舍。

## 旧资源退役

已将以下资源及其 `.meta` 移出 Assets，保存到 `ArtOutput/VFX/ExplosionLibraryV2/Retired/Assets/` 的原相对路径：

- `fx_impact_fire.prefab`、`fx_impact_fire_plus.prefab` 及专用 `mat_impact_fire.mat`、`sheet_impact_fire.png`。
- `Missilefx.prefab` 及其唯一使用的 `boom1.png`。
- 上轮 `fx_grenade_explosion_v1.prefab`、专用材质和大图集。
- V1 重建工具与旧验证源码，避免旧入口重新引入大图集；`grenade-explosion` 预览入口已兼容转到 V2。

退役前扫描了其他 276 个序列化资产的直接依赖，未发现候选集合之外的引用；同时检查 Assets 内的旧 GUID 和资源路径字符串。候选集合内部的互相引用随整组归档。记录见 [retirement-check.json](../../ArtOutput/VFX/ExplosionLibraryV2/Audit/retirement-check.json)；22 个原文件/元数据文件的路径、字节数和 SHA-256 见 [retired-files.json](../../ArtOutput/VFX/ExplosionLibraryV2/Audit/retired-files.json)。当前 Assets 内已不再保留这些旧 GUID 引用。

这是可恢复归档，不是把旧目录留在运行资源中。V1 原始生成图和历史预览仍保留。

## 验证与证据

最终 [validation-report.json](../../ArtOutput/UI/ExplosionLibraryV2/validation-report.json) 的 56 项检查通过，使用真实 Game 场景、技能配置、投射物、怪物碰撞体和实际伤害入口，存档为内存夹具。

覆盖四种技能的原冷却、先发射后命中、实际命中生成对应效果、远处目标不受伤、一次性回收；覆盖实际延迟余爆与较小半径、共享纹理/材质、片段边界、缩放重置、随机状态、暂停冻结、混合爆炸的现有限流及退出战斗清理。每帧 40 次混合请求仍由已有配置接受 8 次，不增大现有上限。五个新 Prefab 均无缺失脚本。

导弹额外使用关闭特效的同配置施放作基线。现有 `PlayerBullet` 在接触时先结算直接伤害，再由 `HomingMissileBullet` 结算 AOE；夹具内穿云破雾主目标为 1、追风逐日为 2，新特效结果与关闭特效时一致。初轮错误地按单个配置伤害断言追风逐日为 1 的报告保留在 `Audit/initial-validation.json`；修正比较基线后复跑通过，未更改原伤害逻辑。

测试期间未捕获运行错误，玩家持久存档保持。控制台既有 AudioService / Pool GlobalRoot 场景关闭清理记录在本轮验证前已存在；本轮没有清空控制台，也不据此宣称整个工程不存在历史错误。

- [四款效果图鉴](../../ArtOutput/UI/ExplosionLibraryV2/gallery.png)
- [四种真实施放动图](../../ArtOutput/UI/ExplosionLibraryV2/runtime-library.gif)
- [四种真实施放视频](../../ArtOutput/UI/ExplosionLibraryV2/runtime-library.mp4)
- [各段截图时间与命中记录](../../ArtOutput/UI/ExplosionLibraryV2/clips.json)

视频按实际截图时间合成，每段均验证存在真实伤害与可见对应特效；没有补绘游戏帧。静态图采样时间分别为普通 0.105 秒、连环 0.075 秒、导弹 0.07 秒、重型 0.08 秒；草地样本为同一场景夹具整体平移后采样，不作为第二次真实命中证据。文字保持前景排序。

尚未验证微信真机帧时/GPU 过绘、所有地图、满级完整连发叠加战斗或最终上传包体。

## 复现

退出 Play Mode 后运行 `ExplosionLibraryV2Builder.Build()`，或菜单“工具/VFX/Build Shared Explosion Library V2”。

预览：`HomeUIEditorPreview.Begin("explosion-library")`；结束：`HomeUIEditorPreview.Finish()`。原场景及 Game View 尺寸会恢复。

合成预览媒体：

```powershell
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' 'Tools\VFX\render-explosion-library-proof.cjs'
```

此脚本仅在运行报告通过后处理真实截图，依赖当前机器的 sharp 和 ffmpeg；移植机器时调整其路径。本轮源文件修改前备份位于 `Library/VFXBackups/ExplosionLibraryV2-20261025/`。只回退本轮文件与引用，不覆盖工程中其他已有工作。
