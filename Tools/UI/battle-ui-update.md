# Battle UI V2 integration

基准稿：`ArtOutput/UI/BattleDesignV2`，1080 × 1920。此次接入战斗 HUD、暂停、胜利/失败结算、复活，不替换关卡 Tilemap。

## 资源与预制体

- 在原路径更新 `Assets/Prefab/Battle/GameLayer.prefab`、`PauseButton.prefab`、`Assets/Prefab/Result/GamePausePanel.prefab`、`GameResultPanel.prefab`、`GameRevivePanel.prefab`、`Assets/Prefab/Energy.prefab`，保留 GUID。
- 添加结算/暂停专用 `BattleDamageRow`、`BattlePassiveCard`、`BattleRewardCell`。原 `GameResultSkillDamageCell` / `GamePassiveSkillCell` 预制体和共享 `ItemCell` 预制体保留。
- 小资源放在 `Assets/Res/image/Game/battle/`，全部英文命名；共 7 个 PNG，磁盘约 13.6 KiB。复用已有公共纸面、按钮、技能图标、角色头像。此数字不代表最终微信包体增量；没有新建 Atlas。
- 原预制体快照在 `Library/UIBackups/Battle-*`，本轮具体目录记录于 `Library/UnityMCPTools/battle-backup-path.txt`。没有删除旧美术资源。

## 行为与布局

- HUD：能量与 Boss HP 保持互斥；能量掉落只更换圆珠 Sprite，不改变能量数量、吸附、对象池或碰撞参数。圆珠透明光晕包含在 64 px 纹理内。
- 底部不添加常驻技能栏和整条遮罩，小地图保持左下角，所有 HUD 装饰图形不拦截输入。暂停按钮仍位于原 TouchLayer 之上。
- 暂停：保留音量开关、主动技能伤害统计、五个被动槽、退出确认和返回战斗。
- 结算：主动技能列表在固定视口内滚动，第五行不会挤掉被动/奖励/底部按钮。胜利展示真实星级与解锁提示，下一关是否可用仍由原关卡进度判断；失败仅显示返回与重试。奖励只展示传入的 `rewardItems`，不新增奖励发放。
- 复活：不展示立绘，圆形倒计时居中，使用原 unscaled 时间与复活/放弃回调。广告图标表示现有入口；广告 Provider 仍为项目原来的模拟实现，本轮没有接入广告 SDK。
- 适配：HUD 顶部及小地图以安全区为边界定位；弹窗保持固定排版、等比缩放到可用区域。主动列表和奖励列表各自滚动；1080×1440、1080×1920、1080×2400 经 Editor 检查。微信原生胶囊不在 Editor 中伪造，预留顶部右侧区域，仍需设备确认。

## 生成、检查

1. `Tools/UI/generate-battle-assets.cjs` 生成小型程序化 UI 素材。
2. Unity 菜单 `工具/Battle UI/Apply Approved V2`（`BattleUIBuilder.Apply()`）可重复构建。
3. `HomeUIEditorPreview.Begin("battle")` 进入隔离验证；使用实际 Game 场景、内存存档和注入的测试广告回调。`HomeUIEditorPreview.Finish()` 恢复原场景与 Game View 尺寸。
4. 结果在 `ArtOutput/UI/Battle/validation-report.json`；截图汇总由 `Tools/UI/render-battle-validation.cjs` 生成。

验证范围包括 HUD 数据刷新、能量条比例与 Boss 切换、操作区域射线、暂停与音效/退出确认、五技能滚动、奖励状态复用、下一关门槛、复活成功/失败/超时、防连点及长短屏显示。Editor 验证不等同于微信真机广告或最终包体验证。
