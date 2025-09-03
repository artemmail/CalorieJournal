using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FoodBot.Data;
using FoodBot.Services;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class ProfileController : ControllerBase
{
    private readonly PersonalCardService _cards;

    public ProfileController(PersonalCardService cards)
    {
        _cards = cards;
    }

    [HttpGet]
    public async Task<ActionResult<PersonalCard?>> Get(CancellationToken ct)
    {
        var card = await _cards.GetAsync(User.GetChatId(), ct);
        return Ok(card);
    }

    [HttpPost]
    public Task<PersonalCard> Save([FromBody] PersonalCard card, CancellationToken ct)
        => _cards.UpsertAsync(User.GetChatId(), card, ct);
}
