using UnityEngine;

/// <summary>
/// 标记墙壁对象，供 WallStuckResolver / 卡墙检测使用。
/// 挂在 BattleMapLoader 与 BossArenaLock 生成的围墙 GameObject 上。
/// </summary>
[DisallowMultipleComponent]
public class WallMarker : MonoBehaviour { }
