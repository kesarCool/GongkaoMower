#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Play Mode checks with deferred callbacks; never writes a player profile or calls native sharing.</summary>
public static class WeChatShareFlowValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/WeChat/Validate Share Reward Flow")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run sharing validation in Play Mode.");
        int checks = 0;
        Action<bool, string> check = (condition, name) =>
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            checks++;
        };
        var host = new GameObject("ShareFlowValidation");
        var previousOverride = WXShareHelper.EditorRequestOverride;
        GameRevivePanel revive = null;
        try
        {
            var runner = host.AddComponent<WXShareRequestRunner>();
            int completed = 0; bool reward = false;
            Action<bool> callback = success => { completed++; reward = success; };
            runner.Request("test", callback);
            string firstId = Field<string>(runner, "_requestId");
            State(runner, firstId, "dispatched");
            check(completed == 0, "dispatch alone does not grant");
            Set(runner, "_requestedAt", Time.realtimeSinceStartup - 6f);
            Invoke(runner, "Update");
            check(completed == 1 && !reward, "native no-popup unlocks in five seconds");
            State(runner, firstId, "hidden"); State(runner, firstId, "returned");
            check(completed == 1, "late no-popup callbacks do not grant");

            runner.Request("test", callback);
            string id = Field<string>(runner, "_requestId");
            State(runner, id, "hidden"); State(runner, id, "dispatched"); State(runner, id, "returned");
            State(runner, id, "returned");
            check(completed == 2 && reward, "hidden before dispatch still rewards once after return");

            runner.Request("test", callback); id = Field<string>(runner, "_requestId");
            int rejectedDuplicates = 0;
            runner.Request("duplicate", success => { if (!success) rejectedDuplicates++; });
            check(rejectedDuplicates == 1 && Field<string>(runner, "_requestId") == id, "duplicate request preserves original");
            State(runner, "wrong-id", "returned"); check(completed == 2, "wrong request id ignored");
            State(runner, id, "dispatched"); State(runner, id, "hidden");
            Set(runner, "_requestedAt", Time.realtimeSinceStartup - 121f); State(runner, id, "returned");
            check(completed == 3 && !reward, "return after expiry cannot grant");

            var reviveHost = new GameObject("ShareReviveValidation"); reviveHost.transform.SetParent(host.transform);
            revive = reviveHost.AddComponent<GameRevivePanel>();
            var provider = new DeferredProvider(); int revived = 0, gaveUp = 0;
            var payload = new GameRevivePanelPayload { adProvider = provider, onRevived = () => revived++, onGiveUp = () => gaveUp++ };
            revive.OnOpen(payload); Invoke(revive, "OnReviveClicked"); Invoke(revive, "OnReviveClicked");
            check(provider.callbacks.Count == 1 && Field<Coroutine>(revive, "_countdownRoutine") == null, "revive pending suppresses taps and pauses countdown");
            provider.callbacks[0](false);
            check(revived == 0 && gaveUp == 0 && !Field<bool>(revive, "_resolved") && Field<Coroutine>(revive, "_countdownRoutine") != null,
                "failed share resumes countdown without losing the run");
            Invoke(revive, "OnReviveClicked"); provider.callbacks[0](true);
            check(revived == 0, "late previous revive callback cannot complete retry");
            revive.OnClose(); revive.OnOpen(payload); provider.callbacks[1](true);
            check(revived == 0, "reopened revive ignores old request");
            Invoke(revive, "OnReviveClicked"); provider.callbacks[2](true); provider.callbacks[2](true);
            check(revived == 1 && gaveUp == 0, "current revive grants once");

            var cards = host.AddComponent<CardSelectionSystem>();
            Set(cards, "_isSelecting", true); Set(cards, "_remainingFreeRefresh", 0); Set(cards, "_remainingAdRefresh", 1);
            var shareCallbacks = new List<Action<bool>>();
            WXShareHelper.EditorRequestOverride = (title, complete) => shareCallbacks.Add(complete);
            Invoke(cards, "OnAdRefreshRequested"); Invoke(cards, "OnAdRefreshRequested");
            check(shareCallbacks.Count == 1 && Field<bool>(cards, "_shareRefreshInFlight"), "card refresh suppresses duplicate taps");
            shareCallbacks[0](false); Invoke(cards, "OnAdRefreshRequested"); shareCallbacks[0](true);
            check(Field<bool>(cards, "_shareRefreshInFlight") && Field<int>(cards, "_remainingAdRefresh") == 1, "old card callback cannot spend retry allowance");
            Set(cards, "_selectionVersion", Field<int>(cards, "_selectionVersion") + 1); shareCallbacks[1](true);
            check(Field<int>(cards, "_remainingAdRefresh") == 1, "previous selection share cannot mutate later selection");
            return "PASS: " + checks + " share reward checks (receiver, five-second no-popup timeout, expiry, revive retry, stale card callbacks).";
        }
        finally
        {
            WXShareHelper.EditorRequestOverride = previousOverride;
            if (revive != null) revive.OnClose();
            UnityEngine.Object.Destroy(host);
        }
    }

    [Serializable] private sealed class StateMessage { public string requestId, state; }
    private static void State(WXShareRequestRunner runner, string id, string state) =>
        runner.OnShareBridgeState(JsonUtility.ToJson(new StateMessage { requestId = id, state = state }));
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private sealed class DeferredProvider : IReviveAdProvider
    {
        public readonly List<Action<bool>> callbacks = new List<Action<bool>>();
        public void RequestReviveAd(Action<bool> onComplete) => callbacks.Add(onComplete);
    }
}
#endif
