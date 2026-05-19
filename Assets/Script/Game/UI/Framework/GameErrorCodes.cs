/// <summary>
/// 错误码字符串常量，与配表 <c>ErrorCodeTable.ErrorCode</c> 列一致（c错误码c.xls）。
/// </summary>
public static class GameErrorCodes
{
    // Shell / 选关
    public const string UiManagerMissing = "ERR_UI_MANAGER_MISSING";
    public const string UiPanelNotRegistered = "ERR_UI_PANEL_NOT_REGISTERED";
    public const string TableManagerMissing = "ERR_TABLE_MANAGER_MISSING";
    public const string LevelSelectListNotConfigured = "ERR_LEVEL_SELECT_LIST_NOT_CONFIGURED";
    public const string LevelNotSelected = "ERR_LEVEL_NOT_SELECTED";
    public const string LevelLocked = "ERR_LEVEL_LOCKED";
    public const string LevelNoContext = "ERR_LEVEL_NO_CONTEXT";
    public const string LevelNoNext = "ERR_LEVEL_NO_NEXT";

    // 功能 / 登录
    public const string FeatureNotImplemented = "ERR_FEATURE_NOT_IMPLEMENTED";
    public const string LoginSceneNameEmpty = "ERR_LOGIN_SCENE_NAME_EMPTY";

    // 战斗 / 结算
    public const string LevelWaveTableMissing = "ERR_LEVEL_WAVE_TABLE_MISSING";
    public const string LevelWaveConfigMissing = "ERR_LEVEL_WAVE_CONFIG_MISSING";
    public const string BattleSpawnerMissing = "ERR_BATTLE_SPAWNER_MISSING";
    public const string UiResultPanelMissing = "ERR_UI_RESULT_PANEL_MISSING";
    public const string UiRevivePanelMissing = "ERR_UI_REVIVE_PANEL_MISSING";
    public const string CardPanelNotRegistered = "ERR_CARD_PANEL_NOT_REGISTERED";
    public const string PlayerSkillsMissing = "ERR_PLAYER_SKILLS_MISSING";

    // 存档 / 表
    public const string SaveLoadFailed = "ERR_SAVE_LOAD_FAILED";
    public const string TableFileMissing = "ERR_TABLE_FILE_MISSING";
}
