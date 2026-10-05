using CloudflareTray.Models;
using CloudflareTray.Services;

namespace CloudflareTray.Tests;

public class TrayIconAssetsTests
{
    [Theory]
    [InlineData(new TunnelState[0], TrayStatus.Idle)]
    [InlineData(new[] { TunnelState.Stopped, TunnelState.Stopped }, TrayStatus.Idle)]
    [InlineData(new[] { TunnelState.Stopped, TunnelState.Running }, TrayStatus.Running)]
    [InlineData(new[] { TunnelState.Running, TunnelState.Reconnecting }, TrayStatus.Reconnecting)]
    public void StatusOf_ReconnectingWinsOverRunning(TunnelState[] states, TrayStatus expected) =>
        Assert.Equal(expected, TrayIconAssets.StatusOf(states));

    [Theory]
    [InlineData("GNOME", true)]
    [InlineData("ubuntu:GNOME", true)]
    [InlineData("KDE", false)]
    [InlineData(null, false)]
    public void IsGnome(string? desktop, bool expected) =>
        Assert.Equal(expected, TrayIconAssets.IsGnome(desktop));

    /// <summary>Every combination must exist as a file, otherwise the app crashes when the status changes.</summary>
    [Fact]
    public void AllCombinations_ExistAsAssets()
    {
        var assets = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CloudflareTray/Assets"));

        foreach (var status in Enum.GetValues<TrayStatus>())
        foreach (var white in new[] { true, false })
        foreach (var windows in new[] { true, false })
        {
            var uri = TrayIconAssets.Uri(status, white, windows);
            var path = Path.Combine(assets, uri["avares://CloudflareTray/Assets/".Length..]);
            Assert.True(File.Exists(path), $"{path} is missing – run tools/generate-icons.sh");
        }
    }

    [Fact]
    public void ToolTip_MentionsReconnecting()
    {
        Assert.Equal("Cloudflare Tunnel Manager – no tunnels", TrayIconAssets.ToolTip(0, 0, 0));
        Assert.Equal("Cloudflare Tunnel Manager – 2 of 3 active", TrayIconAssets.ToolTip(3, 2, 0));
        Assert.Contains("1 reconnecting", TrayIconAssets.ToolTip(3, 1, 1));
    }
}
