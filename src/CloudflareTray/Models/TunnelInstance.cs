using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CloudflareTray.Services;
using Microsoft.Extensions.Logging;

namespace CloudflareTray.Models;

public enum TunnelState
{
    Stopped,
    Running,
    Reconnecting,
}

public class TunnelInstance(
    string profile,
    TunnelItemConfig config,
    TunnelCredentialItem? credential,
    ILogger<TunnelInstance> logger) : IDisposable
{
    private static readonly TimeSpan InitialRestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRestartDelay = TimeSpan.FromMinutes(5);
    // If the process ran at least this long, it counts as stable and the backoff starts over
    private static readonly TimeSpan StableRunTime = TimeSpan.FromMinutes(1);

    public string Profile { get; } = profile;
    public TunnelItemConfig Config { get; } = config;
    public TunnelCredentialItem? Credential { get; } = credential;

    // Guards _process, State and the restart state: Exited and restarts run on the thread pool
    private readonly object _lock = new();
    private Process? _process;
    private DateTime _startedAt;
    private CancellationTokenSource? _restartCts;
    private int _restartAttempts;

    public TunnelState State { get; private set; }

    /// <summary>Last error message from cloudflared or from starting it; null after a successful start.</summary>
    public string? LastError { get; private set; }

    /// <summary>Raised when <see cref="State"/> or <see cref="LastError"/> changes, possibly from the thread pool.</summary>
    public event EventHandler? StateChanged;

    public void Start()
    {
        lock (_lock)
        {
            CancelPendingRestart();
            _restartAttempts = 0;
            if (State == TunnelState.Running) return;
            if (!StartProcess()) State = TunnelState.Stopped;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Must be called while holding <see cref="_lock"/>.</summary>
    private bool StartProcess()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = CloudflaredLocator.Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in CloudflaredArguments.Build(Config)) psi.ArgumentList.Add(arg);

            // Passed via the environment rather than as an argument, so the secret doesn't show up in `ps`.
            // Without a credential, remove them explicitly, otherwise values from the app's environment would be inherited.
            if (Credential != null)
            {
                psi.Environment["TUNNEL_SERVICE_TOKEN_ID"] = Credential.ClientId;
                psi.Environment["TUNNEL_SERVICE_TOKEN_SECRET"] = Credential.ClientSecret;
            }
            else
            {
                psi.Environment.Remove("TUNNEL_SERVICE_TOKEN_ID");
                psi.Environment.Remove("TUNNEL_SERVICE_TOKEN_SECRET");
            }

            _process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true,
            };

            // If the process exits immediately, OnProcessExited waits for the lock until State is set
            _process.Exited += OnProcessExited;
            _process.ErrorDataReceived += OnErrorDataReceived;

            _startedAt = DateTime.UtcNow;
            _process.Start();
            // Drain both pipes, otherwise cloudflared blocks as soon as the buffer is full
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            State = TunnelState.Running;
            LastError = null;
            logger.LogInformation("Tunnel {Profile}/{Tunnel} started: {Hostname} -> {LocalAddress} (PID {Pid})",
                Profile, Config.Name, Config.Hostname, Config.LocalAddress, _process.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tunnel {Profile}/{Tunnel} could not be started", Profile, Config.Name);
            DisposeProcess();
            LastError = ex.Message;
            return false;
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;

        // cloudflared logs to stderr in the format "<time> <level> <message>"
        var message = ExtractError(e.Data);
        if (message == null)
        {
            logger.LogDebug("cloudflared {Profile}/{Tunnel}: {Line}", Profile, Config.Name, e.Data);
            return;
        }
        logger.LogWarning("cloudflared {Profile}/{Tunnel}: {Message}", Profile, Config.Name, message);

        lock (_lock)
        {
            if (!ReferenceEquals(sender, _process)) return;
            LastError = message;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? ExtractError(string line)
    {
        foreach (var level in (ReadOnlySpan<string>)[" ERR ", " FTL "])
        {
            var index = line.IndexOf(level, StringComparison.Ordinal);
            if (index >= 0) return line[(index + level.Length)..].Trim();
        }
        return null;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            // Late event from a process that was already stopped or replaced
            if (!ReferenceEquals(sender, _process)) return;

            LastError ??= $"cloudflared exited (exit code {_process!.ExitCode}).";
            DisposeProcess();

            if (DateTime.UtcNow - _startedAt >= StableRunTime)
                _restartAttempts = 0;
            ScheduleRestart();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Must be called while holding <see cref="_lock"/>.</summary>
    private void ScheduleRestart()
    {
        // 5 s, 10 s, 20 s, … up to at most 5 min
        var delay = TimeSpan.FromTicks(Math.Min(
            MaxRestartDelay.Ticks,
            InitialRestartDelay.Ticks << Math.Min(_restartAttempts, 10)));
        _restartAttempts++;

        CancelPendingRestart();
        State = TunnelState.Reconnecting;
        _restartCts = new CancellationTokenSource();
        logger.LogWarning("Tunnel {Profile}/{Tunnel} disconnected: {Error}. Retry {Attempt} in {Delay:0} seconds",
            Profile, Config.Name, LastError, _restartAttempts, delay.TotalSeconds);
        _ = RestartAfterAsync(delay, _restartCts.Token);
    }

    private async Task RestartAfterAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_lock)
        {
            // Start() or Stop() got in between
            if (token.IsCancellationRequested) return;

            _restartCts?.Dispose();
            _restartCts = null;

            if (!StartProcess()) ScheduleRestart();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Must be called while holding <see cref="_lock"/>.</summary>
    private void CancelPendingRestart()
    {
        _restartCts?.Cancel();
        _restartCts?.Dispose();
        _restartCts = null;
    }

    public void Stop()
    {
        bool changed;
        lock (_lock)
        {
            CancelPendingRestart();
            _restartAttempts = 0;
            changed = State != TunnelState.Stopped || LastError != null;

            if (_process != null)
            {
                // An Exited event arriving concurrently is discarded afterwards by the sender comparison
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while stopping tunnel {Profile}/{Tunnel}", Profile, Config.Name);
                }
                DisposeProcess();
                logger.LogInformation("Tunnel {Profile}/{Tunnel} stopped", Profile, Config.Name);
            }

            State = TunnelState.Stopped;
            LastError = null;
        }
        if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Must be called while holding <see cref="_lock"/>.</summary>
    private void DisposeProcess()
    {
        if (_process == null) return;
        _process.Exited -= OnProcessExited;
        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
