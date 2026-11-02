# Boss wheel V2 — integrated

已将确认的奶油纸面转盘及「闭箱 → 蓄力 → 开箱 → 技能落位」动效接入现有工程。

## Changed behavior

- 原有 16 格环形技能位保持正向，主动 / 被动区分底色；移动光标与中奖勾选分开，中奖项保留金框。
- 宝箱先闭合。光标停止后轻晃、聚光，随后切换开启图并回弹；实际中奖技能逐项沿曲线飞入下方卡片，最后启用继续按钮。
- 跳过动画仍保留短开箱与奖励落位。所有等待、位移、缩放使用 unscaled time，在原生战斗暂停锁下继续播放。
- 结果支持 1 / 2 / 3 项，按数量居中；重复技能沿用原累加等级展示。新增开始 / 确认防重复保护，发奖回调仍仅执行一次且先于结果绑定。
- 关闭时清理光效和飞行卡，重开后恢复闭箱与初始选项。

`BossWheelController.cs` 未修改：技能池、权重、中奖数量、升技能、羁绊限制及战斗恢复事件仍由原控制器负责。现有音效继续使用 WheelTick / UiSwitch，没有新增音频。

## Assets and references

新资源分类在 `Assets/Res/image/Game/bosswheel/`，使用英文命名。共 13 张 PNG，源 PNG 合计 **563,154 bytes（约 550 KiB）**，其中宝箱闭合 / 开启图均为 512 × 512；其余为一张转盘底图、九宫格和小光效。该数字不是最终微信构建增量。

纸面、主按钮、字体材质继续复用已有资源。没有新增字体、FontAsset、字体图集、SpriteAtlas、Spine 或逐帧动画；现有 msyh SDF 文件时间与内容保持原样，仍为单图集。

以下原 Prefab 原位更新，GUID 保留，Game 场景的注册引用不需重接：

- `Assets/Prefab/CardSelect/BossWheelPanel.prefab`
- `Assets/Prefab/CardSelect/WheelSlotCell.prefab`
- `Assets/Prefab/CardSelect/WheelRewardSkillCell.prefab`

旧散图未删除。修改前文件备份在 `Library/UnityMCPTools/boss-wheel-backup-path.txt` 指向的目录中。

代码入口：`BossWheelPanel` 保留原回调与数据绑定，`BossWheelPresentation` 仅处理视图。飞行卡、星芒、聚光固定复用，不在每帧生成新对象。编辑器重建入口为「工具 / Battle UI / Apply Boss Wheel V2」。

## Validation

`ArtOutput/UI/BossWheel/validation-report.json` 的 **28 项实际 Editor 运行检查全部通过**，覆盖原生 Game 场景与控制器、真实技能升级、暂停播放、发奖与确认防重复、同技能连续升级、1 / 2 / 3 项展示、取消与复用、1080 × 1440 / 1920 / 2400、字体复用、存档隔离。

连续两次飞刀升级的测试补齐了原有突破所需被动，使最后一级按原规则可达；没有放宽游戏上限或改动羁绊逻辑。

三个 Prefab 审计结果均为 0 缺失脚本、0 缺失引用；编辑器已退出测试并恢复原场景，无未保存场景变化。测试使用内存存档，未改玩家进度。

实际截图、动图及录像保存在 `ArtOutput/UI/BossWheel/`，与旧设计稿目录分开。当前尚未验证微信真机及最终构建大小。
