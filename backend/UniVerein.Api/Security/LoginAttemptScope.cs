namespace UniVerein.Api.Security;

public enum LoginAttemptScope
{
    Login,
    TwoFactorLogin,
    TwoFactorSetup,
    TwoFactorEnable,
    TwoFactorDisable
}
