using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选角弹窗（Prefab 驱动）：角色列表 + 选中详情 + 确认换将。
/// 已上阵角色不显示确认按钮，换将后持久化到 PlayerProfileService。
/// </summary>
[DisallowMultipleComponent]
public class CharacterSelectionPanel : UIPanelBase
{
    [Header("数据")]
    [SerializeField] private CharacterCatalog characterCatalog;
    [SerializeField] private SkillCatalog skillCatalog;

    [Header("列表")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private CharacterSelectionElement cellPrefab;

    [Header("详情")]
    [SerializeField] private Image detailPortrait;
    [Tooltip("立绘待机微动组件（挂 detailPortrait 同物体）。")]
    [SerializeField] private IdleBreathAnim portraitAnim;
    [SerializeField] private TextMeshProUGUI detailNameText;
    [SerializeField] private TextMeshProUGUI detailLevelText;
    [SerializeField] private Image detailSkillIcon;
    [SerializeField] private TextMeshProUGUI detailSkillText;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI tipsText;

    [Header("属性行")]
    [Tooltip("UpgradeAttrRow 预制体，动态实例化 8 行。")]
    [SerializeField] private GameObject attrRowPrefab;
    [Tooltip("属性行容器（挂 VerticalLayoutGroup）。")]
    [SerializeField] private Transform attrRowsContainer;

    [Header("视图切换")]
    [SerializeField] private Toggle statsTabToggle;
    [SerializeField] private Toggle skillTabToggle;
    [Tooltip("属性视图容器（属性行 + 升级/升阶按钮）。")]
    [SerializeField] private GameObject statsView;
    [Tooltip("技能视图容器（技能图标/描述 + 升阶技能描述）。")]
    [SerializeField] private GameObject skillView;

    [Header("升级操作")]
    [SerializeField] private TextMeshProUGUI upgradeCostText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private GameObject upgradeRedPoint; // 金币足够时显示

    [Header("升阶操作")]
    [Tooltip("升阶面板（含碎片消耗 + 升阶按钮），与升级按钮互斥。")]
   // [SerializeField] private GameObject promotePanel;
    [SerializeField] private TextMeshProUGUI promoteFragmentCostText;
    [SerializeField] private Button promoteButton;
    [SerializeField] private GameObject promoteRedPoint; // 碎片+等级足够时显示

    [Header("Toggle 红点")]
    [Tooltip("\"基础属性\" Tab 上的红点——当前选中角色可升级时显示。")]
    [SerializeField] private GameObject statsToggleBadge;
    [Tooltip("\"技能\" Tab 上的红点——当前选中角色可升阶时显示。")]
    [SerializeField] private GameObject skillToggleBadge;

    [Header("升阶技能描述")]
    [Tooltip("一阶 Rare 技能描述文本。")]
    [SerializeField] private TextMeshProUGUI promoteRareDescText;
    [Tooltip("二阶 Legend 技能描述文本。")]
    [SerializeField] private TextMeshProUGUI promoteLegendDescText;

    [Header("按钮")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private CharacterPanelPresentation presentation;

    // 当前已上阵的角色 ID（来自持久化存档）
    private string _equippedCharId;
    // 列表中选中的角色 ID
    private string _selectedCharId;
    private int _selectedIndex = -1;
    private readonly List<CharacterSelectionElement> _cells = new List<CharacterSelectionElement>();
    private readonly List<UpgradeAttrRow> _attrRows = new List<UpgradeAttrRow>();
    private bool _upgradeCooldown;
    private bool _isStatsView = true;
    private bool _initialized, _initializing, _refreshing, _transactionBusy, _refreshPending;
    private int _stagePresentationOverride = -1;
    private CharacterGrowthFeedback _growthFeedback;
    private int _rowWriteIndex;

    public string SelectedCharacterId => _selectedCharId;
    public Button TutorialUpgradeButton => upgradeButton;

    public void ShowTutorialStats()
    {
        SetStatsView(true);
    }
    public bool IsGrowthFeedbackPlaying => _growthFeedback != null && _growthFeedback.IsPlaying;
    public CharacterGrowthFeedback GrowthFeedback => _growthFeedback;
    public IReadOnlyList<UpgradeAttrRow> AttributeRows => _attrRows;
    public bool IsSkillView => !_isStatsView;

    public override void OnOpen(object payload)
    {
        // SpendGold/Persist publishes synchronously. Never rebuild a transaction or its visible feedback midway.
        if (_transactionBusy || IsGrowthFeedbackPlaying || _initializing || _refreshing)
        {
            _refreshPending = true;
            return;
        }
        if (_initialized) { RefreshPanelPreservingSelection(); return; }
        _initializing = true;
        try
        {

        // 读已上阵角色
        PlayerProfileService.Instance.LoadOrCreate();
        _equippedCharId = PlayerProfileService.Instance.EquippedCharacterId;

        // 数据
        if (characterCatalog == null)
            characterCatalog = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");

        // 新号首次进入：优先上阵 Catalog 默认角色，否则回退第一个已解锁角色
        if (string.IsNullOrEmpty(_equippedCharId) && characterCatalog != null)
        {
            CharacterDefinition def = characterCatalog.GetDefault();
            if (def == null || !CharacterUnlockEvaluator.IsUnlocked(def))
            {
                def = null;
                foreach (var d in characterCatalog.characters)
                {
                    if (d != null && CharacterUnlockEvaluator.IsUnlocked(d)) { def = d; break; }
                }
            }
            if (def != null)
            {
                _equippedCharId = def.characterId;
                PlayerProfileService.Instance.SetEquippedCharacter(def.characterId);
                SelectedCharacterContext.Set(def.characterId);
            }
        }

        // 字体——UIManager 已在 Open 时 ApplyToHierarchy，此处兜底
        BattleChineseFontRuntime.ApplyToHierarchy(transform);

        // 按钮监听
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
            // 锁定原因评估（按钮 Prefab 挂 UiLockedHint 后生效，未挂时静默跳过）
            upgradeButton.GetComponent<UiLockedHint>()?.Bind(EvaluateUpgradeLock);
        }
        if (promoteButton != null)
        {
            promoteButton.onClick.AddListener(OnPromoteClicked);
            promoteButton.GetComponent<UiLockedHint>()?.Bind(EvaluatePromoteLock);
        }

        // 视图切换 Toggle
        if (statsTabToggle != null)
            statsTabToggle.onValueChanged.AddListener(OnStatsTabToggled);
        if (skillTabToggle != null)
            skillTabToggle.onValueChanged.AddListener(OnSkillTabToggled);
        // 默认切到属性视图
        if (statsTabToggle != null && statsTabToggle.isOn)
            SwitchToView(isStats: true);
        else if (skillTabToggle != null && skillTabToggle.isOn)
            SwitchToView(isStats: false);
        else
            SwitchToView(isStats: true);

        PopulateList();
        AutoSelectDefault();
        _initialized = true;
        _refreshPending = false;
        }
        finally { _initializing = false; }
    }

    public override void OnClose()
    {
        SkipGrowthFeedback();
        _upgradeCooldown = false;
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (startGameButton != null) startGameButton.onClick.RemoveListener(OnStartGameClicked);
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        if (promoteButton != null) promoteButton.onClick.RemoveListener(OnPromoteClicked);
        if (statsTabToggle != null) statsTabToggle.onValueChanged.RemoveListener(OnStatsTabToggled);
        if (skillTabToggle != null) skillTabToggle.onValueChanged.RemoveListener(OnSkillTabToggled);
        ClearCells();
        ClearAttrRows();
        _initialized = false;
    }

    private void OnDisable()
    {
        SkipGrowthFeedback();
        _refreshPending = true;
    }

    public void SkipGrowthFeedback()
    {
        if (_growthFeedback != null) _growthFeedback.CompleteImmediately();
        foreach (var row in _attrRows) if (row != null) row.CompleteAnimation();
    }

    private void RefreshPanelPreservingSelection()
    {
        if (_refreshing) { _refreshPending = true; return; }
        _refreshing = true;
        try
        {
            _equippedCharId = PlayerProfileService.Instance.EquippedCharacterId;
            PopulateList();
            bool found = false;
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].CharacterDef != null && _cells[i].CharacterDef.characterId == _selectedCharId)
                { SelectCell(i, _selectedCharId == _equippedCharId); found = true; break; }
            if (!found) AutoSelectDefault();
            _refreshPending = false;
        }
        finally { _refreshing = false; }
    }

    private void ClearAttrRows()
    {
        for (int i = _attrRows.Count - 1; i >= 0; i--)
        {
            if (_attrRows[i] != null) Destroy(_attrRows[i].gameObject);
        }
        _attrRows.Clear();
    }

    // ── 列表 ──────────────────────────────────────────

    private void PopulateList()
    {
        if (characterCatalog == null || characterCatalog.characters.Count == 0)
        {
            ClearCells();
            if (detailPanel != null) detailPanel.SetActive(false);
            if (tipsText != null) { tipsText.text = "配置表为空"; tipsText.gameObject.SetActive(true); }
            return;
        }

        if (tipsText != null) tipsText.gameObject.SetActive(false);

        // 排序：已上阵 > 可上阵 > 未解锁
        var sorted = new List<CharacterDefinition>(characterCatalog.characters);
        sorted.Sort((a, b) =>
        {
            int Priority(CharacterDefinition c)
            {
                if (c == null) return 3;
                if (c.characterId == _equippedCharId) return 0;
                if (CharacterUnlockEvaluator.IsUnlocked(c)) return 1;
                return 2;
            }
            int priority = Priority(a).CompareTo(Priority(b));
            return priority != 0 ? priority : characterCatalog.characters.IndexOf(a).CompareTo(characterCatalog.characters.IndexOf(b));
        });

        var previous = new List<CharacterSelectionElement>(_cells);
        _cells.Clear();
        _selectedIndex = -1;
        for (int i = 0; i < sorted.Count; i++)
        {
            var def = sorted[i];
            if (def == null) continue;

            CharacterSelectionElement cell = null;
            for (int j = previous.Count - 1; j >= 0; j--)
                if (previous[j] != null && previous[j].CharacterDef != null && previous[j].CharacterDef.characterId == def.characterId)
                { cell = previous[j]; previous.RemoveAt(j); break; }
            if (cell == null && cellPrefab != null && listContent != null) cell = Instantiate(cellPrefab, listContent, false);
            if (cell == null) continue;
            int index = _cells.Count;
            bool selected = def.characterId == _selectedCharId;
            cell.Bind(def, index, selected, OnCellClicked);
            cell.transform.SetSiblingIndex(index);
            if (selected) _selectedIndex = index;
            _cells.Add(cell);
        }
        foreach (var cell in previous) if (cell != null) { cell.gameObject.SetActive(false); Destroy(cell.gameObject); }
    }

    private void ClearCells()
    {
        for (int i = _cells.Count - 1; i >= 0; i--)
        {
            if (_cells[i] != null)
                Destroy(_cells[i].gameObject);
        }
        _cells.Clear();
    }

    // ── 默认选中 ──────────────────────────────────────

    private void AutoSelectDefault()
    {
        if (_cells.Count == 0) return;

        // 优先选已上阵角色
        if (!string.IsNullOrEmpty(_equippedCharId))
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].CharacterDef != null &&
                    _cells[i].CharacterDef.characterId == _equippedCharId &&
                    CharacterUnlockEvaluator.IsUnlocked(_cells[i].CharacterDef))
                {
                    SelectCell(i, isEquipped: true);
                    return;
                }
            }
        }

        // 其次选第一个未锁定角色
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].CharacterDef != null && CharacterUnlockEvaluator.IsUnlocked(_cells[i].CharacterDef))
            {
                bool isEquipped = _cells[i].CharacterDef.characterId == _equippedCharId;
                SelectCell(i, isEquipped);
                return;
            }
        }
    }

    // ── 选中 ──────────────────────────────────────────

    private void OnCellClicked(CharacterSelectionElement cell)
    {
        if (_transactionBusy) return;
        if (cell.CharacterDef == null) return;

        if (!CharacterUnlockEvaluator.IsUnlocked(cell.CharacterDef))
        {
            var def = cell.CharacterDef;

            // 碎片集齐 → 弹出二次确认消耗碎片解锁
            if (CharacterUnlockEvaluator.CanFragmentUnlock(def))
            {
                int cost = def.unlockFragmentCount;
                string msg = $"是否确认花费 <color=#FFD700>{cost}</color> 片碎片解锁「<color=#FFD700>{def.displayName}</color>」？";
                UIManager.Instance.ShowConfirm("碎片解锁", msg, confirmed =>
                {
                    if (confirmed)
                    {
                        var result = CharacterUnlockEvaluator.TryConsumeFragmentUnlock(def);
                        if (result.success)
                        {
                            RewardRevealQueue.ShowCharacter("character_unlock:" + def.characterId, "character_unlock", def.characterId);
                            PopulateList();
                            // 自动选中刚解锁的角色
                            SelectCellByCharId(def.characterId);
                        }
                        else
                        {
                            UIManager.Instance.ShowToast("解锁失败，请重试", 1f);
                        }
                    }
                });
                return;
            }

            // 碎片不足或其他条件 → 弹出提示
            string hint = CharacterUnlockEvaluator.GetUnlockHint(cell.CharacterDef);
            if (!string.IsNullOrEmpty(hint))
                UIManager.Instance.ShowToast(hint, 1f);
            return;
        }

        bool isEquipped = cell.CharacterDef.characterId == _equippedCharId;
        SelectCell(cell.Index, isEquipped);
    }

    /// <summary>按角色 ID 选中（用于数据刷新后恢复选中）。</summary>
    private void SelectCellByCharId(string charId)
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].CharacterDef?.characterId == charId)
            {
                SelectCell(i, isEquipped: charId == _equippedCharId);
                return;
            }
        }
    }

    public bool SelectCharacter(string characterId)
    {
        if (_transactionBusy) return false;
        for (int i = 0; i < _cells.Count; i++)
            if (_cells[i].CharacterDef != null && _cells[i].CharacterDef.characterId == characterId
                && CharacterUnlockEvaluator.IsUnlocked(_cells[i].CharacterDef))
            { SelectCell(i, characterId == _equippedCharId); return true; }
        return false;
    }

    private void SelectCell(int index, bool isEquipped)
    {
        if (index < 0 || index >= _cells.Count || _cells[index] == null || _cells[index].CharacterDef == null) return;
        bool changed = _selectedCharId != _cells[index].CharacterDef.characterId;
        if (changed) SkipGrowthFeedback();
        // 清旧选中
        if (_selectedIndex >= 0 && _selectedIndex < _cells.Count)
            _cells[_selectedIndex].SetSelected(false);

        // 设新选中
        var def = _cells[index].CharacterDef;
        _selectedIndex = index;
        _selectedCharId = def.characterId;
        _cells[index].SetSelected(true);
        ShowDetail(def);

        // 已上阵→显示开始游戏，未上阵→显示换将上阵
        RefreshButtons(def.characterId == _equippedCharId);

        // 切角色弹入动画
        if (changed) portraitAnim?.PlayBounce();
    }

    // ── 详情 ──────────────────────────────────────────

    private void ShowDetail(CharacterDefinition def)
    {
        if (detailPanel != null)
            detailPanel.SetActive(true);

        // 头像 + 名称
        if (detailPortrait != null)
        {
            detailPortrait.sprite = def.portrait;
            detailPortrait.enabled = def.portrait != null;
        }
        if (detailNameText != null)
        {
            bool equipped = def.characterId == _equippedCharId;
            detailNameText.text = equipped && presentation == null ? $"{def.displayName}（已上阵）" : def.displayName;
        }

        // 等级 + 阶位
        var data = def.upgradeData;
        var svc = PlayerProfileService.Instance;
        int lv = svc.GetHeroLevel(def.characterId);
        int stage = _stagePresentationOverride >= 0 ? _stagePresentationOverride : data != null ? svc.GetHeroStage(def.characterId) : 0;
        int maxLv = data != null ? svc.GetEffectiveMaxLevel(def.characterId, data) : 1;
        bool isMax = lv >= maxLv;
        bool canPromote = data != null && stage < 2; // 还能升阶
        int nextLv = isMax ? lv : lv + 1;

        string stageLabel = stage == 0 ? "" : (stage == 1 ? (presentation != null ? "<color=#497D9E>[稀有]</color> " : "<color=#88CCFF>[稀有]</color> ") : "<color=#B58029>[传说]</color> ");
        if (detailLevelText != null)
        {
            detailLevelText.text = isMax && !canPromote
                ? $"{stageLabel}Lv.{lv} / {maxLv}  <color=#FFD700>满级</color>"
                : $"{stageLabel}Lv.{lv} / {maxLv}";
            detailLevelText.gameObject.SetActive(true);
        }

        // 属性行（9 行：攻/血/防/移速/暴击率/暴伤/穿透/范围/破防率）
        BuildAttrRows(def, data, lv, nextLv, isMax && !canPromote);

        // 技能
        if (detailSkillText != null)
        {
            SkillId effectiveSkill = def.defaultWeapon != null && def.defaultWeapon.weaponSkillId != SkillId.None
                ? def.defaultWeapon.weaponSkillId
                : def.startingSkill;

            var skillDef = skillCatalog != null ? skillCatalog.Get(effectiveSkill) : null;
            if (detailSkillIcon != null)
            {
                detailSkillIcon.sprite = skillDef != null ? skillDef.icon : null;
                detailSkillIcon.enabled = skillDef != null && skillDef.icon != null;
            }
            if (skillDef != null)
            {
                string desc = skillDef.description;
                if (string.IsNullOrEmpty(desc))
                    desc = skillDef.FormatAllLevelDescriptions(highlightLevel: 1);
                detailSkillText.text = presentation != null ? desc : $"「{skillDef.displayName}」\n{desc}";
            }
            else
            {
                detailSkillText.text = effectiveSkill != SkillId.None ? $"初始技能：{effectiveSkill}" : "";
            }
        }

        // 属性Tab → 升级；技能Tab → 升阶
        bool atLevelCap = isMax;
        bool showUpgrade = data != null && !atLevelCap && _isStatsView;
        bool showPromote = data != null && canPromote && !_isStatsView;

        // 升级消耗 + 按钮
        if (upgradeCostText != null)
        {
            upgradeCostText.gameObject.SetActive(showUpgrade);
            if (showUpgrade)
            {
                int cost = data.GetCostForLevel(nextLv);
                int have = svc.Gold;
                bool canAfford = have >= cost;
                string needColor = canAfford ? (presentation != null ? "#76634C" : "#FFFFFF") : "#B45F48";
                upgradeCostText.text = $"金币 <color={needColor}>{PlayerProfileService.FormatGold(cost)}</color>/{PlayerProfileService.FormatGold(have)}";
            }
        }
        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(showUpgrade);
            if (showUpgrade) upgradeButton.interactable = !_upgradeCooldown && svc.CanAffordGold(data.GetCostForLevel(nextLv));
        }
        if (upgradeRedPoint != null)
            upgradeRedPoint.SetActive(showUpgrade && svc.CanAffordGold(data.GetCostForLevel(nextLv)));

        // 升阶技能描述（始终可见，未解锁置灰）
        string gray = presentation != null ? "#8D7A5D" : "#666666";
        string bright = presentation != null ? "#493627" : "#FFFFFF";
        string gold = "#FFAA00";

        if (promoteRareDescText != null && data != null)
        {
            bool rareUnlocked = stage >= 1;
            string c = rareUnlocked ? bright : gray;
            string tag = rareUnlocked ? $"[<color={gold}>已解锁</color>]" : "[未解锁]";
            promoteRareDescText.text = presentation != null ? $"<color={c}>{data.rareTraitDescription}</color>" : $"一阶：<color={c}>{data.rareTraitDescription}</color> {tag}";
            promoteRareDescText.gameObject.SetActive(!string.IsNullOrEmpty(data.rareTraitDescription));
        }
        if (promoteLegendDescText != null && data != null)
        {
            bool legendUnlocked = stage >= 2;
            string c = legendUnlocked ? bright : gray;
            string tag = legendUnlocked ? $"[<color={gold}>已解锁</color>]" : "[未解锁]";
            promoteLegendDescText.text = presentation != null ? $"<color={c}>{data.legendBreakthroughDescription}</color>" : $"二阶：<color={c}>{data.legendBreakthroughDescription}</color> {tag}";
            promoteLegendDescText.gameObject.SetActive(!string.IsNullOrEmpty(data.legendBreakthroughDescription));
        }

        // 升阶面板 + 按钮（技能 Tab 可见，不满足条件时灰显）
      //  if (promotePanel != null) promotePanel.SetActive(showPromote);
        if (promoteFragmentCostText != null) promoteFragmentCostText.gameObject.SetActive(showPromote);
        if (promoteFragmentCostText != null && showPromote)
        {
            int needFrags = stage == 0 ? data.rareFragmentCost : data.legendFragmentCost;
            int haveFrags = svc.GetFragmentCount(def.characterId);
            bool enough = haveFrags >= needFrags;
            string needColor = enough ? (presentation != null ? "#76634C" : "#FFFFFF") : "#B45F48";
            promoteFragmentCostText.text = $"碎片 <color={needColor}>{needFrags}</color>/{haveFrags}";
        }
        if (promoteButton != null)
        {
            promoteButton.gameObject.SetActive(showPromote);
            if (showPromote)
                promoteButton.interactable = !_upgradeCooldown && !_transactionBusy && svc.CanPromoteStage(def.characterId, data, out _, out _);
        }
        if (promoteRedPoint != null)
            promoteRedPoint.SetActive(showPromote && svc.CanPromoteStage(def.characterId, data, out _, out _));

        // Toggle 红点（不受当前视图影响，始终评估引导用户切 Tab）
        if (statsToggleBadge != null)
            statsToggleBadge.SetActive(CharacterRedDotEvaluator.CanUpgrade(def));
        if (skillToggleBadge != null)
            skillToggleBadge.SetActive(CharacterRedDotEvaluator.CanPromote(def));
        if (presentation != null)
        {
            SkillId effectiveSkill = def.defaultWeapon != null && def.defaultWeapon.weaponSkillId != SkillId.None ? def.defaultWeapon.weaponSkillId : def.startingSkill;
            presentation.Refresh(def, skillCatalog != null ? skillCatalog.Get(effectiveSkill) : null, def.characterId == _equippedCharId, characterCatalog, stage);
        }
    }

    // ── 按钮 ──────────────────────────────────────────

    private void OnConfirmClicked()
    {
        if (string.IsNullOrEmpty(_selectedCharId))
            return;

        UiClickSound.Play();
        SkipGrowthFeedback();
        SelectedCharacterContext.Set(_selectedCharId);
        _equippedCharId = _selectedCharId;

        // 刷新列表排序（旧角色下移，新角色置顶）
        PopulateList();
        AutoSelectDefault();
    }

    /// <summary>已上阵显示开始游戏，未上阵显示换将上阵，二者互斥。</summary>
    private void RefreshButtons(bool isEquipped)
    {
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(!isEquipped);
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isEquipped);
    }

    // ── 视图切换 ──────────────────────────────────────

    private void OnStatsTabToggled(bool isOn)
    {
        if (isOn) { UiClickSound.PlaySwitch(); SwitchToView(isStats: true); }
    }

    private void OnSkillTabToggled(bool isOn)
    {
        if (isOn) { UiClickSound.PlaySwitch(); SwitchToView(isStats: false); }
    }

    private void SwitchToView(bool isStats)
    {
        if (_isStatsView != isStats) SkipGrowthFeedback();
        _isStatsView = isStats;
        if (statsView != null) statsView.SetActive(isStats);
        if (skillView != null) skillView.SetActive(!isStats);
        // 切换后刷新详情，让 ShowDetail 按 _isStatsView 决定升级/升阶显隐
        RefreshCurrentDetail();
    }

    public void SetStatsView(bool isStats)
    {
        if (statsTabToggle != null) statsTabToggle.SetIsOnWithoutNotify(isStats);
        if (skillTabToggle != null) skillTabToggle.SetIsOnWithoutNotify(!isStats);
        SwitchToView(isStats);
    }

    private void RefreshCurrentDetail()
    {
        if (string.IsNullOrEmpty(_selectedCharId) || characterCatalog == null) return;
        var def = characterCatalog.Get(_selectedCharId);
        if (def != null) ShowDetail(def);
    }

    private void OnStartGameClicked()
    {
        if (string.IsNullOrEmpty(_selectedCharId))
            return;

        UiClickSound.Play();

        // 获取选中角色名
        string charName = "";
        if (_selectedIndex >= 0 && _selectedIndex < _cells.Count &&
            _cells[_selectedIndex].CharacterDef != null)
        {
            charName = _cells[_selectedIndex].CharacterDef.displayName;
        }

        // 当前进度最新的未解锁关卡
        HomeHubController.TryResolveStartLevel(out int chapterId, out int levelId);
        string levelLabel = ChapterLevelDisplay.FormatLevelName(levelId);
        string msg = $"是否选择 <b><color=#FF5100><size=+16>「{charName}」</size></color></b> \n进入「{levelLabel}」？";

        UIManager.Instance.ShowConfirm("开始游戏", msg, confirmed =>
        {
            if (confirmed)
            {
                SelectedCharacterContext.Set(_selectedCharId);
                SelectedLevelContext.Set(chapterId, levelId);
                BattleFlowLauncher.TryStartBattleLoading();
            }
        });
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }

    // ── 属性行 ──────────────────────────────────────────

    private void BuildAttrRows(CharacterDefinition def, HeroUpgradeData data, int curLv, int nextLv, bool isMax)
    {
        if (attrRowPrefab == null || attrRowsContainer == null) return;
        var current = CharacterGrowthSnapshot.EvaluateStats(def, curLv);
        var next = CharacterGrowthSnapshot.EvaluateStats(def, nextLv);
        _rowWriteIndex = 0;
        for (int i = 0; i < current.Length; i++) AddAttrRow(current[i], next[i], isMax);
        for (int i = _rowWriteIndex; i < _attrRows.Count; i++)
            if (_attrRows[i] != null) _attrRows[i].gameObject.SetActive(false);
    }

    private void AddAttrRow(CharacterGrowthValue current, CharacterGrowthValue next, bool isMax)
    {
        UpgradeAttrRow row = _rowWriteIndex < _attrRows.Count ? _attrRows[_rowWriteIndex] : null;
        if (row == null)
        {
            var go = Instantiate(attrRowPrefab, attrRowsContainer, false);
            BattleChineseFontRuntime.ApplyToHierarchy(go.transform);
            row = go.GetComponent<UpgradeAttrRow>();
            if (row == null) { Destroy(go); return; }
            if (_rowWriteIndex < _attrRows.Count) _attrRows[_rowWriteIndex] = row;
            else _attrRows.Add(row);
        }
        row.gameObject.SetActive(true);
        row.BindNumeric(current, next, isMax);
        row.SetPresentation(_rowWriteIndex);
        _rowWriteIndex++;
    }

    // ── 升级 ──────────────────────────────────────────

    private void OnUpgradeClicked()
    {
        TryUpgradeSelected();
    }

    public bool TryUpgradeSelected()
    {
        if (_transactionBusy || _upgradeCooldown || IsGrowthFeedbackPlaying || !isActiveAndEnabled) return false;
        var def = characterCatalog != null ? characterCatalog.Get(_selectedCharId) : null;
        if (def == null || def.upgradeData == null || !CharacterUnlockEvaluator.IsUnlocked(def)) return false;
        var before = CharacterGrowthSnapshot.Capture(def);
        _transactionBusy = _upgradeCooldown = true;
        SetGrowthButtonsLocked();
        bool ok;
        try { ok = PlayerProfileService.Instance.UpgradeHero(def.characterId, def.upgradeData); }
        finally { _transactionBusy = false; }
        if (!ok)
        {
            FinishGrowthFeedback();
            if (before.Level >= before.MaxLevel) UIManager.Instance?.ShowToast("已达当前阶位满级，请升阶", 1f);
            else UIManager.Instance?.ShowToast($"升级需要 {PlayerProfileService.FormatGold(def.upgradeData.GetCostForLevel(before.Level + 1))} 金币", 1f);
            return false;
        }
        UiClickSound.Play();
        var after = CharacterGrowthSnapshot.Capture(def);
        RefreshPanelPreservingSelection();
        RefreshGoldHudIfPresent();
        if (!isActiveAndEnabled || _selectedCharId != before.CharacterId) { FinishGrowthFeedback(); return true; }
        EnsureGrowthFeedback().PlayUpgrade(before, after, _attrRows, detailLevelText, FormatLevelLabel(before), FormatLevelLabel(after), presentation, FinishGrowthFeedback);
        return true;
    }

    private void OnPromoteClicked()
    {
        TryPromoteSelected();
    }

    public bool TryPromoteSelected()
    {
        if (_transactionBusy || _upgradeCooldown || IsGrowthFeedbackPlaying || !isActiveAndEnabled) return false;
        var def = characterCatalog != null ? characterCatalog.Get(_selectedCharId) : null;
        if (def == null || def.upgradeData == null || !CharacterUnlockEvaluator.IsUnlocked(def)) return false;
        var svc = PlayerProfileService.Instance;
        var before = CharacterGrowthSnapshot.Capture(def);
        _transactionBusy = _upgradeCooldown = true;
        SetGrowthButtonsLocked();
        bool ok;
        try { ok = svc.PromoteStage(def.characterId, def.upgradeData); }
        finally { _transactionBusy = false; }
        if (!ok)
        {
            FinishGrowthFeedback();
            svc.CanPromoteStage(def.characterId, def.upgradeData, out int missing, out bool lvOk);
            if (!lvOk)
                UIManager.Instance?.ShowToast("等级未达到升阶要求", 1f);
            else if (missing > 0)
                UIManager.Instance?.ShowToast($"碎片不足，还需 {missing} 片", 1f);
            else
                UIManager.Instance?.ShowToast("升阶失败", 1f);
            return false;
        }
        UiClickSound.Play();
        var after = CharacterGrowthSnapshot.Capture(def);
        // Only the presentation remains at the old tier during charge; the save has already committed once.
        _stagePresentationOverride = before.Stage;
        RefreshPanelPreservingSelection();
        if (!isActiveAndEnabled || _selectedCharId != before.CharacterId) { FinishGrowthFeedback(); return true; }
        EnsureGrowthFeedback().PlayPromotion(before, after, _attrRows, detailLevelText, FormatLevelLabel(before), FormatLevelLabel(after),
            presentation, () =>
            {
                _stagePresentationOverride = -1;
                if (_selectedCharId == after.CharacterId) RefreshCurrentDetail();
            }, FinishGrowthFeedback);
        return true;
    }

    private CharacterGrowthFeedback EnsureGrowthFeedback()
    {
        if (_growthFeedback == null) _growthFeedback = GetComponent<CharacterGrowthFeedback>();
        if (_growthFeedback == null) _growthFeedback = gameObject.AddComponent<CharacterGrowthFeedback>();
        return _growthFeedback;
    }

    private void SetGrowthButtonsLocked()
    {
        if (upgradeButton != null) upgradeButton.interactable = false;
        if (promoteButton != null) promoteButton.interactable = false;
    }

    // ── 锁定原因评估（供按钮上的 UiLockedHint 解释"为什么点不了"）──

    private UiLockedHint.Status EvaluateUpgradeLock()
    {
        // 交易中/冷却/成长动画播放中属忙碌：保持静默，不误报金币不足
        if (_transactionBusy || _upgradeCooldown || IsGrowthFeedbackPlaying)
            return UiLockedHint.Status.Silent();

        var def = characterCatalog != null ? characterCatalog.Get(_selectedCharId) : null;
        var data = def != null ? def.upgradeData : null;
        if (data == null) return UiLockedHint.Status.Available();

        var svc = PlayerProfileService.Instance;
        int nextLv = svc.GetHeroLevel(def.characterId) + 1;
        return svc.CanAffordGold(data.GetCostForLevel(nextLv))
            ? UiLockedHint.Status.Available()
            : UiLockedHint.Status.Deny(GameErrorCodes.GoldNotEnough);
    }

    private UiLockedHint.Status EvaluatePromoteLock()
    {
        if (_transactionBusy || _upgradeCooldown || IsGrowthFeedbackPlaying)
            return UiLockedHint.Status.Silent();

        var def = characterCatalog != null ? characterCatalog.Get(_selectedCharId) : null;
        var data = def != null ? def.upgradeData : null;
        if (data == null) return UiLockedHint.Status.Available();

        // 优先级与 TryPromoteSelected 失败分支一致：先等级后碎片
        if (PlayerProfileService.Instance.CanPromoteStage(def.characterId, data, out int missing, out bool levelOk))
            return UiLockedHint.Status.Available();
        if (!levelOk) return UiLockedHint.Status.Deny(GameErrorCodes.StageLevelNotEnough);
        if (missing > 0) return UiLockedHint.Status.Deny(GameErrorCodes.FragmentNotEnough);
        return UiLockedHint.Status.Silent(); // 其余原因（如已是最高阶）不做硬编提示
    }

    private void FinishGrowthFeedback()
    {
        _upgradeCooldown = false;
        _stagePresentationOverride = -1;
        // Coalesce any number of HomeTabBar.RefreshActive calls into this final refresh, without replaying feedback.
        if (_initialized)
        {
            if (_refreshPending) RefreshPanelPreservingSelection();
            else RefreshCurrentDetail();
        }
    }

    private string FormatLevelLabel(CharacterGrowthSnapshot snapshot)
    {
        string tier = snapshot.Stage == 0 ? "" : snapshot.Stage == 1
            ? (presentation != null ? "<color=#497D9E>[稀有]</color> " : "<color=#88CCFF>[稀有]</color> ")
            : "<color=#B58029>[传说]</color> ";
        return tier + "Lv." + snapshot.Level + " / " + snapshot.MaxLevel
            + (snapshot.Level >= snapshot.MaxLevel && snapshot.Stage >= 2 ? "  <color=#FFD700>满级</color>" : "");
    }

    private void RefreshGoldHudIfPresent()
    {
        var hub = FindObjectOfType<HomeHubController>();
        if (hub != null) hub.RefreshCurrencyHud();
    }

}
