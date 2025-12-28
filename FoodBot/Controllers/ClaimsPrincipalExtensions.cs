using System;
using System.Security.Claims;

namespace FoodBot.Controllers;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue("user_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user_id claim");
}
