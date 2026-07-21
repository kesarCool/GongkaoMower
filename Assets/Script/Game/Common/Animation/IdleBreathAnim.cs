using UnityEngine;

/// <summary>
/// 通用待机微动：呼吸缩放 + 上下浮动，UI 立绘 / 局内角色均可挂。
/// 外部通过 <see cref="IsAnimating"/> 暂停/恢复（局内移动/攻击时关，待机时开）。
///
/// 弹入动画用纯计时器 + unscaledDeltaTime，不受 Time.timeScale 影响。
/// </summary>
[DisallowMultipleComponent]
public class IdleBreathAnim : MonoBehaviour
{
    [Header("呼吸缩放")]
    [SerializeField] private bool enableBreath = true;
    [Tooltip("缩放幅度，UI 建议 0.03，局内角色建议 0.02。")]
    [SerializeField] private float breathAmplitude = 0.03f;
    [Tooltip("呼吸频率，越大越快。")]
    [SerializeField] private float breathSpeed = 2.1f;

    [Header("上下浮动")]
    [SerializeField] private bool enableFloat = true;
    [Tooltip("浮动像素/单位，UI 建议 3，局内角色建议 0.05。")]
    [SerializeField] private float floatAmplitude = 3f;
    [Tooltip("浮动频率，越大越快。")]
    [SerializeField] private float floatSpeed = 2.5f;

    [Header("弹入（选中/切换时触发）")]
    [Tooltip("弹入最大缩放增量。")]
    [SerializeField] private float bounceScale = 0.08f;
    [Tooltip("弹入持续时间（秒）。")]
    [SerializeField] private float bounceDuration = 0.15f;

    /// <summary>是否播待机动画。局内移动/攻击时设为 false，待机时 true；UI 始终 true。</summary>
    public bool IsAnimating { get; set; } = true;

    private RectTransform _rect;
    private Vector2 _restAnchoredPos;
    private Vector3 _restLocalPos;
    private float _time;
    private float _bounceTimer;
    private bool _hasRect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _hasRect = _rect != null;
        CacheRestPosition();
    }

    private void OnEnable()
    {
        IsAnimating = true;
        CacheRestPosition();
        _time = 0f;
        _bounceTimer = 0f;
    }

    private void OnDisable()
    {
        _bounceTimer = 0f;
        ResetToRest();
    }

    private void CacheRestPosition()
    {
        if (_hasRect)
            _restAnchoredPos = _rect.anchoredPosition;
        else
            _restLocalPos = transform.localPosition;
    }

    /// <summary>触发一次弹入动画（切角色/选中时调用）。任意状态下均可安全调用。</summary>
    public void PlayBounce()
    {
        if (!enabled) return;
        _bounceTimer = bounceDuration;
    }

    private void Update()
    {
        if (!IsAnimating) return;

        // ── 弹入动画 ──
        if (_bounceTimer > 0f)
        {
            _bounceTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(1f - _bounceTimer / bounceDuration);
            float s = 1f + bounceScale * (1f - t);
            transform.localScale = new Vector3(s, s, 1f);

            if (_bounceTimer <= 0f)
                transform.localScale = Vector3.one;

            return;
        }

        // ── 呼吸 ──
        _time += Time.unscaledDeltaTime;

        if (enableBreath)
        {
            float s = 1f + Mathf.Sin(_time * breathSpeed) * breathAmplitude;
            transform.localScale = new Vector3(s, s, 1f);
        }

        if (enableFloat)
        {
            float offset = Mathf.Sin(_time * floatSpeed) * floatAmplitude;
            if (_hasRect)
                _rect.anchoredPosition = new Vector2(_restAnchoredPos.x, _restAnchoredPos.y + offset);
            else
                transform.localPosition = new Vector3(_restLocalPos.x, _restLocalPos.y + offset, _restLocalPos.z);
        }
    }

    private void ResetToRest()
    {
        transform.localScale = Vector3.one;
        if (_hasRect)
            _rect.anchoredPosition = _restAnchoredPos;
        else
            transform.localPosition = _restLocalPos;
    }
}
