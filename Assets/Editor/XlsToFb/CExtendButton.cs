using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CExtendButton : Button
{
    [SerializeField]
    private float cooldown;
    private float lastClickTime;

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (Time.realtimeSinceStartup < lastClickTime + cooldown)
        {
            return;
        }
        lastClickTime = Time.realtimeSinceStartup;
        //FunctionHelper.ShowDebugColorRed("Button Click:",lastClickTime);
        base.OnPointerClick(eventData);
    }
}