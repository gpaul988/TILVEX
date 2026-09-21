using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TILVEX;

/// <summary>
/// Optional online boundary. The POS never requires this service to complete a sale.
/// Providers can be replaced later with a Google API implementation without changing POS workflows.
/// </summary>
public interface IPosIntegrationService
{
    Task<IntegrationResult> SyncAsync(PosData data, CancellationToken cancellationToken = default);
    Task<IntegrationResult> SendNotificationAsync(string channel, string message, CancellationToken cancellationToken = default);
}

public sealed record IntegrationResult(bool Success, IntegrationConnectionStatus Status, string Message);

public sealed class SharedProfileService
{
    private readonly HttpClient _client;
    private readonly IntegrationSettings _settings;

    public SharedProfileService(IntegrationSettings settings, HttpClient? client = null)
    {
        _settings = settings;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public async Task<IntegrationResult> SyncAsync(UserAccount account, BusinessSettings business, string password, string mode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ProfileSyncEndpoint))
            return new(false, IntegrationConnectionStatus.NotConfigured, "Shared profile endpoint is not configured.");

        try
        {
            var endpoint = _settings.ProfileSyncEndpoint.TrimEnd('/') + "/api/auth/profile/sync";
            var businessType = business.BusinessType switch
            {
                BusinessType.BeautySalon => "BEAUTY_SALON",
                BusinessType.ComputerRepair => "COMPUTER_REPAIR",
                BusinessType.GasPetrolStation => "GAS_PETROL_STATION",
                _ => business.BusinessType.ToString().ToUpperInvariant()
            };
            var payload = JsonSerializer.Serialize(new
            {
                mode,
                businessName = business.BusinessName,
                businessType,
                ownerName = account.UserName,
                email = account.Email,
                password
            });
            using var response = await _client.PostAsync(endpoint, new StringContent(payload, Encoding.UTF8, "application/json"), cancellationToken);
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? new(true, IntegrationConnectionStatus.Ready, "Shared profile synchronized.")
                : new(false, response.StatusCode == System.Net.HttpStatusCode.Conflict ? IntegrationConnectionStatus.Error : IntegrationConnectionStatus.Offline, message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, IntegrationConnectionStatus.Offline, $"Profile remains local until connection returns: {ex.Message}");
        }
    }

    public async Task<(UserAccount? Account, BusinessSettings? Business, string Message)> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ProfileSyncEndpoint))
                return (null, null, "Shared profile endpoint is not configured.");
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    mode = "LOGIN",
                    businessName = "TILVEX",
                    businessType = "RETAIL",
                    ownerName = email,
                    email,
                    password
                });
                using var response = await _client.PostAsync(
                    _settings.ProfileSyncEndpoint.TrimEnd('/') + "/api/auth/profile/sync",
                    new StringContent(payload, Encoding.UTF8, "application/json"), cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode) return (null, null, body);
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                var account = new UserAccount
                {
                    UserName = root.GetProperty("name").GetString() ?? email,
                    Email = email,
                    PasswordHash = PosStore.ComputePasswordHash(password),
                    Role = root.GetProperty("role").GetString() ?? "Cashier"
                };
                var business = new BusinessSettings
                {
                    BusinessName = root.TryGetProperty("businessName", out var name) ? name.GetString() ?? "TILVEX" : "TILVEX"
                };
                if (root.TryGetProperty("businessType", out var type))
                {
                    business.BusinessType = type.GetString() switch
                    {
                        "BEAUTY_SALON" => BusinessType.BeautySalon,
                        "COMPUTER_REPAIR" => BusinessType.ComputerRepair,
                        "GAS_PETROL_STATION" => BusinessType.GasPetrolStation,
                        _ when Enum.TryParse<BusinessType>(type.GetString(), true, out var parsedType) => parsedType,
                        _ => BusinessType.Other
                    };
                }
                return (account, business, "Shared profile loaded.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            {
                return (null, null, $"Profile remains local until connection returns: {ex.Message}");
            }
        }
}

public sealed class HttpIntegrationService : IPosIntegrationService
{
    private readonly HttpClient _client;
    private readonly IntegrationSettings _settings;

    public HttpIntegrationService(IntegrationSettings settings, HttpClient? client = null)
    {
        _settings = settings;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<IntegrationResult> SyncAsync(PosData data, CancellationToken cancellationToken = default)
    {
        if (!_settings.OnlineSyncEnabled || string.IsNullOrWhiteSpace(_settings.SyncEndpoint))
            return new(false, IntegrationConnectionStatus.Offline, "Offline-first mode: sync endpoint is not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.SyncEndpoint);
            if (!string.IsNullOrWhiteSpace(_settings.SyncApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SyncApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? new(true, IntegrationConnectionStatus.Ready, $"Sync completed ({(int)response.StatusCode}).")
                : new(false, IntegrationConnectionStatus.Error, $"Sync endpoint returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, IntegrationConnectionStatus.Offline, $"Offline: {ex.Message}");
        }
    }

    public async Task<IntegrationResult> SendNotificationAsync(string channel, string message, CancellationToken cancellationToken = default)
    {
        var endpoint = channel.ToLowerInvariant() switch
        {
            "whatsapp" => _settings.WhatsAppWebhookUrl,
            "sms" => _settings.SmsWebhookUrl,
            "email" => _settings.EmailWebhookUrl,
            _ => ""
        };
        if (string.IsNullOrWhiteSpace(endpoint))
            return new(false, IntegrationConnectionStatus.NotConfigured, $"{channel} notification endpoint is not configured.");

        try
        {
            var payload = JsonSerializer.Serialize(new { channel, message, sentAt = DateTimeOffset.UtcNow });
            using var response = await _client.PostAsync(endpoint, new StringContent(payload, Encoding.UTF8, "application/json"), cancellationToken);
            return response.IsSuccessStatusCode
                ? new(true, IntegrationConnectionStatus.Ready, $"{channel} notification sent.")
                : new(false, IntegrationConnectionStatus.Error, $"{channel} endpoint returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, IntegrationConnectionStatus.Offline, $"Offline: {ex.Message}");
        }
    }
}

public static class ReportExportService
{
    public static string ExportPeriodCsv(PosData data, DateTime from, DateTime to, string path)
    {
        var sales = data.Sales.Where(s => s.CreatedAt >= from && s.CreatedAt < to).OrderBy(s => s.CreatedAt).ToList();
        var rows = new List<string> { "Receipt,Timestamp,Customer,Items,Subtotal,Discount,VAT,Total,Payment" };
        rows.AddRange(sales.Select(s => string.Join(",", new[]
        {
            Csv(s.ReceiptNumber), Csv(s.CreatedAt.ToString("O")), Csv(data.Customers.FirstOrDefault(c => c.Id == s.CustomerId)?.Name),
            Csv(s.ItemCount), Csv(s.Subtotal), Csv(s.Discount), Csv(s.Tax), Csv(s.Total), Csv(s.PaymentMethod)
        })));
        File.WriteAllLines(path, rows, Encoding.UTF8);
        return path;
    }

    public static string ExportPeriodJson(PosData data, DateTime from, DateTime to, string path)
    {
        var payload = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            from,
            to,
            sales = data.Sales.Where(s => s.CreatedAt >= from && s.CreatedAt < to).ToList(),
            refunds = data.Returns.Where(r => r.CreatedAt >= from && r.CreatedAt < to).ToList()
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static string Csv(object? value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
}
