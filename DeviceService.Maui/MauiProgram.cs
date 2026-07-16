using System.Reflection;
using CommunityToolkit.Maui;
using DeviceService.Shared;
using DeviceService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceService.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddHttpClient<ApiClient>(client =>
            {
                var apiBaseUrl = typeof(MauiProgram).Assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(attribute => attribute.Key == "DeviceServiceApiBaseUrl")?.Value
                    ?? "http://localhost:5113/";
                client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());
            builder.Services.AddSingleton(DeviceService.Shared.AuthSession.Current);
            builder.Services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<DeviceService.Shared.AuthSession>());
            builder.Services.AddSingleton<IAccessTokenStore>(sp => sp.GetRequiredService<DeviceService.Shared.AuthSession>());
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
