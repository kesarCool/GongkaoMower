#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM;

/// <summary>微信小游戏：<see cref="WXInnerAudioContext"/> 池 + needDownload 播放。</summary>
public sealed class WeChatAudioBackend : IAudioBackend
{
    private const int PoolSize = 10;

    private readonly List<WXInnerAudioContext> _pool = new List<WXInnerAudioContext>(PoolSize);
    private readonly Dictionary<int, WXInnerAudioContext> _loopContexts = new Dictionary<int, WXInnerAudioContext>(4);
    private readonly HashSet<int> _activeLoopKeys = new HashSet<int>();
    private readonly HashSet<AudioLoadGroup> _loadedGroups = new HashSet<AudioLoadGroup>();
    private bool _loopsPaused;
    private int _poolCursor;

    public bool IsGroupLoaded(AudioLoadGroup group) => _loadedGroups.Contains(group);

    public IEnumerator LoadGroupRoutine(AudioLoadGroup group, AudioCatalog catalog, MonoBehaviour host)
    {
        if (catalog == null || _loadedGroups.Contains(group))
            yield break;

        EnsurePool();

        foreach (AudioCatalog.Entry entry in catalog.EnumerateGroup(group))
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.relativePath))
                continue;

            string src = AudioPathUtility.ResolveWeChatSrc(entry.relativePath);
            var ctx = RentContext();
            ctx.src = src;
            ctx.needDownload = true;
            ctx.volume = 0f;
            ctx.loop = false;
            ctx.Stop();

            yield return null;
        }

        _loadedGroups.Add(group);
    }

    public void Play(string catalogRelativePath, float volume)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return;

        EnsurePool();
        WXInnerAudioContext ctx = RentContext();
        ctx.volume = Mathf.Clamp01(volume);
        ctx.loop = false;
        ctx.src = AudioPathUtility.ResolveWeChatSrc(catalogRelativePath);
        ctx.needDownload = true;
        ctx.Stop();
        ctx.Play();
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
        ctx.volume = Mathf.Clamp01(volume);
        ctx.loop = true;
        ctx.src = AudioPathUtility.ResolveWeChatSrc(catalogRelativePath);
        ctx.needDownload = true;
        ctx.Stop();

        if (!_loopsPaused)
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

            if (paused)
                ctx.Stop();
            else
                ctx.Play();
        }
    }

    public void StopAll()
    {
        _activeLoopKeys.Clear();

        foreach (KeyValuePair<int, WXInnerAudioContext> kv in _loopContexts)
        {
            if (kv.Value != null)
                kv.Value.Stop();
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            WXInnerAudioContext ctx = _pool[i];
            if (ctx != null)
                ctx.Stop();
        }
    }

    private void EnsurePool()
    {
        while (_pool.Count < PoolSize)
            _pool.Add(WX.CreateInnerAudioContext(new InnerAudioContextParam()));
    }

    private WXInnerAudioContext RentContext()
    {
        EnsurePool();
        _poolCursor = (_poolCursor + 1) % _pool.Count;
        return _pool[_poolCursor];
    }
}
#endif
