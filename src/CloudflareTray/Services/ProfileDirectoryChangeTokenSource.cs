using System;
using System.Threading;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace CloudflareTray.Services;

/// <summary>
/// Reports changes to *.json in the profile directory to the <see cref="IOptionsMonitor{TOptions}"/> –
/// including newly added files. Editors and <see cref="ProfileLoader"/> write in several
/// steps, so it only fires after a short quiet period.
/// </summary>
public sealed class ProfileDirectoryChangeTokenSource : IOptionsChangeTokenSource<ProfilesOptions>, IDisposable
{
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private readonly PhysicalFileProvider _files;
    private readonly IDisposable _watch;
    private readonly Timer _timer;
    private readonly object _lock = new();
    private readonly ILogger<ProfileDirectoryChangeTokenSource> _logger;
    private CancellationTokenSource _cts = new();

    public ProfileDirectoryChangeTokenSource(ProfileDirectory directory, ILogger<ProfileDirectoryChangeTokenSource> logger)
    {
        _logger = logger;
        // PhysicalFileProvider requires an existing directory
        ProfileLoader.EnsureDirectory(directory.Path);
        _files = new PhysicalFileProvider(directory.Path);
        _timer = new Timer(_ => Fire());
        _watch = ChangeToken.OnChange(
            () => _files.Watch("*.json"),
            () => _timer.Change(Debounce, Timeout.InfiniteTimeSpan));
    }

    public string Name => Options.DefaultName;

    public IChangeToken GetChangeToken()
    {
        lock (_lock)
        {
            // After firing, the monitor fetches a new token
            if (_cts.IsCancellationRequested) _cts = new CancellationTokenSource();
            return new CancellationChangeToken(_cts.Token);
        }
    }

    private void Fire()
    {
        CancellationTokenSource cts;
        lock (_lock) cts = _cts;
        _logger.LogDebug("Change detected in profile directory, reloading profiles");
        cts.Cancel();
    }

    public void Dispose()
    {
        _watch.Dispose();
        _timer.Dispose();
        _files.Dispose();
    }
}
