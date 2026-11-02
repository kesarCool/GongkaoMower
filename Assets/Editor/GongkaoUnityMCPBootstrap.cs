#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

/// <summary>Local editor automation only; does not load, save, or change game assets.</summary>
[InitializeOnLoad]
internal static class GongkaoUnityMCPBootstrap
{
    private const string BaseUrl = "http://127.0.0.1:8767";
    private const string EnabledKey = "GongkaoMower.UnityMCP.AutoConnect";
    private static bool connecting;
    private static double retryAfter;
    private static int attempts;

    static GongkaoUnityMCPBootstrap()
    {
        if (!Application.isBatchMode)
            EditorApplication.update += OnEditorReady;
    }

    private static void OnEditorReady()
    {
        if (!EditorPrefs.GetBool(EnabledKey, true) || connecting || attempts >= 3 ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < retryAfter)
            return;
        if (MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http))
        {
            EditorApplication.update -= OnEditorReady;
            return;
        }
        attempts++;
        Connect();
    }

    [MenuItem("工具/Unity 自动化/连接本工程")]
    private static async void Connect()
    {
        if (connecting) return;
        connecting = true;
        try
        {
            EditorPrefs.SetBool(EnabledKey, true);
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
            EditorPrefs.SetString("MCPForUnity.HttpUrl", BaseUrl);
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", false);
            EditorPrefs.SetBool("MCPForUnity.TelemetryDisabled", true);
            EditorPrefs.SetBool("MCPForUnity.Security.AllowLanHttpBind", false);
            EditorPrefs.SetBool("MCPForUnity.SetupCompleted", true);
            Environment.SetEnvironmentVariable("UNITY_MCP_TELEMETRY_ENABLED", "false");
            EditorConfigurationCache.Instance.Refresh();

            string root = Directory.GetParent(Application.dataPath).FullName;
            string cache = Path.Combine(root, "Library", "UnityMCPTools");
            string server = Path.Combine(cache, "server-venv", "Scripts", "mcp-for-unity.exe");
            if (!await IsHealthy())
            {
                if (!File.Exists(server))
                    throw new FileNotFoundException("请先运行 Tools/UnityMCP/Install-Dependencies.ps1", server);
                Directory.CreateDirectory(Path.Combine(cache, "logs"));
                var start = new System.Diagnostics.ProcessStartInfo(server)
                {
                    Arguments = "--transport http --http-url " + BaseUrl +
                        " --default-instance GongkaoMower --pidfile \"" + Path.Combine(cache, "server.pid") + "\"",
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                start.EnvironmentVariables["UNITY_MCP_TELEMETRY_ENABLED"] = "false";
                start.EnvironmentVariables["UNITY_MCP_LOG_DIR"] = Path.Combine(cache, "logs");
                System.Diagnostics.Process.Start(start);
                for (int i = 0; i < 30 && !await IsHealthy(); i++)
                    await Task.Delay(500);
            }
            if (!await IsHealthy()) throw new InvalidOperationException("本机 MCP 服务尚未就绪。");
            bool connected = await MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
            var state = MCPServiceLocator.TransportManager.GetState(TransportMode.Http);
            File.WriteAllText(Path.Combine(cache, "editor-connection.json"), JsonUtility.ToJson(new ConnectionReport
            {
                connected = connected,
                project = root,
                unityVersion = Application.unityVersion,
                endpoint = BaseUrl + "/mcp",
                error = state.Error,
                utc = DateTime.UtcNow.ToString("O")
            }, true));
            if (connected) Debug.Log("[Unity 自动化] 本工程已连接 " + BaseUrl + "/mcp");
            else Debug.LogWarning("[Unity 自动化] 连接未完成：" + state.Error);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Unity 自动化] " + ex.Message);
        }
        finally
        {
            connecting = false;
            retryAfter = EditorApplication.timeSinceStartup + 10;
        }
    }

    private static async Task<bool> IsHealthy()
    {
        try
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) })
            {
                string body = await client.GetStringAsync(BaseUrl + "/health");
                return body.Contains("healthy") && body.Contains("MCP for Unity");
            }
        }
        catch { return false; }
    }

    [MenuItem("工具/Unity 自动化/暂停自动连接")]
    private static async void Disconnect()
    {
        EditorPrefs.SetBool(EnabledKey, false);
        await MCPServiceLocator.TransportManager.StopAsync(TransportMode.Http);
        Debug.Log("[Unity 自动化] 已暂停自动连接。可通过菜单重新连接。");
    }

    [Serializable]
    private class ConnectionReport
    {
        public bool connected;
        public string project, unityVersion, endpoint, error, utc;
    }
}
#endif
