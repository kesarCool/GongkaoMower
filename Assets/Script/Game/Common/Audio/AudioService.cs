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

        LoadMutePrefs();
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
        if (_sfxMute) return;

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
        if (_sfxMute) return;

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

    // ── 背景音乐 ──

    /// <summary>BGM 静音键。</summary>
    public const string BgmMuteKey = "audio_bgm_mute";
    /// <summary>BGM 音量键（0~1）。</summary>
    public const string BgmVolumeKey = "audio_bgm_volume";
    /// <summary>音效静音键。</summary>
    public const string SfxMuteKey = "audio_sfx_mute";

    private AudioSource _bgmSource;
    private AudioClip _bgmClip;
    private string _bgmRelativePath;
    private float _bgmCatalogVolume = 0.7f;
    private bool _bgmMute;
    private float _bgmVolume = 0.7f;
    private bool _sfxMute;

    /// <summary>BGM 是否已激活（进入战斗后为 true，离开战斗后为 false）。</summary>
    private bool _bgmActive;
    /// <summary>BGM 是否因 UI 暂停锁暂挂（true = 暂停中，不应启动播放）。</summary>
    private bool _bgmPaused;

    /// <summary>背景音乐开关，PlayerPrefs 持久化。</summary>
    public bool BgmMute
    {
        get => _bgmMute;
        set
        {
            _bgmMute = value;
            PlayerPrefs.SetInt(BgmMuteKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyBgmPlayback();
        }
    }

    /// <summary>背景音乐音量（0~1），PlayerPrefs 持久化。</summary>
    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, _bgmVolume);
            PlayerPrefs.Save();
            ApplyBgmVolume();
        }
    }

    /// <summary>音效开关，PlayerPrefs 持久化。</summary>
    public bool SfxMute
    {
        get => _sfxMute;
        set
        {
            _sfxMute = value;
            PlayerPrefs.SetInt(SfxMuteKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>播放战斗背景音乐（进入战斗时调用）。无论当前静音与否均加载资源，供后续解除静音时立即播放。</summary>
    public void PlayBattleBgm()
    {
        _bgmActive = true;

        if (catalog == null) return;
        LoadBgmClipIfNeeded();
        if (_bgmClip == null) return;

        if (_bgmSource == null)
        {
            var go = new GameObject("BgmSource");
            go.transform.SetParent(transform, false);
            _bgmSource = go.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
        }

        if (_bgmSource.clip != _bgmClip)
            _bgmSource.clip = _bgmClip;

        ApplyBgmPlayback();
    }

    /// <summary>停止战斗背景音乐（离开战斗时调用）。</summary>
    public void StopBattleBgm()
    {
        _bgmActive = false;
        _bgmPaused = false;

        if (_bgmSource != null)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
        }
    }

    /// <summary>暂停战斗背景音乐（暂停面板打开时由 UIManager 调用）。</summary>
    public void PauseBattleBgm()
    {
        _bgmPaused = true;
        if (_bgmSource != null && _bgmSource.isPlaying)
            _bgmSource.Pause();
    }

    /// <summary>恢复战斗背景音乐（暂停面板关闭时由 UIManager 调用）。</summary>
    public void ResumeBattleBgm()
    {
        _bgmPaused = false;
        ApplyBgmPlayback();
    }

    /// <summary>
    /// 统一的 BGM 播放决策：<br/>
    /// 条件：_bgmActive AND !_bgmMute AND !_bgmPaused → 播放<br/>
    /// 否则 → 暂停（如有音源在播）
    /// </summary>
    private void ApplyBgmPlayback()
    {
        if (_bgmSource == null || _bgmClip == null)
            return;

        bool shouldPlay = _bgmActive && !_bgmMute && !_bgmPaused;

        if (shouldPlay)
        {
            if (!_bgmSource.isPlaying)
            {
                _bgmSource.clip = _bgmClip;
                ApplyBgmVolume();
                // time > 0 = 之前被暂停过，UnPause 从断点续播；否则 Play 全新开始
                if (_bgmSource.time > 0f)
                    _bgmSource.UnPause();
                else
                    _bgmSource.Play();
            }
        }
        else
        {
            if (_bgmSource.isPlaying)
                _bgmSource.Pause();
        }
    }

    private void ApplyBgmVolume()
    {
        if (_bgmSource != null)
            _bgmSource.volume = _bgmMute ? 0f : (_bgmCatalogVolume * _bgmVolume);
    }

    private void LoadBgmClipIfNeeded()
    {
        if (_bgmClip != null) return;

        // 从 AudioCatalog MusicSection 读取 BGM 资源路径与基准音量
        if (catalog.TryGet(AudioId.BgmGame, out AudioCatalog.Entry entry) && entry != null)
        {
            _bgmRelativePath = entry.relativePath;
            _bgmCatalogVolume = entry.volume;
        }

        if (string.IsNullOrWhiteSpace(_bgmRelativePath))
        {
            Debug.LogWarning("[AudioService] AudioCatalog 未配置 BgmGame，无法加载 BGM");
            return;
        }

        // 移除扩展名后从 Resources 加载
        string resourcePath = System.IO.Path.ChangeExtension(_bgmRelativePath, null);
        _bgmClip = Resources.Load<AudioClip>(resourcePath);
        if (_bgmClip == null)
            Debug.LogWarning($"[AudioService] 未找到 BGM 资源：{resourcePath}（catalog 路径：{_bgmRelativePath}）");
    }

    private void LoadMutePrefs()
    {
        // 迁移旧主音量键
        const string oldKey = "audio_master_mute";
        if (PlayerPrefs.HasKey(oldKey))
        {
            bool oldVal = PlayerPrefs.GetInt(oldKey) == 1;
            PlayerPrefs.SetInt(BgmMuteKey, oldVal ? 1 : 0);
            PlayerPrefs.SetInt(SfxMuteKey, oldVal ? 1 : 0);
            PlayerPrefs.DeleteKey(oldKey);
        }

        _bgmMute = PlayerPrefs.GetInt(BgmMuteKey, 0) == 1;
        _bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 0.7f);
        _sfxMute = PlayerPrefs.GetInt(SfxMuteKey, 0) == 1;
    }

    private IAudioBackend CreateBackend()
    {
        // 微信环境用 UnityAudioBackend：StreamingAssets 路径 + UnityWebRequest 加载
        // InnerAudioContext 不支持代码包本地文件，只认网络/CDN/wxfile://usr
        return new UnityAudioBackend(transform);
    }
}
