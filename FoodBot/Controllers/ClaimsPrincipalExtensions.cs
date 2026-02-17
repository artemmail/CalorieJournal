using System;
using System.Security.Claims;

namespace FoodBot.Controllers;

public static class ClaimsPrincipalExtensions
{
    public static long GetAppUserId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue("app_user_id"), out var id)
            ? id
            : GetChatId(user);

    public static long GetChatId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue("chat_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException();
}
