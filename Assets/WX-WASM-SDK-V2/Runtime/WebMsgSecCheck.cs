#if WEIXINMINIGAME && TUANJIE_2022_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace WeChatWASM
{
    public struct MsgSecCheckResult
    {
        public int LexiconId;
        public int ResultCode; // 1=pass, 0=flagged, <0=API error (negated errCode)
        public bool IsPass { get { return ResultCode == 1; } }
        public bool IsApiError { get { return ResultCode < 0; } }
    }

    public static class WebMsgSecCheck
    {
        [DllImport("__Internal")]
        private static extern void MsgSecCheckInit(Action<int, int> callback);

        [DllImport("__Internal")]
        private static extern void MsgSecCheckPerform(int lexiconId, string text);

        [DllImport("__Internal")]
        private static extern void MsgSecCheckFinalize();

        private static ConcurrentQueue<MsgSecCheckResult> _resultQueue
            = new ConcurrentQueue<MsgSecCheckResult>();
        private static bool _initialized;

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnMsgSecCheckResult(int lexiconId, int resultCode)
        {
            _resultQueue.Enqueue(new MsgSecCheckResult
            {
                LexiconId = lexiconId,
                ResultCode = resultCode,
            });
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _resultQueue = new ConcurrentQueue<MsgSecCheckResult>();
            MsgSecCheckInit(OnMsgSecCheckResult);
            Debug.Log("[WebMsgSecCheck] Initialized");
        }

        public static void Shutdown()
        {
            _initialized = false;
            MsgSecCheckFinalize();
        }

        public static void Check(int lexiconId, string text)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[WebMsgSecCheck] Not initialized; skipping check");
                _resultQueue.Enqueue(new MsgSecCheckResult
                    { LexiconId = lexiconId, ResultCode = 1 });
                return;
            }
            MsgSecCheckPerform(lexiconId, text ?? string.Empty);
        }

        public static int DrainResults(Action<MsgSecCheckResult> handler)
        {
            int count = 0;
            while (_resultQueue.TryDequeue(out var r))
            {
                handler?.Invoke(r);
                count++;
            }
            return count;
        }

        public static int PendingCount
        {
            get { return _resultQueue != null ? _resultQueue.Count : 0; }
        }
    }
}
#endif
