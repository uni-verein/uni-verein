using MailKit.Security;
using Shouldly;
using UniVerein.Api.Helper;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class MailTlsHelperTests
{
    [Theory]
    [InlineData(587, 465)]
    [InlineData(25, 465)]
    [InlineData(2525, 465)]
    [InlineData(143, 993)]
    [InlineData(587, 993)]
    public void ResolveSocketOptions_EncryptionEnabled_RequiresStartTls_OnOtherPorts(int port, int implicitTlsPort)
    {
        MailTlsHelper.ResolveSocketOptions(true, port, implicitTlsPort).ShouldBe(SecureSocketOptions.StartTls);
    }

    [Theory]
    [InlineData(465, 465)]
    [InlineData(993, 993)]
    public void ResolveSocketOptions_EncryptionEnabled_UsesImplicitTls_OnImplicitTlsPort(int port,
        int implicitTlsPort)
    {
        MailTlsHelper.ResolveSocketOptions(true, port, implicitTlsPort).ShouldBe(SecureSocketOptions.SslOnConnect);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(465)]
    [InlineData(587)]
    [InlineData(993)]
    public void ResolveSocketOptions_EncryptionDisabled_IsNone(int port)
    {
        MailTlsHelper.ResolveSocketOptions(false, port, MailTlsHelper.SmtpImplicitTlsPort)
            .ShouldBe(SecureSocketOptions.None);
    }

    [Fact]
    public void ResolveSocketOptions_NeverReturnsOpportunisticOptions()
    {
        foreach (bool enableSsl in new[] { true, false })
            foreach (int port in new[] { 25, 143, 465, 587, 993, 2525 })
            {
                SecureSocketOptions option = MailTlsHelper.ResolveSocketOptions(enableSsl, port, 465);

                option.ShouldNotBe(SecureSocketOptions.Auto);
                option.ShouldNotBe(SecureSocketOptions.StartTlsWhenAvailable);
            }
    }

    [Fact]
    public void IsTlsUnavailable_RecognizesMissingStartTlsAndHandshakeErrors()
    {
        MailTlsHelper.IsTlsUnavailable(new NotSupportedException("no STARTTLS")).ShouldBeTrue();
        MailTlsHelper.IsTlsUnavailable(new SslHandshakeException("bad certificate")).ShouldBeTrue();
        MailTlsHelper.IsTlsUnavailable(new IOException("connection reset")).ShouldBeFalse();
    }
}
