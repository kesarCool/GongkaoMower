# Boss 入场居中与围场

更新：2026-10-29。

- 进入表驱动的 Boss 波次（中途或最终波）时，先进行一次约 0.96 秒的传送：原地淡出 0.20 秒、完全隐藏时归位并移动镜头 0.52 秒、落点淡入 0.24 秒。中心定位仍复用普通复活的方法。青色椭圆光环、浅色副环和光柱使用线段与角色已有材质，不新增图片、材质资产或字体。
- 镜头在淡出时保持，在隐藏阶段将位置与 Boss 拉远倍率按同一条五次缓动曲线过渡；计算最终缩放对应的固定边界终点，消除一边变焦一边夹紧位置造成的突变。普通变焦改为帧率无关的指数平滑，普通跟随先限制目标再平滑。暂停同时冻结镜头和传送。
- 刷怪器等待传送完成后再发出波次提示及 Boss 出场预警。Boss 生成并挂好 `LastWaveBossMarker` 后立即围场，不再等待第一次受伤；人物仍在场内时不再重复传送。若玩家在长预警期间跑出场地，使用相同传送过渡安全返回，不可见地硬拉回的逻辑已移除。
- Boss 在围场外或过于贴近墙时拉到场内安全边距；同步 Rigidbody2D 坐标并清零残留速度。围墙保持现有16×20配置和石墩美术，无新增图片。
- 传送期间同时阻断点击移动和摇杆速度，暂停技能 Tick，并使用独立的受伤保护，结束后恢复；不改变血量或覆盖原来的复活/受击无敌计时。仍按住的摇杆在结束后继续响应。退出/禁用时恢复被隐藏的渲染器及原色，解除控制保护、销毁光环，防止人物永久隐藏。
- 同场多个已注册 Boss 共用四面墙，不重复传送玩家；最后一个 Boss 死亡后拆墙，禁用/退出场景也清理围墙。
- 受伤事件保留为非刷怪器创建 Boss 的兼容入口；竞技场已锁定时直接跳过，避免每次普通怪受伤都额外检查组件。

主要代码：`BossArenaEntryTransition.cs`、`BossArenaLock.cs`、`SpawnerWaves.cs`、`CameraFollow2D.cs`、`PlayerController.cs`、`DynamicJoystick.cs`、`PlayerSkills.cs`、`PlayerHealth.cs`。当前仍保留“Boss 场内复活不另行换位”的原有规则。

验证：`HomeUIEditorPreview.Begin("boss-entry")` 使用内存存档进入真实 Game 场景，执行实际 `WaveRoutine` 的 Boss 分支及 `TableWaveGroupRoutine` 的配表刷怪路径。覆盖预警前居中、预警期间走开、Boss 满血无伤时立即围场、角色/Boss 均在墙内、四面物理墙、重复受伤不重复传送、多 Boss 生命周期、复活定位和存档保持。

当前验证输出：`ArtOutput/UI/BossArenaEntry/Transition/validation-report.json`；`portal-motion/` 记录真实截图和逐帧时间，预览视频按实际时间戳编码。原立即定位版本的证据仍保留于 `After/`。验证后调用 `HomeUIEditorPreview.Finish()` 恢复原场景。未执行微信真机帧耗时或完整关卡通关验证。
