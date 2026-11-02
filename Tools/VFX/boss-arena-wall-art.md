# Boss 竞技场围墙 V1

本轮将围墙从树木贴图铺成的窄条，更新为暖灰砂石矮墙、木质护柱和朱红布饰，与明快卡通地图统一。

## 项目资源

- 图集：`Assets/Res/image/Game/arena/boss_wall_atlas_v1.png`，512×256，三个 Sprite 切片，源 PNG 约 128 KiB。
- 横墙：`boss_wall_horizontal`，256×64；竖墙：`boss_wall_vertical`，64×256；护柱：`boss_wall_post`，96×144。
- Prefab：`Assets/Prefab/Battle/boss_arena_wall_v1.prefab`。
- `GameLayer.prefab` 的 `BossArenaLock.wallPrefab` 引用新 Prefab；相较本轮开始时，GameLayer 只有这一项引用变动。
- `BossArenaWallVisual` 在生成时按墙方向选择切片，并生成护柱。四个角由上下墙各负责两角，左右墙只生成中间护柱，避免角柱重复。
- 视觉组件没有 Update、Animator、额外碰撞体或每帧材质创建。所有围墙图像共用一张图集。

原 `wall.prefab` 和树木素材保留。四面墙的尺寸、实际碰撞厚度 0.4、生成位置、Boss 拉入边界、触发和清除规则沿用原实现。原序列化字段 `wallThickness: 1` 没有改动；原代码实际使用的是固定 0.4。

## 验证与预览

`BossArenaWallValidationRunner` 使用真实 Game 场景及内存存档，验证普通敌人不会触发围墙、真实 Boss 受伤会触发四面墙、碰撞尺寸保持、四向射线命中围墙、Boss 死亡通知清理、再次生成、组件关闭清理、共享图集及四角护柱唯一等行为。旧版 14 项、新版 19 项编辑器检查通过。

预览：

- `ArtOutput/World/BossArenaWall/comparison.png`：旧新版同地图总览对比。总览镜头临时缩远，仅用于检查四面墙。
- `ArtOutput/World/BossArenaWall/gameplay-1080x1920.png`：沿用实际游戏镜头缩放的近景。
- `ArtOutput/UI/BossWallBefore/validation-report.json`、`ArtOutput/UI/BossWallAfter/validation-report.json`：检查结果。

PNG 文件体积不是最终微信包体增量；未进行本轮微信构建和真机 GPU 性能测量。

## 生成和维护

使用内置 imagegen 生成三块素材的透明源图；精确完整提示词保存在 `ArtOutput/World/BossArenaWall/source/generation-prompt.txt`，原图也在该 source 目录。提示词主题为：与项目地图匹配的明快手绘卡通、暖灰砂石横墙／竖墙、木质金顶朱红布饰护柱、透明背景、低矮且适合重复铺设。

`Tools/VFX/prepare-boss-wall-assets.cjs` 仅做透明边缘裁切、重复轴端帽裁切、缩小和图集排列，不重绘素材；同时检查拼接轴中心像素不透明，避免横断细缝。图集切片坐标保存于 `ArtOutput/World/BossArenaWall/sprite-layout.json`。

Unity 菜单 `Tools/Battle Art/Apply Boss Arena Wall V1` 导入 FullRect 切片，生成新 Prefab 并更新 GameLayer 引用。现有图集名称保持稳定；改布局时需要同步 builder 的切片矩形。

本轮原文件备份位置记录在 `Library/UnityMCPTools/boss-wall-backup-path.txt`。若只需回看旧表现，将 `BossArenaLock.wallPrefab` 指回 `Assets/Prefab/Battle/wall.prefab` 即可。
