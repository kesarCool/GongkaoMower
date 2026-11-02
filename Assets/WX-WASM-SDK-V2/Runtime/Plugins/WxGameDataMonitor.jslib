mergeInto(LibraryManager.library, {
    $WXGameDataMonitor: {
        warned: false,
        warn: function(reason) {
            if (WXGameDataMonitor.warned) return;
            WXGameDataMonitor.warned = true;
            console.warn('[WX SDK monitor] Optional performance telemetry unavailable: ' + reason);
        },
        metricsAvailable: function() {
            var metrics = [
                typeof _WXGetEXFrameTime === 'function' ? _WXGetEXFrameTime : null,
                typeof _WXGetTotalMemorySize === 'function' ? _WXGetTotalMemorySize : null,
                typeof _WXGetDynamicMemorySize === 'function' ? _WXGetDynamicMemorySize : null,
                typeof _WXGetUsedMemorySize === 'function' ? _WXGetUsedMemorySize : null,
                typeof _WXGetUnAllocatedMemorySize === 'function' ? _WXGetUnAllocatedMemorySize : null,
                typeof _WXGetBundleNumberInMemory === 'function' ? _WXGetBundleNumberInMemory : null,
                typeof _WXGetBundleNumberOnDisk === 'function' ? _WXGetBundleNumberOnDisk : null,
                typeof _WXGetBundleSizeInMemory === 'function' ? _WXGetBundleSizeInMemory : null,
                typeof _WXGetBundleSizeOnDisk === 'function' ? _WXGetBundleSizeOnDisk : null
            ];
            for (var i = 0; i < metrics.length; i++) {
                // Undefined-symbol stubs are callable functions too. Calling one
                // would abort Wasm rather than produce a catchable C# exception.
                if (!metrics[i] || /missing\s+function|\babort\s*\(\s*-1\s*\)/.test(Function.prototype.toString.call(metrics[i]))) {
                    WXGameDataMonitor.warn('native metric bridges are not installed; monitor skipped');
                    return false;
                }
            }
            return true;
        },
        monitor: function() {
            if (typeof GameGlobal === 'undefined' || !GameGlobal.manager ||
                typeof GameGlobal.manager.getGameDataMonitor !== 'function') {
                WXGameDataMonitor.warn('game data monitor is unavailable');
                return null;
            }
            return GameGlobal.manager.getGameDataMonitor();
        }
    },
    JSStartGameDataMonitor__deps: ['$WXGameDataMonitor'],
    JSStartGameDataMonitor: function() {
        if (typeof Module === 'undefined' || !Module.IsWxGame) return;
        try {
            if (!WXGameDataMonitor.metricsAvailable()) return;
            var monitor = WXGameDataMonitor.monitor();
            if (!monitor || typeof monitor.start !== 'function') {
                WXGameDataMonitor.warn('monitor start is unavailable');
                return;
            }
            monitor.start();
        } catch (error) {
            WXGameDataMonitor.warn(String(error));
        }
    },
    JSReportUnityProfileData__deps: ['$WXGameDataMonitor'],
    JSReportUnityProfileData: function(
        targetFrameRate, // fps.
        monoHeapReserved, monoHeapUsed, nativeReserved, nativeUnused, nativeAllocated, // profiler.
        setPassCalls, drawCalls, vertices, trianglesCount // render.
    ) {
        try {
        if (!WXGameDataMonitor.metricsAvailable()) return;
        var monitor = WXGameDataMonitor.monitor();
        if (!monitor || typeof monitor.reportUnityProfileData !== 'function') {
            WXGameDataMonitor.warn('monitor reporting is unavailable');
            return;
        }
        let report_data = {
            timestamp: new Date().getTime(),
            fps: {
                targetFrameRate: targetFrameRate,
                avgEXFrameTime: _WXGetEXFrameTime(),
            },
            profiler: {
                monoHeapReserved: monoHeapReserved,
                monoHeapUsed: monoHeapUsed,
                nativeReserved: nativeReserved,
                nativeUnused: nativeUnused,
                nativeAllocated: nativeAllocated,
            },
            render: {
                setPassCalls: setPassCalls,
                drawCalls: drawCalls,
                vertices: vertices,
                trianglesCount: trianglesCount,
            },
            webassembly: {
                totalHeapMemory: _WXGetTotalMemorySize(),
                dynamicMemory: _WXGetDynamicMemorySize(),
                usedHeapMemory: _WXGetUsedMemorySize(),
                unAllocatedMemory: _WXGetUnAllocatedMemorySize(),
            },
            assetbundle: {
                numberInMemory: _WXGetBundleNumberInMemory(),
                numberOnDisk: _WXGetBundleNumberOnDisk(),
                sizeInMemory:  _WXGetBundleSizeInMemory(),
                sizeOnDisk: _WXGetBundleSizeOnDisk(),
            }
        }

        monitor.reportUnityProfileData(report_data);
        } catch (error) {
            WXGameDataMonitor.warn(String(error));
        }
    },
});
