using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using xls;
using System.Reflection;
using XUPorterJSON;
using Debug = UnityEngine.Debug;

using UnityEditor;
using System.Linq;

[CustomEditor(typeof(CExtendButton), false)]
public class ButtonBaseInspector : Editor
{
    private SerializedProperty cooldown;

    private void OnEnable()
    {
        cooldown = serializedObject.FindProperty("cooldown");
    }
}

[System.Serializable]
public class Xls2FBWindow : EditorWindow {
    public static Xls2FBWindow editorWindow;
    private FileSystemWatcher fileWatcher = new FileSystemWatcher ();

    private readonly string PREFIX = "Xls2FBWindow_";

    private static List<string> showResult = new List<string>();
    private Vector2 m_pResulteVec = new Vector2();

    Xls2FBWindow()
    {
        fileWatcher.Path = "../GongkaoMower/Share/table/xls";
        fileWatcher.Filter = "*.xls";
        fileWatcher.Changed += new FileSystemEventHandler (OnProcess);
        fileWatcher.Created += new FileSystemEventHandler (OnProcess);
        fileWatcher.Deleted += new FileSystemEventHandler (OnProcess);
        fileWatcher.Renamed += new RenamedEventHandler (OnProcess);
        fileWatcher.EnableRaisingEvents = true;
    }

    private void OnProcess (object source, FileSystemEventArgs e) {

        if (e.ChangeType == WatcherChangeTypes.Created) {
            // OnCreated(source, e);
        } else if (e.ChangeType == WatcherChangeTypes.Changed) {
            // 文件修改
            for (int i = 0; i < m_pFileList.Count; i++) {
                var info = m_pFileList[i];
                if (e.FullPath == info.xls) {
                    info.modify = true;
                    return;
                }
            }

        } else if (e.ChangeType == WatcherChangeTypes.Deleted) {
            // OnDeleted(source, e);

        }

    }


    [MenuItem ("[TM工具集]/xls转cs")]
    public static void GenerateCSAndDataMd5 () {
        ConvertXls (true, true, true);
    }

    /****
     * ignoremd5 无视md5校验
     * 生成cs
     * 生成数据
     ****/
    private static void ConvertXls (bool ignoremd5, bool cs, bool data)
    {
        string dir = System.Environment.CurrentDirectory;
        List<string> filelist = FindFile ("../GongkaoMower/Share/table/xls/");
        int i = 0;
        foreach (var filename in filelist) {
            Debug.Log(string.Format("开始转表 {0}", filename));
            if (filename.EndsWith (".xlsx")) {
                Debug.LogError(string.Format ("不支持 xlsx {0}", filename));
                continue;
            }
            if(!Convert (filename, ignoremd5, cs, data)){
                Debug.LogError(string.Format("转表失败 {0}", filename));
                return;
            }
            i++;

            EditorUtility.DisplayProgressBar("FB转表", "Converting .. " +i+"/"+filelist.Count , (i) / (float)filelist.Count);
        }
        Debug.Log(string.Format ("xls 转换完成"));
        EditorUtility.ClearProgressBar();
    }

    [MenuItem("转表工具/xls转txt")]
    public static void OpenWindow()
    {
        Xls2FBWindow.editorWindow = EditorWindow.GetWindow<Xls2FBWindow>(false, "FB转表", true);
        Xls2FBWindow.editorWindow.Show();
        Xls2FBWindow.editorWindow.m_pFileList = ChangeXls();
        Xls2FBWindow.editorWindow.LoadConfig();
    }

    public static List<string> FindFile (string sSourcePath) {
        List<string> list = new List<string> ();

        //遍历文件夹

        DirectoryInfo theFolder = new DirectoryInfo (sSourcePath);

        FileInfo[] thefileInfo = theFolder.GetFiles("*.xls", SearchOption.TopDirectoryOnly)
            ?.Where(x => x.Name.EndsWith("c.xls") || x.Name.EndsWith("cs.xls")).ToArray();
        foreach (FileInfo NextFile in thefileInfo) //遍历文件
        {
            var name = NextFile.FullName;
            name = name.Replace('\\', '/');

            if (UseSplitTable() && !m_ShowSplitTable)
            {
                if (XlsxDataUnit.NeedIgnore(name))
                {
                    continue;
                }
            }

            list.Add(name);
        }

        return list;
    }

    public class XlsFileInfo {
        public string xls;
        public string md5;
        public bool toggle;
        public bool modify;

    };

    public List<XlsFileInfo> m_pFileList = ChangeXls ();

    public bool m_bIsTextOnly = false;

    public bool m_bIsWaitForCompile = true;

    public bool m_onlyGenData = true;

    public bool m_genServerCode = false;

    public static bool m_ShowSplitTable = false;   //是否显示分表表格

    private Vector2 m_pSelectedVec = new Vector2 ();

    private bool mBuildProto = false;
    private StringBuilder mCountBuilder = new StringBuilder(2000);
    //StringBuilderCache.Acquire (2000);

    private string mFilter = "";

    private bool mShowDirtyFlag = false;

    public Result m_eResulte = Result.Waitting;

    public enum Result {
        Waitting = 0,
        Running,
        Select,
        Finish,
    };

    private static List<XlsFileInfo> ChangeXls()
    {
        List<string> files = FindFile("../GongkaoMower/Share/table/xls");
        List<XlsFileInfo> result = new List<XlsFileInfo>();
        for (int index = 0; index < files.Count; ++index)
        {
            string filename = files[index];

            if (filename.EndsWith(".xlsx"))
            {
                continue;
            }

            try
            {
                using (FileStream xls = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    string xlsFileMd5 = filename + ".MD5";
                    string oldmd5 = File.Exists(xlsFileMd5) ? System.IO.File.ReadAllText(xlsFileMd5) : "";
                    string newmd5 = GetMd5Hash(xls);

                    XlsFileInfo info = new XlsFileInfo();
                    info.xls = filename;
                    info.md5 = newmd5;
                    info.toggle = false;
                    info.modify = oldmd5 != newmd5;
                    result.Add(info);
                    xls.Close();
                }
            }
            catch (Exception e)
            {
            }
        }

        return result;
    }

    public static bool UseSplitTable()
    {
        return true;//Global.Settings.useSplitTable;
    }

    public static bool DoConvertAFile(string filename)
    {
        var list = ChangeXls();
        foreach (var item in list)
        {
            if (item.xls.Contains(filename))
            {
                return Convert(item.xls, true, true, true);
            }
        }

        return false;
    }

     public static List<NPOI.SS.UserModel.ISheet> MergeTableArray(string name, NPOI.SS.UserModel.ISheet targetSheet)
    {
        List<NPOI.SS.UserModel.ISheet> sheetList = new List<NPOI.SS.UserModel.ISheet>();
        sheetList.Add(targetSheet);
        var tokens = name.Split('/');
        var fileName = tokens[tokens.Length - 1];


        var splitXls = XlsxDataUnit.splitXls;


        for (int i = 0; i < splitXls.Length; ++i)
            if (splitXls[i].mainName.Contains(fileName))
            {
                for (int j = 0; j < splitXls[i].splitFileNames.Length; ++j)
                {
                    var itemSplit = name.Replace(splitXls[i].mainName, splitXls[i].splitFileNames[j]);

                    NPOI.SS.UserModel.ISheet ipas = null;


                    if (!File.Exists(itemSplit))
                        continue;
                    
                    
                    using (FileStream xls = new FileStream(itemSplit, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        
                        if (xls != null)
                        {
                            NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook(xls);
                            ipas = book.GetSheetAt(0);
                        }
                        xls.Close();
                    }

                    if (ipas != null)
                    {
                          sheetList.Add(ipas);
                       // MergeShell(ipas as NPOI.HSSF.UserModel.HSSFSheet, targetSheet as NPOI.HSSF.UserModel.HSSFSheet, originBook, j+1);
                    }
                }

                break;
            }

        return sheetList;
    }

    public static NPOI.SS.UserModel.ISheet MergeTables(string name, NPOI.SS.UserModel.ISheet targetSheet)
    {
        var tokens = name.Split('/');
        var fileName = tokens[tokens.Length - 1];


        var splitXls = XlsxDataUnit.splitXls;


        for (int i = 0; i < splitXls.Length; ++i)
            if (splitXls[i].mainName.Contains(fileName))
            {
                for (int j = 0; j < splitXls[i].splitFileNames.Length; ++j)
                {
                    var itemSplit = name.Replace(splitXls[i].mainName, splitXls[i].splitFileNames[j]);

                    NPOI.SS.UserModel.ISheet ipas = null;


                    if (!File.Exists(itemSplit))
                        continue;

                    using (FileStream xls = new FileStream(itemSplit, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (xls != null)
                        {
                            NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook(xls);
                            ipas = book.GetSheetAt(0);
                        }
                        xls.Close();
                    }

                    if (ipas != null)
                    {
                        targetSheet = MergeShell(targetSheet as NPOI.HSSF.UserModel.HSSFSheet, ipas as NPOI.HSSF.UserModel.HSSFSheet);
                    }
                }

                break;
            }


        return targetSheet;
    }

    public static bool Convert(string filename, bool ignore, bool cs, bool data, bool servercode = false)
    {
        using (FileStream xls = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {

            string xlsFileMd5 = filename + ".MD5";
            string oldmd5 = ignore ? "" : (File.Exists(xlsFileMd5) ? File.ReadAllText(xlsFileMd5) : "");
            string newmd5 = GetMd5Hash(xls);

            // md5码一样  就不转
            if (oldmd5 == newmd5)
            {
                Debug.Log(string.Format("xls {0} 未发生改变", filename));
                return true;
            }

            List<NPOI.SS.UserModel.ISheet> sheetList = null;
            try
            {
                NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook(xls);
                if (book == null)
                {
                    EditorUtility.DisplayDialog("【表不存在!】", filename, "确定", "");
                    return false;
                }

                NPOI.SS.UserModel.ISheet sheet = book.GetSheetAt(0);
                if (sheet == null)
                {
                    return false;
                }

                ////////////////////////////////////////////////////////////

                //新版转表代码(可以大幅提高转表速度)
                Stopwatch watch = new Stopwatch();
                watch.Start();
                
                if (UseSplitTable())
                {
                    sheetList = MergeTableArray(filename, sheet);
                    //Logger.LogErrorFormat("MergeTableArray  {0}", sheetList.Count);
                }

                Table table = new Table();
           
                table.ParserFrom(sheetList, filename);

                watch.Stop();

                if (cs && !fb.GenerateDesc(table))
                {
                    EditorUtility.DisplayDialog("【生成cs错误!】", filename, "确定", "");
                    return false;
                }

                if (!cs)
                {
                    if (!fb.CheckCs(table))
                    {
                        EditorUtility.DisplayDialog("[表格字段可能和cs不匹配了]", filename, "确定", "");
                        return false;
                    }
                }

                if (data && !fb.DumpData(table))
                {
                    EditorUtility.DisplayDialog("【生成数据错误!】", filename, "确定", "");
                    return false;
                }

                showResult.Add(string.Format("xls {0} 转换完成\n", filename));

                book.Close();
                xls.Close();

            }
            catch (Exception)
            {
                Debug.LogError($"转表报错 {sheetList} {filename}");
                throw;
            }
        }

        // XlsxDataUnit 走旧 Proto 表头协议，与 FlatBuffers 表（第 0 行 required/repeated）不兼容；无拆表配置时不应解析，否则会误报 GenerateKeyValueDic 空列。
        if (UseSplitTable() && XlsxDataUnit.splitXls != null && XlsxDataUnit.splitXls.Length > 0)
        {
            XlsxDataUnit unit = new XlsxDataUnit(filename);
            XlsxDataUnit.MergeTables(filename, unit);
        }

        return true;
    }


    public static NPOI.SS.UserModel.ISheet MergeShell(NPOI.HSSF.UserModel.HSSFSheet origin, NPOI.HSSF.UserModel.HSSFSheet merge)
    {
        NPOI.HSSF.UserModel.HSSFWorkbook product = new NPOI.HSSF.UserModel.HSSFWorkbook();

        if (null == origin || null == merge)
        {
            return null;
        }

        origin.CopyTo(product, origin.SheetName, true, true);
        merge.CopyTo(product, "1", true, true);

        var froms = product.GetSheetAt(1);
        var tos = product.GetSheetAt(0);

        int lastRow = tos.LastRowNum;

        for (int i = 5; i <= froms.LastRowNum; ++i)
        {
            NPOI.SS.Util.SheetUtil.CopyRow(froms, i, tos, lastRow + i - 5+1);
        }

        return tos;
    }

    public static string GetMd5Hash(FileStream fs)
    {
        MD5 md5Hash = MD5.Create();
        byte[] data = null;

        data = md5Hash.ComputeHash (fs);

        fs.Position = 0;

        StringBuilder sBuilder = new StringBuilder ();

        for (int i = 0; i < data.Length; i++) {
            sBuilder.Append (data[i].ToString ("x2"));
        }

        return sBuilder.ToString ().ToLower ();
    }

    private void SaveConfig()
    {
        EditorPrefs.SetInt(PREFIX + "Count", m_pFileList.Count);

        EditorPrefs.SetBool(PREFIX + "IsTextOnly", m_onlyGenData);

        EditorPrefs.SetBool(PREFIX + "IsWaitForCompile", m_bIsWaitForCompile);

        for (int i = 0; i < m_pFileList.Count; i++)
        {
            string filepath = m_pFileList[i].xls;
            //EditorPrefs.SetString(PREFIX + string.Format("PATH_{0}", i), filepath);

            bool bToggle = m_pFileList[i].toggle;
            EditorPrefs.SetBool(PREFIX + string.Format("TOGGLE_{0}", i), bToggle);

            string sMD5 = m_pFileList[i].md5;
            EditorPrefs.SetString(PREFIX + string.Format("MD5_{0}", i), sMD5);

            //string sProtoMD5 = m_pProtoMD5[i];
            //EditorPrefs.SetString(PREFIX + string.Format("PROTO_MD5_{0}", i), sProtoMD5);                   
        }
    }

    private void LoadConfig()
    {
        if (m_pFileList == null)
            m_pFileList = ChangeXls();

        int iCount = EditorPrefs.GetInt(PREFIX + "Count");
        //m_pFileList = new string[iCount];

        m_bIsTextOnly = EditorPrefs.GetBool(PREFIX + "IsTextOnly");

        m_bIsWaitForCompile = EditorPrefs.GetBool(PREFIX + "IsWaitForCompile");

        for (int i = 0; i < iCount; i++)
        {
            //m_pFileList[i].xls = EditorPrefs.GetString(PREFIX + string.Format("PATH_{0}", i));
            m_pFileList[i].toggle = EditorPrefs.GetBool(PREFIX + string.Format("TOGGLE_{0}", i));
            m_pFileList[i].md5 = EditorPrefs.GetString(PREFIX + string.Format("MD5_{0}", i));
            //m_pProtoMD5[i] = EditorPrefs.GetString(PREFIX + string.Format("PROTO_MD5_{0}", i));
        }
    }

    public void OnGUI()
    {
        if (EditorApplication.isCompiling)
        {
            EditorGUILayout.HelpBox(string.Format("正在编译中\n"), MessageType.Warning);
            return;
        }

        if (EditorApplication.isPlaying) {
            EditorGUILayout.HelpBox (string.Format ("游戏正在运行\n"), MessageType.Warning);
            return;
        }

        if (m_bIsWaitForCompile && mBuildProto && EditorApplication.isCompiling) {
            mCountBuilder.Append ("..");
            EditorGUILayout.HelpBox (string.Format ("正在编译中{0}\n如果本页面木有刷新，随便点击点击点击", mCountBuilder.ToString ()),
                MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        {
            EditorGUILayout.BeginVertical();
            {
                bool genData = EditorGUILayout.Toggle("只转数据表", m_onlyGenData);
                if (genData != m_onlyGenData)
                    m_onlyGenData = genData;

                bool genServerCode = EditorGUILayout.Toggle("生成服务器代码", m_genServerCode);
                if (genServerCode != m_genServerCode)
                    m_genServerCode = genServerCode;

                bool vvalue = EditorGUILayout.Toggle("等待编译", m_bIsWaitForCompile);
                if (vvalue != m_bIsWaitForCompile)
                {
                    m_bIsWaitForCompile = vvalue;
                }

                bool value = EditorGUILayout.Toggle("显示分表", m_ShowSplitTable);
                if(m_ShowSplitTable!= value)
                {
                    m_ShowSplitTable = value;
                    m_pFileList = ChangeXls();
                    m_eResulte = Result.Select;
                }

                if (mFilter == null)
                    mFilter = "";
                var str = EditorGUILayout.TextField("筛选", mFilter);
                if (str != mFilter)
                {
                    mFilter = str;
                }
            }
            EditorGUILayout.EndVertical ();

            EditorGUILayout.Space ();

            m_pSelectedVec = EditorGUILayout.BeginScrollView(m_pSelectedVec, GUILayout.Height(500));
            {
                EditorGUILayout.BeginVertical("ObjectFieldThumb");
                {
                    if (m_pFileList == null)
                        m_pFileList = ChangeXls();
                    for (int i = 0; i < m_pFileList.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUI.indentLevel++;

                            XlsFileInfo info = m_pFileList[i];
                            string filename = System.IO.Path.GetFileName (info.xls);

                            if (i % 2 == 0) {
                                GUI.color = Color.yellow;
                            }
                            //                             if (info.modify) {
                            //                                 GUI.color = Color.red;
                            //                             }

                            if ((mFilter.Length <= 0 || filename.ToLower().StartsWith(mFilter.ToLower()) || info.toggle))
                            {
                                if (XlsxDataUnit.NeedIgnore(filename))
                                {
                                    EditorGUILayout.LabelField(string.Format("[分表]{0}", filename), GUILayout.Width(200));
                                    EditorGUILayout.LabelField("", GUILayout.Width(50));
                                    if (m_TableMapData != null && m_TableMapData.Count > 0)
                                    {
                                        EditorGUILayout.LabelField("", GUILayout.Width(150));
                                    }
                                }
                                else
                                {
                                    EditorGUILayout.LabelField(filename, GUILayout.Width(200));

                                    bool value = EditorGUILayout.Toggle("", info.toggle, GUILayout.Width(50));
                                    if (value != info.toggle)
                                    {
                                        info.toggle = value;
                                    }

                                    if (m_TableMapData != null && m_TableMapData.Count > 0)
                                    {
                                        if (m_TableMapData.ContainsKey(info.md5))
                                        {
                                            EditorGUILayout.LabelField(m_TableMapData[info.md5].ToString(), GUILayout.Width(150));
                                        }
                                        else
                                        {
                                            EditorGUILayout.LabelField("", GUILayout.Width(150));
                                        }
                                    }
                                }

                                //#if UNITY_STANDALONE_WIN
                                if (StyledButton("开!"))
                                {

                                    ProcessStartInfo processInfo = new ProcessStartInfo();
                                    processInfo.FileName = info.xls;
                                    processInfo.Arguments = "";

                                    Process process = new Process();
                                    process.StartInfo = processInfo;
                                    process.Start();

                                }
                            }

                            //#endif
                            GUI.color = Color.white;

                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndHorizontal ();
                    }
                }
                EditorGUILayout.EndVertical ();
            }
            EditorGUILayout.EndScrollView ();

            EditorGUILayout.BeginHorizontal ("ObjectFieldThumb"); {
                if (StyledButton ("刷新")) {
                    m_pFileList = ChangeXls ();
                    m_eResulte = Result.Select;

                    SaveConfig();
                }

                if (StyledButton("生成映射表"))
                {
                    GerenateMapData();
                }

                if (StyledButton ("全选")) {
                    for (int i = 0; i < m_pFileList.Count; i++) {
                        var info = m_pFileList[i];
                        info.toggle = true;
                    }

                    m_eResulte = Result.Waitting;

                    SaveConfig();
                }

                if (StyledButton ("反选")) {
                    for (int i = 0; i < m_pFileList.Count; i++) {
                        var info = m_pFileList[i];
                        info.toggle = !info.toggle;
                    }

                    m_eResulte = Result.Waitting;
                    SaveConfig();
                }

                if (StyledButton ("清空")) {
                    for (int i = 0; i < m_pFileList.Count; i++) {
                        var info = m_pFileList[i];
                        info.toggle = false;
                    }

                    m_eResulte = Result.Waitting;
                    SaveConfig();
                }

                if (StyledButton ("转表")) {
                    mBuildProto = true;
                    showResult.Clear();

                    for (int i = 0; i < m_pFileList.Count; i++) {
                        var info = m_pFileList[i];
                        if (info.toggle && Convert(info.xls, true, !m_onlyGenData, true, m_genServerCode))
                        {
                            info.modify = false;
                        } 
                    }

                    SaveConfig();

                    m_eResulte = Result.Finish;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal("ObjectFieldThumb");
            {
                m_pResulteVec = EditorGUILayout.BeginScrollView(m_pResulteVec, GUILayout.Height(180));
                {
                    switch (m_eResulte)
                    {
                        case Result.Waitting:
                            EditorGUILayout.LabelField("");
                            break;
                        case Result.Select:
                            EditorGUILayout.LabelField("修改过的表格:");
                            EditorGUILayout.LabelField(showResult.ToString());
                            break;
                        case Result.Finish:
                            EditorGUILayout.LabelField(string.Format("转表完成 {0}：", System.DateTime.Now.ToLongTimeString().ToString()));

                            for(int i=0; i<showResult.Count; ++i)
                                EditorGUILayout.LabelField(showResult[i]);

                            break;
                        default:
                            break;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndHorizontal();

        }

        if (EditorGUI.EndChangeCheck ()) {

        }
    }

    public static bool StyledButton (string label) {
        EditorGUILayout.Space ();
        GUILayoutUtility.GetRect (1, 20);
        EditorGUILayout.BeginHorizontal ();
        GUILayout.FlexibleSpace ();
        bool clickResult = GUILayout.Button (label, "miniButton");
        GUILayout.FlexibleSpace ();
        EditorGUILayout.EndHorizontal ();
        EditorGUILayout.Space ();
        return clickResult;
    }

    [MenuItem("转表工具/数据检查")]
    public static void CheckData()
    {
        Type[] data = TableManager.Instance.GetAllTypeListInEditorMode();
        for (int i = 0; i < data.Length; i++)
        {
            var curType = data[i];
            string filepath = TableManager._getTablePathNew(curType);
            Dictionary<int, object> table = ParseTable(curType, filepath);
            try
            {
                foreach (KeyValuePair<int, object> keyValue in table)
                {
                    int id = keyValue.Key;
                    object obj = keyValue.Value;
                    PropertyInfo[] propertyInfos = curType.GetProperties();
                    for (int pi = 0; pi < propertyInfos.Length; ++pi)
                    {
                        var method = propertyInfos[pi].GetGetMethod();
                        object value = method.Invoke(obj, null);
                    }
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                break;
            }
            EditorUtility.DisplayProgressBar("共:" + data.Length + "个", "Checking ..当前第 " + i + "个", (i + 1) / (float)data.Length);
        }
        EditorUtility.ClearProgressBar();
    }

    private static Dictionary<int, object> ParseTable(Type type, string filename)
    {
        try
        {
            byte[] data = File.ReadAllBytes("Assets/Resources/" + filename);
            Dictionary<int, object> table = new Dictionary<int, object>();
            FlatBuffers.Table ftable = new FlatBuffers.Table();
            FlatBuffers.ByteBuffer buffer = new FlatBuffers.ByteBuffer(data);

            ftable.bb_pos = 0;
            ftable.bb = buffer;

            int length = ftable.__vector_len(0);

            for (int index = 0; index < length; ++index)
            {
                ;
                int offset = ftable.__vector(index);
                var fobj = Activator.CreateInstance(type);

                MethodInfo __assign = type.GetMethod("__assign");
                var IDMap = type.GetProperty("ID").GetGetMethod();

                BindingFlags flag = BindingFlags.Public | BindingFlags.Instance;
                //GetValue方法的参数
                object[] parameters = new object[] { ftable.__indirect(ftable.__vector(0) + index * 4), ftable.bb };
                __assign.Invoke(fobj, flag, Type.DefaultBinder, parameters, null);

                int id = (int)IDMap.Invoke(fobj, null);

                if (!table.ContainsKey(id))
                {
                    table.Add(id, fobj);
                }
                else
                {
                }
            }
            return table;
        }
        catch (Exception e)
        {
            return null;
        }
    }
    
    private string m_MapPath = "../client/Share/table/xls/map.json";
    private Hashtable m_TableMapData = new Hashtable();

    /// <summary>
    /// 初始化映射表表数据
    /// </summary>
    protected void InitMapData()
    {
        string path = GetTableRootPath() + m_MapPath;
        string data = LoadFile(path);
        if (string.IsNullOrEmpty(data))
        {
            return;
        }
        Hashtable jsonData = MiniJSON.jsonDecode(data) as Hashtable;
        if (jsonData == null)
        {
            return;
        }
        if (data != null)
        {
            m_TableMapData = jsonData;
        }
    }

    /// <summary>
    /// 生成映射表数据
    /// </summary>
    protected void GerenateMapData()
    {
        ChangeXls();
        if (m_pFileList == null || m_pFileList.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < m_pFileList.Count; i++)
        {
            var xlsInfo = m_pFileList[i];
            if (m_TableMapData != null && !m_TableMapData.ContainsKey(xlsInfo.md5))
            {
                using (FileStream xls = new FileStream(xlsInfo.xls, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook(xls);
                    if (book == null)
                    {
                        break;
                    }

                    NPOI.SS.UserModel.ISheet sheet = book.GetSheetAt(0);
                    if (sheet == null)
                    {
                        break;
                    }
                    m_TableMapData.Add(xlsInfo.md5, sheet.SheetName);
                }
            }
        }
        string path = GetTableRootPath() + m_MapPath;
        string data = MiniJSON.jsonEncode(m_TableMapData);
        SaveData(path, data);
    }

    /// <summary>
    /// 获取映射表根路径
    /// </summary>
    protected string GetTableRootPath()
    {
        string dataPath = Application.dataPath;
        string[] pathArr = dataPath.Split('/');
        string path = "";
        for(int i=0; i< pathArr.Length - 2; i++)
        {
            path += string.Format("{0}/", pathArr[i]);
        }
        return path;
    }

    /// <summary>
    /// 获取数据
    /// </summary>
    protected string LoadFile(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            return content;
        }
        catch (Exception exception)
        {
        }
        return null;
    }

    /// <summary>
    /// 保存数据
    /// </summary>
    protected void SaveData(string path,string data)
    {
        try
        {
            File.WriteAllText(path, data);
        }
        catch (Exception exception)
        {
        }
    }
}