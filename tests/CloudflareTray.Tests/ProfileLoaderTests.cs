using System.Security.Cryptography;
using CloudflareTray.Models;
using CloudflareTray.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudflareTray.Tests;

public class ProfileLoaderTests : IDisposable
{
    private sealed class InMemoryKeyStore : IKeyStore
    {
        private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
        public byte[] GetOrCreateKey() => _key;
    }

    private readonly string _dir = Directory.CreateTempSubdirectory("cftray-").FullName;
    private readonly ProfileLoader _loader = new(new SecretProtector(new InMemoryKeyStore()), NullLogger<ProfileLoader>.Instance);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string file, string json) => File.WriteAllText(Path.Combine(_dir, file), json);

    private const string Config = """
        { "Tunnels": [
            { "Name": "NAS", "Hostname": "nas.firma.de", "Protocol": "Smb",
              "LocalAddress": "localhost:4450", "Credential": "default" },
            { "Name": "Web", "Hostname": "web.firma.de", "Protocol": "Tcp",
              "LocalAddress": "localhost:8080" } ] }
        """;

    private const string Credentials = """
        { "Credentials": { "default": { "ClientId": "id.access", "ClientSecret": "geheim" } } }
        """;

    [Fact]
    public void LoadAll_FindsProfilesByFilePrefix()
    {
        Write("firma.config.json", Config);
        Write("firma.credentials.json", Credentials);
        Write("kunde.config.json", """{ "Tunnels": [] }""");

        var profiles = _loader.LoadAll(_dir);

        Assert.Equal(["firma", "kunde"], profiles.Select(p => p.Name));
    }

    [Fact]
    public void Load_ResolvesCredentialReference()
    {
        Write("firma.config.json", Config);
        Write("firma.credentials.json", Credentials);

        var profile = _loader.Load(_dir, "firma");
        var tunnels = profile.Configuration.Tunnels;

        Assert.Equal(new("id.access", "geheim"), profile.GetCredential(tunnels[0]));
        Assert.Null(profile.GetCredential(tunnels[1]));
    }

    [Fact]
    public void Load_EncryptsPlaintextSecretsOnDisk()
    {
        Write("firma.config.json", Config);
        Write("firma.credentials.json", Credentials);

        _loader.Load(_dir, "firma");
        var onDisk = File.ReadAllText(Path.Combine(_dir, "firma.credentials.json"));

        Assert.DoesNotContain("geheim", onDisk);
        Assert.Contains("enc:v1:", onDisk);
        Assert.Contains("id.access", onDisk);

        // The second load reads the encrypted file
        var profile = _loader.Load(_dir, "firma");
        Assert.Equal("geheim", profile.Credentials["default"].ClientSecret);
    }

    [Fact]
    public void Load_UnknownCredentialReference_Throws()
    {
        Write("firma.config.json", Config);
        Write("firma.credentials.json", """{ "Credentials": {} }""");

        var ex = Assert.Throws<InvalidDataException>(() => _loader.Load(_dir, "firma"));
        Assert.Contains("default", ex.Message);
    }

    [Fact]
    public void Load_DuplicateTunnelName_Throws()
    {
        Write("firma.config.json", """
            { "Tunnels": [
                { "Name": "Web", "Hostname": "a.firma.de", "Protocol": "Tcp", "LocalAddress": "localhost:1" },
                { "Name": "Web", "Hostname": "b.firma.de", "Protocol": "Tcp", "LocalAddress": "localhost:2" } ] }
            """);

        var ex = Assert.Throws<InvalidDataException>(() => _loader.Load(_dir, "firma"));
        Assert.Contains("Web", ex.Message);
    }

    [Fact]
    public void Save_RoundTripsAndEncryptsSecrets()
    {
        var tunnel = new TunnelItemConfig("SSH", "ssh.firma.de", Protocol.Ssh, "localhost:2222", "default");
        var profile = new TunnelProfile("neu", new TunnelConfiguration([tunnel]),
            new Dictionary<string, TunnelCredentialItem> { ["default"] = new("id.access", "geheim") });

        _loader.Save(_dir, profile);
        var loaded = _loader.Load(_dir, "neu");

        Assert.Equal([tunnel], loaded.Configuration.Tunnels);
        Assert.Equal("geheim", loaded.Credentials["default"].ClientSecret);
        Assert.DoesNotContain("geheim", File.ReadAllText(Path.Combine(_dir, "neu.credentials.json")));
    }
}
