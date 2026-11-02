# 微信 SDK 桥接与主动分享修整

日期：2026-10-27；Unity 2021.3.22f1c1，现有 WX SDK 和 UnityPlugin 版本保持不变。

## 问题与修复范围

旧导出框架的 `WX_SyncFunction_t`、`WX_OneWayNoFunction_vt`、`WX_OnEventRegister`
等符号只是 `missing function → abort(-1)` 占位。分别导致胶囊查询启动崩溃、主动分享失败、
键盘事件注册失败。构建参数允许未定义符号，所以打包成功不等于接口可用。

新增 `Runtime/Plugins/SDK-WX-Generic.jslib`，集中实现 48 个通用 SDK 桥接，
包括同步查询、无回调调用、异步回调、事件订阅与取消、类方法、初始化和帧率设置。
字符串指针、数值、布尔值及返回字符串按 `wx-runtime.dll` 的真实 P/Invoke ABI 转换，
清单见 `Tools/WeChatSdk/bridge-signatures.json`。生成器会核对 DLL SHA256；SDK 升级后
应重新提取 ABI，不能直接沿用此清单。

启动闭包也按实际 DLL 复核：补上 `WXIsCloudTest`、蓝牙回调指针注册，
把触摸、陀螺仪、Socket 的回调注册列入导出必检。SDK 的许多 API 在初始化前会直接拒绝调用，
因此仅补单个分享函数不足以完成接入。

当前 SDK 的可选性能监控还依赖 5 个未实现的原生指标，不能用占位值伪造指标。
监控启动和报告两处均检查依赖，缺失时只警告一次并跳过监控，防止初始化后再次触发 abort。

启动入口先初始化 SDK，再设置微信帧率；胶囊避让继续使用真实窗口和胶囊数据。
桥内捕获 JavaScript 异常并记录具体符号；同步结构查询失败返回空 JSON，
原有 `PlatformMenuExclusion` 会回退到估算位置。异步通用调用失败会通知 C# 的失败与完成回调。

存储、音频仍使用现有导出修复，广告仍使用已有直接桥。本次没有为 SDK 全部 API
提供实现。未使用、未审计接口仍可存在于导出框架中，导出警告会列出它们。
新增业务调用前须核对 P/Invoke、JS 实现及参数 ABI，补桥并加入验证清单。

## 游戏内入口与奖励语义

复活、商城奖励、卡牌刷新均通过统一奖励 Provider 发起主动分享；正式 WebGL
不再点击后直接模拟成功。主动分享和右上角菜单转发复用分享图片池。

每次主动请求带唯一 ID，JS 确认已调用原生分享，并回报小游戏隐藏和返回事件。
没有收到 JS 确认或没有观察到隐藏时短时超时失败；隐藏后等待返回最长 120 秒。
相同请求仅完成一次，失败可重试；复活关闭及选卡会话改变后不接受旧奖励回调。
商城请求绑定商品与单次收据，切换页签仍可完成原请求，不依赖被重建的商品 Cell。
复活等待期间暂停倒计时，失败后恢复剩余时间。Editor/Standalone 保留明确记录日志的
开发模拟，便于现有 UI 联调。

**这里的奖励是“发起分享后返回游戏”的奖励。** `wx.shareAppMessage` 没有可靠的
好友送达确认；原生分享面板取消可能也产生相同的隐藏/返回事件，因此不能保证
“只有成功发给好友才发奖”，也不能把返回日志描述为“分享成功”。
未观察到隐藏/返回事件的设备会得到可重试的失败反馈，而非被当成分享成功。

## 导出保护

`WXBridgeValidator.ValidateFramework` 在存储/音频修复后、写入小游戏框架前检查：

- 已审计符号不能仍是 `missing function` 占位。
- 已审计函数不能被替换为空函数或单纯常量返回。
- 未使用函数被编译器裁剪允许缺席；其他未审计 SDK 占位汇总为警告。

失败时抛出构建错误，并提示重新构建 WebGL。校验通过前保留旧小游戏输出，
避免仅转换一个旧 WebGL 框架时删掉原有导出结果。

**首次必须重新“生成并转换”**，不能只点“WebGL 转小游戏”。后者复用旧的
WebGL 框架，不会重新编译新增 `.jslib`。

## 自动验证

项目根目录执行：

```powershell
node Tools/WeChatSdk/generate-generic-bridge.cjs --check
node Tools/WeChatSdk/test-generic-bridge.cjs
node Tools/WeChatSdk/validate-share-flow.cjs
& ./Tools/WeChatSdk/test-export-guard.ps1
```

桥测试使用真实安装的 SDK JS、模拟微信 API 和 UTF-8 堆，检查参数、返回值、事件、
主动分享路由及异常回调。导出检查测试使用生产 C# 校验器，包含旧真实导出包、
压缩格式、空实现、可选接口及新桥接实现。分享 JS 有 8 项原生边界测试。
Play Mode 可调用 `WeChatShareFlowValidation.Run()` 或菜单
`Tools/WeChat/Validate Share Reward Flow` 执行 15 项接收器、超时、重试和旧回调保护检查。

本次验证结果：48 项 DLL ABI / JS 桥测试、8 项分享边界测试、241 项导出断言通过。
另外使用 Unity 2021 自带 Emscripten 2.0.19，开启未定义符号报错，真实编译并执行了
全部 48 个桥接函数的 Wasm 测试，验证 helper 链接、UTF-8、double、bool、返回字符串、
分享路由及失败回调。结果在 `Library/WeChatSdkValidation/wasm-smoke-result.json`。
使用 Unity 生成的 WebGL Player 响应参数与当前全部运行时源码进行独立 C# 编译也已通过；
日志在 `Library/WeChatSdkValidation/PlayerCompile/compile.log`，仅有既有警告。

Unity 编辑器内 15 项 Play Mode 检查全部通过，并已恢复原 Boot 场景及 Play Mode 设置。
完整 WebGL 构建和小游戏导出已成功，最终产物通过生产导出校验器的全部检查，
IL2CPP 产物中保留了 `WXShareRequestRunner.OnShareBridgeState` 接收方法。
新包在 `Build/minigame/minigame`，代码包标识为 `8fff66e94664f197`，
数据包标识为 `0d7e2885c2897936`。构建状态记录在
`Library/WeChatSdkValidation/build-status.txt`。
原生分享面板、不同微信版本的隐藏/返回事件及实际设备兼容性尚需真机验收。

## 真机验收

1. 重新生成小游戏，用微信开发者工具打开 `Build/minigame/minigame`，真机预览。
2. 检查 Home 正常加载，胶囊避让布局正常，无 `missing function` / `WXUncaughtException`。
3. 分别测试复活、商城、卡牌刷新：应拉起分享面板，返回后最多结算一次奖励；
   等待期间重复点击不能发起重复请求。
4. 测试分享接口报错、没有弹出面板、长时间未返回、面板关闭后迟到回调：
   不提前发奖，失败能重试，旧回调不能给新的会话发奖。
5. 检查右上角菜单转发及分享图片、输入键盘、游戏声音与存档。

模拟测试和 Unity 构建无法替代真机对原生分享面板、微信版本差异及返回事件的验证。
