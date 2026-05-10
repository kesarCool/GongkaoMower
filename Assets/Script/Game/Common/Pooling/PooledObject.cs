using UnityEngine;

[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    // Prefab.GetInstanceID() for the pool source
    public int sourcePrefabId;
}

