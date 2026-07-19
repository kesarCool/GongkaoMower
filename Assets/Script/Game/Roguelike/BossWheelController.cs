using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 击杀转盘控制器：收集可升级技能、构建 8 槽位、权重抽选、发放奖励。
/// 由 <see cref="RoguelikeCardManager"/> 在 Boss 触发时调用 <see cref="StartWheel"/>。
/// </summary>
public class BossWheelController : MonoBehaviour
{
    [Header("技能目录")]
    [Tooltip("查技能定义（图标、名称、最大等级）")]
    public SkillCatalog skillCatalog;

    [Header("转盘配置")]
    [Tooltip("转盘槽位数量（默认 16）")]
    public int wheelSlotCount = 16;

    [Header("权重配置")]
    [Tooltip("主动技能在槽位抽选时的权重")]
    public float activeWeight = 3f;
    [Tooltip("被动技能在槽位抽选时的权重")]
    public float passiveWeight = 1f;

    [Header("中奖数量概率")]
    [Tooltip("抽中 1 张的概率")]
    [Range(0f, 1f)]
    public float win1Weight = 0.3f;
    [Tooltip("抽中 2 张的概率")]
    [Range(0f, 1f)]
    public float win2Weight = 0.5f;
    [Tooltip("抽中 3 张的概率")]
    [Range(0f, 1f)]
    public float win3Weight = 0.2f;

    /// <summary>防止 Boss 连杀两次重复触发。</summary>
    private bool _isWheelActive;
    private readonly Queue<System.Action> _pendingRequests = new Queue<System.Action>();

    private PlayerSkills _playerSkills;

    [Header("调试")]
    [Tooltip("Editor 下按此键直接触发转盘（用于测试 UI）。")]
    [SerializeField] private KeyCode debugTriggerKey = KeyCode.F5;

    private void Awake()
    {
        _playerSkills = GetComponent<PlayerSkills>();
        if (_playerSkills == null)
            _playerSkills = FindObjectOfType<PlayerSkills>();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(debugTriggerKey))
        {
            GameLog.Info("[BossWheel] Debug key pressed, triggering wheel for testing");
            DebugTriggerWheel();
        }
    }
#endif

    /// <summary>
    /// Editor 右键菜单：直接触发转盘（自动补测试技能）。
    /// </summary>
    [ContextMenu("Test: 触发转盘（自动补测试技能）")]
    public void DebugTriggerWheel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BossWheel] 请在运行模式下使用此功能。");
            return;
        }

        if (_playerSkills == null)
            _playerSkills = FindObjectOfType<PlayerSkills>();

        if (_playerSkills == null)
        {
            Debug.LogError("[BossWheel] 场景中无 PlayerSkills，无法测试转盘。");
            return;
        }

        // 兜底：确保至少有一个可升级技能（只补起始技能，不加新技能）
        EnsureMinimalTestSkills();

        StartWheel();
    }

    /// <summary>
    /// 兜底：角色一个技能都没有时，只补起始技能（不加额外技能，不污染玩家技能池）。
    /// </summary>
    private void EnsureMinimalTestSkills()
    {
        if (_playerSkills == null || skillCatalog == null) return;

        // 已有技能 → 不做任何改动
        if (_playerSkills.EquippedSkillCount > 0 || _playerSkills.EquippedPassiveCount > 0)
            return;

        // 完全没有技能 → 只补一个起始技能
        if (_playerSkills.startingSkill != SkillId.None && _playerSkills.HasEmptySlot)
        {
            _playerSkills.TryAddSkill(_playerSkills.startingSkill);
            GameLog.Info("[BossWheel] 兜底补起始技能用于测试");
        }
    }

    /// <summary>
    /// 入口：由 <see cref="RoguelikeCardManager"/> 在 Boss 触发时调用。
    /// </summary>
    public void StartWheel()
    {
        GameLog.Info("[BossWheel] StartWheel enter");

        if (_playerSkills == null)
        {
            GameLog.Warning("[BossWheel] PlayerSkills is null, abort");
            return;
        }

        if (_playerSkills.AllSlotsFullAndMaxLevel)
        {
            GameLog.Info("[BossWheel] All skills maxed, skip wheel");
            // 直接结束选卡流程，让波次继续
            EventBus.Publish(new CardSelectionEndedEvent());
            return;
        }

        if (_isWheelActive)
        {
            GameLog.Info("[BossWheel] Wheel already active, queued");
            _pendingRequests.Enqueue(() => StartWheel());
            return;
        }

        _isWheelActive = true;

        try
        {
            // 1. 收集可升级技能
            var pool = CollectUpgradableSkills();
            if (pool.Count == 0)
            {
                GameLog.Info("[BossWheel] No upgradable skills, skip");
                EndWheel();
                return;
            }

            // 2. 构建 8 槽位
            var slots = BuildWheelSlots(pool, wheelSlotCount);

            // 3. 决定中奖数量
            int winCount = DetermineWinCount(pool.Count);

            // 4. 选中奖槽位
            var winningIndices = PickWinningSlots(slots, winCount);

            GameLog.Info($"[BossWheel] 槽位数={slots.Length} 中奖数={winCount} 中奖=[{string.Join(",", winningIndices)}]");

            // 5. 打开转盘面板
            OpenWheelPanel(slots, winningIndices);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[BossWheel] StartWheel failed: " + ex);
            EndWheel();
        }
    }

    /// <summary>
    /// 收集所有可升级技能：主动 + 被动，且未满级。
    /// </summary>
    private List<WheelSlotData> CollectUpgradableSkills()
    {
        var result = new List<WheelSlotData>();
        if (_playerSkills == null || skillCatalog == null) return result;

        // 主动技能
        var activeIds = new List<SkillId>(5);
        _playerSkills.GetEquippedSkillIdsOrdered(activeIds);
        foreach (var id in activeIds)
        {
            if (id == SkillId.None) continue;
            if (_playerSkills.IsMaxLevel(id)) continue;

            var def = skillCatalog.Get(id);
            int currentLv = _playerSkills.GetSkillLevel(id);
            int maxLv = _playerSkills.GetEffectiveMaxLevel(id, def);

            result.Add(new WheelSlotData
            {
                skillId = id,
                def = def,
                currentLevel = currentLv,
                targetLevel = currentLv + 1,
                isActive = true,
                weight = activeWeight,
            });
        }

        // 被动技能
        var passiveIds = new List<SkillId>(5);
        _playerSkills.GetEquippedPassiveIdsOrdered(passiveIds);
        foreach (var id in passiveIds)
        {
            if (id == SkillId.None) continue;
            if (_playerSkills.IsPassiveMaxLevel(id)) continue;

            var def = skillCatalog.Get(id);
            int currentLv = _playerSkills.GetPassiveSkillLevel(id);

            result.Add(new WheelSlotData
            {
                skillId = id,
                def = def,
                currentLevel = currentLv,
                targetLevel = currentLv + 1,
                isActive = false,
                weight = passiveWeight,
            });
        }

        GameLog.Info($"[BossWheel] CollectUpgradableSkills: active={activeIds.Count} passive={passiveIds.Count} upgradable={result.Count}");
        return result;
    }

    /// <summary>
    /// 从池中构建指定数量的槽位，不足时随机重复补位（加权）。
    /// </summary>
    private WheelSlotData[] BuildWheelSlots(List<WheelSlotData> pool, int slotCount)
    {
        var slots = new WheelSlotData[slotCount];

        // 按权重排序（高权重优先放入，保证不同技能尽量先各占一位）
        var shuffled = new List<WheelSlotData>(pool);
        ShuffleByWeight(shuffled);

        int i = 0;
        // 先用去重技能填
        for (; i < slotCount && i < shuffled.Count; i++)
        {
            slots[i] = shuffled[i];
        }

        // 不够则从池中加权随机补
        for (; i < slotCount; i++)
        {
            slots[i] = WeightedPick(pool);
        }

        return slots;
    }

    /// <summary>
    /// 按权重决定中奖数量：1/2/3 张。
    /// </summary>
    private int DetermineWinCount(int upgradableCount)
    {
        float total = win1Weight + win2Weight + win3Weight;
        if (total <= 0f) return 1;

        float roll = Random.value * total;

        int count;
        if (roll < win1Weight)
            count = 1;
        else if (roll < win1Weight + win2Weight)
            count = 2;
        else
            count = 3;

        // 如果可升级种类只有 1 种，强制 1 张
        if (upgradableCount <= 1) count = 1;
        else if (upgradableCount == 2) count = Mathf.Min(count, 2);

        return count;
    }

    /// <summary>
    /// 从 8 槽位中不放回加权抽取中奖槽位下标。
    /// </summary>
    private int[] PickWinningSlots(WheelSlotData[] slots, int count)
    {
        int n = Mathf.Min(count, slots.Length);
        var result = new int[n];

        // 构建带索引的临时池
        var pool = new List<KeyValuePair<int, float>>(slots.Length);
        for (int i = 0; i < slots.Length; i++)
            pool.Add(new KeyValuePair<int, float>(i, slots[i].weight));

        for (int pick = 0; pick < n && pool.Count > 0; pick++)
        {
            float totalWeight = 0f;
            for (int j = 0; j < pool.Count; j++)
                totalWeight += pool[j].Value;

            float roll = Random.value * totalWeight;
            float accum = 0f;
            int chosenIdx = 0;

            for (int j = 0; j < pool.Count; j++)
            {
                accum += pool[j].Value;
                if (roll <= accum)
                {
                    chosenIdx = j;
                    break;
                }
            }

            result[pick] = pool[chosenIdx].Key;
            pool.RemoveAt(chosenIdx);
        }

        // 按下标顺时针排序：保证动画从第一个中奖卡顺时针走到后续
        System.Array.Sort(result);

        return result;
    }

    /// <summary>
    /// 发放奖励：逐张升级中奖技能。
    /// </summary>
    public void ApplyRewards(WheelSlotData[] slots, int[] winningIndices)
    {
        if (_playerSkills == null) return;

        foreach (var idx in winningIndices)
        {
            if (idx < 0 || idx >= slots.Length) continue;
            var slot = slots[idx];

            if (slot.isActive)
            {
                bool ok = _playerSkills.TryLevelUp(slot.skillId);
                GameLog.Info($"[BossWheel] Reward: active skill {slot.skillId} Lv.{slot.currentLevel}→{slot.targetLevel}, success={ok}");
            }
            else
            {
                bool ok = _playerSkills.TryLevelUpPassive(slot.skillId);
                GameLog.Info($"[BossWheel] Reward: passive skill {slot.skillId} Lv.{slot.currentLevel}→{slot.targetLevel}, success={ok}");
            }
        }
    }

    /// <summary>
    /// 转盘流程结束（面板关闭或异常），恢复战斗。
    /// </summary>
    public void EndWheel()
    {
        GameLog.Info("[BossWheel] EndWheel");
        _isWheelActive = false;

        // 处理排队请求
        if (_pendingRequests.Count > 0)
        {
            var next = _pendingRequests.Dequeue();
            // 延迟一帧避免连续 timeScale 切换
            StartCoroutine(DelayedNextWheel(next));
            return;
        }

        EventBus.Publish(new CardSelectionEndedEvent());
    }

    private System.Collections.IEnumerator DelayedNextWheel(System.Action next)
    {
        yield return new WaitForSecondsRealtime(0.3f);
        if (!_playerSkills.AllSlotsFullAndMaxLevel)
            next?.Invoke();
        else
            EventBus.Publish(new CardSelectionEndedEvent());
    }

    private void OpenWheelPanel(WheelSlotData[] slots, int[] winningIndices)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("[BossWheel] UIManager.Instance is null, cannot open wheel panel");
            // 无 UI 时直接发奖 + 结束
            ApplyRewards(slots, winningIndices);
            EndWheel();
            return;
        }

        var payload = new BossWheelOpenPayload
        {
            Slots = slots,
            WinningIndices = winningIndices,
            OnSpinComplete = () =>
            {
                ApplyRewards(slots, winningIndices);
            },
            OnClose = () =>
            {
                EndWheel();
            }
        };

        var opts = new UiOpenOptions
        {
            PauseTime = true,
            UseUnscaledTime = true,
            CloseOnBack = false,
        };

        UIManager.Instance.Open<BossWheelPanel>(payload, opts);
    }

    #region 工具方法

    /// <summary>加权随机抽取一份数据（复制值，非引用）。</summary>
    private static WheelSlotData WeightedPick(List<WheelSlotData> pool)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
            total += pool[i].weight;

        float roll = Random.value * total;
        float accum = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            accum += pool[i].weight;
            if (roll <= accum)
                return pool[i];
        }

        return pool[pool.Count - 1];
    }

    /// <summary>加权打乱：权重大者优先排前面，同时保留随机性。</summary>
    private static void ShuffleByWeight(List<WheelSlotData> list)
    {
        // Fisher-Yates 变体，权重越大越靠前
        var temp = new List<WheelSlotData>(list);
        list.Clear();

        while (temp.Count > 0)
        {
            float total = 0f;
            for (int i = 0; i < temp.Count; i++) total += temp[i].weight;
            float roll = Random.value * total;
            float accum = 0f;
            int picked = 0;
            for (int i = 0; i < temp.Count; i++)
            {
                accum += temp[i].weight;
                if (roll <= accum) { picked = i; break; }
            }
            list.Add(temp[picked]);
            temp.RemoveAt(picked);
        }
    }

    #endregion
}

/// <summary>
/// 转盘单个槽位数据。
/// </summary>
public class WheelSlotData
{
    public SkillId skillId;
    public SkillDefinitionBase def;
    public int currentLevel;
    public int targetLevel;
    public bool isActive;
    public float weight;
}

/// <summary>
/// Boss 转盘面板打开参数。
/// </summary>
public class BossWheelOpenPayload
{
    public WheelSlotData[] Slots;
    public int[] WinningIndices;
    public System.Action OnSpinComplete;
    public System.Action OnClose;
}
