using System;
using System.Collections.Concurrent;

namespace UniVerein.Api.Security;

public class AccountLockoutTracker
{
    public const int MaxFailures = 5;

    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BaseBlock = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxBlock = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan LevelMemory = TimeSpan.FromHours(24);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<Guid, AccountLockoutState> _states = new();

    public AccountLockoutTracker(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public TimeSpan? GetRemainingLockout(Guid userId)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_states.TryGetValue(userId, out AccountLockoutState? state) && state.BlockedUntil.HasValue && state.BlockedUntil.Value > now)
            return state.BlockedUntil.Value - now;

        return null;
    }

    public TimeSpan? ReserveAttempt(Guid userId, out TimeSpan? startedBlock)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        TimeSpan? remaining = null;
        TimeSpan? started = null;

        _states.AddOrUpdate(
            userId,
            _ => Next(null, now, out remaining, out started),
            (_, existing) => Next(existing, now, out remaining, out started));

        startedBlock = started;
        return remaining;
    }

    public void Reset(Guid userId)
    {
        _states.TryRemove(userId, out _);
    }

    public void Clear()
    {
        _states.Clear();
    }

    private static AccountLockoutState Next(AccountLockoutState? existing, DateTimeOffset now, out TimeSpan? remaining, out TimeSpan? startedBlock)
    {
        remaining = null;
        startedBlock = null;
        if (existing == null || now - existing.LastFailure > LevelMemory)
            existing = new AccountLockoutState(0, now, 0, now, null);

        if (existing.BlockedUntil.HasValue && existing.BlockedUntil.Value > now)
        {
            remaining = existing.BlockedUntil.Value - now;
            return existing;
        }

        int failures = now - existing.WindowStart > FailureWindow ? 1 : existing.Failures + 1;
        DateTimeOffset windowStart = failures == 1 ? now : existing.WindowStart;
        if (failures < MaxFailures)
            return new AccountLockoutState(failures, windowStart, existing.Level, now, null);

        int level = existing.Level + 1;
        TimeSpan block = TimeSpan.FromTicks(Math.Min(BaseBlock.Ticks * (1L << Math.Min(level - 1, 10)), MaxBlock.Ticks));
        startedBlock = block;
        return new AccountLockoutState(0, now, level, now, now + block);
    }
}
