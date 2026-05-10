using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using UnityEditor;
using UnityEngine;

namespace xls
{
    class Xls2FB
    {
        public static List<string> FindFile(string sSourcePath)
        {
            List<String> list = new List<string>();

            //遍历文件夹

            DirectoryInfo theFolder = new DirectoryInfo(sSourcePath);

            FileInfo[] thefileInfo = theFolder.GetFiles("*.xls", SearchOption.TopDirectoryOnly);

            foreach (FileInfo NextFile in thefileInfo) //遍历文件
            {
                list.Add(NextFile.FullName);
            }

            return list;
        }


        public static string GetMd5Hash(FileStream fs)
        {
            MD5 md5Hash = MD5.Create();
            byte[] data = null;

           
            data = md5Hash.ComputeHash(fs);
            fs.Position = 0;

            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString().ToLower();
        }

    
        static void GenerateFbs()
        {
            List<string> filelist = FindFile("../Share/xls/");

            foreach (var filename in filelist)
            {
                if (filename.EndsWith(".xlsx"))
                {
                    Debug.Log(string.Format("不支持 xlsx {0}", filename));
                    continue;
                }

                string xlsFile = filename;
                string xlsFileMd5 = filename + ".MD5";

                using (FileStream xls = new FileStream(xlsFile, FileMode.Open, FileAccess.Read,FileShare.Read))
                {
               
                    string oldmd5 =  File.Exists(xlsFileMd5)?System.IO.File.ReadAllText(xlsFileMd5):"";
                    string newmd5 = GetMd5Hash(xls);

                    if (oldmd5 == newmd5)
                    {
                        Debug.Log(string.Format("xls {0}  未做任何修改", filename));
                        continue;
                    }

                    NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook(xls);
                    if (book == null)
                    {
                        return;
                    }

                    NPOI.SS.UserModel.ISheet sheet = book.GetSheetAt(0);
                    if (sheet == null)
                    {
                        return;
                    }

                    Table table = new Table();
                    table.ParserFrom(sheet, filename);

                    if (!fb.GenerateDesc(table))
                    {
                        Debug.Log(string.Format("xls {0}  严重错误", filename));
                        return;
                    }
                    

                    fb.DumpData(table);

                    book.Close();
                    xls.Close();


                    if (File.Exists(xlsFileMd5))
                    {
                        File.Delete(xlsFileMd5);
                    }

                    Debug.Log(string.Format("xls {0}  转换完成", filename));
                }
            }

            Debug.Log(string.Format("xls 转换完成"));
        }


        static void GenerateFbsData()
        {
            List<string> filelist = FindFile("../Share/xls/");

            foreach (var filename in filelist)
            {

                if (filename.EndsWith(".xlsx"))
                {
                    Debug.Log(string.Format("不支持 xlsx {0}", filename));
                    continue;
                }

                string xlsFile = filename;
                string xlsFileMd5 = filename + ".MD5";

                using (FileStream xls = new FileStream(xlsFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {

                    string oldmd5 = File.Exists(xlsFileMd5) ? System.IO.File.ReadAllText(xlsFileMd5) : "";
                    string newmd5 = GetMd5Hash(xls);

                    if (oldmd5 == newmd5)
                    {
                        Debug.Log(string.Format("xls {0}  未做任何修改", filename));
                        continue;
                    }

                    NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook(xls);
                    if (book == null)
                    {
                        return;
                    }

                    NPOI.SS.UserModel.ISheet sheet = book.GetSheetAt(0);
                    if (sheet == null)
                    {
                        return;
                    }

                    Table table = new Table();
                    table.ParserFrom(sheet, filename);

                    if (!fb.GenerateDesc(table))
                    {
                        Debug.Log(string.Format("xls {0}  严重错误", filename));
                        return;
                    }


                    fb.DumpData(table);

                    book.Close();
                    xls.Close();


                    if (File.Exists(xlsFileMd5))
                    {
                        File.Delete(xlsFileMd5);
                    }
                    //System.IO.File.WriteAllText(xlsFileMd5, newmd5);

                    Debug.Log(string.Format("xls {0}  转换完成", filename));
                }
            }

            Debug.Log(string.Format("xls 转换完成"));
        }
    }
}