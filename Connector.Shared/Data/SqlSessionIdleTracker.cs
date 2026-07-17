using System.Collections.Concurrent;

namespace Connector.Shared.Data;

/// <summary>
/// Tracks last-used timestamps for sticky SQL sessions and decides which ones are idle.
/// </summary>
public sealed class SqlSessionIdleTracker
{
    public const int DefaultIdleTimeoutSeconds = 900;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastUsedUtc =
        new(StringComparer.Ordinal);

    private readonly TimeSpan _idleTimeout;
    private readonly TimeProvider _timeProvider;

    public SqlSessionIdleTracker(TimeSpan idleTimeout, TimeProvider? timeProvider = null)
    {
        _idleTimeout = idleTimeout < TimeSpan.Zero ? TimeSpan.Zero : idleTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TimeSpan IdleTimeout => _idleTimeout;

    public bool IdleTimeoutEnabled => _idleTimeout > TimeSpan.Zero;

    public static TimeSpan ResolveIdleTimeout(int? idleTimeoutSeconds)
    {
        var seconds = idleTimeoutSeconds ?? DefaultIdleTimeoutSeconds;
        if (seconds <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    public void Touch(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        _lastUsedUtc[sessionId] = _timeProvider.GetUtcNow();
    }

    public void Remove(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        _lastUsedUtc.TryRemove(sessionId, out _);
    }

    public IReadOnlyList<string> GetIdleSessionIds()
    {
        if (!IdleTimeoutEnabled || _lastUsedUtc.IsEmpty)
        {
            return Array.Empty<string>();
        }

        var now = _timeProvider.GetUtcNow();
        var idle = new List<string>();
        foreach (var pair in _lastUsedUtc)
        {
            if (IsIdle(pair.Value, now, _idleTimeout))
            {
                idle.Add(pair.Key);
            }
        }

        return idle;
    }

    public static bool IsIdle(DateTimeOffset lastUsedUtc, DateTimeOffset nowUtc, TimeSpan idleTimeout)
    {
        if (idleTimeout <= TimeSpan.Zero)
        {
            return false;
        }

        return nowUtc - lastUsedUtc >= idleTimeout;
    }
}
