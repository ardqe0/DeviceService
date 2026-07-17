using DeviceService.Services;

namespace DeviceService.Shared;

public sealed class AuthSession : IAccessTokenStore
{
    public static AuthSession Current { get; } = new();
    public AuthUser? User { get; private set; }
    public string? Token { get; private set; }
    public string? AccessToken => Token;
    public bool IsAuthenticated => User is not null;
    public bool IsService => User?.Role == "Service";
    public bool IsCustomer => User?.Role == "Customer";
    public event EventHandler? Changed;

    public void Start(AuthResponse response)
    {
        User = response.User;
        SetAccessToken(response.Token);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void End()
    {
        User = null;
        SetAccessToken(null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetAccessToken(string? token)
    {
        Token = token;
        ApiClient.AccessToken = token;

        if (string.IsNullOrWhiteSpace(token) && User is not null)
        {
            User = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
