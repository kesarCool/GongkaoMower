using UnityEngine;
using UnityEngine.UI;

/// <summary>Moves the switch knob without changing the existing audio listeners.</summary>
[RequireComponent(typeof(Toggle))]
public sealed class ThemeAudioToggle : MonoBehaviour
{
    public Image track;
    public RectTransform knob;
    private Toggle toggle;
    private void OnEnable()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(Render);
        Refresh();
    }
    private void OnDisable() { if (toggle != null) toggle.onValueChanged.RemoveListener(Render); }
    public void Refresh()
    {
        if (toggle == null) toggle = GetComponent<Toggle>();
        Render(toggle.isOn);
    }
    private void Render(bool on)
    {
        if (track != null) track.color = on ? new Color32(148, 176, 119, 255) : new Color32(197, 192, 174, 255);
        if (knob != null) knob.anchoredPosition = new Vector2(on ? 29 : -29, 0);
    }
}
