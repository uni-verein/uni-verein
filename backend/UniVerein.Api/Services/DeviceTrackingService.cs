using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.Security;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services;

public class DeviceTrackingService
{
    public static readonly TimeSpan DeviceRetention = TimeSpan.FromDays(365);

    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly TimeProvider _timeProvider;

    public DeviceTrackingService(AppDbContext db, CryptoService crypto, TimeProvider timeProvider)
    {
        _db = db;
        _crypto = crypto;
        _timeProvider = timeProvider;
    }

    public async Task<bool> IsNewDeviceAsync(Guid userId, HttpContext context)
    {
        string? token = DeviceCookie.TryRead(context.Request);
        string? hash = token == null ? null : _crypto.Hash(token);

        List<string> hashes = await _db.UserDevices
            .Where(d => d.UserId == userId)
            .Select(d => d.TokenHash)
            .ToListAsync();

        return hashes.Count > 0 && (hash == null || !hashes.Contains(hash));
    }

    public async Task RegisterAsync(Guid userId, HttpContext context)
    {
        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            string token = DeviceCookie.TryRead(context.Request) ?? DeviceCookie.CreateToken();
            string hash = _crypto.Hash(token);

            UserDeviceEntity? device = await _db.UserDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.TokenHash == hash);
            if (device == null)
            {
                await _db.UserDevices.AddAsync(new UserDeviceEntity { UserId = userId, TokenHash = hash, LastSeenAt = now });
                await _db.UserDevices.Where(d => d.LastSeenAt < now - DeviceRetention).ExecuteDeleteAsync();
            }
            else
            {
                device.LastSeenAt = now;
            }

            await _db.SaveChangesAsync();
            DeviceCookie.Append(context, token, now);
        }
        catch (Exception ex)
        {
            Log.Error("DeviceTrackingService: RegisterAsync -> Registering a device of user {UserId} threw {ExceptionType}.",
                userId, ex.GetType().Name);
        }
    }
}
