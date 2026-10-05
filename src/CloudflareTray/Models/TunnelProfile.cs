using System.Collections.Generic;

namespace CloudflareTray.Models;

/// <summary>
/// A profile consists of {Name}.config.json and {Name}.credentials.json.
/// The credentials are held decrypted in memory here.
/// </summary>
public record TunnelProfile(
    string Name,
    TunnelConfiguration Configuration,
    IReadOnlyDictionary<string, TunnelCredentialItem> Credentials
    )
{
    public TunnelCredentialItem? GetCredential(TunnelItemConfig tunnel) =>
        tunnel.Credential is null ? null : Credentials[tunnel.Credential];
}
