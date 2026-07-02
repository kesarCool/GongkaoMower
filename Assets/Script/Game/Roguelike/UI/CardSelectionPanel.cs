using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选卡面板：3 卡牌 + 刷新按钮（免费 1 次 + 广告 1 次）。由 UIManager 打开。
/// </summary>
public class CardSelectionPanel : UIPanelBase
{
    [Tooltip("3 个卡牌槽位")]
    public CardView[] cardSlots;

    [Tooltip("刷新按钮（Button 组件）")]
    public Button refreshButton;

    [Tooltip("刷新按钮文字（如 刷新(1) / 看广告刷新 / 隐藏）")]
    public TextMeshProUGUI refreshCountText;

    [Header("局内技能 HUD")]
    [Tooltip("主动技能容器（5 槽位父节点）")]
    [SerializeField] private RectTransform skillSlotParent;
    [Tooltip("被动技能容器（5 槽位父节点）")]
    [SerializeField] private RectTransform passiveSlotParent;
    [Tooltip("技能槽位 Cell 预制体（icon + level，主动/被动通用）")]
    [SerializeField] private SkillSlotCell skillCellPrefab;

    private readonly List<SkillSlotCell> _skillCells = new List<SkillSlotCell>(5);
    private readonly List<SkillSlotCell> _passiveCells = new List<SkillSlotCell>(5);
    private PlayerSkills _playerSkills;

    public GameObject panelRoot;

    private Action<int> _onCardSelected;
    private Action _onRefreshRequested;
    private Action _onAdRefreshRequested;

    private int _freeRefreshCount;
    private int _adRefreshCount;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        BuildSlotRow(skillSlotParent, _skillCells, 5);
        BuildSlotRow(passiveSlotParent, _passiveCells, 5);
    }

    private void BuildSlotRow(RectTransform parent, List<SkillSlotCell> into, int count)
    {
        if (parent == null || skillCellPrefab == null) return;

        into.Clear();
        for (int i = 0; i < count; i++)
        {
            var cell = Instantiate(skillCellPrefab, parent, false);
            cell.Bind(null, 0);
            into.Add(cell);
        }
    }

    public override void OnOpen(object payload)
    {
        var p = payload as CardSelectionOpenPayload;
        if (p == null)
        {
            Debug.LogError("[CardSelectionPanel] OnOpen 需要 CardSelectionOpenPayload");
            return;
        }

        // 清除旧监听（包含 Prefab 上可能残留的旧 OnRefreshButtonClick）
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(OnRefreshClicked);
        }

        Show(p.Cards, p.OnCardSelected, p.OnRefreshRequested, p.OnAdRefreshRequested,
            p.FreeRefreshCount, p.AdRefreshCount);
    }

    public override void OnClose()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveAllListeners();
        _playerSkills = null; // 下次打开重新查找
        Hide();
        base.OnClose();
    }

    public bool Show(List<CardDeck.DrawResult> cards, Action<int> onCardSelected,
        Action onRefreshRequested, Action onAdRefreshRequested,
        int freeRefreshCount, int adRefreshCount)
    {
        // 提前初始化 PlayerSkills（卡片绑定前需要用到）
        if (_playerSkills == null)
            _playerSkills = FindObjectOfType<PlayerSkills>();

        _onCardSelected = onCardSelected;
        _onRefreshRequested = onRefreshRequested;
        _onAdRefreshRequested = onAdRefreshRequested;
        _freeRefreshCount = freeRefreshCount;
        _adRefreshCount = adRefreshCount;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        GameLog.Info($"[CardTrace] CardSelectionPanel.Show: panelRoot={(panelRoot != null ? panelRoot.activeSelf.ToString() : "NULL")} panelGO={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}");

        if (cardSlots == null || cardSlots.Length == 0)
        {
            Debug.LogError("[CardSelectionPanel] cardSlots is null or empty");
            return false;
        }

        ClearAllHighlights(); // 清除上一次选卡的残留高亮

        for (int i = 0; i < cardSlots.Length; i++)
        {
            int index = i;
            if (i < cards.Count)
                cardSlots[i].Bind(cards[i], () => OnCardClick(index));
            else
                cardSlots[i].Hide();
        }

        RefreshSkillSlots();
        UpdateRefreshUI();
        return true;
    }

    /// <summary>高亮主动技能槽位（羁绊穿透提醒）。</summary>
    public void HighlightActiveSlot(SkillId id, bool on)
    {
        var activeIds = new List<SkillId>(5);
        _playerSkills?.GetEquippedSkillIdsOrdered(activeIds);
        int idx = activeIds.IndexOf(id);
        if (idx >= 0 && idx < _skillCells.Count)
            _skillCells[idx].SetHighlight(on);
    }

    public void ClearAllHighlights()
    {
        foreach (var c in _skillCells) c.SetHighlight(false);
    }

    private void RefreshSkillSlots()
    {
        if (_playerSkills == null) return;

        // 主动技能
        var catalog = _playerSkills.skillCatalog;
        var activeIds = new List<SkillId>(5);
        _playerSkills.GetEquippedSkillIdsOrdered(activeIds);
        for (int i = 0; i < _skillCells.Count; i++)
        {
            if (i < activeIds.Count)
            {
                var def = catalog != null ? catalog.Get(activeIds[i]) : null;
                int lv = _playerSkills.GetSkillLevel(activeIds[i]);
                int maxLv = def != null ? def.maxLevel : 5;
                bool isBreakthrough = lv >= maxLv;
                _skillCells[i].Bind(def != null ? def.icon : null, lv);
                _skillCells[i].SetBreakthrough(isBreakthrough);
            }
            else
            {
                _skillCells[i].Bind(null, 0);
                _skillCells[i].SetBreakthrough(false);
            }
        }

        // 被动技能
        var passiveIds = new List<SkillId>(5);
        _playerSkills.GetEquippedPassiveIdsOrdered(passiveIds);
        for (int i = 0; i < _passiveCells.Count; i++)
        {
            if (i < passiveIds.Count)
            {
                var def = catalog != null ? catalog.Get(passiveIds[i]) : null;
                int lv = _playerSkills.GetPassiveSkillLevel(passiveIds[i]);
                _passiveCells[i].Bind(def != null ? def.icon : null, lv);
            }
            else
            {
                _passiveCells[i].Bind(null, 0);
            }
        }
    }

    public void UpdateRefreshCount(int free, int ad)
    {
        _freeRefreshCount = free;
        _adRefreshCount = ad;
        UpdateRefreshUI();
    }

    private void UpdateRefreshUI()
    {
        if (_freeRefreshCount > 0)
        {
            if (refreshCountText != null)
                refreshCountText.text = $"刷新({_freeRefreshCount})";
            if (refreshButton != null)
                refreshButton.gameObject.SetActive(true);
        }
        else if (_adRefreshCount > 0)
        {
            if (refreshCountText != null)
                refreshCountText.text = "看广告刷新";
            if (refreshButton != null)
                refreshButton.gameObject.SetActive(true);
        }
        else
        {
            if (refreshButton != null)
                refreshButton.gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        ClearAllHighlights();
        _onCardSelected = null;
        _onRefreshRequested = null;
        _onAdRefreshRequested = null;
    }

    private void OnCardClick(int index) => _onCardSelected?.Invoke(index);

    private void OnRefreshClicked()
    {
        UiClickSound.Play();

        if (_freeRefreshCount > 0)
        {
            _freeRefreshCount--;
            _onRefreshRequested?.Invoke();
        }
        else if (_adRefreshCount > 0)
        {
            // 不在此处扣减——广告成功后由 CardSelectionSystem 扣减并刷新，
            // 广告失败则次数不变，用户可重试。
            _onAdRefreshRequested?.Invoke();
        }
    }
}

public class CardSelectionOpenPayload
{
    public List<CardDeck.DrawResult> Cards;
    public Action<int> OnCardSelected;
    public Action OnRefreshRequested;
    public Action OnAdRefreshRequested;
    public int FreeRefreshCount;
    public int AdRefreshCount;
}
