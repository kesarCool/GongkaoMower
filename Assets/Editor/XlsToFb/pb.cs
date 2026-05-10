//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//
//namespace xls
//{
//    public class pb
//    {
//
//        public bool needCryptic = false;
//        private NPOI.SS.UserModel.ISheet sheet = null;
//
//        public List<Field> fields = new List<Field>();
//        public List<Enum> enums = new List<Enum>();
//
//        public class Enum
//        {
//            public class Value
//            {
//                public string name;
//                public string value;
//                public string desc;
//            }
//            public string name;
//           
//            public List<Value> values = new List<Value>();
//        }
//
//
//
//        public class Field
//        {
//            public string type = "";
//            public string name = "";
//            public bool repeated = false;
//
//            public bool cryptic = false;
//            public int defaultInt;
//            public string defaultString;
//        }
//
//
//        public pb()
//        {
//            
//        }
//
//        public bool Parser(NPOI.SS.UserModel.ISheet sheet)
//        {
//            this.sheet = sheet;
//
//            NPOI.SS.UserModel.IRow requireds = sheet.GetRow(0);
//            NPOI.SS.UserModel.IRow types = sheet.GetRow(1);
//            NPOI.SS.UserModel.IRow names = sheet.GetRow(2);
//            NPOI.SS.UserModel.IRow serverValid = sheet.GetRow(3);
//
//            NPOI.SS.UserModel.IRow cryptics;
//            NPOI.SS.UserModel.IRow descs;
//
//            do
//            {
//                descs = cryptics = sheet.GetRow(4);
//                for (var index = cryptics.FirstCellNum; index < cryptics.LastCellNum; ++index)
//                {
//                    if (cryptics.GetCell(index).StringCellValue == "cryptic")
//                    {
//                        needCryptic = true;
//                        descs = sheet.GetRow(5);
//                        break;
//                    }
//                }
//
//            } while (false);
//
//
//            for (int index = requireds.FirstCellNum; index < requireds.LastCellNum; ++index)
//            {
//                string require = requireds.GetCell(index).StringCellValue;
//
//                string type = types.GetCell(index).StringCellValue.Split(':')[0];
//                string name = names.GetCell(index).StringCellValue;
//                string desc = descs.GetCell(index).StringCellValue;
//                string cryptic = cryptics.GetCell(index).StringCellValue;
//
//                Field field = new Field();
//
//                // 类型
//                if (type == "enum")
//                {
//                    type = "e" + name;
//                    string[] parts = desc.Split('\n');
//
//                    Enum enu = new Enum();
//                    enu.name = type;
//
//                    for (int enumnindex = 0; enumnindex < parts.Length; ++enumnindex)
//                    {
//
//                        string enumvalue = parts[enumnindex];
//                        string[] enumvalues = enumvalue.Split(':');
//
//                        if (enumvalues.Length != 3)
//                        {
//                            continue;
//                        }
//                        Enum.Value value = new Enum.Value();
//
//                        value.name = enumvalues[0];
//                        value.value = enumvalues[1];
//                        value.desc = enumvalues[2];
//                        enu.values.Add(value);
//
//                    }
//                    enums.Add(enu);
//
//                }
//                else if (type == "union" || type == "union(float)")
//                {
//                    type = "UnionCell";
//                }
//
//                field.type = type;
//                field.name = name;
//                field.repeated = require == "repeated";
//                field.cryptic = cryptic == "cryptic";
//                fields.Add(field);
//           
//            }
//            return true;
//        }
//
//        bool GenerateProto()
//        {
//            return true;
//        }
//
//        bool DumpData()
//        {
//            return true;
//        }
//
//
//        // ./protoc.exe  --csharp_out=../pblib  ./pb/BuffDrugConfigInfoTable.proto
//        public static bool Build(NPOI.SS.UserModel.ISheet sheet)
//        {
//            if (sheet == null)
//            {
//                return false;
//            }
//            StringBuilder packagebuilder = new StringBuilder();
//            StringBuilder strbuilder = new StringBuilder();
//
//            do
//            {
//                NPOI.SS.UserModel.IRow requireds = sheet.GetRow(0);
//                NPOI.SS.UserModel.IRow types = sheet.GetRow(1);
//                NPOI.SS.UserModel.IRow names = sheet.GetRow(2);
//                NPOI.SS.UserModel.IRow descs = sheet.GetRow(4);
//
//
//
//                strbuilder.Append("syntax = \"proto3\";\n");
//                strbuilder.Append("import \"Union.proto\"; \n\n");
//                strbuilder.Append("package ProtoTable;\n");
//                strbuilder.Append("message ");
//                strbuilder.Append(sheet.SheetName);
//                strbuilder.Append("{\n");
//
//                bool hasUnion = false;
//
//                for (int index = requireds.FirstCellNum; index < requireds.LastCellNum; ++index)
//                {
//                    string require = requireds.GetCell(index).StringCellValue;
//
//
//                    string type = types.GetCell(index).StringCellValue.Split(':')[0];
//                    
//                    string name = names.GetCell(index).StringCellValue;
//                    string desc = descs.GetCell(index).StringCellValue;
//
//                    // 类型
//                    if (type == "enum")
//                    {
//                        type = "e" + name;
//                        strbuilder.Append("\tenum e");
//                        strbuilder.Append(name);
//  
//                        strbuilder.Append(" { \n");
//
//                        string[] parts = desc.Split('\n');
//                        for (int enumnindex = 0; enumnindex < parts.Length; ++enumnindex)
//                        {
//
//                            string enumvalue = parts[enumnindex];
//                            string[] enumvalues = enumvalue.Split(':');
//
//                            if (enumvalues.Length != 3)
//                            {
//                                continue;
//                            }
//                            strbuilder.Append("\t\t");
//                            strbuilder.Append(enumvalues[0]);
//                            strbuilder.Append(" = ");
//                            strbuilder.Append(enumvalues[1]);
//                            strbuilder.Append(";   // ");
//                            strbuilder.Append(enumvalues[2]);
//                            strbuilder.Append("\n");
//                        }
//
//                        strbuilder.Append("\t}\n");
//
//                    }
//                    else if (type == "union" || type == "union(float)")
//                    {
//                        type = "UnionCell";
//
//                        if (!hasUnion)
//                        {
//                            hasUnion = true;
//                            
//                        }
//                       
//                    }
//
//                    strbuilder.Append("\t");
//                    if (require == "repeated")
//                    {
//                        strbuilder.Append("repeated ");
//                    }
//                    strbuilder.Append(type);
//                    strbuilder.Append(" ");
//
//                    // 名称
//                    strbuilder.Append(name.Split(':')[0]);
//                    strbuilder.Append(" = ");
//                    strbuilder.Append(index + 1);
//                    strbuilder.Append(";\n");
//
//                }
//                strbuilder.Append("}");
//
////                
//
//            } while (false);
//
//            string outputname = "pb/" + sheet.SheetName + ".proto";
//
//            if (File.Exists(outputname))
//            {
//                File.Delete(outputname);
//            }
//            
//            FileStream output = new FileStream(outputname, FileMode.OpenOrCreate, FileAccess.Write);
//            StreamWriter sw = new StreamWriter(output);
//            sw.Write(strbuilder.ToString());
//            sw.Close();
//            output.Close();
//
//
//            if (!cmd.Run("protoc.exe", "--csharp_out=../pblib/pb --proto_path=./pb ./pb/"+ sheet.SheetName + ".proto"))
//            {
//                return false;
//            }
//
//            return true;
//        }
//
//        public static bool GenerateDesc(Table table)
//        {
//            StringBuilder tablebuilder = new StringBuilder();
//
//            tablebuilder.Append("syntax = \"proto3\";\n");
//
//            foreach (var uc in table.ucs)
//            {
////                tablebuilder.Append("import \"" + uc.filename + ".proto\"; \n\n");
//
//                tablebuilder.Append(string.Format("import \"{0}.proto\"; \n\n", uc.filename));
//            }
////            tablebuilder.Append("import \"Union.proto\"; \n\n");
//
//            tablebuilder.Append("package ProtoTable;\n"); //option allow_alias = true; 
//            
//            tablebuilder.Append("message ");
//            tablebuilder.Append(table.name);
//            tablebuilder.Append("{\n");
//
////            tablebuilder.Append(string.Format("message {0} {\n", table.name));
//            foreach (var enu in table.enums)
//            {
//                tablebuilder.Append("\tenum ");
//                tablebuilder.Append(enu.typename);
//       
//                tablebuilder.Append(" { \n");
//
////                tablebuilder.Append(string.Format("\tenum {0} { \n", enu.typename));
//               // tablebuilder.Append("\t\toption allow_alias = true;\n");
//                // 补齐0
//                var first = enu.values[0];
//                if (first.value != "0")
//                {
////                    tablebuilder.Append("\t");
////                    tablebuilder.Append(enu.name);
////                    tablebuilder.Append("_None = 0;   // default\n");
//
//                    tablebuilder.Append(string.Format("\t{0}_None = 0;   // default\n", enu.name));
//                }
//
//
//                foreach (var value in enu.values)
//                {
////                    tablebuilder.Append("\t\t");
////                    tablebuilder.Append(value.name);
////                    tablebuilder.Append(" = ");
////                    tablebuilder.Append(value.value);
////                    tablebuilder.Append(";    // ");
////                    tablebuilder.Append(value.desc);
////                    tablebuilder.Append("\n");
//
//                    tablebuilder.Append(string.Format("\t\t{0} = {1};\t//{2} \n", value.name, value.value, value.desc));
//                }
//
//                tablebuilder.Append("\t}\n");
//            }
//
//
//
////            foreach (var field in table.fields)
//            for (int index = 0; index < table.fields.Count;)
//            {
//                var field = table.fields[index];
////                tablebuilder.Append("\t");
////                if (field.repeated)
////                {
////                    tablebuilder.Append("repeated ");
////                }
////
////                tablebuilder.Append(field.type);
////                tablebuilder.Append(" ");
////                // 名称
////                tablebuilder.Append(field.name);
////                tablebuilder.Append(" = ");
////                tablebuilder.Append(++index);
////                tablebuilder.Append(";\n");
//
//                tablebuilder.Append(string.Format("\t{0}{1} {2} = {3};\n", field.repeated? "repeated ":"", field.type, field.name,++index));
//
//            }
//            tablebuilder.Append("}\n\n");
//
//
//            {
//                string outputname = "pb/" + table.name + ".proto";
//
//                if (File.Exists(outputname))
//                {
//                    File.Delete(outputname);
//                }
//
//                FileStream output = new FileStream(outputname, FileMode.OpenOrCreate, FileAccess.Write);
//                StreamWriter sw = new StreamWriter(output);
//                sw.Write(tablebuilder.ToString());
//                sw.Close();
//                output.Close();
//
//
//                if (!cmd.Run("protoc.exe", "--csharp_out=../pblib/pb --proto_path=./pb ./pb/" + table.name + ".proto"))
//                {
//                    return false;
//                }
//
//            }
//
//
//            return true;
//        }
//    }
//}