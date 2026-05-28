var MsgSecCheckLibrary = {
  $msgSecCheckManager: {
    callbackPtr: 0,
  },

  MsgSecCheckInit: function (callbackPtr) {
    msgSecCheckManager.callbackPtr = callbackPtr;
  },

  MsgSecCheckPerform: function (lexiconId, textPtr) {
    if (typeof wx === "undefined" || typeof wx.security === "undefined" || typeof wx.security.msgSecCheck === "undefined") {
      if (msgSecCheckManager.callbackPtr) {
        Module.dynCall_vii(msgSecCheckManager.callbackPtr, lexiconId, -1);
      }
      return;
    }
    var text = UTF8ToString(textPtr);
    wx.security.msgSecCheck({
      content: text,
      success: function (res) {
        if (msgSecCheckManager.callbackPtr) {
          Module.dynCall_vii(msgSecCheckManager.callbackPtr, lexiconId, 1);
        }
      },
      fail: function (err) {
        if (msgSecCheckManager.callbackPtr) {
          var errCode = (err && err.errCode) ? -err.errCode : 0;
          Module.dynCall_vii(msgSecCheckManager.callbackPtr, lexiconId, errCode);
        }
      },
    });
  },

  MsgSecCheckFinalize: function () {
    msgSecCheckManager.callbackPtr = 0;
  },
};

autoAddDeps(MsgSecCheckLibrary, "$msgSecCheckManager");
mergeInto(LibraryManager.library, MsgSecCheckLibrary);
