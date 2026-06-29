using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 成就系统核心逻辑（全局单例）。
/// - 从 TableManager 加载 AchievementConfig 配置
/// - 订阅 EventBus 跟踪进度
/// - 提供 UI 查询接口
/// - 通过 PlayerProfileService 持久化进度
/// </summary>
public class AchievementService
{
    private static AchievementService _instance;
    public static AchievementService Instance => _instance ??= new AchievementService();

    /// <summary>成就组（运行时组装，供 UI 使用）。</summary>
    public struct Group
    {
        public int groupId;
        public int taskType;
        public int taskParam;
        public int sortOrder;
        public int currentValue;
        public List<StageInfo> stages;
    }

    /// <summary>单个阶段信息。</summary>
    public struct StageInfo
    {
        public int id, stage, targetValue, rewardId, rewardCount;
        public string description;
        public bool isUnlocked, isCompleted, isClaimed;
    }

    private List<object> _configRows;
    private List<Group> _groups;
    private bool _inited;
    private bool _dirtyGroups;

    /// <summary>本局战斗内技能突破到满级的次数（每局重置）。</summary>
    private int _battleSkillMaxCount;
    /// <summary>本局已计数的技能 ID，防止同一技能重复计数（如羁绊被动解锁后再次突破）。</summary>
    private readonly HashSet<SkillId> _battleMaxedSkillIds = new HashSet<SkillId>();

    private AchievementService() { }

    /// <summary>确保在任何场景加载前完成初始化（新号直进战斗也能跟踪进度）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Instance.Init();
    }

    // ── 初始化 ──

    /// <summary>初始化：加载配置、加载进度、订阅事件（幂等）。</summary>
    public void Init()
    {
        if (_inited) return;
        _inited = true;

        LoadConfig();
        SubscribeEvents();
        _dirtyGroups = true;
    }

    private void LoadConfig()
    {
        _configRows = new List<object>();

#if USE_FB_TABLE
        var dict = TableManager.Instance?.GetTable<ProtoTable.AchievementConfig>();
        if (dict == null)
        {
            Debug.LogWarning("[AchievementService] AchievementConfig 表未加载，请确认已运行 xls转cs 并生成 .bytes。");
            return;
        }

        foreach (var kv in dict)
        {
            if (kv.Value is ProtoTable.AchievementConfig row && row.ID > 0)
                _configRows.Add(row);
        }

        _configRows.Sort((a, b) =>
        {
            var ra = (ProtoTable.AchievementConfig)a;
            var rb = (ProtoTable.AchievementConfig)b;
            int cmp = ra.GroupId.CompareTo(rb.GroupId);
            if (cmp != 0) return cmp;
            return ra.Stage.CompareTo(rb.Stage);
        });
#endif

        Debug.Log($"[AchievementService] 加载 {_configRows.Count} 条成就配置行。");
    }

    private void SubscribeEvents()
    {
        EventBus.Subscribe<DiamondEarnedEvent>(OnDiamondEarned);
        EventBus.Subscribe<DiamondSpentEvent>(OnDiamondSpent);
        EventBus.Subscribe<ChapterClearedEvent>(OnChapterCleared);
        EventBus.Subscribe<CharacterUnlockedEvent>(OnCharacterUnlocked);
        EventBus.Subscribe<SkillMaxLevelReachedEvent>(OnSkillMaxLevelReached);
        EventBus.Subscribe<StarEarnedEvent>(OnStarEarned);
        EventBus.Subscribe<HeroLevelUpEvent>(OnHeroLevelUp);
        EventBus.Subscribe<HeroStageUpEvent>(OnHeroStageUp);
        EventBus.Subscribe<GoldEarnedEvent>(OnGoldEarned);
        EventBus.Subscribe<GoldSpentEvent>(OnGoldSpent);
    }

    // ── 登录天数 ──

    /// <summary>进入 Home 时调用，检测登录天数并更新进度。</summary>
    public void OnEnterHome()
    {
        var data = PlayerProfileService.Instance?.Data;
        if (data == null) return;

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (data.lastLoginDate == today) return;

        data.lastLoginDate = today;
        PlayerProfileService.Instance.MarkDirtyAndSave();
        UpdateProgressForTaskType(8, 1); // TaskType=8: LoginDays
    }

    // ── 事件处理 ──

    /// <summary>战斗结算时记录击杀数（一次性计入，不走逐怪事件）。</summary>
    public void RecordKills(int count)
    {
        UpdateProgressForTaskType(1, count);
    }

    private void OnDiamondEarned(DiamondEarnedEvent e)
    {
        UpdateProgressForTaskType(6, e.amount);
    }

    private void OnDiamondSpent(DiamondSpentEvent e)
    {
        UpdateProgressForTaskType(7, e.amount);
    }

    private void OnChapterCleared(ChapterClearedEvent e)
    {
        if (!e.isFirstClear) return;
        // TaskType=2: ChapterClear, 按 levelId 过滤
        UpdateProgressForTaskTypeFiltered(2, 1, e.levelId);
    }

    private void OnCharacterUnlocked(CharacterUnlockedEvent e)
    {
        // TaskType=3: CharacterUnlock（不按具体角色过滤，TaskParam 配置为 0）
        UpdateProgressForTaskType(3, 1);
    }

    private void OnSkillMaxLevelReached(SkillMaxLevelReachedEvent e)
    {
        // TaskType=5: SkillMaxLevel —— 只统计主动技能突破，被动技能不计
        if (e.isPassive) return;
        if (!_battleMaxedSkillIds.Add(e.skillId)) return; // 去重：同一技能本局只计一次
        _battleSkillMaxCount++;
    }

    private void OnStarEarned(StarEarnedEvent e)
    {
        // TaskType=9: StarTotal（累计星星数）
        UpdateProgressForTaskType(9, e.stars);
    }

    private void OnHeroLevelUp(HeroLevelUpEvent e)
    {
        // TaskType=10: HeroLevelUpTotal（角色累计升级次数）
        UpdateProgressForTaskType(10, 1);
    }

    private void OnHeroStageUp(HeroStageUpEvent e)
    {
        // TaskType=11: HeroStageUpTotal（角色累计升阶次数）
        UpdateProgressForTaskType(11, 1);
    }

    private void OnGoldEarned(GoldEarnedEvent e)
    {
        // TaskType=12: GoldEarnTotal（金币累计获得）
        UpdateProgressForTaskType(12, e.amount);
    }

    private void OnGoldSpent(GoldSpentEvent e)
    {
        // TaskType=13: GoldSpendTotal（金币累计消耗）
        UpdateProgressForTaskType(13, e.amount);
    }

    // ── 进度更新 ──

    /// <summary>更新所有匹配 TaskType 的成就进度（不按 TaskParam 过滤）。</summary>
    private void UpdateProgressForTaskType(int taskType, int delta)
    {
        if (_configRows == null || _configRows.Count == 0) return;
        if (delta <= 0) return;

        var data = PlayerProfileService.Instance?.Data;
        if (data == null) return;

        var progress = data.achievementProgress ?? Array.Empty<AchievementProgressEntry>();
        var updated = new System.Collections.Generic.HashSet<int>();
        bool changed = false;

#if USE_FB_TABLE
        for (int i = 0; i < _configRows.Count; i++)
        {
            var row = (ProtoTable.AchievementConfig)_configRows[i];
            if ((int)row.TaskType != taskType) continue;
            if (!updated.Add(row.GroupId)) continue; // 同一 GroupId 只加一次
            progress = AddProgressValue(progress, row.GroupId, delta);
            changed = true;
        }
#endif

        if (changed)
        {
            data.achievementProgress = progress;
            PlayerProfileService.Instance.MarkDirtyAndSave();
            _dirtyGroups = true;
            EventBus.Publish(new PlayerDataChangedEvent());
        }
    }

    /// <summary>更新匹配 TaskType 且 TaskParam 匹配的成就进度（int 参数版，用于 ChapterClear）。</summary>
    private void UpdateProgressForTaskTypeFiltered(int taskType, int delta, int taskParam)
    {
        if (_configRows == null || _configRows.Count == 0) return;
        if (delta <= 0) return;

        var data = PlayerProfileService.Instance?.Data;
        if (data == null) return;

        var progress = data.achievementProgress ?? Array.Empty<AchievementProgressEntry>();
        var updated = new System.Collections.Generic.HashSet<int>();
        bool changed = false;

#if USE_FB_TABLE
        for (int i = 0; i < _configRows.Count; i++)
        {
            var row = (ProtoTable.AchievementConfig)_configRows[i];
            if ((int)row.TaskType != taskType) continue;
            // TaskParam==0 匹配任意；否则必须相等
            if ((int)row.TaskParam != 0 && (int)row.TaskParam != taskParam) continue;
            if (!updated.Add(row.GroupId)) continue; // 同一 GroupId 只加一次
            progress = AddProgressValue(progress, row.GroupId, delta);
            changed = true;
        }
#endif

        if (changed)
        {
            data.achievementProgress = progress;
            PlayerProfileService.Instance.MarkDirtyAndSave();
            _dirtyGroups = true;
            EventBus.Publish(new PlayerDataChangedEvent());
        }
    }

    /// <summary>给指定 Group 的 currentValue 增加 delta，自动扩张数组。返回（可能变更后的）数组。</summary>
    private static AchievementProgressEntry[] AddProgressValue(AchievementProgressEntry[] progress, int groupId, int delta)
    {
        for (int i = 0; i < progress.Length; i++)
        {
            if (progress[i] != null && progress[i].groupId == groupId)
            {
                progress[i].currentValue += delta;
                return progress;
            }
        }

        // 新条目
        Array.Resize(ref progress, progress.Length + 1);
        progress[progress.Length - 1] = new AchievementProgressEntry
        {
            groupId = groupId,
            currentValue = delta,
            claimedStage = 0,
        };
        return progress;
    }

    // ── UI 查询 ──

    /// <summary>获取所有成就组（含进度和阶段状态）。首次调用或 dirty 时重建。</summary>
    public List<Group> GetAchievementGroups()
    {
        if (!_inited) Init();
        if (_dirtyGroups) RebuildGroups();
        return _groups ?? new List<Group>();
    }

    private void RebuildGroups()
    {
        _groups = new List<Group>();
        if (_configRows == null || _configRows.Count == 0) return;

        var progress = PlayerProfileService.Instance?.Data?.achievementProgress
                       ?? Array.Empty<AchievementProgressEntry>();

        int currentGroupId = -1;
        Group currentGroup = default;

#if USE_FB_TABLE
        for (int i = 0; i < _configRows.Count; i++)
        {
            var row = (ProtoTable.AchievementConfig)_configRows[i];
            int gid = (int)row.GroupId;

            if (gid != currentGroupId)
            {
                if (currentGroupId > 0)
                {
                    SortStages(currentGroup.stages);
                    _groups.Add(currentGroup);
                }

                currentGroupId = gid;
                int claimed = GetClaimedStage(progress, gid);

                currentGroup = new Group
                {
                    groupId = gid,
                    taskType = (int)row.TaskType,
                    taskParam = (int)row.TaskParam,
                    sortOrder = (int)row.SortOrder,
                    currentValue = GetProgressValue(progress, gid),
                    stages = new List<StageInfo>(),
                };
            }

            int stage = (int)row.Stage;
            int claimedStage = GetClaimedStage(progress, currentGroupId);
            bool isUnlocked = stage == 1 || claimedStage >= stage - 1;

            // 只展示已解锁的阶段
            if (!isUnlocked) continue;

            bool isCompleted = currentGroup.currentValue >= (int)row.TargetValue;
            bool isClaimed = claimedStage >= stage;

            currentGroup.stages.Add(new StageInfo
            {
                id = (int)row.ID,
                stage = stage,
                description = row.Description ?? "",
                targetValue = (int)row.TargetValue,
                rewardId = (int)row.RewardId,
                rewardCount = (int)row.RewardCount,
                isUnlocked = true,
                isCompleted = isCompleted,
                isClaimed = isClaimed,
            });
        }

        if (currentGroupId > 0)
        {
            SortStages(currentGroup.stages);
            _groups.Add(currentGroup);
        }
#endif

        // 全局排序：可领取优先 → 进行中 → 已领取，同状态按 sortOrder
        _groups.Sort((a, b) =>
        {
            int pa = GetGroupPriority(a);
            int pb = GetGroupPriority(b);
            if (pa != pb) return pa.CompareTo(pb);
            return a.sortOrder.CompareTo(b.sortOrder);
        });
        _dirtyGroups = false;
    }

    private static int GetProgressValue(AchievementProgressEntry[] progress, int groupId)
    {
        for (int i = 0; i < progress.Length; i++)
            if (progress[i] != null && progress[i].groupId == groupId)
                return progress[i].currentValue;
        return 0;
    }

    private static int GetClaimedStage(AchievementProgressEntry[] progress, int groupId)
    {
        for (int i = 0; i < progress.Length; i++)
            if (progress[i] != null && progress[i].groupId == groupId)
                return progress[i].claimedStage;
        return 0;
    }

    /// <summary>组内阶段排序：可领取 → 进行中 → 已领取。</summary>
    private static void SortStages(List<StageInfo> stages)
    {
        if (stages == null) return;
        stages.Sort((a, b) =>
        {
            static int GetPriority(StageInfo s)
            {
                if (s.isClaimed) return 2;
                if (s.isCompleted) return 0; // 可领取
                return 1; // 进行中
            }
            return GetPriority(a).CompareTo(GetPriority(b));
        });
    }

    /// <summary>取 Group 最高优先级：0=有可领取, 1=进行中, 2=全部已领取, 3=无阶段。</summary>
    private static int GetGroupPriority(Group g)
    {
        if (g.stages == null || g.stages.Count == 0) return 3;
        int best = 2;
        for (int i = 0; i < g.stages.Count; i++)
        {
            var s = g.stages[i];
            if (s.isClaimed) continue;      // 已领取，继续找更好的
            if (s.isCompleted) return 0;    // 有可领取 → 最高优
            best = Mathf.Min(best, 1);      // 进行中
        }
        return best;
    }

    // ── 领取 ──

    /// <summary>检查指定阶段是否可领取。</summary>
    public bool CanClaim(int groupId, int stage)
    {
        var groups = GetAchievementGroups();
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].groupId != groupId) continue;
            for (int j = 0; j < groups[i].stages.Count; j++)
            {
                var s = groups[i].stages[j];
                if (s.stage == stage)
                    return s.isUnlocked && s.isCompleted && !s.isClaimed;
            }
        }
        return false;
    }

    /// <summary>领取指定阶段的奖励。返回 (success, rewardId, rewardCount)。</summary>
    public (bool success, int rewardId, int rewardCount) Claim(int groupId, int stage)
    {
        if (!CanClaim(groupId, stage))
            return (false, 0, 0);

        var data = PlayerProfileService.Instance?.Data;
        if (data == null) return (false, 0, 0);

        var progress = data.achievementProgress ?? Array.Empty<AchievementProgressEntry>();

        int idx = -1;
        for (int i = 0; i < progress.Length; i++)
        {
            if (progress[i] != null && progress[i].groupId == groupId) { idx = i; break; }
        }
        if (idx < 0) return (false, 0, 0);

        int rewardId = 0, rewardCount = 0;
#if USE_FB_TABLE
        for (int i = 0; i < _configRows.Count; i++)
        {
            var row = (ProtoTable.AchievementConfig)_configRows[i];
            if ((int)row.GroupId == groupId && (int)row.Stage == stage)
            {
                rewardId = (int)row.RewardId;
                rewardCount = (int)row.RewardCount;
                break;
            }
        }
#endif

        if (rewardId <= 0 || rewardCount <= 0) return (false, 0, 0);

        progress[idx].claimedStage = Mathf.Max(progress[idx].claimedStage, stage);
        data.achievementProgress = progress;

        PlayerProfileService.Instance.AddItem(rewardId, rewardCount);
        PlayerProfileService.Instance.MarkDirtyAndSave();
        _dirtyGroups = true;

        // 通知 HUD 刷新货币显示
        EventBus.Publish(new PlayerDataChangedEvent());

        Debug.Log($"[AchievementService] 领取成就 groupId={groupId} stage={stage} → itemId={rewardId} ×{rewardCount}");
        return (true, rewardId, rewardCount);
    }

    /// <summary>战斗结束时调用：将本局技能满级次数写入峰值（取 max）。</summary>
    public void FinalizeBattle()
    {
        int count = _battleSkillMaxCount;
        _battleSkillMaxCount = 0;
        _battleMaxedSkillIds.Clear();
        if (count <= 0) return;

        UpdateProgressMax(5, count);
    }

    /// <summary>取峰值更新（Mathf.Max）——用于单局最优记录型任务。</summary>
    private void UpdateProgressMax(int taskType, int value)
    {
        if (_configRows == null || _configRows.Count == 0) return;
        if (value <= 0) return;

        var data = PlayerProfileService.Instance?.Data;
        if (data == null) return;

        var progress = data.achievementProgress ?? Array.Empty<AchievementProgressEntry>();
        var updated = new System.Collections.Generic.HashSet<int>();
        bool changed = false;

#if USE_FB_TABLE
        for (int i = 0; i < _configRows.Count; i++)
        {
            var row = (ProtoTable.AchievementConfig)_configRows[i];
            if ((int)row.TaskType != taskType) continue;
            if (!updated.Add(row.GroupId)) continue;

            progress = SetProgressMax(progress, row.GroupId, value);
            changed = true;
        }
#endif

        if (changed)
        {
            data.achievementProgress = progress;
            PlayerProfileService.Instance.MarkDirtyAndSave();
            _dirtyGroups = true;
            EventBus.Publish(new PlayerDataChangedEvent());
        }
    }

    /// <summary>将指定 Group 的 currentValue 设为 max(currentValue, value)。</summary>
    private static AchievementProgressEntry[] SetProgressMax(AchievementProgressEntry[] progress, int groupId, int value)
    {
        for (int i = 0; i < progress.Length; i++)
        {
            if (progress[i] != null && progress[i].groupId == groupId)
            {
                if (value > progress[i].currentValue)
                    progress[i].currentValue = value;
                return progress;
            }
        }

        Array.Resize(ref progress, progress.Length + 1);
        progress[progress.Length - 1] = new AchievementProgressEntry
        {
            groupId = groupId,
            currentValue = value,
            claimedStage = 0,
        };
        return progress;
    }

    public void MarkDirty()
    {
        _dirtyGroups = true;
    }
}
