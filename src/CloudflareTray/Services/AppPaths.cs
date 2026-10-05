using System;
using System.IO;

namespace CloudflareTray.Services;

/// <summary>The app's user directory: ~/.config/CloudflareTray or %APPDATA%\CloudflareTray.</summary>
public static class AppPaths
{
    // Create: on Linux/macOS, GetFolderPath otherwise returns "" if ~/.config doesn't exist yet,
    // and all paths would be relative to the working directory
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        KeyStore.AppName);
}
