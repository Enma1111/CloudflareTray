using CloudflareTray.Services;

namespace CloudflareTray.Tests;

public class AppPathsTests
{
    /// <summary>Without ~/.config, GetFolderPath returned "" and the app crashed on startup.</summary>
    [Fact]
    public void DataDirectory_IsAbsolute_WhenConfigDirectoryDoesNotExistYet()
    {
        if (!OperatingSystem.IsLinux()) return;

        var root = Directory.CreateTempSubdirectory("cftray-").FullName;
        var configHome = Path.Combine(root, "noch-nicht-da");
        var previous = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configHome);

            Assert.Equal(Path.Combine(configHome, "CloudflareTray"), AppPaths.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previous);
            Directory.Delete(root, recursive: true);
        }
    }
}
