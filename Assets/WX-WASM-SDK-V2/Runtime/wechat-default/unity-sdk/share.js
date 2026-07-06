import moduleHelper from './module-helper';
import { formatJsonStr } from './utils';
let shareResolve;
export default {
    WXShareAppMessage(conf) {
        wx.shareAppMessage({
            ...formatJsonStr(conf),
        });
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
wx.showShareMenu({
    menus: ['shareAppMessage', 'shareTimeline'],
});

// ── 被动转发测试：使用后台配置图替代截屏 ──
var __bounceShareImageIds = [];
var __bounceShareFallbackImageIds = [
    'YJNmViJYTqiH4TQ57V3GXA==',
    'y6dKU1EpR/yp3Z5TIxH+gw==',
    'wr335KPGTyaxOsL2ELw9PA==',
    'VdGe7bObQEi2LakmeNWT1g==',
    'o94MHnvkTBSmjOCjHlzV0g==',
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

wx.onShareAppMessage(function () {
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
    return {
        title: '快来一起玩！',
        imageUrlId: imageUrlId,
    };
});
