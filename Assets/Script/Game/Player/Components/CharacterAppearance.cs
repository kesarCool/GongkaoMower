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
    private CharacterAimFrames _aimFrames;
    private SkillId _primarySkill;
    private int _aimSector;
    private int _shotFrame = -1;

    public bool HasDirectionalAimFrames => bodyRenderer != null && _aimFrames != null && _aimFrames.IsComplete;
    public int AimSector => _aimSector;

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
        EventBus.Subscribe<PlayerWeaponFiredVisualEvent>(OnWeaponFired, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SkillCastEvent>(OnSkillCast);
        EventBus.Unsubscribe<PlayerWeaponFiredVisualEvent>(OnWeaponFired);
    }

    private void LateUpdate()
    {
        MirrorFlipX();
    }

    // ── Skin ────────────────────────────────────────────

    public void ApplySkin(CharacterDefinition def)
    {
        if (bodyRenderer == null) return;
        _aimFrames = def != null ? def.aimFrames : null;
        _primarySkill = def != null ? def.startingSkill : SkillId.None;
        if (def != null && def.defaultWeapon != null && def.defaultWeapon.weaponSkillId != SkillId.None)
            _primarySkill = def.defaultWeapon.weaponSkillId;
        _shotFrame = -1;
        _aimSector = 0;
        if (HasDirectionalAimFrames)
        {
            bodyRenderer.sprite = _aimFrames.right;
            bodyRenderer.flipX = false;
        }
        else if (def != null && def.bodySprite != null)
            bodyRenderer.sprite = def.bodySprite;
    }

    /// <summary>由 PlayerController 传入已有索敌结果或真实位移，不增加索敌查询。</summary>
    public bool TrySetDirectionalAim(Vector2 direction, bool snap = false)
    {
        if (!HasDirectionalAimFrames) return false;
        if (direction.sqrMagnitude < 0.000001f || Time.timeScale <= 0f) return true;
        // 当帧实际发射优先，LateUpdate 不再用另一份目标方向覆盖它。
        if (!snap && _shotFrame == Time.frameCount) return true;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int sector = (Mathf.RoundToInt(angle / 45f) % 8 + 8) % 8;
        if (!snap && sector != _aimSector && Mathf.Abs(Mathf.DeltaAngle(_aimSector * 45f, angle)) <= 22.5f + _aimFrames.directionHysteresis)
            return true;
        _aimSector = sector;
        bool mirrored;
        bodyRenderer.sprite = _aimFrames.GetFrame(sector, out mirrored);
        bodyRenderer.flipX = mirrored;
        return true;
    }

    private void OnWeaponFired(PlayerWeaponFiredVisualEvent e)
    {
        if (!HasDirectionalAimFrames || e.owner != transform || e.skillId != _primarySkill || e.projectileCount <= 0 || Time.timeScale <= 0f)
            return;
        TrySetDirectionalAim(e.aimDirection, true);
        _shotFrame = Time.frameCount;
    }

    // ── Weapon ──────────────────────────────────────────

    public void ApplyWeapon(WeaponDefinition def)
    {
        // 先卸旧武器
        if (_weaponChild != null)
            RemoveWeapon();

        // 整身姿态中已经画有枪和双手，避免再叠一把独立武器。
        if (HasDirectionalAimFrames) return;
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
