using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public sealed class HomeFilterTogglePresentation : MonoBehaviour
{
    public bool folder;
    public Image face;
    public Sprite normalSprite,selectedSprite;
    public TMP_Text label;
    public GameObject selectedIcon,connector;
    private Toggle toggle;
    private void Awake(){toggle=GetComponent<Toggle>();}
    private void LateUpdate()
    {
        bool selected=toggle.isOn;
        face.enabled=!folder||selected;face.sprite=selected?selectedSprite:normalSprite;
        label.fontSize=folder?(selected?44:40):31;
        label.fontStyle=selected?FontStyles.Bold:FontStyles.Normal;
        label.color=selected?new Color32(73,54,39,255):new Color32(118,99,76,255);
        if(selectedIcon!=null)selectedIcon.SetActive(selected);
        if(connector!=null)
        {
            connector.SetActive(selected);
            var rt=(RectTransform)connector.transform;
            rt.anchoredPosition=new Vector2(transform.GetSiblingIndex()<=1?472:-500,-88);
        }
    }
}
