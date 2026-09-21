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
                Data = JsonSerializer.Deserialize<PosData>(json, _jsonOptions) ?? new PosData();
            }
            catch (JsonException)
            {
                Data = new PosData();
            }
        }

        Data.Products ??= [];
        Data.Sales ??= [];
        if (Data.Products.Count == 0)
        {
            Data.Products.AddRange([
                new Product { Sku = "GAS-6KG", Barcode = "GAS-6KG", Name = "6kg LPG Cylinder", Category = "Gas", Price = 32.50m, StockQuantity = 20, ReorderLevel = 5, Unit = UnitOfMeasure.Each, UnitLabel = "cylinder" },
                new Product { Sku = "GAS-13KG", Barcode = "GAS-13KG", Name = "13kg LPG Cylinder", Category = "Gas", Price = 58.00m, StockQuantity = 12, ReorderLevel = 4, Unit = UnitOfMeasure.Each, UnitLabel = "cylinder" },
                new Product { Sku = "REG-01", Barcode = "REG-01", Name = "Gas Regulator", Category = "Accessories", Price = 18.95m, StockQuantity = 15, ReorderLevel = 3, Unit = UnitOfMeasure.Each, UnitLabel = "each" }
            ]);
            Save();
        }

        EnsureDefaults();
        Save();
    }

    public void Save()
    {
        // Write-then-replace keeps the local store usable if the process is interrupted.
        var temporaryPath = _dataPath + ".writing";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Data, _jsonOptions));
        File.Move(temporaryPath, _dataPath, true);
        LastSavedAt = DateTime.Now;
    }

    public string ExportJson(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(Data, _jsonOptions));
        return path;
    }

    public void ImportJson(string path)
    {
        var imported = JsonSerializer.Deserialize<PosData>(File.ReadAllText(path), _jsonOptions)
                       ?? throw new InvalidDataException("The selected file is not valid TILVEX POS data.");
        Data = imported;
        EnsureDefaults();
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
            Role = "Admin",
            Active = true
        };

        Data.Users.Add(account);
        Save();
        return account;
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
        Data.Products ??= [];
        Data.Sales ??= [];
        Data.Users ??= [];
        var adminEmail = "admin@tilvex.local";
        if (Data.Users.Count == 0)
        {
            Data.Users.Add(new UserAccount
            {
                UserName = "TILVEX Admin",
                Email = adminEmail,
                PasswordHash = ComputePasswordHash("Tilvex123!"),
                Role = "Admin"
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
                Role = "Admin",
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
        Data.Workflows ??= [];
        Data.Notifications ??= [];
        Data.Settings.Notifications ??= new NotificationSettings();
        Data.Settings.Reports ??= new ReportSettings();
        Data.Settings.Integrations ??= new IntegrationSettings();
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
        Data.SchemaVersion = 3;
    }

    public string Backup()
    {
        var backupPath = Path.Combine(Path.GetDirectoryName(_dataPath)!, $"pos-data-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.Copy(_dataPath, backupPath, true);
        return backupPath;
    }
}
