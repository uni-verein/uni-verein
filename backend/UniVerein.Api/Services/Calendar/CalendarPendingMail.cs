using System;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Models;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

internal class CalendarPendingMail
{
    public Guid UserId { get; }
    public UserSettingType SettingType { get; }
    public Recipient Recipient { get; }
    public EmailRequest Request { get; }

    public CalendarPendingMail(Guid userId, UserSettingType settingType, Recipient recipient, EmailRequest request)
    {
        UserId = userId;
        SettingType = settingType;
        Recipient = recipient;
        Request = request;
    }
}
