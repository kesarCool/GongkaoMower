# 本地编辑器战斗间歇顿挫调查（2026-10-31）

用户截图：Unity Game View Stats，32.9 FPS、CPU main 5.8 ms、render thread 3.9 ms、47 batches、约 2.7k triangles。
用户进一步说明：每隔几秒停一下，发生于战斗移动或攻击。调查使用正在运行的工程，没有重启场景或改玩家数据。

## 已确认的情况

调查时 AppBootstrap.ApplyFrameRateSettings 无平台分支，Editor 也会 targetFrameRate=30、vSync=0。
启动宿主及恢复回调均应用同一策略。实际连接到的 Home 和 Game 运行实例均为该设置，
fixedDeltaTime=1/30、maximumDeltaTime=1/15（本次从 Boot 启动）。
这解释基础流畅度停留于约 30 帧，但不能单独解释周期性长停顿。

同一 Home 场景短对照，仅临时改目标帧率并恢复：

| 目标 | 帧数 | 实际 FPS | P95 ms | P99 ms | 最大 ms | 超过50 ms |
|---|---:|---:|---:|---:|---:|---:|
| 30 | 180 | 30.00 | 34.67 | 35.78 | 43.87 | 0 |
| 60 | 360 | 60.00 | 17.87 | 18.99 | 23.12 | 0 |

这是未聚焦的大厅窗口短样本，不能替代战斗负载或真机测量。
另读取到正常 Home 的 PlayerLoop 约 0.8–1.0 ms，未见当前大厅 CPU 算力不足。

## 捕获到的战斗间隔

轻量捕获按 Time.frameCount 去重、使用 Time.unscaledDeltaTime，在用户进入 Game 后等待 3 秒再开始。
首次超过 50 ms 即停止并保存 Profiler，避免逐帧枚举样本造成大量观测开销。
捕获到 game frame 33763 的间隔 65.0962 ms，GC.CollectionCount(0) 与前帧相同，均为 17949。

附近可用 CPU 样本中，游戏 PlayerLoop 最大约 5.63 ms；
较长的编辑器 GameView 重绘样本为 16.63 ms（其 GameView.Repaint 16.60 ms）。
该记录没有证明 GC 或重度战斗脚本造成这次停顿。
这些是不同层次的时间，不能将 CPU 样本简单相加来精确解释 65 ms；
缺少呈现/GPU/OS 等待数据，编辑器重绘与软件帧调度属于目前需继续验证的方向。

Profiler 记录：Library/LocalPerformance20261031/game-frame-spike.raw。
可读摘要及游戏帧时记录见 game-frame-spike.txt、game-frame-capture.csv。

## 日志开关对照

GameLog.Info 在 Editor 默认启用，Write 会 Debug.Log 并逐条 File.AppendAllText，均在调用线程完成。
EnemyWordLabel.LogVerbose 默认 true；普通怪死亡也输出 CardTrace 的 Boss 跳过日志。
实际 game_debug.log 尾部有持续的出怪、击杀、能量收集、碎片汇总信息，文件当时约 13 MB。
问题在同步输出及频率，不能将文件大小本身当作停顿原因。

保持战斗与 30 FPS 策略，仅临时切换普通 Info，警告和错误仍保留；每段去掉首秒：

| Info | 有效帧数 | 实际 FPS | P95 ms | P99 ms | 最大 ms | 超过50 ms | GC0增量 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 开 | 510 | 29.96 | 35.45 | 39.42 | 57.03 | 1 | 15 |
| 关 | 510 | 30.00 | 35.38 | 37.87 | 42.80 | 0 | 16 |

关闭 Info 后本次样本尖峰减少，值得优先减少逐怪追踪日志和同步文件输出。
但单段约 17 秒、战斗负载未严格复刻，这个结果不能证明日志是唯一根因，也不能证明 GC 本身已优化。

## 观测限制与后续

- periodic-* 是逐帧枚举 Profiler 产生明显自身开销的无效实验，不能用于游戏根因归因。
- combat-light-* 统计了编辑器 Profiler 循环；其 3.2 ms 平均值不是游戏帧间隔，不能换算游戏 FPS。
- 历史 BattlePerf 日志含编辑器暂停、加载或工具调用窗口，极端数值不能当作当前战斗卡顿证据。
- 调查阶段未修改游戏源码/预制体/打包配置，也未把上述假设作为已解决结论；后续实施记录见下文。
- 所有临时目标帧率、普通日志开关及 Editor profiling 均恢复到检测前设置；Play Mode 保持运行。

后续可先对逐怪日志做默认降噪，并将 Editor 预览帧率与微信 30 FPS 策略分开。
若周期性停顿仍在，应以用户确认的停顿时点继续捕获呈现/编辑器等待及战斗调用，
不应直接凭平均 FPS 归因于怪物数量、GC 或资源超限。

API 参考：Unity 2021.3 Application.targetFrameRate 文档说明该值会影响 Editor Game View，
并提示桌面软件限帧可能产生微顿：
https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Application-targetFrameRate.html

## 按建议实施（2026-10-31）

用户确认后完成以下修改：

- AppBootstrap.ApplyFrameRateSettings 在 UNITY_EDITOR 分支使用 60 FPS，Player 分支继续使用原来的 30 FPS；vSync 仍为 0。启动宿主、Boot、焦点返回及暂停恢复统一经过该方法。物理步长未修改。
- EnemyWordLabel.LogVerbose 默认改为 false，跳过逐怪 SetWord 调试日志及其字符串构造；需要排查单个出怪时仍可主动启用。
- BattleOutcomeCoordinator 不再为普通怪或克隆体死亡输出 Boss 跳过日志；原有死亡过滤、Boss 判定与结算流程不变。
- GameLog.EnableInfo 保持原策略，普通功能日志、警告与错误保留；此次没有重写文件日志系统。
- IosFrameRateValidation 使用独立的 Editor 预期值 60，并以 120 作为故意偏离的测试值，防止把未恢复的 60 FPS 误判为通过。

验证结果：

1. Unity 脚本编译完成，EditorUtility.scriptCompilationFailed=false；新程序集与运行时均确认逐怪日志默认关闭。
2. 从实际构建首场景 Login 进入 Play（无 AppBootstrap），12 项启动宿主、焦点/暂停恢复、重复安装与静态重置检查通过；实际 targetFrameRate=60、vSync=0、控制器数量=1。
3. 不激活 AppBootstrap 的隔离检查通过 9 项，覆盖 Editor/Player 预算常量和 Boot 的焦点/暂停恢复逻辑。
4. 使用 Unity 自带 DotNetSdkRoslyn 编译器，以 WebGL Player 定义及引用、当前 Editor 程序集的完整源码清单单独编译 C#，0 错误、4 个已有警告。输出写入 Library，未覆盖运行中程序集或 Build 导出包。
5. 用 Mono.Cecil 检查实际程序集 IL：Editor 的 ApplyFrameRateSettings 写入 60，WebGL Player 写入 30；两份程序集均默认关闭逐怪日志，并移除普通怪/克隆体死亡的两条追踪消息。
6. 验证后退出测试 Play，回到原 Boot 场景，场景未脏，临时 playModeStartScene 已恢复为 null。

证据：Library/LocalPerformance20261031/ImplementationValidation/compiled-policy.json、PlayerCompile/compile-current-roslyn.log。
旧 Player 响应文件缺少近期新增源码，直接使用旧清单的尝试不作为有效编译结果；Mono 目录下的旧 Roslyn 对当前源码的 C# 9 条件表达式支持不同，也不作为当前 Unity 编译结论。最终结果采用当前源码清单和 Unity 实际使用的 DotNetSdkRoslyn。

此次确认修改生效及帧率恢复逻辑通过，尚未进行修改后的战斗移动/攻击复测，不能据此认定周期性停顿已消失。已有 Build 目录是先前导出快照，此次没有重新导出或上传微信包。
