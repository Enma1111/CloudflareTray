using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CloudflareTray.Models;
using Microsoft.Extensions.Logging;

namespace CloudflareTray.Services;

/// <summary>
/// Loads all profiles from a directory. A profile "company" consists of
/// company.config.json and (optionally) company.credentials.json.
/// Plain-text secrets are encrypted on load and the file is written back.
/// </summary>
public class ProfileLoader(SecretProtector protector, ILogger<ProfileLoader> logger)
{
    private const string ConfigSuffix = ".config.json";
    private const string CredentialsSuffix = ".credentials.json";

    public static string DefaultDirectory => Path.Combine(AppPaths.DataDirectory, "profiles");

    public IReadOnlyList<TunnelProfile> LoadAll(string directory) =>
        ListProfileNames(directory).Select(name => Load(directory, name)).ToList();

    public IReadOnlyList<string> ListProfileNames(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, "*" + ConfigSuffix)
            .Select(path => Path.GetFileName(path)[..^ConfigSuffix.Length])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TunnelProfile Load(string directory, string name)
    {
        var configPath = Path.Combine(directory, name + ConfigSuffix);
        var credentialsPath = Path.Combine(directory, name + CredentialsSuffix);

        var config = JsonFile.Read(configPath, AppJsonContext.Default.TunnelConfiguration);
        var credentials = File.Exists(credentialsPath)
            ? LoadCredentials(credentialsPath)
            : new Dictionary<string, TunnelCredentialItem>();

        var duplicate = config.Tunnels.GroupBy(t => t.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new InvalidDataException($"Profile '{name}': tunnel name '{duplicate.Key}' is used more than once.");

        foreach (var tunnel in config.Tunnels)
        {
            if (tunnel.Credential is not null && !credentials.ContainsKey(tunnel.Credential))
                throw new InvalidDataException(
                    $"Profile '{name}': tunnel '{tunnel.Name}' refers to unknown credential '{tunnel.Credential}'.");
        }

        return new TunnelProfile(name, config, credentials);
    }

    /// <summary>
    /// Writes a profile's configuration and credentials. Secrets are stored encrypted.
    /// </summary>
    public void Save(string directory, TunnelProfile profile)
    {
        EnsureDirectory(directory);
        JsonFile.WriteAtomic(Path.Combine(directory, profile.Name + ConfigSuffix), profile.Configuration,
            AppJsonContext.Default.TunnelConfiguration);

        var credentialsPath = Path.Combine(directory, profile.Name + CredentialsSuffix);
        if (profile.Credentials.Count == 0 && !File.Exists(credentialsPath))
            return;

        var encrypted = profile.Credentials.ToDictionary(
            kv => kv.Key,
            kv => kv.Value with { ClientSecret = protector.Protect(kv.Value.ClientSecret) });
        JsonFile.WriteAtomic(credentialsPath, new TunnelCredential(encrypted), AppJsonContext.Default.TunnelCredential);
    }

    /// <summary>Creates the directory; on Unix it is accessible to the user only.</summary>
    public static void EnsureDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(directory);
        else
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private Dictionary<string, TunnelCredentialItem> LoadCredentials(string path)
    {
        var stored = JsonFile.Read(path, AppJsonContext.Default.TunnelCredential).Credentials;

        if (stored.Values.Any(c => !SecretProtector.IsProtected(c.ClientSecret)))
        {
            var encrypted = stored.ToDictionary(
                kv => kv.Key,
                kv => SecretProtector.IsProtected(kv.Value.ClientSecret)
                    ? kv.Value
                    : kv.Value with { ClientSecret = protector.Protect(kv.Value.ClientSecret) });
            JsonFile.WriteAtomic(path, new TunnelCredential(encrypted), AppJsonContext.Default.TunnelCredential);
            stored = encrypted;
            logger.LogInformation("Encrypted plain-text secrets in {Path}", path);
        }

        return stored.ToDictionary(
            kv => kv.Key,
            kv => kv.Value with { ClientSecret = protector.Unprotect(kv.Value.ClientSecret) });
    }
}
