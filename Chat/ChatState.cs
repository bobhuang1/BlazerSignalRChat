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
    private readonly Dictionary<string, int> _userConnCount = new(StringComparer.Ordinal);    // userId -> open connections
    private readonly object _presenceLock = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _connRooms = new();        // connectionId -> roomKeys
    private readonly ConcurrentDictionary<string, string> _typingConnRoom = new();            // connectionId -> roomKey
    private readonly ConcurrentDictionary<string, HashSet<string>> _roomTyping = new();       // roomKey -> userIds
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSeen = new();          // userId -> last active

    private long _nextId;

    // One lock for all message state: the per-room lists, _msgRoom and reactions.
    // Every reader copies what it needs while holding it.
    private readonly object _historyLock = new();
    private readonly Dictionary<long, string> _msgRoom = new();          // messageId -> roomKey
    private readonly Dictionary<string, Dictionary<string, int>> _roomUserMessages = new();  // roomKey -> userId -> messages in history

    // The seed data never changes, so sort once instead of on every render.
    private IReadOnlyList<ChatUser> _usersSorted = Array.Empty<ChatUser>();
    private IReadOnlyList<ChatRoom> _roomsSorted = Array.Empty<ChatRoom>();

    public ChatState()
    {
        Seed();
    }

    public IReadOnlyList<ChatUser> Users => _usersSorted;

    public IReadOnlyList<ChatRoom> Rooms => _roomsSorted;

    public ChatUser? GetUser(string? id) => id is null ? null : _users.TryGetValue(id, out var u) ? u : null;

    public ChatRoom? GetRoom(string? key) => key is null ? null : _rooms.TryGetValue(key, out var r) ? r : null;

    public ChatMessage? LastMessage(string roomKey)
    {
        lock (_historyLock)
        {
            return _messages.TryGetValue(roomKey, out var list) && list.Count > 0 ? list[^1] : null;
        }
    }

    public List<ChatMessage> History(string roomKey)
    {
        lock (_historyLock)
        {
            return _messages.TryGetValue(roomKey, out var list) ? new List<ChatMessage>(list) : new();
        }
    }

    /// <summary>The newest <paramref name="limit"/> messages of a room, oldest first.</summary>
    public HistoryPage RecentHistory(string roomKey, int limit = ChatLimits.HistoryPageSize) =>
        PageBefore(roomKey, long.MaxValue, limit);

    /// <summary>Up to <paramref name="limit"/> messages older than <paramref name="beforeId"/>, oldest first.
    /// The limit is clamped to 1..<see cref="ChatLimits.HistoryPageSize"/> so a client cannot ask for the whole store.</summary>
    public HistoryPage PageBefore(string roomKey, long beforeId, int limit = ChatLimits.HistoryPageSize)
    {
        limit = Math.Clamp(limit, 1, ChatLimits.HistoryPageSize);
        lock (_historyLock)
        {
            if (!_messages.TryGetValue(roomKey, out var list)) return new HistoryPage(new(), false);
            // Ids only grow, so the list is sorted by id; find the first message at or after beforeId.
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (list[mid].Id < beforeId) lo = mid + 1; else hi = mid;
            }
            var start = Math.Max(0, lo - limit);
            return new HistoryPage(list.GetRange(start, lo - start), start > 0);
        }
    }

    public List<ChatUser> Participants(string roomKey)
    {
        lock (_historyLock)
        {
            var result = new List<ChatUser>();
            if (!_messages.TryGetValue(roomKey, out var list)) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in list)
            {
                if (seen.Add(m.User.Id)) result.Add(m.User);
            }
            return result;
        }
    }

    public int ConnectionCount => _connUser.Count;

    public string[] OnlineUsers
    {
        get
        {
            lock (_presenceLock)
            {
                return _userConnCount.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public bool IsOnline(string userId)
    {
        lock (_presenceLock)
        {
            return _userConnCount.ContainsKey(userId);
        }
    }

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

    /// <summary>Registers a connection. Returns false (and registers nothing) when the connection
    /// cap is reached. <paramref name="userCameOnline"/> is true when this is the user's first
    /// open connection, so callers only announce real presence changes.</summary>
    public bool TryRegisterConnection(string connectionId, string userId, int maxConnections, out bool userCameOnline)
    {
        userCameOnline = false;
        lock (_presenceLock)
        {
            if (!_connUser.ContainsKey(connectionId))
            {
                if (_connUser.Count >= maxConnections) return false;
                _connUser[connectionId] = userId;
                _userConnCount.TryGetValue(userId, out var n);
                _userConnCount[userId] = n + 1;
                userCameOnline = n == 0;
            }
        }
        _lastSeen[userId] = DateTimeOffset.Now;
        return true;
    }

    /// <summary>Removes a connection. <paramref name="userWentOffline"/> is true when it was the user's last one.</summary>
    public void UnregisterConnection(string connectionId, out string? userId, out string[] rooms, out bool userWentOffline)
    {
        userWentOffline = false;
        string? uid;
        lock (_presenceLock)
        {
            if (_connUser.TryRemove(connectionId, out uid) && _userConnCount.TryGetValue(uid, out var n))
            {
                if (n <= 1)
                {
                    _userConnCount.Remove(uid);
                    userWentOffline = true;
                }
                else
                {
                    _userConnCount[uid] = n - 1;
                }
            }
        }
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

    /// <summary>Adds a room to a connection's subscriptions. Returns true when it is a first-time join.</summary>
    public bool AddConnectionRoom(string connectionId, string roomKey)
    {
        var set = _connRooms.GetOrAdd(connectionId, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (set)
        {
            return set.Add(roomKey);
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
            _msgRoom[msg.Id] = roomKey;
            if (!_roomUserMessages.TryGetValue(roomKey, out var counts))
            {
                _roomUserMessages[roomKey] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
            }
            counts[user.Id] = counts.GetValueOrDefault(user.Id) + 1;
            if (list.Count > MaxHistoryPerRoom)
            {
                var oldest = list[0];
                _msgRoom.Remove(oldest.Id);
                list.RemoveAt(0);
                if (counts[oldest.User.Id] <= 1) counts.Remove(oldest.User.Id);
                else counts[oldest.User.Id]--;
            }
        }
        _lastSeen[user.Id] = DateTimeOffset.Now;
        return msg;
    }

    /// <summary>Toggles a user's reaction on a message. Returns the room and the
    /// resulting reacting-user ids for that emoji (empty array = reaction removed).</summary>
    public (string RoomKey, string[] UserIds)? ToggleReaction(long messageId, string userId, string emoji)
    {
        lock (_historyLock)
        {
            if (!_msgRoom.TryGetValue(messageId, out var roomKey)) return null;
            if (!_messages.TryGetValue(roomKey, out var list)) return null;
            var index = list.FindIndex(m => m.Id == messageId);
            if (index < 0) return null;
            var msg = list[index];

            // Cap distinct reactions per message so one message cannot grow without bound.
            if (!msg.Reactions.ContainsKey(emoji) && msg.Reactions.Count >= ChatLimits.MaxReactionKindsPerMessage) return null;

            var set = msg.Reactions.TryGetValue(emoji, out var current)
                ? new HashSet<string>(current, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            if (!set.Remove(userId)) set.Add(userId);
            var ids = set.OrderBy(x => x, StringComparer.Ordinal).ToArray();

            // Replace the message instead of mutating it (see ChatMessage.Reactions).
            var reactions = new Dictionary<string, string[]>(msg.Reactions, StringComparer.Ordinal);
            if (ids.Length == 0) reactions.Remove(emoji);
            else reactions[emoji] = ids;
            list[index] = msg with { Reactions = reactions };

            return (roomKey, ids);
        }
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
        string[] members;
        lock (_historyLock)
        {
            members = _roomUserMessages.TryGetValue(roomKey, out var counts) ? counts.Keys.ToArray() : Array.Empty<string>();
        }
        lock (_presenceLock)
        {
            return members.Where(_userConnCount.ContainsKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
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
            new ChatRoom("general", "General", "Chat with everyone", 0, "group"),
            new ChatRoom("announcements", "Announcements", "Team updates", 3, "group"),
            new ChatRoom("random", "Random", "Off-topic fun", 4, "community"),
        };
        foreach (var r in rooms) _rooms[r.Key] = r;
        _usersSorted = users.OrderBy(u => u.Id).ToList();
        _roomsSorted = rooms.OrderBy(r => r.Key).ToList();

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