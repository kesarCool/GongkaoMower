# C# 与 TypeScript 核心差异（面试备考）

> 目标公司技术栈偏 Cocos Creator（JS/TS），但你简历上主力语言是 C#。面试官大概率会问"两种语言你都会，说一下它们的区别"——这个问题不是考语法，是考你对语言设计的理解。

---

## 一、泛型：最大的不同点

### 1.1 核心差异一句话

| | C# 泛型 | TypeScript 泛型 |
|---|---|---|
| 存在时间 | **运行时**保留类型信息 | **编译时**存在，运行时就没了（类型擦除） |
| `List<int>` 在运行时 | 系统知道它是 `List<int>` | 系统只知道它是 `Array`，不知道里面装的啥 |
| 能 `typeof(T)` 吗 | ✅ 能 | ❌ 不能 |
| 能 `new T()` 吗 | ✅ 能（加 `where T : new()`） | ❌ 不能（运行时 T 不存在） |

### 1.2 同一个需求，两种写法

**场景**：写一个函数，接收任意类型的数组，返回第一个元素。

```csharp
// C# — 运行时知道 T 是什么
public T First<T>(List<T> items)
{
    if (items.Count == 0)
        throw new Exception("空数组");
    return items[0];
}

// 运行时可以检查 T 的类型
public T Create<T>() where T : new()
{
    return new T();  // ✅ 运行时 T 是具体类型，能 new
}
```

```typescript
// TypeScript — 编译后 T 就消失了
function first<T>(items: T[]): T {
    if (items.length === 0)
        throw new Error("空数组");
    return items[0];
}

// ❌ 运行时不能 new T()
function create<T>(): T {
    return new T();  // 编译报错：T 只存在于类型空间
}

// ✅ 变通：把构造函数当参数传进去
function create<T>(ctor: new () => T): T {
    return new ctor();
}
```

编译后的 JS：

```javascript
// TS 泛型编译后——T 完全消失
function first(items) {
    if (items.length === 0) throw new Error("空数组");
    return items[0];
}
// 没有任何类型检查代码，全是纯 JS
```

### 1.3 六个实际场景——游戏项目里泛型无处不在

**场景 1：单例基类——任何 MonoBehaviour 都能变成单例**

不用泛型的话，每个 Manager 都要把 Instance 代码抄一遍。用了泛型，一个类搞定所有：

```csharp
// 定义一次，所有 Manager 复用
public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<T>();
            return instance;
        }
    }
}

// 用的时候——
public class UIManager : MonoSingleton<UIManager> { }
public class TableManager : MonoSingleton<TableManager> { }
public class AudioManager : MonoSingleton<AudioManager> { }

// 调用
UIManager.Instance.Open<ShopPanel>();
TableManager.Instance.GetTableItem<LevelWave>(101);
```

TypeScript 里做不到——因为编译后 `T` 消失了，`FindObjectOfType<T>()` 在运行时不知道 T 是什么：

```typescript
// ❌ TS 没有运行时泛型，必须把类本身当参数传
class MonoSingleton {
    // 要传具体的类进来，泛型做不到
}
// ✅ 变通：构造函数注册 + 手动管理
```

**场景 2：UI 弹窗打开——`UIManager.Open<T>()`**

```csharp
// 不用泛型 —— 每种弹窗一个方法，烦死
public ShopPanel OpenShopPanel() { ... }
public SettingPanel OpenSettingPanel() { ... }
public BagPanel OpenBagPanel() { ... }

// 用了泛型 —— 一行搞定所有
public T Open<T>(object payload = null) where T : UIPanelBase
{
    var type = typeof(T);           // ✅ 运行时知道 T 是什么
    var panel = GetOrCreateInstance(type) as T;
    // ... 入栈、显示
    return panel;
}

// 调用方 —— 简洁，类型安全
var shop = UIManager.Instance.Open<ShopPanel>(shopData);
// shop 的类型是 ShopPanel，IDE 有完整补全

var bag = UIManager.Instance.Open<BagPanel>();
// bag 的类型是 BagPanel，不用强转
```

**场景 3：对象池 Get——`GameObjectPool.Get<T>()`**

```csharp
// 不用泛型 —— 调用方要手动 GetComponent
GameObject go = GameObjectPool.Get(bulletPrefab, pos, rot);
Bullet bullet = go.GetComponent<Bullet>();  // 手动取组件

// 用了泛型 —— 直接返回组件类型
public static T Get<T>(T prefabComponent, Vector3 pos, Quaternion rot, Transform parent = null)
    where T : Component
{
    var go = Get(prefabComponent.gameObject, pos, rot, parent);
    return go != null ? go.GetComponent<T>() : null;  // ✅ 内部搞定
}

// 调用方 —— 干净
Bullet bullet = GameObjectPool.Get(bulletPrefab, pos, rot);
// bullet 的类型直接是 Bullet，不用再 GetComponent
```

**场景 4：事件总线——`EventBus.Subscribe<T>()` + `Publish<T>()`**

```csharp
// 每种事件是一个 struct/class
public struct EnemyDiedEvent { public EnemyBase enemy; public Vector3 pos; }
public struct PlayerDamagedEvent { public float damage; public float hpLeft; }

// 订阅 —— 泛型决定了能收到什么事件
EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
EventBus.Subscribe<PlayerDamagedEvent>(OnDamaged);

// 发布 —— 泛型决定了路由到哪些订阅者
EventBus.Publish(new EnemyDiedEvent { enemy = boss, pos = boss.transform.position });
EventBus.Publish(new PlayerDamagedEvent { damage = 10, hpLeft = 90 });

// 回调的强类型 —— 不用 object 转来转去
void OnEnemyDied(EnemyDiedEvent e)  // ← 直接拿到正确类型
{
    e.enemy.health;  // ✅ IDE 补全，编译期类型检查
}
```

不用泛型的话就得用 `object` + 各种 `is` / `as` 判断，又慢又容易写错。

**场景 5：配表查询——`TableManager.GetTableItem<T>(id)`**

```csharp
// 不用泛型 —— 每种表一个方法
public LevelWave GetLevelWave(int id) { ... }
public Monster GetMonster(int id) { ... }
public ChapterLevel GetChapterLevel(int id) { ... }

// 用了泛型 —— 一个方法搞定所有表
public T GetTableItem<T>(int id) where T : class
{
    // typeof(T) 得到表类型 → 查对应字典 → 返回强类型对象
    var dict = mTypeTableDict[typeof(T)];
    return dict[id] as T;
}

// 调用
LevelWave wave = TableManager.Instance.GetTableItem<LevelWave>(101);
Monster monster = TableManager.Instance.GetTableItem<Monster>(5);
```

**场景 6：特质查询——`TraitBehaviour.Find<T>()`**

```csharp
// 同一个玩家身上挂了多个 TraitBehaviour 子类
// 不用泛型 —— 用字符串或者手动遍历
TraitBehaviour trait = GetComponent("TraitBerserk");  // 字符串拼错运行时才报错

// 用了泛型 —— 编译期安全
public static T Find<T>() where T : TraitBehaviour
{
    return FindObjectOfType<T>();  // ✅ 运行时 T 是具体类型
}

// 调用
var berserk = TraitBehaviour.Find<TraitBerserk>();
berserk.killCount;  // ✅ IDE 补全，强类型
```

### 1.4 泛型的底层原理

泛型既不是重载（Overload）也不是重写（Override），是 **"运行时按需实例化"（Reification）**。

这三个概念经常被混在一起问，先区分清楚：

| 概念 | 谁决定的 | 什么时候决定 | 例子 |
|---|---|---|---|
| **重载 Overload** | 编译器 | 编译时，看参数类型/个数选方法 | `Add(int)`, `Add(float)` — 方法名相同但签名不同 |
| **重写 Override** | 运行时 | 运行时，看实际对象类型走子类版本 | `animal.Speak()` — 是狗就叫"汪"，是猫就叫"喵" |
| **泛型实例化** | JIT/AOT 编译器 | 首次调用时，按类型参数生成具体实现 | `List<int>` 和 `List<float>` 各自生成机器码 |

**C# 泛型的工作方式**：编译器把泛型代码当成模板留着，JIT 在第一次碰到 `List<int>` 时现场生成一份针对 `int` 的实现；碰到 `List<float>` 时再生成一份针对 `float` 的。第一次调用有一瞬间的 JIT 开销，之后就是原生速度。

**值类型和引用类型的差异是关键**：

```csharp
// 值类型：每种类型都生成独立的机器码
// 因为 int 4 字节、double 8 字节、struct 大小各不相同
List<int>    → 生成一份 int 版本的机器码
List<float>  → 生成一份 float 版本的机器码
List<double> → 生成一份 double 版本的机器码

// 引用类型：所有引用类型共享同一份机器码
// 因为 string、Enemy、object 在内存里都是 4/8 字节的指针
List<string> → ┐
List<Enemy>  → ├─ 共享同一份代码
List<object> → ┘
```

为什么要知道这个？面试官问"泛型会不会导致代码膨胀"，你就答：**值类型会，引用类型不会**。C++ 模板是所有类型都展开，C# 泛型引用类型共享一份，比 C++ 模板省内存。

**跟 C++ 模板的关键区别**：

| | C# 泛型 | C++ 模板 |
|---|---|---|
| 展开时机 | JIT 运行时按需 | 编译器编译时全展开 |
| 引用类型 | 共享一份代码 | 每种都展开，就算类型差不多 |
| 约束支持 | `where T : new()` / `class` / 具体类型 | C++20 Concepts（类似，但晚了几十年） |
| 编译产物 | IL 中间码 + 运行时代码 | 每个 .cpp 都展开成大量机器码 |

---

### 1.5 泛型约束对比

```csharp
// C# — 丰富的约束
where T : class          // 引用类型
where T : struct         // 值类型
where T : new()          // 有无参构造
where T : MonoBehaviour  // 必须是某类的子类
where T : IDamageable    // 必须实现某接口
```

```typescript
// TypeScript — 用 extends，但只有编译时检查
<T extends { new(): any }>    // 对应 where T : new()
<T extends MonoBehaviour>     // 对应子类约束（但运行时可能不是真的）
<T extends IDamageable>       // 对应接口约束

// ❌ 没有 struct/class 区分（TS 没有值类型概念）
```

### 1.6 面试回答（30 秒版）

> C# 泛型是运行时的——`List<int>` 在运行时系统真的知道它是 int 的列表，所以可以 `typeof(T)`、可以 `new T()`。TypeScript 泛型是编译时的——TS 编译成 JS 后 T 就消失了，系统只知道是个 Array，不知道里面装的什么类型。这是最根本的区别，其他语法层面的差异都是从这个点衍生出来的。

---

## 二、类型系统

| | C# | TypeScript |
|---|---|---|
| 类型存在时机 | 运行时 + 编译时 | 仅编译时（运行时擦除） |
| 类型强制 | 强类型，不能隐式把 int 赋给 string | 编译时检查，运行时照 JS 规则走 |
| `any` / `dynamic` | `dynamic` 关键字，运行时绑定 | `any` 关掉类型检查（TS 特色，C# 没有对应物） |
| 值类型 | ✅ 有（int, float, struct） | ❌ 没有，一切都是对象 |
| `null` vs `undefined` | 只有 `null` | 两个都有（JS 遗产） |
| 可选参数安全 | 编译期 + 运行时有值 | 编译期检查，运行时可能是 `undefined` |

### 2.1 ref / out / in：C# 独有的参数修饰符

这三个是 TypeScript 完全没有的概念，面试时容易被问"C# 有哪些 TS 没有的特性"。

**一句话总结**：

| 修饰符 | 能读吗 | 必须写吗 | 用在哪 |
|---|---|---|---|
| **ref** | ✅ 能读 | 传入前必须初始化 | 既要读旧值又要改新值 |
| **out** | ❌ 不能读（方法内必须赋值） | 方法内必须赋值 | 只当返回值用 |
| **in** | ✅ 能读 | ❌ 不能改（编译报错） | 传大 struct 省内存，语义上保证不修改 |

**为什么要有这三个东西——先理解传值和传引用**

C# 默认是**传值**：调用方法时把变量的值拷贝一份，方法里改的是拷贝，不影响原变量。

```csharp
void AddOne(int x) { x = x + 1; }

int hp = 100;
AddOne(hp);          // 传的是 100 这个值的拷贝
Debug.Log(hp);       // 还是 100，原变量没变
```

**ref** —— 传引用，可以读写：

```csharp
void AddOne(ref int x) { x = x + 1; }  // ref → 改的是原变量

int hp = 100;
AddOne(ref hp);      // 传的是 hp 本身，不是拷贝
Debug.Log(hp);       // 101，原变量被改了
```

`ref` 要求传入前变量已经初始化——因为它会读当前值（`x = x + 1` 读了 `x`）。

**out** —— 传引用，但只写不读（纯输出）：

```csharp
bool TryParse(string input, out int result)
{
    if (int.TryParse(input, out result))
        return true;     // result 被赋值了
    result = 0;          // 必须赋值！
    return false;
}

// 调用
if (TryParse("123", out int number))
    Debug.Log(number);   // 123 — 不用提前声明 number
```

`out` 的特点是：方法内**必须**给参数赋值（每个代码路径都要赋），调用方**不需要**提前初始化——因为方法不会读它的旧值。

**Unity 里最常见的 out 场景**：

```csharp
// Raycast 的命中信息
if (Physics.Raycast(ray, out RaycastHit hit))
{
    Debug.Log(hit.point);  // 命中点
}

// Dictionary 的安全取值
if (dict.TryGetValue(key, out var value))
{
    // value 拿到了
}

// 对象池取组件
public static T Get<T>(T prefab, Vector3 pos, Quaternion rot, out bool fromPool)
    where T : Component
{
    // ...
    fromPool = true;
    return result;
}
```

**in** —— 传引用，但只读（省内存 + 语义保护）：

```csharp
// 大 struct 传值 → 在栈上拷贝 64 字节 → 浪费
void Process(BigStruct data) { ... }       // 传值：拷贝一份

// 加了 in → 传引用（传指针），零拷贝 + 编译器保证不修改
void Process(in BigStruct data) { ... }    // 传引用：零拷贝
    data.field = 5;  // ❌ 编译报错：in 参数不能修改
```

`in` 的核心场景是**大 struct**。C# 的 struct 传参默认是值拷贝——32 字节的 struct 就算了，128 字节往上的 struct 每调用一次就拷贝一次，堆栈压力大。加了 `in` → 只传 8 字节指针 → 零拷贝 + 但编译器保证你不会误改原数据。

**三个一起对比**：

```csharp
// 同一个场景，三种写法

// 默认传值 —— 拷贝开销大，不修改原变量
void UpdateStats(PlayerStats stats) { stats.hp += 10; }  // 原变量不变

// ref —— 无拷贝，可读写
void UpdateStats(ref PlayerStats stats) { stats.hp += 10; }  // 原变量被改

// in —— 无拷贝，只读（编译器保护）
void UpdateStats(in PlayerStats stats) 
{
    stats.hp += 10;  // ❌ 编译报错
    Debug.Log(stats.hp);  // ✅ 能读
}
```

**为什么 TypeScript 没有这些**：

TS/JS 的所有对象（包括数组、函数）都是引用类型，本来就是传引用的——`function(arr)` 里 `arr.push(1)` 会修改原数组。基本类型（number, string, boolean）在 JS 里是不可变的——不存在「传指针直接改原变量」这个需求。所以 `ref`/`out`/`in` 这三个在 JS 生态里没有对应物。

---

## 三、类与继承

### 3.1 基本一致的部分

```csharp
// C#
class Animal {
    protected string name;
    public Animal(string name) { this.name = name; }
    public virtual void Speak() { }
}

class Dog : Animal {
    public Dog(string name) : base(name) { }
    public override void Speak() => Console.WriteLine("汪");
}
```

```typescript
// TypeScript — 几乎一样的语法
class Animal {
    protected name: string;
    constructor(name: string) { this.name = name; }
    speak(): void { }
}

class Dog extends Animal {  // TS 用 extends，C# 用 :
    constructor(name: string) { super(name); }  // TS 用 super，C# 用 base
    speak(): void { console.log("汪"); }
}
```

### 3.2 关键差异

| | C# | TypeScript |
|---|---|---|
| 继承关键字 | `:` | `extends` |
| 父类调用 | `base` | `super` |
| 方法重写 | 需要 `virtual` + `override` | 默认就能 override |
| 访问修饰符 | `public/private/protected/internal` | `public/private/protected`（没有 internal） |
| 抽象类 | `abstract class` | 一样 |
| 接口 | `interface` | 一样 |
| 多继承 | ❌ 不支持（单继承 + 多接口） | ❌ 不支持 |
| **属性** | ✅ 有 `get/set` 属性 | ✅ 有 — 但编译后变成函数调用 |

### 3.3 深入：virtual / override / abstract（面向对象核心）

这是面试高频区。三个概念解决的是同一个核心问题：**基类定义行为框架，子类填充具体实现**。

**virtual（虚方法）**：基类提供默认实现，子类**可选**重写。

```csharp
class Enemy {
    public virtual void Die()
    {
        // 默认死亡：播动画 → 等 1 秒 → 销毁
        PlayDeathAnimation();
        Destroy(gameObject, 1f);
    }
}

class Boss : Enemy {
    public override void Die()
    {
        // Boss 死亡：先震屏 → 回头调基类的死亡逻辑 → 额外掉落
        CameraShake();
        base.Die();  // ← 调用基类的 Die()，复用默认逻辑
        SpawnLoot();
    }
}

class SmallEnemy : Enemy {
    // 不 override —— 直接用基类的 Die()，默认行为够用
}
```

**abstract（抽象方法）**：基类不提供实现，子类**必须**重写。

```csharp
abstract class Skill {
    public abstract void Execute(Transform target);  // 没有函数体！子类必须实现
}

class FireSkill : Skill {
    public override void Execute(Transform target) { /* 火焰逻辑 */ }
}

class IceSkill : Skill {
    public override void Execute(Transform target) { /* 冰冻逻辑 */ }
}

// 如果 new Skill() → 编译报错！抽象类不能实例化
```

**virtual vs abstract 一句话区分**：

| | virtual | abstract |
|---|---|---|
| 基类有没有实现 | ✅ 有默认实现 | ❌ 完全没有 |
| 子类是必须重写 | ❌ 可选 | ✅ 必须 |
| 能 new 吗 | ✅ 能 | ❌ 不能（类不能实例化） |
| 什么时候用 | 有通用行为，但允许子类定制 | 只有行为签名，具体逻辑全交给子类 |

**运行时怎么找到正确的方法——虚方法表（vtable）**：

```csharp
Enemy enemy = new Boss();    // 编译时类型是 Enemy，实际类型是 Boss
enemy.Die();                  // 调用哪个 Die()？
                              // 运行时查 vtable → 找到 Boss.Die() → 执行 Boss 的版本
```

底层原理：每个有虚方法的类都有一个虚方法表（Virtual Method Table），存着"这个方法实际指向哪个实现"。`override` 就是把子类的方法地址写进去，替换掉基类的默认地址。

```
Enemy 的 vtable:            Boss 的 vtable:
  Die → Enemy.Die (0x1000)    Die → Boss.Die (0x2000)  ← 被 override 替换了
  Move → Enemy.Move           Move → Enemy.Move         ← 没 override，还是基类的

enemy.Die() → 查 vtable → 找到 0x2000 → 执行 Boss.Die()
```

**C# vs TypeScript 的关键区别**：

```csharp
// C# —— 必须显式声明 virtual + override，不声明就不能重写
class Animal {
    public void Sleep() { }           // 没 virtual → 不能重写
    public virtual void Speak() { }   // 有 virtual → 可以重写
}

class Dog : Animal {
    public override void Speak() { }  // ✅ 基类声明了 virtual
    // public override void Sleep() { }  // ❌ 编译报错：基类没声明 virtual
}
```

```typescript
// TypeScript —— 所有方法默认都是"virtual"的，直接 override
class Animal {
    sleep(): void { }
    speak(): void { }
}

class Dog extends Animal {
    speak(): void { console.log("汪"); }  // ✅ 直接重写，不需要 virtual/override 关键字
    sleep(): void { }                      // ✅ 也能重写
}
```

C# 的设计哲学是"默认封闭，显式开放"——不想让别人重写的就不加 virtual，防止子类乱改。TS/JS 是"默认开放"——任何方法都能被子类覆盖。

### 3.4 重载（Overload）：同名不同参

重载跟继承没关系——它是在**同一个类**里，定义多个同名方法，参数列表不同，编译器根据调用时的传参选正确的版本。

```csharp
// 同一个类里，同名不同参
class MathUtils {
    public int Add(int a, int b)           => a + b;          // int
    public float Add(float a, float b)     => a + b;          // float
    public Vector3 Add(Vector3 a, Vector3 b) => a + b;        // Vector3
    public string Add(string a, string b)  => a + b;          // 字符串拼接
}

// 调用 —— 编译器根据参数类型自动选
math.Add(3, 5);           // → int 版本
math.Add(3.0f, 5.0f);    // → float 版本
math.Add("hello", "world"); // → string 版本
```

**为什么要有重载**：不用给每种类型起不同的名字（`AddInt`、`AddFloat`、`AddVector`），编译器帮你选。

**TypeScript 没有重载语法**，但可以通过联合类型实现类似效果：

```typescript
// TS 没有真正的重载，用联合类型 + 类型守卫模拟
function add(a: number, b: number): number;
function add(a: string, b: string): string;
function add(a: number | string, b: number | string): number | string {
    if (typeof a === "number" && typeof b === "number") return a + b;
    if (typeof a === "string" && typeof b === "string") return a + b;
    throw new Error("不支持的类型");
}
// 上面两行只是类型声明，真正的实现就一个函数体
```

如果面试官问"C# 的重载底层是怎么实现的"，答案是**编译器的名字修饰（Name Mangling）**——编译器在 IL 代码里给每个重载生成不同的内部名字，比如 `Add` 变成 `Add_int32_int32`、`Add_float32_float32`。调用时根据参数类型选对应的符号。

### 3.5 三概念速查

| | Overload 重载 | Override 重写 | Virtual/Abstract |
|---|---|---|---|
| 在哪 | 同一个类 | 子类 | 基类 |
| 时机 | 编译时决定 | 运行时决定（vtable） | 定义时声明 |
| 干什么 | 同名方法，不同参数 | 替换基类行为 | 声明"子类可以/必须改我" |
| 必要条件 | 参数列表不同即可 | 基类必须有 virtual | — |
| C# 特有 | 支持运算符重载 | 必须显式 virtual+override | struct 不能 virtual |
| TS 对应 | ❌ 联合类型模拟 | ✅ 默认支持 | ❌ 没有 virtual 概念 |

---

## 四、异步编程

### 4.1 语法层面很接近

```csharp
// C#
async Task<string> FetchData()
{
    var data = await HttpClient.GetStringAsync(url);
    return data;
}
```

```typescript
// TypeScript
async function fetchData(): Promise<string> {
    const data = await fetch(url).then(r => r.text());
    return data;
}
```

### 4.2 本质差异

| | C# | TypeScript |
|---|---|---|
| 底层 | 有状态机 + 线程池，真正多线程 | 单线程事件循环（Event Loop），永不阻塞 |
| `Task` vs `Promise` | `Task<T>` 可以不跑在后台线程 | `Promise<T>` 一定是异步回调 |
| 取消 | `CancellationToken` | `AbortController`（浏览器） |
| 同步等异步结果 | `task.Result`（不推荐但能做） | ❌ 完全做不到，单线程会死锁 |

---

## 五、内存与 GC

| | C# | TypeScript |
|---|---|---|
| 内存分区 | 栈（值类型）+ 堆（引用类型） | 只有堆，没有栈分配 |
| GC | 分代 GC（Gen0/1/2） | V8 的分代 GC（新生代/老生代），浏览器不同策略不同 |
| 值类型优势 | struct 在栈上，0 GC 压力 | ❌ 不存在，全在堆上 |
| 手动控制 | `using` / `Dispose` 模式 | ❌ 靠 GC，无确定性析构 |
| 内存占用 | struct 比 class 省得多 | 所有东西都是对象，开销大 |

---

## 六、委托 vs 回调

### 6.1 先搞清楚委托是什么——用最直白的方式

**委托就是一个变量，但这个变量装的是函数，不是数据。**

```csharp
int hp = 100;              // 普通变量：装数据
Action<Enemy> handler;     // 委托变量：装函数（接收 Enemy 参数，返回 void）
```

跟 `int` 对比着看就懂了：

```
int x = 5;                 → x 是个装整数的盒子，里面现在放的是 5
Action f = SomeMethod;     → f 是个装函数的盒子，里面现在放的是 SomeMethod
x = 10;                    → 盒子里换成 10
f = AnotherMethod;         → 盒子里换成 AnotherMethod
调用 x → 拿到 10
调用 f() → 执行 AnotherMethod
```

**所以委托做的事情就是：把函数当参数传。**

```csharp
// 没有委托的话，想传一个"比较规则"进去就很麻烦
void Sort(int[] arr, ??? 比较规则) { ... }

// 有了委托——
void Sort<T>(T[] arr, Func<T, T, int> comparer)
{
    // comparer(a, b) 返回负数 → a < b
    // comparer(a, b) 返回 0     → a == b  
    // comparer(a, b) 返回正数 → a > b
}

// 调用时可以传任何匹配签名的函数
Sort(items, (a, b) => a.hp.CompareTo(b.hp));  // 按血量排
Sort(items, (a, b) => a.name.CompareTo(b.name)); // 按名字排
```

**函数现在可以像 int 一样传来传去——这就是委托。**

### 6.2 四种写法（从啰嗦到简洁）

同一个需求"定义一个能接收 `int, int` 返回 `bool` 的委托"，C# 有四种写法：

```csharp
// 方式一：传统 delegate 声明（最啰嗦，老项目常见）
delegate bool CompareDelegate(int a, int b);
CompareDelegate f1 = (a, b) => a > b;

// 方式二：Func<...> 泛型委托（最常用）
Func<int, int, bool> f2 = (a, b) => a > b;
//  ↑ 最后一个类型参数永远是返回值，前面的是参数

// 方式三：Action<...>（无返回值的 Func）
Action<int, int> f3 = (a, b) => Debug.Log($"{a} + {b} = {a+b}");
//  ↑ 没有最后一个返回类型，因为 Action 永远返回 void

// 方式四：Lambda 直接传参（最简洁，LINQ 里到处都是）
items.Where(x => x.hp > 0);
items.OrderBy(x => x.name);
```

**Func 和 Action 速查**：

```
Action         → void()          无参无返回
Action<int>    → void(int)       一个参无返回
Action<T1,T2>  → void(T1,T2)    两个参无返回（最多 16 个参数）

Func<int>          → int()             无参返回 int
Func<string, int>  → int(string)      一个参返回 int
Func<T1,T2,bool>   → bool(T1,T2)      ← 最后一个才是返回类型！
```

### 6.3 多播委托——一个委托可以挂多个函数

**这是 C# 委托跟 JS 回调最大的区别之一。**

```csharp
Action onEnemyDied = null;

// 加订阅
onEnemyDied += PlayDeathSound;    // 敌人死了 → 播音效
onEnemyDied += SpawnPickup;       // 敌人死了 → 掉落物品
onEnemyDied += UpdateKillCount;   // 敌人死了 → 计分板 +1

// 调用一次 → 三个函数依次执行
onEnemyDied?.Invoke();  // PlayDeathSound → SpawnPickup → UpdateKillCount

// 减订阅
onEnemyDied -= PlayDeathSound;    // 不想播音效了，只去掉这一个
```

**底层原理（面试可能会问）**：

委托内部维护了一个**调用链（Invocation List）**，是一个链表。`+=` 往链尾追加，`-=` 移除一个，`Invoke` 时依次遍历调用：

```
onEnemyDied._invocationList:
  [PlayDeathSound] → [SpawnPickup] → [UpdateKillCount] → null
       ↑
  _target → _methodPtr（每个节点记录 "哪个对象" + "哪个方法"）

onEnemyDied -= PlayDeathSound:
  [SpawnPickup] → [UpdateKillCount] → null
```

### 6.4 event 关键字——防止外部"误杀"

```csharp
// 不加 event —— 外部可以随意覆盖
public Action OnDeath;          // 外部代码可以 OnDeath = null 清空所有订阅

// 加了 event —— 外部只能 += 和 -=，不能 = 赋值
public event Action OnDeath;    // 外部代码 OnDeath = null → 编译报错
```

`event` 不改变委托的任何功能，只加了一层访问控制——**自己内部**可以 Invoke，**外部**只能加减订阅。

### 6.5 闭包陷阱——委托 + Lambda 的常见坑

```csharp
// ❌ 经典 Bug：循环里创建的 Lambda 捕获了同一个变量
for (int i = 0; i < 3; i++)
{
    buttons[i].onClick.AddListener(() => Debug.Log(i));
}
// 三个按钮点了都打印 3 —— 因为 lambda 捕获的是变量 i 本身，不是当时的快照
// 点击时循环早就跑完了，i 已经是 3

// ✅ 在循环体内拷贝一份
for (int i = 0; i < 3; i++)
{
    int captured = i;  // 每次循环都是新的局部变量
    buttons[i].onClick.AddListener(() => Debug.Log(captured));
}
// 三个按钮分别打印 0, 1, 2
```

TS/JS 里有完全一样的坑：

```typescript
// ❌ 同样的问题
for (var i = 0; i < 3; i++) {
    buttons[i].onClick = () => console.log(i);  // 全是 3
}

// ✅ let 是块作用域，天然解决（比 C# 方便）
for (let i = 0; i < 3; i++) {
    buttons[i].onClick = () => console.log(i);  // 0, 1, 2
}
```

### 6.6 跟 TypeScript 的本质区别

| | C# 委托 | TypeScript 函数/回调 |
|---|---|---|
| 多播（一个调用多个函数） | ✅ 内置（`+=` / `-=`），线程安全 | ❌ 手动实现观察者模式 |
| 类型安全的多播 | ✅ `event` 关键字限制外部访问 | ❌ 数组可以被外部清空或覆盖 |
| 返回值聚合 | 多播时返回最后一个订阅者的结果 | 不存在这个概念 |
| 调用空委托 | `?.Invoke()` 安全 | `handlers?.forEach()` 或手判空 |
| 内置泛型类型 | `Action<>` / `Func<>` 16 个重载 | 手写类型别名 `type Fn = (...) => T` |
| 运行时类型信息 | ✅ 可以 `.Method`, `.Target` 反射 | ❌ 函数就是函数，没有元数据 |
| 底层 | 调用链链表 + `MulticastDelegate` | 就是一个回调数组 |

### 6.7 面试回答（30 秒版）

> 委托变量装的不是数据是函数——把函数当参数传。C# 内置了 Action 和 Func 泛型委托，不用每次都声明。最关键的特性是多播——一个 `event` 可以用 `+=` 挂多个订阅者，调用时按顺序执行，`-=` 移除某个订阅。内部是调用链链表实现。TypeScript 没有这个，要手动维护回调数组。Unity 里 `UnityAction`、`Button.onClick` 底层都是委托。

```csharp
// C# — 委托 + 事件
public delegate void OnDeathHandler(Enemy enemy);
public event OnDeathHandler OnDeath;

// 支持多播：一个事件可以挂多个方法
OnDeath += HandleDeath;
OnDeath?.Invoke(enemy);

// 内置泛型委托
Action<int, string>           // void(int, string)
Func<int, string, bool>       // bool(int, string)
```

```typescript
// TypeScript — 回调函数 + 观察者
type OnDeathHandler = (enemy: Enemy) => void;

// 手动管理回调列表（或者用 EventEmitter）
class EventEmitter {
    private handlers: OnDeathHandler[] = [];
    on(handler: OnDeathHandler) { this.handlers.push(handler); }
    emit(enemy: Enemy) { this.handlers.forEach(h => h(enemy)); }
}

// ❌ 没有多播委托、没有 Action/Func 内置类型
// ✅ 但有更灵活的闭包和函数是一等公民
```

---

## 七、枚举

```csharp
// C# 枚举 — 本质是具名整数
enum SkillType { Auto = 0, Lightning = 1, Beam = 2 }
int val = (int)SkillType.Auto;  // ✅ 0
```

```typescript
// TS 枚举 — 分数字枚举和字符串枚举
enum SkillType { Auto, Lightning, Beam }    // 数字枚举，跟 C# 一样
// 编译后 → 生成反向映射对象 { 0: "Auto", Auto: 0, ... }

enum SkillType { Auto = "auto", Lightning = "lightning" }  // 字符串枚举，C# 没有

// ⚠️ TS 枚举编译后会生成额外的 JS 对象，有运行时开销
// 很多 Cocos 项目用 const enum 或联合类型代替来减体积
const enum SkillType { Auto, Lightning }  // 编译后完全内联，零运行时开销
type SkillType = "auto" | "lightning";    // 联合类型替代方案
```

---

## 八、实际工作中的差异

### 8.1 Unity C# 里习以为常、Cocos TS 里不存在的

| C# / Unity | TypeScript / Cocos 替代 |
|---|---|
| `ScriptableObject`（数据容器） | JSON 配置文件 / Prefab + 属性 |
| `[SerializeField]` Inspector 拖拽绑定 | `@property` 装饰器，编辑器面板绑定 |
| `Coroutine`（协程） | `async/await` + `Promise` / Cocos 的 `scheduler` |
| `struct`（栈分配、0 GC） | ❌ 不存在，Cocos 的性能优化靠对象池 |
| `ref / out` 参数 | ❌ 不存在，用返回对象代替 |
| `using` 语句（确定性释放） | ❌ 不存在，靠 try-finally |
| 编译期严格类型检查 | 编译期有检查，但 `any` 能绕过 |

### 8.2 Cocos TS 里方便、Unity C# 里麻烦的

| TypeScript / Cocos | C# / Unity |
|---|---|
| 热更新 | 原生支持（JS 解释执行），`require("新文件")` 即可 | 需要 HybridCLR 补解释器，复杂得多 |
| 快速原型 | 改代码 → 刷新浏览器 → 秒出结果 | 改代码 → Unity 编译 → 等 5-30 秒 |
| JSON 处理 | `JSON.parse/stringify` 内置 | 需要库（JsonUtility / Newtonsoft） |
| 函数式编程 | `.map().filter().reduce()` 原生 | LINQ 能做但 IL2CPP 下有分配开销 |
| 动态类型 | `any` + 运行时鸭子类型，灵活 | `dynamic` 但性能差，一般不推荐 |

---

## 九、面试回答策略

**如果面试官问"你主要写 C#，TS 熟不熟？"**

> 两种都写过。之前在中青宝做《御剑三国》就是 Cocos Creator + TS 技术栈，TypeScript 泛型、装饰器、模块化开发都有实际经验。C# 和 TS 的面向对象语法 80% 相似——类、继承、接口、泛型、async/await 概念都一样。最大的区别是 C# 泛型是运行时的，TS 的编译完就擦除了；C# 有值类型和 struct，TS 全在堆上。切换成本对我来说很低。

**如果追问"你觉得哪种语言更好？"**

> 各有优势。C# 的类型系统更严谨，值类型和 struct 对游戏性能优化帮助很大。但 TS 在 Cocos 里的热更新天然优势是 C# 比不了的——Unity 要上 HybridCLR 才能做到的事，Cocos 替换一个 JS 文件就搞定了。选择主要取决于引擎而不是语言本身。

---

## 十、快速查阅卡

| 概念 | C# | TypeScript |
|---|---|---|
| 泛型运行时保留 | ✅ | ❌（擦除） |
| 值类型 | ✅ struct | ❌ |
| 多播委托 | ✅ delegate/event | ❌（自己实现） |
| LINQ / 函数式 | ✅（IL2CPP 下慎用） | ✅ map/filter/reduce |
| async/await | ✅ | ✅ |
| 确定性析构 | ✅ using/Dispose | ❌ |
| 协程 | ✅ IEnumerator | ✅ async/Promise |
| 热更新友好度 | 低（需 HybridCLR） | 高（原生支持） |
| 启动速度 | 慢（编译） | 快（解释/刷新） |
