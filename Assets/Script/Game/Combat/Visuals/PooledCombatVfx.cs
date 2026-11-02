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
    [Tooltip("0 = 不按范围缩放；大于 0 表示 Prefab 原尺寸对应的伤害半径")]
    [Min(0f)] [SerializeField] private float referenceRadius;

    private Coroutine _releaseRoutine;
    private Vector3 _authoredScale;
    private bool _scaleCaptured;
    private EnemyWordLabel _hitTarget;
    private EnemyBase _hitEnemy;
    private Transform _hitAnchor;
    private Vector3 _hitLocalPoint;
    private int _hitAnchorVersion;

    public void FollowHitAnchor(EnemyWordLabel target, Transform anchor, Vector3 localPoint)
    {
        _hitTarget = target;
        _hitEnemy = target != null ? target.GetComponentInParent<EnemyBase>() : null;
        _hitAnchor = anchor;
        _hitLocalPoint = localPoint;
        _hitAnchorVersion = target != null ? target.HitAnchorVersion : 0;
    }

    private void LateUpdate()
    {
        if (_hitAnchor == null) return;
        // A dead or recycled enemy must not carry its old hit stamp to the next spawn.
        if (_hitTarget == null || !_hitTarget.isActiveAndEnabled || _hitTarget.HitAnchorVersion != _hitAnchorVersion || (_hitEnemy != null && _hitEnemy.Hp <= 0f))
        {
            FollowHitAnchor(null, null, Vector3.zero);
            return;
        }
        if (Time.timeScale == 0f) return;
        Vector3 point = _hitAnchor.TransformPoint(_hitLocalPoint);
        point.z = transform.position.z;
        transform.position = point;
    }

    public void Play()
    {
        Play(0f);
    }

    public void Play(float effectRadius)
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        if (referenceRadius > 0f)
        {
            if (!_scaleCaptured)
            {
                _authoredScale = transform.localScale;
                _scaleCaptured = true;
            }
            float scale = effectRadius > 0f ? effectRadius / referenceRadius : 1f;
            transform.localScale = _authoredScale * scale;
        }

        ApplyRenderSettings();
        StopParticles(true);
        PlayParticles();

        float life = lifetimeOverride > 0f ? lifetimeOverride : EstimateLifetime();
        _releaseRoutine = StartCoroutine(ReleaseAfter(Mathf.Max(0.05f, life)));
    }

    public void OnPoolGet()
    {
        FollowHitAnchor(null, null, Vector3.zero);
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }
    }

    public void OnPoolRelease()
    {
        FollowHitAnchor(null, null, Vector3.zero);
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        StopParticles(true);
        if (_scaleCaptured) transform.localScale = _authoredScale;
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
