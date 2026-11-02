using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnergyProgressPresentation : MonoBehaviour
{
    public EnergySegmentGraphic segments;
    public TextMeshProUGUI caption, countText;
    public RectTransform levelBadge;
    public Image energyOrb;
    private float _target, _displayed, _pickupPulse, _levelFlash;
    private int _level;
    private bool _initialized;
    public float DisplayedProgress => _displayed;

    public void SetProgress(int current, int needed, int level)
    {
        float target = Mathf.Clamp01((float)current / Mathf.Max(1, needed));
        if (!_initialized) { _displayed = target; _initialized = true; }
        else
        {
            if (level > _level) _levelFlash = .42f;
            if (target > _target) _pickupPulse = 1;
        }
        _level = level; _target = target;
        if (countText != null) countText.text = current + " / " + needed;
        if (segments != null) segments.SetProgress(_displayed, 0);
    }
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _pickupPulse = Mathf.MoveTowards(_pickupPulse, 0, dt * 4);
        _levelFlash = Mathf.Max(0, _levelFlash - dt);
        bool levelUp = _levelFlash > 0;
        if (levelUp) _displayed = 1;
        else _displayed = Mathf.Lerp(_displayed, _target, 1f - Mathf.Exp(-dt * 18));
        float flash = levelUp ? Mathf.Sin(_levelFlash / .42f * Mathf.PI) : _pickupPulse * .35f;
        if (segments != null) segments.SetProgress(_displayed, flash);
        if (caption != null) caption.text = levelUp ? "升级！选择技能变强" : "升级能量";
        if (levelBadge != null) levelBadge.localScale = Vector3.one * (1 + Mathf.Max(0, flash) * .12f);
        if (energyOrb != null) energyOrb.rectTransform.localScale = Vector3.one * (1 + _pickupPulse * .14f);
    }
    private void OnDisable()
    {
        if (levelBadge != null) levelBadge.localScale = Vector3.one;
        if (energyOrb != null) energyOrb.rectTransform.localScale = Vector3.one;
    }
}
