using System;
using System.Diagnostics;
using System.IO;

namespace xls
{
    public class cmd
    {
        public static int ProcessCommand(string command, string argument)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(command);
            info.Arguments = argument;
            info.CreateNoWindow = true;
            info.ErrorDialog = true;
            info.UseShellExecute = true;

            if (info.UseShellExecute)
            {
                info.RedirectStandardOutput = false;
                info.RedirectStandardError = false;
                info.RedirectStandardInput = false;
            }
            else
            {
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.RedirectStandardInput = true;
                info.StandardOutputEncoding = System.Text.UTF8Encoding.UTF8;
                info.StandardErrorEncoding = System.Text.UTF8Encoding.UTF8;
            }

            System.Diagnostics.Process process = System.Diagnostics.Process.Start(info);

            if (!info.UseShellExecute)
            {
//                Debug.Log(process.StandardOutput);
//                Debug.Log(process.StandardError);
            }

            process.WaitForExit();
            int code =   process.ExitCode;
            process.Close();

            return code;
        }

        public static bool Run(string exe,string args)
        {

            bool result = false;
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.UseShellExecute = false;    //是否使用操作系统shell启动
                    p.StartInfo.RedirectStandardInput = true;//接受来自调用程序的输入信息
                    p.StartInfo.RedirectStandardOutput = true;//由调用程序获取输出信息
                    p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
                    p.StartInfo.CreateNoWindow = true;//不显示程序窗口
                    p.Start();//启动程序

                    //向cmd窗口发送输入信息
                    p.StandardInput.WriteLine(exe + "  " +args + "&exit");

                    p.StandardInput.AutoFlush = true;

                    //获取cmd窗口的输出信息
                    string output = p.StandardOutput.ReadToEnd();
                    string str = "";
                    StreamReader reader = p.StandardOutput;
                    string line=reader.ReadLine();
                    while (!reader.EndOfStream)
                    {
                        str += line + "  ";
                        line = reader.ReadLine();
                    }

                    p.WaitForExit();//等待程序执行完退出进程

                    if (p.ExitCode != 0)
                    {
                        Console.WriteLine(output);
                    }

                    p.Close();

                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Exception {0} ", e.ToString());
                Console.WriteLine("error {0} {1}", exe, args);
            }
            return true;
        }
    }
}