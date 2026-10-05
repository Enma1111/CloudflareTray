using System;
using System.Collections.Generic;
using System.Linq;
using CloudflareTray.Models;
using Microsoft.Extensions.Logging;

namespace CloudflareTray.Services;

public class CloudflareTunnelService(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly ILogger<TunnelInstance> _instanceLogger = loggerFactory.CreateLogger<TunnelInstance>();
    private List<TunnelInstance> _tunnels = new();

    public IReadOnlyList<TunnelInstance> Tunnels => _tunnels;

    /// <summary>
    /// Reconciles the instances with the profiles: unchanged tunnels keep running, changed ones are
    /// replaced (and restarted if they were active), removed ones are stopped.
    /// </summary>
    public void Sync(IEnumerable<TunnelProfile> profiles)
    {
        var result = new List<TunnelInstance>();

        foreach (var profile in profiles)
        {
            foreach (var config in profile.Configuration.Tunnels)
            {
                var credential = profile.GetCredential(config);
                var existing = _tunnels.FirstOrDefault(t => t.Profile == profile.Name && t.Config.Name == config.Name);

                if (existing != null && existing.Config == config && existing.Credential == credential)
                {
                    result.Add(existing);
                    continue;
                }

                var instance = new TunnelInstance(profile.Name, config, credential, _instanceLogger);
                if (existing != null)
                {
                    var wasActive = existing.State != TunnelState.Stopped;
                    existing.Dispose();
                    if (wasActive) instance.Start();
                }
                result.Add(instance);
            }
        }

        foreach (var removed in _tunnels.Except(result))
            removed.Dispose();

        _tunnels = result;
    }

    public void StartAll()
    {
        foreach (var tunnel in _tunnels)
        {
            tunnel.Start();
        }
    }

    public void StopAll()
    {
        foreach (var tunnel in _tunnels)
        {
            tunnel.Stop();
        }
    }

    public void Dispose()
    {
        StopAll();
        GC.SuppressFinalize(this);
    }
}
