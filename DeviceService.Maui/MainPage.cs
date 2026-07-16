using DeviceService.Maui.Components;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace DeviceService.Maui;

public sealed class MainPage : ContentPage
{
    public MainPage()
    {
        var webView = new BlazorWebView { HostPage = "wwwroot/index.html" };
        webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Routes)
        });

        Content = webView;
    }
}
