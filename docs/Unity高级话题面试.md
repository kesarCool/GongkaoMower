# Unity 高级话题 · 面试备考

> 这四个话题你实际项目没直接用到，但面试官可能问来摸上限。
> 策略：诚实说"我项目用的是 Built-in RP / 2D"，但能展开讲清楚是什么、为什么、什么场景用。

---

## 一、URP / HDRP 渲染管线

### 1.1 先搞清楚 Built-in、URP、HDRP 是什么关系

Unity 有三套渲染管线，不是「哪个更好」，是「哪个适合你的项目」：

| | Built-in（内置） | URP（通用） | HDRP（高清） |
|---|---|---|---|
| 定位 | 老牌默认管线 | 移动端 / 跨平台主力 | 高端 PC / 主机 3A |
| 画质上限 | 中 | 中高 | 非常高 |
| 性能 | 一般 | **好**（为移动端优化） | 差（吃硬件） |
| 可定制性 | 有限（改 Shader 麻烦） | **高**（SRP 架构，可定制） | 高 |
| 适用 | 2D / 原型 / 老项目 | 手游、独立游戏、VR | PC 3A、影视级实时渲染 |
| 你的项目 | ✅ 文字割草机 | — | — |

### 1.2 核心区别：SRP（可编程渲染管线）

Built-in 的渲染流程是黑盒——Unity 内部写好了一套固定的渲染步骤，你只能改某些参数，不能改渲染顺序或自定义 Pass。

URP 和 HDRP 都基于 **SRP（Scriptable Render Pipeline）**——渲染流程暴露给你，你可以：

- 自定义渲染 Pass（比如先画角色再画场景，而不是默认的先场景再角色）
- 自定义 Shader 以 SRP Batcher 兼容方式工作（合批效率比 Built-in 的 GPU Instancing 更好）
- 控制每个 Pass 的渲染目标和后处理

```csharp
// SRP 定制渲染流程（伪代码，展示概念）
class MyRenderer : RenderPipeline {
    override void Render() {
        SetupCamera();
        DrawOpaque();        // 画不透明物体
        DrawSkybox();        // 天空盒
        DrawTransparent();   // 半透明——我可以调顺序
        ApplyPostProcess();  // 后处理
        DrawUI();            // UI 放最后
    }
}
```

### 1.3 URP 的关键优化：SRP Batcher

这是 URP 跟 Built-in 性能差异的核心原因之一。

Built-in：每个材质一次 SetPass Call（切换 Shader 参数） → 材质越多，切换越频繁 → 性能差。

URP 的 SRP Batcher：把一堆材质参数打包进 GPU 常量缓冲区 → 一次刷新全提交 → 材质切换不再是瓶颈 → 场景里几十种材质也能高效渲染。

### 1.4 面试怎么答

> 个人项目《文字割草机》用的是 Built-in 渲染管线——2D 割草类不需要 URP 的 PBR/后处理特性，Built-in 够用且构建链路成熟。但 URP 我了解——它是基于 SRP 架构的可定制管线，核心优化在 SRP Batcher 上，通过常量缓冲区批量提交材质参数，Draw Call 切换效率比 Built-in 高很多。如果我下一个项目是 3D 手游，URP 是首选——画质够、性能好、定制灵活。HDRP 是给 3A 级别的高清渲染用的——光线追踪、体积光、HDR 全支持，但微信小游戏跑不动。

---

## 二、Shader 编写（HLSL / ShaderGraph）

### 2.1 三套 Shader 体系的关系

| | ShaderLab（Built-in） | HLSL + ShaderLab（URP/HDRP） | ShaderGraph |
|---|---|---|---|
| 怎么写 | 写 `.shader` 文件，内嵌 CGPROGRAM | `.shader` 或 `.hlsl` 文件，纯 HLSL | 节点连线，不用写代码 |
| 适用管线 | Built-in | URP / HDRP | URP / HDRP |
| 难度 | 中 | 高（纯代码） | 低（可视化） |
| 灵活性 | 中 | 最高 | 中 |

### 2.2 Built-in Shader 的写法（你实际用的）

```hlsl
// Unity Built-in Surface Shader
Shader "Custom/MyShader" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
}
```

### 2.3 ShaderGraph 是什么

ShaderGraph 是 Unity 的可视化 Shader 编辑器——拖节点 + 连线，不用手写 HLSL。适合快速原型和美术自主调整效果，但性能优化空间不如手写。

**适用场景**：角色材质（溶解、消融、外发光）、后处理特效（屏幕扭曲、色调映射）、水波/流动效果。

### 2.4 面试怎么答

> 我在《文字割草机》项目里写过简单的 Shader——敌人受击闪白、角色技能区域的光环效果。用的 Built-in 的 Surface Shader（CGPROGRAM），HLSL 语法了解但没在实际项目中大量手写。ShaderGraph 我了解它的节点连线方式，适合快速出效果和美术调整，但如果要做深度性能优化还是得手写 HLSL。

---

## 三、ECS / DOTS

### 3.1 先搞清楚：ECS 不是"MonoBehaviour 的替代品"

ECS 要解决的核心问题是**CPU 缓存利用率**。

**传统 MonoBehaviour**：

```
10000 个敌人，每个是一个 GameObject → 每个有自己的 Transform、EnemyAI、EnemyStats
→ 数据散落在内存各处
→ CPU 处理敌人 A 的 Move → 跳到内存另一处取敌人 B 的数据 → 缓存失效（Cache Miss）
→ 在等待内存读取时 CPU 空转
```

**ECS（Entity Component System）**：

```
10000 个敌人 → 所有"位置"数据连续存在一个数组里、所有"血量"数据连续存在另一个数组里
→ CPU 可以一口气处理所有敌人的位置更新
→ 数据在内存里是连续的 → 缓存命中率高 → 极快
```

| | MonoBehaviour | ECS |
|---|---|---|
| 数据存储 | 散落内存 | 连续数组（Chunk） |
| CPU 缓存 | 频繁失效 | 高命中率 |
| 适合场景 | < 1000 个对象 | 10000+ 个对象 |
| 学习曲线 | 低 | 高（思维模型完全不同） |
| Unity 官方态度 | 继续支持 | DOTS 是未来方向，但目前不成熟 |

### 3.2 DOTS 全家桶

DOTS（Data-Oriented Technology Stack）包括四样东西：

| 组件 | 作用 |
|---|---|
| **ECS** | 数据布局 + 系统架构 |
| **Burst Compiler** | 把 C# Job 代码编译成极致优化的机器码 |
| **C# Job System** | 多线程安全的任务调度 |
| **Mathematics** | 针对 SIMD 优化的数学库（`float3` 代替 `Vector3`） |

### 3.3 DOTS 的现实状况（2026 年）

DOTS 发布多年了，但有几个现实问题：

- **1.0 才刚稳定**：API 一直在变，升级成本高
- **生态不完善**：大量 Asset Store 插件不兼容
- **团队门槛高**：从 OOP 思维切到 DOD 思维，大部分团队不愿意
- **只适合特定场景**：真需要十万人同屏的只有割草/模拟经营/RTS 这类，大多数手游不需要

### 3.4 面试怎么答

> ECS 的思想我了解——核心是把数据从面向对象转为面向数据组织（Data-Oriented Design），利用 CPU 缓存的局部性原理提升处理海量对象时的性能。文字割草机同屏敌人数百，用传统的 GameObject + 对象池已经能在 30 FPS 下稳定运行。DOTS 我关注它的发展但没在项目中使用——当前 Unity 版本下 DOTS 的兼容性和团队学习成本对于个人独立项目来说不划算。如果未来做万人同屏的 RTS 或模拟经营类，ECS + Burst + Job System 会是我考虑的方向。

---

## 四、碰撞检测 / 物理 / 网络同步

### 4.1 除了碰撞体 + 刚体，还有哪些方式

Unity 的 `Collider` + `Rigidbody` 是物理引擎方案，缺点是重（每帧遍历碰撞对 + 解算 + 回调分发）。对于不需要真实物理的项目，几种替代：

**替代一：距离判定（最轻量）**

```csharp
// 不需要物理引擎——纯数学，零开销
float dist = Vector2.Distance(bullet.position, enemy.position);
if (dist < hitRadius) { OnHit(); }
```

文字割草机里子弹打怪就是用 `sqrMagnitude` 判定——几十个子弹 × 100 个怪，不需要物理引擎。

**替代二：空间哈希 / 网格分区**

```
把地图切成 256×256 的格子：
→ 每个格子维护一个"里面有谁"的列表
→ 子弹要检测碰撞 → 只查子弹所在格 + 相邻 8 格 → 不用遍历全场景
→ 从 100×100 次比对 → 几×几
```

**替代三：四叉树（Quadtree）**

```
场景 → 分四个象限 → 每个象限再分四个 → 递归
→ 只查有对象的区域
→ 动态对象移动时更新树节点
→ 适用于"分布不均匀"的场景（大多数区域空着，集中在少数区域）
```

**替代四：物理引擎但不用全物理模拟**

```csharp
// Rigidbody 只做碰撞检测（IsKinematic = true），不动画/受力不管
rigidbody.isKinematic = true;
// 移动自己算（不靠刚体力）
transform.position += direction * speed * Time.deltaTime;
// 碰撞回调还在 → 只用接触检测部分，不付物力模拟的代价
```

### 4.2 物理运动——什么时候自己算，什么时候交给引擎

| 场景 | 用谁 | 为什么 |
|---|---|---|
| 障碍物反弹、重力下落 | Rigidbody + 物理引擎 | 真实物理自己算太复杂 |
| 子弹直线飞行 | 自己算 `position += dir * speed * dt` | 直飞不需要物理，引擎开销完全不值 |
| 击退效果 | 自己算 + 插值曲线 | 击退是游戏设计，不是物理模拟。插值比物理更可控 |
| 角色沿地面移动 | CharacterController 或自己算 | 角色移动设计 > 物理正确性 |

**关键认知**：游戏物理 ≠ 真实物理。玩家不关心击退是不是用牛顿力学精确算的——他们只关心「手感好」。所以大部分游戏动作是「自己算 + 插值」，物理引擎只用于真正需要的地方。

### 4.3 网络同步的物理

**物理在网络同步里是灾难**——物理引擎的浮点计算在不同平台/不同编译优化下结果不完全一致。帧同步（Lockstep）要求完全确定性，所以帧同步游戏**不能用物理引擎做核心逻辑**——碰撞、移动全得自己实现确定性版本。

具体处理方式：

```
帧同步：
  物理 = 自己写确定性算法
  碰撞 = 定点数或者（简化后）的浮点——必须确保所有客户端算出来完全一样
  不用 Unity 的 PhysX（不保证确定性）

状态同步：
  物理 = 服务端算（PhysX 可以用，不要求客户端一致性）
  客户端 = 预测 + 插值 + 和解（收到服务端位置后平滑拉正）
```

### 4.4 面试怎么答

> 碰撞检测不一定需要碰撞体和刚体。文字割草机项目里，同屏数百敌人的情况下物理引擎开销太高——子弹打怪的碰撞用 `sqrMagnitude` 距离判断，配合 `CombatTargetRegistry` 索敌注册表把遍历范围从全场景缩减到活跃敌人列表。如果敌人更多，还可以引入空间网格分区——把地图切成格子，只查子弹所在格和相邻格的敌人。网络同步方面，帧同步要求确定性物理——不能依赖 Unity 的 PhysX，要自己实现定点或简化的碰撞算法。状态同步不要求客户端一致性，服务端跑 PhysX，客户端做预测 + 插值和服务器和解。实际项目里我没做过网络物理同步的落地，但理解它的核心矛盾——确定性 vs 物理真实性的取舍。
