var WxKeyboardBridge = {
    // 显示微信原生键盘
    WxKeyboard_Show: function(defaultValuePtr, maxLength) {
        var defaultValue = defaultValuePtr ? UTF8ToString(defaultValuePtr) : '';
        if (typeof wx !== 'undefined' && wx.showKeyboard) {
            if (!window.__wxKeyboardInited) {
                window.__wxKeyboardInited = true;
                window.__wxKeyboardValue = '';
                wx.onKeyboardInput(function(res) {
                    window.__wxKeyboardValue = res.value;
                });
                wx.onKeyboardComplete(function(res) {
                    window.__wxKeyboardValue = res.value;
                });
                wx.onKeyboardConfirm(function(res) {
                    window.__wxKeyboardValue = res.value;
                });
            }
            wx.showKeyboard({
                defaultValue: defaultValue,
                maxLength: maxLength || 50,
                confirmType: 'done'
            });
        }
    },
    // 读取键盘当前文本，写入 buffer 并返回字节数
    WxKeyboard_GetValue: function(buffer, bufferSize) {
        var value = window.__wxKeyboardValue || '';
        if (buffer) {
            stringToUTF8(value, buffer, bufferSize);
        }
        return lengthBytesUTF8(value);
    },
    // 隐藏键盘
    WxKeyboard_Hide: function() {
        if (typeof wx !== 'undefined' && wx.hideKeyboard) {
            wx.hideKeyboard({});
        }
    },
    // 清空缓存文本
    WxKeyboard_Clear: function() {
        window.__wxKeyboardValue = '';
    }
};
mergeInto(LibraryManager.library, WxKeyboardBridge);
