using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small pooled reward card; click actions never award items.</summary>
public sealed class RewardRevealCell : MonoBehaviour
{
    public Image surface, icon, fragmentMark, gradeMark;
    public TMP_Text nameLabel, amountLabel;
    public CanvasGroup group;
    public RectTransform motion;
    public Sprite coin, gem, fallback;
    public CharacterCatalog characters;

    public void Bind(RewardItemEntry entry)
    {
        Sprite sprite = entry.itemId == 1 ? coin : entry.itemId == 2 ? gem : null;
        bool fragment = false;
        if (sprite == null && characters != null)
            foreach (var hero in characters.characters)
                if (hero != null && hero.fragmentItemId == entry.itemId) { sprite = hero.portrait; fragment = true; break; }
        if (sprite == null && !string.IsNullOrEmpty(entry.iconPath)) sprite = Resources.Load<Sprite>(entry.iconPath);
        icon.sprite = sprite != null ? sprite : fallback; icon.preserveAspect = true;
        icon.rectTransform.sizeDelta = fragment ? new Vector2(128, 145) : new Vector2(130, 130);
        fragmentMark.gameObject.SetActive(fragment);
        nameLabel.text = BackpackItemView.DisplayName(entry.itemName);
        amountLabel.text = "× " + entry.count.ToString("N0");
        gradeMark.color = ItemCell.GradeColor(entry.grade);
        surface.color = entry.itemId == 1 ? new Color32(251, 234, 185, 255) : entry.itemId == 2 ? new Color32(215, 239, 242, 255) : new Color32(232, 224, 243, 255);
    }
    public void BindCharacter(CharacterDefinition hero)
    {
        icon.sprite = hero != null && hero.portrait != null ? hero.portrait : fallback; icon.preserveAspect = true;
        icon.rectTransform.sizeDelta = new Vector2(162, 170);
        nameLabel.text = hero != null ? hero.displayName : "新伙伴";
        amountLabel.text = "已解锁"; fragmentMark.gameObject.SetActive(false);
        gradeMark.color = new Color32(233, 174, 63, 255); surface.color = new Color32(250, 234, 193, 255);
    }
    public void Reveal(float progress)
    {
        progress = Mathf.Clamp01(progress); group.alpha = Mathf.Clamp01(progress * 5);
        float p = progress - 1;
        float eased = 1 + 2.3f * p * p * p + 1.3f * p * p;
        motion.localScale = Vector3.one * Mathf.LerpUnclamped(.72f, 1, eased);
        motion.anchoredPosition = Vector2.up * (1 - progress) * -28;
    }
}
