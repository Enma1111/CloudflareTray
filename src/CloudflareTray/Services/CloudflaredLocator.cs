using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace CloudflareTray.Services;

/// <summary>
/// Finds the installed cloudflared in the PATH and checks that it is at least <see cref="RequiredVersion"/>.
/// </summary>
public static partial class CloudflaredLocator
{
    public static readonly Version RequiredVersion = new(2026, 9, 3);

    private const string FileName = "cloudflared";

    // PublicationOnly doesn't cache exceptions: if cloudflared is installed later, the next attempt succeeds
    private static readonly Lazy<string> _path = new(Resolve, LazyThreadSafetyMode.PublicationOnly);

    /// <exception cref="InvalidOperationException">cloudflared is missing or too old.</exception>
    public static string Path => _path.Value;

    private static string Resolve()
    {
        (int ExitCode, string Output) result;
        try
        {
            result = KeyStore.Run(FileName, null, "--version");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"cloudflared not found – please install cloudflared {RequiredVersion} or newer.", ex);
        }

        var version = ParseVersion(result.Output);
        if (result.ExitCode != 0 || version == null)
            throw new InvalidOperationException($"Could not determine the cloudflared version: {result.Output}");
        if (version < RequiredVersion)
            throw new InvalidOperationException(
                $"cloudflared {version} is too old – {RequiredVersion} or newer is required.");

        return FileName;
    }

    /// <summary>Parses the version from the output of <c>cloudflared --version</c>.</summary>
    public static Version? ParseVersion(string output)
    {
        var match = VersionRegex().Match(output);
        return match.Success ? Version.Parse(match.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"cloudflared version (\d+\.\d+\.\d+)")]
    private static partial Regex VersionRegex();
}
