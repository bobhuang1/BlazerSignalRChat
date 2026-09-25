using System.Collections.Concurrent;

namespace BlazerSignalRChat.Chat;

/// <summary>
/// In-memory store for the demo: chat users, rooms, message history and
/// live presence/typing state. Everything is process-local, so the demo
/// restarts with a clean slate.
/// </summary>
public sealed class ChatState
{
    private const long MaxHistoryPerRoom = 500;

    private readonly ConcurrentDictionary<string, ChatUser> _users = new();
    private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _messages = new();

    private readonly ConcurrentDictionary<string, string> _connUser = new();                  // connectionId -> userId
    private readonly ConcurrentDictionary<string, HashSet<string>> _connRooms = new();        // connectionId -> roomKeys
    private readonly ConcurrentDictionary<string, string> _typingConnRoom = new();            // connectionId -> roomKey
    private readonly ConcurrentDictionary<string, HashSet<string>> _roomTyping = new();       // roomKey -> userIds
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSeen = new();          // userId -> last active

    private long _nextId;
    private readonly object _historyLock = new();

    public ChatState()
    {
        Seed();
    }

    public IReadOnlyList<ChatUser> Users => _users.Values.OrderBy(u => u.Id).ToList();

    public IReadOnlyList<ChatRoom> Rooms => _rooms.Values.OrderBy(r => r.Key).ToList();

    public ChatUser? GetUser(string? id) => id is null ? null : _users.TryGetValue(id, out var u) ? u : null;

    public ChatRoom? GetRoom(string? key) => key is null ? null : _rooms.TryGetValue(key, out var r) ? r : null;

    public ChatMessage? LastMessage(string roomKey) =>
        _messages.TryGetValue(roomKey, out var list) && list.Count > 0 ? list[^1] : null;

    public List<ChatMessage> History(string roomKey) =>
        _messages.TryGetValue(roomKey, out var list) ? new List<ChatMessage>(list) : new();

    public List<ChatUser> Participants(string roomKey) =>
        History(roomKey).Select(m => m.User).DistinctBy(u => u.Id).ToList();

    public string[] OnlineUsers => _connUser.Values.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public bool IsOnline(string userId) => _connUser.Values.Contains(userId, StringComparer.Ordinal);

    public string LastSeenText(string userId)
    {
        if (IsOnline(userId)) return "online";
        if (!_lastSeen.TryGetValue(userId, out var seen)) return "never";
        var ago = DateTimeOffset.Now - seen;
        return ago.TotalSeconds < 60 ? "last seen just now"
             : ago.TotalMinutes < 60 ? $"last seen {Math.Max(1, (int)ago.TotalMinutes)}m ago"
             : ago.TotalHours < 24 ? $"last seen {(int)ago.TotalHours}h ago"
             : $"last seen {(int)ago.TotalDays}d ago";
    }

    public void RegisterConnection(string connectionId, string userId)
    {
        _connUser[connectionId] = userId;
        _lastSeen[userId] = DateTimeOffset.Now;
    }

    public void UnregisterConnection(string connectionId, out string? userId, out string[] rooms)
    {
        _connUser.TryRemove(connectionId, out var uid);
        userId = uid;
        rooms = _connRooms.TryRemove(connectionId, out var set) ? set.ToArray() : Array.Empty<string>();

        _typingConnRoom.TryRemove(connectionId, out var typedRoom);
        if (typedRoom is not null)
        {
            lock (_roomTyping)
            {
                if (_roomTyping.TryGetValue(typedRoom, out var s)) s.Remove(userId!);
            }
        }
    }

    public void AddConnectionRoom(string connectionId, string roomKey)
    {
        var set = _connRooms.GetOrAdd(connectionId, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (set)
        {
            set.Add(roomKey);
        }
    }

    public void RemoveConnectionRoom(string connectionId, string roomKey)
    {
        if (_connRooms.TryGetValue(connectionId, out var set))
        {
            lock (set)
            {
                set.Remove(roomKey);
            }
        }
    }

    public ChatMessage AddMessage(string roomKey, ChatUser user, string text)
    {
        var msg = new ChatMessage(Interlocked.Increment(ref _nextId), roomKey, user, text, DateTimeOffset.Now);
        lock (_historyLock)
        {
            var list = _messages.GetOrAdd(roomKey, _ => new List<ChatMessage>());
            list.Add(msg);
            if (list.Count > MaxHistoryPerRoom)
            {
                list.RemoveAt(0);
            }
        }
        _lastSeen[user.Id] = DateTimeOffset.Now;
        return msg;
    }

    public bool SetTyping(string connectionId, string userId, string roomKey, bool isTyping)
    {
        if (isTyping)
        {
            if (_typingConnRoom.TryRemove(connectionId, out var oldRoom) && oldRoom != roomKey)
            {
                DestroyTyping(oldRoom, userId);
            }
            _typingConnRoom[connectionId] = roomKey;
            return GrantTyping(roomKey, userId);
        }

        _typingConnRoom.TryRemove(connectionId, out _);
        return DestroyTyping(roomKey, userId);
    }

    public string[] RoomTypingIds(string roomKey)
    {
        lock (_roomTyping)
        {
            return _roomTyping.TryGetValue(roomKey, out var set) ? set.OrderBy(x => x, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        }
    }

    public string[] RoomOnline(string roomKey)
    {
        var online = _connUser.Values.Distinct().ToHashSet(StringComparer.Ordinal);
        var members = Participants(roomKey).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        return members.Where(online.Contains).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private bool GrantTyping(string roomKey, string userId)
    {
        lock (_roomTyping)
        {
            var set = _roomTyping.GetOrAdd(roomKey, _ => new HashSet<string>(StringComparer.Ordinal));
            return set.Add(userId);
        }
    }

    private bool DestroyTyping(string roomKey, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        lock (_roomTyping)
        {
            if (_roomTyping.TryGetValue(roomKey, out var set))
            {
                return set.Remove(userId);
            }
        }
        return false;
    }

    private void Seed()
    {
        var users = new[]
        {
            new ChatUser("alice", "Alice", 0, "Usually online mid-morning"),
            new ChatUser("bob",   "Bob",   1, "Loves board games and coffee"),
            new ChatUser("carol", "Carol", 2, "Frontend enthusiast"),
            new ChatUser("dave",  "Dave",  3, "Runs the demo servers"),
            new ChatUser("eve",   "Eve",   4, "Here to test SignalR"),
        };
        foreach (var u in users) _users[u.Id] = u;

        var rooms = new[]
        {
            new ChatRoom("general", "General", "Chat with everyone", 0),
            new ChatRoom("announcements", "Announcements", "Team updates", 3),
            new ChatRoom("random", "Random", "Off-topic fun", 4),
        };
        foreach (var r in rooms) _rooms[r.Key] = r;

        var now = DateTimeOffset.Now;
        AddMessage("general", _users["alice"], "Hey everyone! Welcome to the General room 👋");
        AddMessage("general", _users["bob"], "Thanks Alice, happy to be here!");
        AddMessage("general", _users["carol"], "Testing, testing… 1 2 3 🎤");

        AddMessage("announcements", _users["dave"], "The demo servers went live at noon.");
        AddMessage("announcements", _users["eve"], "Reminder: standup moved to 9:30 tomorrow.");
        AddMessage("announcements", _users["alice"], "Got it, thanks!");

        AddMessage("random", _users["bob"], "Anyone watching the new episode tonight?");
        AddMessage("random", _users["carol"], "Yes! But no spoilers in the chat 😄");
        AddMessage("random", _users["eve"], "Seven days since the last incident 🎉");

        // Plausible "last seen" so offline users don't look freshly active at boot.
        _lastSeen[_users["carol"].Id] = now.AddMinutes(-6);
        _lastSeen[_users["dave"].Id] = now.AddHours(-3);
        _lastSeen[_users["eve"].Id] = now.AddDays(-1);
    }
}