using System;
using System.Security.Claims;

namespace FoodBot.Controllers;

public static class ClaimsPrincipalExtensions
{
    public static long GetChatId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue("chat_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException();
}
