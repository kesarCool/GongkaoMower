using UnityEngine;

/// <summary>
/// 单帧爆发特效：放大 + 淡出 + 自毁。
/// 低频用 Instantiate+Destroy，高频勾 usePooling 走对象池。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BurstVfx : MonoBehaviour
{
    [Tooltip("存活时间（秒）")]
    public float duration = 0.35f;
    [Tooltip("缩放：开始→结束")]
    public float scaleFrom = 0.5f;
    public float scaleTo = 1.0f;
    [Tooltip("随机旋转 ± 度数")]
    public float randomRotation = 30f;
    [Tooltip("最小透明度（1=全程可见，0.3=留残影）")]
    [Range(0f, 1f)]
    public float minAlpha = 0.3f;
    [Tooltip("勾选后走对象池回收（高频技能用），否则 Instantiate+Destroy")]
    public bool usePooling;

    private void OnEnable()
    {
        _elapsed = 0f;
        // 池化复用：强制重置缩放，避免累乘
        transform.localScale = Vector3.one;
        if (randomRotation > 0f)
            transform.rotation = Quaternion.Euler(0, 0, Random.Range(-randomRotation, randomRotation));
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) { var c = sr.color; c.a = 1f; sr.color = c; _sr = sr; }
    }

    private float _elapsed;
    private SpriteRenderer _sr;

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / duration);
        float s = Mathf.Lerp(scaleFrom, scaleTo, t);
        transform.localScale = Vector3.one * s;
        if (_sr != null) { var c = _sr.color; c.a = Mathf.Lerp(1f, minAlpha, t); _sr.color = c; }
        if (t >= 1f)
        {
            if (usePooling)
                GameObjectPool.Release(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
