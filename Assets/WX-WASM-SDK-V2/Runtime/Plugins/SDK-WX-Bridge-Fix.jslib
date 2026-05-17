var WXBridgeFixLibrary = {
  SetUnityUIType: function() {
  },
  WXHideLoadingPage: function() {
    // Called by CheckFrame.Update -> WXSDKManagerHandler.HideLoadingPage
  },
};

mergeInto(LibraryManager.library, WXBridgeFixLibrary);
