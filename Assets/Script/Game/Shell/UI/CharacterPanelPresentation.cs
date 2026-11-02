using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation and responsive layout for the approved character page.</summary>
public sealed class CharacterPanelPresentation : MonoBehaviour
{
    public RectTransform bodyViewport, body, heroGroup, tabsRoot, paper, paperShadow, cards;
    public RectTransform statsViewport, statsContent, skillViewport, skillContent;
    public VerticalLayoutGroup statLayout, skillLayout;
    public TMP_Text weaponText, equippedText, skillTitle, unlockedCount;
    public Image equippedMark;
    public TMP_Text[] tierLabels, tierStates;
    public Image[] tierMarks;
    public Sprite tierLocked, tierUnlocked;
    private Vector2 previousSize;

    public void Refresh(CharacterDefinition def, SkillDefinitionBase skill, bool equipped, CharacterCatalog catalog, int stageOverride = -1)
    {
        if (def == null) return;
        if (weaponText != null) weaponText.text = "武器 · " + (def.defaultWeapon != null ? def.defaultWeapon.displayName : "—");
        if (equippedText != null)
        {
            equippedText.text = equipped ? "已上阵" : "待上阵";
            equippedText.color = equipped ? ColorOf("5F8551") : ColorOf("8D7A5D");
        }
        if (equippedMark != null) equippedMark.gameObject.SetActive(equipped);
        if (skillTitle != null) skillTitle.text = skill != null ? skill.displayName : "专属技能";
        int count = 0;
        if (catalog != null) foreach (var entry in catalog.characters) if (entry != null && CharacterUnlockEvaluator.IsUnlocked(entry)) count++;
        if (unlockedCount != null) unlockedCount.text = "已解锁 " + count + " / " + (catalog != null ? catalog.characters.Count : 0);
        var data = def.upgradeData;
        int stage = stageOverride >= 0 ? stageOverride : PlayerProfileService.Instance.GetHeroStage(def.characterId);
        for (int i = 0; i < 2; i++)
        {
            bool unlocked = stage >= i + 1;
            var mark = GetTierMark(i + 1);
            if (mark != null)
            {
                mark.sprite = unlocked ? tierUnlocked : tierLocked;
                var lockedIcon = mark.transform.Find("Lock");
                var checkIcon = mark.transform.Find("Check");
                if (lockedIcon != null) lockedIcon.gameObject.SetActive(!unlocked);
                if (checkIcon != null) checkIcon.gameObject.SetActive(unlocked);
            }
            if (tierStates != null && i < tierStates.Length && tierStates[i] != null)
            {
                tierStates[i].text = unlocked ? "已解锁" : data == null ? "未开放" : "Lv." + (i == 0 ? data.rareRequiredLevel : data.legendRequiredLevel) + " · 未解锁";
                tierStates[i].color = unlocked ? ColorOf("3D7047") : ColorOf("8D7A5D");
            }
            if (tierLabels != null && i < tierLabels.Length && tierLabels[i] != null)
                tierLabels[i].text = i == 0 ? "一阶 · 英雄特质" : "二阶 · 奥义突破";
        }
        ApplyLayout();
        Canvas.ForceUpdateCanvases();
        if (skillContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(skillContent);
    }

    public Image GetTierMark(int stage) => tierMarks != null && stage > 0 && stage <= tierMarks.Length ? tierMarks[stage - 1] : null;

    private void OnEnable() { previousSize = Vector2.zero; }
    private void LateUpdate()
    {
        if (((RectTransform)transform).rect.size != previousSize) ApplyLayout();
    }
    public void ApplyLayout()
    {
        if (body == null || heroGroup == null || tabsRoot == null || paper == null || paperShadow == null || cards == null
            || statsViewport == null || skillViewport == null || statLayout == null || skillLayout == null || statsContent == null) return;
        var size = ((RectTransform)transform).rect.size;
        previousSize = size;
        float extra = Mathf.Max(0, size.y - 1526.4f);
        body.sizeDelta = new Vector2(1080, 1377.6f + extra);
        SetTop(heroGroup, extra / 8, 1080, 367.2f);
        SetTop(tabsRoot, 367.2f + extra / 4, 1080, 113);
        SetTop(paper, 469.2f + extra / 4, 972, 618 + extra / 2);
        SetTop(paperShadow, 475.2f + extra / 4, 972, 618 + extra / 2);
        SetTop(cards, 1100 + extra, 972, 257.2f);
        statsViewport.sizeDelta = new Vector2(892, 396 + extra / 2);
        skillViewport.sizeDelta = new Vector2(892, 453 + extra / 2);
        statLayout.spacing = 0;
        float rowHeight = 44 + extra / 20;
        foreach (Transform row in statsContent)
        {
            var layout = row.GetComponent<LayoutElement>();
            if (layout != null) layout.preferredHeight = rowHeight;
        }
        skillLayout.spacing = 24 + extra / 12;
    }
    private static void SetTop(RectTransform rt, float y, float w, float h)
    { rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y); rt.sizeDelta = new Vector2(w, h); }
    private static Color ColorOf(string hex) { Color c; ColorUtility.TryParseHtmlString("#" + hex, out c); return c; }
}

