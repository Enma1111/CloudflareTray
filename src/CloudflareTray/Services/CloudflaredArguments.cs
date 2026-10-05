using System;
using System.Collections.Generic;
using CloudflareTray.Models;

namespace CloudflareTray.Services;

/// <summary>
/// Builds the command line for cloudflared. All supported protocols go through
/// <c>cloudflared access tcp</c>, which exposes the hostname at the local address
/// (ssh/rdp/smb are just aliases for it in cloudflared).
/// </summary>
public static class CloudflaredArguments
{
    public static readonly IReadOnlyList<Protocol> SupportedProtocols =
        [Protocol.Tcp, Protocol.Ssh, Protocol.Rdp, Protocol.Smb];

    public static IReadOnlyList<string> Build(TunnelItemConfig tunnel) => tunnel.Protocol switch
    {
        Protocol.Tcp or Protocol.Ssh or Protocol.Rdp or Protocol.Smb =>
            ["access", "tcp", "--hostname", tunnel.Hostname, "--url", tunnel.LocalAddress],
        _ => throw new NotSupportedException($"Protocol {tunnel.Protocol} is not supported."),
    };
}
