using UnityEngine;

/// <summary>
/// 环绕刀片纯表现：自转、拖尾/粒子；伤害由同物体上的 <see cref="SkillOrbBladeHit"/> 处理。
/// </summary>
[DisallowMultipleComponent]
public class OrbitingBladeVisual : MonoBehaviour, IPoolReceiver
{
    [Tooltip("刀身相对轨道根的自转角速度（度/秒）")]
    public float spinSpeedDeg = 420f;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private ParticleSystem[] _particles;

    public void Apply(Sprite sprite, Color tint, int sortingOrder, float scale)
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            if (sprite != null)
                _spriteRenderer.sprite = sprite;
            _spriteRenderer.color = tint;
            _spriteRenderer.sortingOrder = sortingOrder;
        }

        transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    public void SetSpinSpeed(float degreesPerSecond)
    {
        spinSpeedDeg = degreesPerSecond;
    }

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (Mathf.Abs(spinSpeedDeg) < 0.01f) return;
        transform.Rotate(0f, 0f, spinSpeedDeg * Time.deltaTime, Space.Self);
    }

    public void OnPoolGet()
    {
        if (_trail != null)
        {
            _trail.Clear();
            _trail.emitting = true;
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null) continue;
            _particles[i].Clear(true);
            _particles[i].Play(true);
        }
    }

    public void OnPoolRelease()
    {
        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null) continue;
            _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Reset()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _trail = GetComponentInChildren<TrailRenderer>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }
}
