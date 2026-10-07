using SpotlightNative.Models;
using SpotlightNative.Services;
using Xunit;

namespace SpotlightNative.Tests;

public sealed class FuzzySearchEngineTests
{
    private static SearchResult Result(string title) => new()
    {
        Title = title,
        Kind = SearchResultKind.File,
        Path = $@"C:\Test\{title}"
    };

    [Fact]
    public void FindsTypo()
    {
        var result = new FuzzySearchEngine([Result("Notepad")]).Search("notepd");
        Assert.Equal("Notepad", Assert.Single(result).Title);
    }

    [Fact]
    public void FindsSubsequenceAndAbbreviation()
    {
        var result = new FuzzySearchEngine([Result("Visual Studio Code")]).Search("vsc");
        Assert.Equal("Visual Studio Code", Assert.Single(result).Title);
    }

    [Fact]
    public void MatchesWordsInAnyOrder()
    {
        var result = new FuzzySearchEngine([Result("final report.docx")]).Search("report final");
        Assert.Equal("final report.docx", Assert.Single(result).Title);
    }

    [Fact]
    public void ExactBeatsPrefixAndFuzzy()
    {
        var result = new FuzzySearchEngine([Result("app"), Result("application"), Result("apx")]).Search("app");
        Assert.Equal(["app", "application", "apx"], result.Select(item => item.Title));
    }

    [Fact]
    public void IgnoresDiacritics()
    {
        var result = new FuzzySearchEngine([Result("Café")]).Search("cafe");
        Assert.Equal("Café", Assert.Single(result).Title);
    }

    [Fact]
    public void EmptyAndShortQueriesAreSafe()
    {
        var engine = new FuzzySearchEngine([Result("Notepad")]);
        Assert.Empty(engine.Search(""));
        Assert.NotNull(engine.Search("n", 5));
    }
}
