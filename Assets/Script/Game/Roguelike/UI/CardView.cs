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
    private static bool _staticsCached;

    private static void CacheStatics()
    {
        if (_staticsCached) return;
        _cachedPlayerSkills = FindObjectOfType<PlayerSkills>();
        _cachedCardSelectionPanel = FindObjectOfType<CardSelectionPanel>();
        _staticsCached = true;
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
            descText.overflowMode = TextOverflowModes.Truncate;
        }
    }

    public void Bind(CardDeck.DrawResult data, Action onClick)
    {
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

        CacheStatics();
        var ps = _cachedPlayerSkills;
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
        var csp = _cachedCardSelectionPanel;
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

    public void OnClick()
    {
        UiClickSound.Play();
        if (animator != null)
            animator.SetTrigger("Selected");

        // 防止点击穿透 / 双击（所有卡按钮一次性锁定）
        var allCards = GetSiblingCardViews();
        foreach (var cv in allCards)
        {
            if (cv.clickButton != null) cv.clickButton.interactable = false;
        }
        if (_clickBounceRoutine != null) return;

        _clickBounceRoutine = StartCoroutine(ClickFeedbackThenConfirm(allCards));
    }

    /// <summary>选中卡放大+亮白；其余卡淡出。动画结束后触发回调。</summary>
    private System.Collections.IEnumerator ClickFeedbackThenConfirm(CardView[] siblings)
    {
        // 选中卡背景直接设纯白
        if (background != null)
        {
            if (!_restColorCached) { _restBgColor = background.color; _restColorCached = true; }
            background.color = Color.white;
        }

        const float stepRate = 30f;
        float stepTime = 1f / stepRate;
        float elapsed = 0f;

        while (elapsed < clickAnimDuration)
        {
            yield return new WaitForSecondsRealtime(stepTime);
            elapsed += stepTime;

            float t = Mathf.Clamp01(elapsed / clickAnimDuration);

            // 选中卡：平滑放大到目标倍率
            float s = Mathf.Lerp(1f, clickTargetScale, t);
            transform.localScale = new Vector3(s, s, 1f);

            // 其余卡：淡出
            float fadeT = Mathf.Clamp01(elapsed / fadeOutDuration);
            float targetAlpha = 1f - fadeT;
            foreach (var cv in siblings)
            {
                if (cv == this) continue;
                if (cv.canvasGroup != null)
                    cv.canvasGroup.alpha = targetAlpha;
            }
        }

        GameLog.Info($"[CardView] ClickFeedback END — invoking callback");
        _clickBounceRoutine = null;
        _onClick?.Invoke();
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
        transform.localScale = Vector3.one;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (clickButton != null) clickButton.interactable = true;
        if (background != null && _restColorCached)
            background.color = _restBgColor;
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
