using CloudflareTray.Models;
using CloudflareTray.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudflareTray.Tests;

public class CloudflareTunnelServiceTests
{
    private static TunnelProfile Profile(params TunnelItemConfig[] tunnels) =>
        new("firma", new TunnelConfiguration([.. tunnels]), new Dictionary<string, TunnelCredentialItem>());

    private static readonly TunnelItemConfig Ssh = new("SSH", "ssh.firma.de", Protocol.Ssh, "localhost:2222");
    private static readonly TunnelItemConfig Web = new("Web", "web.firma.de", Protocol.Tcp, "localhost:8080");

    [Fact]
    public void Sync_KeepsUnchangedInstances()
    {
        using var service = new CloudflareTunnelService(NullLoggerFactory.Instance);
        service.Sync([Profile(Ssh, Web)]);
        var ssh = service.Tunnels[0];

        service.Sync([Profile(Ssh, Web)]);

        Assert.Same(ssh, service.Tunnels[0]);
    }

    [Fact]
    public void Sync_ReplacesChangedAndDropsRemovedInstances()
    {
        using var service = new CloudflareTunnelService(NullLoggerFactory.Instance);
        service.Sync([Profile(Ssh, Web)]);
        var ssh = service.Tunnels[0];

        var changed = Ssh with { LocalAddress = "localhost:2223" };
        service.Sync([Profile(changed)]);

        var instance = Assert.Single(service.Tunnels);
        Assert.NotSame(ssh, instance);
        Assert.Equal(changed, instance.Config);
    }
}
