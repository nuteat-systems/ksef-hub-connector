using Connector.Shared.Data;

namespace Connector.Tests;

public sealed class SqlSessionIdleTrackerTests
{
    [Fact]
    public void ResolveIdleTimeout_uses_default_when_null()
    {
        var timeout = SqlSessionIdleTracker.ResolveIdleTimeout(null);
        Assert.Equal(TimeSpan.FromSeconds(SqlSessionIdleTracker.DefaultIdleTimeoutSeconds), timeout);
    }

    [Fact]
    public void ResolveIdleTimeout_zero_or_negative_disables_idle_cleanup()
    {
        Assert.Equal(TimeSpan.Zero, SqlSessionIdleTracker.ResolveIdleTimeout(0));
        Assert.Equal(TimeSpan.Zero, SqlSessionIdleTracker.ResolveIdleTimeout(-5));
    }

    [Fact]
    public void IsIdle_false_when_timeout_disabled()
    {
        var lastUsed = DateTimeOffset.Parse("2026-07-17T10:00:00Z");
        var now = lastUsed.AddHours(2);
        Assert.False(SqlSessionIdleTracker.IsIdle(lastUsed, now, TimeSpan.Zero));
    }

    [Fact]
    public void IsIdle_true_when_elapsed_reaches_timeout()
    {
        var lastUsed = DateTimeOffset.Parse("2026-07-17T10:00:00Z");
        var timeout = TimeSpan.FromMinutes(15);
        Assert.False(SqlSessionIdleTracker.IsIdle(lastUsed, lastUsed.AddMinutes(14), timeout));
        Assert.True(SqlSessionIdleTracker.IsIdle(lastUsed, lastUsed.AddMinutes(15), timeout));
    }

    [Fact]
    public void GetIdleSessionIds_returns_only_expired_sessions()
    {
        var start = DateTimeOffset.Parse("2026-07-17T12:00:00Z");
        var clock = new FakeTimeProvider(start);
        var tracker = new SqlSessionIdleTracker(TimeSpan.FromMinutes(10), clock);

        tracker.Touch("active");
        clock.Advance(TimeSpan.FromMinutes(5));
        tracker.Touch("fresh");
        clock.Advance(TimeSpan.FromMinutes(6));

        var idle = tracker.GetIdleSessionIds();
        Assert.Contains("active", idle);
        Assert.DoesNotContain("fresh", idle);
    }

    [Fact]
    public void GetIdleSessionIds_empty_when_timeout_disabled()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-17T12:00:00Z"));
        var tracker = new SqlSessionIdleTracker(TimeSpan.Zero, clock);
        tracker.Touch("s1");
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Empty(tracker.GetIdleSessionIds());
    }

    [Fact]
    public void Remove_excludes_session_from_idle_list()
    {
        var start = DateTimeOffset.Parse("2026-07-17T12:00:00Z");
        var clock = new FakeTimeProvider(start);
        var tracker = new SqlSessionIdleTracker(TimeSpan.FromMinutes(1), clock);
        tracker.Touch("s1");
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Contains("s1", tracker.GetIdleSessionIds());
        tracker.Remove("s1");
        Assert.Empty(tracker.GetIdleSessionIds());
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
