namespace BlazerSignalRChat.Chat;

public sealed record ChatUser(string Id, string DisplayName, int Hue, string Bio);

public sealed record ChatRoom(string Key, string Title, string Description, int Hue, string Kind = "group");

public sealed record ChatMessage(long Id, string RoomKey, ChatUser User, string Text, DateTimeOffset SentAt)
{
    /// <summary>emoji -> user ids that reacted with it. Setter needed so SignalR's
    /// JSON deserializer can populate it on the receiving client.</summary>
    public Dictionary<string, HashSet<string>> Reactions { get; set; } = new();
}

public sealed record RoomSnapshot(List<ChatMessage> History, List<ChatUser> Participants);