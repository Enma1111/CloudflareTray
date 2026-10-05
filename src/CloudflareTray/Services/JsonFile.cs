using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CloudflareTray.Models;

namespace CloudflareTray.Services;

/// <summary>
/// Source-generated metadata for all of the app's JSON files.
/// Reflection-based serialization is not available under Native AOT.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(TunnelConfiguration))]
[JsonSerializable(typeof(TunnelCredential))]
[JsonSerializable(typeof(UserPreferences.Data))]
internal partial class AppJsonContext : JsonSerializerContext;

/// <summary>Reads and writes JSON files; never writes directly to the target file.</summary>
internal static class JsonFile
{
    public static T Read<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new InvalidDataException($"{path} is empty or invalid.");
    }

    /// <summary>
    /// Writes to "{path}.tmp" first and only then replaces the target file. If the app crashes
    /// midway, the old file stays fully intact. On Unix the file is readable by the user only.
    /// </summary>
    public static void WriteAtomic<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        var tempPath = path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(tempPath, options))
        {
            JsonSerializer.Serialize(stream, value, typeInfo);
            // Flush to disk before renaming – otherwise a power failure can leave an empty file behind
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
