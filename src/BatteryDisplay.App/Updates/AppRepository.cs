using System.Reflection;

namespace BatteryDisplay.App.Updates;

/// <summary>
/// The GitHub repository the releases come from. Its one source is RepositoryUrl in Directory.Build.props, which
/// the build embeds as assembly metadata; while it is empty or not a GitHub repository, updates stay off.
/// </summary>
public static class AppRepository
{
    public const string MetadataKey = "RepositoryUrl";

    private const string GitHubHost = "github.com";

    /// <summary>https://github.com/&lt;owner&gt;/&lt;repo&gt;, or null while the repository is not set.</summary>
    public static string? Url { get; } = Normalize(typeof(AppRepository).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == MetadataKey)?.Value);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, GitHubHost, StringComparison.OrdinalIgnoreCase))
            return null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
            return null;
        var repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        return repo.Length == 0 ? null : $"https://{GitHubHost}/{segments[0]}/{repo}";
    }
}
