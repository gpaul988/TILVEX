using System.Security.Cryptography;
using System.Text;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Newtonsoft.Json;

namespace TILVEX;

public sealed class SecureCredentialStore
{
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TILVEX", "secure");

    public bool Contains(string key) => File.Exists(GetPath(key));

    public void Set(string key, string value)
    {
        Directory.CreateDirectory(_folder);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GetPath(key), bytes);
    }

    public string? Get(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Delete(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path)) File.Delete(path);
    }

    private string GetPath(string key) =>
        Path.Combine(_folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");
}

public sealed class ProtectedGoogleDataStore : IDataStore
{
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TILVEX", "google-token");

    public async Task StoreAsync<T>(string key, T value)
    {
        Directory.CreateDirectory(_folder);
        var payload = JsonConvert.SerializeObject(value);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(payload), null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(GetPath(key), bytes);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return default;
        var bytes = ProtectedData.Unprotect(await File.ReadAllBytesAsync(path), null, DataProtectionScope.CurrentUser);
        return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes));
    }

    public Task DeleteAsync<T>(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        return Task.CompletedTask;
    }

    private string GetPath(string key) =>
        Path.Combine(_folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");
}

public sealed class OwnerAutomationService
{
    private static readonly string[] SheetsScopes = [SheetsService.Scope.Spreadsheets];
    private readonly PosData _data;
    private readonly SecureCredentialStore _secrets = new();
    private readonly ProtectedGoogleDataStore _tokenStore = new();
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private UserCredential? _credential;

    public OwnerAutomationService(PosData data) => _data = data;

    public bool HasSmtpPassword => _secrets.Contains("smtp-password");
    public bool HasGoogleClientSecret => _secrets.Contains("google-oauth-client-secret");

    public void SaveSmtpPassword(string password) => _secrets.Set("smtp-password", password);
    public void SaveGoogleClientSecret(string secret) => _secrets.Set("google-oauth-client-secret", secret);
    public void ClearSmtpPassword() => _secrets.Delete("smtp-password");

    public async Task<IntegrationResult> SendEmailAsync(
        string subject,
        string body,
        Guid? stableMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _data.Settings.Integrations;
        var owner = _data.Settings.OwnerEmail;
        if (string.IsNullOrWhiteSpace(owner) || !System.Net.Mail.MailAddress.TryCreate(owner, out _))
            return new(false, IntegrationConnectionStatus.NotConfigured, "Set a valid owner email in Settings.");
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || settings.SmtpPort is < 1 or > 65535)
            return new(false, IntegrationConnectionStatus.NotConfigured, "Configure the SMTP server and port in Settings.");

        var password = _secrets.Get("smtp-password");
        if (!string.IsNullOrWhiteSpace(settings.SmtpUser) && string.IsNullOrEmpty(password))
            return new(false, IntegrationConnectionStatus.NotConfigured, "Save the SMTP password in Settings.");

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(owner));
            message.To.Add(MailboxAddress.Parse(owner));
            message.Subject = subject;
            if (stableMessageId.HasValue)
                message.MessageId = $"tilvex-{stableMessageId.Value:N}@tilvex.local";
            message.Body = new TextPart("plain") { Text = body };
            using var client = new SmtpClient();
            client.Timeout = 15000;
            var socketOptions = !settings.SmtpUseTls
                ? SecureSocketOptions.None
                : settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.SmtpUser))
                await client.AuthenticateAsync(settings.SmtpUser, password!, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return new(true, IntegrationConnectionStatus.Ready, $"Email sent to {owner}.");
        }
        catch (Exception ex)
        {
            return new(false, IntegrationConnectionStatus.Error, $"Email delivery failed: {ex.Message}");
        }
    }

    public async Task<IntegrationResult> ConnectGoogleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = await AuthorizeGoogleAsync(cancellationToken);
            var service = CreateSheetsService(credential);
            var spreadsheet = await service.Spreadsheets.Get(_data.Settings.Integrations.GoogleSheetsSpreadsheetId)
                .ExecuteAsync(cancellationToken);
            await EnsureWorksheetAsync(service, cancellationToken);
            _credential = credential;
            return new(true, IntegrationConnectionStatus.Ready, $"Connected to Google Sheets: {spreadsheet.Properties.Title}.");
        }
        catch (Exception ex)
        {
            return new(false, IntegrationConnectionStatus.Error, $"Google Sheets connection failed: {ex.Message}");
        }
    }

    public async Task<IntegrationResult> SyncAuditEventsAsync(CancellationToken cancellationToken = default)
    {
        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            return await SyncAuditEventsCoreAsync(cancellationToken);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<IntegrationResult> SyncAuditEventsCoreAsync(CancellationToken cancellationToken)
    {
        var settings = _data.Settings.Integrations;
        if (!settings.GoogleSheetsConnected || string.IsNullOrWhiteSpace(settings.GoogleSheetsSpreadsheetId))
            return new(false, IntegrationConnectionStatus.NotConfigured, "Connect Google Sheets and set a spreadsheet ID to sync the activity log.");

        var pending = _data.AuditEvents.Where(item => !item.SheetsSynced).OrderBy(item => item.CreatedAt).ToList();
        if (pending.Count == 0)
            return new(true, IntegrationConnectionStatus.Ready, "Activity log is up to date.");

        try
        {
            var credential = _credential ??= await AuthorizeGoogleAsync(cancellationToken);
            var service = CreateSheetsService(credential);
            var sheetName = await EnsureWorksheetAsync(service, cancellationToken);
            var quotedSheet = QuoteSheet(sheetName);
            var existing = await service.Spreadsheets.Values.Get(settings.GoogleSheetsSpreadsheetId, $"{quotedSheet}!A:A")
                .ExecuteAsync(cancellationToken);
            var existingIds = existing.Values?
                .Where(row => row.Count > 0)
                .Select(row => row[0]?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            var missing = pending.Where(item => !existingIds.Contains(item.Id.ToString("D"))).ToList();
            if (missing.Count > 0)
            {
                var rows = missing.Select(item => (IList<object>)new List<object>
                {
                    item.Id.ToString("D"),
                    item.CreatedAt.ToString("O"),
                    item.Type,
                    item.Actor,
                    item.Summary,
                    item.Details
                }).ToList();
                var request = service.Spreadsheets.Values.Append(
                    new ValueRange { Values = rows }, settings.GoogleSheetsSpreadsheetId, $"{quotedSheet}!A:F");
                request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
                request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
                await request.ExecuteAsync(cancellationToken);
            }

            foreach (var item in pending) item.SheetsSynced = true;
            return new(true, IntegrationConnectionStatus.Ready, $"Synced {pending.Count} activity events to Google Sheets.");
        }
        catch (Exception ex)
        {
            return new(false, IntegrationConnectionStatus.Error, $"Google Sheets sync failed; events remain queued locally: {ex.Message}");
        }
    }

    public void DisconnectGoogle()
    {
        _credential = null;
        _tokenStore.ClearAsync().GetAwaiter().GetResult();
    }

    private async Task<UserCredential> AuthorizeGoogleAsync(CancellationToken cancellationToken)
    {
        var settings = _data.Settings.Integrations;
        var clientId = settings.GoogleOAuthClientId.Trim();
        var clientSecret = _secrets.Get("google-oauth-client-secret");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Enter a Google OAuth desktop client ID and client secret in Settings.");
        if (string.IsNullOrWhiteSpace(settings.GoogleSheetsSpreadsheetId))
            throw new InvalidOperationException("Enter the Google spreadsheet ID in Settings.");

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            SheetsScopes, "tilvex-owner", cancellationToken, _tokenStore);
    }

    private static SheetsService CreateSheetsService(UserCredential credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "TILVEX POS"
        });

    private async Task<string> EnsureWorksheetAsync(SheetsService service, CancellationToken cancellationToken)
    {
        var settings = _data.Settings.Integrations;
        var sheetName = string.IsNullOrWhiteSpace(settings.GoogleSheetsWorksheet) ? "Activity" : settings.GoogleSheetsWorksheet.Trim();
        var metadata = await service.Spreadsheets.Get(settings.GoogleSheetsSpreadsheetId).ExecuteAsync(cancellationToken);
        if (metadata.Sheets?.Any(sheet => sheet.Properties?.Title == sheetName) != true)
        {
            await service.Spreadsheets.BatchUpdate(
                new BatchUpdateSpreadsheetRequest
                {
                    Requests = [new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = sheetName } } }]
                }, settings.GoogleSheetsSpreadsheetId).ExecuteAsync(cancellationToken);
        }

        var range = $"{QuoteSheet(sheetName)}!A1:F1";
        var header = await service.Spreadsheets.Values.Get(settings.GoogleSheetsSpreadsheetId, range).ExecuteAsync(cancellationToken);
        var expectedHeader = new[] { "Event ID", "Timestamp", "Type", "Actor", "Summary", "Details" };
        var currentHeader = header.Values?.FirstOrDefault()?.Select(value => value?.ToString()).ToArray();
        if (currentHeader is null || !expectedHeader.SequenceEqual(currentHeader, StringComparer.Ordinal))
        {
            await service.Spreadsheets.Values.Update(
                new ValueRange { Values = [expectedHeader.Cast<object>().ToList()] },
                settings.GoogleSheetsSpreadsheetId, range)
                .ExecuteAsync(cancellationToken);
        }
        return sheetName;
    }

    private static string QuoteSheet(string name) => $"'{name.Replace("'", "''")}'";
}

public static class OwnerReportBuilder
{
    public static string Build(PosData data, DateTime from, DateTime to, string cadence, TimeZoneInfo timeZone)
    {
        var localFrom = TimeZoneInfo.ConvertTime(from, timeZone, TimeZoneInfo.Local);
        var localTo = TimeZoneInfo.ConvertTime(to, timeZone, TimeZoneInfo.Local);
        var sales = data.Sales.Where(item => item.CreatedAt >= localFrom && item.CreatedAt < localTo).ToList();
        var returns = data.Returns.Where(item => item.CreatedAt >= localFrom && item.CreatedAt < localTo).ToList();
        var expenses = data.Expenses.Where(item => item.CreatedAt >= localFrom && item.CreatedAt < localTo).ToList();
        var shifts = data.TillSessions.Where(item => item.OpenedAt >= localFrom && item.OpenedAt < localTo).ToList();
        var grossSales = sales.Sum(item => item.Total);
        var refunds = returns.Sum(item => item.Amount);
        var operatingExpenses = expenses.Sum(item => item.Amount);
        var shiftVariance = shifts.Where(item => item.ClosedAt.HasValue).Sum(item => item.Variance);
        var currency = data.Settings.CurrencySymbol;

        return $"""
            {data.Settings.BusinessName} — {cadence} owner report
            Period: {from:dd MMM yyyy} to {to.AddTicks(-1):dd MMM yyyy}

            Sales
            Transactions: {sales.Count}
            Gross sales: {currency}{grossSales:N2}
            Refunds: {currency}{refunds:N2}
            Net sales: {currency}{grossSales - refunds:N2}

            Operations
            Expenses: {expenses.Count} ({currency}{operatingExpenses:N2})
            Shifts opened: {shifts.Count}
            Closed-shift cash variance: {currency}{shiftVariance:N2}

            Generated by TILVEX from locally recorded data.
            """;
    }
}
