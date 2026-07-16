# Git 工作流 · 面试备考

> Q14-Q18：Git 分支策略、merge vs rebase、Cocos 项目的版本控制陷阱、多人协作规范。
> 八年经验在这块的优势是"我在真实项目里推过规范、解决过冲突"，不是"我知道那条命令"。

---

## Q14：git merge 与 git rebase 区别

**面试官在考什么**：知不知道两者对历史记录的影响不同，以及团队协作中什么时候用 merge 什么时候用 rebase。

**一张图讲清楚**：

```
场景：从 main 拉了个 feature 分支，做了 3 个 commit。同时 main 往前走了一个 commit。

git merge main：

  main:     A---B---C---D
                 \       \
  feature:        E---F---G---M   ← 产生一个 Merge Commit

  → 历史保留了完整的分支轨迹，但多了一个 M 节点
  → "谁在什么时候从哪合了进来" 一目了然


git rebase main：

  main:     A---B---C---D
                           \
  feature:                  E'---F'---G'   ← 三个 commit 被"搬"到 D 之后

  → 历史是一条直线，干净
  → E/F/G 的 commit hash 全变了（因为 parent 变了）
  → 原来的分支轨迹完全消失
```

**什么时候用哪个**：

| 场景 | 用 merge | 用 rebase |
|---|---|---|
| 往 main 合 feature 分支 | ✅ `git merge feature` | ❌ 不要 rebase main |
| feature 分支同步 main 的最新代码 | ✅ 也行 | ✅ `git rebase main`（保持 feature 干净） |
| 已经 push 到远程的分支 | ✅ 安全 | ❌ rebase 后 push -f 会坑队友 |
| 个人本地 WIP 整理 | ❌ 产生无意义的 Merge Commit | ✅ `git rebase -i` 压缩 commit |

**黄金规则**：**不要 rebase 已经 push 过的分支**。因为 rebase 改写了 commit hash → 别人的本地分支还指向旧 commit → 下次 pull 时会冲突到怀疑人生。

**8 年该怎么说**：

> 团队规范是：feature 分支每天下班前 `git rebase main` 保持跟主干同步——这样解决了"最后一天合并时发现冲突堆成山"的问题。但合回 main 时用 `git merge --no-ff feature` 保留 Merge Commit——为了出事时能快速定位"这批代码是哪个 feature 引入的"。简单说就是日常 rebase 保持干净，最终合并 merge 保留轨迹。

---

## Q15：Cocos 项目里哪些该提交，哪些该忽略，.meta 能不能删

**面试官在考什么**：知不知道 Unity/Cocos 的 `.meta` 文件是 GUID 唯一标识——删了就断引用，不是普通的"缓存文件"。

**Cocos 项目 .gitignore 标配**：

```
# 必须忽略
library/          # 编译缓存，每个人的机器重新生成
temp/             # 临时文件
local/            # 本地配置
build/            # 构建产物
node_modules/     # npm 依赖（如果有）

# .meta 文件的处理
*.meta            # ❌ 绝对不能这样写！Cocos 的 .meta 必须提交

# Cocos 自动生成的文件
*.json.meta       # ✅ 必须提交——记录了每个 .json 的 GUID
*.png.meta        # ✅ 必须提交——记录了纹理导入设置
*.prefab.meta     # ✅ 必须提交——记录了 Prefab 的 GUID
```

**`.meta` 为什么不能删**：

Cocos Creator 内部用 UUID（GUID）来引用资源，而不是文件路径。`.meta` 文件里存了这个 UUID + 导入设置（纹理格式、图集配置等）。

```
场景（删除前）：
  scene.fire 引用 texture.png
  → Cocos 内部查 texture.png.meta → UUID = "a1b2c3..." → 找到了

场景（删除后）：
  你把 texture.png.meta 删了
  → Cocos 重新 Import texture.png → 生成新的 UUID = "x9y8z7..."
  → scene.fire 里记录的 UUID 还是 "a1b2c3..." → ❌ 找不到！资源丢失！
```

**`.meta` 必须提交到 Git**。团队成员签出代码 → 拿到一样的 `.meta` → 大家的 GUID 都是一致的 → 互相 checkout 场景不会丢引用。

**8 年该怎么说**：

> 团队里有个新同事以为 `.meta` 是缓存文件，在 `.gitignore` 里加了 `*.meta`，提交之后所有人的场景都 Missing Asset。花了两小时排查——最后在 reflog 里找到 `.gitignore` 的改动，回滚后重新生成所有人的 `.meta`。这之后在新人 Onboarding 文档里第一条就是"Cocos 项目里 `.meta` 文件是 GUID 唯一标识，必须提交，绝对不能删"。

---

## Q16：.scene / .prefab 多人冲突如何处理

**面试官在考什么**：知不知道 `.scene` 和 `.prefab` 是 JSON 文本——两个人同时改了同一个场景/Prefab 同一个节点，diff 会变成 JSON 地狱。知不知道怎么从流程上避免。

**为什么 .scene / .prefab 冲突难解**：

`.scene` 文件里同一个节点的数据散落在文件不同位置（节点的 transform 数据在 A 段，组件数据在 B 段）。两个人同时改同一个 Scene → JSON diff 出现几十处冲突 → 手改 JSON 修复跟拆炸弹一样。

**不是技术解法，是流程解法**：

| 策略 | 做法 |
|---|---|
| **拆分场景** | 大场景拆成多个子场景（Prefab 化）。每个子场景一个文件，独立提交 |
| **功能分区** | 战斗场景 → LevelDesigner 负责关卡布局、UI 组负责 HUD Canvas——不同的 Prefab |
| **锁定机制** | 改场景前在群里说一声"我在改 Home.fire"，其他人绕开 |
| **单人负责制** | 每个场景/Prefab 指定 Owner，别人只能读不能改 |
| **定期合并** | 有冲突早发现，不要攒一个月再合 |

**Cocos 3.x 的改善**：

Cocos 3.x 把 `.fire` 改成了更结构化的格式，冲突比 2.x 好解一些。另外 3.x 更加推崇模块化 Scene + Prefab 嵌套，客观上减少了单文件冲突。

**8 年该怎么说**：

> 在《御剑三国》项目里，Home 大厅场景是冲突重灾区——策划加按钮、美术调布局、程序接脚本都在同一个 `Home.fire` 上。后来做了两件事：第一，大厅拆成 6 个 Prefab——顶部资源条、底部导航栏、侧边任务栏等各自独立，每个人只改自己负责的 Prefab。第二，Introduce 了分支策略——所有 UI Prefab 改动单独开 `ui/xxx` 分支，合之前先 rebase main 检查冲突。`.scene` 冲突从每周 2-3 次降到了发布前才偶尔有一次。

---

## Q17：stash / cherry-pick / reset 的使用场景

**面试官在考什么**：知不知道这些命令不是"考试用"的，是日常开发的真实救命场景。

**stash ——"我写到一半要切分支修 Bug"**

```
正在 feature/a 上写了一半 → PM 说线上有个紧急 Bug 要马上修
  git stash               # 把工作区暂存到"草稿箱"
  git checkout main       # 切到主干
  # 修 Bug → commit → push
  git checkout feature/a  # 切回
  git stash pop           # 拿出来继续写
```

**cherry-pick ——"我只要这一个 commit，别的不要"**

```
main 上修了一个线上 Bug（commit A）
release/1.0 也需要这个修复
  但 main 比 release/1.0 多了很多新功能 commit，不能整个 merge

  git checkout release/1.0
  git cherry-pick A   # 只把 A 这一个 commit 摘过来
```

**reset ——"我提交了不该提交的东西"**

```bash
# soft：撤回 commit，改动回到暂存区（git add 状态）
git reset --soft HEAD~1    # "我刚才不应该 commit，但代码不想丢"

# mixed（默认）：撤回 commit + 暂存，改动回到工作区
git reset HEAD~1           # "commit 和 add 都撤了，改动留着"

# hard：撤回 commit + 暂存 + 工作区——全丢
git reset --hard HEAD~1    # "我刚才写的全是垃圾，全不要了"
```

**8 年该怎么说**：

> 这几个命令是日常高频。最常见的场景是在 feature 分支写到一半 PM 说线上 Bug → `stash` 暂存 → 切主干修 → `stash pop` 回来继续。`cherry-pick` 用在上线节奏不一样的多版本并行——master 修了个 Bug 要同步到 release/1.0 但不想要 master 上的新功能。`reset` 主要是自己误提交后回退——但 `reset --hard` 我只在本地用，远程分支走 `revert` 保留历史。

---

## Q18：团队分支管理模型（Git Flow / Trunk-based）

**面试官在考什么**：知不知道团队协作的分支策略不是随便起的，以及为什么有的团队用 Git Flow 有的用 Trunk-based。

**Git Flow（传统模型）**：

```
main ──────●───────────────●──────────── v2.0
            \              /
develop ────●────●────●───●────●────●───
              \    \   \       /
feature/a     ●───●    \     /
feature/b              ●───●
release/1.0                  ●──●──●
hotfix/urgent                       ●──●
```

**特点**：分支类型分得极细——main / develop / feature / release / hotfix，各有各的合入规则。发布周期长、多人并行开发的传统项目用这套。

**Trunk-based（主干开发）**：

```
main ──────●──●──●──●──●──●──●──●──
              /     /     /
feature/a   ●──●   /     /
feature/b         ●──●──●
feature/c               ●──●
```

**特点**：所有人往 main 合，分支生命周期极短（不超过一天）。依赖 Feature Flag 控制功能开关。CI/CD 要求高——每合一次就自动跑测试并部署。移动端游戏用这套的变体更多。

**游戏团队通常用变体**：

```
不纯粹是 Git Flow 也不纯粹是 Trunk-based：

main          ← 永远是"可以出包"的状态
├── feat/xxx  ← 日常开发，合入 main 前先 rebase main
├── release/x ← 发布分支，只修 Bug
└── hotfix/x  ← 紧急修复，cherry-pick 到 main 和所有 release 分支
```

**8 年该怎么说**：

> 在中青宝的时候推的是 Git Flow 的变体——main 保持可发布、日常开发在 feature 分支上。每周五是 Merge Day——所有 feature 合进 main 然后构建出 QA 包。跟标准 Git Flow 的区别是我们没有 develop 分支——只有 feature 和 main 两层，减少合并链路。热更阶段用 hotfix 分支，cherry-pick 同步到所有线上版本。执行下来最大的收益是上线节奏从两周一版提到了每周一版——因为合并冲突少了、回滚路径短了。
