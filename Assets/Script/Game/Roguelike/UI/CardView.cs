using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单张卡牌UI：根据模板动态显示技能信息
/// </summary>
public class CardView : MonoBehaviour
{
    [Header("UI组件")]
    public Image background;
    public Image icon;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI labelText;
    public Image labelIcon;
    public Image borderGlow;

    [Header("点击")]
    [Tooltip("可为空：自动取同物体上的 Button；点击与 Animator 无关。")]
    public Button clickButton;

    [Header("动画（可选）")]
    public Animator animator;

    private Action _onClick;

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

    /// <summary>
    /// 绑定卡牌数据
    /// </summary>
    public void Bind(CardDeck.DrawResult data, Action onClick)
    {
        _onClick = onClick;
        StopClickPropagationFromDecorations();
        WireButton();

        if (data == null) return;

        var tmpl = data.template;
        if (tmpl == null)
        {
            if (labelText != null) labelText.text = "";
            if (titleText != null) titleText.text = data.skillDef != null ? data.skillDef.displayName : "";
            if (descText != null) descText.text = GetLevelUpPreview(data);
            if (icon != null && data.skillDef != null) icon.sprite = data.skillDef.icon;
            gameObject.SetActive(true);
            return;
        }

        // 应用样式
        var style = tmpl.style;
        if (style != null)
        {
            if (background != null) background.sprite = style.background;
            borderGlow.gameObject.SetActive(false);
     //       if (borderGlow != null)
      //      {
      //          borderGlow.color = style.borderGlowColor;
      //          borderGlow.gameObject.SetActive(style.rarityStars > 0);
      //      }

            labelIcon.gameObject.SetActive(false);
            if (labelIcon != null)
            {
                labelIcon.sprite = style.labelIcon;
                labelIcon.color = style.labelColor;
                labelIcon.gameObject.SetActive(true);
            }
        }

        // 标签文字
        if (labelText != null)
            labelText.text = tmpl.labelText;

        // 技能图标
        if (icon != null && data.skillDef != null)
            icon.sprite = data.skillDef.icon;

        // 标题（动态替换格式）
        if (titleText != null && data.skillDef != null)
        {
            string title = string.Format(tmpl.titleFormat, data.skillDef.displayName);
            titleText.text = title;
        }

        // 描述（显示等级变化）
        if (descText != null)
        {
            string desc = string.Format(tmpl.descriptionFormat,
                data.currentLevel,
                GetLevelUpPreview(data));
            descText.text = desc;
        }

        // 播放入场动画
        if (animator != null)
            animator.SetTrigger("Show");

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>选卡描述：各等级一行（数据来自 SkillDefinitionBase.levelDescriptions 或数值表自动生成）。</summary>
    private string GetLevelUpPreview(CardDeck.DrawResult data)
    {
        if (data.skillDef == null)
            return data.currentLevel == 0 ? "新技能！" : $"升级到 Lv.{data.targetLevel}";

        int max = Mathf.Max(1, data.skillDef.maxLevel);
        if (data.currentLevel == 0)
            return data.skillDef.FormatAllLevelDescriptions(highlightLevel: 1);

        if (data.targetLevel >= max)
            return data.skillDef.FormatAllLevelDescriptions(highlightLevel: max);

        return data.skillDef.FormatAllLevelDescriptions(highlightLevel: data.targetLevel);
    }

    /// <summary>
    /// 点击事件（由 Button 或 Inspector 绑定触发）
    /// </summary>
    public void OnClick()
    {
        UiClickSound.Play();
        if (animator != null)
            animator.SetTrigger("Selected");

        _onClick?.Invoke();
    }

    /// <summary>
    /// 子层 Image/Text 若 raycastTarget=true 会拦截射线，导致根节点 <see cref="Button"/> 收不到点击。
    /// </summary>
    private void StopClickPropagationFromDecorations()
    {
        if (borderGlow != null) borderGlow.raycastTarget = false;
        if (labelIcon != null) labelIcon.raycastTarget = false;
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
