using System.Collections;
using UnityEngine;

/// <summary>
/// 管理玩家角色外观：Body 皮肤 + Weapon 子节点渲染 + 攻击摆动。
/// Body 上的 PlayerHitFeedback 不受影响（仅闪红/震击 Body）。
/// </summary>
[DisallowMultipleComponent]
public class CharacterAppearance : MonoBehaviour
{
    [Header("Body 引用")]
    [Tooltip("Body 子物体的 SpriteRenderer。留空则自动查找孩子名为 Body 的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    // Weapon 子节点
    private GameObject _weaponChild;
    private SpriteRenderer _weaponRenderer;
    private WeaponDefinition _currentWeaponDef;
    private Vector3 _weaponRestPos;
    private Quaternion _weaponRestRot;
    private bool _swingRunning;

    private void Awake()
    {
        if (bodyRenderer == null)
        {
            Transform body = transform.Find("Body");
            if (body != null)
                bodyRenderer = body.GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<SkillCastEvent>(OnSkillCast, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SkillCastEvent>(OnSkillCast);
    }

    private void LateUpdate()
    {
        MirrorFlipX();
    }

    // ── Skin ────────────────────────────────────────────

    public void ApplySkin(CharacterDefinition def)
    {
        if (bodyRenderer == null) return;
        if (def == null || def.bodySprite == null) return;

        bodyRenderer.sprite = def.bodySprite;
    }

    // ── Weapon ──────────────────────────────────────────

    public void ApplyWeapon(WeaponDefinition def)
    {
        // 先卸旧武器
        if (_weaponChild != null)
            RemoveWeapon();

        if (def == null || def.sprite == null) return;

        _currentWeaponDef = def;
        CreateWeaponChild(def);
    }

    public void RemoveWeapon()
    {
        if (_weaponChild != null)
        {
            Destroy(_weaponChild);
            _weaponChild = null;
            _weaponRenderer = null;
        }
        _currentWeaponDef = null;
    }

    private void CreateWeaponChild(WeaponDefinition def)
    {
        _weaponChild = new GameObject("Weapon");
        _weaponChild.transform.SetParent(transform, false);
        _weaponChild.transform.localPosition = def.localPosition;
        _weaponChild.transform.localEulerAngles = def.localRotation;
        _weaponChild.transform.localScale = def.localScale;

        _weaponRenderer = _weaponChild.AddComponent<SpriteRenderer>();
        _weaponRenderer.sprite = def.sprite;
        _weaponRenderer.sortingOrder = (bodyRenderer != null ? bodyRenderer.sortingOrder : 0) + def.sortingOrderOffset;

        _weaponRestPos = _weaponChild.transform.localPosition;
        _weaponRestRot = _weaponChild.transform.localRotation;
    }

    // ── Attack swing ────────────────────────────────────

    private void OnSkillCast(SkillCastEvent e)
    {
        // 仅自动弹射类技能触发武器摆动
        if (e.skillId != SkillId.AutoProjectile) return;
        if (_currentWeaponDef == null || _weaponChild == null) return;

        PlayAttackSwing(_currentWeaponDef);
    }

    private void PlayAttackSwing(WeaponDefinition def)
    {
        if (_swingRunning)
            return;

        StartCoroutine(AttackSwingRoutine(def));
    }

    private IEnumerator AttackSwingRoutine(WeaponDefinition def)
    {
        _swingRunning = true;

        Vector3 restPos = _weaponRestPos;
        Quaternion restRot = _weaponRestRot;
        Transform wt = _weaponChild.transform;

        // 摆出
        float t = 0f;
        float dur = Mathf.Max(0.02f, def.attackSwingDuration);
        Vector3 bobTarget = restPos + def.attackBobOffset;
        Quaternion rotTarget = restRot * Quaternion.Euler(0f, 0f, def.attackSwingAngle);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            wt.localPosition = Vector3.Lerp(restPos, bobTarget, u);
            wt.localRotation = Quaternion.Slerp(restRot, rotTarget, u);
            yield return null;
        }

        // 回弹
        float recover = Mathf.Max(0.02f, def.attackRecoverDuration);
        Vector3 fromPos = wt.localPosition;
        Quaternion fromRot = wt.localRotation;
        t = 0f;
        while (t < recover)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / recover);
            wt.localPosition = Vector3.Lerp(fromPos, restPos, u);
            wt.localRotation = Quaternion.Slerp(fromRot, restRot, u);
            yield return null;
        }

        // 精确归位
        wt.localPosition = restPos;
        wt.localRotation = restRot;

        _swingRunning = false;
    }

    // ── FlipX mirror ────────────────────────────────────

    private void MirrorFlipX()
    {
        if (_weaponChild == null || bodyRenderer == null) return;

        Vector3 ls = _weaponChild.transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = bodyRenderer.flipX ? -absX : absX;
        _weaponChild.transform.localScale = ls;
    }
}
