using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public sealed class CharacterTabPresentation : MonoBehaviour
{
    public GameObject selectedBackground, checkmark, connector;
    public TMP_Text label;
    private Toggle toggle;
    private void Awake() { toggle = GetComponent<Toggle>(); toggle.onValueChanged.AddListener(Refresh); Refresh(toggle.isOn); }
    private void OnEnable() { if (toggle != null) Refresh(toggle.isOn); }
    private void Refresh(bool selected)
    {
        selectedBackground.SetActive(selected); checkmark.SetActive(selected); connector.SetActive(selected);
        label.fontSize = selected ? 44 : 40;
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        label.color = selected ? new Color32(73,54,39,255) : new Color32(141,122,93,255);
    }
}
