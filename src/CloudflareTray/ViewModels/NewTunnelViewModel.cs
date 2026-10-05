using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CloudflareTray.Models;
using CloudflareTray.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudflareTray.ViewModels;

/// <summary>Form for "New tunnel". After a successful <see cref="Validate"/>, the Result properties hold the result.</summary>
public partial class NewTunnelViewModel : ViewModelBase
{
    public const string NoCredential = "(none)";
    public const string NewCredential = "New service token…";
    public const string DefaultProfileName = "default";

    private readonly IReadOnlyList<TunnelProfile> _profiles;

    public NewTunnelViewModel(IReadOnlyList<TunnelProfile> profiles)
    {
        _profiles = profiles;
        ProfileNames = profiles.Select(p => p.Name).ToList();
        _profileName = ProfileNames.FirstOrDefault() ?? DefaultProfileName;
        UpdateCredentialChoices();
    }

    public IReadOnlyList<string> ProfileNames { get; }
    public IReadOnlyList<Protocol> Protocols => CloudflaredArguments.SupportedProtocols;

    [ObservableProperty] private string _profileName;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private Protocol _protocol = Protocol.Tcp;
    [ObservableProperty] private string _hostname = "";
    [ObservableProperty] private string _localAddress = "localhost:";

    [ObservableProperty] private IReadOnlyList<string> _credentialChoices = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewCredential))]
    private string _selectedCredential = NoCredential;

    [ObservableProperty] private string _newCredentialName = "";
    [ObservableProperty] private string _clientId = "";
    [ObservableProperty] private string _clientSecret = "";

    [ObservableProperty] private string? _error;

    public bool IsNewCredential => SelectedCredential == NewCredential;

    /// <summary>Profile name spelled like the existing profile, otherwise the name as entered.</summary>
    public string? ResultProfileName { get; private set; }
    public TunnelItemConfig? ResultTunnel { get; private set; }
    public (string Name, TunnelCredentialItem Item)? ResultNewCredential { get; private set; }

    // Case-insensitive: on Windows and macOS, company.config.json and Company.config.json would be the same file
    private TunnelProfile? SelectedProfile =>
        _profiles.FirstOrDefault(p => string.Equals(p.Name, ProfileName.Trim(), StringComparison.OrdinalIgnoreCase));

    public string ProfileHint => SelectedProfile switch
    {
        { } profile => $"Will be added to profile '{profile.Name}' ({profile.Configuration.Tunnels.Count} existing tunnel(s)).",
        null when ProfileName.Trim().Length > 0 => $"New profile '{ProfileName.Trim()}' will be created.",
        _ => "",
    };

    partial void OnProfileNameChanged(string value)
    {
        UpdateCredentialChoices();
        OnPropertyChanged(nameof(ProfileHint));
    }

    private void UpdateCredentialChoices()
    {
        var existing = SelectedProfile?.Credentials.Keys.Order() ?? Enumerable.Empty<string>();
        CredentialChoices = [NoCredential, .. existing, NewCredential];
        if (!CredentialChoices.Contains(SelectedCredential))
            SelectedCredential = NoCredential;
    }

    public bool Validate()
    {
        Error = FindError();
        if (Error != null) return false;

        var profileCredential = SelectedCredential switch
        {
            NoCredential => null,
            NewCredential => NewCredentialName.Trim(),
            _ => SelectedCredential,
        };
        ResultProfileName = SelectedProfile?.Name ?? ProfileName.Trim();
        ResultTunnel = new TunnelItemConfig(Name.Trim(), Hostname.Trim(), Protocol, LocalAddress.Trim(), profileCredential);
        ResultNewCredential = IsNewCredential
            ? (NewCredentialName.Trim(), new TunnelCredentialItem(ClientId.Trim(), ClientSecret))
            : null;
        return true;
    }

    private string? FindError()
    {
        var profileName = ProfileName.Trim();
        if (!ProfileNameRegex().IsMatch(profileName))
            return "Profile name may only contain letters, digits, spaces, - and _.";

        var name = Name.Trim();
        if (name.Length == 0)
            return "Please enter a name.";
        if (SelectedProfile is { } existing && existing.Configuration.Tunnels.Any(t => t.Name == name))
            return $"Profile '{existing.Name}' already has a tunnel '{name}'.";

        if (!HostnameRegex().IsMatch(Hostname.Trim()))
            return "Please enter a valid hostname, e.g. ssh.example.com (without https://).";

        var match = LocalAddressRegex().Match(LocalAddress.Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var port) || port is < 1 or > 65535)
            return "Please enter the local address as host:port, e.g. localhost:2222.";

        if (IsNewCredential)
        {
            var credentialName = NewCredentialName.Trim();
            if (credentialName.Length == 0 || credentialName is NoCredential or NewCredential)
                return "Please enter a name for the service token.";
            if (SelectedProfile?.Credentials.ContainsKey(credentialName) == true)
                return $"The profile already has a service token '{credentialName}'.";
            if (ClientId.Trim().Length == 0 || ClientSecret.Length == 0)
                return "Please enter the client ID and client secret of the service token.";
        }

        return null;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}_\- ]+$")]
    private static partial Regex ProfileNameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$")]
    private static partial Regex HostnameRegex();

    [GeneratedRegex(@"^[^\s:/]+:(\d+)$")]
    private static partial Regex LocalAddressRegex();
}
