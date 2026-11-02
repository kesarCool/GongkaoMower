using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单张卡牌UI：根据 RoguelikeCardTemplate 显示技能信息。
/// </summary>
public partial class CardView : MonoBehaviour
{
    [Header("UI组件")]
    public Image background;
    public Image icon;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI labelText;

    [Header("新版卡片展示（旧 Prefab 可留空）")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI gainLabelText;
    public TextMeshProUGUI gainValueText;
    public Image badgeImage;
    public Sprite passiveBadge, newBadge, upgradeBadge, breakthroughBadge;
    public Image dividerImage;
    public Image selectionFrame;
    public ScrollRect descriptionScroll;
    public GameObject descriptionScrollHint;
    public RectTransform bondedPanel;
    public SkillStarRow starRow;
    public RectTransform pressVisual;

    public int BondedRowCount { get; private set; }
    public SkillId BoundSkillId { get; private set; }
    private PlayerSkills _ownerSkills;
    private CardSelectionPanel _ownerPanel;

    public void SetContext(PlayerSkills skills, CardSelectionPanel panel)
    {
        _ownerSkills = skills;
        _ownerPanel = panel;
    }

    [Header("点击")]
    public Button clickButton;

    [Header("羁绊展示（被动卡专用）")]
    [Tooltip("羁绊主动技能图标容器。")]
    public Transform bondedGroup;
    [Tooltip("羁绊主动技能 icon 预制体（仅 icon 的 SkillSlotCell）。")]
    public SkillSlotCell bondedIconPrefab;

    [Header("动画（可选）")]
    public Animator animator;

    [Header("点击反馈")]
    [Tooltip("选中后目标缩放（1.12=放大12%）。")]
    [SerializeField] private float clickTargetScale = 1.12f;
    [Tooltip("动画持续时间（秒）。")]
    [SerializeField] private float clickAnimDuration = 0.3f;
    [Tooltip("未选中卡片淡出时间（秒）。")]
    [SerializeField] private float fadeOutDuration = 0.2f;
    [Tooltip("CanvasGroup（用于淡出），未赋值则自动 GetOrAdd。")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Action _onClick;
    private Coroutine _clickBounceRoutine;
    private Color _restBgColor;
    private bool _restColorCached;
    private readonly List<SkillSlotCell> _bondedCells = new List<SkillSlotCell>(4);
    /// <summary>缓存同父级下的兄弟 CardView，避免每次点击都 GetComponentsInChildren。</summary>
    private CardView[] _cachedSiblings;

    // 缓存 FindObjectOfType 结果，避免每张卡 Bind 时全场景扫描
    private static PlayerSkills _cachedPlayerSkills;
    private static CardSelectionPanel _cachedCardSelectionPanel;

    private static void CacheStatics()
    {
        if (_cachedPlayerSkills == null) _cachedPlayerSkills = FindObjectOfType<PlayerSkills>();
        if (_cachedCardSelectionPanel == null) _cachedCardSelectionPanel = FindObjectOfType<CardSelectionPanel>();
    }

    private void Awake()
    {
        if (clickButton == null)
            clickButton = GetComponent<Button>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (descText != null)
        {
            descText.enableWordWrapping = true;
            descText.overflowMode = descriptionScroll != null ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
        }
    }

    public void Bind(CardDeck.DrawResult data, Action onClick)
    {
        if (_clickBounceRoutine != null) { StopCoroutine(_clickBounceRoutine); _clickBounceRoutine = null; }
        BoundSkillId = data != null ? data.skillId : SkillId.None;
        _onClick = onClick;
        StopClickPropagationFromDecorations();
        WireButton();

        // 重置上轮选卡残留状态
        ResetClickFeedbackState();

        if (data == null) return;

        var tmpl = data.template;

        // 背景
        if (tmpl != null && background != null && tmpl.background != null)
            background.sprite = tmpl.background;

        // 标签文字 + 颜色
        if (labelText != null)
        {
            labelText.text = tmpl != null ? tmpl.labelText : "";
            labelText.color = tmpl != null ? tmpl.labelColor : Color.white;
        }

        // 技能图标
        if (icon != null && data.skillDef != null)
            icon.sprite = data.skillDef.icon;

        // 标题
        if (titleText != null && data.skillDef != null)
            titleText.text = data.skillDef.displayName;

        // 描述
        if (descText != null)
            descText.text = GetLevelUpPreview(data);

        if (levelText != null)
        {
            var content = SkillCardPresentation.Build(data);
            levelText.text = content.level;
            if (gainLabelText != null)
            {
                gainLabelText.text = content.gainLabel;
                gainLabelText.enableAutoSizing = true;
                gainLabelText.fontSizeMin = 28;
                gainLabelText.fontSizeMax = 34;
            }
            if (gainValueText != null)
            {
                gainValueText.text = content.gainValue;
                gainValueText.fontSizeMax = data.currentLevel > 0 ? 36 : 62;
            }
            if (descText != null) descText.text = content.description;
            Color accent = tmpl != null ? tmpl.labelColor : new Color32(49, 94, 123, 255);
            levelText.color = accent;
            if (gainLabelText != null) gainLabelText.color = accent;
            if (gainValueText != null) gainValueText.color = accent;
            if (dividerImage != null) dividerImage.color = new Color(accent.r, accent.g, accent.b, 0.2f);
            if (badgeImage != null)
                badgeImage.sprite = data.skillId.IsPassive() ? passiveBadge : data.currentLevel == 0 ? newBadge :
                    data.skillDef != null && data.targetLevel >= data.skillDef.maxLevel ? breakthroughBadge : upgradeBadge;
        }

        if (starRow != null)
        {
            starRow.Bind(data.currentLevel, data.targetLevel, data.skillDef != null ? data.skillDef.maxLevel : 5, true);
            if (levelText != null) levelText.gameObject.SetActive(false);
        }

        // 羁绊展示：被动卡显示其关联的主动技能 icon
        RefreshBondedIcons(data);

        if (animator != null)
            animator.SetTrigger("Show");

        gameObject.SetActive(true);
        RefreshDescriptionScroll();
    }

    private void RefreshDescriptionScroll()
    {
        if (descriptionScroll == null || descText == null) return;
        float height = Mathf.Max(descriptionScroll.viewport.rect.height, descText.preferredHeight);
        descText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        descriptionScroll.verticalNormalizedPosition = 1;
        if (descriptionScrollHint != null)
            descriptionScrollHint.SetActive(height > descriptionScroll.viewport.rect.height + 2);
    }

    private void SetBondedVisible(bool visible)
    {
        BondedRowCount = 0;
        if (bondedPanel != null) bondedPanel.gameObject.SetActive(visible);
        else if (bondedGroup != null) bondedGroup.gameObject.SetActive(visible);
    }

    private void RefreshBondedIcons(CardDeck.DrawResult data)
    {
        SetBondedVisible(false);
        if (bondedGroup == null || bondedIconPrefab == null) return;
        bool isPassive = data.skillId.IsPassive();
        if (data.skillDef == null || !isPassive) return;

        CacheStatics();
        var ps = _ownerSkills != null ? _ownerSkills : _cachedPlayerSkills;
        if (ps == null || ps.skillCatalog == null) return;

        // 收集已装备主动技能家族：family → 已装备的 SkillId
        var equippedFamilies = new Dictionary<SkillId, SkillId>();
        var activeIds = new List<SkillId>(5);
        ps.GetEquippedSkillIdsOrdered(activeIds);
        foreach (var id in activeIds)
        {
            var def = ps.skillCatalog.Get(id);
            if (def != null && def.SkillFamily != id)
                equippedFamilies[def.SkillFamily] = id;
        }

        // 获取当前关卡 + 解锁进度
        int currentLevel = BattleLevelContext.LevelId;
        CardPoolProgression progression = null;
        var rcm = RoguelikeCardManager.Instance;
        if (rcm != null && rcm.cardDeck != null)
            progression = rcm.cardDeck.progression;

        // 查找羁绊到本被动的主动技能，过滤掉本局不会出在卡池的
        var bondedActives = new List<SkillDefinitionBase>(4);
        foreach (var def in ps.skillCatalog.All())
        {
            if (def == null) continue;
            if (def.id.IsPassive()) continue;
            if (def.bondedPassiveId != data.skillId) continue;

            // 关卡未解锁 → 排除
            if (progression != null && !progression.IsUnlocked(def.id, currentLevel))
                continue;

            SkillId family = def.SkillFamily;
            // 家族互斥（与 CardDeck.Draw 一致）
            if (family != def.id || equippedFamilies.ContainsKey(family))
            {
                if (!equippedFamilies.TryGetValue(family, out SkillId allowedId) || def.id != allowedId)
                    continue;
            }

            bondedActives.Add(def);
        }

        // 先清除上一次选中卡的残留高亮
        foreach (var c in _bondedCells) c.SetHighlight(false);

        SetBondedVisible(bondedActives.Count > 0);
        BondedRowCount = Mathf.CeilToInt(bondedActives.Count / 3f);
        if (bondedPanel != null)
            bondedPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 128 + Mathf.Max(0, BondedRowCount - 1) * 84);

        // 触发闪烁：已上阵技能槽位 + 被动卡下羁绊图标（仅该被动未装备时才闪烁提醒）
        bool alreadyEquipped = ps.HasPassiveSkill(data.skillId);
        var csp = _ownerPanel != null ? _ownerPanel : _cachedCardSelectionPanel;
        var matchedIds = new List<SkillId>(4);
        if (!alreadyEquipped)
        {
            foreach (var bonded in bondedActives)
            {
                if (activeIds.Contains(bonded.id))
                    matchedIds.Add(bonded.id);
            }
        }

        while (_bondedCells.Count < bondedActives.Count)
        {
            var cell = Instantiate(bondedIconPrefab, bondedGroup, false);
            _bondedCells.Add(cell);
        }

        for (int i = 0; i < _bondedCells.Count; i++)
        {
            if (i < bondedActives.Count)
            {
                _bondedCells[i].gameObject.SetActive(true);
                _bondedCells[i].BindIconOnly(bondedActives[i].icon);
                if (bondedPanel != null)
                {
                    int countInRow = Mathf.Min(3, bondedActives.Count - (i / 3) * 3);
                    var rt = (RectTransform)_bondedCells[i].transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                    rt.sizeDelta = new Vector2(72, 72);
                    rt.anchoredPosition = new Vector2((276 - (countInRow * 72 + (countInRow - 1) * 12)) * 0.5f + i % 3 * 84, -(i / 3) * 84);
                }
                if (matchedIds.Contains(bondedActives[i].id))
                    _bondedCells[i].SetHighlight(true);
            }
            else
            {
                _bondedCells[i].SetHighlight(false);
                _bondedCells[i].gameObject.SetActive(false);
            }
        }

        // 对应主动技能槽位闪烁
        if (csp != null)
        {
            foreach (var id in matchedIds)
                csp.HighlightActiveSlot(id, true);
        }
    }

    public void Hide()
    {
        _cachedSiblings = null;
        foreach (var c in _bondedCells)
            c.SetHighlight(false);
        if (_clickBounceRoutine != null) { StopCoroutine(_clickBounceRoutine); _clickBounceRoutine = null; }
        ResetClickFeedbackState();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_clickBounceRoutine != null) { StopCoroutine(_clickBounceRoutine); _clickBounceRoutine = null; }
        ResetClickFeedbackState();
    }

    private string GetLevelUpPreview(CardDeck.DrawResult data)
    {
        if (data.skillDef == null)
            return data.currentLevel == 0 ? "新技能！" : $"升级到 Lv.{data.targetLevel}";

        int max = Mathf.Max(1, data.skillDef.maxLevel);
        if (data.currentLevel == 0)
            return data.skillDef.FormatAllLevelDescriptions(highlightLevel: 1);

        if (data.targetLevel >= max)
            return data.skillDef.FormatAllLevelDescriptions(highlightLevel: max) + "\n满级突破！";

        return data.skillDef.FormatAllLevelDescriptions(highlightLevel: data.targetLevel);
    }

    private CardView[] GetSiblingCardViews()
    {
        if (_cachedSiblings != null) return _cachedSiblings;
        if (transform.parent == null) return new CardView[] { this };
        _cachedSiblings = transform.parent.GetComponentsInChildren<CardView>();
        return _cachedSiblings;
    }

    private void ResetClickFeedbackState()
    {
        ResetPressState();
        transform.localScale = Vector3.one;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (clickButton != null) clickButton.interactable = true;
        if (background != null && _restColorCached)
            background.color = _restBgColor;
        if (selectionFrame != null) selectionFrame.gameObject.SetActive(false);
    }

    private void StopClickPropagationFromDecorations()
    {
        if (icon != null) icon.raycastTarget = false;
        if (titleText != null) titleText.raycastTarget = false;
        if (descText != null) descText.raycastTarget = false;
        if (labelText != null) labelText.raycastTarget = false;
        if (background != null) background.raycastTarget = true;
    }

    private void WireButton()
    {
        if (clickButton == null)
            clickButton = GetComponent<Button>();
        if (clickButton == null) return;
        if (clickButton.targetGraphic == null && background != null)
            clickButton.targetGraphic = background;
        clickButton.onClick.RemoveAllListeners();
        clickButton.onClick.AddListener(OnClick);
    }
}
