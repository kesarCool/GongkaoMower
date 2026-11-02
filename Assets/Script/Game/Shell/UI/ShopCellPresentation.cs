using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopCellPresentation : MonoBehaviour
{
    public bool bonus;
    public CharacterCatalog characters;
    public Image face, portrait, currencyIcon, adIcon, ctaFace;
    public TMP_Text nameLabel, amountLabel, priceLabel, limitLabel;
    public Sprite normalCard, mutedCard, goldBonusCard, gemBonusCard, coinReward, gemReward, goldIcon, gemIcon;
    public Sprite primaryButton, secondaryButton, pressedButton, secondaryPressedButton, disabledButton;
    public Button button;
    public void Refresh(ShopCell cell)
    {
        var row=cell.ShopRow;if(row==null)return;
        bool sold=ShopService.IsSoldOut(row),ad=row.PriceType==3;
        face.sprite=sold?mutedCard:bonus?(ad?gemBonusCard:goldBonusCard):normalCard;
        face.color=Color.white;
        if(bonus)
        {
            bool gold=cell.ItemRow!=null&&cell.ItemRow.ItemName.Contains("金币");
            portrait.sprite=gold?coinReward:gemReward;portrait.enabled=true;
            nameLabel.text=gold?"金币":"钻石";
            amountLabel.text=row.ItemNumber.ToString();
        }
        else
        {
            nameLabel.text=(row.LatticeName??"").Replace("(碎片)","碎片").Replace("（碎片）","碎片");
            if(characters!=null)foreach(var hero in characters.characters)
                if(hero!=null&&(row.LatticeName??"").StartsWith(hero.displayName)){portrait.sprite=hero.portrait;portrait.enabled=hero.portrait!=null;break;}
        }
        portrait.color=new Color(1,1,1,sold?.5f:1);
        int remaining=ShopService.GetRemainingPurchases(row);
        limitLabel.text=remaining<0?"":(bonus?"限领 ":"限购 ")+remaining+" / "+row.PurchaseNum;
        priceLabel.text=sold?(bonus?"已领取":"已售罄"):row.PriceType==0?"免费领取":ad?"分享返回领取":ShopService.GetPrice(row).ToString();
        nameLabel.color=sold?Muted:Ink;priceLabel.color=sold?Muted:Ink;
        currencyIcon.gameObject.SetActive(!bonus&&!sold);
        currencyIcon.sprite=row.PriceType==2?gemIcon:goldIcon;
        adIcon.gameObject.SetActive(false);
        ctaFace.sprite=sold?disabledButton:ad?secondaryButton:primaryButton;
        var state=button.spriteState;state.pressedSprite=ad?secondaryPressedButton:pressedButton;state.disabledSprite=disabledButton;button.spriteState=state;
        button.interactable=!sold;
    }
    private static Color Ink=>new Color32(73,54,39,255);
    private static Color Muted=>new Color32(141,122,93,255);
}
