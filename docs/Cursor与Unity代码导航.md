# Cursor 与 Unity 代码导航配置指南

面向：**Unity 2021.3.x（本仓库示例：2021.3.22f1c1）+ Cursor**  
目标：在 Cursor 中对 C# 脚本使用 **跳转定义**、**查找所有引用**、**符号大纲** 等 IDE 能力。

> 说明：这些能力依赖 **C# 语言服务（OmniSharp）** 和 Unity 生成的 **`.sln` / `.csproj`**，不是额外「引用查找插件」。若根目录没有 `.sln`，`F12` / `Shift+F12` 常会失效，只能用全文搜索（`Ctrl+Shift+F`）兜底。

---

## 一、Cursor 常用快捷键（Windows）

| 功能 | 快捷键 |
|------|--------|
| 转到定义 | **F12** 或 **Ctrl + 左键** |
| 预览定义（不离开当前文件） | **Alt + F12** |
| 查找所有引用 | **Shift + F12** |
| 当前文件内符号（类 / 方法 / 属性） | **Ctrl + Shift + O** |
| 整个工作区符号 | **Ctrl + T** |
| 全局文本搜索（语义引用不可用时的兜底） | **Ctrl + Shift + F** |

命令面板（**Ctrl + Shift + P**）可搜索：`Go to Definition`、`References: Find All References`。

右键菜单：**转到定义**、**查找所有引用**。

---

## 二、Cursor 扩展

在 Cursor 扩展市场安装：

| 扩展 | ID | 说明 |
|------|-----|------|
| **C#** | `ms-dotnettools.csharp` | OmniSharp，Unity 项目常用，**建议必装** |
| **Unity**（可选） | `visualstudiotoolsforunity.vstuc` | 与 Unity 编辑器联动 |

安装后执行：**Developer: Reload Window**（重载窗口）。

若同时安装 **C# Dev Kit** 且跳转/引用异常，可尝试只保留 **C#** 扩展，避免冲突。

---

## 三、Unity：External Tools 与生成工程文件

### 3.1 打开 External Tools（不是 Project Settings）

**Windows 菜单路径：**

```
Edit（编辑） → Preferences…（首选项…） → 左侧 External Tools
```

注意：

- **不要**进入 `Edit → Project Settings`（那是项目设置）。
- **Regenerate project files** 按钮在 **External Tools** 页面下方。

### 3.2 推荐设置

1. **External Script Editor**  
   - 选 **Visual Studio Code**，或 **Browse…** 指向 Cursor，例如：  
     `C:\Users\<用户名>\AppData\Local\Programs\cursor\Cursor.exe`

2. **Generate .csproj files for:**（能勾尽量勾）  
   - Embedded packages  
   - Local packages  
   - Registry packages  
   - Git packages（若有）

3. 点击 **Regenerate project files**。

**等价操作**（也会重新生成并打开外部编辑器）：

```
Assets → Open C# Project
```

### 3.3 生成后根目录应出现的文件

在**与 `Assets` 同级**的工程根目录（如 `G:\Demo\GongkaoMower\`）应看到例如：

- `<项目名>.sln`（名称通常与文件夹名一致）
- `Assembly-CSharp.csproj`
- `Assembly-CSharp-Editor.csproj`
- 以及其它程序集对应的 `.csproj`（若有）

生成成功后：

1. **关闭** Cursor。  
2. 用 **文件 → 打开文件夹** 打开**整个工程根目录**（含 `Assets`、`ProjectSettings`、`.sln`），不要只打开 `Assets/Script` 子目录。  
3. 右下角若提示选择项目，选 **Assembly-CSharp**。  
4. 必要时：**Ctrl + Shift + P** → `OmniSharp: Restart OmniSharp`。

---

## 四、Unity Package Manager（生成失败时）

**Window → Package Manager → Unity Registry**，确认已安装 IDE 集成包之一：

| 包名 | 用途 |
|------|------|
| **Visual Studio Code Editor**（`com.unity.ide.vscode`） | 配合 Cursor / VS Code |
| **Visual Studio Editor**（`com.unity.ide.visualstudio`） | 配合 Visual Studio |

安装后回到 **Edit → Preferences → External Tools**，再次 **Regenerate project files**。

---

## 五、常见问题

| 现象 | 可能原因 | 处理 |
|------|----------|------|
| 根目录没有 `.sln` | 未在 Unity 中 Regenerate；或打开的不是项目根 | 按第三节操作；检查 **Console** 是否有红色报错 |
| `Shift+F12` 无结果，但 `Ctrl+Shift+F` 能搜到 | 语言服务未索引工程 | 确认 `.sln` 存在；重装/启用 C# 扩展；Restart OmniSharp |
| 跳转跳到元数据 / 不正确 | 未选对项目 | 右下角选 **Assembly-CSharp** |
| 刚改完脚本不生效 | OmniSharp 缓存 | 等待数秒或 Restart OmniSharp |
| 脚本大面积编译错误 | Unity 编译失败 | 先在 Unity **Console** 修到可编译，再 Regenerate |

---

## 六、与 Unity 自带能力对比

| 能力 | Cursor（配置完成后） | 说明 |
|------|----------------------|------|
| 跳转定义 / 查引用 | F12 / Shift+F12 | 需 `.sln` + C# 扩展 |
| 改代码、AI 辅助 | 主战场 | 本仓库日常开发推荐 |
| Unity 内搜脚本 | Project 窗口搜索 | 无跨文件「引用列表」时不如 IDE |

---

## 七、快速检查清单

- [ ] Unity：**Edit → Preferences → External Tools** 已设置 Cursor / VS Code  
- [ ] 已点击 **Regenerate project files** 或 **Assets → Open C# Project**  
- [ ] 工程根目录存在 `.sln` 与 `Assembly-CSharp.csproj`  
- [ ] Cursor 打开的是**含 `.sln` 的根文件夹**  
- [ ] 已安装 **C#** 扩展（`ms-dotnettools.csharp`）  
- [ ] 在 `.cs` 文件中 **F12**、**Shift+F12** 可用  

---

*文档版本：与 Unity 2021.3.22f1c1 菜单路径对齐；若升级 Unity 大版本，External Tools 界面可能略有变化。*
