using System.Text.Json.Serialization;

namespace TILVEX;

public enum BusinessType
{
    Retail,
    BeautySalon,
    Boutique,
    Electrical,
    ComputerRepair,
    Electronics,
    Pharmacy,
    GasPetrolStation,
    Grocery,
    Hospitality,
    Service,
    Fuel,
    Other
}

public enum UnitOfMeasure
{
    Each,
    Kilogram,
    Gram,
    Litre,
    Millilitre,
    Metre,
    Hour,
    Service,
    Pack
}

public enum IntegrationConnectionStatus
{
    NotConfigured,
    Offline,
    Ready,
    Syncing,
    Error
}

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal ReorderLevel { get; set; } = 5;
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;
    public string UnitLabel { get; set; } = "";
    public string Barcode { get; set; } = "";
}

public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Guid? CustomerId { get; set; }
    public List<SaleLine> Lines { get; set; } = [];
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public bool TaxInclusive { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public decimal Tendered { get; set; }
    [JsonIgnore]     public decimal ItemCount => Lines.Sum(line => line.Quantity);
    [JsonIgnore] public decimal Subtotal => Lines.Sum(line => line.LineTotal);
    [JsonIgnore] public decimal Tax => TaxInclusive
        ? Math.Max(0, (Subtotal - Discount) * TaxRate / (100m + TaxRate))
        : Math.Max(0, (Subtotal - Discount) * TaxRate / 100m);
    [JsonIgnore] public decimal Total => TaxInclusive
        ? Math.Max(0, Subtotal - Discount)
        : Math.Max(0, Subtotal - Discount + Tax);
    [JsonIgnore] public decimal Change => Math.Max(0, Tendered - Total);
}

public sealed class SaleLine
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;
    public string UnitLabel { get; set; } = "";
    [JsonIgnore] public decimal LineTotal => UnitPrice * Quantity;
}

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
}

public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public Guid? SupplierId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Draft";
    public List<PurchaseOrderLine> Lines { get; set; } = [];
    [JsonIgnore] public decimal Total => Lines.Sum(l => l.UnitCost * l.Quantity);
}

public sealed class PurchaseOrderLine
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;
}

public sealed class ReturnRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Reason { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class Promotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal DiscountPercent { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class TaxConfig
{
    public string Name { get; set; } = "Standard VAT";
    public decimal Rate { get; set; } = 20m;
    public bool Inclusive { get; set; } = false;
}

public sealed class TillSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public string Notes { get; set; } = "";
    [JsonIgnore] public decimal Variance => CountedCash - ExpectedCash;
}

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Cashier";
    public bool Active { get; set; } = true;
}

public sealed class BusinessSettings
{
    public string BusinessName { get; set; } = "TILVEX";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string ReceiptFooter { get; set; } = "Thank you for your custom.";
    public string CurrencySymbol { get; set; } = "£";
    public BusinessType BusinessType { get; set; } = BusinessType.Retail;
    public string CompanyLogoPath { get; set; } = "";
    public string TimeZone { get; set; } = TimeZoneInfo.Local.Id;
    public decimal LowStockAlertThreshold { get; set; } = 5;
    public string LowStockOwnerEmail { get; set; } = "";
    public bool EnableSaleNotifications { get; set; } = true;
    public bool EnableOwnerEmailAlerts { get; set; } = false;
    public NotificationSettings Notifications { get; set; } = new();
    public ReportSettings Reports { get; set; } = new();
    public IntegrationSettings Integrations { get; set; } = new();
}

public sealed class NotificationSettings
{
    public bool SaleTimestampNotifications { get; set; } = true;
    public bool LowStockDashboardAlerts { get; set; } = true;
    public bool LowStockOwnerEmailAlerts { get; set; }
    public string WhatsAppWebhookUrl { get; set; } = "";
    public string SmsWebhookUrl { get; set; } = "";
    public string EmailWebhookUrl { get; set; } = "";
}

public sealed class ReportSettings
{
    public bool Daily { get; set; } = true;
    public bool Weekly { get; set; } = true;
    public bool Monthly { get; set; } = true;
    public string OwnerEmail { get; set; } = "";
}

public sealed class IntegrationSettings
{
    public bool OnlineSyncEnabled { get; set; }
    public string SyncEndpoint { get; set; } = "";
    public string ProfileSyncEndpoint { get; set; } = "http://localhost:3000";
    public string SyncApiKey { get; set; } = "";
    public string GoogleDriveFolderId { get; set; } = "";
    public string GoogleSheetsSpreadsheetId { get; set; } = "";
    public string GoogleSheetsWorksheet { get; set; } = "Sales";
    public string WhatsAppWebhookUrl { get; set; } = "";
    public string SmsWebhookUrl { get; set; } = "";
    public string EmailWebhookUrl { get; set; } = "";
    public bool ExportReportsToGoogleDrive { get; set; }
    public bool ExportReportsToGoogleSheets { get; set; }
    public IntegrationConnectionStatus SyncStatus { get; set; } = IntegrationConnectionStatus.Offline;
    public DateTime? LastSyncAt { get; set; }
    public string LastSyncMessage { get; set; } = "Local-only mode; no online integration configured.";
}

public sealed class NotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Delivered { get; set; }
}

public sealed class WorkflowScreen
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class PosData
{
    public int SchemaVersion { get; set; } = 3;
    public List<Product> Products { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public List<Supplier> Suppliers { get; set; } = [];
    public List<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public List<ReturnRecord> Returns { get; set; } = [];
    public List<Promotion> Promotions { get; set; } = [];
    public TaxConfig Tax { get; set; } = new();
    public List<TillSession> TillSessions { get; set; } = [];
    public List<UserAccount> Users { get; set; } = [];
    public BusinessSettings Settings { get; set; } = new();
    public List<WorkflowScreen> Workflows { get; set; } = [];
    public List<NotificationRecord> Notifications { get; set; } = [];
}
