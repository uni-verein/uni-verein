using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OtpNet;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services;

public class TwoFactorService
{
    public const int RecoveryCodeCount = 10;
    public const int RecoveryCodeLength = 12;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int SecretBytes = 20;
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly IConfiguration _config;
    private readonly CryptoService _crypto;
    private readonly TimeProvider _timeProvider;
    private readonly AppDbContext _db;

    public TwoFactorService(IConfiguration config, CryptoService crypto, TimeProvider timeProvider, AppDbContext db)
    {
        _config = config;
        _crypto = crypto;
        _timeProvider = timeProvider;
        _db = db;
    }

    public static string GenerateSecret()
    {
        return Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretBytes));
    }

    public string BuildOtpAuthUri(string username, string secret)
    {
        string issuer = _config["TwoFactor:Issuer"] ?? "UniVerein";
        return new OtpUri(OtpType.Totp, secret, username, issuer, OtpHashMode.Sha1, Digits, StepSeconds).ToString();
    }

    public static string? NormalizeTotpCode(string? code)
    {
        string normalized = (code ?? string.Empty).Replace(" ", string.Empty);
        return normalized.Length == Digits && normalized.All(char.IsAsciiDigit) ? normalized : null;
    }

    public static bool IsTotpFormat(string? code) => NormalizeTotpCode(code) != null;

    public bool TryVerifyCode(string secret, string? code, long? lastUsedStep, out long matchedStep)
    {
        matchedStep = 0;
        string? normalized = NormalizeTotpCode(code);
        if (normalized == null)
            return false;

        Totp totp = new(Base32Encoding.ToBytes(secret), StepSeconds, OtpHashMode.Sha1, Digits);
        if (!totp.VerifyTotp(_timeProvider.GetUtcNow().UtcDateTime, normalized, out long step, new VerificationWindow(previous: 1, future: 1)))
            return false;

        if (lastUsedStep.HasValue && step <= lastUsedStep.Value)
            return false;

        matchedStep = step;
        return true;
    }

    public async Task<List<string>> ReplaceRecoveryCodesAsync(Guid userId)
    {
        await DeleteRecoveryCodesAsync(userId);

        List<string> plain = new();
        for (int i = 0; i < RecoveryCodeCount; i++)
        {
            string raw = new(RandomNumberGenerator.GetItems<char>(Base32Alphabet, RecoveryCodeLength));
            plain.Add($"{raw[..4]}-{raw[4..8]}-{raw[8..]}");
        }

        _db.UserRecoveryCodes.AddRange(plain.Select(code => new UserRecoveryCodeEntity
        {
            UserId = userId,
            CodeHash = HashRecoveryCode(code)
        }));
        await _db.SaveChangesAsync();

        return plain;
    }

    public async Task<bool> TryConsumeRecoveryCodeAsync(Guid userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        string hash = HashRecoveryCode(code);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        int updated = await _db.UserRecoveryCodes
            .Where(x => x.UserId == userId && x.CodeHash == hash && x.UsedAt == null && x.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.UsedAt, now));

        return updated > 0;
    }

    public async Task DeleteRecoveryCodesAsync(Guid userId)
    {
        await _db.UserRecoveryCodes.Where(x => x.UserId == userId).ExecuteDeleteAsync();
    }

    public string CreateChallenge(UserEntity user)
    {
        long expires = _timeProvider.GetUtcNow().Add(ChallengeLifetime).ToUnixTimeSeconds();
        string payload = $"{user.Id:N}.{user.SessionVersion}.{expires}";
        return $"{WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{WebEncoders.Base64UrlEncode(Sign(payload))}";
    }

    public (Guid UserId, int SessionVersion)? ValidateChallenge(string? challenge)
    {
        string[] parts = (challenge ?? string.Empty).Split('.');
        if (parts.Length != 2)
            return null;

        try
        {
            string payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[0]));
            byte[] signature = WebEncoders.Base64UrlDecode(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
                return null;

            string[] fields = payload.Split('.');
            if (fields.Length != 3 || !int.TryParse(fields[1], out int sessionVersion)
                || !long.TryParse(fields[2], out long expires)
                || expires < _timeProvider.GetUtcNow().ToUnixTimeSeconds())
                return null;

            return Guid.TryParseExact(fields[0], "N", out Guid userId) ? (userId, sessionVersion) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public string EncryptSecret(string secret)
    {
        return _crypto.Encrypt(secret);
    }

    public string? DecryptSecret(string? encrypted)
    {
        return _crypto.Decrypt(encrypted);
    }

    private string HashRecoveryCode(string code)
    {
        return _crypto.Hash(code.Replace("-", string.Empty));
    }

    private byte[] Sign(string payload)
    {
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes($"2fa-challenge:{payload}"));
    }

}
