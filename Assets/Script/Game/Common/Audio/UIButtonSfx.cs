using UnityEngine;
using UnityEngine.UI;

/// <summary>Button 点击时播放通用 UI 音效。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonSfx : MonoBehaviour
{
    [SerializeField] private AudioId clickId = AudioId.UiClick;

    private Button _button;

    private void Awake() => _button = GetComponent<Button>();

    private void OnEnable()
    {
        if (_button != null)
            _button.onClick.AddListener(OnClicked);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        if (clickId == AudioId.UiClose)
            UiClickSound.PlayClose();
        else
            UiClickSound.Play();
    }
}
