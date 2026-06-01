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

    private Coroutine _highlightCo;
    private Vector3 _normalScale = Vector3.one;

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
        if (emptyState != null) emptyState.SetActive(false);
    }

    public void Bind(Sprite icon, int level)
    {
        bool hasSkill = level > 0;

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            iconImage.transform.localScale = Vector3.one;
            iconImage.sprite = icon;
            iconImage.enabled = hasSkill && icon != null;
        }

        if (levelText != null)
        {
            levelText.text = hasSkill ? $"Lv.{level}" : "";
        }

        if (emptyState != null)
            emptyState.SetActive(!hasSkill);
    }

    /// <summary>闪烁高亮：提示该技能可被羁绊突破（金色脉冲 + 缩放弹跳）。</summary>
    public void SetHighlight(bool on)
    {
        if (iconImage == null) return;
        if (on)
        {
            _normalScale = iconImage.transform.localScale;
            if (_highlightCo == null)
                _highlightCo = StartCoroutine(FlashRoutine());
        }
        else
        {
            if (_highlightCo != null) { StopCoroutine(_highlightCo); _highlightCo = null; }
            iconImage.color = Color.white;
            iconImage.transform.localScale = _normalScale;
        }
    }

    private IEnumerator FlashRoutine()
    {
        var rt = iconImage.transform;
        while (true)
        {
            float t = Mathf.PingPong(Time.unscaledTime * 8f, 1f);
            // 白色 ↔ 金色 快速切换
            iconImage.color = Color.Lerp(Color.white, new Color(1f, 0.75f, 0.1f, 1f), t);
            // 缩放弹跳 1.0 ↔ 1.25
            rt.localScale = Vector3.Lerp(_normalScale, _normalScale * 1.25f, t);
            yield return null;
        }
    }
}
