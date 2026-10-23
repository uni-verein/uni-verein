using UniVerein.Api.Helper;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class NameSearchHelperTests
{
    [Theory]
    [InlineData("test", "anna tester")]
    [InlineData("TEST", "Anna Tester")]
    [InlineData("nna te", "Anna tester")]
    [InlineData("tester", "anna tester")]
    public void Score_SubstringAnywhere_IsMatch(string searchTerm, string name)
    {
        NameSearchHelper.IsMatch(NameSearchHelper.Score(searchTerm, name)).ShouldBeTrue();
    }

    [Fact]
    public void Score_SubstringInAnyCandidate_IsMatch()
    {
        double score = NameSearchHelper.Score("mann", "Tim Baumann", "Tim", null, "Baumann");

        score.ShouldBe(1);
    }

    [Fact]
    public void Score_Typo_StillMatches()
    {
        NameSearchHelper.IsMatch(NameSearchHelper.Score("tster", "tester")).ShouldBeTrue();
    }

    [Fact]
    public void Score_Unrelated_IsNoMatch()
    {
        NameSearchHelper.IsMatch(NameSearchHelper.Score("Zzzyx Nonexistent", "Rene Tester")).ShouldBeFalse();
    }

    [Fact]
    public void Score_EmptyCandidates_IsNoMatch()
    {
        NameSearchHelper.IsMatch(NameSearchHelper.Score("test", null, " ")).ShouldBeFalse();
    }
}
