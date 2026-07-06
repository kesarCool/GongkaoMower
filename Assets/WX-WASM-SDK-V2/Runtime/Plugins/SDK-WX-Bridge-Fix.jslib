var WXBridgeFixLibrary = {
  // ── 原有占位 ──
  SetUnityUIType: function() {
  },
  WXHideLoadingPage: function() {
  },
  WXGetFontRawData: function() {
  },
  WXShareFontBuffer: function() {
  },

  // ── 广告 API：把 C# [DllImport("__Internal__")] 桥接到 window.WXWASMSDK.* ──
  // 对应 JS 端实现：wechat-default/unity-sdk/ad.js

  WXCreateBannerAd: function (confPtr) {
    var conf = UTF8ToString(confPtr);
    var key = window.WXWASMSDK.WXCreateBannerAd(conf);
    var bufferSize = lengthBytesUTF8(key || "") + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(key || "", buffer, bufferSize);
    return buffer;
  },

  WXCreateFixedBottomMiddleBannerAd: function (adUnitIdPtr, adIntervals, height) {
    var adUnitId = UTF8ToString(adUnitIdPtr);
    var key = window.WXWASMSDK.WXCreateFixedBottomMiddleBannerAd(adUnitId, adIntervals, height);
    var bufferSize = lengthBytesUTF8(key || "") + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(key || "", buffer, bufferSize);
    return buffer;
  },

  WXCreateRewardedVideoAd: function (confPtr) {
    var conf = UTF8ToString(confPtr);
    var key = window.WXWASMSDK.WXCreateRewardedVideoAd(conf);
    var bufferSize = lengthBytesUTF8(key || "") + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(key || "", buffer, bufferSize);
    return buffer;
  },

  WXCreateInterstitialAd: function (confPtr) {
    var conf = UTF8ToString(confPtr);
    var key = window.WXWASMSDK.WXCreateInterstitialAd(conf);
    var bufferSize = lengthBytesUTF8(key || "") + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(key || "", buffer, bufferSize);
    return buffer;
  },

  WXCreateCustomAd: function (confPtr) {
    var conf = UTF8ToString(confPtr);
    var key = window.WXWASMSDK.WXCreateCustomAd(conf);
    var bufferSize = lengthBytesUTF8(key || "") + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(key || "", buffer, bufferSize);
    return buffer;
  },

  WXShowAd: function (idPtr, succPtr, failPtr) {
    var id = UTF8ToString(idPtr);
    var succ = UTF8ToString(succPtr);
    var fail = UTF8ToString(failPtr);
    window.WXWASMSDK.WXShowAd(id, succ, fail);
  },

  WXShowAd2: function (idPtr, branchIdPtr, branchDimPtr, succPtr, failPtr) {
    var id = UTF8ToString(idPtr);
    var branchId = UTF8ToString(branchIdPtr);
    var branchDim = UTF8ToString(branchDimPtr);
    var succ = UTF8ToString(succPtr);
    var fail = UTF8ToString(failPtr);
    window.WXWASMSDK.WXShowAd2(id, branchId, branchDim, succ, fail);
  },

  WXHideAd: function (idPtr, succPtr, failPtr) {
    var id = UTF8ToString(idPtr);
    var succ = UTF8ToString(succPtr);
    var fail = UTF8ToString(failPtr);
    window.WXWASMSDK.WXHideAd(id, succ, fail);
  },

  WXADLoad: function (idPtr, succPtr, failPtr) {
    var id = UTF8ToString(idPtr);
    var succ = UTF8ToString(succPtr);
    var fail = UTF8ToString(failPtr);
    window.WXWASMSDK.WXADLoad(id, succ, fail);
  },

  WXADDestroy: function (idPtr) {
    var id = UTF8ToString(idPtr);
    window.WXWASMSDK.WXADDestroy(id);
  },

  WXADStyleChange: function (idPtr, keyPtr, value) {
    var id = UTF8ToString(idPtr);
    var key = UTF8ToString(keyPtr);
    window.WXWASMSDK.WXADStyleChange(id, key, value);
  },

  WXADGetStyleValue: function (idPtr, keyPtr) {
    var id = UTF8ToString(idPtr);
    var key = UTF8ToString(keyPtr);
    var val = window.WXWASMSDK.WXADGetStyleValue(id, key);
    if (typeof val === "string") {
      var bufferSize = lengthBytesUTF8(val) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(val, buffer, bufferSize);
      return buffer;
    }
    return val || -1;
  },

  WXReportShareBehavior: function (idPtr, confPtr) {
    var id = UTF8ToString(idPtr);
    var conf = UTF8ToString(confPtr);
    var res = window.WXWASMSDK.WXReportShareBehavior(id, conf);
    var bufferSize = lengthBytesUTF8(res || "{}") + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(res || "{}", buffer, bufferSize);
    return buffer;
  },

  // ── 分享图配置：传入 JSON 字符串数组（如 '["id1","id2","id3"]'）──
  SetShareImageIds: function (idsJsonPtr) {
    var idsJson = UTF8ToString(idsJsonPtr);
    if (typeof window.SetShareImageIds === 'function') {
      window.SetShareImageIds(idsJson);
    }
  },
};

mergeInto(LibraryManager.library, WXBridgeFixLibrary);
