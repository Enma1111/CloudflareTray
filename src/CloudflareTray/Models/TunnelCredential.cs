using System.Collections.Generic;

namespace CloudflareTray.Models;

public record TunnelCredential(
    Dictionary<string, TunnelCredentialItem> Credentials
    );

public record TunnelCredentialItem(
    string ClientId,
    string ClientSecret
    );