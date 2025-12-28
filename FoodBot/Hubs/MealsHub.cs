using FoodBot.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FoodBot.Hubs;

[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class MealsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context.User.GetUserId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        catch { }
        await base.OnConnectedAsync();
    }
}
