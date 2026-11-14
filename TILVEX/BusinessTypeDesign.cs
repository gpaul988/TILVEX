namespace TILVEX;

public sealed record BusinessTypeDesign(
    BusinessType Type,
    string Name,
    string Glyph,
    string Description,
    string HeroEyebrow,
    string HeroCopy,
    string FocusTitle,
    string FocusCopy,
    string PrimaryAction,
    int PrimaryTab,
    string SecondaryAction,
    int SecondaryTab,
    string SalesMetric,
    string TransactionMetric,
    string CatalogMetric,
    string StockMetric,
    string RevenueChart,
    string CatalogChart,
    string Accent,
    string Deep,
    string Surface)
{
    private static readonly IReadOnlyDictionary<BusinessType, BusinessTypeDesign> Designs =
        new[]
        {
            new BusinessTypeDesign(BusinessType.Retail, "General retail", "▦", "Everyday products and sales",
                "RETAIL OPERATIONS", "A clear view of today's counter, stock and sales activity.",
                "Retail floor", "Checkout, price checks and replenishment are ready in the shared workspace.",
                "Open checkout", 1, "Check prices", 12, "TODAY'S TAKINGS", "CHECKOUTS", "SELLABLE LINES", "REORDER FLAGS",
                "Sales pulse · 7 days", "Fast movers · units", "#0F9F78", "#172D3A", "#EAF7F3"),
            new BusinessTypeDesign(BusinessType.Grocery, "Grocery", "▤", "Food, drinks and essentials",
                "FRESH GOODS", "A market-style view for daily baskets and shelf availability.",
                "Market floor", "Use checkout and stock controls for grocery sales; no freshness tracking is assumed.",
                "Ring up a basket", 1, "Review stock", 13, "TODAY'S BASKETS", "BASKETS RUNG", "GROCERY ITEMS", "SHELF ALERTS",
                "Basket sales · 7 days", "Popular goods · units", "#739B32", "#29391C", "#F2F7E8"),
            new BusinessTypeDesign(BusinessType.Boutique, "Fashion & apparel", "◇", "Clothing, footwear and accessories",
                "STYLE COLLECTIONS", "A refined sales view for curated apparel and accessory ranges.",
                "Collection desk", "Manage product lines, review sell-through and serve customers from the existing POS tools.",
                "Browse collections", 2, "View customers", 4, "TODAY'S SALES", "STYLE TRANSACTIONS", "STYLE ITEMS", "LOW-STOCK ITEMS",
                "Collection sales · 7 days", "Top styles · units", "#BF4F79", "#382033", "#FBEFF4"),
            new BusinessTypeDesign(BusinessType.Electrical, "Electrical", "ϟ", "Electrical goods and supplies",
                "ELECTRICAL SUPPLY", "A high-contrast counter view for components, equipment and stock movement.",
                "Supply counter", "Use product records, barcode lookup and inventory adjustments for electrical supplies.",
                "Find a component", 12, "Adjust inventory", 13, "TODAY'S SALES", "COUNTER ORDERS", "SUPPLY ITEMS", "STOCK FLAGS",
                "Supply sales · 7 days", "Top components · units", "#C47A24", "#3B2A18", "#FBF3E8"),
            new BusinessTypeDesign(BusinessType.ComputerRepair, "Computer services", "⌘", "Repairs and technical services",
                "TECH SERVICE DESK", "A focused service-counter view with customer and transaction context.",
                "Service desk", "Customer records and sales are available; repair tickets and job scheduling are not enabled.",
                "Open service sale", 1, "Find a customer", 4, "TODAY'S SERVICE SALES", "SERVICE TRANSACTIONS", "CATALOG ITEMS", "STOCK FLAGS",
                "Service sales · 7 days", "Top catalog lines · units", "#6379AD", "#202941", "#EEF1FA"),
            new BusinessTypeDesign(BusinessType.Electronics, "Electronics", "⌁", "Devices and accessories",
                "DEVICE GALLERY", "A crisp device-and-accessory view for fast product discovery.",
                "Device counter", "Use barcode lookup and product management for your device catalog and accessories.",
                "Check a device", 12, "Manage products", 2, "TODAY'S SALES", "DEVICE SALES", "DEVICE LINES", "STOCK FLAGS",
                "Device sales · 7 days", "Top devices · units", "#198BA6", "#15313B", "#EAF6F8"),
            new BusinessTypeDesign(BusinessType.Pharmacy, "Pharmacy", "+", "Health and personal care",
                "HEALTH & WELLNESS", "A calm, legible counter view for health and personal-care products.",
                "Dispensary counter", "Product and sales records are available; prescriptions and clinical records are not enabled.",
                "Find a product", 12, "Review sales", 3, "TODAY'S SALES", "COUNTER TRANSACTIONS", "HEALTH ITEMS", "STOCK FLAGS",
                "Counter sales · 7 days", "Top products · units", "#3976C8", "#1B2C49", "#EDF4FD"),
            new BusinessTypeDesign(BusinessType.GasPetrolStation, "Fuel & gas", "◎", "Fuel, LPG and forecourt sales",
                "FORECOURT OPERATIONS", "A bold forecourt overview for counter sales and on-hand products.",
                "Forecourt counter", "Record sales and track catalog stock; pump, tank and meter integrations are not enabled.",
                "Open forecourt sale", 1, "Review stock", 13, "TODAY'S FORECOURT SALES", "FORECOURT TICKETS", "FUEL & GAS LINES", "STOCK FLAGS",
                "Forecourt sales · 7 days", "Top lines · units", "#D14A3F", "#3B2221", "#FCF0ED"),
            new BusinessTypeDesign(BusinessType.Hospitality, "Cafe & hospitality", "◒", "Food service and hospitality",
                "HOSPITALITY SERVICE", "A warm service view for counter orders and daily trading.",
                "Service counter", "Use the shared checkout and customer tools; table service and reservations are not enabled.",
                "Start an order", 1, "View transactions", 3, "TODAY'S SERVICE SALES", "ORDERS PROCESSED", "MENU LINES", "STOCK FLAGS",
                "Service sales · 7 days", "Top menu lines · units", "#CE7048", "#42281D", "#FBF1EA"),
            new BusinessTypeDesign(BusinessType.BeautySalon, "Beauty & salon", "✦", "Salon services and retail",
                "BEAUTY STUDIO", "A polished studio view for customer-facing services and retail products.",
                "Studio counter", "Customer profiles and checkout are ready; appointment booking is not enabled.",
                "Open checkout", 1, "View customers", 4, "TODAY'S STUDIO SALES", "STUDIO TRANSACTIONS", "RETAIL LINES", "STOCK FLAGS",
                "Studio sales · 7 days", "Top retail lines · units", "#BE529C", "#3E2340", "#FAEFF7"),
            new BusinessTypeDesign(BusinessType.Service, "Services", "◌", "Service-based business",
                "SERVICE OPERATIONS", "A people-first service overview for recorded sales and customer accounts.",
                "Client operations", "Sales, customer records and ledgers are available; appointments and job tracking are not enabled.",
                "Record a service sale", 1, "Open customer ledger", 15, "TODAY'S SERVICE SALES", "SERVICE TRANSACTIONS", "CATALOG ITEMS", "STOCK FLAGS",
                "Service revenue · 7 days", "Top catalog lines · units", "#198D96", "#173439", "#EAF6F6"),
            new BusinessTypeDesign(BusinessType.Fuel, "Fuel services", "⛽", "Fuel products and services",
                "FUEL DESK", "A distinct fuel-service counter for sales, reconciliation and catalog stock.",
                "Fuel desk", "Use current POS sales and stock tools; fuel dispensing equipment is not connected.",
                "Open fuel sale", 1, "Check fuel prices", 12, "TODAY'S FUEL SALES", "FUEL TRANSACTIONS", "FUEL CATALOG", "STOCK FLAGS",
                "Fuel sales · 7 days", "Top fuel lines · units", "#7959BA", "#2B2443", "#F2EFFA"),
            new BusinessTypeDesign(BusinessType.Other, "Other business", "✚", "A flexible point of sale",
                "FLEXIBLE WORKSPACE", "A neutral, adaptable command view for a business with its own operating rhythm.",
                "Business overview", "Keep using the shared checkout, catalog, customer and reporting tools as your needs evolve.",
                "Open checkout", 1, "Review reports", 9, "TODAY'S SALES", "TRANSACTIONS", "CATALOG ITEMS", "STOCK FLAGS",
                "Sales overview · 7 days", "Top catalog lines · units", "#AA842D", "#332D1D", "#F8F4E9")
        }.ToDictionary(design => design.Type);

    public static IReadOnlyCollection<BusinessTypeDesign> All { get; } = Array.AsReadOnly(Designs.Values.ToArray());

    static BusinessTypeDesign()
    {
        var allTypes = Enum.GetValues<BusinessType>();
        if (Designs.Count != allTypes.Length || allTypes.Any(type => !Designs.ContainsKey(type)))
            throw new InvalidOperationException("Every business type must have exactly one workspace design.");

        if (Designs.Values.Select(design => design.Accent).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Designs.Count ||
            Designs.Values.Select(design => design.Glyph).Distinct(StringComparer.Ordinal).Count() != Designs.Count)
            throw new InvalidOperationException("Every business type workspace design must have a unique accent and visual mark.");
    }

    public static BusinessTypeDesign For(BusinessType type) =>
        Designs.TryGetValue(type, out var design) ? design : Designs[BusinessType.Other];
}
