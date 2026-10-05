using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace CloudflareTray.Services;

/// <summary>
/// Provides the master key for secret encryption from the operating system's keyring.
/// </summary>
public interface IKeyStore
{
    byte[] GetOrCreateKey();
}

public static class KeyStore
{
    public static IKeyStore CreateDefault()
    {
        if (OperatingSystem.IsWindows()) return new DpapiKeyStore();
        if (OperatingSystem.IsMacOS()) return new MacKeychainKeyStore();
        if (OperatingSystem.IsLinux()) return new LibSecretKeyStore();
        throw new PlatformNotSupportedException("No keyring available for this operating system.");
    }

    internal const string AppName = "CloudflareTray";
    internal const string KeyName = "master-key";
    internal const int KeySize = 32;

    internal static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeySize);

    internal static (int ExitCode, string Output) Run(string fileName, string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardInput = stdin != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"{fileName} could not be started.");
        if (stdin != null)
        {
            process.StandardInput.Write(stdin);
            process.StandardInput.Close();
        }
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Trim());
    }
}

/// <summary>Linux: GNOME Keyring / KWallet via the Secret Service API (secret-tool from libsecret).</summary>
public class LibSecretKeyStore : IKeyStore
{
    public byte[] GetOrCreateKey()
    {
        var (exitCode, output) = Lookup();
        if (exitCode == 0 && output.Length > 0)
            return Convert.FromBase64String(output);

        var key = KeyStore.NewKey();
        var (storeExit, _) = KeyStore.Run("secret-tool", Convert.ToBase64String(key),
            "store", $"--label={KeyStore.AppName} {KeyStore.KeyName}",
            "application", KeyStore.AppName, "key", KeyStore.KeyName);
        if (storeExit != 0)
            throw new InvalidOperationException("Could not store the master key in the keyring (is a Secret Service running?).");
        return key;
    }

    private static (int, string) Lookup()
    {
        try
        {
            return KeyStore.Run("secret-tool", null,
                "lookup", "application", KeyStore.AppName, "key", KeyStore.KeyName);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("secret-tool not found – please install libsecret.", ex);
        }
    }
}

/// <summary>macOS: Keychain via the security tool.</summary>
public class MacKeychainKeyStore : IKeyStore
{
    public byte[] GetOrCreateKey()
    {
        var (exitCode, output) = KeyStore.Run("security", null,
            "find-generic-password", "-s", KeyStore.AppName, "-a", KeyStore.KeyName, "-w");
        if (exitCode == 0 && output.Length > 0)
            return Convert.FromBase64String(output);

        var key = KeyStore.NewKey();
        var (storeExit, _) = KeyStore.Run("security", null,
            "add-generic-password", "-U", "-s", KeyStore.AppName, "-a", KeyStore.KeyName,
            "-w", Convert.ToBase64String(key));
        if (storeExit != 0)
            throw new InvalidOperationException("Could not store the master key in the keychain.");
        return key;
    }
}

/// <summary>Windows: master key stored in %APPDATA%, bound to the user via DPAPI.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class DpapiKeyStore : IKeyStore
{
    private static readonly string KeyPath = Path.Combine(AppPaths.DataDirectory, $"{KeyStore.KeyName}.bin");

    public byte[] GetOrCreateKey()
    {
        if (File.Exists(KeyPath))
            return ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);

        var key = KeyStore.NewKey();
        Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
        File.WriteAllBytes(KeyPath, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser));
        return key;
    }
}
