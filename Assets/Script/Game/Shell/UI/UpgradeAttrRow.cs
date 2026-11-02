using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 升级属性行：名称 | 当前值 → 下级值（满级时隐藏箭头和下级）。
/// 放在 Prefab 上，由 CharacterSelectionPanel 动态实例化 8 个。
/// </summary>
public class UpgradeAttrRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI curText;
    [SerializeField] private TextMeshProUGUI nextText;
    [SerializeField] private GameObject arrow;
    [SerializeField] private bool homeTheme;
    private Coroutine growthRoutine;
    private TextMeshProUGUI deltaText;
    private Color restColor;
    private bool hasRestColor;
    private string finalText;
    private Vector2 deltaRestPosition;

    public float DisplayedValue { get; private set; }
    public float TargetValue { get; private set; }
    public bool IsAnimating => growthRoutine != null;
    public string DisplayedText => curText != null ? curText.text : "";
    public string DeltaText => deltaText != null && deltaText.gameObject.activeSelf ? deltaText.text : "";

    public void SetPresentation(int index)
    {
        if (!homeTheme) return;
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.enabled = index % 2 == 0;
        if (nameText != null) nameText.fontStyle = index < 3 ? FontStyles.Bold : FontStyles.Normal;
        if (curText != null) curText.fontStyle = index < 3 ? FontStyles.Bold : FontStyles.Normal;
    }

    /// <summary>
    /// 绑定一行属性。
    /// </summary>
    /// <param name="attrName">属性名（"攻击"/"血量"/…）</param>
    /// <param name="curValue">当前值字符串（如 "15"）</param>
    /// <param name="nextValue">下级值字符串（满级时传空）</param>
    /// <param name="isMax">是否满级</param>
    /// <param name="nextColor">下级值颜色（默认绿色表示成长）</param>
    public void Bind(string attrName, string curValue, string nextValue, bool isMax, string nextColor = "#88FF88")
    {
        CompleteAnimation();
        finalText = curValue;
        if (nameText != null) nameText.text = attrName;

        if (curText != null) curText.text = curValue;

        bool hasNext = !isMax && !string.IsNullOrEmpty(nextValue);
        if (homeTheme) nextColor = curValue == nextValue ? "#76634C" : "#3D7047";
        if (nextText != null)
        {
            nextText.text = hasNext ? $"<color={nextColor}>{nextValue}</color>" : "";
            nextText.gameObject.SetActive(hasNext);
        }
        if (arrow != null) arrow.SetActive(hasNext);
    }

    public void BindNumeric(CharacterGrowthValue current, CharacterGrowthValue next, bool isMax)
    {
        Bind(current.Name, current.FormattedValue, next.FormattedValue, isMax);
        DisplayedValue = TargetValue = current.Value;
    }

    public void AnimateFrom(CharacterGrowthValue before, CharacterGrowthValue after, float duration = .5f)
    {
        CompleteAnimation();
        TargetValue = after.Value;
        DisplayedValue = before.Value;
        finalText = after.FormattedValue;
        string delta = after.FormatDeltaFrom(before);
        if (curText == null || !isActiveAndEnabled || string.IsNullOrEmpty(delta))
        {
            CompleteAnimation();
            return;
        }
        if (!hasRestColor) { restColor = curText.color; hasRestColor = true; }
        EnsureDeltaText();
        if (deltaText != null)
        {
            deltaText.text = delta;
            deltaText.gameObject.SetActive(true);
            deltaText.rectTransform.anchoredPosition = deltaRestPosition;
        }
        curText.text = before.FormattedValue;
        growthRoutine = StartCoroutine(RollValue(before, after, Mathf.Max(.05f, duration)));
    }

    IEnumerator RollValue(CharacterGrowthValue before, CharacterGrowthValue after, float duration)
    {
        float elapsed = 0, lifetime = duration + .2f;
        Color green = new Color(.18f, .55f, .29f);
        while (elapsed < lifetime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1 - (1 - t) * (1 - t);
            DisplayedValue = Mathf.Lerp(before.Value, after.Value, eased);
            curText.text = after.FormatValue(DisplayedValue);
            curText.color = Color.Lerp(green, restColor, Mathf.Clamp01((elapsed - duration) / .2f));
            if (deltaText != null)
            {
                float fade = 1 - Mathf.Clamp01((elapsed - duration * .65f) / (lifetime - duration * .65f));
                deltaText.color = new Color(green.r, green.g, green.b, fade);
                deltaText.rectTransform.anchoredPosition = deltaRestPosition + Vector2.up * (elapsed / lifetime * 6);
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        growthRoutine = null;
        CompleteAnimation();
    }

    void EnsureDeltaText()
    {
        if (deltaText != null || curText == null) return;
        var go = new GameObject("GrowthDelta", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        deltaText = go.AddComponent<TextMeshProUGUI>();
        deltaText.font = curText.font;
        deltaText.fontSharedMaterial = curText.fontSharedMaterial;
        deltaText.fontSize = curText.fontSize * .76f;
        deltaText.fontStyle = FontStyles.Bold;
        deltaText.alignment = TextAlignmentOptions.Right;
        deltaText.raycastTarget = false;
        deltaText.enableWordWrapping = false;
        var rt = deltaText.rectTransform;
        rt.anchorMin = curText.rectTransform.anchorMin;
        rt.anchorMax = curText.rectTransform.anchorMax;
        rt.pivot = curText.rectTransform.pivot;
        rt.sizeDelta = new Vector2(140, curText.rectTransform.sizeDelta.y);
        deltaRestPosition = curText.rectTransform.anchoredPosition + Vector2.left * 150 + Vector2.down * 4;
        rt.anchoredPosition = deltaRestPosition;
        go.SetActive(false);
    }

    public void CompleteAnimation()
    {
        if (growthRoutine != null) { StopCoroutine(growthRoutine); growthRoutine = null; }
        DisplayedValue = TargetValue;
        if (curText != null)
        {
            if (finalText != null) curText.text = finalText;
            if (hasRestColor) curText.color = restColor;
        }
        if (deltaText != null) deltaText.gameObject.SetActive(false);
    }

    void OnDisable() { CompleteAnimation(); }
}
