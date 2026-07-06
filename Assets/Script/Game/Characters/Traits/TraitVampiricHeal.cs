using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 击杀回血：击杀时掉落符咒（能量球），拾取回复血量%。参数：[healHpPercent, pickupDurationSec, maxPickups]
/// </summary>
public sealed class TraitVampiricHeal : TraitBehaviour
{
    private float _healPercent = 0.05f;
    private float _pickupDuration = 8f;
    private int _maxPickups = 3;

    private PlayerHealth _health;
    private int _activePickups;
    private Sprite _pickupSprite;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 3) { _healPercent = p[0]; _pickupDuration = p[1]; _maxPickups = Mathf.RoundToInt(p[2]); }
    }

    private void Start()
    {
        _health = GetComponent<PlayerHealth>();
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (_activePickups >= _maxPickups) return;

        // 兜底：没有 Sprite 时创建一个简单的圆形贴图
        Sprite sprite = Resources.Load<Sprite>("UI/Items/icon_golds");
        if (sprite == null)
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        var go = new GameObject("TraitHealPickup");
        go.transform.position = e.position;
        go.transform.localScale = Vector3.one * 0.5f;
        go.layer = gameObject.layer; // 继承玩家层确保碰撞

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 1.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.3f, 1f, 0.4f); // 绿色 = 治疗
        sr.sortingOrder = 10;

        var pickup = go.AddComponent<TraitHealPickup>();
        pickup.Init(_health, _healPercent, _pickupDuration, () => _activePickups--);

        _activePickups++;
        GameLog.Info($"[VampiricHeal] 符咒掉落：pos={e.position}, active={_activePickups}/{_maxPickups}");
    }
}

/// <summary>符咒拾取物：接触时回血 + 超时销毁。</summary>
internal sealed class TraitHealPickup : MonoBehaviour
{
    private PlayerHealth _health;
    private float _healPercent;
    private float _lifetime;
    private System.Action _onDestroyed;
    private bool _consumed;

    public void Init(PlayerHealth health, float healPercent, float lifetime, System.Action onDestroyed)
    {
        _health = health;
        _healPercent = healPercent;
        _lifetime = lifetime;
        _onDestroyed = onDestroyed;
    }

    private void Consume()
    {
        if (_consumed) return;
        _consumed = true;
        _onDestroyed?.Invoke();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!_consumed) _onDestroyed?.Invoke();
    }

    private void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f) Consume();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        int heal = Mathf.RoundToInt(_health.MaxHp * _healPercent);
        if (_health != null) _health.Heal(heal);
        GameLog.Info($"[TraitHealPickup] 拾取符咒：+{heal} HP");
        Consume();
    }
}
