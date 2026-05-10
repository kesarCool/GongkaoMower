using System;
using System.Collections.Generic;
using NPOI.SS.UserModel;

namespace xls
{
    public class Table
    {
        public bool needCryptic = false;
        public NPOI.SS.UserModel.ISheet sheet = null;

        public string tablename;
        public List<Field> fields = new List<Field>();
        public List<Enum> enums = new List<Enum>();
        public HashSet<UserClass> ucs = new HashSet<UserClass>();

        public int tablerow = 0;
        public int tablecol = 0;
        public string[,] data = null;
        public string[] defaultValue = null;
        public string filepath;
        public int encode = 0;


        public Dictionary<string, UserClass> allucs = new Dictionary<string, UserClass>();

        public class Enum
        {
            public class Value
            {
                public string name;
                public string value;
                public string desc;
            }

            public string name;
            public string typename;
            public bool existZero = false;
            public List<Value> values = new List<Value>();
        }

        public class UserClass
        {
            public string label;
            public string classname;
            public string filename;
     

            public UserClass(string l, string c, string f)
            {
                label = l;
                classname = c;
                filename = f;
   
            }
        }

        public class Field
        {
            public string type = "";
            public string name = "";
            public string origntype = "";
            public bool repeated = false;
            public bool isenum = false;
            public bool cryptic = false;
            public bool isfloat = false;
            public int defaultInt;
            public string defaultString;
            // 注释
            public string note = "";
        }


        public Table()
        {
            UserClass uc = new UserClass("union", "UnionCell", "Union");
            allucs.Add("union", uc);
            allucs.Add("union(float)", uc);
        }

        public bool ParserFrom(NPOI.SS.UserModel.ISheet sheet,string fp)
        {
            this.sheet = sheet;
            this.filepath = fp;
            tablename = sheet.SheetName; 
            
            NPOI.SS.UserModel.IRow requireds = sheet.GetRow(0);
            NPOI.SS.UserModel.IRow types = sheet.GetRow(1);
            NPOI.SS.UserModel.IRow names = sheet.GetRow(2);

            NPOI.SS.UserModel.IRow cryptics;
            NPOI.SS.UserModel.IRow descs;


            Console.WriteLine("{0} {1}", tablename, fp);

            do
            {
                descs = cryptics = sheet.GetRow(3);
                for (var index = cryptics.FirstCellNum; index < cryptics.LastCellNum; ++index)
                {
                    if (cryptics.GetCell(index).StringCellValue == "cryptic")
                    {
                        needCryptic = true;
                        descs = sheet.GetRow(4);
                        break;
                    }
                }
            } while (false);


            for (int index = requireds.FirstCellNum; index < requireds.LastCellNum; ++index)
            {
                string require = requireds.GetCell(index).StringCellValue;

                string type = types.GetCell(index).StringCellValue.Split(':')[0];
                string name = names.GetCell(index).StringCellValue.Split(':')[0];
                string desc = descs.GetCell(index).StringCellValue;

                var cell = cryptics.GetCell(index);
                string cryptic = cell != null ? cell.StringCellValue : null; // != null? cryptics.GetCell(index).StringCellValue:"";


                Field field = new Field();
                field.origntype = type;

                // 计算注释
                {
                    string[] parts = desc.Split('\n');
                    if (parts.Length > 0)
                    {
                        field.note = parts[0];
                    }
                }

                // 类型
                if (type == "enum")
                {
                    field.isenum = true;
                    type = "e" + name;
                    string[] parts = desc.Split('\n');

                    Enum enu = new Enum();
                    enu.name = name;
                    enu.typename = type;
                    for (int enumnindex = 0; enumnindex < parts.Length; ++enumnindex)
                    {
                        string enumvalue = parts[enumnindex];
                        string[] enumvalues = enumvalue.Split(':');

                        if (enumvalues.Length != 3)
                        {
                            continue;
                        }

                        Enum.Value value = new Enum.Value();

                        value.name = enumvalues[0];
                        value.value = enumvalues[1];
                        value.desc = enumvalues[2];
                        if (value.value == "0")
                        {
                            enu.values.Insert(0, value);
                        }
                        else
                        {
                            enu.values.Add(value);
                        }


                        if (!enu.existZero && value.value == "0")
                        {
                            enu.existZero = true;
                        }
                    }

                    enums.Add(enu);
                }
                else{
                    UserClass uc = null;
                    if (allucs.TryGetValue(type, out uc))
                    {
                        type = uc.classname;
                        ucs.Add(uc);

                        field.isfloat = field.origntype.Contains("float");
                    }
                }

                if (name == "ID")
                {
                    type = "sint32";
                }

                field.type = type;
                field.name = name;
                field.repeated = require == "repeated";
                field.cryptic = cryptic == "cryptic";

                fields.Add(field);
            }

            // Excel 常把 LastCellNum 拉到右侧空列，产生无字段名的尾列；去掉后再定列数，避免 .fbs 出现非法的 ":;"
            while (fields.Count > 0 && string.IsNullOrWhiteSpace(fields[fields.Count - 1].name))
            {
                fields.RemoveAt(fields.Count - 1);
            }

            int colnum = fields.Count;
            int rownum = sheet.LastRowNum - sheet.FirstRowNum + 1;
            int offset = cryptics == descs ? 5 : 6;
            rownum -= offset;
            data = new string[rownum, colnum];

            tablerow = rownum;
            tablecol = colnum;

            for (int row = 0; row < rownum; ++row)
            {
                NPOI.SS.UserModel.IRow pRow = sheet.GetRow(row + offset);
                if (pRow == null)
                {
                    tablerow = row;
                    break;
                }

                {
                    // 第一列没内容 或者为空 视为终止
                    NPOI.SS.UserModel.ICell cell = pRow.GetCell(0);
                    if (cell == null)
                    {
                        tablerow = row;
                        break;
                    }

                    string value;
                    switch (cell.CellType)
                    {
                        case CellType.Numeric:
                        {
                            value = cell.NumericCellValue.ToString();
                            break;
                        }

                        case CellType.Formula:
                        {
                                value = cell.NumericCellValue.ToString();
                                break;
                        }

                        case CellType.String:
                        {
                            value = cell.StringCellValue;
                            break;
                        }
                        default:
                            value = "";
                            break;
                    }

                    if (value == "")
                    {
                        tablerow = row;
                        break;
                    }

                }
                
                

                for (int col = 0; col < colnum; ++col)
                {
                     string type = fields[col].type;

                    NPOI.SS.UserModel.ICell cell = pRow.GetCell(col);
                    Field field = fields[col];
                    string value;
                    if (cell == null)
                    {
                        value = "";
                        data[row, col] = value;
                        continue;
                    }

                    switch (cell.CellType)
                    {
                        case CellType.Numeric:
                        {
                            value = cell.NumericCellValue.ToString();
                             break;
                        }

                        case CellType.Formula:
                            {
                                value = cell.NumericCellValue.ToString();
                                break;
                            }

                        case CellType.String:
                        {
                            value = cell.StringCellValue;
                                break;
                        }
                        default:
                            value = "";
                            break;
                    }

                    if (value == "-" && type != "string")
                    {
                        value = "";
                    }

                    data[row, col] = value;

                }


            }
            defaultValue = new string[tablecol];
            Dictionary<string, int> maxdic = new Dictionary<string, int>();
            for (int col = 0; col < tablecol; ++col)
            {
                maxdic.Clear();

                for (int row = 0; row < tablerow; ++row)
                {
                    string value = data[row, col];
                    if (maxdic.ContainsKey(value))
                    {
                        maxdic[value] += 1;
                    }
                    else
                    {
                        maxdic.Add(value,1);
                    }
                }

                string def = "";
                int maxnum = 10;

                string type = fields[col].type;
                if (type == "sint32" || type == "enum" || type == "float" || type == "realfloat")
                {
                    def = "0";
                }
                else
                {
                    type = "";
                }

                foreach (var pair in maxdic)
                {
                    if (pair.Value > maxnum)
                    {
                        def = pair.Key;
                    }
                }
                defaultValue[col] = def;
            }



            encode = Encode(tablename);
            CheckExport();
            return true;
        }
        public bool ParserFrom(List<NPOI.SS.UserModel.ISheet> sheets, string fp)
        {
            this.sheet = sheets[0];
            this.filepath = fp;
             tablename = this.sheet.SheetName; 
            
            NPOI.SS.UserModel.IRow requireds = this.sheet.GetRow(0);
            NPOI.SS.UserModel.IRow types = this.sheet.GetRow(1);
            NPOI.SS.UserModel.IRow names = this.sheet.GetRow(2);

            NPOI.SS.UserModel.IRow cryptics;
            NPOI.SS.UserModel.IRow descs;


            Console.WriteLine("{0} {1}", tablename, fp);

            do
            {
                descs = cryptics = this.sheet.GetRow(3);
                for (var index = cryptics.FirstCellNum; index < cryptics.LastCellNum; ++index)
                {
                    if (cryptics.GetCell(index).StringCellValue == "cryptic")
                    {
                        needCryptic = true;
                        descs = this.sheet.GetRow(4);
                        break;
                    }
                }
            } while (false);


            for (int index = requireds.FirstCellNum; index < requireds.LastCellNum; ++index)
            {
                string require = requireds.GetCell(index).StringCellValue;

                string type = types.GetCell(index).StringCellValue.Split(':')[0];
                string name = names.GetCell(index).StringCellValue.Split(':')[0];
                string desc = descs.GetCell(index).StringCellValue;

                var cell = cryptics.GetCell(index);
                string cryptic = cell != null ? cell.StringCellValue : null; // != null? cryptics.GetCell(index).StringCellValue:"";


                Field field = new Field();
                field.origntype = type;

                // 计算注释
                {
                    string[] parts = desc.Split('\n');
                    if (parts.Length > 0)
                    {
                        field.note = parts[0];
                    }
                }

                // 类型
                if (type == "enum")
                {
                    field.isenum = true;
                    type = "e" + name;
                    string[] parts = desc.Split('\n');

                    Enum enu = new Enum();
                    enu.name = name;
                    enu.typename = type;
                    for (int enumnindex = 0; enumnindex < parts.Length; ++enumnindex)
                    {
                        string enumvalue = parts[enumnindex];
                        string[] enumvalues = enumvalue.Split(':');

                        if (enumvalues.Length != 3)
                        {
                            continue;
                        }

                        Enum.Value value = new Enum.Value();

                        value.name = enumvalues[0];
                        value.value = enumvalues[1];
                        value.desc = enumvalues[2];
                        if (value.value == "0")
                        {
                            enu.values.Insert(0, value);
                        }
                        else
                        {
                            enu.values.Add(value);
                        }


                        if (!enu.existZero && value.value == "0")
                        {
                            enu.existZero = true;
                        }
                    }

                    enums.Add(enu);
                }

                else{
                    UserClass uc = null;
                    if (allucs.TryGetValue(type, out uc))
                    {
                        type = uc.classname;
                        ucs.Add(uc);

                        field.isfloat = field.origntype.Contains("float");
                    }
                }

                if (name == "ID")
                {
                    type = "sint32";
                }

                field.type = type;
                field.name = name;
                field.repeated = require == "repeated";
                field.cryptic = cryptic == "cryptic";

                fields.Add(field);
            }

            while (fields.Count > 0 && string.IsNullOrWhiteSpace(fields[fields.Count - 1].name))
            {
                fields.RemoveAt(fields.Count - 1);
            }

            int colnum = fields.Count;
            int offset = cryptics == descs ? 4 : 5;

            List<NPOI.SS.UserModel.IRow> rows = new List<NPOI.SS.UserModel.IRow>();
            for (int i = 0; i < sheets.Count; ++i)
            {
                var currSheet = sheets[i];
                int rindex = i == 0 ? offset : 4;
                while (true)
                {
                    NPOI.SS.UserModel.IRow pRow = currSheet.GetRow(rindex++);
                    if (pRow == null)
                    {
                        break;
                    }
                    NPOI.SS.UserModel.ICell cell = pRow.GetCell(0);
                    if (cell == null)
                    {
                        break;
                    }
                    rows.Add(pRow);
                }
            }



            data = new string[rows.Count, colnum];

            tablerow = rows.Count;
            tablecol = colnum;

            // int rowIndex = -1;
            for (int row = 0; row < rows.Count; ++row)
            {

                NPOI.SS.UserModel.IRow pRow = rows[row];
                if (pRow == null)
                {
                    tablerow = row;
                    break;
                }

                {

                    // 第一列没内容 或者为空 视为终止
                    NPOI.SS.UserModel.ICell cell = pRow.GetCell(0);
                    if (cell == null)
                    {
                        tablerow = row;
                        break;
                    }

                    string value;
                    switch (cell.CellType)
                    {
                        case CellType.Numeric:
                        {
                            value = cell.NumericCellValue.ToString();
                            break;
                        }

                        case CellType.Formula:
                        {
                                value = cell.NumericCellValue.ToString();
                                break;
                        }

                        case CellType.String:
                        {
                            value = cell.StringCellValue;
                            break;
                        }
                        default:
                            value = "";
                            break;
                    }

                    if (value == "")
                    {
                        tablerow = row;
                        break;
                    }
                }
                
                for (int col = 0; col < colnum; ++col)
                {
                     string type = fields[col].type;

                    NPOI.SS.UserModel.ICell cell = pRow.GetCell(col);
                    Field field = fields[col];
                    string value;
                    if (cell == null)
                    {
                        value = "";
                        data[row, col] = value;
                        continue;
                    }

                    switch (cell.CellType)
                    {
                        case CellType.Numeric:
                        {
                            value = cell.NumericCellValue.ToString();
                             break;
                        }

                        case CellType.Formula:
                            {
                                value = cell.NumericCellValue.ToString();
                                break;
                            }

                        case CellType.String:
                        {
                            value = cell.StringCellValue;
                                break;
                        }
                        default:
                            value = "";
                            break;
                    }

                    if (value == "-" && type != "string")
                    {
                        value = "";
                    }

                    data[row, col] = value;

                }


            }
            defaultValue = new string[tablecol];
            Dictionary<string, int> maxdic = new Dictionary<string, int>();
            for (int col = 0; col < tablecol; ++col)
            {
                maxdic.Clear();

                for (int row = 0; row < tablerow; ++row)
                {
                    string value = data[row, col];
                    if (maxdic.ContainsKey(value))
                    {
                        maxdic[value] += 1;
                    }
                    else
                    {
                        maxdic.Add(value,1);
                    }
                }

                string def = "";
                int maxnum = 10;

                string type = fields[col].type;
                if (type == "sint32" || type == "enum" || type == "float" || type == "realfloat")
                {
                    def = "0";
                }
                else
                {
                    type = "";
                }

                foreach (var pair in maxdic)
                {
                    if (pair.Value > maxnum)
                    {
                        def = pair.Key;
                    }
                }
                defaultValue[col] = def;
            }



            encode = Encode(tablename);
            CheckExport();
            return true;
        }
        // 随便写的 
        public static int Encode(string str)
        {
            int result = 0;
            var arr = str.ToCharArray();
            foreach (var code in arr)
            {
                result ^= code;
                result = (result * 5) + 0x76546b64;
            }
            return result;
        }


        public void CheckExport()
        {
            int count = 0;
            for (int index = 0; index < fields.Count; ++index)
            {
                count += fields[index].note.Contains("notexport")  ? 1 : 0;
            }

            if (count == 0)
            {
                return;
            }


            int colindex = 0;
            for (int index = 0; index < fields.Count; ++index)
            {
                bool export = !fields[index].note.Contains("notexport");
                if (export)
                {
                    for (int row = 0; row < this.tablerow; ++row)
                    {
                        this.data[row, colindex] = this.data[row, index];
                    }
                    colindex++;
                }
            }

            this.tablecol = colindex;

            fields.RemoveAll((Field field) =>
            {
                return field.note.Contains("notexport");
            });

        }
        
    }
}