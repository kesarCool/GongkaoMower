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
    [SerializeField] private TextMeshProUGUI detailNameText;
    [SerializeField] private TextMeshProUGUI detailAttackText;
    [SerializeField] private TextMeshProUGUI detailHpText;
    [SerializeField] private TextMeshProUGUI detailSkillText;
    [SerializeField] private GameObject detailPanel;

    [Header("按钮")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmButtonLabel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button closeButton;

    // 当前已上阵的角色 ID（来自持久化存档）
    private string _equippedCharId;
    // 列表中选中的角色 ID
    private string _selectedCharId;
    private int _selectedIndex = -1;
    private readonly List<CharacterSelectionElement> _cells = new List<CharacterSelectionElement>();

    public override void OnOpen(object payload)
    {
        // 读已上阵角色
        PlayerProfileService.Instance.LoadOrCreate();
        _equippedCharId = PlayerProfileService.Instance.EquippedCharacterId;

        // 数据
        if (characterCatalog == null)
            characterCatalog = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");

        // 字体——UIManager 已在 Open 时 ApplyToHierarchy，此处兜底
        BattleChineseFontRuntime.ApplyToHierarchy(transform);

        // 按钮监听
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        PopulateList();
        AutoSelectDefault();
    }

    public override void OnClose()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (startGameButton != null) startGameButton.onClick.RemoveListener(OnStartGameClicked);
        ClearCells();
    }

    // ── 列表 ──────────────────────────────────────────

    private void PopulateList()
    {
        ClearCells();

        if (characterCatalog == null || characterCatalog.characters.Count == 0)
        {
            if (detailPanel != null) detailPanel.SetActive(false);
            return;
        }

        // 排序：已上阵 > 可上阵 > 未解锁
        var sorted = new List<CharacterDefinition>(characterCatalog.characters);
        sorted.Sort((a, b) =>
        {
            int Priority(CharacterDefinition c)
            {
                if (c == null) return 3;
                if (c.characterId == _equippedCharId) return 0;
                if (!c.locked) return 1;
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
                    !_cells[i].CharacterDef.locked)
                {
                    SelectCell(i, isEquipped: true);
                    return;
                }
            }
        }

        // 其次选第一个未锁定角色
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].CharacterDef != null && !_cells[i].CharacterDef.locked)
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
        if (cell.CharacterDef == null || cell.CharacterDef.locked)
            return;

        bool isEquipped = cell.CharacterDef.characterId == _equippedCharId;
        SelectCell(cell.Index, isEquipped);
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
    }

    // ── 详情 ──────────────────────────────────────────

    private void ShowDetail(CharacterDefinition def)
    {
        if (detailPanel != null)
            detailPanel.SetActive(true);

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

        if (detailAttackText != null)
            detailAttackText.text = $"攻击：{def.baseAttack:F0}";

        float hp = def.baseHp + def.maxHpBonus;
        if (detailHpText != null)
            detailHpText.text = $"血量：{hp:F0}";

        if (detailSkillText != null)
        {
            SkillId effectiveSkill = def.defaultWeapon != null && def.defaultWeapon.weaponSkillId != SkillId.None
                ? def.defaultWeapon.weaponSkillId
                : def.startingSkill;

            var skillDef = skillCatalog != null ? skillCatalog.Get(effectiveSkill) : null;
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
        string msg = $"是否选择 <b><color=#FFD700><size=+16>「{charName}」</size></color></b> \n进入「{levelLabel}」？";

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
}
