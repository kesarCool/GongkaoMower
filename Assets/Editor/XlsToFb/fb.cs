using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlatBuffers;
using UnityEditor;
using UnityEngine;

namespace xls
{
    public class fb
    {
        //./flatc.exe  --csharp -o ../fblib ./fb/BuffDrugConfigInfoTable.fbs

        /// <summary>与 <see cref="TableManager"/> 的 Resources 路径 Data/table_fb/ 一致。</summary>
        private const string TableFbBytesResourcesDir = "Assets/Resources/Data/table_fb/";

        private static Dictionary<string, string> typemap = new Dictionary<string, string>();

        private static string TypeMapping(string type)
        {
            if (typemap.Count == 0)
            {
                typemap.Add("sint32", "int32");
                typemap.Add("float", "int32");
                typemap.Add("realfloat", "float");
                typemap.Add("union", "UnionCell");
            }


            if (typemap.ContainsKey(type))
            {
                return typemap[type];
            }

            return type;
        }

        /// <summary>flatc / C# 标识符：仅 ASCII 字母数字下划线，且不能以数字开头。</summary>
        private static bool IsValidFlatBufferTableName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 120)
                return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ascii = c < 128;
                if (i == 0)
                {
                    if (!ascii || !(char.IsLetter(c) || c == '_'))
                        return false;
                }
                else
                {
                    if (!ascii || !(char.IsLetterOrDigit(c) || c == '_'))
                        return false;
                }
            }
            return true;
        }
     

        public static string GenerateDescEx(Table table)
        {
            StringBuilder tablebuilder = new StringBuilder();

            foreach (var uc in table.ucs)
            {
                tablebuilder.Append(string.Format("include \"{0}.fbs\"; \n\n", uc.filename));
            }

            // 枚举
            if (table.enums.Count > 0)
            {
                // 
                tablebuilder.Append(string.Format("namespace ProtoTable.{0};\n", table.tablename));

                foreach (var enu in table.enums)
                {
                    tablebuilder.Append("enum ");
                    tablebuilder.Append(enu.typename);
                    tablebuilder.Append(":int");
                    tablebuilder.Append(" { \n");

                    // 补齐0
                    var first = enu.values[0];
                    if (first.value != "0")
                    {
                        tablebuilder.Append(string.Format("\t{0}_None = 0,   // default\n", enu.name));
                    }

                    foreach (var value in enu.values)
                    {
                        tablebuilder.Append(string.Format("\t{0} = {1},    // {2}\n", value.name, value.value,
                            value.desc));
                    }

                    tablebuilder.Append("}\n");
                }
            }

            tablebuilder.Append(string.Format("namespace ProtoTable.{0};\n", table.tablename));
            tablebuilder.Append("enum eCrypt :int{\n");
            tablebuilder.Append("\tcode = " + table.encode.ToString() + ",");
            tablebuilder.Append("}\n");

            tablebuilder.Append("\n");
            tablebuilder.Append("namespace ProtoTable;\n");
            tablebuilder.Append("table ");
            tablebuilder.Append(table.tablename);
            tablebuilder.Append("{\n");

            for (int colindex = 0; colindex < table.fields.Count; ++colindex)
            {
                var field = table.fields[colindex];
                if (string.IsNullOrWhiteSpace(field.name))
                    continue;

                tablebuilder.Append("\t");
                tablebuilder.Append(field.name);
                tablebuilder.Append(":");

                if (field.repeated)
                {
                    tablebuilder.Append("[");
                }

                tablebuilder.Append(field.isenum ? table.tablename + "." + TypeMapping(field.type) : TypeMapping(field.type));

                if (field.repeated)
                {
                    tablebuilder.Append("]");
                }

                tablebuilder.Append(";\n");
            }

            tablebuilder.Append("}\n\n");

            tablebuilder.Append("root_type ");
            tablebuilder.Append(table.tablename);
            tablebuilder.Append(";");
            return tablebuilder.ToString();
        }

        public static bool GenerateDesc(Table table)
        {
            if (!IsValidFlatBufferTableName(table.tablename))
            {
                const string hint = "Excel 第一个 Sheet 的名称须为英文标识符（字母或下划线开头，仅 ASCII 字母/数字/_），例如 LexiconTable。\n当前 Sheet 名: ";
                Debug.LogError("[fb.GenerateDesc] " + hint + "\"" + table.tablename + "\"");
                EditorUtility.DisplayDialog("表名无效（flatc 无法生成）", hint + "\"" + table.tablename + "\"", "确定");
                return false;
            }

            StringBuilder tablebuilder = new StringBuilder();

            foreach (var uc in table.ucs)
            {
                tablebuilder.Append(string.Format("include \"{0}.fbs\"; \n\n", uc.filename));
            }

            // 枚举
            if (table.enums.Count > 0)
            {
                tablebuilder.Append(string.Format("namespace ProtoTable.{0};\n", table.tablename));

                foreach (var enu in table.enums)
                {
                    tablebuilder.Append("enum ");
                    tablebuilder.Append(enu.typename);
                    tablebuilder.Append(":int");
                    tablebuilder.Append(" { \n");

                    // 补齐0
                    var first = enu.values[0];
                    if (first.value != "0")
                    {
                        tablebuilder.Append(string.Format("\t{0}_None = 0,   // default\n", enu.name));
                    }


                    foreach (var value in enu.values)
                    {
                        tablebuilder.Append(string.Format("\t{0} = {1},    // {2}\n", value.name, value.value,
                            value.desc));
                    }

                    tablebuilder.Append("}\n");
                }
            }
            tablebuilder.Append(string.Format("namespace ProtoTable.{0};\n", table.tablename));
            tablebuilder.Append("enum eCrypt :int{\n");
            tablebuilder.Append("\tcode = " + table.encode.ToString() + ",");
            tablebuilder.Append("}\n");


            tablebuilder.Append("\n");
            tablebuilder.Append("namespace ProtoTable;\n");
            tablebuilder.Append("table ");
            tablebuilder.Append(table.tablename);
            tablebuilder.Append("{\n");

            for (int colindex = 0;colindex < table.fields.Count;++colindex)
            {
                var field = table.fields[colindex];
                if (string.IsNullOrWhiteSpace(field.name))
                    continue;

                tablebuilder.Append("\t");
                tablebuilder.Append(field.name);
                tablebuilder.Append(":");

                if (field.repeated)
                {
                    tablebuilder.Append("[");
                }

                tablebuilder.Append(
                    field.isenum ? table.tablename + "." + TypeMapping(field.type) : TypeMapping(field.type));

                if (field.repeated)
                {
                    tablebuilder.Append("]");
                }

                tablebuilder.Append(";\n");
            }

            tablebuilder.Append("}\n\n");

            tablebuilder.Append("root_type ");
            tablebuilder.Append(table.tablename);
            tablebuilder.Append(";");


            {
                string outputname = "Assets/Editor/XlsToFb/fbs/" + table.tablename + ".fbs";

                if (File.Exists(outputname))
                {
                    File.Delete(outputname);
                }

                using (FileStream output = new FileStream(outputname, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    StreamWriter sw = new StreamWriter(output);
                    sw.Write(tablebuilder.ToString());
                    sw.Close();
                    output.Close();
                }

                // 执行转换命令（cwd = Assets/Editor/XlsToFb：向上两级到 Assets，再 Script/Table/ProtoTable）
                string cs = "Assets/Script/Table/ProtoTable/" + table.tablename + ".cs";
                string dir = System.IO.Directory.GetCurrentDirectory();
                string protoDir = Path.Combine(dir, "Assets", "Script", "Table", "ProtoTable");
                Directory.CreateDirectory(protoDir);
                string flatcDir = Path.GetFullPath(Path.Combine(dir, "Assets", "Editor", "XlsToFb"));
                string flatcExe = Path.Combine(flatcDir, "flatc.exe");
                if (!File.Exists(flatcExe))
                {
                    Debug.LogError("[fb.GenerateDesc] 未找到 flatc.exe: " + flatcExe);
                    return false;
                }
                System.IO.Directory.SetCurrentDirectory(flatcDir);
                int code = cmd.ProcessCommand(flatcExe, " --csharp --gen-onefile -o ../../Script/Table/ProtoTable fbs/" + table.tablename + ".fbs");

                System.IO.Directory.SetCurrentDirectory(dir);

                if (code != 0)
                {
                    Debug.LogError("[fb.GenerateDesc] flatc 失败，退出码=" + code + "，表=" + table.tablename);
                    return false;
                }

                if (!File.Exists(cs))
                {
                    Debug.LogError("[fb.GenerateDesc] 未找到生成文件: " + Path.GetFullPath(cs) + "（请检查 flatc -o 与表名/sheet 名是否合法）");
                    return false;
                }
            }

            return true;
        }

        public static bool CheckCs(Table table)
        {
            string outputname = "Assets/Editor/XlsToFb/fbs/" + table.tablename + ".fbs";
            if (!File.Exists(outputname))
            {
                // 预设没改字段
                return false;
            }

            string olddesc = File.ReadAllText(outputname);
            string newdesc = GenerateDescEx(table);
            return olddesc == newdesc;
        }

        private static int ConvertToInt(string value, bool isFloat = false)
        {
            int ret = 0;
            ret = isFloat ? (int)(Convert.ToDouble(value) * 1000) : Convert.ToInt32(value);
            return ret;
        }

        private const char SPLITE_REPEATED = '|';
        private const string FIX_EVERY_SPLIT = ",";
        private const string FIX_GROW_SPLIT = ";";
        private const string RepeatedSplit = "|";

        private static FlatBufferBuilder builder = new FlatBufferBuilder(1024 * 1024);

        public static bool DumpData(Table table)
        {
            builder.Clear();
            int[] rowoffsets = new int[table.tablerow];
            int[] coloffsets = new int[table.tablecol];
            for (int row = 0; row < table.tablerow; ++row)
            {
                for (int col = 0; col < table.tablecol; ++col)
                {
                    xls.Table.Field field = table.fields[col];
                    string content = table.data[row, col];


                    if (content == "")
                    {
                        coloffsets[col] = 0;
                        continue;
                    }
                    try
                    {
                        if (field.repeated)
                        {
                            if (field.type == "UnionCell")
                            {
                                if (content == "0")
                                {
                                    coloffsets[col] = 0;
                                }
                                else
                                {
                                    string[] unions = content.Split(SPLITE_REPEATED);
                                    int[] unionoffset = new int[unions.Length];

                                    for (int index = 0; index < unions.Length; ++index)
                                    {

                                        unionoffset[index] = DumpUnionCell(builder, unions[index], field.isfloat, true);
                                    }

                                    builder.StartVector(sizeof(int), unionoffset.Length, sizeof(int));
                                    for (int i = unionoffset.Length - 1; i >= 0; i--)
                                    {
                                        builder.AddOffset(unionoffset[i]);
                                    }

                                    coloffsets[col] = builder.EndVector().Value;
                                }


                            }
                            else if (field.type == "sint32")
                            {
                                string[] ints = content.Split(SPLITE_REPEATED);



                                builder.StartVector(sizeof(int), ints.Length, sizeof(int));
                                for (int i = ints.Length - 1; i >= 0; i--)
                                {
                                    int result = 0;
                                    if (int.TryParse(ints[i], out result))
                                    {
                                        builder.AddInt(result ^ table.encode);

                                    }
                                    else
                                    {
                                        builder.AddInt(0 ^ table.encode);
                                    }
                                }

                                coloffsets[col] = builder.EndVector().Value;
                            }
                            else if (field.type == "float")
                            {


                                string[] ints = content.Split(SPLITE_REPEATED);



                                builder.StartVector(sizeof(int), ints.Length, sizeof(int));
                                for (int i = ints.Length - 1; i >= 0; i--)
                                {
                                    int result = 0;
                                    float value = 0.0f;
                                    if (float.TryParse(ints[i], out value))
                                    {
                                        value *= 1000;
                                        result = (int)value;
                                        builder.AddInt(result ^ table.encode);
                                    }
                                    else
                                    {
                                        // 记录一下日志
                                        builder.AddInt(0 ^ table.encode);
                                    }
                                }

                                coloffsets[col] = builder.EndVector().Value;



                            }
                            else if (field.type == "string")
                            {
                                string[] strings = content.Split(SPLITE_REPEATED);
                                int[] stringoffset = new int[strings.Length];

                                for (int index = 0; index < strings.Length; ++index)
                                {
                                    stringoffset[index] = builder.CreateString(strings[index]).Value;
                                }

                                builder.StartVector(sizeof(int), stringoffset.Length, sizeof(int));
                                for (int i = stringoffset.Length - 1; i >= 0; i--)
                                {
                                    builder.AddOffset(stringoffset[i]);
                                }

                                coloffsets[col] = builder.EndVector().Value;
                            }
                            else if (field.type == "realfloat")
                            {
                                string[] floats = content.Split(SPLITE_REPEATED);

                                builder.StartVector(sizeof(int), floats.Length, sizeof(int));
                                for (int i = floats.Length - 1; i >= 0; i--)
                                {
                                    builder.AddFloat(float.Parse(floats[i]));
                                }

                                coloffsets[col] = builder.EndVector().Value;
                            }
                            else
                            {
                                if (field.isenum)
                                {

                                    string[] ints = content.Split(SPLITE_REPEATED);

                                    builder.StartVector(sizeof(int), ints.Length, sizeof(int));
                                    for (int i = ints.Length - 1; i >= 0; i--)
                                    {
                                        builder.AddInt(int.Parse(ints[i]));
                                    }
                                    coloffsets[col] = builder.EndVector().Value;


                                    continue;
                                }


                                Console.WriteLine("有类型未处理 ");
                            }
                        }
                        else
                        {
                            if (field.type == "UnionCell")
                            {
                                coloffsets[col] = DumpUnionCell(builder, content, field.isfloat);
                            }
                            else if (field.type == "string")
                            {
                                coloffsets[col] = builder.CreateString(content).Value;
                            }
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }


                builder.StartObject(table.tablecol);
                for (int col = table.tablecol - 1; col >= 0; --col)
                {
                    xls.Table.Field field = table.fields[col];
                    string content = table.data[row, col];
                    if (field.repeated)
                    {
                        builder.AddOffset(col, coloffsets[col], 0);
                    }
                    else
                    {
                        switch (field.type)
                        {
                            case "sint32":
                            {

                                int result = 0;
  
                                if (int.TryParse(content, out result))
                                {
                                    builder.AddInt(col, result ^ table.encode, 0);
                                }
                                else
                                {
                                    builder.AddInt(col, 0, 0);
                                }
                                break;
                            }
                            case "float":
                            {
                                int result = 0;
                                    float f = 0.0f;
                                if (float.TryParse(content, out f))
                                {
                                    f *= 1000;
                                    result = (int)f;
                                    builder.AddInt(col, result ^ table.encode, 0);
                                }
                                else
                                {
                                    builder.AddInt(col, 0, 0);
                                }

                                break;
                            }
                            case "string":
                                builder.AddOffset(col, coloffsets[col], 0);
                                break;
                            case "UnionCell":
                                builder.AddOffset(col, coloffsets[col], 0);
                                break;


                            case "realfloat":
                            {
                                float result = 0.0f;
                                if (float.TryParse(content, out result))
                                {
                                    builder.AddFloat(col, result, 0);
                                }
                                else
                                {
                                    builder.AddFloat(col, 0.0f, 0);
                                }

                                break;
                            }


                            case "bool":
                                int num = 0;
                                int.TryParse(content, out num);
                                builder.AddBool(col, num != 0, false);
                                break;
                          
                            default:

                            {
                                // 
                                if (field.isenum)
                                {
                                    int result = 0;
                                    if (int.TryParse(content, out result))
                                    {
                                        builder.AddInt(col, result, 0);
                                    }
                                    else
                                    {
                                        builder.AddInt(col, 0, 0);
                                    }

                                    continue;
                                }




                                Console.WriteLine("Error:有类型未处理 ");
                                continue;
                             }
                        }
                    }
                }

                rowoffsets[row] = builder.EndObject();
            }


            builder.StartVector(sizeof(int), rowoffsets.Length, sizeof(int));
            for (int i = rowoffsets.Length - 1; i >= 0; i--)
            {
                builder.AddOffset(rowoffsets[i]);
            }

            int endoffset = builder.EndVector().Value;


            builder.Finish(endoffset);
            byte[] data = builder.SizedByteArray();

            string dirPath = "RawData/Data/table_fb/";
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

//             // 原始二进制
//             string path = dirPath + table.tablename + ".bytes";
// 
//             if (File.Exists(path))
//             {
//                 File.Delete(path);
//             }
// 
//             using (FileStream output = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
//             {
//                 output.Write(data, 0, data.Length);
//                 output.Close();
//             }

            // asset 文件（运行时 Resources.Load: Data/table_fb/{表名}）
            if (!Directory.Exists(TableFbBytesResourcesDir))
                Directory.CreateDirectory(TableFbBytesResourcesDir);
            string target = TableFbBytesResourcesDir + table.tablename + ".bytes";
            if (File.Exists(target))
            {
                File.Delete(target);
            }
            // AssetBinary asset = ScriptableSingleton<AssetBinary>.CreateInstance<AssetBinary>();
            // asset.m_DataBytes = data;
            // AssetDatabase.CreateAsset(asset, target);
            File.WriteAllBytes(target, data);

            AssetDatabase.ImportAsset(target);
            return true;
        }

        
        private static int DumpUnionCell(FlatBufferBuilder builder, string union, bool isf,bool isArray = false)
        {
            // 对数组里的unioncell 不能返回offset为0  必须是有效的
            if (union == "0" && isArray == false)
            {
                return 0;
            }

            if (union.Contains(FIX_EVERY_SPLIT))
            {
                var stringValues = union.Split(FIX_EVERY_SPLIT[0]);

                builder.StartVector(4, stringValues.Length, 4);


                for (int index = stringValues.Length-1; index >= 0; --index)
                {
                    builder.AddInt(ConvertToInt(stringValues[index], isf));
                }



                VectorOffset offset = builder.EndVector();

                builder.StartObject(1);
                builder.AddOffset(0,offset.Value,0);
                int objoffset = builder.EndObject();

                builder.StartObject(5);
                builder.AddInt(0, 3, 0);
                builder.AddOffset(1, objoffset, 0);
                builder.AddInt(2, 0, 0);
                builder.AddInt(3, 0, 0);
                builder.AddInt(4, 0, 0);
                return builder.EndObject();
            }
            else if (union.Contains(FIX_GROW_SPLIT))
            {
                var stringValues = union.Split(FIX_GROW_SPLIT[0]);
                if (stringValues.Length != 2)
                {
                    Console.WriteLine("[GenerateText] union 格式错误 {0}", union);
                    return 0;
                }

                builder.StartObject(5);
                builder.AddInt(0, 2, 0);
                builder.AddOffset(1, 0, 0);
                builder.AddInt(2, 0, 0);
                builder.AddInt(3, ConvertToInt(stringValues[0], isf), 0);
                builder.AddInt(4, ConvertToInt(stringValues[1], isf), 0);
                return builder.EndObject();
            }
            else
            {
                builder.StartObject(5);
                builder.AddInt(0, 1, 0);
                builder.AddOffset(1,0, 0);
                builder.AddInt(2, ConvertToInt(union, isf), 0);
                builder.AddInt(3, 0, 0);
                builder.AddInt(4,0 , 0);
                return builder.EndObject();
            }
        }
    }
}