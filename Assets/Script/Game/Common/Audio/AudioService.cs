using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局音效服务（DontDestroyOnLoad）：分组异步加载 + 2D 播放。
/// </summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [Tooltip("留空则从 Resources/Audio/MainAudioCatalog 加载。")]
    [SerializeField] private AudioCatalog catalog;

    private IAudioBackend _backend;
    private readonly Dictionary<AudioId, float> _lastPlayTime = new Dictionary<AudioId, float>(16);
    private bool _loopsPausedByGame;

    public static AudioService Ensure()
    {
        if (Instance != null) return Instance;

        var existing = FindObjectOfType<AudioService>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        var go = new GameObject(nameof(AudioService));
        DontDestroyOnLoad(go);
        return go.AddComponent<AudioService>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapCommonGroup()
    {
        AudioService service = Ensure();
        service.StartCoroutine(service.LoadGroupAsync(AudioLoadGroup.Common));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (catalog == null)
            catalog = Resources.Load<AudioCatalog>(AudioCatalog.DefaultResourcesPath);

        _backend = CreateBackend();

        if (GetComponent<AudioEventBridge>() == null)
            gameObject.AddComponent<AudioEventBridge>();

        if (catalog == null)
            Debug.LogWarning("[AudioService] 未找到 AudioCatalog，请在 Resources/Audio/MainAudioCatalog 创建或 Inspector 指定。");

        LoadMasterMute();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (_backend == null) return;
        SyncLoopPauseState();
    }

    private void SyncLoopPauseState()
    {
        bool paused = Time.timeScale <= 0f;
        if (paused == _loopsPausedByGame) return;

        _loopsPausedByGame = paused;
        _backend.SetLoopsPaused(paused);
    }

    public bool IsGroupLoaded(AudioLoadGroup group) => _backend != null && _backend.IsGroupLoaded(group);

    public IEnumerator LoadGroupAsync(AudioLoadGroup group)
    {
        if (_backend == null || catalog == null)
            yield break;

        yield return _backend.LoadGroupRoutine(group, catalog, this);
    }

    public void PlayUiClick() => Play(AudioId.UiClick);

    public void PlayUiClose() => Play(AudioId.UiClose);

    public void Play(AudioId id)
    {
        if (id == AudioId.None || catalog == null || _backend == null)
            return;

        if (!catalog.TryGet(id, out AudioCatalog.Entry entry) || entry == null)
        {
            Debug.LogWarning("[AudioService] 未配置 AudioId：" + id);
            return;
        }

        if (entry.minInterval > 0f &&
            _lastPlayTime.TryGetValue(id, out float last) &&
            Time.unscaledTime - last < entry.minInterval)
            return;

        _lastPlayTime[id] = Time.unscaledTime;
        _backend.Play(entry.relativePath, entry.volume, entry.pitchVariation, entry.pitchOffset, entry.volumeVariation);
    }

    /// <summary>常驻循环音（如环绕刀片）；同一 <see cref="AudioId"/> 重复调用会先停再播。</summary>
    public void PlayLoop(AudioId id)
    {
        if (id == AudioId.None || catalog == null || _backend == null)
            return;

        if (!catalog.TryGet(id, out AudioCatalog.Entry entry) || entry == null ||
            string.IsNullOrWhiteSpace(entry.relativePath))
        {
            Debug.LogWarning("[AudioService] 未配置循环音效：" + id);
            return;
        }

        SyncLoopPauseState();
        _backend.PlayLoop(entry.relativePath, entry.volume, (int)id);
    }

    public void StopLoop(AudioId id)
    {
        if (_backend == null || id == AudioId.None)
            return;
        _backend.StopLoop((int)id);
    }

    // ── 主音量静音 ──

    private const string MasterMuteKey = "audio_master_mute";
    private bool _masterMute;

    /// <summary>全局静音开关，PlayerPrefs 持久化，默认 false（不静音）。不影响局内各音效的相对缩放。</summary>
    public bool MasterMute
    {
        get => _masterMute;
        set
        {
            _masterMute = value;
            PlayerPrefs.SetInt(MasterMuteKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMasterMute();
        }
    }

    private void ApplyMasterMute()
    {
        AudioListener.volume = _masterMute ? 0f : 1f;
    }

    private void LoadMasterMute()
    {
        _masterMute = PlayerPrefs.GetInt(MasterMuteKey, 0) == 1;
        ApplyMasterMute();
    }

    private IAudioBackend CreateBackend()
    {
        // 微信环境用 UnityAudioBackend：StreamingAssets 路径 + UnityWebRequest 加载
        // InnerAudioContext 不支持代码包本地文件，只认网络/CDN/wxfile://usr
        return new UnityAudioBackend(transform);
    }
}
