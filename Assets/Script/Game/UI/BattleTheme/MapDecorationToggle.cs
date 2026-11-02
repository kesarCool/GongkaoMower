using UnityEngine;
using UnityEngine.UI;

/// <summary>Pause-page checkbox restores the saved preference whenever the panel opens.</summary>
[RequireComponent(typeof(Toggle))]
[DisallowMultipleComponent]
public sealed class MapDecorationToggle : MonoBehaviour
{
    private Toggle _toggle;

    private void OnEnable()
    {
        _toggle = GetComponent<Toggle>();
        _toggle.SetIsOnWithoutNotify(BattleMapPreferences.ShowDecoration);
        GetComponent<ThemeAudioToggle>()?.Refresh();
        _toggle.onValueChanged.AddListener(OnChanged);
        BattleMapPreferences.DecorationVisibilityChanged += Refresh;
        BattleChineseFontRuntime.ApplyToHierarchy(transform);
    }

    private void OnDisable()
    {
        if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnChanged);
        BattleMapPreferences.DecorationVisibilityChanged -= Refresh;
    }

    private void OnChanged(bool show) { BattleMapPreferences.ShowDecoration = show; }
    private void Refresh(bool show)
    {
        _toggle.SetIsOnWithoutNotify(show);
        GetComponent<ThemeAudioToggle>()?.Refresh();
    }
}
