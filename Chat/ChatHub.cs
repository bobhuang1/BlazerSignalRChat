using Microsoft.AspNetCore.SignalR;

namespace BlazerSignalRChat.Chat;

public sealed class ChatHub : Hub
{
    private static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(4);

    private readonly ChatState _state;

    public ChatHub(ChatState state) => _state = state;

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
        _state.RegisterConnection(Context.ConnectionId, user.Id);
        await Clients.All.SendAsync("OnlineUsersChanged", _state.OnlineUsers);
        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(string roomKey)
    {
        var user = CurrentUser;
        if (user is null || _state.GetRoom(roomKey) is null) return;

        _state.AddConnectionRoom(Context.ConnectionId, roomKey);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);

        var snapshot = _state.SnapshotFor(roomKey);
        await Clients.Caller.SendAsync("JoinedRoom", roomKey, snapshot.History, snapshot.Participants);
        await Clients.GroupExcept(roomKey, Context.ConnectionId).SendAsync("SystemEvent", roomKey, $"{user.DisplayName} joined");
        await Clients.Group(roomKey).SendAsync("RoomPresenceChanged", roomKey, _state.RoomOnline(roomKey));
    }

    public async Task LeaveRoom(string roomKey)
    {
        var user = CurrentUser;
        if (user is null) return;

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

        text = text.Trim();
        if (text.Length > 2000) text = text[..2000];

        var msg = _state.AddMessage(roomKey, user, text);
        await Clients.Group(roomKey).SendAsync("ReceiveMessage", msg);

        if (_state.SetTyping(Context.ConnectionId, user.Id, roomKey, false))
        {
            await Clients.Group(roomKey).SendAsync("TypingChanged", roomKey, _state.RoomTypingIds(roomKey));
        }
    }

    public async Task UserTyping(string roomKey, bool isTyping)
    {
        var user = CurrentUser;
        if (user is null || _state.GetRoom(roomKey) is null) return;

        if (_state.SetTyping(Context.ConnectionId, user.Id, roomKey, isTyping))
        {
            await Clients.Group(roomKey).SendAsync("TypingChanged", roomKey, _state.RoomTypingIds(roomKey));
        }

        if (isTyping)
        {
            // Auto-clear the typing indicator if the user walks away.
            var connectionId = Context.ConnectionId;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TypingTimeout);
                if (_state.SetTyping(connectionId, user.Id, roomKey, false))
                {
                    await Clients.Group(roomKey).SendAsync("TypingChanged", roomKey, _state.RoomTypingIds(roomKey));
                }
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _state.UnregisterConnection(Context.ConnectionId, out var userId, out var rooms);

        foreach (var room in rooms)
        {
            if (userId is not null && _state.GetUser(userId) is { } user)
            {
                await Clients.GroupExcept(room, Context.ConnectionId).SendAsync("SystemEvent", room, $"{user.DisplayName} left");
            }
            await Clients.Group(room).SendAsync("TypingChanged", room, _state.RoomTypingIds(room));
            await Clients.Group(room).SendAsync("RoomPresenceChanged", room, _state.RoomOnline(room));
        }

        await Clients.All.SendAsync("OnlineUsersChanged", _state.OnlineUsers);
        await base.OnDisconnectedAsync(exception);
    }
}

public static class ChatStateExtensions
{
    /// <summary>Snapshot of a room for a freshly joining caller.</summary>
    public static RoomSnapshot SnapshotFor(this ChatState state, string roomKey) =>
        new(state.History(roomKey), state.Participants(roomKey));
}