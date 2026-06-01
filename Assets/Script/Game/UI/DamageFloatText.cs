using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 世界空间伤害飘字（配合 <see cref="GameObjectPool"/> 与 <see cref="DamageFloatTextPresenter"/>）。
/// Prefab 上挂本脚本 + 子级或同级的 <see cref="TextMeshPro"/>。
/// </summary>
[DisallowMultipleComponent]
public class DamageFloatText : MonoBehaviour, IPoolReceiver
{
    [SerializeField] private TMP_Text label;

    [Tooltip("总存活时间（秒）")]
    [SerializeField] private float lifetime = 0.65f;

    [Tooltip("上升速度（世界单位/秒）")]
    [SerializeField] private float riseSpeed = 1.35f;

    [Tooltip("随机水平漂移最大速度（世界单位/秒）")]
    [SerializeField] private float driftSpeed = 0.45f;

    [Tooltip("初始相对缩放的脉冲：从该值过渡到 1")]
    [SerializeField] private float spawnScalePunch = 1.2f;

    [SerializeField] private Color textColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private Color critColor = new Color(1f, 0.84f, 0f, 1f); // #FFD700 金色
    [SerializeField] private float critScaleMul = 1.5f;

    [Tooltip("2D 下可抬高 sortingOrder，避免被地形挡住")]
    [SerializeField] private int sortingOrder = 200;

    [Tooltip("为 true 时每帧朝向主相机（侧视/斜视角常用；纯俯视可关，沿用 Prefab 旋转）")]
    [SerializeField] private bool faceMainCamera;

    private Vector3 _floatDirection;
    private Vector3 _driftVelocity;
    private Coroutine _co;
    private Transform _cam;
    private bool _isCrit;

    private void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshPro>(true);
        if (label != null)
        {
            var rend = label.GetComponent<Renderer>();
            if (rend != null)
                rend.sortingOrder = sortingOrder;
        }
    }

    public void OnPoolGet()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        if (faceMainCamera && Camera.main != null)
            _cam = Camera.main.transform;
        else
            _cam = null;

        // 池化复用时重新应用中文 SDF 字体（LiberationSans 无中文）
        if (label != null)
            BattleChineseFontRuntime.ApplyToTMP(label);
    }

    public void OnPoolRelease()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
    }

    /// <summary>在调用方从对象池取出后调用，设置数值与起点。</summary>
    public void Play(float damage, Vector3 worldPosition, bool isCrit = false)
    {
        if (label == null)
        {
            GameObjectPool.Release(gameObject);
            return;
        }

        bool crit = isCrit;
        _isCrit = isCrit;
        label.text = crit ? FormatCritDamage(damage) : FormatDamage(damage);
        label.color = crit ? critColor : textColor;
        label.alpha = 1f;
        label.transform.localScale = Vector3.one * (crit ? spawnScalePunch * critScaleMul : spawnScalePunch);

        transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        label.ForceMeshUpdate(true);

        // 让文字网格的 bounds 中心落在 spawn 点上，抵消子物体 localPosition / 对齐枢轴带来的偏移
        var rend = label.GetComponent<Renderer>();
        if (rend != null)
        {
            Vector3 bc = rend.bounds.center;
            transform.position += worldPosition - bc;
        }
        else
        {
            Vector3 pivotDelta = label.transform.position - transform.position;
            if (pivotDelta.sqrMagnitude > 1e-10f)
                transform.position = worldPosition - pivotDelta;
        }

        float mainAngle = Random.Range(0f, Mathf.PI * 2f);
        _floatDirection = new Vector3(Mathf.Cos(mainAngle), Mathf.Sin(mainAngle), 0f);

        float driftAngle = Random.Range(0f, Mathf.PI * 2f);
        _driftVelocity = new Vector3(Mathf.Cos(driftAngle), Mathf.Sin(driftAngle), 0f) * driftSpeed;

        if (_co != null)
            StopCoroutine(_co);
        _co = StartCoroutine(FloatRoutine());
    }

    private static string FormatDamage(float damage)
    {
        if (damage <= 0f) return "0";
        if (damage < 1f) return damage.ToString("0.##");
        if (Mathf.Abs(damage - Mathf.Round(damage)) < 0.05f)
            return "-" + Mathf.RoundToInt(damage).ToString();
        return "-" + damage.ToString("0.#");
    }

    private static string FormatCritDamage(float damage)
    {
        return  FormatDamage(damage) + "暴击!";
    }

    private IEnumerator FloatRoutine()
    {
        float t = 0f;
        Transform textTr = label.transform;
        Vector3 baseScale = Vector3.one;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float u = t / lifetime;

            transform.position += (_floatDirection * riseSpeed + _driftVelocity) * Time.deltaTime;

            if (_cam != null)
                transform.rotation = Quaternion.LookRotation(_cam.position - transform.position, Vector3.up);

            float scaleMul = Mathf.Lerp(spawnScalePunch, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f)));
            textTr.localScale = baseScale * scaleMul;

            Color c = _isCrit ? critColor : textColor;
            c.a = (_isCrit ? critColor.a : textColor.a) * (1f - Mathf.SmoothStep(0f, 1f, u));
            label.color = c;

            yield return null;
        }

        _co = null;
        GameObjectPool.Release(gameObject);
    }
}
