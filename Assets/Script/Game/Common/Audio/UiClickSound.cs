/// <summary>壳层 / 局内 UI 按钮音效。</summary>
public static class UiClickSound
{
    public static void Play() => AudioService.Ensure().PlayUiClick();

    public static void PlayClose() => AudioService.Ensure().PlayUiClose();

    public static void PlaySwitch() => AudioService.Ensure().PlayUiSwitch();
}
