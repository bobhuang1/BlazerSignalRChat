using System.Collections.Concurrent;

namespace BlazerSignalRChat.Chat;

/// <summary>Abuse limits for the hub. The demo has no login, so these keep one noisy client
/// from slowing everybody else down.</summary>
public static class ChatLimits
{
    public const int MaxConnections = 200;
    public const int MaxMessageLength = 2000;
    public const int MaxEmojiLength = 16;
    public const int MaxReactionKindsPerMessage = 24;

    /// <summary>Largest hub message the server accepts. A 2000-character message is well under this.</summary>
    public const long MaxReceiveMessageSize = 16 * 1024;
}

public enum HubAction
{
    Join,
    Send,
    React,
    Typing,
}

/// <summary>Classic token bucket: holds up to <c>capacity</c> tokens and refills at a steady rate.</summary>
public sealed class TokenBucket
{
    private readonly double _capacity;
    private readonly double _refillPerSecond;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private double _tokens;
    private long _lastTimestamp;

    public TokenBucket(double capacity, double refillPerSecond, TimeProvider time)
    {
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
        _time = time;
        _tokens = capacity;
        _lastTimestamp = time.GetTimestamp();
    }

    public bool TryTake()
    {
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            var elapsed = _time.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
            _lastTimestamp = now;
            _tokens = Math.Min(_capacity, _tokens + elapsed * _refillPerSecond);
            if (_tokens < 1) return false;
            _tokens -= 1;
            return true;
        }
    }
}

/// <summary>One token bucket per connection and action, so a spammer is throttled on its own.</summary>
public sealed class HubRateLimiter
{
    private static readonly Dictionary<HubAction, (double Capacity, double RefillPerSecond)> Rules = new()
    {
        [HubAction.Join] = (10, 1),
        [HubAction.Send] = (10, 2),
        [HubAction.React] = (10, 3),
        [HubAction.Typing] = (6, 2),
    };

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<(string ConnectionId, HubAction Action), TokenBucket> _buckets = new();

    public HubRateLimiter() : this(TimeProvider.System) { }

    public HubRateLimiter(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>True when the connection may perform the action now.</summary>
    public bool TryAcquire(string connectionId, HubAction action)
    {
        var (capacity, refill) = Rules[action];
        var bucket = _buckets.GetOrAdd((connectionId, action), _ => new TokenBucket(capacity, refill, _time));
        return bucket.TryTake();
    }

    public void Remove(string connectionId)
    {
        foreach (var action in Rules.Keys)
        {
            _buckets.TryRemove((connectionId, action), out _);
        }
    }
}
