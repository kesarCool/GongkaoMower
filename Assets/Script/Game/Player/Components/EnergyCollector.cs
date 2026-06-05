using UnityEngine;

/// <summary>
/// 自动收集能量：每隔 collectInterval 秒，将范围内所有 EnergyPickup 强制拉向玩家。
/// 挂 Player 上，配合 EnergyPickup.ForceCollectBy 使用。
/// </summary>
[DisallowMultipleComponent]
public class EnergyCollector : MonoBehaviour
{
    [Tooltip("每隔几秒收一波")]
    public float collectInterval = 2f;
    [Tooltip("收集半径")]
    public float collectRadius = 5f;

    private float _timer;

    private void Update()
    {
        if (collectInterval <= 0f || collectRadius <= 0f) return;

        _timer += Time.deltaTime;
        if (_timer < collectInterval) return;
        _timer = 0f;

        CollectInRadius();
    }

    private void CollectInRadius()
    {
        bool prev = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;
        var hits = Physics2D.OverlapCircleAll(transform.position, collectRadius);

        int collected = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            EnergyPickup ep = hits[i].GetComponent<EnergyPickup>();
            if (ep == null) ep = hits[i].GetComponentInParent<EnergyPickup>();
            if (ep == null || !ep.isActiveAndEnabled) continue;

            ep.ForceCollectBy(transform);
            collected++;
        }

        Physics2D.queriesHitTriggers = prev;

        if (collected > 0)
            Debug.Log($"[EnergyCollector] 自动收集 {collected} 个能量");
    }
}
