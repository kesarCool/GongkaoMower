using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按钮防连点：挂到任意 Button 上即可拦截短时间内的重复点击。
/// 通过在按钮上方加盖半透明遮罩来阻断 raycast，不修改 button.interactable，避免与业务逻辑冲突。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class AntiSpamClick : MonoBehaviour
{
    [Tooltip("两次点击之间的最小间隔（秒）。")]
    [SerializeField] [Range(0.1f, 3f)] private float cooldown = 0.5f;

    [Tooltip("冷却期间遮罩的不透明度（0=全透明，0.5=半灰）。")]
    [SerializeField] [Range(0f, 1f)] private float blockerAlpha = 0.35f;

    private Button _button;
    private Image _blocker;
    private Coroutine _cooldownRoutine;

    /// <summary>当前是否在冷却中（点击会被拦截）。</summary>
    public bool IsCoolingDown => _blocker != null && _blocker.raycastTarget;

    private void Awake()
    {
        _button = GetComponent<Button>();
        BuildBlocker();
    }

    private void OnEnable()
    {
        if (_button != null)
            _button.onClick.AddListener(OnButtonClick);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        // 遮罩盖住按钮，后续点击被阻断
        _blocker.raycastTarget = true;
        SetBlockerVisible(true);

        if (_cooldownRoutine != null)
            StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(EndCooldown());
    }

    private IEnumerator EndCooldown()
    {
        yield return new WaitForSecondsRealtime(cooldown);

        _blocker.raycastTarget = false;
        SetBlockerVisible(false);
        _cooldownRoutine = null;
    }

    private void SetBlockerVisible(bool visible)
    {
        var c = _blocker.color;
        c.a = visible ? blockerAlpha : 0f;
        _blocker.color = c;
    }

    private void BuildBlocker()
    {
        var go = new GameObject("__anti_spam_blocker__", typeof(Image))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        var rt = go.transform as RectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _blocker = go.GetComponent<Image>();
        _blocker.sprite = CreateWhitePixelSprite();
        _blocker.type = Image.Type.Sliced;
        _blocker.color = new Color(0.5f, 0.5f, 0.5f, 0f);
        _blocker.raycastTarget = false;
    }

    private static Sprite CreateWhitePixelSprite()
    {
        // Texture2D.whiteTexture 是 Unity 内置的 1×1 白纹理，无需额外资源。
        return Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f),
            4f);
    }
}
