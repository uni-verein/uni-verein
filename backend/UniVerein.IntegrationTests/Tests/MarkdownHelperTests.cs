using UniVerein.Api.Helper;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class MarkdownHelperTests
{
    [Fact]
    public void ToHtml_EscapesRawHtml()
    {
        string html = MarkdownHelper.ToHtml("Release <script>alert(1)</script> <img src=x onerror=alert(1)>");

        html.ShouldNotContain("<script");
        html.ShouldNotContain("<img");
        html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void ToHtml_RendersMarkdown()
    {
        string html = MarkdownHelper.ToHtml("## Changes\n\n- **Fix** login");

        html.ShouldContain("<h2");
        html.ShouldContain("<li><strong>Fix</strong> login</li>");
    }
}
