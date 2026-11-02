using TMPro;
using UnityEngine;

/// <summary>Hide the backing surface when the result has no new character to announce.</summary>
public sealed class BattleUnlockVisibility : MonoBehaviour
{
    public TMP_Text label;
    private void LateUpdate()
    {
        // Keep this component running while the label itself is toggled by GameResultPanel.
        var graphic = GetComponent<UnityEngine.UI.Image>();
        if (graphic != null) graphic.enabled = label != null && label.gameObject.activeSelf;
    }
}
