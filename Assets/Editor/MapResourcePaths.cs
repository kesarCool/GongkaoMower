/// <summary>Canonical imported map art locations; final maps remain in Resources/Map.</summary>
public static class MapResourcePaths
{
    public const string Styles = "Assets/Res/Map/Styles";
    public const string SharedCartoon = Styles + "/SharedCartoon";
    public const string AutumnAcademy = Styles + "/AutumnAcademy";
    public const string Common = Styles + "/Common";
    public const string RuntimeMaps = "Assets/Resources/Map";

    public static string BambooMap(int stage)
    {
        if (stage < 1 || stage > 8) throw new System.ArgumentOutOfRangeException("stage");
        return RuntimeMaps + "/ZlMap" + stage + ".prefab";
    }

    public static string[] BambooFiles()
    {
        var files = new string[16];
        for (int stage = 1; stage <= 8; stage++)
        {
            files[(stage - 1) * 2] = BambooMap(stage);
            files[(stage - 1) * 2 + 1] = BambooMap(stage) + ".meta";
        }
        return files;
    }
}
