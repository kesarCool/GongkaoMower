using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using WeChatWASM;

// Compile the production validator directly, with only these Unity API test doubles.
namespace UnityEditor.Build
{
    public class BuildFailedException : Exception
    {
        public BuildFailedException(string message) : base(message) { }
    }
}
namespace UnityEngine
{
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void LogWarning(object message) { Warnings.Add(message.ToString()); }
    }
}

public static class ExportGuardTests
{
    private static int checks;

    private static void Assert(bool condition, string label)
    {
        checks++;
        if (!condition) throw new Exception(label);
    }

    private static void Accept(string framework, string label)
    {
        WXBridgeValidator.ValidateFramework(framework);
        checks++;
    }

    private static void Reject(string framework, string expected, string label)
    {
        try { WXBridgeValidator.ValidateFramework(framework); }
        catch (UnityEditor.Build.BuildFailedException error)
        {
            Assert(error.Message.Contains(expected), label + ": diagnostic did not name " + expected);
            return;
        }
        throw new Exception(label + ": expected export failure");
    }

    private static string Stub(string symbol)
    {
        return "function _" + symbol + "(){err(\"missing function: " + symbol + "\");abort(-1)}";
    }

    public static int Main(string[] args)
    {
        try
        {
            string root = args[0];
            var audited = new HashSet<string>(WXBridgeValidator.GetAuditedBridges(), StringComparer.Ordinal);
            var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                File.ReadAllText(Path.Combine(root, "Tools/WeChatSdk/bridge-signatures.json")));
            var signatures = (IEnumerable)manifest["signatures"];
            foreach (Dictionary<string, object> signature in signatures)
            {
                string symbol = (string)signature["symbol"];
                Assert(audited.Contains(symbol), "Every repaired ABI must be guarded: " + symbol);
                Reject(Stub(symbol), symbol, "Every repaired import rejects old-export stubs");
                Reject("function _" + symbol + "(){/* placeholder */}", symbol,
                    "Every repaired import rejects no-op bodies");
            }

            Reject(" ", "empty", "Empty framework");
            Reject("function _WX_SyncFunction_t() {\r\n err('missing function: WX_SyncFunction_t');\r\n abort(-1);\r\n}",
                "WX_SyncFunction_t", "Capsule crash with CRLF/single quotes");
            Reject("function a(){err('missing function: WX_OneWayNoFunction_vt');abort(-1)}",
                "WX_OneWayNoFunction_vt", "Minified JS local names do not hide missing imports");
            Reject("var _WX_OnEventRegister=function(a){ };", "WX_OnEventRegister", "Empty assignment");
            Reject("function _WX_OffEventRegister() { // no callback\n /* old } */ return; }",
                "WX_OffEventRegister", "Comments and bare return");
            Reject("function _WXInitializeSDK() { return 0; }", "WXInitializeSDK", "Constant-return placeholder");
            Reject("function _WXInitializeSDK() { return void 0; }", "WXInitializeSDK", "Minified undefined return");
            Reject("function _WX_SyncFunction_t() { abort ( - 1 ); }", "abort placeholder",
                "Optimized abort without missing-function message");
            foreach (string startup in new[] { "WXIsCloudTest", "WX_RegisterOnBLECharacteristicValueChangeCallback",
                "WX_RegisterOnTouchStartCallback", "WX_RegisterOnTouchEndCallback", "WX_RegisterOnTouchMoveCallback",
                "WX_RegisterOnTouchCancelCallback", "WX_RegisterTCPSocketOnMessageCallback",
                "WX_RegisterUDPSocketOnMessageCallback", "WX_RegisterStartGyroscopeCallback",
                "WX_RegisterStopGyroscopeCallback", "WX_RegisterOnGyroscopeChangeCallback", "JSStartGameDataMonitor",
                "JSReportUnityProfileData" })
            {
                Reject(Stub(startup), startup, "Startup dependency must be linked: " + startup);
                Reject("function _" + startup + "(){}", startup, "Startup dependency cannot be a no-op: " + startup);
            }
            Reject("function _WXStorageGetStringSync() {return '';}", "WXStorageGetStringSync", "Storage guard");
            Reject("function _WXInnerAudioContextPlay() {return undefined;}", "WXInnerAudioContextPlay", "Audio guard");
            Reject("function _WX_SyncFunction_t() { if (true) {", "incomplete", "Truncated critical bridge");
            Reject(Stub("WX_OneWayFunction") + Stub("WX_SyncFunction_t"), "WX_OneWayFunction",
                "Failure accumulates names");

            Accept("function _WX_SyncFunction_t(a,b){return window.WXWASMSDK.WX_SyncFunction_t(a,b)}",
                "Minified declaration dispatch");
            Accept("var _WX_SyncFunction_t = function(a,b) { return window.WXWASMSDK.WX_SyncFunction_t(a,b); };",
                "Assignment dispatch");
            Accept("function _WX_OneWayNoFunction_vt(a){var data={message:'}',url:'https://example.test/a/*b*/'};" +
                "/* } */ if(data){window.WXWASMSDK.WX_OneWayNoFunction_vt(a,JSON.stringify(data));}}",
                "Nested braces/comments/string literals");
            Accept("function _WXHideLoadingPage(){}", "Deliberate legacy no-op is outside audited set");
            Accept("function _unrelated(){}", "Unused imports may be stripped");

            UnityEngine.Debug.Warnings.Clear();
            string optional = Stub("WXCameraCreateCamera") + Stub("WX_SyncFunction_tnnt");
            Accept(optional, "Unsupported optional imports do not fail export");
            Assert(UnityEngine.Debug.Warnings.Count == 1, "One actionable optional-import warning");
            Assert(UnityEngine.Debug.Warnings[0].Contains("Before using a new SDK API"), "Warn about new API audit");
            string[] missing = WXBridgeValidator.FindMissingBridges(optional + Stub("WXCameraCreateCamera"));
            Assert(missing.SequenceEqual(new[] { "WXCameraCreateCamera", "WX_SyncFunction_tnnt" }),
                "Public missing-import report is sorted and unique");

            string current = Path.Combine(root, "Build/minigame/minigame/webgl.wasm.framework.unityweb.js");
            if (File.Exists(current))
            {
                string framework = File.ReadAllText(current);
                string critical = WXBridgeValidator.FindMissingBridges(framework).FirstOrDefault(audited.Contains);
                if (critical != null)
                {
                    Reject(framework, critical, "Real previous exported framework");
                    Console.WriteLine("Verified rejection of existing stale export: " + critical);
                }
                else
                {
                    Accept(framework, "Current real exported framework");
                    Console.WriteLine("Verified current real export: no audited bridge placeholders or empty implementations.");
                }
            }

            string genericBridge = Path.Combine(root, "Assets/WX-WASM-SDK-V2/Runtime/Plugins/SDK-WX-Generic.jslib");
            if (File.Exists(genericBridge))
            {
                string bridgeSource = File.ReadAllText(genericBridge);
                foreach (Match match in Regex.Matches(bridgeSource, @"\b(?<name>WX\w*)\s*:\s*function\b"))
                    Assert(audited.Contains(match.Groups["name"].Value), "Generic .jslib coverage: " + match.Value);
                string emittedBodies = Regex.Replace(bridgeSource,
                    @"\b(?<name>WX\w*)\s*:\s*function\s*\(", "function _${name}(");
                Accept(emittedBodies, "All current generic bridge implementation bodies pass the guard");
            }
            Console.WriteLine("PASS: " + checks + " export guard assertions.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
