using System.Collections;
using UnityEngine;

/// <summary>平台音频后端：Unity AudioSource 池或微信 InnerAudioContext。</summary>
public interface IAudioBackend
{
    bool IsGroupLoaded(AudioLoadGroup group);

    IEnumerator LoadGroupRoutine(AudioLoadGroup group, AudioCatalog catalog, MonoBehaviour host);

    void Play(string catalogRelativePath, float volume, float pitchVariation = 0f, float pitchOffset = 0f, float volumeVariation = 0f);

    void PlayLoop(string catalogRelativePath, float volume, int loopKey);

    void StopLoop(int loopKey);

    void SetLoopsPaused(bool paused);

    void StopAll();
}
