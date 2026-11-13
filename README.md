# Finish By for Jellyfin

Finish By shows media that can finish before a daily cutoff. Set a different time for each weekday in **Dashboard → Plugins → Finish By**. Disabled days show the full library. The default is disabled on every day; enable Monday through Friday and set 21:00 for a weekday 9pm rule.

The settings page detects the time zone of the device used for setup; you can change it there. The chosen time zone applies to all users. Cutoffs from midnight through 5:59 am automatically fall on the following calendar day; until that cutoff, the previous day's rule applies. Cutoffs from 6:00 am onward fall on the selected day. The one-hour **Show everything** override temporarily disables filtering for everyone. Refresh library views after changing settings or override state.

The filter compares the cutoff with the current time plus an item's remaining runtime. If Jellyfin includes a playback position, resumed items use the time left; otherwise they use the full runtime. An item ending exactly at the cutoff is shown. After the cutoff, no playable items appear that day. Series are shown only when their next unwatched or partly watched episode **after the latest watched episode** can finish before the cutoff. Navigation folders remain visible. Unknown-runtime playable items are hidden while a cutoff is active.

This is a server-wide display filter for Jellyfin API lists, including the main item query and lists of recent media. It is **not** a playback restriction: direct item links and streams still work. A client that caches library lists may need a refresh when a cutoff changes or passes. On active days, the plugin fetches complete results for paged item queries before filtering and paginating; very large libraries may make those requests slower.

## Install from Jellyfin

The catalog includes builds for Jellyfin **10.11.11** and **12.4**. In Jellyfin, open **Dashboard → Plugins → Repositories**, add a repository named **Finish By**, and use this URL:

```text
https://raw.githubusercontent.com/GitUpGitUp/jellyfin-plugin--finish-by/main/manifest.json
```

Open the **Catalog** tab, install **Finish By**, and restart Jellyfin. Then open the plugin settings and enable the days and cutoff times you want.

## Build manually

Install the .NET 10 SDK, then publish the target matching your server:

```sh
cd FinishBy
dotnet publish Jellyfin.Plugin.FinishBy.csproj -f net9.0 -c Release -o publish/10.11
dotnet publish Jellyfin.Plugin.FinishBy.csproj -f net10.0 -c Release -o publish/12.4
```

Copy the matching DLL and `.deps.json`, plus `compat/10.11/meta.json` for Jellyfin 10.11.11 or the root `meta.json` for Jellyfin 12.4, into a new `FinishBy` folder under the server's `plugins` directory. Restart Jellyfin. The referenced Jellyfin assemblies are supplied by the server.

## Scope

The filter handles the Jellyfin API's `BaseItemDto` list responses. Jellyfin clients can also show cached data or specialized screens whose responses use other models. Playback permissions, library metadata, and media files are untouched.
