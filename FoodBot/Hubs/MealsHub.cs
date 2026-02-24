using FoodBot.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FoodBot.Hubs;

[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class MealsHub : Hub
{
    private readonly ILogger<MealsHub> _log;

    public MealsHub(ILogger<MealsHub> log)
    {
        _log = log;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
            {
                await base.OnConnectedAsync();
                return;
            }

            var chatId = Context.User.GetChatId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{chatId}");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add connection {ConnectionId} to chat group", Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }
}
