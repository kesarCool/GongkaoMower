using UnityEngine;

/// <summary>标记 GameObject 为克隆体，防止分身技能无限套娃。</summary>
[DisallowMultipleComponent]
public sealed class CloneMarker : MonoBehaviour
{
}
