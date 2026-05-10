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

    [Tooltip("触发一次选卡所需能量")]
    public int energyToTriggerCard = 10;

    [Tooltip("触发后是否扣除能量（通常为 true，表示消耗一轮能量进入选卡）")]
    public bool consumeEnergyOnTrigger = true;

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

    private void TryTriggerCardSelection()
    {
        int need = Mathf.Max(1, energyToTriggerCard);
        if (energy < need) return;

        _triggerCount += 1;

        if (consumeEnergyOnTrigger)
            energy -= need;

        Debug.Log($"[PlayerEnergy] 触发选卡：第{_triggerCount}次（剩余能量={energy}）");
        OnEnergyChanged.Invoke(energy);
        OnCardSelectionTriggered.Invoke(_triggerCount);

        // 发布全局事件，后续“肉鸽选卡 UI”只需订阅这个事件即可弹出
        EventBus.Publish(new CardSelectionTriggeredEvent
        {
            player = transform,
            triggerCount = _triggerCount,
            energyLeft = energy
        });
    }
}

