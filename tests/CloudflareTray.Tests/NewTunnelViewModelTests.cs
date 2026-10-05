using CloudflareTray.Models;
using CloudflareTray.ViewModels;

namespace CloudflareTray.Tests;

public class NewTunnelViewModelTests
{
    private static readonly TunnelProfile Firma = new("firma",
        new TunnelConfiguration([new("Web", "web.firma.de", Protocol.Tcp, "localhost:8080", "default")]),
        new Dictionary<string, TunnelCredentialItem> { ["default"] = new("id.access", "geheim") });

    private static NewTunnelViewModel Dialog(string profileName, string name = "SSH") => new([Firma])
    {
        ProfileName = profileName,
        Name = name,
        Protocol = Protocol.Ssh,
        Hostname = "ssh.firma.de",
        LocalAddress = "localhost:2222",
    };

    [Fact]
    public void ProfileName_DifferentCase_UsesExistingProfile()
    {
        var dialog = Dialog("FIRMA");

        Assert.Contains("default", dialog.CredentialChoices);
        Assert.Contains("'firma'", dialog.ProfileHint);
        Assert.True(dialog.Validate());
        Assert.Equal("firma", dialog.ResultProfileName);
    }

    [Fact]
    public void ProfileName_DifferentCase_StillDetectsDuplicateTunnel()
    {
        var dialog = Dialog("Firma", name: "Web");

        Assert.False(dialog.Validate());
        Assert.Contains("Web", dialog.Error);
    }

    [Fact]
    public void ProfileName_Unknown_CreatesNewProfile()
    {
        var dialog = Dialog(" kunde ");

        Assert.Contains("New profile 'kunde'", dialog.ProfileHint);
        Assert.DoesNotContain("default", dialog.CredentialChoices);
        Assert.True(dialog.Validate());
        Assert.Equal("kunde", dialog.ResultProfileName);
    }
}
