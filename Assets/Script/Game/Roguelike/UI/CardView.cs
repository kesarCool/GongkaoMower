using System;
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

        if (animator != null)
            animator.SetTrigger("Show");

        gameObject.SetActive(true);
    }

    public void Hide()
    {
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
