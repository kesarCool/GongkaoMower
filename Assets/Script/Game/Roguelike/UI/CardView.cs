using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单张卡牌UI：根据 RoguelikeCardTemplate 显示技能信息。
/// </summary>
public class CardView : MonoBehaviour
{
    [Header("UI组件")]
    public Image background;
    public Image icon;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI labelText;

    [Header("点击")]
    public Button clickButton;

    [Header("羁绊展示（被动卡专用）")]
    [Tooltip("羁绊主动技能图标容器。")]
    public Transform bondedGroup;
    [Tooltip("羁绊主动技能 icon 预制体（仅 icon 的 SkillSlotCell）。")]
    public SkillSlotCell bondedIconPrefab;

    [Header("动画（可选）")]
    public Animator animator;

    private Action _onClick;
    private readonly List<SkillSlotCell> _bondedCells = new List<SkillSlotCell>(4);

    private void Awake()
    {
        if (clickButton == null)
            clickButton = GetComponent<Button>();

        if (descText != null)
        {
            descText.enableWordWrapping = true;
            descText.overflowMode = TextOverflowModes.Truncate;
        }
    }

    public void Bind(CardDeck.DrawResult data, Action onClick)
    {
        _onClick = onClick;
        StopClickPropagationFromDecorations();
        WireButton();

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

        // 羁绊展示：被动卡显示其关联的主动技能 icon
        RefreshBondedIcons(data);

        if (animator != null)
            animator.SetTrigger("Show");

        gameObject.SetActive(true);
    }

    private void RefreshBondedIcons(CardDeck.DrawResult data)
    {
        if (bondedGroup == null || bondedIconPrefab == null) return;
        bool isPassive = data.skillId.IsPassive();
        if (data.skillDef == null || !isPassive) { bondedGroup.gameObject.SetActive(false); return; }

        var ps = FindObjectOfType<PlayerSkills>();
        if (ps == null || ps.skillCatalog == null) { bondedGroup.gameObject.SetActive(false); return; }

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

        bondedGroup.gameObject.SetActive(bondedActives.Count > 0);

        // 触发闪烁：已上阵技能槽位 + 被动卡下羁绊图标（仅该被动未装备时才闪烁提醒）
        bool alreadyEquipped = ps.HasPassiveSkill(data.skillId);
        var csp = FindObjectOfType<CardSelectionPanel>();
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
        foreach (var c in _bondedCells)
            c.SetHighlight(false);
        gameObject.SetActive(false);
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

    public void OnClick()
    {
        UiClickSound.Play();
        if (animator != null)
            animator.SetTrigger("Selected");
        _onClick?.Invoke();
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
