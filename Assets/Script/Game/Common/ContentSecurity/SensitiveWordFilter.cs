using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 敏感词过滤器：本地黑名单（主力） + msgSecCheck 远程检测（兜底）。
/// 本地词库加载 <10ms，不影响游戏加载；远程检测在后台异步运行。
/// </summary>
public class SensitiveWordFilter
{
    public const string LogTag = "[SensitiveWord]";

    private static SensitiveWordFilter _instance;
    public static SensitiveWordFilter Instance
    {
        get { return _instance ?? (_instance = new SensitiveWordFilter()); }
    }

    // 本地黑名单
    private HashSet<string> _localBlacklist;
    private HashSet<char> _localBlackChars;
    private bool _localLoaded;

    // msgSecCheck 结果缓存（lexiconId → true=pass, false=flagged）
    private Dictionary<int, bool> _remoteCache = new Dictionary<int, bool>();
    private HashSet<int> _pendingCheckIds = new HashSet<int>();
    private bool _remoteCheckRunning;

    // 后台检测统计
    public int LocalLoadedCount { get; private set; }
    public int RemoteCacheHitCount { get; private set; }
    public int RemoteFlaggedCount { get; private set; }

    /// <summary>
    /// 从 Resources/Data/sensitive_words.txt 加载本地敏感词表。
    /// 毫秒级完成，不阻塞。
    /// </summary>
    public void LoadLocalBlacklist()
    {
        if (_localLoaded) return;

        _localBlacklist = new HashSet<string>(StringComparer.Ordinal);
        _localBlackChars = new HashSet<char>();

        var asset = Resources.Load<TextAsset>("Data/sensitive_words");
        if (asset == null)
        {
            Debug.LogWarning($"{LogTag} 未找到 sensitive_words.txt，本地词库为空");
            _localLoaded = true;
            return;
        }

        int wordCount = 0;
        int charCount = 0;
        var lines = asset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            _localBlacklist.Add(line);
            wordCount++;

            // 单字直接加入字符集；多字词拆成单字也加入
            foreach (var ch in line)
            {
                if (_localBlackChars.Add(ch))
                    charCount++;
            }
        }

        LocalLoadedCount = wordCount;
        _localLoaded = true;

        Resources.UnloadAsset(asset);
        GameLog.Info($"{LogTag} 本地词库加载完成：{wordCount} 词, {charCount} 字");
    }

    /// <summary>
    /// 从 PlayerPrefs 恢复远程检测缓存。
    /// </summary>
    public void LoadRemoteCache()
    {
        var json = PlayerPrefs.GetString("sensitive_word_cache", string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            // 格式: "id1:1,id2:0,id3:1"
            var pairs = json.Split(',');
            foreach (var pair in pairs)
            {
                var parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int val))
                    _remoteCache[id] = val == 1;
            }
            GameLog.Info($"{LogTag} 远程缓存恢复：{_remoteCache.Count} 条");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} 远程缓存解析失败：{e.Message}");
        }
    }

    private void SaveRemoteCache()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in _remoteCache)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(kv.Key);
            sb.Append(':');
            sb.Append(kv.Value ? "1" : "0");
        }
        PlayerPrefs.SetString("sensitive_word_cache", sb.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 判断词条是否允许显示。
    /// 1. 本地黑名单命中 → 拦截
    /// 2. 远程缓存命中 → 返回缓存结果
    /// 3. 未命中 → 放行（fail-open），加入后台检测队列
    /// </summary>
    public bool IsLexiconAllowed(int lexiconId, string displayText)
    {
        if (!_localLoaded)
        {
            LoadLocalBlacklist();
        }

        // 本地黑名单：完整匹配
        if (_localBlacklist != null && _localBlacklist.Contains(displayText))
        {
            LogBlock(lexiconId, displayText, "本地词库（完整匹配）");
            return false;
        }

        // 本地黑名单：单字匹配（只要包含任一敏感字就拦截）
        if (_localBlackChars != null)
        {
            foreach (var ch in displayText)
            {
                if (_localBlackChars.Contains(ch))
                {
                    LogBlock(lexiconId, displayText, $"本地词库（单字匹配 '{ch}'）");
                    return false;
                }
            }
        }

        // 远程缓存
        if (_remoteCache.TryGetValue(lexiconId, out bool allowed))
        {
            if (!allowed)
            {
                RemoteCacheHitCount++;
                LogBlock(lexiconId, displayText, "远程缓存（已标记）");
            }
            return allowed;
        }

        // 未命中：放行，加入后台检测队列
        if (!_pendingCheckIds.Contains(lexiconId))
            _pendingCheckIds.Add(lexiconId);

        return true;
    }

    private static void LogBlock(int lexiconId, string text, string reason)
    {
        var preview = text.Length > 20 ? text.Substring(0, 20) + "..." : text;
        GameLog.Info($"{LogTag} 拦截 lexiconId={lexiconId} text={preview} reason={reason}");
    }

    /// <summary>
    /// 后台协程：逐条提交 msgSecCheck，带频率控制，检测结果写入缓存。
    /// 不阻塞主流程，适合在 BattleLoadingSceneController 中通过 StartCoroutine 启动。
    /// </summary>
    public IEnumerator BackgroundCheckAll(Action<int, int, string> progressCallback = null)
    {
#if UNITY_EDITOR || !WEIXINMINIGAME
        GameLog.Info($"{LogTag} 非微信环境，跳过后台 msgSecCheck");
        yield break;
#else
        if (_remoteCheckRunning) yield break;
        _remoteCheckRunning = true;

        // 等待 TableManager 初始化（如果还未初始化）
        while (TableManager.Instance == null)
        {
            yield return null;
        }

        // 收集所有待检测词条
        var toCheck = new List<KeyValuePair<int, string>>();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<LexiconTable>();
        if (dict != null)
        {
            foreach (var kv in dict)
            {
                if (kv.Value is LexiconTable lx && !string.IsNullOrEmpty(lx.DisplayText))
                {
                    if (!_remoteCache.ContainsKey(lx.ID))
                        toCheck.Add(new KeyValuePair<int, string>(lx.ID, lx.DisplayText));
                }
            }
        }
#endif

        int total = toCheck.Count;
        if (total == 0)
        {
            GameLog.Info($"{LogTag} 后台检测：所有词条已在缓存中，跳过");
            _remoteCheckRunning = false;
            yield break;
        }

        GameLog.Info($"{LogTag} 后台检测开始：待检测 {total} 条");
        progressCallback?.Invoke(0, total, $"内容安全检查 0/{total}");

        // 初始化 msgSecCheck 桥接
        try
        {
            WeChatWASM.WebMsgSecCheck.Initialize();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} msgSecCheck 初始化失败：{e.Message}，后台检测终止");
            _remoteCheckRunning = false;
            yield break;
        }

        int sentIndex = 0;
        int resultIndex = 0;
        int passed = 0;
        int flagged = 0;
        int errors = 0;
        float lastSendTime = -999f;
        const float minInterval = 0.2f; // 200ms 间隔
        const float timeoutSeconds = 120f;
        float startTime = Time.time;

        while (resultIndex < total)
        {
            float elapsed = Time.time - startTime;

            // 消费回调结果
            WeChatWASM.WebMsgSecCheck.DrainResults(r =>
            {
                resultIndex++;
                _remoteCache[r.LexiconId] = r.IsPass;
                if (r.IsPass) passed++;
                else if (r.IsApiError) errors++;
                else flagged++;
            });

            // 提交下一批（频率控制）
            if (sentIndex < total && (Time.time - lastSendTime) >= minInterval)
            {
                var entry = toCheck[sentIndex];
                WeChatWASM.WebMsgSecCheck.Check(entry.Key, entry.Value);
                sentIndex++;
                lastSendTime = Time.time;
            }

            int done = Math.Min(resultIndex, sentIndex);
            if (done % 10 == 0 || done >= total)
                progressCallback?.Invoke(done, total, $"内容安全检查 {done}/{total}");

            // 超时保护
            if (elapsed > timeoutSeconds)
            {
                Debug.LogWarning($"{LogTag} 后台检测超时（{timeoutSeconds}s），已完成 {resultIndex}/{total}，剩余标记为通过");
                for (int i = sentIndex; i < total; i++)
                    _remoteCache[toCheck[i].Key] = true;
                break;
            }

            yield return null;
        }

        RemoteFlaggedCount = flagged;
        SaveRemoteCache();

        WeChatWASM.WebMsgSecCheck.Shutdown();
        _remoteCheckRunning = false;

        progressCallback?.Invoke(total, total,
            $"安全检查完成: 通过{passed}, 拦截{flagged}, 错误{errors}");
        GameLog.Info($"{LogTag} 后台检测完成：total={total} pass={passed} flag={flagged} err={errors}");
#endif
    }

    /// <summary>
    /// 重置所有状态（调试用）。
    /// </summary>
    public void Reset()
    {
        _localBlacklist?.Clear();
        _localBlackChars?.Clear();
        _localLoaded = false;
        _remoteCache.Clear();
        _pendingCheckIds.Clear();
        LocalLoadedCount = 0;
        RemoteCacheHitCount = 0;
        RemoteFlaggedCount = 0;
    }
}
