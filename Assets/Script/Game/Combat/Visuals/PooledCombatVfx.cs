using System.Collections;
using UnityEngine;

/// <summary>
/// 池化战斗特效组件：由 <see cref="CombatVfxSpawner"/> 借出后 <see cref="Play"/>，播完自动回池。
/// </summary>
[DisallowMultipleComponent]
public class PooledCombatVfx : MonoBehaviour, IPoolReceiver
{
    [SerializeField] private ParticleSystem[] _particles;
    [Tooltip("0 = 按粒子 main.duration + startLifetime 估算")]
    [SerializeField] private float lifetimeOverride;
    [SerializeField] private int sortingOrder = 100;

    private Coroutine _releaseRoutine;

    public void Play()
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        ApplyRenderSettings();
        StopParticles(true);
        PlayParticles();

        float life = lifetimeOverride > 0f ? lifetimeOverride : EstimateLifetime();
        _releaseRoutine = StartCoroutine(ReleaseAfter(Mathf.Max(0.05f, life)));
    }

    public void OnPoolGet()
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }
    }

    public void OnPoolRelease()
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        StopParticles(true);
    }

    private IEnumerator ReleaseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _releaseRoutine = null;
        CombatVfxSpawner.NotifyReleased(gameObject);
        GameObjectPool.Release(gameObject);
    }

    private void ApplyRenderSettings()
    {
        ParticleSystemRenderer[] renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            ParticleSystemRenderer r = renderers[i];
            if (r == null) continue;
            r.sortingLayerID = 0;
            r.sortingOrder = sortingOrder;
        }
    }

    private void PlayParticles()
    {
        if (_particles == null || _particles.Length == 0)
            _particles = GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null) continue;
            _particles[i].Clear(true);
            _particles[i].Play(true);
        }
    }

    private void StopParticles(bool clear)
    {
        if (_particles == null) return;

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null) continue;
            _particles[i].Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private float EstimateLifetime()
    {
        if (_particles == null || _particles.Length == 0)
            _particles = GetComponentsInChildren<ParticleSystem>(true);

        float max = 0.35f;
        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem ps = _particles[i];
            if (ps == null) continue;

            var main = ps.main;
            float t = main.duration;
            if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                t += main.startLifetime.constantMax;
            else
                t += main.startLifetime.constant;

            if (t > max) max = t;
        }

        return max;
    }

    private void Reset()
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }
}
