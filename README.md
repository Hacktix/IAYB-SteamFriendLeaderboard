# IAYB Steam Friend Leaderboard

## What's this?
A BepInEx plugin for [I Am Your Beast](https://store.steampowered.com/app/1876590/I_Am_Your_Beast/) which adds a leaderboard display to the level select screen of the game, letting you compare your best times with those of your Steam friends.

## What does it look like?
![Screenshot](https://cdn.discordapp.com/attachments/1289266484018675741/1318271008528138401/image.png?ex=67625fcc&is=67610e4c&hm=31a96480dd692d766a25869491bffa2e737a959e8d694bcc777891d29fd0d728&)

## How do I use it?
* Install [BepInEx](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2)
* [Download the mod](https://github.com/Hacktix/IAYB-SteamFriendLeaderboard/releases/latest)
* Drag the `BepInEx` folder from the mod zip file into the IAYB game folder
* Boot up the game

**NOTE:** You will only be able to see times of players who also have the mod installed and are on your Steam friends list.

## Is there a global leaderboard?
No, and there never will be. I neither have the ability nor the interest to prevent cheating, and vetting leaderboard entries manually goes way past my paygrade (of 0$, as I'm working on this as a hobby and hosting all the relevant infrastructure needed for it at my own cost).

-----

## WARNING: Nerd stuff under here

### How does it connect to Steam?
It uses the Steamworks API that's also used by the game itself in order to interface with Steam.

### Where is leaderboard data stored?
I've set up a small webserver connected to a database that is storing all leaderboard data. If I was a developer of the game I could've used the official Steam leaderboards feature, but as I am not, this is the next best thing. The API endpoint the mod connects to is configurable via a config file for the plugin, and documentation about the functionality of the API is in the works, so if someone wants to host their own leaderboard instance, they could very much do that.

### How is cheating prevented?
It's not! That's why these are friend-only leaderboards, not global ones. The mod uses the Steam Session Ticket system in order to ensure that people cannot impersonate others and falsify their record times, but the actual values themselves aren't checked at all. But, since they'd only be visible to Steam Friends anyway, I don't feel the need to add anything of that sort.
