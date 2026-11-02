using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 文字怪展示：在子物体上挂 <see cref="TextMeshPro"/> 或 <see cref="TextMeshProUGUI"/>。
/// 支持随机旋转、字号，以及描边 + 顶点渐变（需 Distance Field 类 TMP 材质，工程默认 LiberationSans SDF 即可）。
/// </summary>
[DisallowMultipleComponent]
public class EnemyWordLabel : MonoBehaviour
{
    // Opt in when diagnosing individual spawns; avoid synchronous logs per enemy.
    public static bool LogVerbose = false;

    [Tooltip("世界空间 TMP；为空则在 Awake 时子物体中查找")]
    [SerializeField] private TextMeshPro worldText;

    [Tooltip("UI TMP；为空则在 Awake 时子物体中查找")]
    [SerializeField] private TextMeshProUGUI uiText;

    [Header("表现（随机）")]
    [SerializeField] private bool randomizeZRotation = true;

    [SerializeField] private Vector2 rotationZDegreesRange = new Vector2(-28f, 28f);

    [SerializeField] private bool randomizeFontSize = true;

    [SerializeField] private Vector2 fontSizeMultiplierRange = new Vector2(0.82f, 1.28f);

    [SerializeField] private float fontSizeAbsoluteMax = 96f;

    [SerializeField] private float fontSizeAbsoluteMin = 8f;

    [Header("立体字（描边 + 渐变）")]
    [Tooltip("关闭则不改色、不描边、不用顶点渐变")]
    [SerializeField] private bool enableStereoTextStyle = true;
    [Tooltip("使用与明快地图匹配的共享 SDF 描边材质；不新增字体或图集。")]
    [SerializeField] private bool useCartoonTextTheme = true;

    [Tooltip("为 true 时复制一份 fontMaterial，避免改描边影响同材质其它字")]
    [SerializeField] private bool instanceMaterialPerEnemy = true;

    [Tooltip("字面底色基准；偏亮偏暖，暗地图上也清晰可见")]
    [SerializeField] private Color stereoFaceBase = new Color32(255,245,221,255);

    [Tooltip("为 true 且本波已设置 WordMonsterWaveStyle 时，整波共用一波底色，仅做下方「微色相」抖动")]
    [SerializeField] private bool preferWaveSharedTint = true;

    [Tooltip("整波底色下，每只怪在 H 通道 ± 随机的小范围，避免完全克隆又保持同色系")]
    [SerializeField] private float perEnemyMicroHueJitter = 0.02f;

    [Tooltip("无整波底色时：每次 SetWord 在 H 通道上 ± 随机（幅度较大）")]
    [SerializeField] private bool randomizeStereoHue = true;

    [SerializeField] private float stereoHueShift = 0.07f;

    [Tooltip("描边宽度（略粗以增加暗底对比度）")]
    [SerializeField] private Vector2 outlineWidthRange = new Vector2(0.20f, 0.40f);

    [Tooltip("描边颜色，略深、不透明更显立体")]
    [SerializeField] private Color outlineColor = new Color(0.04f, 0.02f, 0.14f, 0.94f);

    [Tooltip("四角顶点渐变：上亮下暗，模拟受光面")]
    [SerializeField] private bool stereoUseVertexGradient = true;

    [Range(0f, 1f)]
    [SerializeField] private float gradientTopBrighten = 0.25f;

    [Range(0f, 1f)]
    [SerializeField] private float gradientBottomDarken = 0.12f;

    [Header("受击反馈")]
    [SerializeField] private bool enableHitFeedback = true;

    [Tooltip("与 EnemyAI 一致，用于击退方向（远离玩家）")]
    [SerializeField] private string playerTagForKnockback = "Player";

    [SerializeField] private float hitFlashDuration = 0.1f;

    [SerializeField] private float hitPunchScale = 1.08f;

    [SerializeField] private float hitPunchDuration = 0.12f;

    [Tooltip("TMP 子物体沿击退方向的最大位移（本地 XY，单位与场景一致）")]
    [SerializeField] private float hitKnockDistance = 0.11f;

    [SerializeField] private float hitKnockRecoverDuration = 0.14f;

    private float _baselineWorldFont = -1f;
    private float _baselineUiFont = -1f;
    private bool _materialInstancedWorld;
    private bool _materialInstancedUi;

    private Vector3 _strikeBaseLocalPosW;
    private Vector3 _strikeBaseLocalScaleW = Vector3.one;
    private Color32 _strikeBaseFaceW = Color.white;

    private Vector3 _strikeBaseLocalPosU;
    private Vector3 _strikeBaseLocalScaleU = Vector3.one;
    private Color32 _strikeBaseFaceU = Color.white;
    private Color _strikeBaseTintW, _strikeBaseTintU;
    private VertexGradient _strikeGradientW, _strikeGradientU;

    private Coroutine _strikeRoutine;
    private EnemyBase _enemyBase;
    public int HitAnchorVersion { get; private set; }

    private void Awake()
    {
        ResolveTargets();
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.TryApplyTo(this);
        CaptureBaselines();
        CaptureStrikeBaselines();
    }

    private void OnEnable()
    {
        HitAnchorVersion++;
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.TryApplyTo(this);

        if (!enableHitFeedback)
            return;
        _enemyBase = GetComponentInParent<EnemyBase>();
        if (_enemyBase != null)
            _enemyBase.OnDamaged.AddListener(OnDamagedHitFeedback);
    }

    private void OnDisable()
    {
        HitAnchorVersion++;
        if (_enemyBase != null)
            _enemyBase.OnDamaged.RemoveListener(OnDamagedHitFeedback);
        _enemyBase = null;
        StopHitFeedbackAndReset();
    }

    private void OnDamagedHitFeedback(float amount)
    {
        if (!enableHitFeedback || amount <= 0f)
            return;
        PlayHitFeedback();
    }

    /// <summary> 受击：短闪白 + TMP 缩放 punch + 字块轻击退（仅视觉，不影响刚体） </summary>
    public void PlayHitFeedback()
    {
        if (!enableHitFeedback)
            return;
        ResolveTargets();
        if (worldText == null && uiText == null)
            return;

        if (_strikeRoutine != null)
        {
            StopCoroutine(_strikeRoutine);
            _strikeRoutine = null;
            RestoreStrikeVisuals();
        }
        CaptureStrikeBaselines();
        _strikeRoutine = StartCoroutine(HitFeedbackRoutine());
    }

    private IEnumerator HitFeedbackRoutine()
    {
        Vector2 knockDir = ComputeKnockDir2D();
        float total = Mathf.Max(hitFlashDuration, hitPunchDuration, hitKnockRecoverDuration);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float flashT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hitFlashDuration));
            float punchT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hitPunchDuration));
            float knockT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hitKnockRecoverDuration));

            Color white = Color.white;
            float punchHump = Mathf.Sin(punchT * Mathf.PI);
            float scaleMul = Mathf.Lerp(1f, hitPunchScale, punchHump);
            float knockMag = hitKnockDistance * (1f - Mathf.SmoothStep(0f, 1f, knockT));
            Vector3 knockLocal = new Vector3(knockDir.x, knockDir.y, 0f) * knockMag;

            if (worldText != null)
            {
                if(useCartoonTextTheme) ApplyVertexFlash(worldText,_strikeBaseTintW,_strikeGradientW,flashT);
                else worldText.faceColor = (Color32)Color.Lerp(white, _strikeBaseFaceW, flashT);
                Transform tw = worldText.transform;
                tw.localScale = _strikeBaseLocalScaleW * scaleMul;
                tw.localPosition = _strikeBaseLocalPosW + knockLocal;
            }

            if (uiText != null)
            {
                if(useCartoonTextTheme) ApplyVertexFlash(uiText,_strikeBaseTintU,_strikeGradientU,flashT);
                else uiText.faceColor = (Color32)Color.Lerp(white, _strikeBaseFaceU, flashT);
                Transform tu = uiText.transform;
                tu.localScale = _strikeBaseLocalScaleU * scaleMul;
                tu.localPosition = _strikeBaseLocalPosU + knockLocal;
            }

            yield return null;
        }

        RestoreStrikeVisuals();
        _strikeRoutine = null;
    }

    private Vector2 ComputeKnockDir2D()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTagForKnockback);
        if (p == null)
        {
            Vector2 r = Random.insideUnitCircle;
            return r.sqrMagnitude > 0.0001f ? r.normalized : Vector2.right;
        }

        Vector2 d = (Vector2)transform.position - (Vector2)p.transform.position;
        if (d.sqrMagnitude < 0.0001f)
            return Vector2.right;
        return d.normalized;
    }

    private void CaptureStrikeBaselines()
    {
        if (worldText != null)
        {
            Transform tw = worldText.transform;
            _strikeBaseLocalPosW = tw.localPosition;
            _strikeBaseLocalScaleW = tw.localScale;
            _strikeBaseFaceW = worldText.faceColor;
            _strikeBaseTintW=worldText.color;_strikeGradientW=worldText.colorGradient;
        }

        if (uiText != null)
        {
            Transform tu = uiText.transform;
            _strikeBaseLocalPosU = tu.localPosition;
            _strikeBaseLocalScaleU = tu.localScale;
            _strikeBaseFaceU = uiText.faceColor;
            _strikeBaseTintU=uiText.color;_strikeGradientU=uiText.colorGradient;
        }
    }

    private void RestoreStrikeVisuals()
    {
        if (worldText != null)
        {
            if(useCartoonTextTheme){worldText.color=_strikeBaseTintW;worldText.colorGradient=_strikeGradientW;}
            else worldText.faceColor = _strikeBaseFaceW;
            Transform tw = worldText.transform;
            tw.localPosition = _strikeBaseLocalPosW;
            tw.localScale = _strikeBaseLocalScaleW;
        }

        if (uiText != null)
        {
            if(useCartoonTextTheme){uiText.color=_strikeBaseTintU;uiText.colorGradient=_strikeGradientU;}
            else uiText.faceColor = _strikeBaseFaceU;
            Transform tu = uiText.transform;
            tu.localPosition = _strikeBaseLocalPosU;
            tu.localScale = _strikeBaseLocalScaleU;
        }
    }

    private void StopHitFeedbackAndReset()
    {
        if (_strikeRoutine != null)
        {
            StopCoroutine(_strikeRoutine);
            _strikeRoutine = null;
        }
        RestoreStrikeVisuals();
    }

    /// <summary>Snapshot the stable glyph geometry / colors rather than the lethal hit's temporary punch and flash.</summary>
    public void PrepareForReviveSnapshot()
    {
        StopHitFeedbackAndReset();
        if(worldText!=null)worldText.ForceMeshUpdate();
    }

    private static void ApplyVertexFlash(TMP_Text tmp,Color tint,VertexGradient gradient,float amount)
    {
        // Use vertex colors for per-enemy feedback; never mutate the shared font material.
        if(tmp.enableVertexGradient)
        {
            tmp.color=tint;
            tmp.colorGradient=new VertexGradient(Color.Lerp(Color.white,gradient.topLeft,amount),Color.Lerp(Color.white,gradient.topRight,amount),Color.Lerp(Color.white,gradient.bottomLeft,amount),Color.Lerp(Color.white,gradient.bottomRight,amount));
        }
        else tmp.color=Color.Lerp(Color.white,tint,amount);
    }

    private void ResolveTargets()
    {
        if (worldText == null)
            worldText = GetComponentInChildren<TextMeshPro>(true);
        if (uiText == null)
            uiText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>局内将 TMP 切换为中文字体（prefab 占位为 LiberationSans，避免首包序列化依赖 msyh）。</summary>
    public void ApplyBattleChineseFont(TMP_FontAsset font)
    {
        if (font == null)
            return;

        ResolveTargets();
        if (worldText != null)
        {
            worldText.font = font;
            worldText.fontSharedMaterial = useCartoonTextTheme?BattleTextTheme.MaterialFor(font,BattleTextTheme.Role.Word):font.material;
            worldText.SetVerticesDirty();
            worldText.SetLayoutDirty();
        }

        if (uiText != null)
        {
            uiText.font = font;
            uiText.fontSharedMaterial = useCartoonTextTheme?BattleTextTheme.MaterialFor(font,BattleTextTheme.Role.Word):font.material;
            uiText.SetVerticesDirty();
            uiText.SetLayoutDirty();
        }
    }

    private void CaptureBaselines()
    {
        if (worldText != null && _baselineWorldFont < 0f)
            _baselineWorldFont = worldText.fontSize;
        if (uiText != null && _baselineUiFont < 0f)
            _baselineUiFont = uiText.fontSize;
    }

    public void SetWord(string text)
    {
        ResolveTargets();
        // 重新尝试加载 + 应用中文字体（data-package 子包可能此时才加载完成）
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.TryApplyTo(this);
        CaptureBaselines();
        string s = text ?? string.Empty;

        if (LogVerbose)
            GameLog.Info($"{MonsterWordSpawnBinding.LogTag} SetWord on={gameObject.name} worldText={(worldText != null ? worldText.gameObject.name : "null")} uiText={(uiText != null ? uiText.gameObject.name : "null")} len={s.Length}");

        if (worldText == null && uiText == null)
        {
            Debug.LogWarning($"{MonsterWordSpawnBinding.LogTag} SetWord: 未找到 TextMeshPro / TextMeshProUGUI 子组件，请在 prefab 子级添加 TMP 或 Inspector 指定。path={BuildPath()}");
            return;
        }

        if (worldText != null)
            worldText.text = s;
        if (uiText != null)
            uiText.text = s;

        ApplyVisualVariation();
        CaptureStrikeBaselines();
    }

    /// <summary>供死亡碎字：解析世界空间 TMP；无字或仅 UI 字则 false。</summary>
    public bool TryGetWorldTextForShatter(out TextMeshPro tmp)
    {
        ResolveTargets();
        tmp = worldText;
        return tmp != null && !string.IsNullOrEmpty(tmp.text);
    }

    /// <summary>Presentation contact on a visible character, independent of the enemy's physics shape.</summary>
    public bool TryGetHitAnchor(Vector2 source, out Transform anchor, out Vector3 localPoint)
    {
        anchor = null;
        localPoint = Vector3.zero;
        var text = HitAnchorText();
        if (text == null) return false;
        Vector3 localSource = text.transform.InverseTransformPoint(source);
        float nearest = float.PositiveInfinity;
        var info = text.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;
            Vector3 lo = character.bottomLeft, hi = character.topRight;
            if (hi.x <= lo.x || hi.y <= lo.y) continue;
            var edge = new Vector3(Mathf.Clamp(localSource.x, lo.x, hi.x), Mathf.Clamp(localSource.y, lo.y, hi.y), 0);
            // Place the small bright core just inside the glyph instead of outside its collider.
            var point = Vector3.Lerp(edge, (lo + hi) * .5f, .18f);
            float distance = ((Vector2)text.transform.TransformPoint(point) - source).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            localPoint = point;
            anchor = text.transform;
        }
        return anchor != null;
    }

    public bool TryGetVisualCenter(out Vector2 point)
    {
        point = transform.position;
        var text = HitAnchorText();
        if (text == null) return false;
        var info = text.textInfo;
        Bounds bounds = new Bounds();
        bool found = false;
        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;
            if (!found) { bounds = new Bounds(character.bottomLeft, Vector3.zero); found = true; }
            bounds.Encapsulate(character.bottomLeft);
            bounds.Encapsulate(character.topRight);
        }
        if (found) point = text.transform.TransformPoint(bounds.center);
        return found;
    }

    private TMP_Text HitAnchorText()
    {
        ResolveTargets();
        TMP_Text text = worldText != null && worldText.isActiveAndEnabled ? (TMP_Text)worldText : uiText;
        if (text == null || !text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text)) return null;
        if (text.havePropertiesChanged || text.textInfo.characterCount == 0) text.ForceMeshUpdate();
        return text;
    }

    private void ApplyVisualVariation()
    {
        if (worldText != null)
            ApplyToTmp(worldText.transform, worldText, _baselineWorldFont, ref _materialInstancedWorld);

        if (uiText != null)
            ApplyToTmp(uiText.transform, uiText, _baselineUiFont, ref _materialInstancedUi);
    }

    private void ApplyToTmp(Transform tr, TMP_Text tmp, float baseline, ref bool materialInstanced)
    {
        if (!useCartoonTextTheme && instanceMaterialPerEnemy && tmp.fontSharedMaterial != null && !materialInstanced)
        {
            tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
            materialInstanced = true;
        }

        if (randomizeZRotation)
        {
            float z = Random.Range(rotationZDegreesRange.x, rotationZDegreesRange.y);
            tr.localRotation = Quaternion.Euler(0f, 0f, z);
        }

        if (randomizeFontSize && baseline > 0f)
        {
            float mul = Random.Range(fontSizeMultiplierRange.x, fontSizeMultiplierRange.y);
            float size = Mathf.Clamp(baseline * mul, fontSizeAbsoluteMin, fontSizeAbsoluteMax);
            tmp.fontSize = size;
        }

        if (enableStereoTextStyle)
            ApplyStereoLook(tmp);
    }

    private void ApplyStereoLook(TMP_Text tmp)
    {
        Color face;
        bool useWave = preferWaveSharedTint && WordMonsterWaveStyle.HasWaveTint;

        if (useWave)
        {
            face = WordMonsterWaveStyle.WaveFaceTint;
            if (perEnemyMicroHueJitter > 0f)
            {
                Color.RGBToHSV(face, out float h, out float s, out float v);
                h = Mathf.Repeat(h + Random.Range(-perEnemyMicroHueJitter, perEnemyMicroHueJitter), 1f);
                face = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
                face.a = WordMonsterWaveStyle.WaveFaceTint.a;
            }
        }
        else
        {
            face = stereoFaceBase;
            if (randomizeStereoHue)
            {
                Color.RGBToHSV(face, out float h, out float s, out float v);
                h = Mathf.Repeat(h + Random.Range(-stereoHueShift, stereoHueShift), 1f);
                face = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
                face.a = stereoFaceBase.a;
            }
        }

        if(useCartoonTextTheme) BattleTextTheme.Apply(tmp,BattleTextTheme.Role.Word);
        else
        {
            float ow = Random.Range(outlineWidthRange.x, outlineWidthRange.y);
            tmp.outlineWidth = ow;
            tmp.outlineColor = outlineColor;
        }

        if (stereoUseVertexGradient)
        {
            tmp.enableVertexGradient = true;
            tmp.color = Color.white;

            Color top = Color.Lerp(face, Color.white, gradientTopBrighten);
            Color bottom = Color.Lerp(face, Color.black, gradientBottomDarken);
            top.a = face.a;
            bottom.a = face.a;

            tmp.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }
        else
        {
            tmp.enableVertexGradient = false;
            tmp.color = face;
        }

        tmp.ForceMeshUpdate(true);
    }

    private string BuildPath()
    {
        var t = transform;
        var stack = new System.Collections.Generic.List<string>();
        int guard = 0;
        while (t != null && guard++ < 24)
        {
            stack.Add(t.name);
            t = t.parent;
        }
        stack.Reverse();
        return string.Join("/", stack);
    }
}
