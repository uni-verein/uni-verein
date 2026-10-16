using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;

namespace UniVerein.Api.Services.Calendar;

public class CalendarMailQueue
{
    // Upper bound of waiting jobs, protects the memory when mails are produced faster than they can be sent.
    public const int MaxQueuedJobs = 1000;

    // Mails caused by one user's actions (invitations, changes, deletions) within MailLimitWindow. Further jobs are
    // dropped, the in-app notifications still exist. System mails like reminders are not limited.
    public const int MaxJobsPerActor = 30;
    public static readonly TimeSpan MailLimitWindow = TimeSpan.FromHours(1);

    private readonly Channel<CalendarMailJob> _channel = Channel.CreateBounded<CalendarMailJob>(
        new BoundedChannelOptions(MaxQueuedJobs) { FullMode = BoundedChannelFullMode.Wait });

    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _actorJobs = new();
    private readonly object _actorJobsLock = new();
    private readonly TimeProvider _timeProvider;

    public CalendarMailQueue(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    // Returns false when the job was not queued because of a limit.
    public bool Enqueue(CalendarMailJob job)
    {
        if (job.UserIds.Count == 0)
            return false;

        if (job.ActorUserId is Guid actorId && !TryReserveActorSlot(actorId))
        {
            Log.Warning($"CalendarMailQueue: User {actorId} reached the limit of {MaxJobsPerActor} calendar mails per hour, mail dropped.");
            return false;
        }

        if (_channel.Writer.TryWrite(job))
            return true;

        Log.Warning($"CalendarMailQueue: Queue is full ({MaxQueuedJobs} jobs), mail dropped.");
        return false;
    }

    public ValueTask<CalendarMailJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public bool TryDequeue([MaybeNullWhen(false)] out CalendarMailJob job)
    {
        return _channel.Reader.TryRead(out job);
    }

    private bool TryReserveActorSlot(Guid actorId)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_actorJobsLock)
        {
            if (_actorJobs.Count > 100)
                RemoveExpiredActors(now);

            if (!_actorJobs.TryGetValue(actorId, out Queue<DateTimeOffset>? times))
            {
                times = new Queue<DateTimeOffset>();
                _actorJobs[actorId] = times;
            }

            while (times.Count > 0 && now - times.Peek() >= MailLimitWindow)
                times.Dequeue();

            if (times.Count >= MaxJobsPerActor)
                return false;

            times.Enqueue(now);
            return true;
        }
    }

    private void RemoveExpiredActors(DateTimeOffset now)
    {
        foreach (Guid id in _actorJobs.Where(x => x.Value.Count == 0 || now - x.Value.Last() >= MailLimitWindow)
                     .Select(x => x.Key).ToList())
            _actorJobs.Remove(id);
    }
}
