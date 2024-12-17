using System;
using System.Collections.Generic;
using System.Threading;
using Steamworks;

namespace SteamFriendLeaderboard;

public static class SteamUtil
{
    private static List<CSteamID> friendsCache = new();
    private static Callback<GetTicketForWebApiResponse_t> m_AuthTicketForWebApiResponseCallback;
    private static string m_SessionTicket;

    #region Initialization
    
    /// <summary>
    /// Should be called during game initialization, after Steamworks Initialization is complete.
    /// Requests a Steam Session Ticket.
    /// </summary>
    public static void Init()
    {
        if(m_AuthTicketForWebApiResponseCallback is null)
            RequestSteamTicket();
    }

    /// <summary>
    /// Called when Steam responds with a Session Ticket.
    /// </summary>
    private static void OnAuthCallback(GetTicketForWebApiResponse_t callback)
    {
        Plugin.Logger.LogDebug($"OnAuth Callback called: {callback.m_eResult}");
        m_SessionTicket = BitConverter.ToString(callback.m_rgubTicket).Replace("-", string.Empty);
        m_AuthTicketForWebApiResponseCallback.Dispose();
        m_AuthTicketForWebApiResponseCallback = null;
        Plugin.Logger.LogDebug($"Received Steam ticket: {m_SessionTicket}");
    }

    /// <summary>
    /// Requests a Steam Session ticket and continuously triggers callback handling until it's received.
    /// </summary>
    private static void RequestSteamTicket()
    {
        m_AuthTicketForWebApiResponseCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnAuthCallback);
        HAuthTicket ticketReq = SteamUser.GetAuthTicketForWebApi(null);
        Plugin.Logger.LogDebug($"Requested Steam ticket {ticketReq.m_HAuthTicket}");
        new Thread(() =>
        {
            Thread.Sleep(500);
            while (m_SessionTicket is null)
            {
                Plugin.Logger.LogDebug($"Haven't received Steam ticket yet, running callbacks manually...");
                Thread.Sleep(500);
                SteamAPI.RunCallbacks();
            }
        }).Start();
    }

    #endregion
    
    /// <summary>
    /// Returns the Steam ID of the player.
    /// </summary>
    public static CSteamID GetOwnSteamID()
    {
        CSteamID steamId = SteamUser.GetSteamID();
        Plugin.Logger.LogDebug($"Found own SteamID {steamId.m_SteamID}");
        return SteamUser.GetSteamID();
    }

    /// <summary>
    /// Returns the stringified binary content of the Steam Session Ticket.
    /// </summary>
    public static string GetSessionTicket()
    {
        return m_SessionTicket;
    }

    /// <summary>
    /// Returns a list of Steam IDs representing the friends list of the player.
    /// </summary>
    public static List<CSteamID> GetFriendsList()
    {
        if(friendsCache.Count != 0)
            return friendsCache;
        
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagAll);
        List<CSteamID> friends = new List<CSteamID>();
        for (int i = 0; i < friendCount; i++)
        {
            CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagAll);
            friends.Add(friend);
        }

        Plugin.Logger.LogDebug($"Found {friendCount} Steam Friends:");
        for(int i = 0; i < friendCount; i++)
            Plugin.Logger.LogDebug($"> {friends[i].m_SteamID}");
        
        friendsCache = friends;
        return friends;
    }

    /// <summary>
    /// Returns the Display Name of the Steam user with the given ID.
    /// </summary>
    public static string GetDisplayName(ulong steamId)
    {
        return GetDisplayName(new CSteamID(steamId));
    }
    
    /// <summary>
    /// Returns the Display Name of the Steam user with the given ID.
    /// </summary>
    public static string GetDisplayName(CSteamID steamId)
    {
        Plugin.Logger.LogDebug($"Getting display name for {steamId.m_SteamID}");
        string name = SteamFriends.GetFriendPersonaName(steamId);
        Plugin.Logger.LogDebug($"Found display name: {name}");
        return name;
    }
}