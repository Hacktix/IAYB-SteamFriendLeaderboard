using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Steamworks;

namespace SteamFriendLeaderboard;

public static class LeaderboardServerUtil
{
    /// <summary>
    /// Record class representing data received from the leaderboard server when querying.
    /// </summary>
    public class Record
    {
        public ulong steamid;
        public double time;

        public string GetFormattedTime() => $"{Math.Round(time * 100.0) / 100.0}";
    }

    /// <summary>
    /// Record class representing data sent to the leaderboard server when posting records.
    /// </summary>
    public class PostRecordData
    {
        public string stageid;
        public double time;
        public bool horde;

        public PostRecordData(string stageid, double time, bool horde)
        {
            this.stageid = stageid;
            this.time = time;
            this.horde = horde;
        }
    }

    /// <summary>
    /// Class representing the body of the POST request made to the leaderboard server when posting records.
    /// </summary>
    public class PostRecordRequest
    {
        public string ticket;
        public List<PostRecordData> records;
    }

    /// <summary>
    /// Returns a properly formatted URL leading to the given leaderboard API endpoint, taking into account potential
    /// inconsistencies in the user configuration.
    /// </summary>
    private static string getRequestUrl(string endpoint)
    {
        string protocol = Plugin.leaderboardApiHTTPS.Value ? "https" : "http";
        string hostname = $"{Plugin.leaderboardApiHostname.Value}:{Plugin.leaderboardApiPort.Value}";
        string path = $"{Plugin.leaderboardApiBasePath.Value}";
        if(!path.StartsWith("/"))
            path = "/" + path;
        if(!path.EndsWith("/"))
            path += "/";
        path += endpoint;
        return $"{protocol}://{hostname}{path}";
    }

    /// <summary>
    /// Sends the given request body to the leaderboard server in a new Thread, as to not block execution.
    /// </summary>
    public static void PostRecords(PostRecordRequest body)
    {
        new Thread(() =>
        {
            body.ticket = SteamUtil.GetSessionTicket();
            string requestUrl = getRequestUrl("leaderboard");
            string stringBody = JsonConvert.SerializeObject(body);
            Plugin.Logger.LogDebug(stringBody);

            using (HttpClient client = new HttpClient())
            {
                HttpContent content = new StringContent(stringBody, Encoding.UTF8, "application/json");
                var response = client.PostAsync(requestUrl, content).Result;
                if(!response.IsSuccessStatusCode)
                    throw new WebException($"Failed to post leaderboard: {response.StatusCode}");
            }
        }).Start();
    }

    /// <summary>
    /// Returns a list of records fetched from the leaderboard server for the given stage.
    /// </summary>
    public static List<Record> GetRecords(string stage, bool horde)
    {
        List<CSteamID> friendIds = SteamUtil.GetFriendsList();
        friendIds.Add(SteamUtil.GetOwnSteamID());
        string steamidParam = string.Join(",", friendIds.Select(x => x.m_SteamID).ToArray());
        string stageParam = HttpUtility.UrlEncode(stage);
        string requestUrl = getRequestUrl("leaderboard") + $"?ids={steamidParam}&stage={stageParam}";
        
        if(horde)
            requestUrl += $"&horde=true";

        List<Record> records;
        using (HttpClient client = new HttpClient())
        {
            Plugin.Logger.LogDebug($"Getting leaderboard records from {requestUrl}");
            var response = client.GetAsync(requestUrl).Result;
            if(!response.IsSuccessStatusCode)
                throw new WebException($"Failed getting leaderboard records from {requestUrl} with status code {response.StatusCode}");
            Plugin.Logger.LogDebug($"Result: {response.Content.ReadAsStringAsync().Result}");
            records = JsonConvert.DeserializeObject<List<Record>>(response.Content.ReadAsStringAsync().Result);
        }
        return records;
    }
}