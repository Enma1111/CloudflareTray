using System.Security.Cryptography;
using CloudflareTray.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudflareTray.Tests;

public class ProfilesOptionsTests : IDisposable
{
    private sealed class InMemoryKeyStore : IKeyStore
    {
        private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetOrCreateKey() => _key;
    }

    private readonly string _dir = Directory.CreateTempSubdirectory("cftray-").FullName;
    private readonly ServiceProvider _services;

    public ProfilesOptionsTests()
    {
        _services = new ServiceCollection()
            .AddLogging()
            .AddCloudflareTray(_dir)
            .AddSingleton<IKeyStore, InMemoryKeyStore>()
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private IOptionsMonitor<ProfilesOptions> Monitor => _services.GetRequiredService<IOptionsMonitor<ProfilesOptions>>();

    private void Write(string file, string json) => File.WriteAllText(Path.Combine(_dir, file), json);

    private const string WebTunnel = """
        { "Tunnels": [ { "Name": "Web", "Hostname": "web.firma.de", "Protocol": "Tcp", "LocalAddress": "localhost:8080" } ] }
        """;

    [Fact]
    public void BrokenProfile_DoesNotBlockOthers()
    {
        Write("firma.config.json", WebTunnel);
        Write("kaputt.config.json", "{ nicht json");

        var options = Monitor.CurrentValue;

        Assert.Equal("firma", Assert.Single(options.Profiles).Name);
        Assert.True(options.Errors.ContainsKey("kaputt"));
        Assert.Null(options.DirectoryError);
    }

    [Fact]
    public void ProfilesDifferingOnlyInCase_SecondIsRejected()
    {
        Write("firma.config.json", WebTunnel);
        Write("Firma.config.json", WebTunnel);
        // On a case-insensitive file system there is only one file
        if (Directory.GetFiles(_dir).Length == 1) return;

        var options = Monitor.CurrentValue;

        var profile = Assert.Single(options.Profiles);
        var error = Assert.Single(options.Errors);
        Assert.NotEqual(profile.Name, error.Key);
        Assert.Equal(profile.Name, error.Key, ignoreCase: true);
    }

    [Fact]
    public async Task NewProfileFile_TriggersOnChange()
    {
        var monitor = Monitor;
        Assert.Empty(monitor.CurrentValue.Profiles);

        var changed = new TaskCompletionSource<ProfilesOptions>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = monitor.OnChange(o => { if (o.Profiles.Count > 0) changed.TrySetResult(o); });

        Write("kollege.config.json", WebTunnel);

        var options = await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("kollege", Assert.Single(options.Profiles).Name);
        Assert.Same(options, monitor.CurrentValue);
    }

    [Fact]
    public async Task EditedProfileFile_TriggersOnChange()
    {
        Write("firma.config.json", WebTunnel);
        var monitor = Monitor;
        Assert.Equal("localhost:8080", monitor.CurrentValue.Profiles[0].Configuration.Tunnels[0].LocalAddress);

        var changed = new TaskCompletionSource<ProfilesOptions>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = monitor.OnChange(o => changed.TrySetResult(o));

        Write("firma.config.json", WebTunnel.Replace("8080", "9090"));

        var options = await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("localhost:9090", options.Profiles[0].Configuration.Tunnels[0].LocalAddress);
    }
}
