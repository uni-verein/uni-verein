using System;
using System.Collections.Generic;
using UniVerein.Api.ApiRequests;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public class CalendarMailJob
{
    public IReadOnlyCollection<Guid> UserIds { get; }
    public UserSettingType SettingType { get; }
    public EmailRequest Request { get; }

    // The user whose action caused the mail, null for system mails like reminders.
    public Guid? ActorUserId { get; }

    public CalendarMailJob(IReadOnlyCollection<Guid> userIds, UserSettingType settingType, EmailRequest request,
        Guid? actorUserId = null)
    {
        UserIds = userIds;
        SettingType = settingType;
        Request = request;
        ActorUserId = actorUserId;
    }
}
