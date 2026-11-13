# Finish By for Jellyfin

Finish By shows media that can finish before a daily cutoff. Set a different time for each weekday in **Dashboard → Plugins → Finish By**. Disabled days show the full library. The default is disabled on every day; enable Monday through Friday and set 21:00 for a weekday 9pm rule.

The rule uses the Jellyfin server's local time zone. It compares the cutoff with the current time plus the item's remaining runtime. If Jellyfin includes a playback position, resumed items use the time left; otherwise they use the full runtime. An item ending exactly at the cutoff is shown. After the cutoff, no playable items appear that day. Navigation folders and series stay visible, even if their contents are empty. Unknown-runtime playable items are hidden while a cutoff is active.

This is a server-wide display filter for Jellyfin API lists, including the main item query and lists of recent media. It is **not** a playback restriction: direct item links and streams still work. A client that caches library lists may need a refresh when a cutoff changes or passes. On active days, the plugin fetches complete results for paged item queries before filtering and paginating; very large libraries may make those requests slower.

## Install from Jellyfin

This release targets Jellyfin **12.4**. In Jellyfin, open **Dashboard → Plugins → Repositories**, add a repository named **Finish By**, and use this URL:

```text
https://raw.githubusercontent.com/GitUpGitUp/jellyfin-plugin--finish-by/main/manifest.json
```

Open the **Catalog** tab, install **Finish By**, and restart Jellyfin. Then open the plugin settings and enable the days and cutoff times you want.

## Build manually

Install the .NET 10 SDK, then run:

```sh
cd FinishBy
dotnet publish Jellyfin.Plugin.FinishBy.csproj -c Release -o publish
```

Copy `publish/Jellyfin.Plugin.FinishBy.dll`, `publish/Jellyfin.Plugin.FinishBy.deps.json`, and `meta.json` into a new `FinishBy` folder under the Jellyfin server's `plugins` directory, then restart Jellyfin. The referenced Jellyfin assemblies are supplied by the server. Match the `Jellyfin.Controller` and `Jellyfin.Model` package versions in the project file to your server version if needed.

## Scope

The filter handles the Jellyfin API's `BaseItemDto` list responses. Jellyfin clients can also show cached data or specialized screens whose responses use other models. Playback permissions, library metadata, and media files are untouched.
