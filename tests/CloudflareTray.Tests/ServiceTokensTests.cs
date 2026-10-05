using System.Security.Cryptography;
using CloudflareTray.Models;
using CloudflareTray.Services;
using CloudflareTray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudflareTray.Tests;

public class ServiceTokensTests : IDisposable
{
    private sealed class InMemoryKeyStore : IKeyStore
    {
        private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetOrCreateKey() => _key;
    }

    private readonly string _dir = Directory.CreateTempSubdirectory("cftray-").FullName;
    private readonly ServiceProvider _services;
    private readonly MainWindowViewModel _vm;

    public ServiceTokensTests()
    {
        _services = new ServiceCollection()
            .AddLogging()
            .AddCloudflareTray(_dir)
            .AddSingleton<IKeyStore, InMemoryKeyStore>()
            .AddSingleton(new UserPreferences(Path.Combine(_dir, "preferences.json"), NullLogger<UserPreferences>.Instance))
            .BuildServiceProvider();

        WriteConfig("default");
        Write("firma.credentials.json", """
            { "Credentials": { "default": { "ClientId": "id.access", "ClientSecret": "geheim" },
                               "alt": { "ClientId": "alt.access", "ClientSecret": "alt-geheim" } } }
            """);
        Write("leer.config.json", """{ "Tunnels": [] }""");

        _vm = _services.GetRequiredService<MainWindowViewModel>();
        _vm.Reload();
    }

    public void Dispose()
    {
        _vm.Dispose();
        _services.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private void Write(string file, string json) => File.WriteAllText(Path.Combine(_dir, file), json);

    private void WriteConfig(string credential) => Write("firma.config.json", $$"""
        { "Tunnels": [ { "Name": "Web", "Hostname": "web.firma.de", "Protocol": "Tcp",
                         "LocalAddress": "localhost:8080", "Credential": "{{credential}}" } ] }
        """);

    private TunnelProfile LoadFromDisk(string name) => _services.GetRequiredService<ProfileLoader>().Load(_dir, name);

    private ServiceTokenItemViewModel Token(string name) =>
        _vm.TokenGroups.Single(g => g.Name == "firma").Tokens.Single(t => t.Name == name);

    [Fact]
    public void Tab_ShowsAllProfilesWithUsage()
    {
        Assert.Equal(["firma", "leer"], _vm.TokenGroups.Select(g => g.Name));
        Assert.True(_vm.TokenGroups[1].IsEmpty);

        Assert.Equal(["Web"], Token("default").UsedBy);
        Assert.True(Token("alt").IsUnused);
        Assert.False(Token("default").RequestDeleteCommand.CanExecute(null));
        Assert.True(Token("alt").RequestDeleteCommand.CanExecute(null));
    }

    [Fact]
    public void Edit_EmptySecret_KeepsOldSecret()
    {
        var token = Token("default");
        token.EditCommand.Execute(null);
        token.EditClientId = "neu.access";
        token.ApplyEditCommand.Execute(null);

        Assert.False(token.IsEditing);
        Assert.Equal(new("neu.access", "geheim"), LoadFromDisk("firma").Credentials["default"]);
        Assert.Equal("neu.access", Token("default").ClientId);
    }

    [Fact]
    public void Edit_NewSecret_ReplacesOnlyThisToken()
    {
        var token = Token("default");
        token.EditCommand.Execute(null);
        token.EditSecret = "rotiert";
        token.ApplyEditCommand.Execute(null);

        var credentials = LoadFromDisk("firma").Credentials;
        Assert.Equal(new("id.access", "rotiert"), credentials["default"]);
        Assert.Equal(new("alt.access", "alt-geheim"), credentials["alt"]);
    }

    [Fact]
    public void Edit_EmptyClientId_ShowsErrorAndKeepsEditing()
    {
        var token = Token("default");
        token.EditCommand.Execute(null);
        token.EditClientId = " ";
        token.ApplyEditCommand.Execute(null);

        Assert.True(token.IsEditing);
        Assert.NotNull(token.Error);
        Assert.Equal("id.access", LoadFromDisk("firma").Credentials["default"].ClientId);
    }

    [Fact]
    public void Edit_SurvivesReload()
    {
        var token = Token("default");
        token.EditCommand.Execute(null);
        token.EditClientId = "halb.fertig";

        // e.g. a colleague drops in a new file
        Write("kunde.config.json", """{ "Tunnels": [] }""");
        _vm.Reload();

        Assert.Same(token, Token("default"));
        Assert.True(token.IsEditing);
        Assert.Equal("halb.fertig", token.EditClientId);
    }

    [Fact]
    public void Delete_UnusedToken_AfterConfirmation()
    {
        var token = Token("alt");
        token.RequestDeleteCommand.Execute(null);
        Assert.True(LoadFromDisk("firma").Credentials.ContainsKey("alt"));

        token.ConfirmDeleteCommand.Execute(null);

        Assert.False(LoadFromDisk("firma").Credentials.ContainsKey("alt"));
        Assert.DoesNotContain(_vm.TokenGroups[0].Tokens, t => t.Name == "alt");
    }

    [Fact]
    public void Delete_TokenUsedInMeantime_IsNotDeleted()
    {
        var token = Token("alt");
        token.RequestDeleteCommand.Execute(null);

        // A tunnel uses the token before the file watcher fires
        WriteConfig("alt");
        token.ConfirmDeleteCommand.Execute(null);

        Assert.True(LoadFromDisk("firma").Credentials.ContainsKey("alt"));
        Assert.Contains("Web", token.Error);
        Assert.False(token.IsConfirmingDelete);
    }

    [Fact]
    public void TrayHint_CanBeDismissedPermanently()
    {
        Assert.False(_vm.ShowTrayHint);
        _vm.IsTrayAvailable = false;
        Assert.True(_vm.ShowTrayHint);

        _vm.DismissTrayHintCommand.Execute(null);

        Assert.False(_vm.ShowTrayHint);
        var reloaded = new UserPreferences(Path.Combine(_dir, "preferences.json"), NullLogger<UserPreferences>.Instance);
        Assert.True(reloaded.TrayHintDismissed);
    }

    [Fact]
    public void Preferences_BrokenFile_FallsBackToDefaults()
    {
        var path = Path.Combine(_dir, "kaputt.json");
        File.WriteAllText(path, "{ nicht json");

        var preferences = new UserPreferences(path, NullLogger<UserPreferences>.Instance);

        Assert.False(preferences.TrayHintDismissed);
        preferences.TrayHintDismissed = true;
        Assert.True(new UserPreferences(path, NullLogger<UserPreferences>.Instance).TrayHintDismissed);
    }
}
