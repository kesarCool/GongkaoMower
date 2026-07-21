# Cocos Creator 引擎 · 面试备考

> 覆盖引擎特性、渲染、资源管理。8 年经验不是靠背 API，是靠「我用这个解决了什么问题」。
> 每题分四层：面试官问什么 → 标准答案 → 深入展开 → 项目实战故事。

---

## Q1：组件生命周期回调的执行顺序

**面试官在考什么**：你知不知道 `onLoad` / `start` / `update` 的执行时机差异——这直接决定 Init 逻辑放哪、会不会出现依赖未就绪的 Bug。

**标准答案（30 秒口述）**：

```
实例化 → onLoad（最早，只一次，数据初始化放这）
      → onEnable（每次 Active=true 都触发，订阅事件/注册监听放这）
      → start（晚于 onLoad，但保证同帧所有 onLoad 已跑完——依赖其他组件的数据放这）
      → update（每帧，只主循环逻辑）
      → lateUpdate（每帧末尾，跟随镜头/后处理）
      → onDisable（每次 Active=false，注销监听/清理临时引用）
      → onDestroy（销毁时，只一次，释放资源/取消所有订阅）
```

**关键坑位**：

- `onLoad` 里不能依赖其他组件的 `start` 初始化数据——因为 `onLoad` 比 `start` 早，其他组件可能还没初始化完。跨组件依赖初始化放 `start`。
- `onEnable` 不一定只调一次——预制体从对象池拿出来 `SetActive(true)` 会再触发一次。所以 `onEnable` 里不能做「只应该发生一次」的事（比如创建单例、加载全局资源）。
- `update` 里每帧 `getComponent` 或者 `Find` 是性能杀手——高频调用要缓存引用。

**8 年该怎么说（带入项目）**：

> 在《御剑三国》做 UI 系统的时候踩过这个坑。选角面板里，角色头像组件在 `onLoad` 里从全局数据管理器拿角色属性数据，但全局数据管理器的 `start` 比它晚，拿了个 null。后来定了规范——所有组件自己的数据初始化放 `onLoad`，跨组件的依赖初始化放 `start`，因为 Cocos 保证同帧内所有 `onLoad` 跑完才跑 `start`。

#### Q1 延伸：Update 和 FixedUpdate 的区别

**Unity 独有，Cocos Creator 没有 FixedUpdate**：Creator 没有独立物理时钟，`update(dt)` 直接带 deltaTime 参数。

**核心差异**：Update 跟着渲染帧走，FixedUpdate 跟着物理时钟走。两者频率没有固定关系。

```
时间轴：  0.0   0.02  0.04   0.06  0.08

30FPS 下：
Update      x     x     x      x     x       ← 每 0.033s 一次

FixedUpdate x  x  x  x  x  x  x  x  x  x     ← fixedDeltaTime=0.02s，每秒固定 50 次

15FPS（掉帧）：
Update      x           x            x        ← 变稀疏了

FixedUpdate x  x  x  x  x  x  x  x  x  x     ← 频率不变，还是 50 次
```

**为什么需要两条时间线**：物理计算对步长一致性要求极高——同样的力在不同步长下算出的轨迹不一样。碰撞检测以固定间隔执行，步长变了会漏碰撞。FixedUpdate 保证物理步长恒定，帧率怎么波动都不影响物理结果。

**什么放哪里**：

| Update | FixedUpdate |
|---|---|
| 输入采集（按键/触摸） | 碰撞检测（`OnTriggerEnter` 等） |
| 移动（`transform.Translate`） | 刚体力（`AddForce` / `velocity`） |
| 相机跟随 | 自定义确定性 Tick |
| UI 刷新、计时器显示 | 帧同步逻辑驱动 |

**常见坑**：

- **FixedUpdate 里读 Input**：帧率低时一帧内 FixedUpdate 可能连续跑两三次，每次都读到同一个按键 → 玩家戳一下技能发了三发。正确做法：Update 里 `Input.GetKeyDown` → 设标记位，FixedUpdate 里读标记位。
- **高速运动物体放 Update 碰撞会穿透**：子弹快 → Update 帧间越过了敌人 → 碰撞没触发。解法：高速物体走 `Rigidbody.MovePosition`（连续碰撞检测）或直接用 `Raycast`。
- **文字割草机的做法**：锁了 30FPS + `fixedDeltaTime = 1/30s`，渲染帧跟物理步同步，避免两条时间线不一致导致的碰撞漏帧。

---

## Q2：@ccclass / @property / @executeInEditMode 装饰器

**面试官在考什么**：能不能用 Cocos 的装饰器体系做编辑器可视化配置——这是 Cocos 跟纯代码开发的核心差异，体现"组件化思维"。

**标准答案（30 秒口述）**：

```typescript
@ccclass('PlayerController')  // 声明这是一个 Cocos 组件，可挂到节点上
export class PlayerController extends Component {

    @property({ type: cc.Integer, tooltip: '移动速度' })
    speed: number = 300;  // 编辑器面板里直接调，不用改代码

    @property({ type: cc.Prefab, tooltip: '子弹预制体' })
    bulletPrefab: cc.Prefab = null;  // 编辑器拖拽绑定，不用写 Resources.Load

    @property({ type: cc.Node, tooltip: '血条节点' })
    hpBar: cc.Node = null;
}
```

| 装饰器 | 作用 | 没有会怎样 |
|---|---|---|
| `@ccclass` | 声明为组件类 | 无法挂到节点上，引擎不认识 |
| `@property` | 属性暴露到编辑器面板 | 策划调参数必须找程序改代码 |
| `@executeInEditMode` | 编辑器模式下也跑 `update` | 编辑器里看不到实时预览效果 |

**`@property` 的高级用法**：

```typescript
// 类型 + 范围 + slide 条
@property({ type: cc.Float, range: [0, 1, 0.01], slide: true })
critRate: number = 0.3;

// 下拉菜单（限定选项）
@property({ type: cc.Enum(SkillType) })
skillType: SkillType = SkillType.Fire;

// 节点数组（编辑器里拖多个）
@property({ type: [cc.Node] })
waypoints: cc.Node[] = [];
```

**`@executeInEditMode` 的实际场景**：

地图编辑器里摆了格子 → 需要实时看到寻路网格线的变化 → 挂 `@executeInEditMode` → 编辑器里挪障碍物，寻路线段瞬间刷出来。没有这个装饰器 → 必须点运行才能看 → 迭代效率直接废了。

**8 年该怎么说**：

> 在《御剑三国》里做地图编辑器的时候，策划要能可视化地配关卡波次——在哪刷怪、刷什么怪。用 `@property` + `@executeInEditMode` 做了个编辑器工具，策划在场景里拖出生点标记，编辑模式下实时看到波次预览。这个工具让策划不依赖程序员就能独立调关卡，迭代速度从「提需求→程序改→出包→验证」三天变成半天。

---

## Q3：Prefab 嵌套与实例化；改 Prefab 如何同步实例

**面试官在考什么**：知不知道 Prefab 的继承链机制，以及多人协作下 Prefab 修改后怎么保证所有实例正确同步。

**标准答案**：

```
实例化流程：
  Prefab Asset（模板）
       ↓ instantiate()
  实例节点（拷贝，有自己的数据）

修改同步规则：
  - 改了 Prefab Asset 的属性值 → 实例的「未覆盖属性」自动同步
  - 实例自己的「覆盖属性」——不会同步（引擎尊重你的局部修改）
  - 改了 Prefab 的层级结构（加/删子节点） → 实例同步结构变更
```

**嵌套 Prefab 的常见坑**：

```
场景：
  EnemySpawner.prefab 里嵌了一个 Enemy.prefab 的实例

  Enemy.prefab 改了属性 → EnemySpawner.prefab 里的实例会同步 ✅
  Enemy.prefab 改了属性 → 场景里已生成的敌人物体不会同步 ❌（已经不关联 Prefab）
```

所以嵌套 Prefab 里改了里层 Prefab 的属性，**要确保外层的 Prefab 重新保存一次**，否则构建时可能拿到旧数据。

**多人协作下 Prefab 冲突的处理**：

`.prefab` 是 JSON 文本，两个人同时改同一个 Prefab 的同一个节点 → JSON diff 冲突 → 解法不是手改 JSON（找死），是**模块化拆分**和**划分所有权**。

**8 年该怎么说**：

> 《御剑三国》的 UI 预制体一度 50+ 个，经常出现两个人改同一个通用 Panel 产生冲突。后来拆成三级结构：通用 UI 元件（按钮底框/弹窗背板）→ 功能组件（商店卡片/背包格）→ 完整面板。每个 Level 指定一个人负责，跨 Level 的改动必须知会负责人。`.prefab` 冲突率从每周一把降到基本为零。

---

## Q4：资源管理 resources / assetManager / Bundle，如何防泄漏

**面试官在考什么**：知不知道 Cocos 各个资源加载方式的使用场景，以及引用了资源不释放导致内存泄漏的常见情况。

**标准答案**：

| 加载方式 | 适用场景 | 释放方式 | 风险 |
|---|---|---|---|
| `cc.resources.load` | 少量固定资源（启动 Logo、首帧 UI） | `cc.resources.release` 或自动（场景切换） | 不会自动释放，忘了就泄漏 |
| `cc.assetManager.loadBundle` | 大模块资源（战斗 Bundle、大厅 Bundle） | `bundle.releaseAll()` | Bundle 内部互相引用容易环 |
| `cc.assetManager.loadRemote` | CDN 远程资源（活动图、热更新资源） | 手动 release | 网络失败要兜底 |

**内存泄漏的三种典型场景**：

**场景一：加载了一直没用，也没释放**

```typescript
// ❌ 加载了 A Bundle 的一张贴图，后来不用了，忘了 release
cc.assetManager.loadBundle("Battle", (err, bundle) => {
    bundle.load("bg", cc.SpriteFrame, (err, sf) => {
        this.bgSprite.spriteFrame = sf;  // 用了
        // 切场景后 this 销毁了，但 sf 被 bundle 持有引用，没释放
    });
});
// 正确做法：场景销毁时 release
onDestroy() {
    cc.assetManager.getBundle("Battle")?.release("bg");
}
```

**场景二：闭包持有引用**

```typescript
// ❌ 定时器里的闭包持有贴图引用 → 即使场景切了，贴图因为是 bundle 加载的不会被 GC
this.schedule(() => {
    this.sprite.spriteFrame = someLoadedSpriteFrame;  // 闭包引用
}, 0.1);
```

**场景三：Bundle 的循环依赖**

```
Battle Bundle 引用了 Common Bundle 的贴图
Common Bundle 引用了 Battle Bundle 的音效
→ 两个 Bundle 互相引用，谁的 releaseAll 都不彻底
```

**Cocos 3.x 的改进**：3.x 里资源加载统一走 `assetManager`，加载和释放都有引用计数追踪，`addRef` / `decRef` 统一管理——比 2.x 的散装 API 安全不少。但引用计数不代表万事大吉，循环引用还是破了。

**8 年该怎么说**：

> 《御剑三国》在海外多语言版本上线时，切语言需要重新加载对应语言的 UI 图集。最早直接用 `cc.resources.load` 加载每个语言包，切语言时没有主动 release 旧语言包——切三四次之后低端机直接 OOM。后来全改成 Bundle 管理：每种语言单独一个 Bundle，切换时 `oldBundle.releaseAll()` 再加载新的。另外在 `onDestroy` 里加了防御——挂在这个节点下的所有 load 操作全 cancel 掉。内存稳定之后在 512MB 的低端安卓机上也跑得动了。

---

## Q5：事件系统 node.on / EventTarget，冒泡与捕获

**面试官在考什么**：知不知道 Cocos 事件系统的冒泡机制，以及 TOUCH 事件在节点树里的传播路径——这直接决定 UI 穿透点击怎么修、全局事件怎么拦截。

**标准答案**：

**Cocos 2.x 事件传播**：

```
触摸屏幕 → 引擎从场景根往下找 → 命中目标节点 → 事件冒泡往上

阶段一：捕获阶段（从根往目标，少用）
阶段二：目标阶段（到达目标节点）
阶段三：冒泡阶段（从目标往根，常用——UI 穿透问题通常在这一层修）
```

```typescript
// 冒泡：子节点触发 → 往上通知父节点
this.node.on('click', callback, this);         // 默认：目标 + 冒泡
this.node.on('click', callback, this, true);   // 第四个参数 true：捕获阶段

// 阻止冒泡（防止 UI 穿透）
event.stopPropagation();  // 停下来，父节点收不到
```

**典型 Bug：弹窗遮罩没挡住点击**

```
场景：
  ShopPanel（浮在最上面）
    ├─ 遮罩层（透明全屏 Image，点右上角关闭）
    └─ 内容层

  遮罩层的空白区域点了 → 点击穿透到下面地图上的城池 → 触发了行走

解决：
  遮罩层挂上 touch 事件 → 回调里 event.stopPropagation() → 点击被拦截
  或者遮罩层挂 Button 组件（默认 swallowTouches=true，自动拦截）
```

**Cocos 3.x 的变化**：

3.x 里事件从 `cc.Node.EventType` 变成了 `Node.EventType`，`cc.Event` 变成了 `EventTouch`，但冒泡机制没变。新增了 `EventTarget` 类（纯逻辑事件，不跟节点绑定——相当于自己造一个不挂 GameObject 的事件发射器）。

**自定义全局事件总线的实际用法**：

```typescript
// 不挂节点的全局事件总线
class GameEventBus {
    private static _eventTarget = new cc.EventTarget();

    static on(event: string, cb: Function, target?: any) {
        this._eventTarget.on(event, cb, target);
    }

    static emit(event: string, ...args: any[]) {
        this._eventTarget.emit(event, ...args);
    }
}

// 用法：跨模块通信 + 不用挂节点
GameEventBus.on('player_died', (data) => { ... });
GameEventBus.emit('player_died', { enemyId: 5 });
```

**8 年该怎么说**：

> 《御剑三国》野外地图上，预览行军路线的时候，路线节点和城池节点重叠 → 点击路线经常会穿透点到城池上 → 弹出城池信息面板。调试之后发现是事件系统的冒泡顺序问题——路线节点在上层，但城池节点是父节点→冒泡到了城池上触发了城池的 click。解法是在路线节点的 touchstart 回调里 `stopPropagation`，加上父节点做了一个遮罩拦截——只有当前操作模式是「查看路线」时才拦截，其他模式正常穿透。后来这个模式开关封装成了事件管理器的一个 feature，地图上所有交互都根据当前模式决定是否拦截冒泡。

---

## Q6：Draw Call 如何产生，列举 ≥4 种降低手段

**面试官在考什么**：这是纯渲染管线面试题，要讲到合批被打断的原因，不是只背手段名称。

> 详细回答见 [面试高频追问.md](面试高频追问.md) 第七章 7.9 节，包含了 Draw Call 打断的 7 个原因和 9 种降 Draw Call 手段。这里补充 Cocos 特有的部分：

**Cocos 的自动合批（Auto Batch）机制**：

Cocos 引擎在渲染时会自动尝试将**相邻的、使用同一纹理的 Sprite** 合并成一次 Draw Call。条件全部满足才能合：

```
合批条件（缺一不可）：
  ✅ 相邻的 RenderComponent（中间不能被其他纹理的节点隔开）
  ✅ 使用同一个 Texture（或者说同一张 SpriteAtlas）
  ✅ 使用同一个 Material（Shader 和参数一样）
  ✅ 同一个 Blend 模式
  ✅ 没有参与 Mask（在 Mask 内的节点自成一个批次）
```

**Cocos 3.x 合批流程图**：

```
场景节点树
  ↓ 遍历收集 RenderComponent
  ↓ 生成 RenderData（顶点/UV/纹理ID）
  ↓ 按（Layer → Material → Texture → Z-order）排序
  ↓ 遍历排序后的队列：
     当前纹理 == 上一个纹理 → 合入当前批次
     当前纹理 != 上一个纹理 → 提交上一个批次 → 开启新批次
  ↓
GPU Draw Calls
```

**Cocos 降 Draw Call 的 6 大手段**：

| 手段 | 难度 | 效果 | 说明 |
|---|---|---|---|
| **自动图集** | 低 | ★★★★★ | 把散图拖进 Auto Atlas 配置 → 构建时自动合成大图 |
| **静态合图** | 中 | ★★★★★ | 不动的装饰树/建筑标记为 Static → 引擎合并 Mesh |
| **Label 字符集** | 低 | ★★★★ | 用 Bitmap Font 代替系统字体，多段文字共用字符纹理 |
| **减少 Mask** | 中 | ★★★ | 每个 Mask = +2 DC，能不用就不用 |
| **调整节点层级** | 低 | ★★ | 把同纹理的节点在树里靠在一起，避免被插队打断 |
| **分 Camera** | 高 | ★★★ | 战斗和 UI 不同 Camera → 独立渲染队列，互不打断 |

**8 年该怎么说**：

> 《御剑三国》野外地图上，同屏 200+ 棵树、石头、城池装饰，早期不做优化 Draw Call 能上 300。优化分了三步：第一步，所有地图装饰统一走一张 SpriteAtlas，自动合图直接把同屏树从一百到几个 DC。第二步，城池装饰标记为 Static，引擎构建时做静态合图。第三步，把野怪、行军线、特效分别限制在同屏上限 50 以内，超过不渲染——既控了 DC 也控了逻辑开销。最终同屏 200 棵树的场景稳定在 15-20 Draw Calls 以内。

> 另一个印象深刻的点是野外地图上有个行军预览线——用 Graphics 画的线段。每次手指滑动都重新描绘 → 每帧多 3-4 个 Draw Call。后来改成用一个预渲染的虚线纹理 + Scale 拉伸 → 1 个 DC 解决问题。

---

## Q7：渲染流程与自定义 Shader / Effect

**面试官在考什么**：知不知道 Cocos Effect 文件的语法结构，能不能写简单 Shader，以及性能影响。

**Cocos 渲染流程（简化）**：

```
① Application（逻辑层）
   节点 update / 动画 / 物理

② Scene Culling（裁剪）
   剔除视野外节点 → 只留可见的进渲染队列

③ Render Pipeline（渲染管线）
   收集 RenderData → 排序 → 合批 → 每批提交 Draw Call

④ GPU（图形处理器）
   顶点着色器 → 光栅化 → 片段着色器 → 输出到帧缓冲
```

**自定义 Shader 在 Cocos 里怎么写**：

Cocos 用的 Effect 文件是 YAML + GLSL，不像 Unity 的 HLSL/ShaderLab。结构分两块：

```yaml
# my_effect.effect
CCEffect %{
  techniques:
  - passes:
    - vert: my_vert
      frag: my_frag
      blendState:
        targets:
        - blend: true
          blendSrc: src_alpha
          blendDst: one_minus_src_alpha
      properties:
        glowColor: { value: [1, 0, 0, 1] }  # 暴露给编辑器的参数
}%

CCProgram my_vert %{
  // GLSL 顶点着色器
  precision highp float;
  // ...
}%

CCProgram my_frag %{
  // GLSL 片段着色器
  precision highp float;
  uniform vec4 glowColor;
  // ...
}%
```

**常见自定义效果**：

| 效果 | 原理 | 性能开销 |
|---|---|---|
| 外发光 | Frag Shader 边缘检测 + 颜色叠加 | 中 |
| 灰度化 | dot(color.rgb, vec3(0.299, 0.587, 0.114)) | 低 |
| 溶解（Dissolve） | 噪声纹理 + clip(noise - threshold) | 中 |
| UV 动画（流水/瀑布） | Vert Shader 里偏移 UV | 低 |

**8 年该怎么说**：

> 在《御剑三国》里做了两个 Shader 效果。一个是技能卡的稀有度外发光——用 Frag Shader 取邻近像素的颜色差来画描边，配合 `@property` 暴露颜色参数到编辑器，美术不用碰 Shader 就能调金色/紫色。另一个是行军线的流动虚线效果——用 UV 偏移动画让线段看起来在流动，比用序列帧省了大半内存。

> 写 Effect 的时候踩过一个坑：忘了在移动端声明 `precision highp float` → 片段着色器用低精度跑 → 边缘渐变出现色带（Banding）。加上 highp 之后那台测试机恢复正常。这之后所有移动端 Shader 都是 highp 开头。
