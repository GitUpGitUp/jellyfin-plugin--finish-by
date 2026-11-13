using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.FinishBy;

/// <summary>Filters library lists. Direct item and playback endpoints remain accessible.</summary>
public sealed class FinishByActionFilter : IAsyncActionFilter
{
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

        if (result.Value is QueryResult<BaseItemDto> query)
        {
            var visible = query.Items.Where(item => FinishByPolicy.ShouldShow(item, remaining.Value)).ToArray();
            result.Value = unpaged
                ? new QueryResult<BaseItemDto>(
                    Math.Max(startIndex, 0),
                    visible.Length,
                    visible.Skip(Math.Max(startIndex, 0)).Take(limit is >= 0 ? limit.Value : int.MaxValue).ToArray())
                : new QueryResult<BaseItemDto>(query.StartIndex, query.TotalRecordCount, visible);
        }
        else if (result.Value is IEnumerable<BaseItemDto> items)
        {
            result.Value = items.Where(item => FinishByPolicy.ShouldShow(item, remaining.Value)).ToArray();
        }
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
