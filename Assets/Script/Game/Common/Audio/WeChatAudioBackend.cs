#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM;

/// <summary>微信小游戏：<see cref="WXInnerAudioContext"/> 异步播放后端。</summary>
public sealed class WeChatAudioBackend : IAudioBackend
{
    private const float LoadTimeout = 15f;

    private readonly Dictionary<int, WXInnerAudioContext> _loopContexts = new Dictionary<int, WXInnerAudioContext>(4);
    private readonly HashSet<int> _activeLoopKeys = new HashSet<int>();
    private readonly HashSet<AudioLoadGroup> _loadedGroups = new HashSet<AudioLoadGroup>();
    private readonly List<WXInnerAudioContext> _preloaded = new List<WXInnerAudioContext>(16);
    private bool _loopsPaused;

    public bool IsGroupLoaded(AudioLoadGroup group) => _loadedGroups.Contains(group);

    /// <summary>真正的预加载：设 src → 轮询 duration>0 → 确认文件下载并解码。</summary>
    public IEnumerator LoadGroupRoutine(AudioLoadGroup group, AudioCatalog catalog, MonoBehaviour host)
    {
        if (catalog == null || _loadedGroups.Contains(group))
            yield break;

        var queue = new List<string>(8);
        foreach (AudioCatalog.Entry entry in catalog.EnumerateGroup(group))
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.relativePath))
                queue.Add(AudioPathUtility.ResolveWeChatSrc(entry.relativePath));
        }

        if (queue.Count == 0)
        {
            _loadedGroups.Add(group);
            yield break;
        }

        int loaded = 0;
        foreach (string src in queue)
        {
            WXInnerAudioContext ctx = WX.CreateInnerAudioContext(new InnerAudioContextParam());
            ctx.needDownload = true;
            ctx.volume = 0f;
            ctx.loop = false;
            ctx.src = src;
            _preloaded.Add(ctx);

            // 轮询等待：duration > 0 说明文件已加载且解码
            float deadline = Time.unscaledTime + LoadTimeout;
            while (ctx.duration <= 0.001f && Time.unscaledTime < deadline)
                yield return null;

            if (ctx.duration > 0.001f)
                loaded++;
            else
                Debug.LogWarning($"[WeChatAudioBackend] 预加载超时: {src}");

            ctx.Stop();
            yield return null;
        }

        Debug.Log($"[WeChatAudioBackend] 预加载完成: {group}，{loaded}/{queue.Count}");
        _loadedGroups.Add(group);
    }

    /// <summary>播放：创建新 context（已缓存文件 src 赋值瞬间可用，duration 立即可读）。</summary>
    public void Play(string catalogRelativePath, float volume, float pitchVariation = 0f, float pitchOffset = 0f, float volumeVariation = 0f)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return;

        string src = AudioPathUtility.ResolveWeChatSrc(catalogRelativePath);
        float rate = 1f + pitchOffset;
        if (pitchVariation > 0.0001f)
            rate += Random.Range(-pitchVariation, pitchVariation);

        float vol = Mathf.Clamp01(volume);
        if (volumeVariation > 0.0001f)
            vol = Mathf.Clamp01(vol + Random.Range(-volumeVariation, volumeVariation));

        WXInnerAudioContext ctx = WX.CreateInnerAudioContext(new InnerAudioContextParam());
        ctx.needDownload = true;
        ctx.volume = vol;
        ctx.playbackRate = rate;
        ctx.loop = false;
        ctx.src = src;

        var host = AudioService.Instance;
        if (host != null)
            host.StartCoroutine(AwaitAndPlay(ctx, src));
    }

    private IEnumerator AwaitAndPlay(WXInnerAudioContext ctx, string src)
    {
        float deadline = Time.unscaledTime + LoadTimeout;
        while (ctx != null && ctx.duration <= 0.001f && Time.unscaledTime < deadline)
            yield return null;

        if (ctx != null && ctx.duration > 0.001f)
        {
            ctx.Play();
        }
        else if (ctx != null)
        {
            Debug.LogWarning($"[WeChatAudioBackend] 播放超时: {src}");
            ctx.Destroy();
        }
    }

    public void PlayLoop(string catalogRelativePath, float volume, int loopKey)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return;

        if (!_loopContexts.TryGetValue(loopKey, out WXInnerAudioContext ctx) || ctx == null)
        {
            ctx = WX.CreateInnerAudioContext(new InnerAudioContextParam());
            _loopContexts[loopKey] = ctx;
        }

        _activeLoopKeys.Add(loopKey);
        ctx.needDownload = true;
        ctx.volume = Mathf.Clamp01(volume);
        ctx.loop = true;
        ctx.src = AudioPathUtility.ResolveWeChatSrc(catalogRelativePath);

        // 等加载完再播
        var host = AudioService.Instance;
        if (host != null)
            host.StartCoroutine(AwaitLoopPlay(ctx, loopKey));
    }

    private IEnumerator AwaitLoopPlay(WXInnerAudioContext ctx, int loopKey)
    {
        float deadline = Time.unscaledTime + LoadTimeout;
        while (ctx != null && ctx.duration <= 0.001f && Time.unscaledTime < deadline)
            yield return null;

        if (ctx != null && ctx.duration > 0.001f && !_loopsPaused && _activeLoopKeys.Contains(loopKey))
            ctx.Play();
    }

    public void StopLoop(int loopKey)
    {
        _activeLoopKeys.Remove(loopKey);
        if (!_loopContexts.TryGetValue(loopKey, out WXInnerAudioContext ctx) || ctx == null)
            return;
        ctx.loop = false;
        ctx.Stop();
    }

    public void SetLoopsPaused(bool paused)
    {
        if (_loopsPaused == paused) return;
        _loopsPaused = paused;

        foreach (int loopKey in _activeLoopKeys)
        {
            if (!_loopContexts.TryGetValue(loopKey, out WXInnerAudioContext ctx) || ctx == null)
                continue;
            if (paused) ctx.Stop();
            else ctx.Play();
        }
    }

    public void StopAll()
    {
        _activeLoopKeys.Clear();
        foreach (var kv in _loopContexts)
            kv.Value?.Stop();

        for (int i = _preloaded.Count - 1; i >= 0; i--)
        {
            try { _preloaded[i]?.Destroy(); } catch { }
        }
        _preloaded.Clear();
    }
}
#endif
