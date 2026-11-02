using System;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>跨场景接收 JS 分享状态。奖励策略是发起分享后返回，无法证明消息送达。</summary>
[Preserve]
public sealed class WXShareRequestRunner : MonoBehaviour
{
    public const float DispatchTimeoutSeconds = 5f;
    public const float ReturnTimeoutSeconds = 120f;
    private static WXShareRequestRunner _instance;
    private string _requestId;
    private Action<bool> _onComplete;
    private float _requestedAt;
    private bool _dispatched;
    private bool _hidden;

    public static WXShareRequestRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                var host = new GameObject(nameof(WXShareRequestRunner));
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<WXShareRequestRunner>();
            }
            return _instance;
        }
    }

    public void Request(string title, Action<bool> onComplete)
    {
        if (_requestId != null)
        {
            Debug.LogWarning("[WXShare] 已有分享请求，忽略重复请求");
            onComplete?.Invoke(false);
            return;
        }

        _requestId = Guid.NewGuid().ToString("N");
        _requestedAt = Time.realtimeSinceStartup;
        _onComplete = onComplete;
        _dispatched = false;
        _hidden = false;
        GameLog.Info($"[WXShare] request={_requestId} 调用 SDK，等待 JS 确认");
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WeChatWASM.WX.ShareAppMessage(new WeChatWASM.ShareAppMessageOption
            {
                title = string.IsNullOrEmpty(title) ? "快来一起玩文字割草机！" : title,
                query = "mowerShareRequest=" + _requestId,
            });
#endif
        }
        catch (Exception ex)
        {
            Finish(false, "SDK 调用异常: " + ex.Message);
        }
    }

    [Serializable]
    private sealed class BridgeState
    {
        public string requestId;
        public string state;
        public string error;
    }

    // Called by JS Module.SendMessage. Keep public and preserved for IL2CPP stripping.
    [Preserve]
    public void OnShareBridgeState(string json)
    {
        BridgeState result;
        try { result = JsonUtility.FromJson<BridgeState>(json); }
        catch (Exception ex) { Debug.LogWarning("[WXShare] 无效 JS 状态: " + ex.Message); return; }
        if (result == null || _requestId == null || result.requestId != _requestId) return;
        if (Expired()) { Finish(false, "分享请求已超时，忽略迟到回调"); return; }

        switch (result.state)
        {
            case "dispatched":
                _dispatched = true;
                GameLog.Info($"[WXShare] request={_requestId} JS 已调用原生分享，等待返回");
                break;
            case "hidden":
                _hidden = true;
                GameLog.Info($"[WXShare] request={_requestId} 观察到小游戏隐藏，等待返回");
                break;
            case "returned":
                if (_dispatched && _hidden) Finish(true, "观察到小游戏隐藏后返回（不代表分享送达）");
                break;
            case "failed":
            case "cancelled":
                Finish(false, result.state + ": " + result.error);
                break;
        }
    }

    private bool Expired() => Time.realtimeSinceStartup - _requestedAt >=
        (_dispatched && _hidden ? ReturnTimeoutSeconds : DispatchTimeoutSeconds);

    private void Update()
    {
        if (_requestId != null && Expired())
            Finish(false, _dispatched && _hidden ? "未在时限内观察到分享返回，可重试" :
                _dispatched ? "JS 已调用分享但未观察到界面切换，可重试并检查真机日志" : "JS 未确认分享调用，请检查 SDK 桥接");
    }

    private void Finish(bool success, string reason)
    {
        if (_requestId == null) return;
        string id = _requestId;
        var callback = _onComplete;
        _requestId = null;
        _onComplete = null;
        _dispatched = false;
        _hidden = false;
        GameLog.Info($"[WXShare] request={id} rewardEligible={success}: {reason}");
        callback?.Invoke(success);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        Finish(false, "分享接收器已销毁");
    }
}
