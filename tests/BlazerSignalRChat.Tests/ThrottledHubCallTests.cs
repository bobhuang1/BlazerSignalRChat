using System.Security.Claims;
using BlazerSignalRChat.Chat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace BlazerSignalRChat.Tests;

public class ThrottledHubCallTests
{
    private sealed class FakeContext : HubCallerContext
    {
        private readonly FeatureCollection _features = new();
        public FakeContext(string user)
        {
            var http = new DefaultHttpContext();
            http.Request.QueryString = new QueryString("?user=" + user);
            _features.Set<IHttpContextFeature>(new HttpContextFeature(http));
        }
        private sealed class HttpContextFeature(HttpContext c) : IHttpContextFeature
        {
            public HttpContext? HttpContext { get; set; } = c;
        }
        public override string ConnectionId => "c1";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features => _features;
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private static (ChatHub Hub, HubRateLimiter Limiter) NewHub()
    {
        var limiter = new HubRateLimiter();
        var hub = new ChatHub(new ChatState(), limiter, null!) { Context = new FakeContext("alice") };
        return (hub, limiter);
    }

    [Fact]
    public async Task Throttled_LoadEarlier_is_flagged_and_keeps_has_more()
    {
        var (hub, limiter) = NewHub();
        while (limiter.TryAcquire("c1", HubAction.History)) { }

        var page = await hub.LoadEarlier("general", long.MaxValue);

        Assert.True(page.Throttled);
        Assert.True(page.HasMore);
        Assert.Empty(page.Messages);
    }

    [Fact]
    public async Task Unthrottled_LoadEarlier_is_not_flagged()
    {
        var (hub, _) = NewHub();

        var page = await hub.LoadEarlier("general", long.MaxValue);

        Assert.False(page.Throttled);
    }

    [Fact]
    public async Task Throttled_JoinRoom_returns_false()
    {
        var (hub, limiter) = NewHub();
        while (limiter.TryAcquire("c1", HubAction.Join)) { }

        Assert.False(await hub.JoinRoom("general", true));
    }
}
