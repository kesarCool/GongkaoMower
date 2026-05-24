using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Editor / Standalone / WebGL：UnityWebRequest 缓存 + AudioSource 池。</summary>
public sealed class UnityAudioBackend : IAudioBackend
{
    private const int DefaultPoolSize = 12;

    private readonly Transform _audioRoot;
    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>(32);
    private readonly HashSet<AudioLoadGroup> _loadedGroups = new HashSet<AudioLoadGroup>();
    private readonly List<AudioSource> _pool = new List<AudioSource>(DefaultPoolSize);
    private readonly Dictionary<int, AudioSource> _loopSources = new Dictionary<int, AudioSource>(4);
    private readonly HashSet<int> _activeLoopKeys = new HashSet<int>();
    private bool _loopsPaused;
    private int _poolCursor;

    public UnityAudioBackend(Transform audioRoot)
    {
        _audioRoot = audioRoot;
    }

    public bool IsGroupLoaded(AudioLoadGroup group) => _loadedGroups.Contains(group);

    public IEnumerator LoadGroupRoutine(AudioLoadGroup group, AudioCatalog catalog, MonoBehaviour host)
    {
        if (catalog == null || _loadedGroups.Contains(group))
            yield break;

        foreach (AudioCatalog.Entry entry in catalog.EnumerateGroup(group))
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.relativePath))
                continue;

            if (_clipCache.ContainsKey(entry.relativePath))
                continue;

            yield return LoadClipRoutine(entry.relativePath);
        }

        _loadedGroups.Add(group);
    }

    public void Play(string catalogRelativePath, float volume, float pitchVariation = 0f, float pitchOffset = 0f, float volumeVariation = 0f)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return;

        if (!_clipCache.TryGetValue(catalogRelativePath, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning("[UnityAudioBackend] 未加载音效：" + catalogRelativePath);
            return;
        }

        float pitch = 1f + pitchOffset;
        if (pitchVariation > 0.0001f)
            pitch += Random.Range(-pitchVariation, pitchVariation);

        float vol = Mathf.Clamp01(volume);
        if (volumeVariation > 0.0001f)
            vol = Mathf.Clamp01(vol + Random.Range(-volumeVariation, volumeVariation));

        AudioSource source = RentSource();
        source.pitch = pitch;
        source.PlayOneShot(clip, vol);
    }

    public void PlayLoop(string catalogRelativePath, float volume, int loopKey)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return;

        if (!_clipCache.TryGetValue(catalogRelativePath, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning("[UnityAudioBackend] 未加载循环音效：" + catalogRelativePath);
            return;
        }

        if (!_loopSources.TryGetValue(loopKey, out AudioSource source) || source == null)
        {
            source = CreateSource();
            source.name = "LoopSfx_" + loopKey;
            _loopSources[loopKey] = source;
        }

        _activeLoopKeys.Add(loopKey);
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.loop = true;
        source.spatialBlend = 0f;

        if (_loopsPaused)
            source.Stop();
        else
            source.Play();
    }

    public void StopLoop(int loopKey)
    {
        _activeLoopKeys.Remove(loopKey);

        if (!_loopSources.TryGetValue(loopKey, out AudioSource source) || source == null)
            return;

        source.Stop();
        source.clip = null;
    }

    public void SetLoopsPaused(bool paused)
    {
        if (_loopsPaused == paused) return;
        _loopsPaused = paused;

        foreach (int loopKey in _activeLoopKeys)
        {
            if (!_loopSources.TryGetValue(loopKey, out AudioSource source) || source == null || source.clip == null)
                continue;

            if (paused)
                source.Stop();
            else
                source.Play();
        }
    }

    public void StopAll()
    {
        _activeLoopKeys.Clear();

        foreach (KeyValuePair<int, AudioSource> kv in _loopSources)
        {
            if (kv.Value != null)
                kv.Value.Stop();
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            AudioSource s = _pool[i];
            if (s != null)
                s.Stop();
        }
    }

    private AudioSource RentSource()
    {
        EnsurePool();
        if (_pool.Count == 0)
            return CreateSource();

        _poolCursor = (_poolCursor + 1) % _pool.Count;
        AudioSource src = _pool[_poolCursor];
        if (src == null)
        {
            src = CreateSource();
            _pool[_poolCursor] = src;
        }

        if (src.isPlaying)
            src.Stop();

        return src;
    }

    private void EnsurePool()
    {
        if (_pool.Count > 0) return;

        for (int i = 0; i < DefaultPoolSize; i++)
            _pool.Add(CreateSource());
    }

    private AudioSource CreateSource()
    {
        var go = new GameObject("SfxSource");
        if (_audioRoot != null)
            go.transform.SetParent(_audioRoot, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private IEnumerator LoadClipRoutine(string catalogRelativePath)
    {
#if UNITY_EDITOR
        if (TryLoadImportedClipEditor(catalogRelativePath, out AudioClip imported))
        {
            _clipCache[catalogRelativePath] = imported;
            yield break;
        }
#endif

        string url = BuildLoadUrl(catalogRelativePath);
        if (string.IsNullOrEmpty(url))
            yield break;

        AudioType audioType = GuessAudioType(catalogRelativePath);
        using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        if (req.downloadHandler is DownloadHandlerAudioClip dh)
            dh.compressed = false;
        req.timeout = 15;
        yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
        bool success = req.result == UnityWebRequest.Result.Success;
#else
        bool success = !req.isNetworkError && !req.isHttpError;
#endif

        if (!success)
        {
            Debug.LogWarning("[UnityAudioBackend] 加载失败：" + catalogRelativePath + " err=" + req.error);
            yield break;
        }

        AudioClip clip = null;
        string getContentError = null;
        try
        {
            clip = DownloadHandlerAudioClip.GetContent(req);
        }
        catch (System.Exception ex)
        {
            getContentError = ex.Message;
        }

        if (clip == null || clip.length <= 0.001f)
        {
            Debug.LogWarning("[UnityAudioBackend] AudioClip 无效（空或长度 0）：" + catalogRelativePath +
                             (getContentError != null ? " ex=" + getContentError : ""));
            yield break;
        }

        clip.name = Path.GetFileNameWithoutExtension(catalogRelativePath);
        _clipCache[catalogRelativePath] = clip;
    }

#if UNITY_EDITOR
    private static bool TryLoadImportedClipEditor(string catalogRelativePath, out AudioClip clip)
    {
        clip = null;
        if (string.IsNullOrWhiteSpace(catalogRelativePath))
            return false;

        string assetPath = "Assets/Res/" + catalogRelativePath.Trim().Replace('\\', '/');
        clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        return clip != null && clip.length > 0.001f;
    }
#endif

    private static string BuildLoadUrl(string catalogRelativePath)
    {
#if UNITY_EDITOR
        string filePath = AudioPathUtility.ResolveEditorSourcePath(catalogRelativePath);
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            return new System.Uri(filePath).AbsoluteUri;
#endif

        string streaming = AudioPathUtility.ResolveStreamingUrl(catalogRelativePath);
        if (!string.IsNullOrEmpty(streaming) && File.Exists(streaming))
            return streaming;

        return null;
    }

    private static AudioType GuessAudioType(string path)
    {
        string ext = Path.GetExtension(path)?.ToLowerInvariant();
        return ext switch
        {
            ".wav" => AudioType.WAV,
            ".ogg" => AudioType.OGGVORBIS,
            _ => AudioType.MPEG,
        };
    }
}
