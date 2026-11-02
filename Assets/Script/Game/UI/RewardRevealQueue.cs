using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Serializes successful local reward receipts. Never grants currency, fragments or unlocks.</summary>
[DisallowMultipleComponent]
public sealed class RewardRevealQueue : MonoBehaviour
{
    readonly Queue<RewardRevealRequest> pending = new Queue<RewardRevealRequest>();
    readonly HashSet<string> seen = new HashSet<string>();
    readonly Queue<string> history = new Queue<string>();
    UIManager manager;
    Coroutine pump;
    public RewardRevealPanel Current { get; private set; }
    public int PendingCount => pending.Count;
    public bool IsShowing => Current != null && Current.IsOpen;
    public static RewardRevealQueue Instance
    {
        get
        {
            var ui = UIManager.Instance;
            return ui == null ? null : ui.GetComponent<RewardRevealQueue>() ?? ui.gameObject.AddComponent<RewardRevealQueue>();
        }
    }

    void Awake() { manager = GetComponent<UIManager>(); SceneManager.sceneUnloaded += OnSceneUnloaded; }
    void OnEnable()
    {
        if (pump == null && (pending.Count > 0 || IsShowing)) pump = StartCoroutine(Pump());
    }
    void OnDisable()
    {
        if (pump != null) StopCoroutine(pump);
        pump = null;
    }
    void OnDestroy() { SceneManager.sceneUnloaded -= OnSceneUnloaded; }
    void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != "Home") return;
        // Items are already saved. Stale local presentation must not interrupt a new battle.
        pending.Clear();
        if (Current != null && manager != null && manager.Top == Current) Current.Dismiss();
    }

    public static bool ShowItem(string receiptId, string source, int itemId, int count)
    {
        if (itemId <= 0 || count <= 0) return false;
        return ShowItems(receiptId, source, new[] { RewardRevealRequest.Item(itemId, count) });
    }
    public static bool ShowItems(string receiptId, string source, IEnumerable<RewardItemEntry> items, string title = "恭喜获得")
    {
        if (items == null) return false;
        var request = new RewardRevealRequest { receiptId = receiptId, source = source, title = title };
        foreach (var item in items)
        {
            if (item == null || item.itemId <= 0 || item.count <= 0) continue;
            var existing = request.items.Find(i => i.itemId == item.itemId);
            if (existing == null) request.items.Add(RewardRevealRequest.Copy(item));
            else existing.count = (int)Math.Min(int.MaxValue, (long)existing.count + item.count);
        }
        return request.items.Count > 0 && Submit(request);
    }
    public static bool ShowCharacter(string receiptId, string source, string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        return Submit(new RewardRevealRequest { receiptId = receiptId, source = source, title = "新伙伴加入", characterId = characterId });
    }
    static bool Submit(RewardRevealRequest request)
    {
        var queue = Instance;
        if (queue == null) { Debug.LogWarning("[Reward reveal] UIManager is unavailable; earned rewards remain saved."); return false; }
        if (string.IsNullOrEmpty(request.receiptId)) request.receiptId = Guid.NewGuid().ToString("N");
        if (!queue.seen.Add(request.receiptId)) return false;
        queue.history.Enqueue(request.receiptId);
        while (queue.history.Count > 256) queue.seen.Remove(queue.history.Dequeue());
        queue.pending.Enqueue(request);
        if (queue.pump == null) queue.pump = queue.StartCoroutine(queue.Pump());
        return true;
    }
    IEnumerator Pump()
    {
        // Confirm callbacks execute before CloseConfirm. Always cross a frame boundary before opening.
        yield return null;
        while (manager != null && (pending.Count > 0 || IsShowing))
        {
            if (IsShowing || !manager.CanPresentQueuedFeedback || !SceneManager.GetSceneByName("Home").isLoaded)
            { yield return null; continue; }
            if (pending.Count == 0) break;
            if (!manager.HasRegisteredPanel<RewardRevealPanel>())
            {
                Debug.LogWarning("[Reward reveal] Panel prefab is not registered. Earned rewards remain saved.");
                pending.Clear(); break;
            }
            ItemTooltip.Hide();
            Current = manager.Open<RewardRevealPanel>(pending.Dequeue(), UiOpenOptions.NonPausingModal);
            if (Current == null) break;
            yield return null;
        }
        Current = null; pump = null;
    }
    public void SkipCurrent() { if (IsShowing) Current.CompleteReveal(); }
    public void CloseCurrent() { if (IsShowing) Current.Dismiss(); }
}
