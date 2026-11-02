# 战斗页地图装饰开关

2026-10-24。入口位于暂停页，设置区已统一为三张等宽浅底卡片：背景音乐、游戏音效、地图装饰。标题居上、相同的滑动开关居下，不再混用复选框、小图标与音频开关。战斗 HUD 上不保留入口。地图装饰首次使用默认开启；之后恢复玩家上次保存的选择，包括重新打开暂停页、重载地图、下一局和重新进入游戏。

开关仅操作当前地图内名为 `Decoration` 的节点，保持地形、围墙和角色正常。关闭后，小地图中的对应装饰也一起隐藏。重新显示时不会重新打开原本单独关闭的子摆件。没有 Decoration 的地图不会报错。

偏好使用 PlayerPrefs 键 `GongkaoMower.Battle.ShowDecoration`，缺省值为 1；切换后立即 Save。暂停页复选框启用时使用 SetIsOnWithoutNotify，不会以 Prefab 的默认勾选覆盖保存值。显示设置独立于角色、货币等玩家进度。

## 接入文件

- `Assets/Script/Game/Battle/BattleMapPreferences.cs`：偏好读写与变化通知。
- `Assets/Script/Game/Battle/BattleMapLoader.cs`：加载地图时应用偏好，并响应即时切换。
- `Assets/Script/Game/UI/BattleTheme/MapDecorationToggle.cs`：开关同步及订阅管理。读取保存状态后，显式刷新滑块外观，避免无通知赋值导致外观与状态不一致。暂停页使用既有弹窗输入层。
- `Assets/Prefab/Battle/GameLayer.prefab`：已移除 `HudTop/DecorationToggle`。
- `Assets/Prefab/Result/GamePausePanel.prefab`：三张卡片位于 x=42、332、622，y=252，每张 `272×120`，间距 18。标题居中，开关为 `104×52`。继承暂停面板的安全区位置与缩放。
- 设置区加高后，技能区及底部按钮下移 40，面板高度由 1430 调为 1470；输出技能列表高度保持 465，保留原有五行容量。
- `Assets/Script/Game/UI/Framework/ThemeAudioToggle.cs`：共用的滑块外观，增加无通知赋值后的 Refresh 入口。`GamePausePanel` 中音频标签固定为“背景音乐、游戏音效”，状态由开关表达。
- `Assets/Editor/BattleDecorationToggleBuilder.cs`：菜单 `Tools > Battle UI > Add Decoration Checkbox`，移除 HUD 入口并更新暂停页。原 `BattleUIBuilder` 的重建流程也已同步。

复用现有圆角底图、滑块及微软雅黑 TMP 字体，没有新增图片或字体。整张设置卡可点击，标题和滑块图片不单独拦截射线。本次调整前的两个 Prefab 备份在 `Library/UIBackups/PauseMapDecoration-20261024-130952-069/`。

## 验证

`Assets/ArtPreview/UI/MapDecorationValidationRunner.cs` 在实际 Game 场景中运行，通过原有暂停按钮打开注册面板，入口 `HomeUIEditorPreview.Begin("map-decoration")`。24 项全部通过，包括 HUD 无入口、原触摸层顺序保持、默认值、标题区域射线命中、三张卡片对齐且不重叠、统一的固定标签与滑块外观、关闭状态及重开后的外观同步、音频绑定保留、即时隐藏/恢复、偏好写入、单个子摆件状态保留、地形与四面围墙保持开启、继续战斗、暂停页重开、重载地图、跨局恢复、原始偏好与玩家存档还原、无新增运行时错误。

记录与实际截图：

- `ArtOutput/UI/MapDecoration/validation-report.json`
- `ArtOutput/UI/MapDecoration/pause-decorations-visible-1080x1920.png`
- `ArtOutput/UI/MapDecoration/pause-decorations-hidden-1080x1920.png`
- `ArtOutput/UI/MapDecoration/battle-no-decoration-control-1080x1920.png`
- `ArtOutput/UI/MapDecoration/settings-preview.png`（实际截图局部）

验证使用内存玩家存档；测试结束恢复原始设置与场景。尚未执行微信真机验证。
