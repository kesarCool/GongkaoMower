# Git 分支操作速查

> 场景：主分支 `main` 保持提审状态（冻结），新功能在 `feat/xxx` 分支开发。审核通过或打回后按对应流程操作。

---

## 当前分支结构

```
main (冻结 — 提审1.1.10 + 少量修改)
  └── feat/new-feature (新功能开发)
```

---

## 一、创建功能分支

| | 操作 |
|---|---|
| **TortoiseGit** | 工程目录空白处右键 → **TortoiseGit → Create Branch**<br>Name 填 `feat/new-feature`<br>Base On 选 `HEAD` 或 `main`<br>勾选 **Switch to new branch** → OK |
| **命令行** | `git checkout -b feat/new-feature` |

---

## 二、切换分支

| | 操作 |
|---|---|
| **TortoiseGit** | 右键 → **TortoiseGit → Switch/Checkout**<br>Branch 下拉选目标分支 → OK |
| **命令行** | `git checkout feat/new-feature`<br>`git checkout main` |

---

## 三、日常提交

### 提交（Commit）

| | 操作 |
|---|---|
| **TortoiseGit** | 右键 → **Git Commit → "分支名"**<br>勾选要提交的文件<br>填写 Message → **Commit** |
| **命令行** | `git add .`<br>`git commit -m "做了什么"` |

### 推送到远端（Push）

| | 操作 |
|---|---|
| **TortoiseGit** | 右键 → **TortoiseGit → Push**<br>Remote 选 `origin`，Destination 选目标分支 → OK |
| **命令行** | `git push origin feat/new-feature`<br>（首次：`git push -u origin feat/new-feature`） |

### 拉取远端（Pull）

| | 操作 |
|---|---|
| **TortoiseGit** | 右键 → **TortoiseGit → Pull**<br>Remote 选 `origin`，勾 Auto-load Putty Key（如有） → OK |
| **命令行** | `git pull origin main` |

---

## 四、把 main 的修复同步到功能分支

> 场景：审核打回，在 main 上修完 bug，修复需要带到功能分支。

| | 操作 |
|---|---|
| **TortoiseGit** | 1. 先切到 `feat/new-feature`（Switch/Checkout）<br>2. 右键 → **TortoiseGit → Merge**<br>3. Branch 选 `main` → OK<br>4. 无冲突则完成；有冲突 → 跳到「解决冲突」 |
| **命令行** | `git checkout feat/new-feature`<br>`git merge main` |

---

## 五、审核通过：功能分支合并回 main

| 步骤 | TortoiseGit | 命令行 |
|------|-------------|--------|
| ① 确认功能分支干净 | 右键 → **TortoiseGit → Check for modifications**，应无未提交文件 | `git status` |
| ② 切到 main | Switch/Checkout → 选 `main` | `git checkout main` |
| ③ 拉最新 main | Pull → Remote 选 `origin` → OK | `git pull origin main` |
| ④ 合并 | 右键 → **TortoiseGit → Merge**<br>Branch 选 `feat/new-feature` → OK | `git merge feat/new-feature` |
| ⑤ 推远端 | Push → Destination 选 `main` → OK | `git push origin main` |
| ⑥ 删除功能分支（可选） | Switch/Checkout → 点分支右侧 `...` → **Delete Branch** | `git branch -d feat/new-feature` |

---

## 六、解决冲突

### 可视化合并（推荐）

> 合并时弹冲突 → 冲突文件上右键 → **TortoiseGit → Resolve**

打开 **TortoiseGitMerge**，左右两栏对照：
- 左侧 = 当前分支（你所在的）
- 右侧 = 合并来源分支
- 底部 = 合并结果

逐行点选要保留哪一侧，或用工具栏按钮。改完后 **Save** 退出，冲突文件自动标记为 resolved。

### 手改冲突标记

冲突文件里长这样：

```
<<<<<<< HEAD
main 分支的代码
=======
feat/new-feature 的代码
>>>>>>> feat/new-feature
```

删掉 `<<<<<<<` / `=======` / `>>>>>>>` 三行标记，保留最终版本，保存。

### 完成合并

| | 操作 |
|---|---|
| **TortoiseGit** | Resolve 后右键 → **Git Commit → "分支名"**，Message 保留默认合并信息 → Commit |
| **命令行** | `git add .`<br>`git commit`（会弹出编辑器，保存默认 merge message） |

### 反悔

| | 操作 |
|---|---|
| **TortoiseGit** | 关闭 TortoiseGitMerge，回到工程目录右键 → **TortoiseGit → Revert** |
| **命令行** | `git merge --abort` |

---

## 七、Tag 管理

| 操作 | TortoiseGit | 命令行 |
|------|-------------|--------|
| 创建 Tag | 右键 → **TortoiseGit → Create Tag**<br>Name 填 `v1.1.10-review`<br>Base On 点 `...` 选提审 commit → OK | `git tag v1.1.10-review 1728ee6` |
| 推送 Tag | Push → 勾选 **Include Tags** → OK | `git push origin v1.1.10-review` |
| 查看所有 Tag | Switch/Checkout → Branch 下拉框末尾展开 | `git tag` |
| 切到 Tag | Switch/Checkout → Branch 下拉选对应 tag | `git checkout v1.1.10-review` |

---

## 八、查看日志 / 历史

| | 操作 |
|---|---|
| **TortoiseGit** | 右键 → **TortoiseGit → Show log** |
| **命令行** | `git log --oneline --graph` |

> Show log 的图形化分支线比命令行直观很多，合并前先看一眼，心里有数。

---

## 九、紧急情况

| 情况 | TortoiseGit | 命令行 |
|------|-------------|--------|
| 切分支前忘了提交<br>改动带过去了 | 右键 → **TortoiseGit → Stash Save**<br>切分支后 → **Stash Pop** | `git stash` → 切分支 → `git stash pop` |
| 合并到一半想撤销 | Merge 窗口关掉，Resolve 时直接 Revert | `git merge --abort` |
| 在错误分支上提交了 | Switch 到正确分支 → Merge 那个错误 commit → 回到错误分支 Reset 掉 | `git cherry-pick <hash>` |
| 撤销最后一次提交<br>（未 push） | 右键 → **TortoiseGit → Reset**<br>Mode 选 **Soft** → OK | `git reset --soft HEAD~1` |
