using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TILVEX;

public sealed class PosStore
{
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    public PosData Data { get; private set; } = new();
    public string DataPath => _dataPath;
    public DateTime LastSavedAt { get; private set; }
    private Dictionary<string, Dictionary<Guid, string>> _auditSnapshot = new();

    public PosStore()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TILVEX");
        Directory.CreateDirectory(folder);
        _dataPath = Path.Combine(folder, "pos-data.json");
        Load();
    }

    public void Load()
    {
        if (File.Exists(_dataPath))
        {
            try
            {
                var json = File.ReadAllText(_dataPath);
                MigrateLegacySyncCredential(json);
                Data = JsonSerializer.Deserialize<PosData>(json, _jsonOptions) ?? new PosData();
            }
            catch (JsonException)
            {
                Data = new PosData();
            }
        }

        Data.Products ??= [];
        Data.Sales ??= [];
        EnsureDefaults();
        _auditSnapshot = CaptureAuditSnapshot();
        Save();
    }

    public void Save()
    {
        RecordAuditChanges();
        if (string.IsNullOrEmpty(Data.Settings.Integrations.SyncApiKey))
            new SecureCredentialStore().Delete("sync-api-key");
        else
            new SecureCredentialStore().Set("sync-api-key", Data.Settings.Integrations.SyncApiKey);

        // Write-then-replace keeps the local store usable if the process is interrupted.
        var temporaryPath = _dataPath + ".writing";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Data, _jsonOptions));
        File.Move(temporaryPath, _dataPath, true);
        LastSavedAt = DateTime.Now;
        _auditSnapshot = CaptureAuditSnapshot();
    }

    public string ExportJson(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(Data, _jsonOptions));
        return path;
    }

    public void ImportJson(string path)
    {
        var json = File.ReadAllText(path);
        MigrateLegacySyncCredential(json);
        var imported = JsonSerializer.Deserialize<PosData>(json, _jsonOptions)
                       ?? throw new InvalidDataException("The selected file is not valid TILVEX POS data.");
        Data = imported;
        EnsureDefaults();
        Data.Settings.Integrations.GoogleSheetsConnected = false;
        Save();
    }

    public UserAccount? Authenticate(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        var normalizedEmail = email.Trim();
        var hash = ComputePasswordHash(password);
        return Data.Users.FirstOrDefault(user =>
            user.Active &&
            user.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase) &&
            user.PasswordHash.Equals(hash, StringComparison.Ordinal));
    }

    public UserAccount? Register(string userName, string email, string password)
    {
        var normalizedName = userName?.Trim() ?? string.Empty;
        var normalizedEmail = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
            return null;

        if (Data.Users.Any(user => user.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            return null;

        var account = new UserAccount
        {
            UserName = normalizedName,
            Email = normalizedEmail,
            PasswordHash = ComputePasswordHash(password),
            Role = "SuperAdmin",
            Active = true
        };

        Data.Users.Add(account);
        if (string.IsNullOrWhiteSpace(Data.Settings.OwnerEmail) &&
            account.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            Data.Settings.OwnerEmail = account.Email;
            Data.Settings.LowStockOwnerEmail = account.Email;
            Data.Settings.Reports.OwnerEmail = account.Email;
        }
        Save();
        return account;
    }

    public void ImportSharedProfile(UserAccount account, BusinessSettings settings)
    {
        Data.Users.RemoveAll(user => user.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase));
        Data.Users.Add(account);
        Data.Settings.BusinessName = settings.BusinessName;
        Data.Settings.BusinessType = settings.BusinessType;
        if (string.IsNullOrWhiteSpace(Data.Settings.OwnerEmail) &&
            !account.Email.EndsWith("@tilvex.local", StringComparison.OrdinalIgnoreCase))
        {
            Data.Settings.OwnerEmail = account.Email;
            Data.Settings.Reports.OwnerEmail = account.Email;
            Data.Settings.LowStockOwnerEmail = account.Email;
        }
        Save();
    }

    public static string ComputePasswordHash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password.Trim()));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    private void EnsureDefaults()
    {
        var existingProfileWasConfigured =
            !string.IsNullOrWhiteSpace(Data.Settings?.BusinessName) &&
            (!Data.Settings.BusinessName.Equals("TILVEX", StringComparison.OrdinalIgnoreCase) ||
             !string.IsNullOrWhiteSpace(Data.Settings.Address) ||
             !string.IsNullOrWhiteSpace(Data.Settings.Phone));
        var needsSetupMigration = Data.SchemaVersion < 4;
        Data.Products ??= [];
        Data.Sales ??= [];
        foreach (var sale in Data.Sales)
        {
            sale.Lines ??= [];
            sale.Payments ??= [];
        }
        Data.Users ??= [];
        var adminEmail = "admin@tilvex.local";
        if (Data.Users.Count == 0)
        {
            Data.Users.Add(new UserAccount
            {
                UserName = "TILVEX Admin",
                Email = adminEmail,
                PasswordHash = ComputePasswordHash("Tilvex123!"),
                Role = "SuperAdmin"
            });
        }

        foreach (var user in Data.Users)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                user.Email = $"{(string.IsNullOrWhiteSpace(user.UserName) ? "user" : user.UserName).Replace(" ", ".").ToLowerInvariant()}@tilvex.local";
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = ComputePasswordHash("Tilvex123!");
            }

            if (string.IsNullOrWhiteSpace(user.Role))
            {
                user.Role = "Cashier";
            }
        }

        if (!Data.Users.Any(user => user.Email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase)))
        {
            Data.Users.Add(new UserAccount
            {
                UserName = "TILVEX Admin",
                Email = adminEmail,
                PasswordHash = ComputePasswordHash("Tilvex123!"),
                Role = "SuperAdmin",
                Active = true
            });
        }

        // Existing v2 installations had a single Manager account; retain owner access while
        // introducing the explicit admin-only settings boundary.
        if (Data.Users.Count == 1 &&
            Data.Users[0].UserName.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase))
            Data.Users[0].Role = "Admin";
        Data.Settings ??= new BusinessSettings();
        Data.Tax ??= new TaxConfig();
        Data.Customers ??= [];
        Data.Suppliers ??= [];
        Data.PurchaseOrders ??= [];
        Data.Returns ??= [];
        Data.Promotions ??= [];
        Data.TillSessions ??= [];
        Data.Expenses ??= [];
        Data.LedgerTransactions ??= [];
        Data.StockAdjustments ??= [];
        Data.Workflows ??= [];
        Data.Notifications ??= [];
        Data.EmailOutbox ??= [];
        foreach (var sale in Data.Sales.Where(sale => string.IsNullOrWhiteSpace(sale.CustomerName) && sale.CustomerId.HasValue))
            sale.CustomerName = Data.Customers.FirstOrDefault(customer => customer.Id == sale.CustomerId)?.Name ?? "";
        Data.Settings.Notifications ??= new NotificationSettings();
        Data.Settings.Reports ??= new ReportSettings();
        Data.Settings.Integrations ??= new IntegrationSettings();
        Data.Settings.Integrations.SyncApiKey = new SecureCredentialStore().Get("sync-api-key") ??
                                                Data.Settings.Integrations.SyncApiKey;
        Data.AuditEvents ??= [];
        if (string.IsNullOrWhiteSpace(Data.Settings.OwnerEmail))
        {
            Data.Settings.OwnerEmail = FirstNonEmpty(
                Data.Settings.Email,
                Data.Settings.LowStockOwnerEmail,
                Data.Settings.Reports.OwnerEmail,
                Data.Users.FirstOrDefault(user =>
                    user.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) &&
                    !user.Email.EndsWith("@tilvex.local", StringComparison.OrdinalIgnoreCase))?.Email);
        }

        if (string.IsNullOrWhiteSpace(Data.Settings.Reports.OwnerEmail))
            Data.Settings.Reports.OwnerEmail = Data.Settings.OwnerEmail;
        if (string.IsNullOrWhiteSpace(Data.Settings.LowStockOwnerEmail))
            Data.Settings.LowStockOwnerEmail = Data.Settings.OwnerEmail;
        if (string.IsNullOrWhiteSpace(Data.Settings.Reports.TimeZone))
            Data.Settings.Reports.TimeZone = Data.Settings.TimeZone;
        if (needsSetupMigration && existingProfileWasConfigured)
            Data.Settings.SetupCompleted = true;
        if (string.IsNullOrWhiteSpace(Data.Settings.Integrations.WhatsAppWebhookUrl))
            Data.Settings.Integrations.WhatsAppWebhookUrl = Data.Settings.Notifications.WhatsAppWebhookUrl;
        if (string.IsNullOrWhiteSpace(Data.Settings.Integrations.SmsWebhookUrl))
            Data.Settings.Integrations.SmsWebhookUrl = Data.Settings.Notifications.SmsWebhookUrl;
        if (string.IsNullOrWhiteSpace(Data.Settings.Integrations.EmailWebhookUrl))
            Data.Settings.Integrations.EmailWebhookUrl = Data.Settings.Notifications.EmailWebhookUrl;
        foreach (var product in Data.Products)
        {
            if (string.IsNullOrWhiteSpace(product.Barcode)) product.Barcode = product.Sku;
            if (string.IsNullOrWhiteSpace(product.UnitLabel)) product.UnitLabel = product.Unit.ToString();
        }
        Data.SchemaVersion = 6;
    }

    private static void MigrateLegacySyncCredential(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("Settings", out var settings) &&
            settings.TryGetProperty("Integrations", out var integrations) &&
            integrations.TryGetProperty("SyncApiKey", out var key) &&
            key.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(key.GetString()))
        {
            new SecureCredentialStore().Set("sync-api-key", key.GetString()!);
        }

    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private Dictionary<string, Dictionary<Guid, string>> CaptureAuditSnapshot()
    {
        static Dictionary<Guid, string> Snapshot<T>(IEnumerable<T> rows, Func<T, Guid> id) =>
            rows.ToDictionary(id, row => JsonSerializer.Serialize(row));

        return new Dictionary<string, Dictionary<Guid, string>>
        {
            ["Sale"] = Snapshot(Data.Sales, item => item.Id),
            ["Return"] = Snapshot(Data.Returns, item => item.Id),
            ["Purchase"] = Snapshot(Data.PurchaseOrders, item => item.Id),
            ["Expense"] = Snapshot(Data.Expenses, item => item.Id),
            ["Ledger payment"] = Snapshot(Data.LedgerTransactions, item => item.Id),
            ["Inventory adjustment"] = Snapshot(Data.StockAdjustments, item => item.Id),
            ["Till session"] = Snapshot(Data.TillSessions, item => item.Id),
            ["Product"] = Snapshot(Data.Products, item => item.Id),
            ["Customer"] = Snapshot(Data.Customers, item => item.Id),
            ["Supplier"] = Snapshot(Data.Suppliers, item => item.Id),
            ["User"] = Snapshot(Data.Users, item => item.Id),
            ["Promotion"] = Snapshot(Data.Promotions, item => item.Id),
            ["Workflow"] = Snapshot(Data.Workflows, item => item.Id),
            ["Tax settings"] = new Dictionary<Guid, string>
            {
                [Guid.Empty] = JsonSerializer.Serialize(Data.Tax)
            },
            ["Business settings"] = new Dictionary<Guid, string>
            {
                [Guid.Empty] = SerializeAuditSettings()
            }
        };
    }

    private string SerializeAuditSettings()
    {
        var business = Data.Settings;
        var integration = business.Integrations;
        var reports = business.Reports;
        var notifications = business.Notifications;
        return JsonSerializer.Serialize(new
        {
            business.BusinessName,
            business.OwnerEmail,
            business.Email,
            business.Address,
            business.City,
            business.Country,
            business.Phone,
            business.ReceiptFooter,
            business.CurrencySymbol,
            business.BusinessType,
            business.CompanyLogoPath,
            business.TimeZone,
            business.LowStockAlertThreshold,
            business.LowStockOwnerEmail,
            business.EnableSaleNotifications,
            business.EnableOwnerEmailAlerts,
            business.SaleEmailAlerts,
            Notifications = new
            {
                notifications.SaleTimestampNotifications,
                notifications.LowStockDashboardAlerts,
                notifications.LowStockOwnerEmailAlerts
            },
            Reports = new
            {
                reports.Daily,
                reports.Weekly,
                reports.Monthly,
                reports.OwnerEmail,
                reports.SendTime,
                reports.TimeZone
            },
            Integrations = new
            {
                integration.OnlineSyncEnabled,
                integration.GoogleSheetsConnected,
                integration.ExportReportsToGoogleDrive,
                integration.ExportReportsToGoogleSheets
            }
        });
    }

    private void RecordAuditChanges()
    {
        var current = CaptureAuditSnapshot();
        foreach (var (type, rows) in current)
        {
            _auditSnapshot.TryGetValue(type, out var previous);
            previous ??= [];
            foreach (var (id, value) in rows)
            {
                var action = !previous.TryGetValue(id, out var oldValue) ? "created" :
                    oldValue != value ? "updated" : "";
                if (action.Length == 0) continue;

                var details = GetAuditDetails(type, id);
                Data.AuditEvents.Add(new AuditEvent
                {
                    Actor = AppSession.CurrentUser?.UserName ?? "System",
                    Type = type,
                    Summary = $"{type} {action}",
                    Details = JsonSerializer.Serialize(details)
                });
            }

            foreach (var id in previous.Keys.Except(rows.Keys))
            {
                Data.AuditEvents.Add(new AuditEvent
                {
                    Actor = AppSession.CurrentUser?.UserName ?? "System",
                    Type = type,
                    Summary = $"{type} removed",
                    Details = JsonSerializer.Serialize(new { id })
                });
            }
        }
    }

    private object GetAuditDetails(string type, Guid id) => type switch
    {
        "Sale" when Data.Sales.FirstOrDefault(item => item.Id == id) is { } sale =>
            new
            {
                sale.ReceiptNumber,
                sale.CreatedAt,
                sale.CustomerName,
                sale.Subtotal,
                sale.Discount,
                sale.Tax,
                sale.TaxRate,
                sale.TaxInclusive,
                sale.Total,
                sale.Tendered,
                sale.Change,
                sale.PaymentMethod,
                Lines = sale.Lines.Select(line => new
                {
                    line.Sku,
                    line.Name,
                    line.UnitPrice,
                    line.Quantity,
                    line.Unit,
                    line.UnitLabel,
                    line.LineTotal
                }),
                Payments = sale.Payments.Select(payment => new { payment.Method, payment.Amount })
            },
        "Return" when Data.Returns.FirstOrDefault(item => item.Id == id) is { } record =>
            new { record.ReceiptNumber, record.CreatedAt, record.Amount, record.Reason },
        "Purchase" when Data.PurchaseOrders.FirstOrDefault(item => item.Id == id) is { } purchase =>
            new { purchase.Number, purchase.CreatedAt, purchase.Status, purchase.Total },
        "Expense" when Data.Expenses.FirstOrDefault(item => item.Id == id) is { } expense =>
            new { expense.CreatedAt, expense.Category, expense.Amount, expense.PaymentMethod },
        "Ledger payment" when Data.LedgerTransactions.FirstOrDefault(item => item.Id == id) is { } ledger =>
            new { ledger.CreatedAt, ledger.PartyType, ledger.Amount, ledger.PaymentMethod },
        "Inventory adjustment" when Data.StockAdjustments.FirstOrDefault(item => item.Id == id) is { } adjustment =>
            new { adjustment.CreatedAt, adjustment.ProductName, adjustment.QuantityChange, adjustment.QuantityAfter, adjustment.Reason },
        "Till session" when Data.TillSessions.FirstOrDefault(item => item.Id == id) is { } till =>
            new { till.OpenedAt, till.ClosedAt, till.OpeningFloat, till.ExpectedCash, till.CountedCash },
        "Product" when Data.Products.FirstOrDefault(item => item.Id == id) is { } product =>
            new
            {
                product.Sku,
                product.Name,
                product.Category,
                product.Price,
                product.StockQuantity,
                product.ReorderLevel,
                product.Unit,
                product.UnitLabel,
                product.Barcode
            },
        "Customer" when Data.Customers.FirstOrDefault(item => item.Id == id) is { } customer =>
            new { customer.Name, customer.Phone, customer.Email },
        "Supplier" when Data.Suppliers.FirstOrDefault(item => item.Id == id) is { } supplier =>
            new { supplier.Name, supplier.Contact, supplier.Phone, supplier.Email },
        "User" when Data.Users.FirstOrDefault(item => item.Id == id) is { } user =>
            new { user.UserName, user.Role, user.Active },
        "Promotion" when Data.Promotions.FirstOrDefault(item => item.Id == id) is { } promotion =>
            new { promotion.Name, promotion.Code, promotion.DiscountPercent, promotion.Active },
        "Workflow" when Data.Workflows.FirstOrDefault(item => item.Id == id) is { } workflow =>
            new { workflow.Name, workflow.Enabled },
        "Tax settings" => new { Data.Tax.Name, Data.Tax.Rate, Data.Tax.Inclusive },
        "Business settings" => new { Data.Settings.BusinessName, Data.Settings.BusinessType },
        _ => new { id }
    };

    public string Backup()
    {
        var backupPath = Path.Combine(Path.GetDirectoryName(_dataPath)!, $"pos-data-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.Copy(_dataPath, backupPath, true);
        return backupPath;
    }
}
