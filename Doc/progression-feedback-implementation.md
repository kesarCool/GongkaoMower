# 获得物品与角色养成反馈

完成日期：2026-10-24。沿用 `feat/WeChatminigame` 分支与现有奶油卡通 UI。角色战斗动作留待单独制作。

## 已接入

### 获得物品

新增 `Assets/Prefab/RewardReveal/RewardRevealPanel.prefab`、`RewardRevealCard.prefab`，注册在 Home 场景 UIManager。复用现有面板、金币/钻石、碎片图标、光晕和字体，没有新增字体库或位图资源。

- 标题“恭喜获得”，物品按顺序淡入和轻弹，显示名称、实际数量、品级及碎片标记。
- 单件居中，多件网格排列；超出一排可滚动，支持长屏和安全区缩放。
- 点击可加速揭晓，再点击“收下”关闭。
- 已接免费领取、广告商品、付费兑换、成就、兑换码与碎片解锁角色；战斗结算保持已有奖励展示，背包浏览保持详情提示。

`RewardRevealQueue` 从明确成功的业务入口接收回执，等确认框与过渡结束后依次展示。`ShowItem` / `ShowItems` / `ShowCharacter` 只做展示，不发物品；同一回执去重并复制调用方数据。广告请求有进行中锁、商品快照、30 秒截止及单次回调结算，避免旧 Cell 被刷新后串商品。缺失注册或配置时保留已到账奖励并警告，不创建空奖励弹窗。

### 升级

角色卡与属性行复用；一次升级捕获真实 before/after 数值，约 0.5 秒插值并显示短暂绿色增量、等级轻弹和圆台短光。整数、倍率、百分比沿用原面板精度。增长提示在行内轻微漂移，避免第一行被滚动视口裁切。

处理了 SpendGold / PlayerDataChanged 同步重入刷新；动画期间合并刷新，切换角色、页签、关闭或禁用时立即显示最终结果，不串角色、不重播、不重复扣费。

### 升阶与解锁

约 1.05 秒：蓄光 → 阶级提升 → 真实能力描述揭晓。阶级文字变化、锁标点亮、专属技能图标轻弹，复用 UI 音效并遵守静音设置；点击可跳过。业务先扣碎片并保存，动画只展示结果。长能力描述自动缩小或省略，技能页保留完整说明。

## 维护入口

- 编辑器菜单：`Tools/Home UI/Apply Progression Feedback`。
- 生成器：`Assets/Editor/ProgressionFeedbackUIBuilder.cs`。重复运行会更新两个新预制体，Home 注册去重；保存前会检查 Home 是否存在未保存修改。
- 奖励组件：`Assets/Script/Game/UI/RewardReveal*.cs`。
- 业务接入：`ShopView`、`AchievementPanel`、`SettingsPanel`、`GiftCodeService`、`CharacterSelectionPanel`。
- 养成表现：`CharacterGrowthFeedback`、`UpgradeAttrRow`、`CharacterPanelPresentation`。
- 业务测试替代广告/礼包存储的入口仅在 `UNITY_EDITOR` 下编译；正式路径继续使用现有广告服务和 PlayerPrefs。

原文件备份在 `Library/UIBackups/ProgressionFeedback-20261023/`，包含本轮开始时已存在的未提交修改。不要使用整分支重置来回退本功能。

## 验证与边界

运行 `HomeUIEditorPreview.Begin("feedback")`，通过 `ProgressionFeedbackValidationRunner` 在真实 Home 场景驱动业务与 UI。验证结束用 `HomeUIEditorPreview.Finish()` 恢复原场景。验证会临时启用后台运行，销毁时恢复原值；使用隔离内存存档与礼包领取记录，不触发真实广告。

39 项检查通过，覆盖队列顺序/去重/数据副本、普通及广告商品、取消和确认兑换、成就真实奖励、礼包有效/无效/重复、升级精度/连点/中间帧/切页、Rare/Legend 升阶/锁标/中途切角色、满级/资源不足及存档保持。

截图与报告：`ArtOutput/UI/ProgressionFeedback/`，包含 1080×1920 与 1080×2400、实际约 0.5 秒升级中间帧、升阶揭晓、单件/多件奖励。

尚未执行微信真机广告、最终包体和设备性能测量。普通奖励来源按当前配置验证；当前成就表实际测试的奖励 ID 为 2，不将它声称为已验证的非钻石成就配置。
