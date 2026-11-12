using Microsoft.AspNetCore.SignalR;

namespace BlazerSignalRChat.Chat;

public sealed class ChatHub : Hub
{
    private readonly ChatState _state;
    private readonly HubRateLimiter _limiter;
    private readonly TypingTimeouts _typingTimeouts;

    public ChatHub(ChatState state, HubRateLimiter limiter, TypingTimeouts typingTimeouts)
    {
        _state = state;
        _limiter = limiter;
        _typingTimeouts = typingTimeouts;
    }

    private bool Allowed(HubAction action) => _limiter.TryAcquire(Context.ConnectionId, action);

    private ChatUser? CurrentUser =>
        _state.GetUser(Context.GetHttpContext()?.Request.Query["user"].ToString());

    public override async Task OnConnectedAsync()
    {
        var user = CurrentUser;
        if (user is null)
        {
            Context.Abort();
            return;
        }
        if (!_state.TryRegisterConnection(Context.ConnectionId, user.Id, ChatLimits.MaxConnections, out var cameOnline))
        {
            Context.Abort();
            return;
        }
        // Only the first tab of a user changes who is online, so other tabs stay quiet.
        if (cameOnline)
        {
            await Clients.All.SendAsync("OnlineUsersChanged", _state.OnlineUsers);
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Subscribes the caller to a room. History is only sent when the client asks for it
    /// (the room it is showing), so a tab does not pull every room's history on connect.</summary>
    public async Task JoinRoom(string roomKey, bool withHistory)
    {
        var user = CurrentUser;
        if (user is null || _state.GetRoom(roomKey) is null) return;
        if (!Allowed(HubAction.Join)) return;

        var firstJoin = _state.AddConnectionRoom(Context.ConnectionId, roomKey);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);

        var snapshot = withHistory ? _state.SnapshotFor(roomKey) : new RoomSnapshot(new(), new());
        await Clients.Caller.SendAsync("JoinedRoom", roomKey, snapshot.History, snapshot.Participants);
        if (firstJoin)
        {
            // Keep the feed quiet on tab switches: the "joined" line is only
            // broadcast the first time this connection subscribes.
            await Clients.GroupExcept(roomKey, Context.ConnectionId).SendAsync("SystemEvent", roomKey, $"{user.DisplayName} joined");
            await Clients.Group(roomKey).SendAsync("RoomPresenceChanged", roomKey, _state.RoomOnline(roomKey));
        }
    }

    public async Task LeaveRoom(string roomKey)
    {
        var user = CurrentUser;
        if (user is null) return;
        if (!Allowed(HubAction.Join)) return;

        _state.RemoveConnectionRoom(Context.ConnectionId, roomKey);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
        await Clients.GroupExcept(roomKey, Context.ConnectionId).SendAsync("SystemEvent", roomKey, $"{user.DisplayName} left");
        await Clients.Group(roomKey).SendAsync("RoomPresenceChanged", roomKey, _state.RoomOnline(roomKey));
    }

    public async Task SendMessage(string roomKey, string text)
    {
        var user = CurrentUser;
        if (user is null || _state.GetRoom(roomKey) is null) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!Allowed(HubAction.Send)) return;

        text = text.Trim();
        if (text.Length > ChatLimits.MaxMessageLength) text = text[..ChatLimits.MaxMessageLength];

        var msg = _state.AddMessage(roomKey, user, text);
        await Clients.Group(roomKey).SendAsync("ReceiveMessage", msg);

        _typingTimeouts.Cancel(Context.ConnectionId);
        if (_state.SetTyping(Context.ConnectionId, user.Id, roomKey, false))
        {
            await Clients.Group(roomKey).SendAsync("TypingChanged", roomKey, _state.RoomTypingIds(roomKey));
        }
    }

    /// <summary>Toggles the caller's reaction on a message and fans the result out to the room.</summary>
    public async Task React(long messageId, string emoji)
    {
        var user = CurrentUser;
        if (user is null) return;
        emoji = (emoji ?? "").Trim();
        if (emoji.Length is 0 or > ChatLimits.MaxEmojiLength) return;
        if (!Allowed(HubAction.React)) return;

        var result = _state.ToggleReaction(messageId, user.Id, emoji);
        if (result is null) return;
        var (roomKey, userIds) = result.Value;

        await Clients.Group(roomKey).SendAsync("ReactionChanged", messageId, emoji, userIds);
    }

    public async Task UserTyping(string roomKey, bool isTyping)
    {
        var user = CurrentUser;
        if (user is null || _state.GetRoom(roomKey) is null) return;
        if (!Allowed(HubAction.Typing)) return;

        if (_state.SetTyping(Context.ConnectionId, user.Id, roomKey, isTyping))
        {
            await Clients.Group(roomKey).SendAsync("TypingChanged", roomKey, _state.RoomTypingIds(roomKey));
        }

        // Auto-clear the typing indicator if the user walks away; each keystroke restarts the timer.
        if (isTyping) _typingTimeouts.Restart(Context.ConnectionId, user.Id, roomKey);
        else _typingTimeouts.Cancel(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _typingTimeouts.Cancel(Context.ConnectionId);
        _limiter.Remove(Context.ConnectionId);
        _state.UnregisterConnection(Context.ConnectionId, out var userId, out var rooms, out var wentOffline);

        foreach (var room in rooms)
        {
            if (userId is not null && _state.GetUser(userId) is { } user)
            {
                await Clients.GroupExcept(room, Context.ConnectionId).SendAsync("SystemEvent", room, $"{user.DisplayName} left");
            }
            await Clients.Group(room).SendAsync("TypingChanged", room, _state.RoomTypingIds(room));
            await Clients.Group(room).SendAsync("RoomPresenceChanged", room, _state.RoomOnline(room));
        }

        if (wentOffline)
        {
            await Clients.All.SendAsync("OnlineUsersChanged", _state.OnlineUsers);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public static class ChatStateExtensions
{
    /// <summary>Snapshot of a room for a freshly joining caller.</summary>
    public static RoomSnapshot SnapshotFor(this ChatState state, string roomKey) =>
        new(state.History(roomKey), state.Participants(roomKey));
}