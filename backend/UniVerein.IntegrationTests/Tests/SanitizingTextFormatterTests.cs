using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using UniVerein.Api.Services;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SanitizingTextFormatterTests
{
    private const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}";
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static string Format(string messageTemplate, Exception? exception = null, params object[] values)
    {
        MessageTemplate parsed = new MessageTemplateParser().Parse(messageTemplate);
        List<LogEventProperty> properties = parsed.Tokens.OfType<PropertyToken>()
            .Zip(values, (token, value) => new LogEventProperty(token.PropertyName, new ScalarValue(value)))
            .ToList();

        LogEvent logEvent = new(Timestamp, LogEventLevel.Information, exception, parsed, properties);
        using StringWriter writer = new();
        new SanitizingTextFormatter(Template).Format(logEvent, writer);

        return writer.ToString();
    }

    [Fact]
    public void Format_PlainMessage_IsWrittenAsOneLine()
    {
        // Arrange
        const string message = "Hello world";

        // Act
        string result = Format(message);

        // Assert
        result.ShouldBe("2026-10-19 12:00:00 [INF] Hello world" + Environment.NewLine);
    }

    [Fact]
    public void Format_LineBreakInStructuredProperty_IsEscaped()
    {
        // Arrange
        const string name = "Max\n2026-01-01 00:00:00 [INF] Fake entry";

        // Act
        string result = Format("Enrolled {Name}", null, name);

        // Assert
        result.ShouldBe("2026-10-19 12:00:00 [INF] Enrolled Max\\n2026-01-01 00:00:00 [INF] Fake entry" +
                        Environment.NewLine);
    }

    [Fact]
    public void Format_LineBreakInMessageTemplate_IsEscaped()
    {
        // Arrange
        const string messageTemplate = "Category: A\r\nFake entry";

        // Act
        string result = Format(messageTemplate);

        // Assert
        result.ShouldBe("2026-10-19 12:00:00 [INF] Category: A\\r\\nFake entry" + Environment.NewLine);
        result.TrimEnd().Split(Environment.NewLine).Length.ShouldBe(1);
    }

    [Theory]
    [InlineData("a\u001b[31mred", "a\\u001b[31mred")]
    [InlineData("a\u0085b", "a\\u0085b")]
    [InlineData("a\u2028b", "a\\u2028b")]
    [InlineData("a\u2029b", "a\\u2029b")]
    public void Format_OtherControlCharacters_AreEscaped(string value, string expected)
    {
        // Act
        string result = Format("{Value}", null, value);

        // Assert
        result.ShouldBe($"2026-10-19 12:00:00 [INF] {expected}" + Environment.NewLine);
    }

    [Fact]
    public void Format_Tab_IsKept()
    {
        // Arrange
        const string value = "a\tb";

        // Act
        string result = Format("{Value}", null, value);

        // Assert
        result.ShouldContain("a\tb");
    }

    [Fact]
    public void Format_Exception_ContinuationLinesAreIndented()
    {
        // Arrange
        Exception exception = new InvalidOperationException("boom\n2026-01-01 00:00:00 [INF] Fake entry");

        // Act
        string result = Format("Failed", exception);

        // Assert
        string[] lines = result.TrimEnd().Split(Environment.NewLine);
        lines[0].ShouldBe("2026-10-19 12:00:00 [INF] Failed");
        lines.Length.ShouldBeGreaterThan(1);
        lines[1].ShouldStartWith("System.InvalidOperationException: boom");
        foreach (string line in lines[2..])
            line.ShouldStartWith(" ");
    }

    [Fact]
    public void Format_ExceptionLine_NeverStartsLikeAnEntry()
    {
        // Arrange
        Exception exception = new InvalidOperationException("x\r\n2026-01-01 00:00:00 [INF] Fake entry");

        // Act
        string result = Format("Failed", exception);

        // Assert
        foreach (string line in result.TrimEnd().Split(Environment.NewLine)[1..])
            line.ShouldNotMatch(@"^\d{4}-\d{2}-\d{2} ");
    }
}
