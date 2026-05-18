using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首次死亡复活弹窗：10 秒倒计时（Image Fill 1→0）、退出进结算、看广告复活（默认直接成功）。
/// </summary>
[DisallowMultipleComponent]
public class GameRevivePanel : UIPanelBase
{
    [SerializeField] private float countdownSeconds = 10f;

    private Image _countdownFillImage;
    private TextMeshProUGUI _textTime;
    private Button _btnRevive;
    private Button _btnExit;

    private GameRevivePanelPayload _payload;
    private Coroutine _countdownRoutine;
    private bool _resolved;

    private void Awake()
    {
        _countdownFillImage = transform.Find("Image")?.GetComponent<Image>();
        _textTime = transform.Find("Image/TextTime")?.GetComponent<TextMeshProUGUI>();
        if (_textTime == null)
            _textTime = transform.Find("TextTime")?.GetComponent<TextMeshProUGUI>();

        _btnRevive = transform.Find("ButtonRevive")?.GetComponent<Button>();
        _btnExit = transform.Find("ButtonExit")?.GetComponent<Button>();

        if (_btnRevive != null) _btnRevive.onClick.AddListener(OnReviveClicked);
        if (_btnExit != null) _btnExit.onClick.AddListener(OnExitClicked);
    }

    public override void OnOpen(object payload)
    {
        _payload = payload as GameRevivePanelPayload;
        _resolved = false;

        float duration = _payload != null && _payload.countdownSeconds > 0f
            ? _payload.countdownSeconds
            : countdownSeconds;

        if (_countdownFillImage != null)
        {
            _countdownFillImage.type = Image.Type.Filled;
            _countdownFillImage.fillAmount = 1f;
        }

        SetButtonsInteractable(true);

        if (_countdownRoutine != null)
            StopCoroutine(_countdownRoutine);
        _countdownRoutine = StartCoroutine(CountdownRoutine(duration));

        transform.SetAsLastSibling();
    }

    public override void OnClose()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        _resolved = false;
        _payload = null;
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        float remaining = duration;
        while (remaining > 0f && !_resolved)
        {
            remaining -= UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float ratio = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;

            if (_countdownFillImage != null)
                _countdownFillImage.fillAmount = ratio;

            if (_textTime != null)
                _textTime.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();

            yield return null;
        }

        _countdownRoutine = null;

        if (!_resolved)
            OnTimeout();
    }

    private void OnReviveClicked()
    {
        if (_resolved) return;
        _resolved = true;
        StopCountdown();
        SetButtonsInteractable(false);

        IReviveAdProvider provider = _payload?.adProvider ?? DefaultReviveAdProvider.Instance;
        provider.RequestReviveAd(success =>
        {
            if (success)
                _payload?.onRevived?.Invoke();
            else
                _payload?.onGiveUp?.Invoke();
        });
    }

    private void OnExitClicked()
    {
        if (_resolved) return;
        _resolved = true;
        StopCountdown();
        SetButtonsInteractable(false);
        _payload?.onGiveUp?.Invoke();
    }

    private void OnTimeout()
    {
        if (_resolved) return;
        _resolved = true;
        SetButtonsInteractable(false);
        _payload?.onGiveUp?.Invoke();
    }

    private void StopCountdown()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_btnRevive != null) _btnRevive.interactable = interactable;
        if (_btnExit != null) _btnExit.interactable = interactable;
    }
}

/// <summary>复活面板数据（由 <see cref="BattleOutcomeCoordinator"/> 传入）。</summary>
public sealed class GameRevivePanelPayload
{
    public float countdownSeconds = 10f;
    public IReviveAdProvider adProvider;
    public Action onGiveUp;
    public Action onRevived;
}
