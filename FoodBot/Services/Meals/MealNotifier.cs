using Microsoft.AspNetCore.SignalR;
using FoodBot.Hubs;

namespace FoodBot.Services;

public interface IMealNotifier
{
    Task MealUpdated(long chatId, MealListItem item);
}

public sealed class MealNotifier : IMealNotifier
{
    private readonly IHubContext<MealsHub> _hub;
    public MealNotifier(IHubContext<MealsHub> hub)
    {
        _hub = hub;
    }

    public Task MealUpdated(long chatId, MealListItem item)
    {
        return _hub.Clients.Group($"chat-{chatId}").SendAsync("MealUpdated", item);
    }
}
