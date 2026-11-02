# iOS 持续卡顿复查（2026-10-28）

用户反馈：上一轮包在怪物或特效密集时仍持续变慢，安卓明显好于 iOS，没有体感改善。
上轮 Windows 编辑器中墙体检测的局部收益没有转化为已确认的 iOS 改善，不能继续把它作为主因结论。

## 本次云测确认了什么

来源：`Build/1.1.19测试报告/1793116974_wx27c7354ddadf45da_testcloud_result.xlsx`，只读，原文件未修改。
体验版本仍为 1.1.19，测试时间 2026-10-28 00:02:54–00:32:13，10 台设备，其中 3 台 iPhone。

| 机型 | 工作表行 | FPS | CPU 均值 | 内存均值 MB | 内存峰值 MB | Stutter |
|---|---:|---:|---:|---:|---:|---:|
| iPhone XS | 5 | 58 | 99.5% | 790.6 | 820 | 0.8 |
| iPhone 12 Pro | 6 | 57 | 81.6% | 813.8 | 836 | 1.1 |
| iPhone 15 Plus | 11 | 59 | 46.9% | 842.5 | 856 | 0.5 |

上述取自「启动运行性能详情」。三台 iPhone 的设备均值等权平均为 FPS 58、CPU 76.0%、
内存 815.63 MB；不是逐帧加权值。Stutter 单位未标明，保留原始数值。
三个 iPhone 型号均未出现在 10 月 24 日的 9 台 iPhone 样本中，不能做同机前后性能归因。
两轮仅有 OPPO A72 5G 的型号重合；即便型号相同，也缺少相同操作轨迹与时间线，不能当作受控实验。
不同平台 CPU 百分比的统计口径未说明，不能据此计算 iOS 比安卓慢多少倍。

Drawcall、三角面数、顶点数仍然缺失；工作簿没有逐帧耗时、GPU 耗时、游戏操作轨迹，
没有 `[BattlePerf]` 或 codeMd5/dataMd5，无法单凭表格确认上传二进制身份或判断 CPU/GPU 分项瓶颈。
版本号相同也不等于包相同。10 台设备没有失败或 JSError；XS 记录 1 次黑屏，缺少截图与发生时间。
所有设备资源下载失败为 0，仍各有一次未列 URL 的 request 失败。

## 本次发现的帧率控制缺陷

1. `AppBootstrap` 旧逻辑只给 Editor/Standalone 设置 `Application.targetFrameRate=30`，
   WebGL 仅在 SDK 初始化后调用 `WX.SetPreferredFramesPerSecond(30)`，且全局 vSync 为 1。
2. 当前 SDK 的 `wx-runtime.dll` 自身在这个 API 内提示：
   「不建议使用 WX.SetPreferredFramesPerSecond()，建议用 Application.targetFrameRate 代替」。
   typed bridge 的 double/JS ABI 正确，问题不在这条参数桥接。
3. 当前实际导出的 `_emscripten_set_main_loop_timing` 会根据 Unity 节拍调用
   `wx.setPreferredFramesPerSecond(60/value)`。单次微信 30 FPS 请求不能代表 Unity 调度状态也已变为 30。
   对真实导出函数的 VM 调用已复现 `(1,1) → 微信30 → (1,1)` 导致 `[60,30,60]`。
4. 转换器为避免双重跳帧，把 `Browser.mainLoop.timingValue` 强制写为 1，
   但标准 `resume()` 会读取这个值并重放，丢失原来目标 30 FPS 所需的逻辑 value=2。
   Unity 原生缓存仍可能是 `(1,2)`，只重新赋同一个 30 不能保证再次下发。

上述是源码、DLL 与真实产物可证明的调度缺陷。它们说明旧的「30 FPS 已生效」假设不成立，
但还不足以独自证明整个 iOS 密集战斗卡顿都由它们造成。

## 修改与对照设计

- Unity 与微信统一使用 Unity 的目标帧率入口：`vSyncCount=0`、`Application.targetFrameRate=30`，
  WebGL 不再排除；保留 SDK 初始化，移除已不推荐的单次 `WX.SetPreferredFramesPerSecond` 调用。
- 转换后的调度器保存独立的逻辑 timingValue，供恢复时重放；RAF 实际步进仍为 1，
  避免微信已降为 30 后再隔帧执行导致 15 FPS。遇到未知模板结构应中止导出，不能静默跳过。
- 开发包增加 `[FrameSchedule]` 记录；`[PerformanceBuild]` 记录包标记、MD5、DPR。
  启动记录还包括实际高性能模式/高性能模式+标志和渲染上下文类型；配置开关不等于设备实际进入了该模式。
  `[BattlePerf]` 增加 revision、Unity 目标帧率、vSync、采样时长及帧数，继续记录实际 FPS 与渲染尺寸。
- 默认 iOS DPR 改为 2，同时生成原生 DPR 对照包。两个包使用完全相同的 Wasm、数据、技能与资源；
  文件差异只允许 `check-version.js` 的 DPR 条件和 `game.js` 的诊断标签。

本次不同时改 GC、怪物碰撞规则、索敌频率、伤害计算或特效显示预算。
它们仍有可调查成本，但缺少分项真机耗时，不能继续混合修改后再推断收益。
工作区中另一个已完成的剑光美术更新会随本轮构建进入包；A/B 两包都包含相同资源，
但不能把本轮包与上轮包的总差异全部归因于帧率设置。

## 真机验收

版本标记：`ios-frame-control-20261028`。同一台 iPhone、相同关卡/技能/时间段对比：

1. A：统一帧率与恢复修复，使用原生 iOS DPR。
2. B：与 A 完全相同，iOS DPR 为 2。
3. 检查进入战斗后 `[BattlePerf]` 的 `targetFps=30`、`vsync=0` 和真实 FPS，
   再检查分享返回、切到后台再回来后是否仍维持目标，是否出现 15 FPS。
4. 核对 A/B 的 `renderWidth/renderHeight` 是否真实改变；仅修改配置而未改变实际画布不能算有效分辨率实验。
5. 如果 B 在同一密集波次明显改善，支持渲染像素负载方向；如果 A/B 都慢，继续测文本重建、物理与索敌 CPU，
   不能直接把「DPR 2 无效」等同为 GPU 完全无关。

30 FPS 是当前工程原本设定的运行目标，不能按 60 FPS 游戏标准解读平均帧率下降为性能退化。
真正需要对比的是长帧、帧时间分布、同一负载下的稳定性及恢复后调度。

## 本地验证结果

- 对真实旧导出函数执行 VM 测试，已复现独立微信调用被覆盖为 `[60,30,60]`，
  以及暂停恢复 `[30,60]`。原结果另存 `baseline-frame-scheduling-validation.json`。
- 对 C# 修补器生成的真实框架，暂停恢复为 `[30,30]`；RAF 和 33 ms 方式各连续恢复 5 次均维持 30。
  60/30/20/15 FPS 映射与原转换器的 16/33/66 ms 映射均被检查，实际 RAF 步长保持 1。
  这些验证的是当前 SDK 的 JavaScript 调度，不是 iOS 整机性能测量。
- 修补器幂等、Release 不输出调度日志、空或未知/残缺结构拒绝导出，共 5 项检查通过。
- Unity Play Mode 的目标帧率、vSync、失焦/恢复与暂停/恢复共 7 项检查通过，
  测试没有启用 AppBootstrap，也没有加载或保存玩家档案；分享流程 15 项回归再次通过。
- 独立空场景已输出新增的 revision、targetFps、vsync、frameCount、windowSeconds 字段。
  编辑器长帧中包含工具调用与首次运行开销，不能用该空场景样本评价 iOS 密集战斗。

构建结果、真实最终导出函数调度测试和包文件差异结果见
`Library/IosPerformance20261028` 与 `Build/ios-performance-20261028/comparison-manifest.json`。
iOS 新包效果仍待真机验证。

## 本轮交付

完整 WebGL 构建与微信导出成功，0 错误、4 条现有编译警告（音频不可达分支及未使用字段）。
已检查实际 WebGL 的 IL2CPP 输出：AppBootstrap.Awake 调用统一设置函数，
该函数确实调用原生 `QualitySettings.vSyncCount=0` 和 `Application.targetFrameRate=30`，
并非只编入 Editor 分支。最终小游戏 framework 的逻辑节拍修补与审计范围内的 SDK 桥接检查均通过。

| 项目 | 结果 |
|---|---|
| revision | `ios-frame-control-20261028` |
| codeMd5（A/B 相同） | `314f13982f792874` |
| dataMd5（A/B 相同） | `9537b5b6af26ba6f` |
| A 原生 iOS DPR | `Build/ios-performance-20261028/A-native-dpr` |
| B iOS DPR 2 | `Build/ios-performance-20261028/B-dpr2` |
| 默认新包（与 B 相同） | `Build/minigame/minigame` |
| B 上传目录大小 | 23,719,616 字节，约 22.62 MiB |
| B 主包大小 | 1,166,070 字节 |
| 旧 e92e 包留档 | `Build/ios-performance-20261028/before-frame-fix-e92e` |

A/B 共 66 个文件，逐文件 SHA256 比对仅 `check-version.js` 和 `game.js` 不同，
差异分别为 iOS DPR 条件和日志标签；Wasm、数据及所有其它资源完全相同。
12 组平台/DPR 条件测试通过，入口 JavaScript 语法检查通过，最终实际导出函数的恢复调度测试通过。
两份包的 codeMd5/dataMd5 相同是刻意保持二进制一致，辨认 A/B 应看 `[PerformanceBuild]` 的 `variant`。

建议先用 B 复测密集战斗，再用 A 在同机同条件对照；同时检查首次进入、分享返回与后台恢复。
保留 vConsole 中 `[PerformanceBuild]`、`[FrameSchedule]`、`[BattlePerf]` 三类日志。
如果云测 FPS 与 Unity Update 统计不一致，应先核对这几类实际日志，不能直接假定统计口径相同。
本轮完成的是已证实调度缺陷修复与可控分辨率试验包，尚没有新包 iOS 的实际改善数据。

## 09:32 新云测报告复核

来源：`Build/1.1.19测试报告/1793147785_wx27c7354ddadf45da_testcloud_result.xlsx`，
测试时间 2026-10-28 08:36:25–09:12:50。用户确认上传默认新包 `Build/minigame/minigame` 或 B-dpr2。
原工作簿只读，未修改。以下与同日 00:02 的 `1793116974` 报告比较。

| 机型 | FPS 旧→新 | CPU 旧→新 | 平均内存 MB 旧→新 | 峰值内存 MB 旧→新 | Stutter 旧→新 |
|---|---:|---:|---:|---:|---:|
| iPhone XS | 58→58 | 99.5%→93.6% | 790.6→743.5 | 820→777 | 0.8→1.7 |
| iPhone 12 Pro | 57→57 | 81.6%→76.3% | 813.8→776.5 | 836→800 | 1.1→1.1 |
| iPhone 15 Plus | 59→59 | 46.9%→47.4% | 842.5→821.2 | 856→869 | 0.5→0.5 |

定位：旧「启动运行性能详情」第 5、6、11 行；新表第 4、3、7 行。
三款 iOS 型号、系统、分辨率和硬件描述对应一致，但无设备唯一 ID 或同场景操作轨迹。
XS 为 iOS 18.5，另外两款为 17.5.1。两轮微信客户端均为 8.0.76.36、体验版本均为 1.1.19。

三设备等权平均 CPU 从 76.0% 降至 72.43%，下降 3.57 个百分点；
平均内存从 815.63 MB 降至 780.4 MB，减少 35.23 MB（约 4.32%）。
这支持本次采样中部分资源指标下降；不能据此归因 DPR 或证明密集战斗改善。
FPS 全部不变，XS Stutter 上升，另外两款不变，15 Plus 峰值内存还增加 13 MB。
当前结论仍是：没有足够证据认定 iOS 卡顿得到改善。

全设备概况：启动均值 3720→3283 ms，启动评分 79→83；运行 89、网络 75、兼容 99 均未变。
10 台设备全部为「建议优化」、失败设备 0，不能把「成功设备数 0」读成全部运行失败。
网络表全部设备仍各有 1 次 request 失败（100%），下载/上传失败均为 0；
未提供该 request 的 URL 或错误码，不能归因 SDK 桥、分享或资源下载。
兼容表 XS 仍有 1 次黑屏、黑屏率 0.6%，全部设备 JSError 为 0。
没有 OOM 或超限判据，不能仅凭内存数值认定已超限。

需优先查证：B 包目标为 30 FPS，但云测仍报 57–59 FPS。
工作簿没有 Unity Update 统计、实际渲染尺寸、调度调用或包指纹，不能据此确定目标帧率失效，
也不能用包配置替代真机生效证据。当前可用日志未提供本轮 iPhone 的三类诊断记录。
下一步需要本轮 iPhone 的 `[PerformanceBuild]`、`[FrameSchedule]`、`[BattlePerf]`，
核对 variant、codeMd5/dataMd5、targetFps/vsync、实际 fps、P95/P99、怪物数与渲染尺寸，
再决定继续排查调度、像素负载，还是文本重建、索敌和物理 CPU。

## 新报告触发的入口缺陷定位与 R2 修复

继续检查后确认了一个具体遗漏，不能再将接近 60 FPS 仅作为统计口径待查：
**上一轮实际导出包的首场景是 Login，而帧率设置只在 Boot 中的 AppBootstrap 执行。**

- `EditorBuildSettings` 的第一个启用场景为 Login，SDK 构建器按列表原序打包。
- 直接解析上一轮 B 包的 `globalgamemanagers`，实际场景顺序也为 Login、Boot、Game、Home、BattleLoading。
- AppBootstrap 仅挂在 Boot；Login 按钮直接进入 Home 或 BattleLoading，不加载 Boot。
- 上一轮资源包的 RuntimeInitializeOnLoads.json 中没有独立帧率初始化。
  因此，即使 IL2CPP 包含设置 30 FPS 的函数，默认玩家入口也不会调用它。
  框架恢复补丁只能保留收到的目标，不能自行把默认的 60 改成 30。

上一轮验证遗漏了真实打包首场景，证明了设置函数而没有证明运行入口。这是本次明确修正的验证缺口。
这一发现能解释目标没有下发，仍不能独自证明所有密集战斗卡顿均由它造成。

新增 AppFrameRateController，在 BeforeSceneLoad 自动安装独立的跨场景宿主，
启动、恢复焦点、暂停返回时应用统一目标；重复安装和静态重置复用已有宿主。
当前业务场景顺序保持不变，不创建 AppBootstrap、不触发其档案和跳转逻辑。
开发包新增 `[FrameControl]` 启动/恢复记录，revision 改为 `ios-login-entry-20261028-r2`。

从真实 Login 场景进入 Play Mode，确认 Boot/AppBootstrap 不存在，仍自动创建唯一宿主、
目标 30/vSync 0；启动、恢复、重复安装与静态重置共 12 项通过，旧设置函数的 7 项检查也通过。
已恢复编辑器原先 Boot 场景，打包首场景仍为 Login。

同次实测还纠正了物理时间步假设：Login 路径使用项目设置 fixedDeltaTime=0.02、
maximumDeltaTime≈0.3333，即原先 Boot 中的 30 Hz 物理设置也未执行。
R2 只修正帧率入口，未同时改物理步长，避免将不同收益与碰撞行为混在一起；
BattlePerf 现额外记录这两个实际参数。此前基于 30 Hz 的索敌工作量估算不适用于该 Login 启动路径。

R2 完整构建和实际产物入口核验结果见后续交付记录；iOS 实机长帧仍需新包日志验证。

### R2 最终交付结果

- 完整 WebGL 构建和微信转换成功；构建耗时 181.44 秒（不含后续转换压缩），0 错误、4 条现有编译警告。
- 实际压缩 data 包的 7 项核验通过：两种 runtime hook、R2 revision、Login 默认入口、原场景顺序、框架恢复补丁和诊断标记。
- 实际 IL2CPP 输出已确认 AppFrameRateController.Install/Apply 调用统一帧率设置函数；
  最终 framework 的 SDK 桥接审计和 JavaScript 恢复映射验证通过。
- Unity 已退出 Play Mode、恢复原 Boot 编辑器场景，无脏场景；构建首场景仍为 Login。
- 新 A/B 文件比较、12 组平台/DPR 条件、入口语法检查通过，66 文件仅两处策略/标签差异，二进制完全相同。

| 项目 | R2 结果 |
|---|---|
| revision | `ios-login-entry-20261028-r2` |
| codeMd5 | `3d35445cd2807010` |
| dataMd5 | `d2e617d521a101eb` |
| 推荐复测 | `Build/ios-performance-20261028-r2/B-dpr2` |
| 同机对照 | `Build/ios-performance-20261028-r2/A-native-dpr` |
| 默认目录 | `Build/minigame/minigame`，已更新为 R2 B |
| B 目录总量 | 23,723,347 字节，约 22.62 MiB |

验证记录在 `Library/IosPerformance20261028R2`，包比较记录在新目录的 `comparison-manifest.json`。
旧 `Build/ios-performance-20261028` 保留。本次修复已确认的运行入口遗漏，
没有把本地验证结果当作真机卡顿改善证据；下一轮需核对 R2 标记及实际帧时间。

## 11:24 报告与上传目录身份复核

新来源：`Build/1.1.19测试报告/1793154609_wx27c7354ddadf45da_testcloud_result.xlsx`，
只读，测试时间 2026-10-28 10:30:09–11:08:05。用户说明已重新上传 R2 并设为体验版后发起。
报告仍标记体验版本 1.1.19、微信 8.0.76.36，没有 revision/hash/上传包 ID；测试单 ID 不能当作包 ID。

| 机型 | CPU 08:36轮→10:30轮 | FPS 08:36轮→10:30轮 | Stutter 08:36轮→10:30轮 |
|---|---:|---:|---:|
| iPhone XS | 93.6%→92.8% | 58→59 | 1.7→1.3 |
| iPhone 12 Pro | 76.3%→76.0% | 57→57 | 1.1→0.4 |
| iPhone 15 Plus | 47.4%→52.7% | 59→59 | 0.5→0.4 |

三台等权平均 CPU 72.43%→73.83%，没有整体下降。Stutter 三台均下降，但不能仅凭这点认定总体性能问题解决。
XS 的存储描述从 60G 变为 239G，不能假定完全相同物理机；型号/系统一致不等于受控测试。
新表所有设备黑屏和 JSError 均为 0，兼容评分 100；每设备一次无 URL 的 request 失败仍在。

本地复核：默认 minigame 与 R2 B 共 66 文件逐项 SHA256 完全一致；实际包入口再验 7/7 通过，
引用全部指向 code 3d35445cd2807010/data d2e617d521a101eb，未发现引用旧包或 CDN。
这确认本地交付正确，不能单独证明云测选中的包身份。

11:38 从已打开微信开发者工具窗口的可访问页面信息直接提取 projectpath，确认为：
`G:\Demo\GongkaoMower\Build\ios-performance-20261028\B-dpr2`，窗口还显示「代码上传成功」。
这是旧 B 目录，缺少 `-r2`，对应旧 code314f13982f792874/data9537b5b6af26ba6f。
这提供了具体的选错目录线索；当前窗口不是上传历史，仍不足以百分之百证明 10:30 云测用的是旧包。

已另备唯一名称目录 `Build/WeChatUpload-iOS-R2-20261028`，显示名 GongkaoMower-iOS-R2-1028。
与 R2 B 逐文件验证，只有项目显示名配置不同，所有游戏文件一致；没有执行上传。
后续先用唯一目录和启动指纹确认上传/体验版对象，再判断 30 FPS 是否真正运行及 iOS CPU 方向。

## 13:33 报告：CPU 下降与 30 FPS 行为确认

用户重新构建后提供 `Build/1.1.19测试报告/1793162429_wx27c7354ddadf45da_testcloud_result.xlsx`，
只读，体验版本已为 1.1.20；测试时间 2026-10-28 12:40:29–13:10:48。
对照上一轮 1793154609（1.1.19，10:30）的相同型号：

| 机型 | CPU 旧→新 | 下降百分点 | 相对下降 | FPS 旧→新 | Stutter 旧→新 |
|---|---:|---:|---:|---:|---:|
| iPhone XS | 92.8%→51.5% | 41.3 | 44.5% | 59→30 | 1.3→0.6 |
| iPhone 12 Pro | 76.0%→39.6% | 36.4 | 47.9% | 57→29 | 0.4→0.7 |
| iPhone 15 Plus | 52.7%→36.1% | 16.6 | 31.5% | 59→30 | 0.4→0.3 |

来源位置：上一轮「启动运行性能详情」第 4、5、11 行，本轮第 4、3、11 行。
三款 iOS 的设备均值等权平均 CPU 从 73.83% 降至 42.4%，下降 31.43 个百分点，
相对下降约 42.6%。全设备概况 CPU 为 30.2%→18.6%，不是单独的 iOS 均值。
当前 10 款机型均为 29–30 FPS，与工程目标 30 一致；前几轮均约 60。
这次报告已经体现限帧后的运行行为和明确降负载收益，不再只有本地函数验证。
收益主要来自降低目标帧率后的单位时间工作量，不能当作每帧算法成本降低一半的证据。

三款 iOS 系统/型号一致，但 XS 存储描述从 239G 变为 60G，未证明同一物理机或相同操作轨迹；
也仍缺少 runtime hash、分段战斗负载与 P95/P99，因此不作精准包身份或密集战斗全部解决的结论。
当前默认导出 game.js 的 revision/code/data 仍与 R2 对应，仅作为本地信息记录。

残留项：12 Pro Stutter 从 0.4 升至 0.7（单位未标明）；XS 再有 1 次黑屏、0.5%，
其余设备黑屏为 0，全部 JSError 为 0。资源下载/上传失败为 0，各设备一次无 URL 的 request 失败仍在。
内存总体变化有限：iOS 平均 795.07→786.83 MB，XS 峰值 807→812 MB；不能声称内存问题也已解决。
本轮仅分析，未再修改游戏代码、包配置或性能策略。后续先确认群怪与特效密集阶段的体感和长帧。
