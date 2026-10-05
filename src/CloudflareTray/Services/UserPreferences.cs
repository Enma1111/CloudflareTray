using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CloudflareTray.Services;

/// <summary>
/// Small settings written by the app itself (as opposed to the profiles, which the user maintains).
/// </summary>
public class UserPreferences
{
    public static string DefaultPath => Path.Combine(AppPaths.DataDirectory, "preferences.json");

    // internal rather than private so the JSON source generator (AppJsonContext) can see the type
    internal sealed record Data(bool TrayHintDismissed = false);

    private readonly string _path;
    private readonly ILogger<UserPreferences> _logger;
    private Data _data;

    public UserPreferences(string path, ILogger<UserPreferences> logger)
    {
        _path = path;
        _logger = logger;
        _data = Load();
    }

    public bool TrayHintDismissed
    {
        get => _data.TrayHintDismissed;
        set
        {
            _data = _data with { TrayHintDismissed = value };
            Save();
        }
    }

    private Data Load()
    {
        try
        {
            if (!File.Exists(_path)) return new Data();
            return JsonFile.Read(_path, AppJsonContext.Default.Data);
        }
        catch (Exception ex)
        {
            // Broken file: use defaults; it gets overwritten on the next save
            _logger.LogWarning(ex, "Could not read preferences from {Path}", _path);
            return new Data();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            JsonFile.WriteAtomic(_path, _data, AppJsonContext.Default.Data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write preferences to {Path}", _path);
        }
    }
}
