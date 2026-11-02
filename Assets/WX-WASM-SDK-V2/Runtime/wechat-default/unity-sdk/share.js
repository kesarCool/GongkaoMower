import moduleHelper from './module-helper';
import { formatJsonStr } from './utils';
let shareResolve;
const receiver = 'WXShareRequestRunner';
const requestLifetimeMs = 120000;
let pendingShare;
let lifecycleRegistered = false;

function notifyShare(requestId, state, error = '') {
    if (!requestId) return;
    const message = { requestId, state, error };
    console.info('[WXShare]', JSON.stringify(message));
    try {
        GameGlobal.Module.SendMessage(receiver, 'OnShareBridgeState', JSON.stringify(message));
    } catch (e) {
        console.error('[WXShare] Unity 状态回传失败:', e);
    }
}

function readRequestId(query = '') {
    const match = /(?:^|&)mowerShareRequest=([^&]*)/.exec(query);
    return match ? decodeURIComponent(match[1]) : '';
}

function completeShare(request, state, error = '') {
    if (pendingShare !== request) return;
    pendingShare = null;
    notifyShare(request.requestId, state, error);
}

function registerShareLifecycle() {
    if (lifecycleRegistered) return;
    if (typeof wx.onHide !== 'function' || typeof wx.onShow !== 'function') {
        throw new Error('当前环境缺少 onHide/onShow，无法观察分享返回');
    }
    wx.onHide(() => {
        if (pendingShare && !pendingShare.hidden) {
            pendingShare.hidden = true;
            notifyShare(pendingShare.requestId, 'hidden');
        }
    });
    wx.onShow(() => {
        const request = pendingShare;
        if (!request || !request.hidden) return;
        // A hide/show cycle is a return signal, never proof of delivery. Native
        // clients may not distinguish picker cancellation from a completed share.
        if (Date.now() - request.requestedAt >= requestLifetimeMs) {
            completeShare(request, 'failed', '分享返回已超时');
        } else if (request.dispatched) {
            completeShare(request, 'returned');
        } else {
            request.returned = true;
        }
    });
    lifecycleRegistered = true;
}

export default {
    WXShareAppMessage(conf) {
        let requestId = '';
        try {
            const config = formatJsonStr(conf);
            if (!config.imageUrl && !config.imageUrlId) config.imageUrlId = pickShareImageId();
            requestId = readRequestId(config.query);
            if (typeof wx.shareAppMessage !== 'function') throw new Error('当前环境不支持 shareAppMessage');
            if (requestId) {
                registerShareLifecycle();
                if (pendingShare) completeShare(pendingShare, 'failed', '已被新分享请求替换');
                pendingShare = { requestId, requestedAt: Date.now(), hidden: false, dispatched: false };
            }
            const request = pendingShare;
            wx.shareAppMessage({
                ...config,
                // Some old clients expose failures. These callbacks are only
                // negative signals; success must never be interpreted as delivery.
                fail(res) {
                    if (!requestId || !request) return;
                    const error = res && res.errMsg ? res.errMsg : '分享调用失败';
                    completeShare(request, /cancel/i.test(error) ? 'cancelled' : 'failed', error);
                },
            });
            if (requestId && pendingShare === request) {
                request.dispatched = true;
                notifyShare(requestId, 'dispatched');
                if (request.returned) completeShare(request, 'returned');
            }
        } catch (e) {
            console.error('[WXShare] 原生分享调用失败:', e);
            if (pendingShare && pendingShare.requestId === requestId) pendingShare = null;
            notifyShare(requestId, 'failed', e && e.message ? e.message : String(e));
        }
    },
    WXOnShareAppMessage(conf, isPromise) {
        wx.onShareAppMessage(() => ({
            ...formatJsonStr(conf),
            promise: isPromise
                ? new Promise((resolve) => {
                    shareResolve = resolve;
                    moduleHelper.send('OnShareAppMessageCallback');
                })
                : null,
        }));
    },
    WXOnShareAppMessageResolve(conf) {
        if (shareResolve) {
            shareResolve(formatJsonStr(conf));
        }
    },
};
if (typeof wx.showShareMenu === 'function') {
    wx.showShareMenu({ menus: ['shareAppMessage', 'shareTimeline'] });
}

// ── 被动转发测试：使用后台配置图替代截屏 ──
var __bounceShareImageIds = [];
var __bounceShareFallbackImageIds = [
    'YJNmViJYTqiH4TQ57V3GXA==',
    'y6dKU1EpR/yp3Z5TIxH+gw==',
    'wr335KPGTyaxOsL2ELw9PA==',
    'VdGe7bObQEi2LakmeNWT1g==',
    'o94MHnvkTBSmjOCjHlzV0g==',
    'Y2qxCMabR0KUNfIaNr2IuQ==',
    '2VnfZAzPQD+DLpCYwGccXg==',
    'Az6Vrv8VRX2Sm8fY9eWUeg==',
];

/** C# 侧调用：设置转发图片 ID 池（多个后台素材 ID），每次转发随机取一张。 */
window.SetShareImageIds = function (idsJson) {
    try {
        var ids = JSON.parse(idsJson);
        if (Array.isArray(ids) && ids.length > 0) {
            __bounceShareImageIds = ids;
        }
    } catch (e) {
        console.warn('[Share] SetShareImageIds 解析失败:', e);
    }
};

function pickShareImageId() {
    var imageUrlId = '';
    if (__bounceShareImageIds.length > 0) {
        // 随机取一张
        var idx = Math.floor(Math.random() * __bounceShareImageIds.length);
        imageUrlId = __bounceShareImageIds[idx];
    } else {
        // 兜底：默认后台素材 ID 池随机取一张
        if (__bounceShareFallbackImageIds.length > 0) {
            var idx = Math.floor(Math.random() * __bounceShareFallbackImageIds.length);
            imageUrlId = __bounceShareFallbackImageIds[idx];
        }
    }
    return imageUrlId;
}

if (typeof wx.onShareAppMessage === 'function') {
    wx.onShareAppMessage(function () {
        return { title: '快来一起玩！', imageUrlId: pickShareImageId() };
    });
}
