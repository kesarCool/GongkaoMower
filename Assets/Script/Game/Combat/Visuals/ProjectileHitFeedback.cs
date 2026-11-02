using UnityEngine;

/// <summary>Optional presentation only: pooled hit stamp and clean reusable projectile trails.</summary>
[DisallowMultipleComponent]
public sealed class ProjectileHitFeedback : MonoBehaviour, IPoolReceiver
{
    [SerializeField] private GameObject hitPrefab;
    [Min(.01f)] [SerializeField] private float visualRadius = .45f;
    private TrailRenderer[] _trails;

    private void Awake() { _trails = GetComponentsInChildren<TrailRenderer>(true); }
    private void OnEnable() { ResetTrails(true); }
    private void OnDisable() { ResetTrails(false); }
    public void OnPoolGet() { ResetTrails(true); }
    public void OnPoolRelease() { ResetTrails(false); }

    public bool Play(Collider2D target)
    {
        if (hitPrefab == null || target == null) return false;
        Vector2 point = CombatHitPlacement.Resolve(target, transform.position, out var label, out var anchor, out var localPoint);
        return CombatVfxSpawner.TryPlayPooled(hitPrefab, new Vector3(point.x, point.y, -.15f), Quaternion.identity, visualRadius, label, anchor, localPoint);
    }

    private void ResetTrails(bool emitting)
    {
        if (_trails == null) _trails = GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < _trails.Length; i++)
        {
            if (_trails[i] == null) continue;
            _trails[i].emitting = false;
            _trails[i].Clear();
            _trails[i].emitting = emitting;
        }
    }
}
