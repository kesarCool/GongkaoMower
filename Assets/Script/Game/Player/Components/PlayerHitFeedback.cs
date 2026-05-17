using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家受击视觉（方案 B）：挂在 <c>Body</c> 上，闪白 + 缩放 Punch + 沿远离伤害源方向轻击退（仅 Transform，不改刚体）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHitFeedback : MonoBehaviour
{
    [SerializeField] private bool enableHitFeedback = true;

    [Tooltip("留空则用本物体 SpriteRenderer")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [SerializeField] private PlayerHealth playerHealth;

    [Header("反馈参数")]
    [SerializeField] private float hitFlashDuration = 0.1f;
    [SerializeField] private float hitPunchScale = 1.08f;
    [SerializeField] private float hitPunchDuration = 0.12f;
    [SerializeField] private float hitKnockDistance = 0.11f;
    [SerializeField] private float hitKnockRecoverDuration = 0.14f;

    [Tooltip("无伤害源时，用该 Tag 找最近敌人作为击退方向")]
    [SerializeField] private string enemyTag = "monster";

    private Vector3 _baseLocalPos;
    private Vector3 _baseLocalScale = Vector3.one;
    private Color _baseColor = Color.white;
    private Coroutine _routine;
    private Vector2 _lastKnockDir = Vector2.right;

    private void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        CaptureBaselines();
    }

    private void OnEnable()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.OnDamaged.AddListener(OnDamaged);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged.RemoveListener(OnDamaged);
        StopAndReset();
    }

    private void OnDamaged(float amount, Transform damageSource)
    {
        if (!enableHitFeedback || amount <= 0f)
            return;

        _lastKnockDir = ComputeKnockDir2D(damageSource);
        PlayHitFeedback();
    }

    public void PlayHitFeedback()
    {
        if (!enableHitFeedback)
            return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            RestoreVisuals();
        }

        CaptureBaselines();
        _routine = StartCoroutine(HitFeedbackRoutine());
    }

    private IEnumerator HitFeedbackRoutine()
    {
        float total = Mathf.Max(hitFlashDuration, hitPunchDuration, hitKnockRecoverDuration);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float flashT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hitFlashDuration));
            float punchT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hitPunchDuration));
            float knockT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hitKnockRecoverDuration));

            float punchHump = Mathf.Sin(punchT * Mathf.PI);
            float scaleMul = Mathf.Lerp(1f, hitPunchScale, punchHump);
            float knockMag = hitKnockDistance * (1f - Mathf.SmoothStep(0f, 1f, knockT));
            Vector3 knockLocal = new Vector3(_lastKnockDir.x, _lastKnockDir.y, 0f) * knockMag;

            transform.localScale = _baseLocalScale * scaleMul;
            transform.localPosition = _baseLocalPos + knockLocal;

            if (bodyRenderer != null)
                bodyRenderer.color = Color.Lerp(Color.red, _baseColor, flashT);

            yield return null;
        }

        RestoreVisuals();
        _routine = null;
    }

    private Vector2 ComputeKnockDir2D(Transform damageSource)
    {
        if (damageSource != null)
        {
            Vector2 d = (Vector2)transform.position - (Vector2)damageSource.position;
            if (d.sqrMagnitude > 0.0001f)
                return d.normalized;
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float bestSq = float.MaxValue;
        Vector2 bestDir = Vector2.right;
        Vector2 self = transform.position;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            Vector2 d = self - (Vector2)enemies[i].transform.position;
            float sq = d.sqrMagnitude;
            if (sq < bestSq && sq > 0.0001f)
            {
                bestSq = sq;
                bestDir = d.normalized;
            }
        }

        if (bestSq < float.MaxValue)
            return bestDir;

        Vector2 rnd = Random.insideUnitCircle;
        return rnd.sqrMagnitude > 0.0001f ? rnd.normalized : Vector2.right;
    }

    private void CaptureBaselines()
    {
        _baseLocalPos = transform.localPosition;
        _baseLocalScale = transform.localScale;
        if (bodyRenderer != null)
            _baseColor = bodyRenderer.color;
    }

    private void RestoreVisuals()
    {
        transform.localPosition = _baseLocalPos;
        transform.localScale = _baseLocalScale;
        if (bodyRenderer != null)
            bodyRenderer.color = _baseColor;
    }

    private void StopAndReset()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        RestoreVisuals();
    }
}
