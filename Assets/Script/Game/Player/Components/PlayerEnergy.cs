using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerEnergy
/// - 击杀掉落能量 -> 玩家拾取 -> 累计能量
/// - 能量满后触发“肉鸽选卡”事件（第一版先打印，后续接 UI）
/// </summary>
[DisallowMultipleComponent]
public class PlayerEnergy : MonoBehaviour
{
    [System.Serializable] public class IntEvent : UnityEvent<int> { }

    [Header("能量")]
    [Tooltip("当前能量")]
    public int energy = 0;

    [Tooltip("触发后是否扣除能量（通常为 true，表示消耗一轮能量进入选卡）")]
    public bool consumeEnergyOnTrigger = true;

    /// <summary>本局已触发选卡次数（0 表示尚未触发过）。</summary>
    public int CompletedCardSelectionCount => _triggerCount;

    /// <summary>下次选卡所需能量：第 1 次 1、第 2 次 2……即 <c>已完成次数 + 1</c>。</summary>
    public int EnergyRequiredForNextCard => Mathf.Max(1, _triggerCount + 1);

    [Header("事件")]
    [Tooltip("能量变化时触发（参数为当前能量）")]
    public IntEvent OnEnergyChanged = new IntEvent();

    [Tooltip("能量达到阈值、应该弹出选卡时触发（参数为触发次数/轮次）")]
    public IntEvent OnCardSelectionTriggered = new IntEvent();

    private int _triggerCount;

    private void Start()
    {
        // 初始同步一次 UI（如果有订阅）
        OnEnergyChanged.Invoke(energy);
    }

    public void AddEnergy(int amount)
    {
        if (amount <= 0) return;

        energy += amount;
        if (energy < 0) energy = 0;
        OnEnergyChanged.Invoke(energy);

        TryTriggerCardSelection();
    }

    /// <summary>新局重置选卡次数与能量（可选，由关卡入口调用）。</summary>
    public void ResetForNewRun()
    {
        energy = 0;
        _triggerCount = 0;
        OnEnergyChanged.Invoke(energy);
    }

    private void TryTriggerCardSelection()
    {
        while (energy >= EnergyRequiredForNextCard)
        {
            int need = EnergyRequiredForNextCard;

            _triggerCount += 1;

            if (consumeEnergyOnTrigger)
                energy -= need;

            if (energy < 0) energy = 0;

            Debug.Log($"[PlayerEnergy] 触发选卡：第{_triggerCount}次，消耗能量={need}，剩余={energy}，下次需要={EnergyRequiredForNextCard}");
            OnEnergyChanged.Invoke(energy);
            OnCardSelectionTriggered.Invoke(_triggerCount);

            EventBus.Publish(new CardSelectionTriggeredEvent
            {
                player = transform,
                triggerCount = _triggerCount,
                energyLeft = energy
            });
        }
    }
}

