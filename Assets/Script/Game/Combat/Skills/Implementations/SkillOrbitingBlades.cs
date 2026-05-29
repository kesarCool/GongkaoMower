using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 环绕刀片：轨道根公转 + Prefab 池化刀片（表现见 <see cref="OrbitingBladeVisual"/>）
/// </summary>
public class SkillOrbitingBlades : SkillBase
{
    public int bladeCount = 3;
    public float orbitRadius = 1.2f;
    public float rotateSpeedDeg = 180f;
    public float damagePerTick = 1f;
    public float tickInterval = 0.15f;

    private readonly GameObject _bladePrefab;
    private readonly Sprite _bladeSprite;
    private readonly int _spriteSortingOrder;
    private readonly Color _spriteTint;
    private readonly float _visualScale;

    private Transform _orbitRoot;
    private float _angle;
    private readonly List<GameObject> _activeBlades = new List<GameObject>(8);

    public SkillOrbitingBlades(
        GameObject bladePrefab,
        Sprite bladeSprite,
        int bladeCount,
        float orbitRadius,
        float rotateSpeedDeg,
        float damagePerTick,
        float tickInterval,
        int spriteSortingOrder,
        Color spriteTint,
        float visualScale)
    {
        Id = SkillId.OrbitingBlades;
        _bladePrefab = bladePrefab;
        _bladeSprite = bladeSprite;
        _spriteSortingOrder = spriteSortingOrder;
        _spriteTint = spriteTint;
        _visualScale = Mathf.Max(0.01f, visualScale);
        this.bladeCount = Mathf.Max(1, bladeCount);
        this.orbitRadius = Mathf.Max(0.1f, orbitRadius);
        this.rotateSpeedDeg = rotateSpeedDeg;
        this.damagePerTick = Mathf.Max(0.01f, damagePerTick);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
    }

    public override void OnEquip(SkillContext ctx)
    {
        base.OnEquip(ctx);
        EnsureOrbitVisuals();
        AudioService.Ensure().PlayLoop(AudioId.SkillOrbitingBlades);
    }

    public override void OnUnequip()
    {
        AudioService.Ensure().StopLoop(AudioId.SkillOrbitingBlades);
        base.OnUnequip();
        ClearBlades();
        if (_orbitRoot != null)
        {
            Object.Destroy(_orbitRoot.gameObject);
            _orbitRoot = null;
        }
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;

        if (_orbitRoot == null) EnsureOrbitVisuals();

        _orbitRoot.position = _ctx.player.position;
        _angle += rotateSpeedDeg * deltaTime;
        _orbitRoot.rotation = Quaternion.Euler(0f, 0f, _angle);
    }

    private void EnsureOrbitVisuals()
    {
        if (_orbitRoot != null) return;
        if (_ctx.player == null) return;

        GameObject root = new GameObject("OrbitingBlades");
        root.transform.SetParent(_ctx.player, false);
        root.transform.localPosition = Vector3.zero;
        _orbitRoot = root.transform;

        RebuildBlades();
    }

    private void RebuildBlades()
    {
        if (_orbitRoot == null) return;

        ClearBlades();

        float step = 360f / bladeCount;
        float bladeSpin = Mathf.Max(120f, rotateSpeedDeg * 2f);

        for (int i = 0; i < bladeCount; i++)
        {
            float deg = i * step;
            Vector3 local = Quaternion.Euler(0f, 0f, deg) * (Vector3.right * orbitRadius);
            Quaternion localRot = Quaternion.Euler(0f, 0f, deg);

            GameObject blade = SpawnBlade(local, localRot);
            if (blade == null) continue;

            blade.transform.SetParent(_orbitRoot, false);
            blade.transform.localPosition = local;
            blade.transform.localRotation = localRot;

            ConfigureBlade(blade, bladeSpin);
            _activeBlades.Add(blade);
        }
    }

    private GameObject SpawnBlade(Vector3 localPos, Quaternion localRot)
    {
        if (_bladePrefab != null)
        {
            return GameObjectPool.Get(
                _bladePrefab,
                _orbitRoot.TransformPoint(localPos),
                _orbitRoot.rotation * localRot,
                _orbitRoot);
        }

        return SpawnLegacyBlade(localPos, localRot);
    }

    private GameObject SpawnLegacyBlade(Vector3 localPos, Quaternion localRot)
    {
        GameObject blade = new GameObject("Blade_Legacy");
        blade.transform.SetParent(_orbitRoot, false);
        blade.transform.localPosition = localPos;
        blade.transform.localRotation = localRot;
        blade.transform.localScale = Vector3.one * _visualScale;

        var sr = blade.AddComponent<SpriteRenderer>();
        sr.sprite = _bladeSprite != null ? _bladeSprite : RuntimeSprites.GetUiPlaceholderSprite();
        sr.color = _spriteTint;
        sr.sortingOrder = _spriteSortingOrder;

        var col = blade.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.35f, 0.8f);

        var rb = blade.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        blade.AddComponent<SkillOrbBladeHit>();
        return blade;
    }

    private void ConfigureBlade(GameObject blade, float bladeSpinDeg)
    {
        SkillOrbBladeHit hit = blade.GetComponent<SkillOrbBladeHit>();
        if (hit == null) hit = blade.AddComponent<SkillOrbBladeHit>();
        hit.damagePerTick = damagePerTick;
        hit.tickInterval = tickInterval;
        hit.damageSourceSkillId = SkillId.OrbitingBlades;

        var ps = GetPlayerSkills();
        if (ps != null) hit.SetPlayerSkills(ps);

        OrbitingBladeVisual visual = blade.GetComponent<OrbitingBladeVisual>();
        if (visual != null)
        {
            Sprite sprite = _bladeSprite;
            if (sprite == null)
            {
                SpriteRenderer sr = blade.GetComponent<SpriteRenderer>();
                if (sr != null) sprite = sr.sprite;
            }

            visual.Apply(sprite, _spriteTint, _spriteSortingOrder, _visualScale);
            visual.SetSpinSpeed(bladeSpinDeg);
        }
        else
        {
            blade.transform.localScale = Vector3.one * _visualScale;
            SpriteRenderer sr = blade.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (_bladeSprite != null) sr.sprite = _bladeSprite;
                sr.color = _spriteTint;
                sr.sortingOrder = _spriteSortingOrder;
            }
        }
    }

    private void ClearBlades()
    {
        for (int i = _activeBlades.Count - 1; i >= 0; i--)
        {
            GameObject blade = _activeBlades[i];
            _activeBlades.RemoveAt(i);
            if (blade == null) continue;

            if (_bladePrefab != null)
                GameObjectPool.Release(blade);
            else
                Object.Destroy(blade);
        }

        if (_orbitRoot == null) return;
        for (int i = _orbitRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = _orbitRoot.GetChild(i).gameObject;
            if (_bladePrefab != null)
                GameObjectPool.Release(child);
            else
                Object.Destroy(child);
        }
    }

    public void ApplyRuntimeStats(int bladeCount, float orbitRadius, float rotateSpeedDeg, float damagePerTick, float tickInterval)
    {
        this.bladeCount = Mathf.Max(1, bladeCount);
        this.orbitRadius = Mathf.Max(0.1f, orbitRadius);
        this.rotateSpeedDeg = rotateSpeedDeg;
        this.damagePerTick = Mathf.Max(0.01f, damagePerTick);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
        RebuildBlades();
    }
}
