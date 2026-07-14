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

    [Header("升阶技能描述")]
    [Tooltip("一阶 Rare 技能描述文本。")]
    [SerializeField] private TextMeshProUGUI promoteRareDescText;
    [Tooltip("二阶 Legend 技能描述文本。")]
    [SerializeField] private TextMeshProUGUI promoteLegendDescText;

    [Header("按钮")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button closeButton;

    // 当前已上阵的角色 ID（来自持久化存档）
    private string _equippedCharId;
    // 列表中选中的角色 ID
    private string _selectedCharId;
    private int _selectedIndex = -1;
    private readonly List<CharacterSelectionElement> _cells = new List<CharacterSelectionElement>();
    private readonly List<UpgradeAttrRow> _attrRows = new List<UpgradeAttrRow>();
    private bool _upgradeCooldown;
    private Coroutine _upgradeCooldownRoutine;
    private bool _isStatsView = true;

    public override void OnOpen(object payload)
    {
        // 已打开时仅刷新列表保持选中（避免 HomeTabBar.RefreshActive 重走 AutoSelectDefault 跳角色）
        if (_cells.Count > 0)
        {
            string savedCharId = _selectedCharId;
            PopulateList();
            if (!string.IsNullOrEmpty(savedCharId))
                SelectCellByCharId(savedCharId);
            return;
        }

        // 读已上阵角色
        PlayerProfileService.Instance.LoadOrCreate();
        _equippedCharId = PlayerProfileService.Instance.EquippedCharacterId;

        // 数据
        if (characterCatalog == null)
            characterCatalog = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");

        // 新号首次进入：自动上阵第一个已解锁角色
        if (string.IsNullOrEmpty(_equippedCharId) && characterCatalog != null)
        {
            foreach (var def in characterCatalog.characters)
            {
                if (def != null && CharacterUnlockEvaluator.IsUnlocked(def))
                {
                    _equippedCharId = def.characterId;
                    PlayerProfileService.Instance.SetEquippedCharacter(def.characterId);
                    SelectedCharacterContext.Set(def.characterId);
                    break;
                }
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
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (promoteButton != null)
            promoteButton.onClick.AddListener(OnPromoteClicked);

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
    }

    public override void OnClose()
    {
        if (_upgradeCooldownRoutine != null) { StopCoroutine(_upgradeCooldownRoutine); _upgradeCooldownRoutine = null; }
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
        ClearCells();

        if (characterCatalog == null || characterCatalog.characters.Count == 0)
        {
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
            return Priority(a).CompareTo(Priority(b));
        });

        for (int i = 0; i < sorted.Count; i++)
        {
            var def = sorted[i];
            if (def == null) continue;

            var cell = Instantiate(cellPrefab, listContent, false);
            cell.Bind(def, i, false, OnCellClicked);
            _cells.Add(cell);
        }
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
                            UIManager.Instance.ShowToast($"「{def.displayName}」已解锁！", 2f);
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

    private void SelectCell(int index, bool isEquipped)
    {
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
        portraitAnim?.PlayBounce();
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
            detailNameText.text = equipped ? $"{def.displayName}（已上阵）" : def.displayName;
        }

        // 等级 + 阶位
        var data = def.upgradeData;
        var svc = PlayerProfileService.Instance;
        int lv = svc.GetHeroLevel(def.characterId);
        int stage = data != null ? svc.GetHeroStage(def.characterId) : 0;
        int maxLv = data != null ? svc.GetEffectiveMaxLevel(def.characterId, data) : 1;
        bool isMax = lv >= maxLv;
        bool canPromote = data != null && stage < 2; // 还能升阶
        int nextLv = isMax ? lv : lv + 1;

        string stageLabel = stage == 0 ? "" : (stage == 1 ? "<color=#88CCFF>[稀有]</color> " : "<color=#FFAA00>[传说]</color> ");
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
                detailSkillText.text = $"「{skillDef.displayName}」\n{desc}";
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
                string needColor = canAfford ? "#FFFFFF" : "#FF4444";
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
        string gray = "#666666";
        string bright = "#FFFFFF";
        string gold = "#FFAA00";

        if (promoteRareDescText != null && data != null)
        {
            bool rareUnlocked = stage >= 1;
            string c = rareUnlocked ? bright : gray;
            string tag = rareUnlocked ? $"[<color={gold}>已解锁</color>]" : "[未解锁]";
            promoteRareDescText.text = $"一阶：<color={c}>{data.rareTraitDescription}</color> {tag}";
            promoteRareDescText.gameObject.SetActive(!string.IsNullOrEmpty(data.rareTraitDescription));
        }
        if (promoteLegendDescText != null && data != null)
        {
            bool legendUnlocked = stage >= 2;
            string c = legendUnlocked ? bright : gray;
            string tag = legendUnlocked ? $"[<color={gold}>已解锁</color>]" : "[未解锁]";
            promoteLegendDescText.text = $"二阶：<color={c}>{data.legendBreakthroughDescription}</color> {tag}";
            promoteLegendDescText.gameObject.SetActive(!string.IsNullOrEmpty(data.legendBreakthroughDescription));
        }

        // 升阶面板 + 按钮（技能 Tab 可见，不满足条件时灰显）
      //  if (promotePanel != null) promotePanel.SetActive(showPromote);
        if (promoteFragmentCostText != null && showPromote)
        {
            int needFrags = stage == 0 ? data.rareFragmentCost : data.legendFragmentCost;
            int haveFrags = svc.GetFragmentCount(def.characterId);
            bool enough = haveFrags >= needFrags;
            string needColor = enough ? "#FFFFFF" : "#FF4444";
            promoteFragmentCostText.text = $"碎片 <color={needColor}>{needFrags}</color>/{haveFrags}";
        }
        if (promoteButton != null)
        {
            promoteButton.gameObject.SetActive(showPromote);
            if (showPromote)
                promoteButton.interactable = svc.CanPromoteStage(def.characterId, data, out _, out _);
        }
        if (promoteRedPoint != null)
            promoteRedPoint.SetActive(showPromote && svc.CanPromoteStage(def.characterId, data, out _, out _));
    }

    // ── 按钮 ──────────────────────────────────────────

    private void OnConfirmClicked()
    {
        if (string.IsNullOrEmpty(_selectedCharId))
            return;

        UiClickSound.Play();
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
        _isStatsView = isStats;
        if (statsView != null) statsView.SetActive(isStats);
        if (skillView != null) skillView.SetActive(!isStats);
        // 切换后刷新详情，让 ShowDetail 按 _isStatsView 决定升级/升阶显隐
        RefreshCurrentDetail();
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
        ChapterLevelNavigation.TryGetMaxUnlockedLevel(out int chapterId, out int levelId);
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
        // 清旧行
        for (int i = _attrRows.Count - 1; i >= 0; i--)
        {
            if (_attrRows[i] != null) Destroy(_attrRows[i].gameObject);
        }
        _attrRows.Clear();

        if (attrRowPrefab == null || attrRowsContainer == null) return;

        var baseAttr = def.attributes.ApplyMinimums();

        // 1. 攻击（乘算）
        float curAtk = baseAttr.attack * Mul(data, data?.attackMulAtMax ?? 0f, curLv);
        float nextAtk = baseAttr.attack * Mul(data, data?.attackMulAtMax ?? 0f, nextLv);
        AddAttrRow("攻击", $"{curAtk:F0}", isMax ? "" : $"{nextAtk:F0}", isMax);

        // 2. 血量（乘算）
        float curHp = baseAttr.maxHp * Mul(data, data?.maxHpMulAtMax ?? 0f, curLv);
        float nextHp = baseAttr.maxHp * Mul(data, data?.maxHpMulAtMax ?? 0f, nextLv);
        AddAttrRow("血量", $"{curHp:F0}", isMax ? "" : $"{nextHp:F0}", isMax);

        // 3. 防御（乘算）
        float curDef = baseAttr.defense * Mul(data, data?.defenseMulAtMax ?? 0f, curLv);
        float nextDef = baseAttr.defense * Mul(data, data?.defenseMulAtMax ?? 0f, nextLv);
        AddAttrRow("防御", $"{curDef:F0}", isMax ? "" : $"{nextDef:F0}", isMax);

        // 4. 移速（乘算）
        float curSpd = baseAttr.moveSpeed * Mul(data, data?.moveSpeedMulAtMax ?? 0f, curLv);
        float nextSpd = baseAttr.moveSpeed * Mul(data, data?.moveSpeedMulAtMax ?? 0f, nextLv);
        AddAttrRow("移速", $"{curSpd:F1}", isMax ? "" : $"{nextSpd:F1}", isMax);

        // 5. 暴击率（加算，显示 %）
        float curCrit = baseAttr.critRate + Add(data, data?.critRateAddAtMax ?? 0f, curLv);
        float nextCrit = baseAttr.critRate + Add(data, data?.critRateAddAtMax ?? 0f, nextLv);
        AddAttrRow("暴击率", $"{curCrit * 100f:F0}%", isMax ? "" : $"{nextCrit * 100f:F0}%", isMax);

        // 6. 暴伤（乘算，base 始终 ≥2.0）
        float curCdm = baseAttr.critDamageMul * Mul(data, data?.critDmgMulAtMax ?? 0f, curLv);
        float nextCdm = baseAttr.critDamageMul * Mul(data, data?.critDmgMulAtMax ?? 0f, nextLv);
        AddAttrRow("暴伤", $"×{curCdm:F1}", isMax ? "" : $"×{nextCdm:F1}", isMax);

        // 7. 穿透率（加算，显示 %）
        float curPr = baseAttr.pierceRate + Add(data, data?.pierceRateAddAtMax ?? 0f, curLv);
        float nextPr = baseAttr.pierceRate + Add(data, data?.pierceRateAddAtMax ?? 0f, nextLv);
        AddAttrRow("穿透率", $"{curPr * 100f:F0}%", isMax ? "" : $"{nextPr * 100f:F0}%", isMax);

        // 8. 范围（乘算）
        float curRange = baseAttr.attackRangeMul * Mul(data, data?.attackRangeMulAtMax ?? 0f, curLv);
        float nextRange = baseAttr.attackRangeMul * Mul(data, data?.attackRangeMulAtMax ?? 0f, nextLv);
        AddAttrRow("范围", $"×{curRange:F1}", isMax ? "" : $"×{nextRange:F1}", isMax);

        // 9. 破防率（加算，显示 %）
        float curPen = baseAttr.penRate + Add(data, data?.penRateAddAtMax ?? 0f, curLv);
        float nextPen = baseAttr.penRate + Add(data, data?.penRateAddAtMax ?? 0f, nextLv);
        AddAttrRow("破防率", $"{curPen * 100f:F0}%", isMax ? "" : $"{nextPen * 100f:F0}%", isMax);
    }

    private void AddAttrRow(string name, string curValue, string nextValue, bool isMax)
    {
        var go = Instantiate(attrRowPrefab, attrRowsContainer, false);
        BattleChineseFontRuntime.ApplyToHierarchy(go.transform); // 动态实例化的 TMP 需要手动挂中文字体
        var row = go.GetComponent<UpgradeAttrRow>();
        if (row == null) { Destroy(go); return; }
        row.Bind(name, curValue, nextValue, isMax);
        _attrRows.Add(row);
    }

    // ── 升级 ──────────────────────────────────────────

    private void OnUpgradeClicked()
    {
        if (_upgradeCooldown) return;
        if (string.IsNullOrEmpty(_selectedCharId)) return;
        UiClickSound.Play();
        var def = characterCatalog?.Get(_selectedCharId);
        if (def?.upgradeData == null) return;

        // 冷却锁：防止连点/长按触发多次升级
        _upgradeCooldown = true;
        if (upgradeButton != null) upgradeButton.interactable = false;

        var svc = PlayerProfileService.Instance;
        bool ok = svc.UpgradeHero(_selectedCharId, def.upgradeData);
        if (ok)
        {
            // 刷新列表并保持当前选中（UpgradeHero 内部事件会触发 RefreshActive → OnOpen，
            // OnOpen 现在对 reopen 做了保护不再跳角色，但这里仍兜底确保选中不变）
            string savedCharId = _selectedCharId;
            PopulateList();
            if (!string.IsNullOrEmpty(savedCharId))
                SelectCellByCharId(savedCharId);
            RefreshGoldHudIfPresent();
        }
        else
        {
            // 失败时立即解冻（按钮状态由 ShowDetail → 这里没调用，手动恢复）
            _upgradeCooldown = false;
            if (upgradeButton != null && upgradeButton.gameObject.activeInHierarchy)
                upgradeButton.interactable = svc.CanAffordGold(def.upgradeData.GetCostForLevel(svc.GetHeroLevel(_selectedCharId) + 1));
            int lv = svc.GetHeroLevel(_selectedCharId);
            int maxLv = svc.GetEffectiveMaxLevel(_selectedCharId, def.upgradeData);
            if (lv >= maxLv)
                UIManager.Instance?.ShowToast("已达当前阶位满级，请升阶", 1f);
            else
            {
                int cost = def.upgradeData.GetCostForLevel(lv + 1);
                UIManager.Instance?.ShowToast($"升级需要 {PlayerProfileService.FormatGold(cost)} 金币", 1f);
            }
            return;
        }

        // 成功：延迟解冻
        if (_upgradeCooldownRoutine != null) StopCoroutine(_upgradeCooldownRoutine);
        _upgradeCooldownRoutine = StartCoroutine(ResetUpgradeCooldown(def.upgradeData));
    }

    private System.Collections.IEnumerator ResetUpgradeCooldown(HeroUpgradeData data)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        _upgradeCooldown = false;
        if (upgradeButton != null && upgradeButton.gameObject.activeInHierarchy
            && !string.IsNullOrEmpty(_selectedCharId) && data != null)
        {
            int nextLv = PlayerProfileService.Instance.GetHeroLevel(_selectedCharId) + 1;
            int maxLv = PlayerProfileService.Instance.GetEffectiveMaxLevel(_selectedCharId, data);
            bool showUpgrade = nextLv <= maxLv;
            upgradeButton.interactable = showUpgrade && PlayerProfileService.Instance.CanAffordGold(data.GetCostForLevel(nextLv));
        }
    }

    private void OnPromoteClicked()
    {
        if (string.IsNullOrEmpty(_selectedCharId)) return;
        var def = characterCatalog?.Get(_selectedCharId);
        if (def?.upgradeData == null) return;
        UiClickSound.Play();

        var svc = PlayerProfileService.Instance;
        bool ok = svc.PromoteStage(_selectedCharId, def.upgradeData);
        if (ok)
        {
            string savedCharId = _selectedCharId;
            PopulateList();
            if (!string.IsNullOrEmpty(savedCharId))
                SelectCellByCharId(savedCharId);
        }
        else
        {
            svc.CanPromoteStage(_selectedCharId, def.upgradeData, out int missing, out bool lvOk);
            if (!lvOk)
                UIManager.Instance?.ShowToast("等级未达到升阶要求", 1f);
            else if (missing > 0)
                UIManager.Instance?.ShowToast($"碎片不足，还需 {missing} 片", 1f);
            else
                UIManager.Instance?.ShowToast("升阶失败", 1f);
        }
    }

    private void RefreshGoldHudIfPresent()
    {
        var hub = FindObjectOfType<HomeHubController>();
        if (hub != null) hub.RefreshCurrencyHud();
    }

    // ── 辅助公式 ──────────────────────────────────────

    private static float Mul(HeroUpgradeData data, float atMax, int level)
    {
        if (data == null || level <= 1) return 1f;
        float t = (level - 1f) / Mathf.Max(1, data.maxLevel - 1);
        return Mathf.Lerp(1f, atMax, Mathf.Clamp01(t));
    }

    private static float Add(HeroUpgradeData data, float atMax, int level)
    {
        if (data == null || level <= 1) return 0f;
        float t = (level - 1f) / Mathf.Max(1, data.maxLevel - 1);
        return Mathf.Lerp(0f, atMax, Mathf.Clamp01(t));
    }
}
