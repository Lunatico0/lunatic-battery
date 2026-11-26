using System.Reflection;
using BatteryDisplay.App.Updates;

namespace BatteryDisplay.App.Tests.Updates;

public class AppRepositoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com/owner")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("http://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/tree/main")]
    [InlineData("not a url")]
    public void NotAGitHubRepository_IsNoFeed(string? value)
    {
        Assert.Null(AppRepository.Normalize(value));
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", "https://github.com/owner/repo")]
    [InlineData(" https://github.com/owner/repo/ ", "https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("https://GitHub.com/Owner/Repo", "https://github.com/Owner/Repo")]
    public void GitHubRepository_IsNormalized(string value, string expected)
    {
        Assert.Equal(expected, AppRepository.Normalize(value));
    }

    [Fact]
    public void TheBuildEmbedsTheRepositoryUrlFromDirectoryBuildProps()
    {
        var keys = typeof(AppRepository).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Select(a => a.Key);

        Assert.Contains(AppRepository.MetadataKey, keys);
    }
}
