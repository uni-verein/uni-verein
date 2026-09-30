using System.Security.Claims;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public static class IbanMasking
{
    public const char MaskChar = '*';
    public const int VisibleChars = 6;

    public static bool IsPrivileged(ClaimsPrincipal? user)
    {
        string? role = user?.FindFirst(ClaimTypes.Role)?.Value;
        return role is nameof(UserRole.ADMIN) or nameof(UserRole.FINANCIAL_MANAGER);
    }

    public static string Mask(string? iban)
    {
        if (string.IsNullOrEmpty(iban))
            return string.Empty;
        if (iban.Length <= VisibleChars)
            return new string(MaskChar, iban.Length);

        return new string(MaskChar, iban.Length - VisibleChars) + iban[^VisibleChars..];
    }

    public static string MaskUnlessPrivileged(string? iban, bool isPrivileged)
    {
        return isPrivileged ? iban ?? string.Empty : Mask(iban);
    }

    public static string? IgnoreMasked(string? iban)
    {
        return iban != null && iban.Contains(MaskChar) ? null : iban;
    }
}
