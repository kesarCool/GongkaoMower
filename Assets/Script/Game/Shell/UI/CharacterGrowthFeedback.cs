using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Display values captured from the same character data and saved level as the character page.</summary>
public struct CharacterGrowthValue
{
    public string Name { get; private set; }
    public float Value { get; private set; }
    public string Prefix { get; private set; }
    public string Suffix { get; private set; }
    public string NumberFormat { get; private set; }
    public string FormattedValue => FormatValue(Value);
    public CharacterGrowthValue(string name, float value, string prefix = "", string suffix = "", string numberFormat = "0.##")
    { Name = name; Value = value; Prefix = prefix; Suffix = suffix; NumberFormat = numberFormat; }
    public string FormatValue(float value) => Prefix + value.ToString(NumberFormat ?? "0.##", CultureInfo.InvariantCulture) + Suffix;
    public string FormatDeltaFrom(CharacterGrowthValue before)
    {
        // Subtract the values shown on the panel, after each endpoint's own rounding.
        // Rounding the raw difference instead makes 43 -> 46 show +2.76 on a linear curve.
        decimal previous = before.DisplayNumber;
        decimal difference = DisplayNumber - previous;
        if (difference == 0) return "";
        return (difference > 0 ? "+" : "−")
            + Math.Abs(difference).ToString(NumberFormat ?? "0.##", CultureInfo.InvariantCulture) + Suffix;
    }
    private decimal DisplayNumber => decimal.Parse(Value.ToString(NumberFormat ?? "0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}

/// <summary>An operation's before/after snapshot; animation never reads a changing selection or charges currency.</summary>
public sealed class CharacterGrowthSnapshot
{
    public readonly string CharacterId;
    public readonly int Level, Stage, MaxLevel;
    public readonly CharacterGrowthValue[] Stats;
    public readonly string UnlockedAbility;
    CharacterGrowthSnapshot(string id, int level, int stage, int maxLevel, CharacterGrowthValue[] stats, string ability)
    { CharacterId = id; Level = level; Stage = stage; MaxLevel = maxLevel; Stats = stats; UnlockedAbility = ability; }

    public static CharacterGrowthSnapshot Capture(CharacterDefinition def)
    {
        var service = PlayerProfileService.Instance;
        int level = service.GetHeroLevel(def.characterId);
        int stage = def.upgradeData != null ? service.GetHeroStage(def.characterId) : 0;
        int maxLevel = def.upgradeData != null ? service.GetEffectiveMaxLevel(def.characterId, def.upgradeData) : 1;
        string ability = def.upgradeData == null ? "" : stage == 1 ? def.upgradeData.rareTraitDescription
            : stage >= 2 ? def.upgradeData.legendBreakthroughDescription : "";
        return new CharacterGrowthSnapshot(def.characterId, level, stage, maxLevel, EvaluateStats(def, level), ability);
    }

    public static CharacterGrowthValue[] EvaluateStats(CharacterDefinition def, int level)
    {
        var a = def.attributes.ApplyMinimums();
        var d = def.upgradeData;
        return new[]
        {
            new CharacterGrowthValue("攻击", a.attack * Mul(d, d != null ? d.attackMulAtMax : 1, level), "", "", "F0"),
            new CharacterGrowthValue("血量", a.maxHp * Mul(d, d != null ? d.maxHpMulAtMax : 1, level), "", "", "F0"),
            new CharacterGrowthValue("防御", a.defense * Mul(d, d != null ? d.defenseMulAtMax : 1, level), "", "", "F0"),
            new CharacterGrowthValue("移速", a.moveSpeed * Mul(d, d != null ? d.moveSpeedMulAtMax : 1, level), "", "", "F1"),
            new CharacterGrowthValue("暴击率", (a.critRate + Add(d, d != null ? d.critRateAddAtMax : 0, level)) * 100, "", "%", "F0"),
            new CharacterGrowthValue("暴伤", a.critDamageMul * Mul(d, d != null ? d.critDmgMulAtMax : 1, level), "×", "", "F1"),
            new CharacterGrowthValue("穿透率", (a.pierceRate + Add(d, d != null ? d.pierceRateAddAtMax : 0, level)) * 100, "", "%", "F0"),
            new CharacterGrowthValue("范围", a.attackRangeMul * Mul(d, d != null ? d.attackRangeMulAtMax : 1, level), "×", "", "F1"),
            new CharacterGrowthValue("破防率", (a.penRate + Add(d, d != null ? d.penRateAddAtMax : 0, level)) * 100, "", "%", "F0")
        };
    }
    static float Mul(HeroUpgradeData data, float max, int level) => data != null ? data.EvaluateMul(max, level) : 1;
    static float Add(HeroUpgradeData data, float max, int level) => data != null ? data.EvaluateAdd(max, level) : 0;
}

/// <summary>One committed upgrade/promotion presentation. No service writes, new textures or per-frame object creation.</summary>
[DisallowMultipleComponent]
public sealed class CharacterGrowthFeedback : MonoBehaviour
{
    public bool IsPlaying { get; private set; }
    public string Phase { get; private set; } = "Idle";
    public float RevealProgress { get; private set; }
    public CharacterGrowthSnapshot Before { get; private set; }
    public CharacterGrowthSnapshot After { get; private set; }
    Coroutine routine;
    IReadOnlyList<UpgradeAttrRow> rows;
    TMP_Text levelText;
    Vector3 levelScale, markScale;
    Image mark, platformLight, skillIcon;
    Vector3 skillIconScale;
    Color markColor;
    CanvasGroup banner;
    TMP_Text bannerTitle, bannerDescription;
    string finalLevelLabel;
    Action reveal, completed;
    bool revealed;

    public void PlayUpgrade(CharacterGrowthSnapshot before, CharacterGrowthSnapshot after,
        IReadOnlyList<UpgradeAttrRow> attributeRows, TMP_Text level, string oldLabel, string newLabel,
        CharacterPanelPresentation presentation, Action onCompleted)
    {
        Prepare(before, after, attributeRows, level, oldLabel, newLabel, presentation, null, onCompleted);
        for (int i = 0; i < rows.Count && i < before.Stats.Length && i < after.Stats.Length; i++)
            if (rows[i] != null) rows[i].AnimateFrom(before.Stats[i], after.Stats[i]);
        Phase = "Upgrade";
        routine = StartCoroutine(Play(false));
    }

    public void PlayPromotion(CharacterGrowthSnapshot before, CharacterGrowthSnapshot after,
        IReadOnlyList<UpgradeAttrRow> attributeRows, TMP_Text level, string oldLabel, string newLabel,
        CharacterPanelPresentation presentation, Action onReveal, Action onCompleted)
    {
        Prepare(before, after, attributeRows, level, oldLabel, newLabel, presentation, onReveal, onCompleted);
        EnsureBanner(level, presentation);
        Phase = "Charge";
        routine = StartCoroutine(Play(true));
    }

    void Prepare(CharacterGrowthSnapshot before, CharacterGrowthSnapshot after, IReadOnlyList<UpgradeAttrRow> attributeRows,
        TMP_Text level, string oldLabel, string newLabel, CharacterPanelPresentation presentation, Action onReveal, Action onCompleted)
    {
        CompleteImmediately();
        Before = before; After = after; rows = attributeRows;
        levelText = level; finalLevelLabel = newLabel;
        if (levelText != null) { levelScale = levelText.transform.localScale; levelText.text = oldLabel; }
        mark = presentation != null ? presentation.GetTierMark(after.Stage) : null;
        if (mark != null) { markScale = mark.transform.localScale; markColor = mark.color; }
        var skillRoot = presentation != null && presentation.skillTitle != null ? presentation.skillTitle.transform.parent : null;
        var iconTransform = skillRoot != null ? skillRoot.Find("SkillIcon") : null;
        skillIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (skillIcon != null) skillIconScale = skillIcon.transform.localScale;
        EnsurePlatformLight(presentation);
        reveal = onReveal; completed = onCompleted; revealed = false;
        RevealProgress = 0; IsPlaying = true;
    }

    IEnumerator Play(bool promotion)
    {
        float elapsed = 0, duration = promotion ? 1.05f : .7f;
        while (elapsed < duration)
        {
            if (promotion)
            {
                Phase = elapsed < .32f ? "Charge" : elapsed < .58f ? "Promote" : "Reveal";
                if (elapsed >= .32f && levelText != null) levelText.text = finalLevelLabel;
                if (elapsed >= .58f) RevealFinal();
                RevealProgress = Mathf.Clamp01((elapsed - .58f) / .47f);
                if (banner != null)
                {
                    banner.alpha = Mathf.Clamp01(elapsed / .12f);
                    bannerTitle.text = elapsed < .32f ? "凝聚力量" : After.Stage >= 2 ? "传说 · 阶级提升" : "稀有 · 阶级提升";
                    bannerDescription.text = elapsed < .58f ? "新能力即将解锁"
                        : string.IsNullOrEmpty(After.UnlockedAbility) ? "新阶级已解锁" : After.UnlockedAbility;
                }
                if (mark != null && revealed)
                {
                    mark.transform.localScale = markScale * (1 + Mathf.Sin(RevealProgress * Mathf.PI) * .22f);
                    mark.color = Color.Lerp(new Color(1, .83f, .36f), markColor, RevealProgress);
                }
                if (skillIcon != null && revealed)
                    skillIcon.transform.localScale = skillIconScale * (1 + Mathf.Sin(RevealProgress * Mathf.PI) * .18f);
            }
            else if (elapsed >= .08f && levelText != null) levelText.text = finalLevelLabel;
            float pulse = promotion ? Mathf.Sin(Mathf.Clamp01((elapsed - .25f) / .5f) * Mathf.PI)
                : Mathf.Sin(Mathf.Clamp01(elapsed / .42f) * Mathf.PI);
            if (levelText != null) levelText.transform.localScale = levelScale * (1 + pulse * .10f);
            if (platformLight != null)
            {
                float light = promotion ? Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) : Mathf.Sin(Mathf.Clamp01(elapsed / .6f) * Mathf.PI);
                platformLight.color = new Color(1, .83f, .34f, light * (promotion ? .85f : .65f));
                platformLight.rectTransform.localScale = Vector3.one * (1 + light * .12f);
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        routine = null;
        CompleteImmediately();
    }

    void RevealFinal()
    {
        if (revealed) return;
        revealed = true;
        if (Before != null && After != null && After.Stage > Before.Stage)
            AudioService.Ensure().Play(AudioId.UiSwitch);
        var callback = reveal; reveal = null;
        callback?.Invoke();
    }

    public void CompleteImmediately()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (!IsPlaying) return;
        RevealFinal();
        if (rows != null) foreach (var row in rows) if (row != null) row.CompleteAnimation();
        if (levelText != null) { levelText.text = finalLevelLabel; levelText.transform.localScale = levelScale; }
        if (mark != null) { mark.transform.localScale = markScale; mark.color = markColor; }
        if (skillIcon != null) skillIcon.transform.localScale = skillIconScale;
        if (platformLight != null) platformLight.color = Color.clear;
        if (banner != null) { banner.alpha = 0; banner.gameObject.SetActive(false); }
        RevealProgress = 1; Phase = "Idle"; IsPlaying = false;
        var callback = completed; completed = null;
        callback?.Invoke();
    }

    void EnsurePlatformLight(CharacterPanelPresentation presentation)
    {
        if (platformLight != null || presentation == null || presentation.heroGroup == null) return;
        var platform = presentation.heroGroup.Find("Platform") as RectTransform;
        var glow = presentation.heroGroup.Find("Glow");
        var source = glow != null ? glow.GetComponent<Image>() : null;
        if (platform == null || source == null || source.sprite == null) return;
        var go = new GameObject("GrowthLight", typeof(RectTransform));
        go.transform.SetParent(platform, false);
        platformLight = go.AddComponent<Image>();
        platformLight.sprite = source.sprite; platformLight.raycastTarget = false; platformLight.color = Color.clear;
        var rt = platformLight.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(platform.rect.width * 1.15f, platform.rect.height * 1.8f);
    }

    void EnsureBanner(TMP_Text source, CharacterPanelPresentation presentation)
    {
        if (source != null && source.font != null && source.font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            source.font.TryAddCharacters("凝聚力量传说稀有阶级提升新能力即将解锁点击跳过" + After.UnlockedAbility, out _);
        if (banner == null && source != null)
        {
            var go = new GameObject("GrowthReveal", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(0, 30); rt.sizeDelta = new Vector2(820, 235);
            var background = go.AddComponent<Image>();
            var paper = presentation != null && presentation.paper != null ? presentation.paper.GetComponent<Image>() : null;
            if (paper != null) { background.sprite = paper.sprite; background.type = Image.Type.Sliced; }
            background.color = new Color(1, .96f, .82f);
            banner = go.AddComponent<CanvasGroup>();
            var skip = go.AddComponent<Button>(); skip.targetGraphic = background; skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(CompleteImmediately);
            bannerTitle = NewText(go.transform, source, "Title", new Vector2(0, 72), new Vector2(740, 56), 40);
            bannerTitle.fontStyle = FontStyles.Bold;
            bannerDescription = NewText(go.transform, source, "Ability", new Vector2(0, -4), new Vector2(740, 95), 30);
            bannerDescription.enableWordWrapping = true;
            bannerDescription.enableAutoSizing = true;
            bannerDescription.fontSizeMin = 24; bannerDescription.fontSizeMax = 30;
            bannerDescription.overflowMode = TextOverflowModes.Ellipsis;
            var hint = NewText(go.transform, source, "Skip", new Vector2(0, -88), new Vector2(740, 30), 23);
            hint.text = "点击跳过"; hint.color = new Color(.48f, .42f, .30f);
        }
        if (banner != null) { banner.alpha = 0; banner.gameObject.SetActive(true); banner.transform.SetAsLastSibling(); }
    }

    static TMP_Text NewText(Transform parent, TMP_Text source, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial; text.fontSize = fontSize;
        text.color = new Color(.29f, .22f, .14f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        var rt = text.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = position; rt.sizeDelta = size;
        return text;
    }
    void OnDisable() { CompleteImmediately(); }
}
