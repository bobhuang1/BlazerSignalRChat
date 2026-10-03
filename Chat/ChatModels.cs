namespace BlazerSignalRChat.Chat;

public sealed record ChatUser(string Id, string DisplayName, int Hue, string Bio);

public sealed record ChatRoom(string Key, string Title, string Description, int Hue, string Kind = "group");

public sealed record ChatMessage(long Id, string RoomKey, ChatUser User, string Text, DateTimeOffset SentAt)
{
    /// <summary>emoji -> user ids that reacted with it. Never mutated: a reaction toggle
    /// stores a new message with a new map, so a message that SignalR is serializing (or a
    /// component is rendering) never changes underneath it. <c>init</c> lets the JSON
    /// deserializer populate it on the receiving client.</summary>
    public IReadOnlyDictionary<string, string[]> Reactions { get; init; } = new Dictionary<string, string[]>();
}

public sealed record RoomSnapshot(List<ChatMessage> History, List<ChatUser> Participants);