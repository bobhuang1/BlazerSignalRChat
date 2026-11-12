using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace BlazerSignalRChat.Chat;

/// <summary>
/// Clears a user's typing indicator a few seconds after their last keystroke. There is one
/// timer per connection and every keystroke restarts it. Hub instances only live for one
/// invocation, so this singleton broadcasts through IHubContext.
/// </summary>
public sealed class TypingTimeouts
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    private readonly ChatState _state;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<TypingTimeouts> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _timers = new();

    public TypingTimeouts(ChatState state, IHubContext<ChatHub> hubContext, ILogger<TypingTimeouts> logger)
    {
        _state = state;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>Starts (or restarts) the countdown for a connection.</summary>
    public void Restart(string connectionId, string userId, string roomKey)
    {
        var cts = new CancellationTokenSource();
        var previous = default(CancellationTokenSource);
        _timers.AddOrUpdate(connectionId, cts, (_, old) =>
        {
            previous = old;
            return cts;
        });
        Dispose(previous);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout, cts.Token);
                if (_timers.TryRemove(new KeyValuePair<string, CancellationTokenSource>(connectionId, cts)))
                {
                    cts.Dispose();
                }
                if (_state.SetTyping(connectionId, userId, roomKey, false))
                {
                    await _hubContext.Clients.Group(roomKey).SendAsync("TypingChanged", roomKey, _state.RoomTypingIds(roomKey));
                }
            }
            catch (OperationCanceledException)
            {
                // Replaced by a newer keystroke or cancelled on stop/disconnect.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Typing auto-clear failed for room {Room}", roomKey);
            }
        });
    }

    /// <summary>Stops the countdown, for example when the user sends the message or disconnects.</summary>
    public void Cancel(string connectionId)
    {
        if (_timers.TryRemove(connectionId, out var cts)) Dispose(cts);
    }

    private static void Dispose(CancellationTokenSource? cts)
    {
        if (cts is null) return;
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
        cts.Dispose();
    }
}
