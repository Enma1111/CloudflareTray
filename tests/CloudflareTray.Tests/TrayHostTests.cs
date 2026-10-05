using CloudflareTray.Services;

namespace CloudflareTray.Tests;

public class TrayHostTests
{
    [Theory]
    [InlineData("(true,)", true)]
    [InlineData("(false,)\n", false)]
    [InlineData("", false)]
    public void ParseNameHasOwner(string output, bool expected) =>
        Assert.Equal(expected, TrayHost.ParseNameHasOwner(output));
}
