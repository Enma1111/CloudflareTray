using CloudflareTray.Services;

namespace CloudflareTray.Tests;

public class CloudflaredLocatorTests
{
    [Fact]
    public void ParseVersion_ReadsVersionFromOutput()
    {
        var version = CloudflaredLocator.ParseVersion(
            "cloudflared version 2026.9.3 (built 2026-09-25-1200 UTC)");

        Assert.Equal(new Version(2026, 9, 3), version);
    }

    [Fact]
    public void ParseVersion_ReturnsNullForUnknownOutput()
    {
        Assert.Null(CloudflaredLocator.ParseVersion("command not found"));
    }
}
