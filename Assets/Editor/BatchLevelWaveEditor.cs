using System.Collections.Generic;
using System.Text;
using FlatBuffers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LevelWave 表批量编辑。菜单：Window → 表格工具 → LevelWave 批量编辑
/// 编辑模式下直接读 .bytes，无需 Play Mode。
/// </summary>
public class BatchLevelWaveEditor : EditorWindow
{
    private string _levelIdInput = "101";
    private float _atkMul = 1.2f;
    private float _hpMul = 1.2f;
    private float _spdMul = 1.0f;
    private float _expMul = 1.0f;
    private bool _showPreview;

    private Vector2 _scroll;
    private List<Row> _rows = new List<Row>();

    // ── 完整行 ──
    private struct Row
    {
        public int id, levelId, wave, waveTimeContinue, monsterId;
        public int attack, maxHp; public float speed;
        public int exp, prop, timeStart; public float intervalSpawn;
        public int totalMonster, lineSpawn;
        public bool isBoss; public int quantityBoss; public bool iscirculate;

        public static float mA, mH, mS, mE;
        public int NA => Mathf.RoundToInt(attack * mA);
        public int NH => Mathf.RoundToInt(maxHp * mH);
        public float NS => speed * mS;
        public int NE => Mathf.RoundToInt(exp * mE);
    }

    private static readonly string[] HEADER =
    {
        "ID","levelId","wave","waveTimeContinue","monsterId",
        "attack","maxHp","speed","exp","prop",
        "timeStart","intervalSpawn","totalMonster","lineSpawn",
        "iscirculate","isBoss","quantityBoss"
    };

    [MenuItem("Window/表格工具/LevelWave 批量编辑")]
    public static void Open() => GetWindow<BatchLevelWaveEditor>("LevelWave 批量编辑");

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("LevelWave 批量属性调整", EditorStyles.boldLabel);

        // ── 关卡选择 ──
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("关卡 LevelId", GUILayout.Width(80));
        _levelIdInput = EditorGUILayout.TextField(_levelIdInput, GUILayout.Width(80));
        if (GUILayout.Button("加载", GUILayout.Width(60))) Load(int.TryParse(_levelIdInput, out int lid) ? lid : 0);
        GUILayout.EndHorizontal();

        // ── 倍率 ──
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("倍率", GUILayout.Width(80));
        _atkMul = EditorGUILayout.FloatField(_atkMul, GUILayout.Width(60));
        EditorGUILayout.LabelField("攻击×", GUILayout.Width(42));
        _hpMul = EditorGUILayout.FloatField(_hpMul, GUILayout.Width(60));
        EditorGUILayout.LabelField("血量×", GUILayout.Width(42));
        _spdMul = EditorGUILayout.FloatField(_spdMul, GUILayout.Width(60));
        EditorGUILayout.LabelField("速度×", GUILayout.Width(42));
        _expMul = EditorGUILayout.FloatField(_expMul, GUILayout.Width(60));
        EditorGUILayout.LabelField("经验×", GUILayout.Width(42));
        GUILayout.EndHorizontal();

        // ── 操作 ──
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("操作", GUILayout.Width(80));
        if (GUILayout.Button(_showPreview ? "隐藏预览" : "显示预览", GUILayout.Width(80))) _showPreview = !_showPreview;
        if (GUILayout.Button("重置倍率", GUILayout.Width(80))) { _atkMul = _hpMul = _spdMul = _expMul = 1f; _showPreview = true; }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("复制 Tab（粘贴到 Excel）", GUILayout.Width(180))) ExportTab();
        if (GUILayout.Button("复制 CSV", GUILayout.Width(80))) ExportCsv();
        GUILayout.EndHorizontal();

        if (_rows.Count == 0) { EditorGUILayout.HelpBox("输入关卡 LevelId → 点「加载」", MessageType.Info); return; }

        EditorGUILayout.LabelField($"共 {_rows.Count} 行  黄色=倍率修改过的值  导Tab可直接粘贴Excel", EditorStyles.miniLabel);

        Row.mA = _atkMul; Row.mH = _hpMul; Row.mS = _spdMul; Row.mE = _expMul;

        // ── 表格 ──
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // 表头
        EditorGUILayout.BeginHorizontal();
        H("ID", 32); H("Lv", 24); H("W", 20); H("WCont", 40); H("Mon", 30);
        H("Atk", 60); H("HP", 60); H("Spd", 40); H("Exp", 32); H("Prop", 32);
        H("tStart", 40); H("Interv", 42); H("Tot", 30); H("Line", 30);
        H("Cir", 22); H("Boss", 32); H("Qty", 28);
        EditorGUILayout.EndHorizontal();

        foreach (var r in _rows)
        {
            EditorGUILayout.BeginHorizontal();
            L(r.id.ToString(), 32);
            L(r.levelId.ToString(), 24);
            L(r.wave.ToString(), 20);
            L(r.waveTimeContinue.ToString(), 40);
            L(r.monsterId.ToString(), 30);

            C(_showPreview && r.attack != r.NA, _showPreview ? $"{r.attack}>{r.NA}" : r.attack.ToString(), 60);
            C(_showPreview && r.maxHp != r.NH, _showPreview ? $"{r.maxHp}>{r.NH}" : r.maxHp.ToString(), 60);
            C(_showPreview && !Mathf.Approximately(r.speed, r.NS), _showPreview ? $"{r.speed:F1}>{r.NS:F1}" : r.speed.ToString("F1"), 40);
            C(_showPreview && r.exp != r.NE, _showPreview ? $"{r.exp}>{r.NE}" : r.exp.ToString(), 32);

            L(r.prop.ToString(), 32); L(r.timeStart.ToString(), 40);
            L(r.intervalSpawn.ToString("F1"), 42); L(r.totalMonster.ToString(), 30);
            L(r.lineSpawn.ToString(), 30); L(r.iscirculate ? "1" : "", 22);
            L(r.isBoss ? "1" : "", 32); L(r.quantityBoss.ToString(), 28);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ── 辅助 ──
    static void H(string t, float w) => EditorGUILayout.LabelField(t, EditorStyles.miniLabel, GUILayout.Width(w));
    static void L(string t, float w) => EditorGUILayout.LabelField(t, GUILayout.Width(w));
    static void C(bool changed, string t, float w)
    {
        var old = GUI.color;
        if (changed) GUI.color = Color.yellow;
        EditorGUILayout.LabelField(t, GUILayout.Width(w));
        GUI.color = old;
    }

    // ── 加载 ──

    void Load(int levelId)
    {
        _rows.Clear(); _showPreview = false;
        if (levelId <= 0) { Debug.LogWarning("[LvWaveEd] 无效的 LevelId。"); return; }

        var asset = Resources.Load<TextAsset>("Data/table_fb/LevelWave");
        if (asset == null)
        {
            Debug.LogError("[LvWaveEd] 未找到 Resources/Data/table_fb/LevelWave.bytes，请先跑导表。");
            EditorUtility.DisplayDialog("加载失败", "未找到 LevelWave.bytes\n请先运行导表工具生成 .bytes 文件", "确定");
            return;
        }

        var tb = new Table();
        var bb = new ByteBuffer(asset.bytes);
        tb.bb_pos = 0; tb.bb = bb;

        int n = tb.__vector_len(0);
        int v = tb.__vector(0);

        for (int i = 0; i < n; i++)
        {
            int off = tb.__indirect(v + i * 4);
            var lw = new ProtoTable.LevelWave();
            lw.__init(off, tb.bb);
            if (lw.levelId != levelId) continue;

            _rows.Add(new Row
            {
                id = lw.ID, levelId = lw.levelId, wave = lw.wave,
                waveTimeContinue = lw.waveTimeContinue, monsterId = lw.monsterId,
                attack = lw.attack, maxHp = lw.maxHp, speed = lw.speed,
                exp = lw.exp, prop = lw.prop, timeStart = lw.timeStart,
                intervalSpawn = lw.intervalSpawn, totalMonster = lw.totalMonster,
                lineSpawn = lw.lineSpawn, iscirculate = lw.iscirculate,
                isBoss = lw.isBoss, quantityBoss = lw.quantityBoss,
            });
        }

        _rows.Sort((a, b) => a.wave.CompareTo(b.wave));
        Debug.Log($"[LvWaveEd] 关卡 {levelId} 共 {_rows.Count} 行。");
        if (_rows.Count == 0) EditorUtility.DisplayDialog("无数据", $"LevelWave 表中未找到 levelId={levelId} 的行。", "确定");
    }

    // ── 导出 ──

    void ExportTab() => DoExport("\t", "Tab 分隔");
    void ExportCsv() => DoExport(",", "CSV");

    void DoExport(string sep, string label)
    {
        if (_rows.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, HEADER));
        foreach (var r in _rows)
        {
            sb.Append(string.Join(sep, new string[] {
                r.id.ToString(), r.levelId.ToString(), r.wave.ToString(),
                r.waveTimeContinue.ToString(), r.monsterId.ToString(),
                (_showPreview ? r.NA : r.attack).ToString(),
                (_showPreview ? r.NH : r.maxHp).ToString(),
                (_showPreview ? r.NS : r.speed).ToString("F1"),
                (_showPreview ? r.NE : r.exp).ToString(),
                r.prop.ToString(), r.timeStart.ToString(),
                r.intervalSpawn.ToString("F1"),
                r.totalMonster.ToString(), r.lineSpawn.ToString(),
                r.iscirculate ? "1":"0", r.isBoss ? "1":"0", r.quantityBoss.ToString(),
            }));
            sb.AppendLine();
        }
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"[LvWaveEd] {label} 已复制 {_rows.Count} 行。");
        EditorUtility.DisplayDialog("导出完成", $"{label} 格式已复制到剪贴板\n{_rows.Count} 行 → 可直接粘贴到 Excel", "确定");
    }
}
