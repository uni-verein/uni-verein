using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;

namespace UniVerein.Api.Services.LoginNotification;

public class LoginNotificationQueue
{
    public const int MaxQueuedJobs = 1000;

    // Somebody who knows a password can try it again and again. One mail per user in this window is enough to warn
    // the user and keeps the mailbox (and the SMTP server) from being flooded.
    public static readonly TimeSpan MailInterval = TimeSpan.FromMinutes(15);

    private readonly Channel<LoginNotificationJob> _channel = Channel.CreateBounded<LoginNotificationJob>(
        new BoundedChannelOptions(MaxQueuedJobs) { FullMode = BoundedChannelFullMode.Wait });
    private readonly Dictionary<Guid, DateTimeOffset> _lastQueued = new();
    private readonly object _lastQueuedLock = new();
    private readonly TimeProvider _timeProvider;

    public LoginNotificationQueue(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool Enqueue(LoginNotificationJob job)
    {
        if (!TryReserveSlot(job.UserId))
            return false;

        if (_channel.Writer.TryWrite(job))
            return true;

        Log.Warning($"LoginNotificationQueue: Queue is full ({MaxQueuedJobs} jobs), mail dropped.");
        return false;
    }

    public ValueTask<LoginNotificationJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public void Clear()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }

        lock (_lastQueuedLock)
            _lastQueued.Clear();
    }

    private bool TryReserveSlot(Guid userId)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_lastQueuedLock)
        {
            if (_lastQueued.Count > 100)
            {
                foreach (Guid id in _lastQueued.Where(x => now - x.Value >= MailInterval).Select(x => x.Key).ToList())
                    _lastQueued.Remove(id);
            }

            if (_lastQueued.TryGetValue(userId, out DateTimeOffset last) && now - last < MailInterval)
                return false;

            _lastQueued[userId] = now;
            return true;
        }
    }
}
