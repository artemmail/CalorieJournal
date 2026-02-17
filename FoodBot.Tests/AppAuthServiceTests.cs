using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class AppAuthServiceTests
{
    private static (BotDbContext db, AppAuthService svc) CreateService()
    {
        var opts = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new BotDbContext(opts);

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test_super_secret_key_which_is_long_enough_123456",
                ["Auth:Issuer"] = "foodbot-test",
                ["Auth:Audience"] = "foodbot-test-aud",
                ["Auth:AccessTokenHours"] = "2",
                ["Auth:RefreshTokenDays"] = "7",
                ["Auth:StartCodeMinutes"] = "15"
            })
            .Build();

        var jwt = new JwtService(cfg);
        var svc = new AppAuthService(db, jwt, cfg);
        return (db, svc);
    }

    [Fact]
    public async Task StartAnonymousAsync_CreatesUserAndReturnsTokens()
    {
        var (db, svc) = CreateService();

        var session = await svc.StartAnonymousAsync("install-1", "Android", CancellationToken.None);

        Assert.NotEmpty(session.AccessToken);
        Assert.NotEmpty(session.RefreshToken);
        Assert.True(session.IsAnonymous);
        Assert.NotEqual(0, session.AppUserId);
        Assert.NotEqual(0, session.ChatId);

        Assert.Equal(1, await db.AppUsers.CountAsync());
        Assert.Equal(1, await db.AppRefreshTokens.CountAsync());
        Assert.Equal(1, await db.AppUserDevices.CountAsync());
    }

    [Fact]
    public async Task RefreshAsync_RotatesTokenAndReturnsNewSession()
    {
        var (db, svc) = CreateService();
        var session = await svc.StartAnonymousAsync("install-2", "Android", CancellationToken.None);

        var refreshed = await svc.RefreshAsync(session.RefreshToken, "install-2", "Android", CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.NotEqual(session.RefreshToken, refreshed!.RefreshToken);
        Assert.Equal(session.AppUserId, refreshed.AppUserId);

        var allRefresh = await db.AppRefreshTokens.CountAsync();
        Assert.Equal(2, allRefresh);
    }

    [Fact]
    public async Task RequestCodeAndStatus_WorkForAppUser()
    {
        var (_, svc) = CreateService();
        var session = await svc.StartAnonymousAsync("install-3", "Android", CancellationToken.None);

        var code = await svc.RequestCodeAsync(session.AppUserId, CancellationToken.None);
        var status = await svc.GetStatusAsync(code.Code, CancellationToken.None);

        Assert.NotNull(status);
        Assert.False(status!.Linked);
        Assert.True(status.SecondsLeft > 0);
    }
}
