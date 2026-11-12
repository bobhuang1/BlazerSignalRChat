using BlazerSignalRChat.Chat;

namespace BlazerSignalRChat.Tests;

public class HubRateLimiterTests
{
    /// <summary>A clock the test advances by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan by) => _ticks += (long)(by.TotalSeconds * TimestampFrequency);
    }

    [Fact]
    public void Burst_is_allowed_then_limited()
    {
        var limiter = new HubRateLimiter(new ManualTime());

        var allowed = 0;
        for (var i = 0; i < 50; i++)
        {
            if (limiter.TryAcquire("c1", HubAction.Send)) allowed++;
        }

        Assert.Equal(10, allowed);
    }

    [Fact]
    public void Tokens_refill_over_time()
    {
        var time = new ManualTime();
        var limiter = new HubRateLimiter(time);
        while (limiter.TryAcquire("c1", HubAction.Send)) { }

        Assert.False(limiter.TryAcquire("c1", HubAction.Send));

        time.Advance(TimeSpan.FromSeconds(1));   // Send refills 2 per second
        Assert.True(limiter.TryAcquire("c1", HubAction.Send));
        Assert.True(limiter.TryAcquire("c1", HubAction.Send));
        Assert.False(limiter.TryAcquire("c1", HubAction.Send));
    }

    [Fact]
    public void Refill_never_exceeds_capacity()
    {
        var time = new ManualTime();
        var limiter = new HubRateLimiter(time);
        time.Advance(TimeSpan.FromHours(1));

        var allowed = 0;
        for (var i = 0; i < 50; i++)
        {
            if (limiter.TryAcquire("c1", HubAction.React)) allowed++;
        }

        Assert.Equal(10, allowed);
    }

    [Fact]
    public void Connections_and_actions_are_limited_independently()
    {
        var limiter = new HubRateLimiter(new ManualTime());
        while (limiter.TryAcquire("c1", HubAction.Send)) { }

        Assert.False(limiter.TryAcquire("c1", HubAction.Send));
        Assert.True(limiter.TryAcquire("c1", HubAction.React));
        Assert.True(limiter.TryAcquire("c2", HubAction.Send));
    }

    [Fact]
    public void Remove_forgets_a_connection()
    {
        var limiter = new HubRateLimiter(new ManualTime());
        while (limiter.TryAcquire("c1", HubAction.Send)) { }

        limiter.Remove("c1");

        Assert.True(limiter.TryAcquire("c1", HubAction.Send));
    }
}
