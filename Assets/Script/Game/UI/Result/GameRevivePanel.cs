using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首次死亡复活弹窗：倒计时、退出进结算、分享返回复活。请求期间暂停倒计时。
/// </summary>
[DisallowMultipleComponent]
public class GameRevivePanel : UIPanelBase
{
    [SerializeField] private float countdownSeconds = 10f;

    [SerializeField] private Image _countdownFillImage;
    [SerializeField] private TextMeshProUGUI _textTime;
    [SerializeField] private Button _btnRevive;
    [SerializeField] private Button _btnExit;

    private GameRevivePanelPayload _payload;
    private Coroutine _countdownRoutine;
    private bool _resolved;
    private bool _requesting;
    private int _requestVersion;
    private float _remainingSeconds;
    private float _countdownDuration;

    private void Awake()
    {
        if (_countdownFillImage == null) _countdownFillImage = transform.Find("Image")?.GetComponent<Image>();
        if (_textTime == null) _textTime = transform.Find("Image/TextTime")?.GetComponent<TextMeshProUGUI>();
        if (_textTime == null)
            _textTime = transform.Find("TextTime")?.GetComponent<TextMeshProUGUI>();

        if (_btnRevive == null) _btnRevive = transform.Find("ButtonRevive")?.GetComponent<Button>();
        if (_btnExit == null) _btnExit = transform.Find("ButtonExit")?.GetComponent<Button>();

        if (_btnRevive != null) _btnRevive.onClick.AddListener(OnReviveClicked);
        if (_btnExit != null) _btnExit.onClick.AddListener(OnExitClicked);
    }

    public override void OnOpen(object payload)
    {
        _payload = payload as GameRevivePanelPayload;
        _resolved = false;
        _requesting = false;
        _requestVersion++;
        GetComponent<BattlePanelPresentation>()?.Present();

        float duration = _payload != null && _payload.countdownSeconds > 0f
            ? _payload.countdownSeconds
            : countdownSeconds;
        _remainingSeconds = _countdownDuration = duration;
        var reviveText = _btnRevive != null ? _btnRevive.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (reviveText != null) reviveText.text = "分享返回复活";

        if (_countdownFillImage != null)
        {
            _countdownFillImage.type = Image.Type.Filled;
            _countdownFillImage.fillAmount = 1f;
        }

        SetButtonsInteractable(true);

        if (_countdownRoutine != null)
            StopCoroutine(_countdownRoutine);
        _countdownRoutine = StartCoroutine(CountdownRoutine());

        transform.SetAsLastSibling();
    }

    public override void OnClose()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        _resolved = true;
        _requesting = false;
        _requestVersion++;
        _payload = null;
    }

    private IEnumerator CountdownRoutine()
    {
        while (_remainingSeconds > 0f && !_resolved)
        {
            _remainingSeconds -= UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float ratio = _countdownDuration > 0f ? Mathf.Clamp01(_remainingSeconds / _countdownDuration) : 0f;

            if (_countdownFillImage != null)
                _countdownFillImage.fillAmount = ratio;

            if (_textTime != null)
                _textTime.text = Mathf.CeilToInt(Mathf.Max(0f, _remainingSeconds)).ToString();

            yield return null;
        }

        _countdownRoutine = null;

        if (!_resolved)
            OnTimeout();
    }

    private void OnReviveClicked()
    {
        if (_resolved || _requesting) return;
        UiClickSound.Play();
        _requesting = true;
        int version = ++_requestVersion;
        StopCountdown();
        SetButtonsInteractable(false);

        UIManager.Instance?.ShowToast("分享返回后复活，倒计时已暂停", 2f);

        IReviveAdProvider provider = _payload?.adProvider ?? WeChatRewardedAdProvider.Instance;
        var payload = _payload;
        Action<bool> complete = success =>
        {
            if (this == null || !_requesting || _resolved || _requestVersion != version || _payload != payload) return;
            _requesting = false;
            if (success)
            {
                _resolved = true;
                payload?.onRevived?.Invoke();
            }
            else
            {
                SetButtonsInteractable(true);
                _countdownRoutine = StartCoroutine(CountdownRoutine());
                UIManager.Instance?.ShowToast("未能确认分享返回，可重试或退出", 2f);
            }
        };
        try { provider.RequestReviveAd(complete); }
        catch (Exception ex)
        {
            Debug.LogWarning("[Revive] 分享请求异常: " + ex.Message);
            complete(false);
        }
    }

    private void OnExitClicked()
    {
        if (_resolved || _requesting) return;
        UiClickSound.PlayClose();
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
