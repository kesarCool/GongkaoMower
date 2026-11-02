using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleRewardCell : MonoBehaviour
{
    public Image icon, fragment;
    public TMP_Text label, amount;
    public Sprite coin, gem, fallback;
    public CharacterCatalog characters;
    public void Bind(RewardItemEntry entry)
    {
        Sprite sprite = entry.itemId == 1 ? coin : entry.itemId == 2 ? gem : null; bool isFragment = false;
        if (sprite == null && characters != null)
            foreach (var hero in characters.characters)
                if (hero != null && hero.fragmentItemId == entry.itemId) { sprite = hero.portrait; isFragment = true; break; }
        if (sprite == null && !string.IsNullOrEmpty(entry.iconPath)) sprite = Resources.Load<Sprite>(entry.iconPath);
        icon.sprite = sprite != null ? sprite : fallback; fragment.gameObject.SetActive(isFragment);
        label.text = BackpackItemView.DisplayName(entry.itemName); amount.text = "+" + entry.count;
    }
}
