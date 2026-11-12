using BlazerSignalRChat.Chat;

namespace BlazerSignalRChat.Tests;

public class HistoryPagingTests
{
    private static ChatState StateWith(string room, int total)
    {
        var state = new ChatState();
        var user = state.GetUser("alice")!;
        var seeded = state.History(room).Count;
        for (var i = seeded; i < total; i++) state.AddMessage(room, user, "m" + i);
        return state;
    }

    [Fact]
    public void Small_room_returns_everything_and_no_more()
    {
        var state = new ChatState();

        var page = state.RecentHistory("general");

        Assert.Equal(state.History("general").Select(m => m.Id), page.Messages.Select(m => m.Id));
        Assert.False(page.HasMore);
    }

    [Fact]
    public void Join_sends_only_the_newest_page_oldest_first()
    {
        var state = StateWith("general", 120);

        var page = state.RecentHistory("general");

        var all = state.History("general");
        Assert.Equal(ChatLimits.HistoryPageSize, page.Messages.Count);
        Assert.Equal(all.Skip(all.Count - ChatLimits.HistoryPageSize).Select(m => m.Id), page.Messages.Select(m => m.Id));
        Assert.True(page.HasMore);
    }

    [Fact]
    public void Paging_back_walks_the_whole_history_without_gaps_or_repeats()
    {
        var state = StateWith("general", 130);
        var all = state.History("general");

        var collected = new List<long>();
        var page = state.RecentHistory("general");
        collected.InsertRange(0, page.Messages.Select(m => m.Id));
        while (page.HasMore)
        {
            page = state.PageBefore("general", collected[0]);
            Assert.NotEmpty(page.Messages);
            collected.InsertRange(0, page.Messages.Select(m => m.Id));
        }

        Assert.Equal(all.Select(m => m.Id), collected);
    }

    [Fact]
    public void Exact_multiple_of_page_size_reports_no_more_on_the_last_page()
    {
        var state = StateWith("general", 100);

        var first = state.RecentHistory("general");
        var second = state.PageBefore("general", first.Messages[0].Id);

        Assert.True(first.HasMore);
        Assert.Equal(ChatLimits.HistoryPageSize, second.Messages.Count);
        Assert.False(second.HasMore);
    }

    [Fact]
    public void Limit_is_clamped_so_a_client_cannot_pull_the_whole_store()
    {
        var state = StateWith("general", 300);

        Assert.Equal(ChatLimits.HistoryPageSize, state.PageBefore("general", long.MaxValue, 100000).Messages.Count);
        Assert.Single(state.PageBefore("general", long.MaxValue, 0).Messages);
        Assert.Single(state.PageBefore("general", long.MaxValue, -5).Messages);
    }

    [Fact]
    public void Paging_before_the_oldest_message_or_in_an_unknown_room_is_empty()
    {
        var state = StateWith("general", 80);
        var oldest = state.History("general")[0].Id;

        var none = state.PageBefore("general", oldest);
        var unknown = state.PageBefore("nope", long.MaxValue);

        Assert.Empty(none.Messages);
        Assert.False(none.HasMore);
        Assert.Empty(unknown.Messages);
        Assert.False(unknown.HasMore);
    }

    [Fact]
    public void Server_still_stores_500_while_clients_get_pages()
    {
        var state = StateWith("general", 700);

        Assert.Equal(500, state.History("general").Count);
        Assert.Equal(ChatLimits.HistoryPageSize, state.RecentHistory("general").Messages.Count);
    }

    [Fact]
    public void Snapshot_carries_the_capped_history_and_has_more_flag()
    {
        var state = StateWith("general", 200);

        var snapshot = state.SnapshotFor("general");

        Assert.Equal(ChatLimits.HistoryPageSize, snapshot.History.Count);
        Assert.True(snapshot.HasMore);
    }

    [Fact]
    public void History_hub_action_is_rate_limited()
    {
        var limiter = new HubRateLimiter(TimeProvider.System);

        var allowed = 0;
        for (var i = 0; i < 30; i++)
        {
            if (limiter.TryAcquire("c1", HubAction.History)) allowed++;
        }

        Assert.InRange(allowed, 5, 6);
    }
}
