using DeviceService.Core.Entities;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;

namespace DeviceService.Services;

public interface IAccessTokenProvider
{
    string? AccessToken { get; }
}

public interface IAccessTokenStore : IAccessTokenProvider
{
    void SetAccessToken(string? token);
}

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class NewTicketCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
public class CreateServiceTicketRequest
{
    public int? CustomerId { get; set; }
    public NewTicketCustomerRequest? NewCustomer { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ServiceTicketListItem
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int StatusValue { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ServiceTicketDetailItem : ServiceTicketListItem
{
    public string Brand { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
    public string? TrackingUrl { get; set; }
    public bool EmailSent { get; set; }
    public string? EmailMessage { get; set; }
    public List<StatusHistoryItem> StatusHistories
 { get; set; } = new();
}

public class StatusHistoryItem
{
    public DateTime ChangedAt { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class StatusOptionItem
{
    public int Value { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class UpdateServiceTicketRequest
{
    public int Status { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
}

public class EmailSendResult
{
    public int ServiceTicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string TrackingToken { get; set; } = string.Empty;
    public string TrackingUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class TrackingInfoResult
{
    public TrackingDeviceInfo DeviceInfo { get; set; } = new();
    public string CurrentStatus { get; set; } = string.Empty;
    public decimal? EstimatedPrice { get; set; }
    public List<TrackingHistoryResult> StatusHistory { get; set; } = new();
}

public class TrackingChallengeResult
{
    public string MaskedPhoneNumber { get; set; } = string.Empty;
    public int AttemptsRemaining { get; set; }
}

public class TrackingPhoneVerificationRequest
{
    public string Token { get; set; } = string.Empty;
    public string PhoneLastFour { get; set; } = string.Empty;
}

public class TrackingDeviceInfo
{
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string Complaint { get; set; } = string.Empty;
}

public class TrackingHistoryResult
{
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}

public class AuthResponse { public string Token { get; set; } = string.Empty; public AuthUser User { get; set; } = new(); }
public class AuthUser { public int Id { get; set; } public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; public int? CustomerId { get; set; } }
public class RegisterAccountRequest { public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public bool IsService { get; set; } public string? BusinessName { get; set; } public string? TaxNumber { get; set; } public string? BusinessAddress { get; set; } public string? ContactName { get; set; } public string? ServiceRegistrationCode { get; set; } }
public class CustomerDashboard { public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public List<CustomerDashboardTicket> Tickets { get; set; } = new(); }
public class CustomerDashboardTicket { public int Id { get; set; } public string TicketNumber { get; set; } = string.Empty; public string DeviceName { get; set; } = string.Empty; public string Brand { get; set; } = string.Empty; public string? SerialNumber { get; set; } public DateTime CreatedDate { get; set; } public string Status { get; set; } = string.Empty; public decimal? EstimatedPrice { get; set; } public string TrackingToken { get; set; } = string.Empty; public List<CustomerDashboardStatusHistory> StatusHistories { get; set; } = new(); }
public class CustomerDashboardStatusHistory { public string Status { get; set; } = string.Empty; public DateTime ChangedAt { get; set; } public string? Notes { get; set; } }

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider? _accessTokenProvider;
    public static string? AccessToken { get; set; }
    public string LastErrorMessage { get; private set; } = string.Empty;

    public ApiClient(HttpClient httpClient, IAccessTokenProvider? accessTokenProvider = null)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        ApplyAccessToken();
    }

    public void SetAccessToken(string? token)
    {
        AccessToken = token;
        if (_accessTokenProvider is IAccessTokenStore tokenStore)
        {
            tokenStore.SetAccessToken(token);
        }

        ApplyAccessToken();
    }

    private void ApplyAccessToken()
    {
        var token = _accessTokenProvider?.AccessToken ?? AccessToken;
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        ApplyAccessToken();
        LastErrorMessage = string.Empty;
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/login", new { email, password });
            if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<AuthResponse>();
            LastErrorMessage = await ReadErrorAsync(response, "E-posta veya şifre hatalı.");
            return null;
        }
        catch (HttpRequestException) { LastErrorMessage = "API sunucusuna ulaşılamıyor. DeviceService.API projesinin çalıştığını kontrol edin."; return null; }
        catch (TaskCanceledException) { LastErrorMessage = "API isteği zaman aşımına uğradı."; return null; }
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterAccountRequest request)
    {
        ApplyAccessToken();
        LastErrorMessage = string.Empty;
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/register", request);
            if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<AuthResponse>();
            LastErrorMessage = await ReadErrorAsync(response, "Kayıt oluşturulamadı.");
            return null;
        }
        catch (HttpRequestException) { LastErrorMessage = "API sunucusuna ulaşılamıyor. DeviceService.API projesinin çalıştığını kontrol edin."; return null; }
        catch (TaskCanceledException) { LastErrorMessage = "API isteği zaman aşımına uğradı."; return null; }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() ?? fallback
                : fallback;
        }
        catch { return fallback; }
    }

    public async Task<CustomerDashboard?> GetCustomerDashboardAsync()
    {
        ApplyAccessToken();
        try { return await _httpClient.GetFromJsonAsync<CustomerDashboard>("api/Auth/my-tickets"); } catch { return null; }
    }

    // Tracking (Public)
    public async Task<TrackingChallengeResult?> StartTrackingVerificationAsync(string token)
    {
        ApplyAccessToken();
        LastErrorMessage = string.Empty;
        try
        {
            using var response = await _httpClient.GetAsync($"api/Tracking/{Uri.EscapeDataString(token)}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<TrackingChallengeResult>();

            LastErrorMessage = await ReadErrorAsync(response, "Takip kayd\u0131 bulunamad\u0131.");
            return null;
        }
        catch
        {
            LastErrorMessage = "Takip sunucusuna ula\u015f\u0131lam\u0131yor.";
            return null;
        }
    }

    public async Task<TrackingInfoResult?> VerifyTrackingPhoneAsync(TrackingPhoneVerificationRequest request)
    {
        ApplyAccessToken();
        LastErrorMessage = string.Empty;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/Tracking/verify", request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<TrackingInfoResult>();

            LastErrorMessage = await ReadErrorAsync(response, "Telefon do\u011frulamas\u0131 ba\u015far\u0131s\u0131z.");
            return null;
        }
        catch
        {
            LastErrorMessage = "Takip sunucusuna ula\u015f\u0131lam\u0131yor.";
            return null;
        }
    }

    // Customers
    public async Task<List<Customer>> GetCustomersAsync()
    {
        ApplyAccessToken();
        try
        {
            return await _httpClient.GetFromJsonAsync<List<Customer>>("api/Customers") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<Customer?> CreateCustomerAsync(Customer customer)
    {
        ApplyAccessToken();
        try
        {
            var request = new CreateCustomerRequest
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                PhoneNumber = customer.PhoneNumber,
                Email = customer.Email
            };

            var response = await _httpClient.PostAsJsonAsync("api/Customers", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API Hatası: {response.StatusCode} - {errorContent}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<Customer>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateCustomer Exception: {ex.Message}");
            return null;
        }
    }

    // Devices
    public async Task<List<Device>> GetDevicesAsync()
    {
        ApplyAccessToken();
        try
        {
            return await _httpClient.GetFromJsonAsync<List<Device>>("api/Devices") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<Device?> CreateDeviceAsync(Device device)
    {
        ApplyAccessToken();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Devices", device);
            return await response.Content.ReadFromJsonAsync<Device>();
        }
        catch
        {
            return null;
        }
    }

    // Service Tickets
    public async Task<List<ServiceTicketListItem>> GetServiceTicketListAsync()
    {
        ApplyAccessToken();
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ServiceTicketListItem>>("api/ServiceTickets") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<ServiceTicketDetailItem?> GetServiceTicketDetailAsync(int id)
    {
        ApplyAccessToken();
        try
        {
            return await _httpClient.GetFromJsonAsync<ServiceTicketDetailItem>($"api/ServiceTickets/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<StatusOptionItem>> GetServiceTicketStatusOptionsAsync()
    {
        ApplyAccessToken();
        try
        {
            return await _httpClient.GetFromJsonAsync<List<StatusOptionItem>>("api/ServiceTickets/status-options") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<string> GetNextServiceTicketNumberAsync()
    {
        ApplyAccessToken();
        try
        {
            using var response = await _httpClient.GetAsync("api/ServiceTickets/next-number");
            if (!response.IsSuccessStatusCode)
                return "SF-000001";

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return document.RootElement.GetProperty("ticketNumber").GetString() ?? "SF-000001";
        }
        catch
        {
            return "SF-000001";
        }
    }

    public async Task<ServiceTicketDetailItem?> CreateServiceTicketAsync(CreateServiceTicketRequest request)
    {
        ApplyAccessToken();
        LastErrorMessage = string.Empty;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/ServiceTickets", request);
            if (!response.IsSuccessStatusCode)
            {
                LastErrorMessage = await ReadErrorAsync(response, "Servis fişi oluşturulamadı.");
                return null;
            }


            return await response.Content.ReadFromJsonAsync<ServiceTicketDetailItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateServiceTicket Exception: {ex.Message}");
            return null;
        }
    }

    public async Task<ServiceTicketDetailItem?> UpdateServiceTicketAsync(int id, UpdateServiceTicketRequest request)
    {
        ApplyAccessToken();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/ServiceTickets/{id}", request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ServiceTicketDetailItem>();
        }
        catch
        {
            return null;
        }
    }

    // Email
    public async Task<EmailSendResult?> SendEmailAsync(int ticketId)
    {
        ApplyAccessToken();
        LastErrorMessage = string.Empty;
        try
        {
            var response = await _httpClient.PostAsync($"api/ServiceTickets/{ticketId}/send-email", null);
            if (!response.IsSuccessStatusCode)
            {
                LastErrorMessage = await ReadErrorAsync(response, "E-posta gönderilemedi.");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EmailSendResult>();
        }
        catch (HttpRequestException)
        {
            LastErrorMessage = "E-posta gönderme isteği API sunucusuna ulaştırılamadı.";
            return null;
        }
        catch (TaskCanceledException)
        {
            LastErrorMessage = "E-posta gönderme isteği zaman aşımına uğradı.";
            return null;
        }
    }
}
