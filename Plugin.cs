using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Progress;
using TMPro;
using UnityEngine;

namespace SteamFriendLeaderboard;

[BepInPlugin("dev.hacktix.steamfriendleaderboard", "Steam Friend Leaderboard", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    #region Configuration
    
    internal static ConfigEntry<string> leaderboardApiHostname;
    internal static ConfigEntry<int> leaderboardApiPort;
    internal static ConfigEntry<string> leaderboardApiBasePath;
    internal static ConfigEntry<bool> leaderboardApiHTTPS;
    
    internal static ConfigEntry<int> leaderboardMaxNameDisplayLength;
        
    /// <summary>
    /// Initializer Method for the Plugin, which applies Harmony patches and handles config stuff.
    /// </summary>
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Initializing Steam Friend Leaderboard...");
        
        Harmony.CreateAndPatchAll(typeof(Plugin));
        Logger.LogInfo($"Steam Friend Leaderboard Patches applied.");

        leaderboardApiHostname = Config.Bind(
            "Network",
            "LeaderboardApiHostname",
            "citadel.ix.tc",
            "Hostname to connect to for the leaderboard API."
        );
        leaderboardApiPort = Config.Bind(
            "Network",
            "LeaderboardApiPort",
            6969,
            "Port to connect to for the leaderboard API."
        );
        leaderboardApiBasePath = Config.Bind(
            "Network",
            "LeaderboardApiBasePath",
            "/iayb/v1/",
            "Base path to leaderboard API."
        );
        leaderboardApiHTTPS = Config.Bind(
            "Network",
            "LeaderboardApiHTTPS",
            true,
            "Whether to use HTTPS when connection to the leaderboard API."
        );

        leaderboardMaxNameDisplayLength = Config.Bind(
            "Interface",
            "LeaderboardMaxNameDisplayLength",
            20,
            "The maximum number of characters of player names that should be shown in the leaderboard UI before they are cut off."
        );
    }

    #endregion

    #region Steam Initialization
    
    /// <summary>
    /// Called after the GameManager has finished initializing (which is necessary, as it also initializes the
    /// Steamworks API). Initializes the SteamUtil class, which requests a Steam Session Ticket for later use.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "Initialize")]
    [HarmonyPostfix]
    static void InitializeGame()
    {
        SteamUtil.Init();
    }

    #endregion

    #region Posting Records
    
    /// <summary>
    /// Called anytime the game saves progress. Posts the records for all stages to the leaderboard server.
    /// </summary>
    [HarmonyPatch(typeof(SaveSystem), "SaveAllData")]
    [HarmonyPostfix]
    static void PostTimes(SaveDataGameState ___CurrentSaveState)
    {
        List<LeaderboardServerUtil.PostRecordData> records = new List<LeaderboardServerUtil.PostRecordData>();
        foreach (SaveDataGameSlot slot in ___CurrentSaveState.Slots)
        {
            foreach (LevelData level in slot.GetLevelData())
            {
                if (!level.GetLevelCompleted())
                    continue;
                string id = GameUtil.GetStageId(level);
                double score = level.GetBestTime() == 0f ? level.GetHordeScore() : level.GetBestTime();
                LeaderboardServerUtil.PostRecordData record = new LeaderboardServerUtil.PostRecordData(id, score, level.GetBestTime() == 0f);
                records.Add(record);
            }
        }
        LeaderboardServerUtil.PostRecordRequest req = new LeaderboardServerUtil.PostRecordRequest();
        req.records = records;
        LeaderboardServerUtil.PostRecords(req);
    }

    #endregion

    #region Fetching Records

    /// <summary>
    /// Called whenever the player clicks on a level button in the level selection screen.
    /// Fetches the latest leaderboard data for the selected level (unless it's a cutscene or of "Story" type).
    /// </summary>
    [HarmonyPatch(typeof(UILevelSelectButton), "Select")]
    [HarmonyPostfix]
    static void SelectLevel(UILevelSelectButton __instance)
    {
        if (__instance.GetSceneInformation() is not LevelInformation || (__instance.GetSceneInformation() as LevelInformation).GetLevelType() == LevelInformation.LevelType.Story)
        {
            SetLeaderboardStatus("UNAVAILABLE");
            return;
        }
        
        string stageName = GameUtil.GetStageId(__instance.GetSceneInformation() as LevelInformation);
        Logger.LogDebug($"Selected Level: {stageName}");
        
        List<LeaderboardServerUtil.Record> records = LeaderboardServerUtil.GetRecords(stageName, (__instance.GetSceneInformation() as LevelInformation).GetLevelType() == LevelInformation.LevelType.Horde);
        SetLeaderboardContent(records);
    }

    #endregion

    #region Adjust existing UI

    /// <summary>
    /// Adapts the width of the main level select anchor to be wider, and the level select button list anchor to
    /// be more narrow, in order to fit the leaderboard UI in.
    /// </summary>
    [HarmonyPatch(typeof(UILevelSelectionRoot), "Start")]
    [HarmonyPrefix]
    static void AdjustLevelSelectAnchors()
    {
        GameObject mainAnchor = GameObject.Find("Main Wide Anchor");
        RectTransform mainAnchorRectTransform = mainAnchor.GetComponent<RectTransform>();
        mainAnchorRectTransform.anchorMin = new Vector2(0.4f, 0.5f);
        mainAnchorRectTransform.anchorMax = new Vector2(0.6f, 0.5f);
        
        GameObject scrollAreaAnchor = GameObject.Find("Scroll Area");
        RectTransform scrollAreaAnchorRectTransform = scrollAreaAnchor.GetComponent<RectTransform>();
        scrollAreaAnchorRectTransform.anchorMin = new Vector2(0.4f, 0f);;
    }

    #endregion

    #region Custom Leaderboard UI
    
    private static TextMeshProUGUI leaderboardNamesText;
    private static TextMeshProUGUI leaderboardTimesText;
    private static TextMeshProUGUI leaderboardStatusText;

    /// <summary>
    /// Fully clears all text displayed on the leaderboard pane.
    /// </summary>
    public static void ClearLeaderboardContent()
    {
        leaderboardNamesText.text = "";
        leaderboardTimesText.text = "";
        leaderboardStatusText.text = "";
    }
    
    /// <summary>
    /// Displays the given list of records on the leaderboard pane.
    /// The leaderboard is implicitly cleared before the records are displayed.
    /// </summary>
    public static void SetLeaderboardContent(List<LeaderboardServerUtil.Record> records)
    {
        ClearLeaderboardContent();
        foreach (LeaderboardServerUtil.Record record in records)
        {
            string steamName = SteamUtil.GetDisplayName(record.steamid);
            string formattedName = GameUtil.CropUsername(steamName);
            leaderboardNamesText.text += $"{formattedName}\n";
            
            string formattedTime = record.GetFormattedTime();
            leaderboardTimesText.text += $"{formattedTime}\n";
        }
    }

    /// <summary>
    /// Clears the leaderboard and displays a status message in the center of the leaderboard pane using the default
    /// gray font color.
    /// </summary>
    public static void SetLeaderboardStatus(string text) => SetLeaderboardStatus(text, Color.gray);
    
    /// <summary>
    /// Clears the leaderboard and displays a status message in the center of the leaderboard pane.
    /// </summary>
    public static void SetLeaderboardStatus(string text, Color color)
    {
        ClearLeaderboardContent();
        leaderboardStatusText.text = text;
        leaderboardStatusText.color = color;
    }
    
    /// <summary>
    /// Creates the leaderboard pane within the level selection menu.
    /// </summary>
    [HarmonyPatch(typeof(UILevelSelectionRoot), "Start")]
    [HarmonyPrefix]
    static void CreateLeaderboardUI()
    {
        TMP_FontAsset font = GameObject.Find("Name Text").GetComponent<TextMeshProUGUI>().font;
        
        GameObject leaderboardAnchor = new GameObject("Leaderboard Anchor", typeof(RectTransform));
        RectTransform leaderboardAnchorRectTransform = leaderboardAnchor.GetComponent<RectTransform>();
        leaderboardAnchor.transform.parent = GameObject.Find("Below Anchor").transform;
        leaderboardAnchorRectTransform.anchorMin = new Vector2(0, 0);
        leaderboardAnchorRectTransform.anchorMax = new Vector2(0, 1);
        leaderboardAnchorRectTransform.offsetMin = new Vector2(Screen.width * -0.2f, Screen.height * -0.65f);
        leaderboardAnchorRectTransform.offsetMax = new Vector2(0, 0);
        leaderboardAnchorRectTransform.pivot = new Vector2(0, 1);
        leaderboardAnchorRectTransform.anchoredPosition = new Vector2(0f, 0f);
        
        GameObject leaderboardBacking = Instantiate(GameObject.Find("Backing"), leaderboardAnchor.transform);
        RectTransform leaderboardBackingRectTransform = leaderboardBacking.GetComponent<RectTransform>();
        CanvasRenderer leaderboardBackingRenderer = leaderboardBacking.GetComponent<CanvasRenderer>();
        leaderboardBackingRectTransform.anchorMin = new Vector2(0, 0);
        leaderboardBackingRectTransform.anchorMax = new Vector2(1, 1);
        leaderboardBackingRectTransform.offsetMin = Vector2.zero;
        leaderboardBackingRectTransform.offsetMax = Vector2.zero;
        leaderboardBackingRectTransform.pivot = new Vector2(0.5f, 0.5f);
        leaderboardBackingRenderer.SetColor(Color.black);
        
        GameObject leaderboardHeader = new GameObject("Leaderboard Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        leaderboardHeader.transform.parent = leaderboardAnchor.transform;
        TextMeshProUGUI leaderboardHeaderText = leaderboardHeader.GetComponent<TextMeshProUGUI>();
        RectTransform leaderboardHeaderRectTransform = leaderboardHeader.GetComponent<RectTransform>();
        leaderboardHeaderText.text = "LEADERBOARD";
        leaderboardHeaderText.alignment = TextAlignmentOptions.Top;
        leaderboardHeaderText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        leaderboardHeaderText.font = font;
        leaderboardHeaderRectTransform.anchorMin = new Vector2(0, 0);
        leaderboardHeaderRectTransform.anchorMax = new Vector2(1, 0.975f);
        leaderboardHeaderRectTransform.offsetMin = Vector2.zero;
        leaderboardHeaderRectTransform.offsetMax = Vector2.zero;
        leaderboardHeaderRectTransform.pivot = Vector2.zero;
        
        GameObject leaderboardStatus = new GameObject("Leaderboard Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        leaderboardStatus.transform.parent = leaderboardAnchor.transform;
        leaderboardStatusText = leaderboardStatus.GetComponent<TextMeshProUGUI>();
        RectTransform leaderboardStatusRectTransform = leaderboardStatus.GetComponent<RectTransform>();
        leaderboardStatusText.text = "";
        leaderboardStatusText.alignment = TextAlignmentOptions.Top;
        leaderboardStatusText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        leaderboardStatusText.font = font;
        leaderboardStatusRectTransform.anchorMin = new Vector2(0, 0);
        leaderboardStatusRectTransform.anchorMax = new Vector2(1, 0.8f);
        leaderboardStatusRectTransform.offsetMin = Vector2.zero;
        leaderboardStatusRectTransform.offsetMax = Vector2.zero;
        leaderboardStatusRectTransform.pivot = Vector2.zero;
        
        GameObject leaderboardNames = new GameObject("Leaderboard Names", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        leaderboardNames.transform.parent = leaderboardAnchor.transform;
        leaderboardNamesText = leaderboardNames.GetComponent<TextMeshProUGUI>();
        RectTransform leaderboardNamesRectTransform = leaderboardNames.GetComponent<RectTransform>();
        leaderboardNamesText.alignment = TextAlignmentOptions.TopLeft;
        leaderboardNamesText.horizontalAlignment = HorizontalAlignmentOptions.Left;
        leaderboardNamesText.font = font;
        leaderboardNamesRectTransform.anchorMin = new Vector2(0.05f, 0);
        leaderboardNamesRectTransform.anchorMax = new Vector2(0.95f, 0.9f);
        leaderboardNamesRectTransform.offsetMin = Vector2.zero;
        leaderboardNamesRectTransform.offsetMax = Vector2.zero;
        leaderboardNamesRectTransform.pivot = Vector2.zero;
        
        GameObject leaderboardTimes = new GameObject("Leaderboard Times", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        leaderboardTimes.transform.parent = leaderboardAnchor.transform;
        leaderboardTimesText = leaderboardTimes.GetComponent<TextMeshProUGUI>();
        RectTransform leaderboardTimesRectTransform = leaderboardTimes.GetComponent<RectTransform>();
        leaderboardTimesText.alignment = TextAlignmentOptions.TopRight;
        leaderboardTimesText.horizontalAlignment = HorizontalAlignmentOptions.Right;
        leaderboardTimesText.font = font;
        leaderboardTimesRectTransform.anchorMin = new Vector2(0.05f, 0);
        leaderboardTimesRectTransform.anchorMax = new Vector2(0.95f, 0.9f);
        leaderboardTimesRectTransform.offsetMin = Vector2.zero;
        leaderboardTimesRectTransform.offsetMax = Vector2.zero;
        leaderboardTimesRectTransform.pivot = Vector2.zero;
    }

    #endregion
}
