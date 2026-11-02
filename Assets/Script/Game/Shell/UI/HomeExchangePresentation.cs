using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeExchangePresentation : MonoBehaviour
{
    public ShopCell bonusCellPrefab;
    public RectTransform content, bonusContent, fragmentContent, fragmentHeading, footerHint;
    public void RefreshLayout()
    {
        int bonus=ActiveChildren(bonusContent),fragments=ActiveChildren(fragmentContent);
        float bonusHeight=bonus==0?0:Mathf.Ceil(bonus/2f)*380-24;
        bonusContent.sizeDelta=new Vector2(972,bonusHeight);
        float headingY=bonusHeight+(bonus>0?30:0);
        fragmentHeading.gameObject.SetActive(fragments>0);
        fragmentHeading.anchoredPosition=new Vector2(0,-headingY);
        float fragmentsY=headingY+(fragments>0?82:0);
        fragmentContent.anchoredPosition=new Vector2(0,-fragmentsY);
        float fragmentHeight=fragments==0?0:Mathf.Ceil(fragments/3f)*402-24;
        fragmentContent.sizeDelta=new Vector2(972,fragmentHeight);
        footerHint.anchoredPosition=new Vector2(0,-(fragmentsY+fragmentHeight+24));
        content.sizeDelta=new Vector2(972,fragmentsY+fragmentHeight+76);
    }
    private static int ActiveChildren(Transform root)
    {int count=0;foreach(Transform child in root)if(child.gameObject.activeSelf)count++;return count;}
}
