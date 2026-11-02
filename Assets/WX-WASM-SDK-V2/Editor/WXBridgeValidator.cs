using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Build;
using UnityEngine;

namespace WeChatWASM
{
    /// <summary>Checks the emitted framework, including an export that reuses an old WebGL build.</summary>
    public static class WXBridgeValidator
    {
        // Keep the generic entries in sync with Tools/WeChatSdk/bridge-signatures.json.
        // SDK APIs share these imports, so one missing import can break several unrelated game features.
        // An absent function is allowed: Emscripten may strip imports that the game does not use.
        private static readonly HashSet<string> AuditedBridges = new HashSet<string>(new[]
        {
            "WXInitializeSDK", "WX_SetPreferredFramesPerSecond", "WXIsCloudTest",
            // InitSDK registers these even when the game never uses their features.
            "WX_RegisterOnTouchStartCallback", "WX_RegisterOnTouchEndCallback",
            "WX_RegisterOnTouchMoveCallback", "WX_RegisterOnTouchCancelCallback",
            "WX_RegisterTCPSocketOnMessageCallback", "WX_RegisterUDPSocketOnMessageCallback",
            "WX_RegisterOnBLECharacteristicValueChangeCallback",
            "WX_RegisterStartGyroscopeCallback", "WX_RegisterStopGyroscopeCallback",
            "WX_RegisterOnGyroscopeChangeCallback", "JSStartGameDataMonitor", "JSReportUnityProfileData",
            "WX_OneWayFunction", "WX_OneWayNoFunction_v", "WX_OneWayNoFunction_vs",
            "WX_OneWayNoFunction_vt", "WX_OneWayNoFunction_vst", "WX_OneWayNoFunction_vsn",
            "WX_OneWayNoFunction_vnns", "WX_OnEventRegister", "WX_OffEventRegister",
            "WX_SyncFunction_bs", "WX_SyncFunction_t", "WX_SyncFunction_tt", "WX_SyncFunction_b",
            "WX_SyncFunction_bsnn", "WX_SyncFunction_bt", "WX_SyncFunction_nt",
            "WX_SyncFunction_ss", "WX_SyncFunction_tnn",
            "WX_ClassFunction", "WX_ClassConstructor", "WX_ClassSetProperty",
            "WX_ClassOnEventFunction", "WX_ClassOffEventFunction", "WX_ClassOneWayFunction",
            "WX_ClassOneWayNoFunction_v", "WX_ClassOneWayNoFunction_vs", "WX_ClassOneWayNoFunction_vn",
            "WX_ClassOneWayNoFunction_t", "WX_ClassOneWayNoFunction_vt",
            "WX_OnAddToFavorites", "WX_OffAddToFavorites", "WX_OnAddToFavorites_Resolve",
            "WX_OnCopyUrl", "WX_OffCopyUrl", "WX_OnCopyUrl_Resolve",
            "WX_OnHandoff", "WX_OffHandoff", "WX_OnHandoff_Resolve",
            "WX_OnShareTimeline", "WX_OffShareTimeline", "WX_OnShareTimeline_Resolve",
            "WX_OnGameLiveStateChange", "WX_OffGameLiveStateChange", "WX_OnGameLiveStateChange_Resolve",
            // Previously repaired direct bridges remain part of the export contract.
            "WXCreateBannerAd", "WXCreateFixedBottomMiddleBannerAd", "WXCreateRewardedVideoAd",
            "WXCreateInterstitialAd", "WXCreateCustomAd", "WXShowAd", "WXShowAd2", "WXHideAd",
            "WXADLoad", "WXADDestroy", "WXADStyleChange", "WXADGetStyleValue", "WXReportShareBehavior",
            "SetShareImageIds",
            "WXStorageDeleteAllSync", "WXStorageDeleteKeySync", "WXStorageGetFloatSync",
            "WXStorageGetIntSync", "WXStorageGetStringSync", "WXStorageHasKeySync",
            "WXStorageSetFloatSync", "WXStorageSetIntSync", "WXStorageSetStringSync",
            "WXCreateInnerAudioContext", "WXInnerAudioContextAddListener", "WXInnerAudioContextDestroy",
            "WXInnerAudioContextGetBool", "WXInnerAudioContextGetFloat", "WXInnerAudioContextPause",
            "WXInnerAudioContextPlay", "WXInnerAudioContextRemoveListener", "WXInnerAudioContextSeek",
            "WXInnerAudioContextSetBool", "WXInnerAudioContextSetFloat", "WXInnerAudioContextSetString",
            "WXInnerAudioContextStop", "WXPreDownloadAudios"
        }, StringComparer.Ordinal);

        private static readonly Regex MissingFunction = new Regex(
            "[\"']missing\\s+function\\s*:\\s*(?<symbol>[A-Za-z_$][A-Za-z0-9_$]*)\\s*[\"']",
            RegexOptions.CultureInvariant);

        // Emscripten emits declarations; assignments also support optimized/minified framework fixtures.
        private static readonly Regex FunctionStart = new Regex(
            @"\bfunction\s+(?<name>[A-Za-z_$][A-Za-z0-9_$]*)\s*\([^)]*\)\s*\{|" +
            @"\b(?<name>[A-Za-z_$][A-Za-z0-9_$]*)\s*=\s*function\s*\([^)]*\)\s*\{",
            RegexOptions.CultureInvariant);

        private static readonly Regex ConstantReturn = new Regex(
            @"^\s*;*\s*(?:return(?:\s+(?:undefined|void\s+0|null|false|true|[-+]?\d+(?:\.\d+)?|" +
            "\"\"|''" + @"))?\s*;?\s*)?$", RegexOptions.CultureInvariant);

        private static readonly Regex AbortPlaceholder = new Regex(
            @"\b(?:abort|_abort)\s*\(\s*-\s*1\s*\)", RegexOptions.CultureInvariant);

        public static string[] GetAuditedBridges()
        {
            return AuditedBridges.OrderBy(symbol => symbol, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Returns unresolved Emscripten import names, independent of whitespace or minified JS names.</summary>
        public static string[] FindMissingBridges(string frameworkText)
        {
            if (frameworkText == null) throw new ArgumentNullException("frameworkText");
            return MissingFunction.Matches(frameworkText).Cast<Match>()
                .Select(match => match.Groups["symbol"].Value).Distinct(StringComparer.Ordinal)
                .OrderBy(symbol => symbol, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Call after storage/audio repair and before writing the converted framework.</summary>
        public static void ValidateFramework(string frameworkText)
        {
            if (string.IsNullOrWhiteSpace(frameworkText))
                throw new BuildFailedException("[WXBridgeValidator] The exported framework is empty.");

            string[] missing = FindMissingBridges(frameworkText);
            var failures = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string symbol in missing)
                if (AuditedBridges.Contains(symbol)) failures.Add(symbol + " (missing function)");

            foreach (Match match in FunctionStart.Matches(frameworkText))
            {
                string symbol = match.Groups["name"].Value.TrimStart('_');
                if (!AuditedBridges.Contains(symbol)) continue;
                int start = match.Index + match.Length;
                int end = FindClosingBrace(frameworkText, start);
                if (end < 0)
                {
                    failures.Add(symbol + " (incomplete function)");
                    continue;
                }
                string body = RemoveComments(frameworkText.Substring(start, end - start));
                if (ConstantReturn.IsMatch(body)) failures.Add(symbol + " (empty/constant implementation)");
                if (AbortPlaceholder.IsMatch(body)) failures.Add(symbol + " (abort placeholder)");
            }

            if (failures.Count > 0)
                throw new BuildFailedException("[WXBridgeValidator] SDK bridge validation failed:\n  " +
                    string.Join("\n  ", failures.ToArray()) +
                    "\nRebuild WebGL with the current SDK .jslib plugins, then export again. " +
                    "Export-only reuses the previous WebGL framework and cannot repair its missing generic imports. " +
                    "Do not replace these bridges with empty functions.");

            string[] optional = missing.Where(symbol => !AuditedBridges.Contains(symbol)).ToArray();
            if (optional.Length > 0)
                Debug.LogWarning("[WXBridgeValidator] " + optional.Length +
                    " unaudited SDK imports still have missing-function placeholders: " + string.Join(", ", optional) +
                    ". Before using a new SDK API, audit its P/Invoke and JavaScript ABI, supply its bridge, " +
                    "and add the import to WXBridgeValidator and bridge-signatures.json. " +
                    "Optional SDK imports alone do not fail this build.");
        }

        private static int FindClosingBrace(string text, int start)
        {
            int depth = 1;
            for (int i = start; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\'' || ch == '"' || ch == '`') { i = SkipString(text, i); continue; }
                if (ch == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*'))
                { i = SkipComment(text, i); continue; }
                if (ch == '{') depth++;
                else if (ch == '}' && --depth == 0) return i;
            }
            return -1;
        }

        private static string RemoveComments(string text)
        {
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\'' || ch == '"' || ch == '`')
                {
                    int end = SkipString(text, i);
                    result.Append(text, i, end - i + 1);
                    i = end;
                }
                else if (ch == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*'))
                { i = SkipComment(text, i); result.Append(' '); }
                else result.Append(ch);
            }
            return result.ToString();
        }

        private static int SkipString(string text, int start)
        {
            char quote = text[start];
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '\\') i++;
                else if (text[i] == quote) return i;
            }
            return text.Length - 1;
        }

        private static int SkipComment(string text, int start)
        {
            if (text[start + 1] == '/')
            {
                int end = text.IndexOf('\n', start + 2);
                return end < 0 ? text.Length - 1 : end;
            }
            int closing = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
            return closing < 0 ? text.Length - 1 : closing + 1;
        }
    }
}
