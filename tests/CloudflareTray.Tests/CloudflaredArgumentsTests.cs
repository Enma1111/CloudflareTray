using CloudflareTray.Models;
using CloudflareTray.Services;

namespace CloudflareTray.Tests;

public class CloudflaredArgumentsTests
{
    [Fact]
    public void Build_UsesAccessTcpWithHostnameAndLocalAddress()
    {
        var tunnel = new TunnelItemConfig("SSH", "ssh.firma.de", Protocol.Ssh, "localhost:2222");

        Assert.Equal(
            ["access", "tcp", "--hostname", "ssh.firma.de", "--url", "localhost:2222"],
            CloudflaredArguments.Build(tunnel));
    }

    [Fact]
    public void Build_UnsupportedProtocol_Throws()
    {
        var tunnel = new TunnelItemConfig("X", "x.firma.de", Protocol.Curl, "localhost:1");

        Assert.Throws<NotSupportedException>(() => CloudflaredArguments.Build(tunnel));
    }
}
