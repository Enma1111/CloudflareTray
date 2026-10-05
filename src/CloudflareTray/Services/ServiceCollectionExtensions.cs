using CloudflareTray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudflareTray.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCloudflareTray(this IServiceCollection services, string profileDirectory)
    {
        services.AddSingleton<IKeyStore>(_ => KeyStore.CreateDefault());
        services.AddSingleton<SecretProtector>();
        services.AddSingleton<ProfileLoader>();
        services.AddSingleton<CloudflareTunnelService>();
        services.AddSingleton(sp => new UserPreferences(UserPreferences.DefaultPath, sp.GetRequiredService<ILogger<UserPreferences>>()));

        // IOptionsMonitor<ProfilesOptions>: profiles are reloaded when the JSON files change
        services.AddSingleton(new ProfileDirectory(profileDirectory));
        services.AddOptions<ProfilesOptions>();
        services.AddSingleton<IConfigureOptions<ProfilesOptions>, ConfigureProfilesOptions>();
        services.AddSingleton<IOptionsChangeTokenSource<ProfilesOptions>, ProfileDirectoryChangeTokenSource>();

        services.AddSingleton<MainWindowViewModel>();
        return services;
    }
}
