using Microsoft.AspNetCore.SignalR;
using FoodBot.Hubs;

namespace FoodBot.Services;

public interface IMealNotifier
{
    Task MealUpdated(long userId, MealListItem item);
}

public sealed class MealNotifier : IMealNotifier
{
    private readonly IHubContext<MealsHub> _hub;
    public MealNotifier(IHubContext<MealsHub> hub)
    {
        _hub = hub;
    }

    public Task MealUpdated(long userId, MealListItem item)
    {
        return _hub.Clients.Group($"user-{userId}").SendAsync("MealUpdated", item);
    }
}
