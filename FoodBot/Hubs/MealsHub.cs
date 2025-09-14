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
            var chatId = Context.User.GetChatId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{chatId}");
        }
        catch { }
        await base.OnConnectedAsync();
    }
}
