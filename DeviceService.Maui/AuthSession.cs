using DeviceService.Services;
using Microsoft.Maui.Storage;
using System.Text;
using System.Text.Json;

namespace DeviceService.Maui;

public sealed class AuthSession
{
    private const string RememberedSessionKey = "device-service-remembered-session";

    public static AuthSession Current { get; } = new();
    public AuthUser? User { get; private set; }
    public string? Token { get; private set; }
    public bool IsAuthenticated => User is not null;
    public bool IsService => User?.Role == "Service";
    public bool IsCustomer => User?.Role == "Customer";
    public event EventHandler? Changed;

    public async Task RestoreAsync()
    {
        try
        {
            var storedSession = await SecureStorage.Default.GetAsync(RememberedSessionKey);
            var response = string.IsNullOrWhiteSpace(storedSession)
                ? null
                : JsonSerializer.Deserialize<AuthResponse>(storedSession);

            if (response is null || string.IsNullOrWhiteSpace(response.Token) || IsExpired(response.Token))
            {
                SecureStorage.Default.Remove(RememberedSessionKey);
                return;
            }

            Apply(response);
        }
        catch
        {
            SecureStorage.Default.Remove(RememberedSessionKey);
        }
    }

    public async Task StartAsync(AuthResponse response, bool rememberMe)
    {
        Apply(response);

        if (rememberMe)
            await SecureStorage.Default.SetAsync(RememberedSessionKey, JsonSerializer.Serialize(response));
        else
            SecureStorage.Default.Remove(RememberedSessionKey);
    }

    public void End()
    {
        User = null;
        Token = null;
        ApiClient.AccessToken = null;
        SecureStorage.Default.Remove(RememberedSessionKey);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(AuthResponse response)
    {
        User = response.User;
        Token = response.Token;
        ApiClient.AccessToken = Token;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsExpired(string token)
    {
        try
        {
            var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("exp", out var expiry)
                && DateTimeOffset.FromUnixTimeSeconds(expiry.GetInt64()) <= DateTimeOffset.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}