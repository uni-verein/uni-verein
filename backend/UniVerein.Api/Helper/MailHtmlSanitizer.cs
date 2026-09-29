using System;
using System.Collections.Generic;
using Ganss.Xss;

namespace UniVerein.Api.Helper;

public static class MailHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return Sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        HtmlSanitizer sanitizer = new(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "strong", "b", "em", "i", "u", "s", "code", "pre", "blockquote",
                "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "hr", "a", "img", "span"
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "target", "rel", "src", "alt", "title", "style"
            },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "color", "text-align"
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "http", "https", "mailto", "cid", "data"
            },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "src"
            }
        });

        sanitizer.FilterUrl += (_, e) =>
        {
            if (!e.OriginalUrl.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return;

            bool isInlineImage = e.Tag.NodeName.Equals("IMG", StringComparison.OrdinalIgnoreCase) &&
                                 e.OriginalUrl.TrimStart().StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) &&
                                 !e.OriginalUrl.TrimStart().StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase);

            if (!isInlineImage)
                e.SanitizedUrl = null;
        };

        return sanitizer;
    }
}
