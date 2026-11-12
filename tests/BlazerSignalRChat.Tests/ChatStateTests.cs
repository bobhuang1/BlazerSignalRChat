using BlazerSignalRChat.Chat;

namespace BlazerSignalRChat.Tests;

public class ChatStateTests
{
    private const int Cap = 100;

    [Fact]
    public void Presence_is_per_user_not_per_connection()
    {
        var state = new ChatState();

        Assert.True(state.TryRegisterConnection("c1", "alice", Cap, out var firstCameOnline));
        Assert.True(state.TryRegisterConnection("c2", "alice", Cap, out var secondCameOnline));
        Assert.True(firstCameOnline);
        Assert.False(secondCameOnline);
        Assert.True(state.IsOnline("alice"));
        Assert.Equal(new[] { "alice" }, state.OnlineUsers);

        state.UnregisterConnection("c1", out _, out _, out var wentOfflineAfterFirst);
        Assert.False(wentOfflineAfterFirst);
        Assert.True(state.IsOnline("alice"));

        state.UnregisterConnection("c2", out var userId, out _, out var wentOfflineAfterLast);
        Assert.True(wentOfflineAfterLast);
        Assert.Equal("alice", userId);
        Assert.False(state.IsOnline("alice"));
        Assert.Empty(state.OnlineUsers);
    }

    [Fact]
    public void Unregistering_an_unknown_connection_changes_nothing()
    {
        var state = new ChatState();
        state.TryRegisterConnection("c1", "alice", Cap, out _);

        state.UnregisterConnection("nope", out var userId, out _, out var wentOffline);

        Assert.Null(userId);
        Assert.False(wentOffline);
        Assert.True(state.IsOnline("alice"));
    }

    [Fact]
    public void Connection_cap_rejects_extra_connections()
    {
        var state = new ChatState();
        Assert.True(state.TryRegisterConnection("c1", "alice", 2, out _));
        Assert.True(state.TryRegisterConnection("c2", "bob", 2, out _));

        Assert.False(state.TryRegisterConnection("c3", "carol", 2, out var cameOnline));
        Assert.False(cameOnline);
        Assert.False(state.IsOnline("carol"));
        Assert.Equal(2, state.ConnectionCount);

        state.UnregisterConnection("c1", out _, out _, out _);
        Assert.True(state.TryRegisterConnection("c3", "carol", 2, out _));
    }

    [Fact]
    public void RoomOnline_lists_only_online_participants_of_the_room()
    {
        var state = new ChatState();
        // Seed data: general has alice, bob, carol; announcements has dave, eve, alice; random has bob, carol, eve.
        state.TryRegisterConnection("c1", "bob", Cap, out _);
        state.TryRegisterConnection("c2", "dave", Cap, out _);
        state.TryRegisterConnection("c3", "bob", Cap, out _);

        Assert.Equal(new[] { "bob" }, state.RoomOnline("general"));
        Assert.Equal(new[] { "dave" }, state.RoomOnline("announcements"));
        Assert.Equal(new[] { "bob" }, state.RoomOnline("random"));
        Assert.Empty(state.RoomOnline("no-such-room"));
    }

    [Fact]
    public void Participants_keep_first_message_order_without_duplicates()
    {
        var state = new ChatState();
        var carol = state.GetUser("carol")!;
        state.AddMessage("general", carol, "again");

        var ids = state.Participants("general").Select(u => u.Id).ToArray();

        Assert.Equal(new[] { "alice", "bob", "carol" }, ids);
    }

    [Fact]
    public void History_is_capped_and_a_user_drops_out_of_the_room_when_their_last_message_is_trimmed()
    {
        var state = new ChatState();
        var eve = state.GetUser("eve")!;
        // "random" starts with bob, carol and eve (one message each). Flood it as eve.
        for (var i = 0; i < 500; i++) state.AddMessage("random", eve, "spam " + i);

        Assert.Equal(500, state.History("random").Count);
        Assert.Equal(new[] { "eve" }, state.Participants("random").Select(u => u.Id));

        state.TryRegisterConnection("c1", "bob", Cap, out _);
        state.TryRegisterConnection("c2", "eve", Cap, out _);
        Assert.Equal(new[] { "eve" }, state.RoomOnline("random"));
    }

    [Fact]
    public void Reaction_toggles_on_and_off()
    {
        var state = new ChatState();
        var id = state.History("general")[0].Id;

        var on = state.ToggleReaction(id, "bob", "+1");
        Assert.Equal(("general", new[] { "bob" }), on);

        var off = state.ToggleReaction(id, "bob", "+1");
        Assert.Equal(("general", Array.Empty<string>()), off);
        Assert.Empty(state.History("general")[0].Reactions);
    }

    [Fact]
    public void Reaction_kinds_per_message_are_capped_but_existing_ones_still_toggle()
    {
        var state = new ChatState();
        var id = state.History("general")[0].Id;
        for (var i = 0; i < ChatLimits.MaxReactionKindsPerMessage; i++)
        {
            Assert.NotNull(state.ToggleReaction(id, "bob", "e" + i));
        }

        Assert.Null(state.ToggleReaction(id, "bob", "one-too-many"));
        Assert.Equal(ChatLimits.MaxReactionKindsPerMessage, state.History("general")[0].Reactions.Count);

        // An emoji that is already present can still be toggled by another user or removed.
        Assert.NotNull(state.ToggleReaction(id, "carol", "e0"));
        Assert.NotNull(state.ToggleReaction(id, "bob", "e0"));
        Assert.NotNull(state.ToggleReaction(id, "carol", "e0"));
        Assert.False(state.History("general")[0].Reactions.ContainsKey("e0"));
        // Removing the last user of an emoji frees a slot.
        Assert.NotNull(state.ToggleReaction(id, "bob", "one-too-many"));
    }

    [Fact]
    public void Reaction_on_unknown_message_is_ignored()
    {
        var state = new ChatState();
        Assert.Null(state.ToggleReaction(9999, "bob", "+1"));
    }

    [Fact]
    public void Typing_is_tracked_per_room_and_cleared_on_disconnect()
    {
        var state = new ChatState();
        state.TryRegisterConnection("c1", "alice", Cap, out _);

        Assert.True(state.SetTyping("c1", "alice", "general", true));
        Assert.False(state.SetTyping("c1", "alice", "general", true));
        Assert.Equal(new[] { "alice" }, state.RoomTypingIds("general"));

        state.UnregisterConnection("c1", out _, out _, out _);
        Assert.Empty(state.RoomTypingIds("general"));
    }

    [Fact]
    public void Users_and_rooms_are_sorted_and_stable()
    {
        var state = new ChatState();

        Assert.Equal(new[] { "alice", "bob", "carol", "dave", "eve" }, state.Users.Select(u => u.Id));
        Assert.Equal(new[] { "announcements", "general", "random" }, state.Rooms.Select(r => r.Key));
        Assert.Same(state.Users, state.Users);
    }
}

public class TypingAndMembershipTests
{
    [Fact]
    public void One_tab_stopping_does_not_clear_the_other_tabs_typing()
    {
        var state = new ChatState();
        Assert.True(state.SetTyping("c1", "alice", "general", true));
        Assert.False(state.SetTyping("c2", "alice", "general", true));

        Assert.False(state.SetTyping("c1", "alice", "general", false));
        Assert.Equal(new[] { "alice" }, state.RoomTypingIds("general"));

        Assert.True(state.SetTyping("c2", "alice", "general", false));
        Assert.Empty(state.RoomTypingIds("general"));
    }

    [Fact]
    public void Disconnect_clears_only_that_connections_typing()
    {
        var state = new ChatState();
        state.TryRegisterConnection("c1", "alice", 10, out _);
        state.TryRegisterConnection("c2", "alice", 10, out _);
        state.SetTyping("c1", "alice", "general", true);
        state.SetTyping("c2", "alice", "general", true);

        state.UnregisterConnection("c1", out _, out _, out _);

        Assert.Equal(new[] { "alice" }, state.RoomTypingIds("general"));
    }

    [Fact]
    public void IsInRoom_follows_join_and_leave()
    {
        var state = new ChatState();
        Assert.False(state.IsInRoom("c1", "general"));
        state.AddConnectionRoom("c1", "general");
        Assert.True(state.IsInRoom("c1", "general"));
        state.RemoveConnectionRoom("c1", "general");
        Assert.False(state.IsInRoom("c1", "general"));
    }
}
