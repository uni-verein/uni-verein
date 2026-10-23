using System;
using System.IO;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace UniVerein.Api.Services;

public class SanitizingTextFormatter : ITextFormatter
{
    private const string ContinuationIndent = "    ";
    private readonly MessageTemplateTextFormatter _messageFormatter;

    public SanitizingTextFormatter(string outputTemplate)
    {
        _messageFormatter = new MessageTemplateTextFormatter(outputTemplate);
    }

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using StringWriter message = new();
        _messageFormatter.Format(logEvent, message);

        output.Write(Escape(message.ToString(), escapeLineBreaks: true));
        output.Write(Environment.NewLine);

        if (logEvent.Exception == null)
            return;

        string exception = logEvent.Exception.ToString().ReplaceLineEndings(Environment.NewLine + ContinuationIndent);
        output.Write(Escape(exception, escapeLineBreaks: false));
        output.Write(Environment.NewLine);
    }

    public static string Escape(string value, bool escapeLineBreaks)
    {
        StringBuilder? builder = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            string? replacement = c switch
            {
                '\r' when escapeLineBreaks => "\\r",
                '\n' when escapeLineBreaks => "\\n",
                '\r' or '\n' => null,
                _ when IsControl(c) => $"\\u{(int)c:x4}",
                _ => null
            };

            if (replacement == null)
            {
                builder?.Append(c);
                continue;
            }

            builder ??= new StringBuilder(value.Length + 8).Append(value, 0, i);
            builder.Append(replacement);
        }

        return builder?.ToString() ?? value;
    }

    private static bool IsControl(char c)
    {
        return (char.IsControl(c) && c != '\t') || c == '\u2028' || c == '\u2029';
    }
}
