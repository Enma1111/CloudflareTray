using System;

namespace CloudflareTray.Services;

/// <summary>
/// Checks whether a tray icon can be shown at all. On Linux, Avalonia needs a StatusNotifierWatcher
/// on the session bus for this – on GNOME it only exists with the AppIndicator extension.
/// </summary>
public static class TrayHost
{
    public const string MissingHint =
        "No tray available – closing only minimizes the window. On GNOME, install the " +
        "“AppIndicator and KStatusNotifierItem Support” extension (gnome-shell-extension-appindicator).";

    /// <summary>Checked anew on every call, since the extension can be enabled at runtime.</summary>
    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux()) return true;

        try
        {
            var (exitCode, output) = KeyStore.Run("gdbus", null,
                "call", "--session",
                "--dest", "org.freedesktop.DBus",
                "--object-path", "/org/freedesktop/DBus",
                "--method", "org.freedesktop.DBus.NameHasOwner",
                "org.kde.StatusNotifierWatcher");
            return exitCode == 0 && ParseNameHasOwner(output);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Without gdbus, better to minimize than to hide the window where it can't be reached
            return false;
        }
    }

    /// <summary>gdbus replies with "(true,)" or "(false,)".</summary>
    public static bool ParseNameHasOwner(string output) =>
        output.Trim().StartsWith("(true", StringComparison.Ordinal);
}
