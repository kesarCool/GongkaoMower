# 兑换、词汇、成就 UI 接入

根据 `ArtOutput/UI/HomePagesDesignV1` 的确认效果图更新，继续使用当前开发分支。

## 已更新的入口

- 兑换：`Assets/Prefab/Shop/ShowView.prefab`
- 词汇：`Assets/Prefab/LexiconPreview/LexiconPreviewPanel.prefab`
- 成就：`Assets/Prefab/Achievement/AchievementPanel.prefab`

上述 Prefab 保持原 GUID，Home 场景和底部导航继续通过原引用打开。新增子 Prefab 为 ExchangeBonusCard、ExchangeFragmentCard、LexiconPairRow、AchievementThemeCell；原子 Prefab 和旧素材保留。

## 资源

- `Assets/Res/image/Home/exchange/`：补给卡底与金币、钻石组合图。
- `Assets/Res/image/Home/lexicon/`：地区筛选普通、选中底图。
- `Assets/Res/image/common/`：通用卡底、进度条、编号底、时钟、广告与碎片图标。

新增资源与文件使用英文命名。角色立绘、货币图标、按钮、篇章页签复用已有资产；未添加新字体或 SpriteAtlas。资源参数见 `Tools/UI/home-pages-assets.json`。

## 逻辑范围

ShopService、AchievementService、PlayerProfileService、广告 Provider、数据表和保存格式均未修改。

兑换仍由 ShopView → ShopService 处理免费领取、广告回调、确认/取消、扣费、发奖、限购、售罄和周期重置。展示层按价格类型区分补给和其他商品，数量变化时自动排列；卡面整体可点。余额、限购、倒计时均使用实时数据，效果图中的演示数字没有写入配置。

词汇保持原主题/分类单选和 LoopScrollRect 池化。一行绑定两个相邻词条，奇数最后一行隐藏右项，池化复用时恢复右项；长词自动换行并撑高行高。新样式使用首次条目定位；空分类显示正常空态文案。未增加搜索、收藏、题目或释义功能。

成就保持原“可领取 → 进行中 → 已领取”排序、阶段解锁和单项领取。新展示层在状态改变处显示分区标题，顶部待领取数量从实际数据计算；领取后沿用原逻辑重新生成列表。奖励图标与数值仍来自当前配置，较长描述撑高行卡。

现有广告 Provider 是工程原有的模拟成功回调，本轮保留此实现。编辑器验证此路径不代表微信真实激励视频已接通。

## 工具与验证

- 重建：Unity 菜单 `工具 / Home UI / Apply Exchange Lexicon Achievements`。
- 预览：`HomeUIEditorPreview.Begin("pages")`。
- 结束：`HomeUIEditorPreview.Finish()`。
- 报告与实际渲染：`ArtOutput/UI/HomePages/`。

34 项编辑器检查全部通过，运行期间未记录到错误；实机渲染已核对。未进行微信真机或最终构建体积验证。

验证使用内存保存服务，不购买真实商品，不修改玩家持久化存档。覆盖三页打开、免费领取、模拟广告、付费确认/取消、准确扣费与物品增加、余额不足、售罄、刷新恢复、两主题分类、双列尾项/复用/长词、成就领取/防重复/待领计数、长短屏和返回旧页面。

源工作文件备份：`Library/UIBackups/HomePages-20261018-192151/`。该备份位于 Library，不参与游戏构建。微信真机触控、系统胶囊、安全区与最终包体需构建后验证。
