using UnityEngine;

/// <summary>
/// 世界空间玩家血条：用 localScale.x 显示血量比例，无论如何 pivot 都从左侧缩。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHpBar : MonoBehaviour
{
    [SerializeField] private Transform fillTransform;
    [SerializeField] private PlayerHealth playerHealth;

    private Vector3 _fullScale;
    private float _fullWidth;
    private float _pivotOffsetX;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        ResolveFillTransform();
        if (fillTransform != null)
        {
            _fullScale = fillTransform.localScale;
            var sr = fillTransform.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                _fullWidth = sr.sprite.bounds.size.x;
            }
            else
            {
                _fullWidth = _fullScale.x;
            }

            _pivotOffsetX = 0f;

            // 如果 pivot 在中心，scale 缩下去会往两边缩，补一个位置偏移让它从左边缩
            var sr2 = fillTransform.GetComponent<SpriteRenderer>();
            if (sr2 != null)
            {
                float pivotX = sr2.sprite != null ? sr2.sprite.pivot.x / sr2.sprite.rect.width : 0.5f;
                if (pivotX > 0.1f)
                    _pivotOffsetX = -_fullWidth * 0.5f * _fullScale.x;
            }

            // 确保初始状态满血
            ApplyRatio(1f);
        }
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

    private void Start()
    {
        // 兜底：确保所有组件就绪后再刷一次
        Refresh();
    }

    private void ResolveFillTransform()
    {
        if (fillTransform != null) return;

        Transform objHp = transform.Find("ObjHp");
        if (objHp == null)
        {
            foreach (Transform child in transform)
            {
                if (child.name == "ObjHp" || child.name.StartsWith("ObjHp"))
                { objHp = child; break; }
            }
        }
        if (objHp != null)
        {
            Transform hp = objHp.Find("Hp");
            if (hp != null) fillTransform = hp;
        }
    }

    private void OnHealthChanged(float _, float __)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (fillTransform == null)
        {
            ResolveFillTransform();
            if (fillTransform == null) return;
            _fullScale = fillTransform.localScale;
        }

        if (playerHealth == null) return;

        float ratio = Mathf.Clamp01(playerHealth.Hp / Mathf.Max(0.0001f, playerHealth.MaxHp));
        ApplyRatio(ratio);
    }

    private void ApplyRatio(float ratio)
    {
        fillTransform.localScale = new Vector3(_fullScale.x * ratio, _fullScale.y, _fullScale.z);

        // pivot 补偿：如果 pivot 不在最左边，调整位置让缩条始终从左边开始
        if (Mathf.Abs(_pivotOffsetX) > 0.001f)
        {
            Vector3 pos = fillTransform.localPosition;
            pos.x = _pivotOffsetX * (1f - ratio);
            fillTransform.localPosition = pos;
        }
    }
}
