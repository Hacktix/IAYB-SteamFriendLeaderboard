using Progress;

namespace SteamFriendLeaderboard;

public static class GameUtil
{
    /// <summary>
    /// Returns a leaderboard server-compatible string representing an ID of the given stage.
    /// </summary>
    public static string GetStageId(LevelInformation info)
    {
        int id = info.GetLevelNumber();
        string category = info.GetLevelCategoryName();
        return $"{category}_{id}";
    }
    
    /// <summary>
    /// Returns a leaderboard server-compatible string representing an ID of the given stage.
    /// </summary>
    public static string GetStageId(LevelData info)
    {
        int id = info.GetID();
        string category = info.GetCategory();
        return $"{category}_{id}";
    }

    /// <summary>
    /// Returns a cropped username if it's longer than the configured maximum display value, full name otherwise.
    /// </summary>
    public static string CropUsername(string name)
    {
        if(name.Length > Plugin.leaderboardMaxNameDisplayLength.Value)
            name = name.Substring(0, Plugin.leaderboardMaxNameDisplayLength.Value) + "...";
        return name;
    }
}