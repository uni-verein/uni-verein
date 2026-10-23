using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace UniVerein.Api.Security;

public class LoginAttemptTracker
{
    private const int MaxEntries = 50_000;

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Attempt> _attempts = new();
    private DateTimeOffset _nextPurge = DateTimeOffset.MinValue;

    public LoginAttemptTracker(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    private sealed record Attempt(int FailedAttempts, DateTimeOffset LastFailedAttempt, DateTimeOffset? BlockedUntil);

    public TimeSpan? GetRemainingLockout(string username, string clientIp)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_attempts.TryGetValue(BuildKey(username, clientIp), out Attempt? attempt)
            && attempt.BlockedUntil.HasValue && attempt.BlockedUntil.Value > now)
        {
            return attempt.BlockedUntil.Value - now;
        }

        return null;
    }

    public void RegisterFailure(string username, string clientIp)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        PurgeExpired(now);

        string key = BuildKey(username, clientIp);
        if (_attempts.Count >= MaxEntries && !_attempts.ContainsKey(key))
        {
            Log.Warning("LoginAttemptTracker: RegisterFailure -> Tracking table is full, failed login is not tracked.");
            return;
        }

        _attempts.AddOrUpdate(
            key,
            _ => NextAttempt(0, null, now),
            (_, existing) => NextAttempt(existing.FailedAttempts, existing.LastFailedAttempt, now));
    }

    public void Reset(string username, string clientIp)
    {
        _attempts.TryRemove(BuildKey(username, clientIp), out _);
    }

    public void Clear()
    {
        _attempts.Clear();
    }

    private static Attempt NextAttempt(int failedAttempts, DateTimeOffset? lastFailedAttempt, DateTimeOffset now)
    {
        int effective = LoginGuard.GetEffectiveFailedAttempts(failedAttempts, lastFailedAttempt, now) + 1;
        return new Attempt(effective, now, LoginGuard.GetLockoutReleaseTime(effective, now));
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        if (now < _nextPurge && _attempts.Count < MaxEntries)
            return;

        _nextPurge = now.AddSeconds(LoginGuard.LockoutWindowSeconds);
        foreach (KeyValuePair<string, Attempt> entry in _attempts.Where(x => IsExpired(x.Value, now)))
            _attempts.TryRemove(entry);
    }

    private static bool IsExpired(Attempt attempt, DateTimeOffset now)
    {
        return (now - attempt.LastFailedAttempt).TotalSeconds > LoginGuard.LockoutWindowSeconds
               && (!attempt.BlockedUntil.HasValue || attempt.BlockedUntil.Value <= now);
    }

    private static string BuildKey(string username, string clientIp)
    {
        string usernameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(username)));
        return $"{NormalizeClientIp(clientIp)}|{usernameHash}";
    }

    // A single IPv6 subscriber gets at least a /64, so every address of it counts as the same client.
    private static string NormalizeClientIp(string clientIp)
    {
        if (!IPAddress.TryParse(clientIp, out IPAddress? address))
            return clientIp;

        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4().ToString();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        byte[] bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
