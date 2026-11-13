using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.FinishBy;

/// <summary>Filters library lists. Direct item and playback endpoints remain accessible.</summary>
public sealed class FinishByActionFilter : IAsyncActionFilter
{
    private readonly ILibraryManager _library;
    private readonly IUserManager _users;
    private readonly IUserDataManager _userData;

    public FinishByActionFilter(ILibraryManager library, IUserManager users, IUserDataManager userData)
    {
        _library = library;
        _users = users;
        _userData = userData;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var remaining = FinishByPolicy.GetTimeRemaining(configuration, DateTimeOffset.UtcNow);
        if (remaining is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var isItemQuery = descriptor is not null && ContainsItemQuery(descriptor.MethodInfo.ReturnType);
        var startIndex = ReadInt(context, "startIndex") ?? 0;
        var limit = ReadInt(context, "limit");
        var unpaged = isItemQuery && descriptor is not null
            && (context.ActionArguments.ContainsKey("startIndex") || context.ActionArguments.ContainsKey("limit"))
            && descriptor.MethodInfo.GetParameters()
                .Where(parameter => parameter.Name is "startIndex" or "limit")
                .All(parameter => parameter.ParameterType == typeof(int?));

        // Fetch the ordered list before slicing it so hidden entries cannot leave page gaps.
        if (unpaged)
        {
            if (context.ActionArguments.ContainsKey("startIndex"))
            {
                context.ActionArguments["startIndex"] = null;
            }

            if (context.ActionArguments.ContainsKey("limit"))
            {
                context.ActionArguments["limit"] = null;
            }
        }

        var executed = await next().ConfigureAwait(false);
        if (executed.Result is not ObjectResult result)
        {
            return;
        }

        var userId = context.HttpContext.User.FindFirst("Jellyfin-UserId")?.Value;
        var user = Guid.TryParse(userId, out var id) ? _users.GetUserById(id) : null;
        bool show(BaseItemDto item) => item.Type == BaseItemKind.Series && user is not null
            ? ShouldShowSeries(item, user, remaining.Value)
            : FinishByPolicy.ShouldShow(item, remaining.Value);

        context.HttpContext.Response.Headers.CacheControl = "no-store";
        if (result.Value is QueryResult<BaseItemDto> query)
        {
            var visible = query.Items.Where(show).ToArray();
            result.Value = unpaged
                ? new QueryResult<BaseItemDto>(
                    Math.Max(startIndex, 0),
                    visible.Length,
                    visible.Skip(Math.Max(startIndex, 0)).Take(limit is >= 0 ? limit.Value : int.MaxValue).ToArray())
                : new QueryResult<BaseItemDto>(query.StartIndex, query.TotalRecordCount, visible);
        }
        else if (result.Value is IEnumerable<BaseItemDto> items)
        {
            result.Value = items.Where(show).ToArray();
        }
    }

    private bool ShouldShowSeries(BaseItemDto series, Jellyfin.Database.Implementations.Entities.User user, TimeSpan remaining)
    {
        var episodes = _library.GetItemsResult(new InternalItemsQuery
        {
            ParentId = series.Id,
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Episode],
            User = user
        }).Items.OrderBy(episode => episode.ParentIndexNumber ?? int.MaxValue)
            .ThenBy(episode => episode.IndexNumber ?? int.MaxValue)
            .ToArray();

        var progress = episodes.Select(episode => (Episode: episode, Data: _userData.GetUserData(user, episode)))
            .ToArray();
        var lastWatched = Array.FindLastIndex(progress, pair => pair.Data?.Played == true);
        var candidates = progress.Skip(lastWatched + 1)
            .Where(pair => pair.Data?.Played != true)
            .ToArray();
        var next = candidates.FirstOrDefault(pair => pair.Data?.PlaybackPositionTicks > 0);
        if (next.Episode is null)
        {
            next = candidates.FirstOrDefault(pair => pair.Episode.ParentIndexNumber is > 0);
        }

        if (next.Episode is null)
        {
            next = candidates.FirstOrDefault();
        }
        if (next.Episode is null)
        {
            return false;
        }

        var remainingTicks = next.Episode.RunTimeTicks ?? 0;
        if (remainingTicks <= 0)
        {
            return false;
        }

        var position = Math.Clamp(next.Data?.PlaybackPositionTicks ?? 0, 0, remainingTicks);
        return remaining > TimeSpan.Zero && remainingTicks - position <= remaining.Ticks;
    }

    private static int? ReadInt(ActionExecutingContext context, string name) =>
        context.ActionArguments.TryGetValue(name, out var value) && value is int number ? number : null;

    private static bool ContainsItemQuery(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        if (type.GetGenericTypeDefinition() == typeof(QueryResult<>)
            && type.GetGenericArguments()[0] == typeof(BaseItemDto))
        {
            return true;
        }

        return type.GetGenericArguments().Any(ContainsItemQuery);
    }
}
