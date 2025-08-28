using System.Security.Claims;
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

    private long GetChatId() =>
        long.TryParse(User.FindFirstValue("chat_id"), out var id) ? id : throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<PersonalCard?>> Get(CancellationToken ct)
    {
        var card = await _cards.GetAsync(GetChatId(), ct);
        return Ok(card);
    }

    [HttpPost]
    public Task<PersonalCard> Save([FromBody] PersonalCard card, CancellationToken ct)
        => _cards.UpsertAsync(GetChatId(), card, ct);
}
