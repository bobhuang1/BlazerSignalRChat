namespace BlazerSignalRChat.Chat;

public sealed record ChatUser(string Id, string DisplayName, int Hue, string Bio);

public sealed record ChatRoom(string Key, string Title, string Description, int Hue);

public sealed record ChatMessage(long Id, string RoomKey, ChatUser User, string Text, DateTimeOffset SentAt);

public sealed record RoomSnapshot(List<ChatMessage> History, List<ChatUser> Participants);