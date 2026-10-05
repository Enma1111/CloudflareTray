using System;
using System.Collections.Generic;
using System.Linq;
using CloudflareTray.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudflareTray.Services;

/// <summary>Directory containing the profile files.</summary>
public sealed record ProfileDirectory(string Path);

/// <summary>
/// State of all profiles in the <see cref="ProfileDirectory"/>. Provided via <see cref="IOptionsMonitor{TOptions}"/>
/// and reloaded whenever the JSON files change.
/// </summary>
public class ProfilesOptions
{
    public List<TunnelProfile> Profiles { get; } = [];

    /// <summary>Profiles that failed to load: profile name → error message.</summary>
    public Dictionary<string, string> Errors { get; } = [];

    /// <summary>Error while listing the directory; <see cref="Profiles"/> is empty in that case.</summary>
    public string? DirectoryError { get; set; }
}

/// <summary>Fills <see cref="ProfilesOptions"/> via the <see cref="ProfileLoader"/>. A broken profile doesn't prevent the others from loading.</summary>
public class ConfigureProfilesOptions(
    ProfileLoader loader,
    ProfileDirectory directory,
    ILogger<ConfigureProfilesOptions> logger) : IConfigureOptions<ProfilesOptions>
{
    public void Configure(ProfilesOptions options)
    {
        IReadOnlyList<string> names;
        try
        {
            names = loader.ListProfileNames(directory.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read profile directory {Directory}", directory.Path);
            options.DirectoryError = ex.Message;
            return;
        }

        foreach (var name in names)
        {
            // Only possible on Linux; on Windows and macOS these would be the same files
            var clash = options.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (clash != null)
            {
                logger.LogError("Profile {Profile} differs from {Existing} only in letter case", name, clash.Name);
                options.Errors[name] = $"Differs from profile '{clash.Name}' only in letter case.";
                continue;
            }

            try
            {
                options.Profiles.Add(loader.Load(directory.Path, name));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Profile {Profile} could not be loaded", name);
                options.Errors[name] = ex.Message;
            }
        }

        logger.LogInformation("Loaded {Count} profile(s) with {Tunnels} tunnel(s) from {Directory}",
            options.Profiles.Count, options.Profiles.Sum(p => p.Configuration.Tunnels.Count), directory.Path);
    }
}
