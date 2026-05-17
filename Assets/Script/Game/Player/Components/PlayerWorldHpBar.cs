using UnityEngine;

/// <summary>
/// 驱动 <c>ObjHp/Hp</c> 子物体横向缩放表示血量（世界 Sprite 条，不随 Body 受击缩放）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHpBar : MonoBehaviour
{
    [Tooltip("前景条 Transform（ObjHp 下的 Hp）；留空则自动查找")]
    [SerializeField] private Transform fillTransform;

    [SerializeField] private PlayerHealth playerHealth;

    private Vector3 _fullLocalScale = Vector3.one;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        ResolveFillTransform();
        if (fillTransform != null)
            _fullLocalScale = fillTransform.localScale;
    }

    private void OnEnable()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.OnHealthChanged.AddListener(OnHealthChanged);

        Refresh();
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged.RemoveListener(OnHealthChanged);
    }

    private void ResolveFillTransform()
    {
        if (fillTransform != null)
            return;

        Transform objHp = transform.Find("ObjHp");
        if (objHp == null)
        {
            foreach (Transform child in transform)
            {
                if (child.name == "ObjHp" || child.name.StartsWith("ObjHp"))
                {
                    objHp = child;
                    break;
                }
            }
        }

        if (objHp != null)
        {
            Transform hp = objHp.Find("Hp");
            if (hp != null)
                fillTransform = hp;
        }
    }

    private void OnHealthChanged(float _, float __)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (fillTransform == null)
            ResolveFillTransform();

        if (fillTransform == null || playerHealth == null)
            return;

        float max = Mathf.Max(0.0001f, playerHealth.MaxHp);
        float ratio = Mathf.Clamp01(playerHealth.Hp / max);
        fillTransform.localScale = new Vector3(_fullLocalScale.x * ratio, _fullLocalScale.y, _fullLocalScale.z);
    }
}
