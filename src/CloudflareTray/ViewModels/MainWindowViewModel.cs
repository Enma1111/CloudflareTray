using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CloudflareTray.Models;
using CloudflareTray.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudflareTray.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IOptionsMonitor<ProfilesOptions> _options;
    private readonly IOptionsMonitorCache<ProfilesOptions> _optionsCache;
    private readonly ProfileLoader _loader;
    private readonly CloudflareTunnelService _service;
    private readonly string _directory;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly UserPreferences _preferences;
    private readonly IDisposable? _subscription;

    private List<TunnelProfile> _profiles = [];

    public MainWindowViewModel(
        IOptionsMonitor<ProfilesOptions> options,
        IOptionsMonitorCache<ProfilesOptions> optionsCache,
        ProfileLoader loader,
        CloudflareTunnelService service,
        ProfileDirectory directory,
        UserPreferences preferences,
        ILogger<MainWindowViewModel> logger)
    {
        _logger = logger;
        _preferences = preferences;
        _options = options;
        _optionsCache = optionsCache;
        _loader = loader;
        _service = service;
        _directory = directory.Path;

        // JSON files edited by hand or newly dropped in; the notification comes from the thread pool
        _subscription = options.OnChange(_ => Dispatcher.UIThread.Post(Apply));
    }

    public ObservableCollection<ProfileGroupViewModel> Groups { get; } = [];
    public ObservableCollection<ServiceTokenGroupViewModel> TokenGroups { get; } = [];

    /// <summary>Set by the window: shows the dialog and returns true if the tunnel should be created.</summary>
    public Func<NewTunnelViewModel, Task<bool>>? ShowNewTunnelDialog { get; set; }


    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isEmpty = true;

    /// <summary>For the tray icon: dot in the tunnel and tooltip.</summary>
    [ObservableProperty] private TrayStatus _trayStatus;
    [ObservableProperty] private string _trayToolTip = TrayIconAssets.ToolTip(0, 0, 0);

    /// <summary>Without a tray there is no tray menu: closing minimizes and "Quit" is shown in the toolbar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTrayHint))]
    private bool _isTrayAvailable = true;

    public string TrayHint => TrayHost.MissingHint;

    /// <summary>The hint can be dismissed permanently; "Quit" stays in the toolbar regardless.</summary>
    public bool ShowTrayHint => !IsTrayAvailable && !_preferences.TrayHintDismissed;

    [RelayCommand]
    private void DismissTrayHint()
    {
        _preferences.TrayHintDismissed = true;
        OnPropertyChanged(nameof(ShowTrayHint));
    }

    /// <summary>Reloads all profiles immediately without waiting for the file watcher.</summary>
    [RelayCommand]
    public void Reload()
    {
        _optionsCache.TryRemove(Options.DefaultName);
        Apply();
    }

    /// <summary>
    /// Applies the current state from <see cref="ProfilesOptions"/>. Unchanged tunnels keep running,
    /// changed ones are restarted. A profile that fails to load keeps its previous state.
    /// </summary>
    private void Apply()
    {
        var loaded = _options.CurrentValue;
        var errors = new List<string>();
        List<TunnelProfile> profiles;

        if (loaded.DirectoryError != null)
        {
            errors.Add($"Could not read profiles: {loaded.DirectoryError}");
            profiles = _profiles;
        }
        else
        {
            profiles = [.. loaded.Profiles];
            foreach (var (name, message) in loaded.Errors)
            {
                errors.Add($"Profile '{name}': {message}");
                var previous = _profiles.FirstOrDefault(p => p.Name == name);
                if (previous != null) profiles.Add(previous);
            }
            profiles.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        }

        _profiles = profiles;
        _service.Sync(_profiles);
        RebuildGroups();
        RebuildTokenGroups();
        Error = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
    }

    [RelayCommand]
    private async Task NewTunnel()
    {
        if (ShowNewTunnelDialog == null) return;

        var dialog = new NewTunnelViewModel(_profiles);
        if (!await ShowNewTunnelDialog(dialog) || dialog.ResultTunnel == null || dialog.ResultProfileName == null) return;

        var profileName = dialog.ResultProfileName;
        var profile = _profiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))
            ?? new TunnelProfile(profileName, new TunnelConfiguration([]), new Dictionary<string, TunnelCredentialItem>());

        var credentials = new Dictionary<string, TunnelCredentialItem>(profile.Credentials);
        if (dialog.ResultNewCredential is var (credentialName, credential))
            credentials[credentialName] = credential;

        Save(profile with
        {
            Configuration = new TunnelConfiguration([.. profile.Configuration.Tunnels, dialog.ResultTunnel]),
            Credentials = credentials,
        });
    }

    private void DeleteTunnel(TunnelItemViewModel item)
    {
        var profile = _profiles.FirstOrDefault(p => p.Name == item.Instance.Profile);
        if (profile == null) return;

        // The service token is kept, other tunnels may be using it
        Save(profile with
        {
            Configuration = new TunnelConfiguration(
                profile.Configuration.Tunnels.Where(t => t.Name != item.Instance.Config.Name).ToList()),
        });
    }

    private string? SaveServiceToken(ServiceTokenItemViewModel token, string clientId, string? newSecret)
    {
        // Fresh from disk so that changes made to the file in the meantime are not lost
        Reload();
        var profile = _profiles.FirstOrDefault(p => p.Name == token.Profile);
        if (profile == null || !profile.Credentials.TryGetValue(token.Name, out var current))
            return "The service token no longer exists.";

        var credentials = new Dictionary<string, TunnelCredentialItem>(profile.Credentials)
        {
            [token.Name] = new(clientId, newSecret ?? current.ClientSecret),
        };
        if (!Save(profile with { Credentials = credentials })) return Error;

        _logger.LogInformation("Service token {Profile}/{Token} changed (secret replaced: {SecretReplaced})",
            token.Profile, token.Name, newSecret != null);
        return null;
    }

    private string? DeleteServiceToken(ServiceTokenItemViewModel token)
    {
        Reload();
        var profile = _profiles.FirstOrDefault(p => p.Name == token.Profile);
        if (profile == null || !profile.Credentials.ContainsKey(token.Name)) return null;

        var usedBy = UsedBy(profile, token.Name);
        if (usedBy.Count > 0)
            return $"Not deleted, it is now used by: {string.Join(", ", usedBy)}";

        var credentials = new Dictionary<string, TunnelCredentialItem>(profile.Credentials);
        credentials.Remove(token.Name);
        if (!Save(profile with { Credentials = credentials })) return Error;

        _logger.LogInformation("Service token {Profile}/{Token} deleted", token.Profile, token.Name);
        return null;
    }

    private static List<string> UsedBy(TunnelProfile profile, string credential) =>
        profile.Configuration.Tunnels.Where(t => t.Credential == credential).Select(t => t.Name).ToList();

    private bool Save(TunnelProfile profile)
    {
        try
        {
            _loader.Save(_directory, profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile {Profile} could not be saved", profile.Name);
            Error = $"Saving failed: {ex.Message}";
            return false;
        }
        Reload();
        return true;
    }

    /// <summary>
    /// One group per profile, even without tunnels.
    /// Only rebuilds the list when something changed and reuses the view models of unchanged
    /// instances, so that e.g. an open delete confirmation survives a reload.
    /// </summary>
    private void RebuildGroups()
    {
        var existing = Groups.SelectMany(g => g.Tunnels).ToList();
        if (Groups.Select(g => g.Name).SequenceEqual(_profiles.Select(p => p.Name))
            && existing.Select(t => t.Instance).SequenceEqual(_service.Tunnels))
        {
            UpdateStatus();
            return;
        }

        var byInstance = existing.ToDictionary(t => t.Instance);
        Groups.Clear();

        foreach (var profile in _profiles)
        {
            var items = _service.Tunnels
                .Where(t => t.Profile == profile.Name)
                .Select(t => byInstance.Remove(t, out var item) ? item : new TunnelItemViewModel(t, DeleteTunnel, UpdateStatus))
                .ToList();
            Groups.Add(new ProfileGroupViewModel(profile.Name, items));
        }

        foreach (var removed in byInstance.Values)
            removed.Dispose();
        UpdateStatus();
    }

    /// <summary>
    /// Like <see cref="RebuildGroups"/>: rows of existing tokens are reused, so that an edit in progress
    /// or an open delete confirmation survives a reload.
    /// </summary>
    private void RebuildTokenGroups()
    {
        var existing = TokenGroups.SelectMany(g => g.Tokens).ToDictionary(t => (t.Profile, t.Name));
        var groups = new List<(string Name, List<ServiceTokenItemViewModel> Tokens)>();

        foreach (var profile in _profiles)
        {
            var tokens = new List<ServiceTokenItemViewModel>();
            foreach (var (name, credential) in profile.Credentials.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var usedBy = UsedBy(profile, name);
                if (existing.Remove((profile.Name, name), out var item))
                    item.Update(credential.ClientId, usedBy);
                else
                    item = new ServiceTokenItemViewModel(profile.Name, name, credential.ClientId, usedBy, SaveServiceToken, DeleteServiceToken);
                tokens.Add(item);
            }
            groups.Add((profile.Name, tokens));
        }

        var unchanged = TokenGroups.Count == groups.Count && TokenGroups.Zip(groups).All(pair =>
            pair.First.Name == pair.Second.Name && pair.First.Tokens.SequenceEqual(pair.Second.Tokens));
        if (unchanged) return;

        TokenGroups.Clear();
        foreach (var (name, tokens) in groups)
            TokenGroups.Add(new ServiceTokenGroupViewModel(name, tokens));
    }

    private void UpdateStatus()
    {
        var total = _service.Tunnels.Count;
        var active = _service.Tunnels.Count(t => t.State == TunnelState.Running);
        var reconnecting = _service.Tunnels.Count(t => t.State == TunnelState.Reconnecting);
        StatusText = $"{total} tunnel(s) configured ({active} active)";
        IsEmpty = Groups.Count == 0;
        TrayStatus = TrayIconAssets.StatusOf(_service.Tunnels.Select(t => t.State));
        TrayToolTip = TrayIconAssets.ToolTip(total, active, reconnecting);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        foreach (var tunnel in Groups.SelectMany(g => g.Tunnels))
            tunnel.Dispose();
        GC.SuppressFinalize(this);
    }
}
