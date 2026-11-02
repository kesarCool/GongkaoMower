using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能槽位 Cell（选卡面板 HUD）：icon + 等级，主动/被动通用。
/// </summary>
public class SkillSlotCell : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private GameObject emptyState;

    [Header("选卡新版皮肤（其他复用面板可留空）")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Sprite activeSprite, passiveSprite, emptySprite;
    [SerializeField] private GameObject levelBadge;
    [SerializeField] private Image highlightFrame;
    public SkillStarRow starRow;
    public Image shimmerImage;
    private bool _isPassive;

    public void SetPassive(bool passive) { _isPassive = passive; }

    private Coroutine _highlightCo;
    private Vector3 _normalScale = Vector3.one;
    private Vector2 _normalPosition;
    private Color _restIconColor = Color.white;

    private void Awake()
    {
        if (iconImage != null) _normalPosition = iconImage.rectTransform.anchoredPosition;
    }

    /// <summary>仅显示 icon，不显示等级和空状态（羁绊展示用）。</summary>
    public void BindIconOnly(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.color = Color.white;
            iconImage.transform.localScale = Vector3.one;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
        if (levelText != null) levelText.text = "";
        if (levelBadge != null) levelBadge.SetActive(false);
        if (starRow != null) starRow.gameObject.SetActive(false);
        if (emptyState != null) emptyState.SetActive(false);
    }

    public void Bind(Sprite icon, int level, int maxLevel = 5)
    {
        bool hasSkill = level > 0;
        if (slotBackground != null)
            slotBackground.sprite = hasSkill ? (_isPassive ? passiveSprite : activeSprite) : emptySprite;
        if (levelBadge != null) levelBadge.SetActive(hasSkill && starRow == null);
        if (starRow != null)
        {
            starRow.Bind(level, level, maxLevel, false);
            starRow.gameObject.SetActive(hasSkill);
            if (levelText != null) levelText.gameObject.SetActive(false);
        }

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            iconImage.transform.localScale = Vector3.one;
            iconImage.sprite = icon;
            iconImage.enabled = hasSkill && icon != null;
        }

        if (levelText != null)
        {
            levelText.gameObject.SetActive(starRow == null);
            levelText.text = hasSkill ? $"Lv.{level}" : "";
        }

        if (emptyState != null)
            emptyState.SetActive(!hasSkill);
    }

    /// <summary>设置突破满级态：icon 变红。</summary>
    public void SetBreakthrough(bool on)
    {
        _restIconColor = Color.white;
        if (iconImage != null)
        {
            // Stars carry the red max-level state; retain the original readable icon.
            iconImage.color = starRow != null ? Color.white : on ? new Color(1f, .2f, .15f) : Color.white;
            _restIconColor = iconImage.color;
        }
    }

    /// <summary>闪烁高亮：提示该技能可被羁绊突破（金色脉冲 + 缩放弹跳）。</summary>
    public void SetHighlight(bool on)
    {
        if (iconImage == null) return;
        if (highlightFrame != null) highlightFrame.gameObject.SetActive(on);
        if (on)
        {
            if (_highlightCo == null && Application.isPlaying && isActiveAndEnabled)
                _highlightCo = StartCoroutine(FlashRoutine());
        }
        else
        {
            if (_highlightCo != null) { StopCoroutine(_highlightCo); _highlightCo = null; }
            iconImage.color = _restIconColor;
            iconImage.transform.localScale = _normalScale;
            iconImage.rectTransform.anchoredPosition = _normalPosition;
            if (shimmerImage != null) shimmerImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator FlashRoutine()
    {
        var rt = iconImage.transform;
        while (true)
        {
            float phase = Mathf.Repeat(Time.unscaledTime * 1.35f, 1f);
            float t = Mathf.Sin(phase * Mathf.PI);
            if (highlightFrame != null)
            {
                highlightFrame.color = new Color(1, 1, 1, Mathf.Lerp(.4f, 1, t));
            }
            iconImage.color = Color.Lerp(_restIconColor, new Color(1f, .8f, .42f), t * .55f);
            rt.localScale = _normalScale * Mathf.Lerp(1f, 1.22f, t);
            iconImage.rectTransform.anchoredPosition = _normalPosition + Vector2.up * (5 * t);
            if (shimmerImage != null)
            {
                shimmerImage.gameObject.SetActive(true);
                shimmerImage.color = new Color(1, 1, 1, Mathf.Sin(phase * Mathf.PI) * .78f);
                float width = iconImage.rectTransform.rect.width;
                shimmerImage.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-width, width, phase), 0);
            }
            yield return null;
        }
    }

    private void OnDisable()
    {
        if (_highlightCo != null) { StopCoroutine(_highlightCo); _highlightCo = null; }
        SetHighlight(false);
    }
}
