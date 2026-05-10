using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 肉鸽卡池/抽卡单例：与 PlayerSkills 解耦，集中处理
/// 能量事件、无 UI 自动抽选、无卡组时的占位升级。
/// </summary>
public class RoguelikeCardManager : MonoBehaviour
{
    public static RoguelikeCardManager Instance { get; private set; }

    [Header("数据")]
    [Tooltip("卡组（ScriptableObject，内含 SkillCatalog/解锁表/抽卡规则）")]
    public CardDeck cardDeck;

    [Header("卡池：当前关卡（硬性解锁用）")]
    [SerializeField] private int _currentLevel = 1001;

    [Header("场景引用（可自动查找）")]
    [SerializeField] private CardSelectionSystem cardSelectionSystem;

    // 无卡组时占位：按槽位轮询升级
    private int _placeholderCursor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (cardSelectionSystem == null)
            cardSelectionSystem = FindObjectOfType<CardSelectionSystem>(true);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<CardSelectionTriggeredEvent>(OnCardSelectionTriggered, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CardSelectionTriggeredEvent>(OnCardSelectionTriggered);
    }

    public int CurrentLevel
    {
        get => _currentLevel;
        set => _currentLevel = value;
    }

    private void OnCardSelectionTriggered(CardSelectionTriggeredEvent e)
    {
        var ps = FindPlayerSkills();
        if (ps == null)
        {
            Debug.LogWarning("[RoguelikeCardManager] No PlayerSkills in scene");
            return;
        }

        if (ps.AllSlotsFullAndMaxLevel)
        {
            Debug.Log("[RoguelikeCardManager] All skills maxed, skip");
            return;
        }

        if (cardSelectionSystem == null)
            cardSelectionSystem = FindObjectOfType<CardSelectionSystem>(true);

        // 有选卡 UI 流程：交给 CardSelectionSystem
        if (cardSelectionSystem != null)
        {
            cardSelectionSystem.BeginSelectionFromManager();
            return;
        }

        // 无 UI：有卡组用卡池规则自动抽 1 次（优先本组件上的 CardDeck，其次 CardSelectionSystem 上的 deck）
        var deckRef = cardDeck != null ? cardDeck : cardSelectionSystem != null ? cardSelectionSystem.deck : null;
        if (deckRef != null)
        {
            ApplyDrawWithoutUI(ps);
            return;
        }

        // 无卡组：占位轮询升级
        ApplyPlaceholder(ps);
    }

    /// <summary>
    /// 供选卡系统调用：与 CardDeck 里逻辑一致
    /// </summary>
    public List<CardDeck.DrawResult> DrawFromPool(int level, PlayerSkills playerSkills, List<SkillId> exclude)
    {
        if (playerSkills == null)
            return new List<CardDeck.DrawResult>();

        CardDeck deckToUse = cardDeck;
        if (deckToUse == null && cardSelectionSystem != null)
            deckToUse = cardSelectionSystem.deck;

        if (deckToUse == null)
            return new List<CardDeck.DrawResult>();

        return deckToUse.Draw(level, playerSkills, exclude);
    }

    public PlayerSkills FindPlayerSkills()
    {
        return FindObjectOfType<PlayerSkills>();
    }

    private void ApplyDrawWithoutUI(PlayerSkills ps)
    {
        // 原 PlayerSkills 内「有 skillCatalog 」的卡池规则（无 UI，随机一次结果）
        if (ps.skillCatalog == null) return;

        var upgrade = new List<SkillId>(8);
        var gain = new List<SkillId>(8);

        foreach (var def in ps.skillCatalog.All())
        {
            if (def == null) continue;
            if (def.id == SkillId.None) continue;

            if (ps.HasSkill(def.id))
            {
                if (!ps.IsMaxLevel(def.id))
                    upgrade.Add(def.id);
            }
            else
            {
                if (ps.HasEmptySlot)
                    gain.Add(def.id);
            }
        }

        SkillId pick = SkillId.None;
        var pickedUpgrade = false;

        if (upgrade.Count > 0)
        {
            pick = upgrade[Random.Range(0, upgrade.Count)];
            pickedUpgrade = true;
        }
        else if (gain.Count > 0)
        {
            pick = gain[Random.Range(0, gain.Count)];
        }

        if (pick == SkillId.None) return;

        if (pickedUpgrade)
            ps.TryLevelUp(pick);
        else
            ps.TryAddSkill(pick);
    }

    private void ApplyPlaceholder(PlayerSkills ps)
    {
        if (ps.EquippedSkillCount <= 0) return;

        int idx = _placeholderCursor % ps.EquippedSkillCount;
        _placeholderCursor++;
        ps.UpgradeByEquippedIndex(idx);
    }
}
