using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CloudflareTray.Models;

public record TunnelConfiguration(
    List<TunnelItemConfig> Tunnels
    );

[JsonConverter(typeof(JsonStringEnumConverter<Protocol>))]
public enum Protocol
{
    Unknown,
    Tcp,
    Ssh,
    Rdp,
    Smb,
    Curl
}


public record Arguments(
    Protocol Protocol,
    string? TargetAddress,
    int? LocalPort
);

public record TunnelItemConfig(
    string Name,
    string Hostname,
    Protocol Protocol,
    string LocalAddress,
    string? Credential = null
    );