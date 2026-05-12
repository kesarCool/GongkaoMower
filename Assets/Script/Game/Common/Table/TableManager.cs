using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System;
using ProtoTable;

public class TableManager : MonoSingleton<TableManager>
{
    private const string kTablePath = "Data/table_fb/";

    private bool bInit = false;
    static public bool bNeedUninit = false;

    private Type[] mTypeList =
    {
        typeof(LexiconTable),
        typeof(LevelWave),
        typeof(Monster),
        typeof(ChapterLevel),
    };

    private Dictionary<Type, Dictionary<int, object>> mTypeTableDict = new Dictionary<Type, Dictionary<int, object>>();

    
    public Dictionary<int, object> AddTableInEditorMode(Type type)
    {
        if (mTypeTableDict.ContainsKey(type))
        {
            return mTypeTableDict[type];
        }

        Dictionary<int, object> tableData = _loadTable(type);
        mTypeTableDict.Add(type, tableData);
        return tableData;
    }

    public Type[] GetAllTypeListInEditorMode()
    {
        return mTypeList;
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        return path.Replace('\\', '/');
    }

    public static string Combine(string path1, string path2)
    {
        string combinedPath = System.IO.Path.Combine(path1, path2);
        return Normalize(combinedPath);
    }

    public static string _getTablePathNew(Type type)
    {
        return string.Format("{0}.bytes", Combine(kTablePath, type.Name));
    }

    /// <summary>
    /// 游戏表格初始化（异步）
    /// </summary>
    public void Init()
    {
        if (bInit)
        {
            return;
        }

        bInit = true;

        //加载游戏表格
        //---------------------------------------------------------------------
        for (int i = 0; i < mTypeList.Length; i++)      
        {
            var curType = mTypeList[i];
            Dictionary<int, object> tableData = _loadTable(curType);
            mTypeTableDict.Add(mTypeList[i], tableData);    
        }

        Debug.Log($"TableManager Init success, type count: {mTypeTableDict.Count}");
    }

    private Dictionary<int, object> _loadTable(Type type)
    {
        Dictionary<int, object> table = new Dictionary<int, object>();
        string filepath = _getTablePath(type);

        do
        {
            byte[] data = null;

            TextAsset textAssetFB = Resources.Load<TextAsset>(filepath);
            if (textAssetFB != null)
                data = textAssetFB.bytes;
            else
            {
                string altBytes = Path.Combine(Application.dataPath, "Res", "Data", "table_fb", type.Name + ".bytes");
                if (File.Exists(altBytes))
                    data = File.ReadAllBytes(altBytes);
            }

            if (data == null || data.Length == 0)
                return table;

            _fillTableFromFlatBufferBytes(table, type, data);

            return table;
        } while (false);
    }

    private static void _fillTableFromFlatBufferBytes(Dictionary<int, object> table, Type type, byte[] data)
    {
        FlatBuffers.Table ftable = new FlatBuffers.Table();
        FlatBuffers.ByteBuffer buffer = new FlatBuffers.ByteBuffer(data);

        ftable.bb_pos = 0;
        ftable.bb = buffer;

        int length = ftable.__vector_len(0);

        for (int index = 0; index < length; ++index)
        {
            int offset = ftable.__vector(index);
            var fobj = Activator.CreateInstance(type);
            MethodInfo __assign = type.GetMethod("__assign");
            var IDMap = type.GetProperty("ID").GetGetMethod();

            BindingFlags flag = BindingFlags.Public | BindingFlags.Instance;
            object[] parameters = new object[] { ftable.__indirect(ftable.__vector(0) + index * 4), ftable.bb };
            __assign.Invoke(fobj, flag, Type.DefaultBinder, parameters, null);

            int id = (int)IDMap.Invoke(fobj, null);

            if (!table.ContainsKey(id))
                table.Add(id, fobj);
        }
    }

    public void UnInit()
    {
        bInit = false;
    }


    public Dictionary<int, object> GetTable<T>()
    {
        return GetTable(typeof(T));
    }

    public Dictionary<int, object> GetTable(Type curType)
    {
        Dictionary<int, object> NullTable = new Dictionary<int, object>();

        if (!mTypeTableDict.ContainsKey(curType))
        {
            return NullTable;
        }

        Dictionary<int, object> table = mTypeTableDict[curType];
        if (table == null)
        {
            return NullTable;
        }

        return table;
    }


    public object GetTableItem<T>(int id)
    {
        var curType = typeof(T);

        Dictionary<int, object> curTblDict = null;
        if (mTypeTableDict.TryGetValue(curType, out curTblDict))
        {
            object curItem = null;
            if (curTblDict.TryGetValue(id, out curItem))
            {
                return (T)curItem;
            }
            else
            {
                return null;
            }
        }
        else
        {
            return null;
        }
    }

    public object GetTableItem(Type curType,int id, string who = "", string dowhat = "")
    {
        Dictionary<int, object> curTblDict = null;
        if(mTypeTableDict.TryGetValue(curType,out curTblDict))
        {
            object curItem = null;
            if(curTblDict.TryGetValue(id,out curItem))
            {
                return curItem;
            }
            else
            {
                return null;
            }
        }
        else
        {
            return null;
        }
    }

    public T GetTableItemByIndex<T>(int iIndex)
    {
        var curType = typeof(T);
        if (!mTypeTableDict.ContainsKey(curType))
        {
            return default(T);
        }

        var table = mTypeTableDict[curType];

        if (table == null)
        {
            return default(T);
        }

        int iCount = 0;
        foreach (var TableID in table.Keys)
        {
            if (iCount == iIndex)
            {
                return (T)(table[TableID]);
            }

            iCount++;
        }

        return default(T);
    }

    public int GetTableItemCount<T>()
    {
        var curType = typeof(T);
        if (!mTypeTableDict.ContainsKey(curType))
        {
            return -1;
        }

        var table = mTypeTableDict[curType];

        if (table == null)
        {
            return -1;
        }

        return table.Count;
    }

    public T GetTableItem<T>(string key)
    {
        var item = (T)GetTableItem<T>(key.GetHashCode());

        if (item == null)
        {
        }

        return item;
    }

    private string _getTablePath(Type type)
    {
        return kTablePath + type.Name;
    }

   /* public static int GetValueFromUnionCell(UnionCell ucell, int level, bool bNeedBaseLevel = true)
    {
        if (bNeedBaseLevel && level <= 0)
        {
            level = 1;
        }

        if (level > 0)
        {
            var valueType = ucell.valueType;

            if (valueType == UnionCellType.union_fix)
            {
                return ucell.fixValue;
            }

            if (valueType == UnionCellType.union_fixGrow)
            {
                return ucell.fixInitValue + (level - 1) * ucell.fixLevelGrow;
            }

            if (valueType == UnionCellType.union_everyvalue)
            {
                if (level - 1 < ucell.eValues.everyValues.Count)
                {
                    return ucell.eValues.everyValues[level - 1];
                }
                //超过就返回最后那个
                else
                {
                    return ucell.eValues.everyValues[ucell.eValues.everyValues.Count - 1];
                }
            }

            return 0;
        }

        return 0;
    }*/
}
