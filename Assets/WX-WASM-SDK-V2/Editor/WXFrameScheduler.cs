using System;
using System.Text.RegularExpressions;
using UnityEditor.Build;

namespace WeChatWASM
{
    /// <summary>Retains Unity's logical rate while the WeChat RAF adapter uses a physical step of one.</summary>
    public static class WXFrameScheduler
    {
        private const string Marker = "/* WX logical frame timing */";
        private const string ReplayMarker = "/* WX logical frame replay */";
        private static readonly Regex Entry = new Regex(@"function\s+_emscripten_set_main_loop_timing\s*\(\s*mode\s*,\s*value\s*\)\s*\{", RegexOptions.CultureInvariant);
        private static readonly Regex Replay = new Regex(@"var\s+timingValue\s*=\s*Browser\.mainLoop\.timingValue\s*;", RegexOptions.CultureInvariant);
        private static readonly Regex Raf = new Regex(@"Browser\.mainLoop\.timingValue\s*=\s*1\s*;\s*wx\.setPreferredFramesPerSecond\(\s*60\s*/\s*value\s*\)\s*;", RegexOptions.CultureInvariant);

        public static string Apply(string framework, bool diagnostics)
        {
            if (string.IsNullOrWhiteSpace(framework)) throw new BuildFailedException("[WXFrameScheduler] Empty framework.");
            if (framework.Contains(Marker))
            {
                if (!framework.Contains(ReplayMarker) || !framework.Contains("Browser.mainLoop.wxLogicalTimingValue = value;"))
                    throw new BuildFailedException("[WXFrameScheduler] Incomplete logical timing repair.");
                return framework;
            }
            if (Entry.Matches(framework).Count != 1 || Replay.Matches(framework).Count != 1 || Raf.Matches(framework).Count != 1)
                throw new BuildFailedException("[WXFrameScheduler] Unknown WeChat loop template; review the scheduler before export.");

            framework = Entry.Replace(framework, match => match.Value + "\n" + Marker + "\nBrowser.mainLoop.wxLogicalTimingValue = value;", 1);
            framework = Replay.Replace(framework,
                "var timingValue = typeof Browser.mainLoop.wxLogicalTimingValue === 'number' ? Browser.mainLoop.wxLogicalTimingValue : Browser.mainLoop.timingValue; " + ReplayMarker, 1);
            if (diagnostics)
                framework = Raf.Replace(framework, match => match.Value +
                    "\nconsole.info('[FrameSchedule] ' + JSON.stringify({mode:Browser.mainLoop.timingMode,logicalValue:Browser.mainLoop.wxLogicalTimingValue,requestedFps:60/value,rafStep:Browser.mainLoop.timingValue}));", 1);
            return framework;
        }
    }
}
