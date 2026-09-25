namespace BlazerSignalRChat.Services;

/// <summary>
/// Per-circuit (per-browser-tab) state. Each tab acts as its own demo user,
/// which is what lets you open several tabs and chat between users.
/// </summary>
public sealed class SessionState
{
    public string CurrentUserId { get; set; } = "alice";

    public HashSet<string> ArchivedRooms { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Unread { get; } = new(StringComparer.Ordinal);
}