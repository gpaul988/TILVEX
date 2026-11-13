using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace TILVEX;

public partial class MainWindow : Window
{
    private sealed record BusinessTypeOption(BusinessType Type, string DisplayName);
    private sealed record LedgerEntryView(DateTime Date, string Entry, string Reference, decimal Debit, decimal Credit, decimal Balance);

    private readonly PosStore _store = new();
    private IPosIntegrationService _integration = null!;
    private readonly ObservableCollection<SaleLine> _cart = [];
    private Product? _selectedProduct;
    private Customer? _selectedCustomer;
    private Supplier? _selectedSupplier;
    private Promotion? _selectedPromotion;
    private PurchaseOrder? _draftPurchase;

    public MainWindow()
    {
        InitializeComponent();
        OperatorText.Text = $"Good morning, {AppSession.CurrentUser?.UserName ?? "operator"}";
        MainTabs.SelectedIndex = 1;
        MainPageTitle.Text = "Point of sale";
        CartGrid.ItemsSource = _cart;
        UnitCombo.ItemsSource = Enum.GetValues<UnitOfMeasure>();
        BusinessTypeCombo.ItemsSource = Enum.GetValues<BusinessType>()
            .Select(type => new BusinessTypeOption(type, BusinessTypeDisplayName(type)))
            .ToList();
        BusinessTypeCombo.DisplayMemberPath = nameof(BusinessTypeOption.DisplayName);
        BusinessTypeCombo.SelectedValuePath = nameof(BusinessTypeOption.Type);
        LoadSettings();
        ApplyCurrencyCulture();
        HeaderSubtitle.Text = "Scan items, build a receipt and take payment";
        _integration = new HttpIntegrationService(_store.Data.Settings.Integrations);
        Refresh();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => RefreshDashboardCharts();

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var tabIndex))
            return;

        NavigateToTab(tabIndex);
    }

    private void NavigateMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || !int.TryParse(menuItem.Tag?.ToString(), out var tabIndex))
            return;

        NavigateToTab(tabIndex);
    }

    private void NavigateToTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= MainTabs.Items.Count)
            return;

        MainTabs.SelectedIndex = tabIndex;
        MainPageTitle.Text = tabIndex switch
        {
            0 => "Business command centre",
            1 => "Point of sale",
            2 => "Product management",
            3 => "Sales management",
            4 => "Customers",
            5 => "Purchase management",
            6 => "Returns & refunds",
            7 => "Promotions & VAT",
            8 => "Till reconciliation",
            9 => "Reports & export",
            10 => "Settings & users",
            11 => "Database tools",
            12 => "Price checker",
            13 => "Inventory management",
            14 => "Expense management",
            15 => "Customer & supplier ledgers",
            16 => "Shift management",
            _ => "Business command centre"
        };
        HeaderSubtitle.Text = tabIndex switch
        {
            0 => "Sales, stock and daily operations",
            1 => "Create a sale and take payment",
            2 => "Manage products, prices and stock levels",
            3 => "Review completed transactions and receipts",
            4 => "Manage customer records and contact details",
            5 => "Manage suppliers and purchase orders",
            6 => "Process returns and customer refunds",
            7 => "Manage discounts and tax configuration",
            8 => "Open, close and reconcile the cash drawer",
            9 => "Review performance and export business data",
            10 => "Manage business settings, users and integrations",
            11 => "Import, export and migrate local POS data",
            12 => "Find a product and confirm its current selling price and availability",
            13 => "Monitor stock levels and record inventory adjustments",
            14 => "Record and review day-to-day operating expenses",
            15 => "Review customer receivables and supplier payables",
            16 => "Open, monitor and close cashier shifts",
            _ => "Sales, stock and daily operations"
        };
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        AppSession.CurrentUser = null;
        var authWindow = new AuthWindow();
        authWindow.Show();
        Close();
    }

    private void Refresh()
    {
        var products = _store.Data.Products.OrderBy(p => p.Name).ToList();
        ProductCategoryCombo.ItemsSource = new[] { "All categories" }
            .Concat(products.Select(product => product.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
            .ToList();
        ProductCategoryCombo.SelectedIndex = 0;
        RefreshSaleProducts();
        InventoryGrid.ItemsSource = products;
        RefreshSalesHistory();
        RecentSalesGrid.ItemsSource = _store.Data.Sales.OrderByDescending(s => s.CreatedAt).Take(10).ToList();
        CustomersGrid.ItemsSource = _store.Data.Customers.OrderBy(c => c.Name).ToList();
        CustomerCombo.ItemsSource = _store.Data.Customers.OrderBy(c => c.Name).ToList();
        SuppliersGrid.ItemsSource = _store.Data.Suppliers.OrderBy(s => s.Name).ToList();
        PurchaseSupplierCombo.ItemsSource = _store.Data.Suppliers.OrderBy(s => s.Name).ToList();
        PurchaseProductCombo.ItemsSource = products;
        PurchaseOrdersGrid.ItemsSource = _store.Data.PurchaseOrders.OrderByDescending(p => p.CreatedAt).ToList();
        ReturnsGrid.ItemsSource = _store.Data.Returns.OrderByDescending(r => r.CreatedAt).ToList();
        PromotionsGrid.ItemsSource = _store.Data.Promotions.OrderBy(p => p.Name).ToList();
        PromotionCombo.ItemsSource = _store.Data.Promotions.Where(p => p.Active && (!p.ExpiresAt.HasValue || p.ExpiresAt >= DateTime.Today)).ToList();
        TillGrid.ItemsSource = _store.Data.TillSessions.OrderByDescending(t => t.OpenedAt).ToList();
        ShiftTillGrid.ItemsSource = _store.Data.TillSessions.OrderByDescending(t => t.OpenedAt).ToList();
        UsersGrid.ItemsSource = _store.Data.Users.OrderBy(u => u.UserName).ToList();
        WorkflowsGrid.ItemsSource = _store.Data.Workflows.OrderBy(w => w.Name).ToList();
        ExpensesGrid.ItemsSource = _store.Data.Expenses.OrderByDescending(expense => expense.CreatedAt).ToList();
        InventoryAdjustmentsGrid.ItemsSource = _store.Data.StockAdjustments.OrderByDescending(adjustment => adjustment.CreatedAt).Take(100).ToList();
        RefreshPriceLookup();
        RefreshInventoryView();
        RefreshLedgerParties();
        RefreshShiftStatus();
        ProductCountText.Text = products.Count.ToString();
        var lowStock = products.Where(IsLowStock).ToList();
        LowStockText.Text = lowStock.Count.ToString();
        LowStockDetailText.Text = lowStock.Count == 0 ? "Stock levels healthy" : $"{lowStock.Count} items need attention";
        BusinessTypeText.Text = _store.Data.Settings.BusinessType.ToString();
        SyncStatusText.Text = $"{_store.Data.Settings.Integrations.SyncStatus}: {_store.Data.Settings.Integrations.LastSyncMessage}";
        NotificationStatusText.Text = _store.Data.Notifications.OrderByDescending(n => n.CreatedAt).FirstOrDefault()?.Message ?? "No local notifications";
        var today = _store.Data.Sales.Where(s => s.CreatedAt.Date == DateTime.Today).ToList();
        TodaySalesText.Text = today.Sum(s => s.Total).ToString("C");
        TodayTransactionsText.Text = today.Count.ToString();
        var openTill = _store.Data.TillSessions.LastOrDefault(t => !t.ClosedAt.HasValue);
        TillStatusText.Text = openTill is null ? "Closed" : "Open";
        UpdateCartTotal();
        RefreshReport();
        RefreshDashboardCharts();
    }

    private static readonly Color ChartAccent = Color.FromRgb(0x0F, 0x9F, 0x78);
    private static readonly Color ChartAccentDark = Color.FromRgb(0x0B, 0x80, 0x5E);
    private static readonly Color ChartInk = Color.FromRgb(0x17, 0x23, 0x34);
    private static readonly Color ChartMuted = Color.FromRgb(0x6B, 0x77, 0x8A);
    private static readonly Color ChartLine = Color.FromRgb(0xE3, 0xE9, 0xF0);

    private void RefreshDashboardCharts()
    {
        if (RevenueTrendCanvas is null) return;
        DrawRevenueTrend();
        DrawTopProducts();
        DrawPaymentDonut();
        DrawStockHealth();
        DrawTransactionHours();
    }

    private static SolidColorBrush ChartBrush(Color color, byte opacity = 255) =>
        new(Color.FromArgb(opacity, color.R, color.G, color.B));

    private static void AddChartText(Canvas canvas, string text, double left, double top, double fontSize = 10, Color? color = null, bool bold = false)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = ChartBrush(color ?? ChartMuted),
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = Math.Max(40, canvas.ActualWidth - left - 8)
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        canvas.Children.Add(label);
    }

    private static double ChartWidth(Canvas canvas, double fallback) => canvas.ActualWidth > 120 ? canvas.ActualWidth : fallback;

    private static void AddGridLines(Canvas canvas, double width, double height, int rows = 3)
    {
        for (var row = 0; row <= rows; row++)
        {
            var y = 12 + (height - 42) * row / rows;
            canvas.Children.Add(new Line
            {
                X1 = 12, X2 = width - 10, Y1 = y, Y2 = y,
                Stroke = ChartBrush(ChartLine, 190), StrokeThickness = 1,
                StrokeDashArray = [2, 4]
            });
        }
    }

    private void DrawRevenueTrend()
    {
        var canvas = RevenueTrendCanvas;
        canvas.Children.Clear();
        var width = ChartWidth(canvas, 420);
        const double height = 178;
        var points = Enumerable.Range(0, 7).Select(index =>
        {
            var date = DateTime.Today.AddDays(-6 + index);
            return (Label: date.ToString("ddd"), Value: _store.Data.Sales.Where(s => s.CreatedAt.Date == date.Date).Sum(s => s.Total));
        }).ToList();
        var max = points.Max(point => point.Value);
        RevenueEmptyText.Visibility = max <= 0 ? Visibility.Visible : Visibility.Collapsed;
        AddGridLines(canvas, width, height);
        if (max <= 0) return;

        var plotHeight = height - 46;
        var plotWidth = width - 26;
        var linePoints = points.Select((point, index) => new Point(
            13 + plotWidth * index / 6,
            14 + plotHeight - (double)(point.Value / max) * plotHeight)).ToList();
        var area = new Polygon { Fill = ChartBrush(ChartAccent, 38), Points = new PointCollection(linePoints.Concat([new Point(linePoints[^1].X, height - 30), new Point(linePoints[0].X, height - 30)])) };
        canvas.Children.Add(area);
        canvas.Children.Add(new Polyline { Points = new PointCollection(linePoints), Stroke = ChartBrush(ChartAccent), StrokeThickness = 2.2, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ChartAccent, BlurRadius = 10, ShadowDepth = 0, Opacity = .55 } });
        foreach (var (point, index) in points.Select((value, index) => (value, index)))
        {
            canvas.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = ChartBrush(ChartInk), Stroke = ChartBrush(ChartAccent), StrokeThickness = 1.5, ToolTip = $"{point.Label}: {point.Value:C}", Margin = new Thickness(linePoints[index].X - 3, linePoints[index].Y - 3, 0, 0) });
            AddChartText(canvas, point.Label, linePoints[index].X - 12, height - 25);
        }
        AddChartText(canvas, $"{points.Sum(point => point.Value):C}", 13, 13, 12, ChartInk, true);
    }

    private void DrawTopProducts()
    {
        var canvas = TopProductsCanvas;
        canvas.Children.Clear();
        var width = ChartWidth(canvas, 300);
        var products = _store.Data.Sales.SelectMany(s => s.Lines)
            .GroupBy(line => line.Name).Select(group => (Name: group.Key, Quantity: group.Sum(line => line.Quantity)))
            .OrderByDescending(item => item.Quantity).Take(5).ToList();
        TopProductsEmptyText.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (products.Count == 0) return;
        var max = products.Max(product => product.Quantity);
        var barWidth = Math.Max(80, width - 125);
        foreach (var (product, index) in products.Select((value, index) => (value, index)))
        {
            var y = 14 + index * 30;
            AddChartText(canvas, product.Name, 0, y + 1, 10, ChartInk);
            canvas.Children.Add(new Rectangle { Width = barWidth * (double)(product.Quantity / max), Height = 11, RadiusX = 3, RadiusY = 3, Fill = ChartBrush(index == 0 ? ChartAccent : ChartAccentDark, (byte)(index == 0 ? 230 : 170)), Margin = new Thickness(112, y, 0, 0), ToolTip = $"{product.Quantity:0.##} units" });
            AddChartText(canvas, product.Quantity.ToString("0.##"), 116 + barWidth * (double)(product.Quantity / max), y - 2, 10, ChartInk, true);
        }
    }

    private void DrawPaymentDonut()
    {
        var canvas = PaymentDonutCanvas;
        canvas.Children.Clear();
        var width = ChartWidth(canvas, 210);
        var payments = _store.Data.Sales.GroupBy(s => s.PaymentMethod)
            .Select(group => (Method: group.Key, Total: group.Sum(s => s.Total))).OrderByDescending(item => item.Total).ToList();
        var total = payments.Sum(item => item.Total);
        PaymentEmptyText.Visibility = total <= 0 ? Visibility.Visible : Visibility.Collapsed;
        if (total <= 0) return;
        var center = new Point(Math.Min(width * .42, 78), 78);
        var brushes = new[] { ChartBrush(ChartAccent), ChartBrush(ChartAccentDark), ChartBrush(ChartAccent, 130) };
        var start = -90d;
        for (var index = 0; index < payments.Count; index++)
        {
            var sweep = (double)(payments[index].Total / total) * 359.8;
            canvas.Children.Add(new System.Windows.Shapes.Path { Data = DonutSegment(center, 52, 35, start, sweep), Fill = brushes[index % brushes.Length], Stroke = ChartBrush(ChartLine), StrokeThickness = 1 });
            start += sweep;
        }
        AddChartText(canvas, total.ToString("C0"), center.X - 28, center.Y - 9, 12, ChartInk, true);
        foreach (var (payment, index) in payments.Take(4).Select((value, index) => (value, index)))
        {
            var y = 28 + index * 29;
            canvas.Children.Add(new Rectangle { Width = 7, Height = 7, RadiusX = 2, RadiusY = 2, Fill = brushes[index % brushes.Length], Margin = new Thickness(Math.Min(width * .68, 132), y + 3, 0, 0) });
            AddChartText(canvas, payment.Method, Math.Min(width * .68, 132) + 13, y, 10, ChartInk);
            AddChartText(canvas, $"{payment.Total / total:P0}", Math.Min(width * .68, 132) + 13, y + 13, 9);
        }
    }

    private void DrawStockHealth()
    {
        var canvas = StockHealthCanvas;
        canvas.Children.Clear();
        var width = ChartWidth(canvas, 280);
        var products = _store.Data.Products.ToList();
        StockEmptyText.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (products.Count == 0) return;
        var healthy = products.Count(product => !IsLowStock(product));
        var attention = products.Count - healthy;
        var barWidth = width - 24;
        AddChartText(canvas, $"{healthy}/{products.Count} catalogued products healthy", 12, 18, 12, ChartInk, true);
        AddChartText(canvas, attention == 0 ? "No reorder signals" : $"{attention} reorder signal{(attention == 1 ? "" : "s")}", 12, 42, 10, attention == 0 ? ChartAccent : ChartAccentDark);
        canvas.Children.Add(new Rectangle { Width = barWidth, Height = 18, RadiusX = 9, RadiusY = 9, Fill = ChartBrush(ChartLine), Margin = new Thickness(12, 72, 0, 0) });
        canvas.Children.Add(new Rectangle { Width = barWidth * healthy / products.Count, Height = 18, RadiusX = 9, RadiusY = 9, Fill = ChartBrush(ChartAccent), Margin = new Thickness(12, 72, 0, 0) });
        AddChartText(canvas, "HEALTHY", 12, 105, 9, ChartMuted, true);
        AddChartText(canvas, "ATTENTION", width - 76, 105, 9, ChartMuted, true);
        AddChartText(canvas, $"{healthy * 100 / products.Count}%", 12, 122, 16, ChartAccent, true);
        AddChartText(canvas, $"{attention * 100 / products.Count}%", width - 55, 122, 16, ChartAccentDark, true);
    }

    private void DrawTransactionHours()
    {
        var canvas = TransactionHourCanvas;
        canvas.Children.Clear();
        var width = ChartWidth(canvas, 230);
        var buckets = Enumerable.Range(0, 6).Select(index =>
        {
            var from = index * 4;
            return (Label: $"{from:00}", Count: _store.Data.Sales.Count(s => s.CreatedAt.Hour >= from && s.CreatedAt.Hour < from + 4));
        }).ToList();
        var max = buckets.Max(item => item.Count);
        TransactionHourEmptyText.Visibility = max == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (max == 0) return;
        var barWidth = Math.Max(12, (width - 28) / buckets.Count - 7);
        for (var index = 0; index < buckets.Count; index++)
        {
            var barHeight = 94d * buckets[index].Count / max;
            var x = 14 + index * (barWidth + 7);
            canvas.Children.Add(new Rectangle { Width = barWidth, Height = barHeight, RadiusX = 3, RadiusY = 3, Fill = ChartBrush(index == buckets.Count - 1 ? ChartAccent : ChartAccentDark, 190), Margin = new Thickness(x, 110 - barHeight, 0, 0), ToolTip = $"{buckets[index].Label}:00 · {buckets[index].Count} transactions" });
            AddChartText(canvas, buckets[index].Label, x - 2, 118, 9);
        }
        AddChartText(canvas, $"{_store.Data.Sales.Count} transactions", 12, 13, 11, ChartInk, true);
    }

    private static PathGeometry DonutSegment(Point center, double outerRadius, double innerRadius, double startAngle, double sweepAngle)
    {
        static Point PointAt(Point origin, double radius, double angle)
        {
            var radians = angle * Math.PI / 180;
            return new Point(origin.X + radius * Math.Cos(radians), origin.Y + radius * Math.Sin(radians));
        }
        var outerStart = PointAt(center, outerRadius, startAngle);
        var outerEnd = PointAt(center, outerRadius, startAngle + sweepAngle);
        var innerStart = PointAt(center, innerRadius, startAngle + sweepAngle);
        var innerEnd = PointAt(center, innerRadius, startAngle);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
        figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0, sweepAngle > 180, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerStart, true));
        figure.Segments.Add(new ArcSegment(innerEnd, new Size(innerRadius, innerRadius), 0, sweepAngle > 180, SweepDirection.Counterclockwise, true));
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshSaleProducts();
    }

    private void ProductCategory_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshSaleProducts();

    private void ShowAllProducts_Click(object sender, RoutedEventArgs e)
    {
        ProductSearchBox.Clear();
        ProductCategoryCombo.SelectedIndex = 0;
        RefreshSaleProducts();
    }

    private void RefreshSaleProducts()
    {
        if (SaleProductsGrid is null || ProductSearchBox is null || ProductCategoryCombo is null)
            return;

        var search = ProductSearchBox.Text.Trim();
        var category = ProductCategoryCombo.SelectedItem as string;
        var products = _store.Data.Products
            .Where(product => string.IsNullOrWhiteSpace(category) ||
                              category.Equals("All categories", StringComparison.OrdinalIgnoreCase) ||
                              product.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(search) ||
                        p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.Barcode.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => product.Name)
            .ToList();
        SaleProductsGrid.ItemsSource = products;
        ProductCountLabel.Text = $"{products.Count} product button{(products.Count == 1 ? "" : "s")}";
    }

    private void RegisterAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        switch (button.Tag?.ToString())
        {
            case "Close":
                MainTabs.SelectedIndex = 0;
                MainPageTitle.Text = "Business command centre";
                HeaderSubtitle.Text = "Sales, stock and daily operations";
                break;
            case "New":
                _cart.Clear();
                CartGrid.Items.Refresh();
                PromotionCombo.SelectedItem = null;
                CustomerCombo.SelectedItem = null;
                TenderedBox.Text = "0";
                BarcodeBox.Clear();
                UpdateCartTotal();
                BarcodeBox.Focus();
                break;
            case "Remove":
                RemoveCartItem_Click(sender, e);
                break;
            case "Scan":
                BarcodeBox.Focus();
                break;
            case "Customer":
                CustomerCombo.Focus();
                CustomerCombo.IsDropDownOpen = true;
                break;
            case "Return":
                MainTabs.SelectedIndex = 6;
                MainPageTitle.Text = "Business command centre";
                HeaderSubtitle.Text = "Process returns and customer refunds";
                break;
            case "Stock":
                MainTabs.SelectedIndex = 2;
                MainPageTitle.Text = "Business command centre";
                HeaderSubtitle.Text = "Manage products, prices and stock levels";
                break;
            case "Pay":
                CompleteSale_Click(sender, e);
                break;
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (MainTabs.SelectedIndex != 1)
            return;

        var action = e.Key switch
        {
            Key.F1 => "Close",
            Key.F2 => "New",
            Key.F3 => "Remove",
            Key.F4 => "Scan",
            Key.F5 => "Customer",
            Key.F6 => "Return",
            Key.F7 => "Stock",
            Key.F8 => "Pay",
            _ => null
        };
        if (action is null)
            return;

        RegisterAction_Click(new Button { Tag = action }, new RoutedEventArgs());
        e.Handled = true;
    }

    private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { AddBarcode_Click(sender, e); e.Handled = true; }
    }

    private void AddBarcode_Click(object sender, RoutedEventArgs e)
    {
        var sku = BarcodeBox.Text.Trim();
        var product = _store.Data.Products.FirstOrDefault(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase) ||
                                                                p.Barcode.Equals(sku, StringComparison.OrdinalIgnoreCase));
        if (product is null) { MessageBox.Show("No product matches that barcode or SKU."); return; }
        AddProductToCart(product);
        BarcodeBox.Clear();
        BarcodeBox.Focus();
    }

    private void SaleProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SaleProductsGrid.SelectedItem is Product product) AddProductToCart(product);
        SaleProductsGrid.SelectedItem = null;
    }

    private void AddProductToCart(Product product)
    {
        var line = _cart.FirstOrDefault(l => l.ProductId == product.Id);
        if (line is null) _cart.Add(new SaleLine { ProductId = product.Id, Sku = product.Sku, Name = product.Name, UnitPrice = product.Price, Quantity = 1, Unit = product.Unit, UnitLabel = product.UnitLabel });
        else if (line.Quantity < product.StockQuantity) line.Quantity++;
        CartGrid.Items.Refresh();
        UpdateCartTotal();
    }

    private void RemoveCartItem_Click(object sender, RoutedEventArgs e)
    {
        if (CartGrid.SelectedItem is SaleLine line) _cart.Remove(line);
        UpdateCartTotal();
    }

    private void CompleteSale_Click(object sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0) { MessageBox.Show("Add at least one product to the sale."); return; }
        foreach (var line in _cart)
        {
            var product = _store.Data.Products.First(p => p.Id == line.ProductId);
            if (line.Quantity > product.StockQuantity) { MessageBox.Show($"Not enough stock for {product.Name}."); return; }
        }
        var paymentMethod = (PaymentCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
        var isOnAccount = paymentMethod.Equals("On account", StringComparison.OrdinalIgnoreCase);
        var customer = CustomerCombo.SelectedItem as Customer;
        if (isOnAccount && customer is null)
        {
            MessageBox.Show("Select a customer before recording a sale on account.");
            return;
        }
        var tendered = decimal.TryParse(TenderedBox.Text, out var amount) ? amount : 0;
        var sale = new Sale
        {
            ReceiptNumber = $"GG-{DateTime.Now:yyyyMMdd-HHmmss}",
            CustomerId = customer?.Id,
            CustomerName = customer?.Name ?? "",
            Discount = CalculateDiscount(),
            TaxRate = _store.Data.Tax.Rate,
            TaxInclusive = _store.Data.Tax.Inclusive,
            PaymentMethod = paymentMethod,
            Tendered = tendered
        };
        sale.Lines.AddRange(_cart.Select(line => new SaleLine { ProductId = line.ProductId, Sku = line.Sku, Name = line.Name, UnitPrice = line.UnitPrice, Quantity = line.Quantity, Unit = line.Unit, UnitLabel = line.UnitLabel }));
        sale.Payments = isOnAccount ? [] : ParsePayments(paymentMethod, TenderedBox.Text, sale.Total);
        if (!isOnAccount && (sale.Payments.Count == 0 || Math.Abs(sale.Payments.Sum(p => p.Amount) - sale.Total) > 0.005m))
        {
            MessageBox.Show($"Payment allocations must equal the sale total ({sale.Total:C}). For split payment enter: Cash:10, Gift card:5 in Tendered.");
            return;
        }
        if (sale.PaymentMethod == "Cash" && tendered < sale.Total) { MessageBox.Show($"Cash tendered is less than the total ({sale.Total:C})."); return; }
        foreach (var line in _cart) _store.Data.Products.First(p => p.Id == line.ProductId).StockQuantity -= line.Quantity;
        _store.Data.Sales.Add(sale);
        if (_store.Data.Settings.Notifications.SaleTimestampNotifications)
            _store.Data.Notifications.Add(new NotificationRecord
            {
                Type = "Sale",
                Message = $"Sale {sale.ReceiptNumber} recorded at {sale.CreatedAt:HH:mm:ss} ({sale.Total:C})."
            });
        _store.Save();
        _cart.Clear();
        CartGrid.Items.Refresh();
        Refresh();
        _ = NotifyLowStockAsync();
        MessageBox.Show($"Receipt {sale.ReceiptNumber}\n\nTOTAL: {sale.Total:C}" +
                        (isOnAccount ? $"\nCharged to {customer!.Name}'s account." : $"\nChange: {sale.Change:C}"), "Sale completed");
        PrintSale(sale);
    }

    private decimal CalculateDiscount()
    {
        var subtotal = _cart.Sum(l => l.LineTotal);
        return _selectedPromotion is null ? 0 : Math.Round(subtotal * _selectedPromotion.DiscountPercent / 100m, 2);
    }

    private static List<PaymentAllocation> ParsePayments(string method, string input, decimal total)
    {
        if (method == "Split payment")
        {
            return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split(':', 2, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2 && decimal.TryParse(parts[1], out _))
                .Select(parts => new PaymentAllocation { Method = NormalizePaymentMethod(parts[0]), Amount = decimal.Parse(parts[1]) })
                .Where(payment => payment.Amount > 0)
                .ToList();
        }
        return decimal.TryParse(input, out var amount) && amount > 0
            ? [new PaymentAllocation { Method = NormalizePaymentMethod(method), Amount = Math.Min(amount, total) }]
            : [];
    }

    private static string NormalizePaymentMethod(string method) => method.Trim().ToLowerInvariant() switch
    {
        "loyalty points" => "Loyalty points",
        "gift card" => "Gift card",
        "on account" => "On account",
        "store credit" => "Store credit",
        "bank transfer" => "Bank transfer",
        "card" => "Card",
        _ => "Cash"
    };

    private void PromotionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPromotion = PromotionCombo.SelectedItem as Promotion;
        UpdateCartTotal();
    }

    private void TenderedBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCartTotal();

    private void PaymentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TenderedBox is null || PaymentAmountLabel is null) return;
        var onAccount = (PaymentCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "On account";
        PaymentAmountLabel.Text = onAccount ? "No payment collected" : "Paid amount";
        TenderedBox.IsEnabled = !onAccount;
        if (onAccount) TenderedBox.Text = "0";
        UpdateCartTotal();
    }

    private void UpdateCartTotal()
    {
        if (CartSubtotalText is null || CartDiscountText is null || CartTaxText is null ||
            CartTotalText is null || TenderedBox is null || ChangeText is null)
            return;

        var subtotal = _cart.Sum(l => l.LineTotal);
        var discount = CalculateDiscount();
        var tax = _store.Data.Tax.Inclusive
            ? Math.Max(0, (subtotal - discount) * _store.Data.Tax.Rate / (100m + _store.Data.Tax.Rate))
            : Math.Max(0, (subtotal - discount) * _store.Data.Tax.Rate / 100m);
        var total = _store.Data.Tax.Inclusive
            ? Math.Max(0, subtotal - discount)
            : Math.Max(0, subtotal - discount + tax);
        CartSubtotalText.Text = subtotal.ToString("C");
        CartDiscountText.Text = discount.ToString("C");
        CartTaxText.Text = tax.ToString("C");
        CartTotalText.Text = total.ToString("C");
        var isOnAccount = (PaymentCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "On account";
        var tendered = decimal.TryParse(TenderedBox.Text, out var t) ? t : 0;
        ChangeText.Text = isOnAccount ? "On account" : Math.Max(0, tendered - total).ToString("C");
    }

    private void InventoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InventoryGrid.SelectedItem is not Product product) return;
        _selectedProduct = product;
        SkuBox.Text = product.Sku; NameBox.Text = product.Name; CategoryBox.Text = product.Category;
        BarcodeProductBox.Text = product.Barcode; UnitCombo.SelectedItem = product.Unit; UnitLabelBox.Text = product.UnitLabel;
        PriceBox.Text = product.Price.ToString("0.00"); StockBox.Text = product.StockQuantity.ToString(); ReorderBox.Text = product.ReorderLevel.ToString();
    }

    private void NewProduct_Click(object sender, RoutedEventArgs e)
    {
        _selectedProduct = null;
        SkuBox.Clear(); NameBox.Clear(); CategoryBox.Clear(); BarcodeProductBox.Clear(); UnitLabelBox.Clear();
        UnitCombo.SelectedItem = UnitOfMeasure.Each; PriceBox.Clear(); StockBox.Clear(); ReorderBox.Text = "5";
    }

    private void SaveProduct_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SkuBox.Text) || string.IsNullOrWhiteSpace(NameBox.Text) ||
            !decimal.TryParse(PriceBox.Text, out var price) ||                 !decimal.TryParse(StockBox.Text, out var stock) ||
                !decimal.TryParse(ReorderBox.Text, out var reorder) || price < 0 || stock < 0 || reorder < 0)
        { MessageBox.Show("Enter a SKU, name, valid price, stock quantity and reorder level."); return; }
        var barcode = string.IsNullOrWhiteSpace(BarcodeProductBox.Text) ? SkuBox.Text.Trim() : BarcodeProductBox.Text.Trim();
        if (_store.Data.Products.Any(existing => existing.Id != _selectedProduct?.Id &&
            (existing.Sku.Equals(SkuBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
             existing.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase))))
        {
            MessageBox.Show("Each product must have a unique SKU and barcode.");
            return;
        }
        var product = _selectedProduct ?? new Product();
        product.Sku = SkuBox.Text.Trim(); product.Barcode = barcode;
        product.Name = NameBox.Text.Trim(); product.Category = CategoryBox.Text.Trim();
        product.Unit = UnitCombo.SelectedItem is UnitOfMeasure unit ? unit : UnitOfMeasure.Each;
        product.UnitLabel = string.IsNullOrWhiteSpace(UnitLabelBox.Text) ? product.Unit.ToString() : UnitLabelBox.Text.Trim();
        product.Price = price; product.StockQuantity = stock; product.ReorderLevel = reorder;
        if (_selectedProduct is null) _store.Data.Products.Add(product);
        _store.Save(); Refresh(); InventoryGrid.SelectedItem = product;
    }

    private void PriceLookupBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RefreshPriceLookup();
            e.Handled = true;
        }
    }

    private void PriceLookupBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshPriceLookup();

    private void ClearPriceLookup_Click(object sender, RoutedEventArgs e) => PriceLookupBox.Clear();

    private void SalesHistorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshSalesHistory();

    private void RefreshSalesHistory()
    {
        if (SalesHistoryGrid is null || SalesHistorySearchBox is null) return;
        var query = SalesHistorySearchBox.Text.Trim();
        SalesHistoryGrid.ItemsSource = _store.Data.Sales
            .Where(sale => query.Length == 0 ||
                           sale.ReceiptNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           sale.PaymentMethod.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           sale.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(sale => sale.CreatedAt)
            .ToList();
    }

    private void RefreshPriceLookup()
    {
        if (PriceLookupBox is null || PriceLookupResultText is null || PriceLookupStockText is null) return;
        var query = PriceLookupBox.Text.Trim();
        if (query.Length == 0)
        {
            PriceLookupResultText.Text = "Scan a barcode or search by product name or SKU.";
            PriceLookupStockText.Text = "";
            return;
        }

        var matches = _store.Data.Products.Where(product =>
            product.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            product.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        if (matches.Count == 0)
        {
            PriceLookupResultText.Text = "No matching product found.";
            PriceLookupStockText.Text = "";
            return;
        }

        var product = matches[0];
        PriceLookupResultText.Text = $"{product.Name}\n{product.Sku}  ·  {product.Category}";
        PriceLookupStockText.Text = $"{product.Price:C}  ·  {product.StockQuantity:0.##} {product.UnitLabel} in stock" +
                                    (matches.Count > 1 ? "\nMore than one product matches; refine the search." : "");
    }

    private void InventorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshInventoryView();

    private void InventoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshInventoryView();

    private void RefreshInventoryView()
    {
        if (InventoryManagementGrid is null || InventorySearchBox is null || InventoryFilterCombo is null) return;
        var query = InventorySearchBox.Text.Trim();
        var filter = (InventoryFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All stock";
        var products = _store.Data.Products.Where(product =>
            query.Length == 0 ||
            product.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            product.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            product.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        products = filter switch
        {
            "Reorder needed" => products.Where(IsLowStock),
            "Out of stock" => products.Where(product => product.StockQuantity <= 0),
            _ => products
        };
        var result = products.OrderBy(product => product.Name).ToList();
        InventoryManagementGrid.ItemsSource = result;
        if (InventoryManagementCountText is not null)
            InventoryManagementCountText.Text = $"{result.Count} products · {result.Sum(product => product.StockQuantity):0.##} units on hand";
    }

    private void AdjustInventory_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryManagementGrid.SelectedItem is not Product product)
        {
            MessageBox.Show("Select a product before adjusting stock.");
            return;
        }
        if (!decimal.TryParse(InventoryAdjustmentQuantityBox.Text, out var adjustment) || adjustment == 0 ||
            string.IsNullOrWhiteSpace(InventoryAdjustmentReasonBox.Text))
        {
            MessageBox.Show("Enter a non-zero quantity change and a reason for the adjustment.");
            return;
        }
        if (product.StockQuantity + adjustment < 0)
        {
            MessageBox.Show($"This adjustment cannot reduce {product.Name} below zero stock.");
            return;
        }

        product.StockQuantity += adjustment;
        _store.Data.StockAdjustments.Add(new StockAdjustment
        {
            ProductId = product.Id,
            ProductName = product.Name,
            QuantityChange = adjustment,
            QuantityAfter = product.StockQuantity,
            Reason = InventoryAdjustmentReasonBox.Text.Trim(),
            RecordedBy = AppSession.CurrentUser?.UserName ?? "operator"
        });
        _store.Save();
        InventoryAdjustmentQuantityBox.Clear();
        InventoryAdjustmentReasonBox.Clear();
        Refresh();
    }

    private void SaveExpense_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ExpenseCategoryBox.Text) ||
            string.IsNullOrWhiteSpace(ExpenseDescriptionBox.Text) ||
            !decimal.TryParse(ExpenseAmountBox.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Enter an expense category, description and amount greater than zero.");
            return;
        }

        var paymentMethod = (ExpensePaymentCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
        _store.Data.Expenses.Add(new OperatingExpense
        {
            Category = ExpenseCategoryBox.Text.Trim(),
            Description = ExpenseDescriptionBox.Text.Trim(),
            Amount = amount,
            PaymentMethod = paymentMethod,
            Reference = ExpenseReferenceBox.Text.Trim(),
            RecordedBy = AppSession.CurrentUser?.UserName ?? "operator"
        });
        _store.Save();
        ExpenseDescriptionBox.Clear();
        ExpenseAmountBox.Clear();
        ExpenseReferenceBox.Clear();
        Refresh();
    }

    private void LedgerPartyTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshLedgerParties();

    private void LedgerPartyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshLedger();

    private void RefreshLedgerParties()
    {
        if (LedgerPartyTypeCombo is null || LedgerPartyCombo is null) return;
        var type = (LedgerPartyTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer";
        var currentId = (LedgerPartyCombo.SelectedItem as Customer)?.Id ?? (LedgerPartyCombo.SelectedItem as Supplier)?.Id;
        var parties = type == "Supplier"
            ? _store.Data.Suppliers.OrderBy(supplier => supplier.Name).Cast<object>().ToList()
            : _store.Data.Customers.OrderBy(customer => customer.Name).Cast<object>().ToList();
        LedgerPartyCombo.ItemsSource = parties;
        LedgerPartyCombo.DisplayMemberPath = nameof(Customer.Name);
        LedgerPartyCombo.SelectedItem = parties.FirstOrDefault(party =>
            party is Customer customer && customer.Id == currentId ||
            party is Supplier supplier && supplier.Id == currentId);
        RefreshLedger();
    }

    private void RefreshLedger()
    {
        if (LedgerEntriesGrid is null || LedgerPartyTypeCombo is null || LedgerPartyCombo is null) return;
        var type = (LedgerPartyTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer";
        var partyId = (LedgerPartyCombo.SelectedItem as Customer)?.Id ?? (LedgerPartyCombo.SelectedItem as Supplier)?.Id ?? Guid.Empty;
        if (partyId == Guid.Empty)
        {
            LedgerEntriesGrid.ItemsSource = Array.Empty<LedgerEntryView>();
            LedgerBalanceText.Text = 0m.ToString("C");
            return;
        }

        var rows = new List<(DateTime Date, string Entry, string Reference, decimal Debit, decimal Credit)>();
        if (type == "Customer")
        {
            foreach (var sale in _store.Data.Sales.Where(sale => sale.CustomerId == partyId && sale.PaymentMethod == "On account"))
            {
                rows.Add((sale.CreatedAt, "Credit sale", sale.ReceiptNumber, sale.Total, 0));
                var refund = _store.Data.Returns.FirstOrDefault(item =>
                    item.ReceiptNumber.Equals(sale.ReceiptNumber, StringComparison.OrdinalIgnoreCase));
                if (refund is not null)
                    rows.Add((refund.CreatedAt, "Credit sale refund", refund.ReceiptNumber, 0, refund.Amount));
            }
        }
        else
        {
            foreach (var purchase in _store.Data.PurchaseOrders.Where(order => order.SupplierId == partyId && order.Status == "Received"))
                rows.Add((purchase.ReceivedAt ?? purchase.CreatedAt, "Received purchase", purchase.Number, 0, purchase.Total));
        }

        foreach (var transaction in _store.Data.LedgerTransactions.Where(item => item.PartyId == partyId && item.PartyType == type))
        {
            var isCustomerReceipt = type == "Customer";
            rows.Add((transaction.CreatedAt, isCustomerReceipt ? "Customer receipt" : "Supplier payment",
                transaction.Reference, isCustomerReceipt ? 0 : transaction.Amount, isCustomerReceipt ? transaction.Amount : 0));
        }

        decimal balance = 0;
        var entries = rows.OrderBy(row => row.Date).Select(row =>
        {
            balance += type == "Customer" ? row.Debit - row.Credit : row.Credit - row.Debit;
            return new LedgerEntryView(row.Date, row.Entry, row.Reference, row.Debit, row.Credit, balance);
        }).ToList();
        LedgerEntriesGrid.ItemsSource = entries.OrderByDescending(entry => entry.Date).ToList();
        LedgerBalanceText.Text = balance.ToString("C");
        LedgerBalanceCaptionText.Text = balance >= 0
            ? type == "Customer" ? "Amount due from customer" : "Amount payable to supplier"
            : type == "Customer" ? "Customer credit balance" : "Supplier account credit";
    }

    private void RecordLedgerPayment_Click(object sender, RoutedEventArgs e)
    {
        var type = (LedgerPartyTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer";
        var partyId = (LedgerPartyCombo.SelectedItem as Customer)?.Id ?? (LedgerPartyCombo.SelectedItem as Supplier)?.Id ?? Guid.Empty;
        var partyName = (LedgerPartyCombo.SelectedItem as Customer)?.Name ?? (LedgerPartyCombo.SelectedItem as Supplier)?.Name;
        if (partyId == Guid.Empty || string.IsNullOrWhiteSpace(partyName))
        {
            MessageBox.Show($"Select a {type.ToLowerInvariant()} first.");
            return;
        }
        if (!decimal.TryParse(LedgerPaymentAmountBox.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Enter a payment amount greater than zero.");
            return;
        }

        RefreshLedger();
        var balance = LedgerEntriesGrid.ItemsSource?.Cast<LedgerEntryView>().FirstOrDefault()?.Balance ?? 0;
        if (amount > balance)
        {
            MessageBox.Show($"The payment cannot exceed the outstanding balance of {balance:C}.");
            return;
        }
        _store.Data.LedgerTransactions.Add(new LedgerTransaction
        {
            PartyType = type,
            PartyId = partyId,
            Amount = amount,
            PaymentMethod = (LedgerPaymentMethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash",
            Reference = LedgerPaymentReferenceBox.Text.Trim(),
            RecordedBy = AppSession.CurrentUser?.UserName ?? "operator"
        });
        _store.Save();
        LedgerPaymentAmountBox.Clear();
        LedgerPaymentReferenceBox.Clear();
        Refresh();
    }

    private void CustomersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not Customer customer) return;
        _selectedCustomer = customer; CustomerNameBox.Text = customer.Name; CustomerPhoneBox.Text = customer.Phone;
        CustomerEmailBox.Text = customer.Email; CustomerAddressBox.Text = customer.Address; CustomerNotesBox.Text = customer.Notes;
    }

    private void NewCustomer_Click(object sender, RoutedEventArgs e) { _selectedCustomer = null; CustomerNameBox.Clear(); CustomerPhoneBox.Clear(); CustomerEmailBox.Clear(); CustomerAddressBox.Clear(); CustomerNotesBox.Clear(); }

    private void SaveCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CustomerNameBox.Text)) { MessageBox.Show("Enter a customer name."); return; }
        var c = _selectedCustomer ?? new Customer();
        c.Name = CustomerNameBox.Text.Trim(); c.Phone = CustomerPhoneBox.Text.Trim(); c.Email = CustomerEmailBox.Text.Trim(); c.Address = CustomerAddressBox.Text.Trim(); c.Notes = CustomerNotesBox.Text.Trim();
        if (_selectedCustomer is null) _store.Data.Customers.Add(c);
        _store.Save(); Refresh();
    }

    private void SuppliersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SuppliersGrid.SelectedItem is not Supplier supplier) return;
        _selectedSupplier = supplier; SupplierNameBox.Text = supplier.Name; SupplierContactBox.Text = supplier.Contact; SupplierPhoneBox.Text = supplier.Phone; SupplierAddressBox.Text = supplier.Address;
    }

    private void NewSupplier_Click(object sender, RoutedEventArgs e) { _selectedSupplier = null; SupplierNameBox.Clear(); SupplierContactBox.Clear(); SupplierPhoneBox.Clear(); SupplierAddressBox.Clear(); }

    private void SaveSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SupplierNameBox.Text)) { MessageBox.Show("Enter a supplier name."); return; }
        var s = _selectedSupplier ?? new Supplier();
        s.Name = SupplierNameBox.Text.Trim(); s.Contact = SupplierContactBox.Text.Trim(); s.Phone = SupplierPhoneBox.Text.Trim(); s.Address = SupplierAddressBox.Text.Trim();
        if (_selectedSupplier is null) _store.Data.Suppliers.Add(s);
        _store.Save(); Refresh();
    }

    private void AddPurchaseLine_Click(object sender, RoutedEventArgs e)
    {
        if (PurchaseSupplierCombo.SelectedItem is not Supplier supplier || PurchaseProductCombo.SelectedItem is not Product product ||
            !decimal.TryParse(PurchaseQtyBox.Text, out var qty) || !decimal.TryParse(PurchaseCostBox.Text, out var cost) || qty <= 0)
        { MessageBox.Show("Select a supplier and product and enter valid quantity/cost."); return; }
        if (_draftPurchase is null || _draftPurchase.Status != "Draft" || _draftPurchase.SupplierId != supplier.Id)
        {
            _draftPurchase = new PurchaseOrder { Number = $"PO-{DateTime.Now:yyyyMMdd-HHmmss}", SupplierId = supplier.Id };
        }
        _draftPurchase.Lines.Add(new PurchaseOrderLine { ProductId = product.Id, ProductName = product.Name, Quantity = qty, UnitCost = cost, Unit = product.Unit });
        if (!_store.Data.PurchaseOrders.Contains(_draftPurchase)) _store.Data.PurchaseOrders.Add(_draftPurchase);
        _store.Save(); Refresh();
    }

    private void ReceivePurchase_Click(object sender, RoutedEventArgs e)
    {
        var order = _draftPurchase is { Status: "Draft" }
            ? _draftPurchase
            : _store.Data.PurchaseOrders.LastOrDefault(p => p.Status == "Draft");
        if (order is null) { MessageBox.Show("Add a purchase order line first."); return; }
        foreach (var line in order.Lines)
        {
            var product = _store.Data.Products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product is not null) product.StockQuantity += line.Quantity;
        }
        order.Status = "Received";
        order.ReceivedAt = DateTime.Now;
        _draftPurchase = null;
        _store.Save();
        Refresh();
    }

    private void RefundReceipt_Click(object sender, RoutedEventArgs e)
    {
        var sale = _store.Data.Sales.FirstOrDefault(s => s.ReceiptNumber.Equals(ReturnReceiptBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (sale is null) { MessageBox.Show("Receipt not found."); return; }
        if (_store.Data.Returns.Any(r => r.ReceiptNumber.Equals(sale.ReceiptNumber, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("That receipt has already been refunded."); return; }
        foreach (var line in sale.Lines)
        {
            var product = _store.Data.Products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product is not null) product.StockQuantity += line.Quantity;
        }
        _store.Data.Returns.Add(new ReturnRecord
        {
            ReceiptNumber = sale.ReceiptNumber,
            Reason = ReturnReasonBox.Text.Trim(),
            Amount = sale.Total,
            PaymentMethod = sale.PaymentMethod == "On account" ? "On account" : "Cash"
        });
        _store.Save(); ReturnReceiptBox.Clear(); ReturnReasonBox.Clear(); Refresh();
    }

    private void PromotionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPromotion = PromotionsGrid.SelectedItem as Promotion;
        if (_selectedPromotion is not null) { PromotionNameBox.Text = _selectedPromotion.Name; PromotionCodeBox.Text = _selectedPromotion.Code; PromotionPercentBox.Text = _selectedPromotion.DiscountPercent.ToString("0.##"); }
    }

    private void SavePromotion_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromotionNameBox.Text) || !decimal.TryParse(PromotionPercentBox.Text, out var percent) || percent < 0 || percent > 100) { MessageBox.Show("Enter a name and a discount from 0 to 100."); return; }
        var p = _selectedPromotion ?? new Promotion();
        p.Name = PromotionNameBox.Text.Trim(); p.Code = PromotionCodeBox.Text.Trim(); p.DiscountPercent = percent; p.Active = true;
        if (_selectedPromotion is null) _store.Data.Promotions.Add(p);
        _store.Save(); Refresh();
    }

    private void SaveTax_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (!decimal.TryParse(TaxRateBox.Text, out var rate) || rate < 0 || rate > 100) { MessageBox.Show("Enter a valid VAT rate."); return; }
        _store.Data.Tax.Name = TaxNameBox.Text.Trim(); _store.Data.Tax.Rate = rate; _store.Data.Tax.Inclusive = TaxInclusiveBox.IsChecked == true;
        _store.Save(); Refresh();
    }

    private void OpenTill_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(OpeningFloatBox.Text, out var opening) || opening < 0) { MessageBox.Show("Enter a valid opening float."); return; }
        StartTill(opening);
    }

    private void OpenShift_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ShiftOpeningFloatBox.Text, out var opening) || opening < 0) { MessageBox.Show("Enter a valid opening float."); return; }
        StartTill(opening);
    }

    private void StartTill(decimal opening)
    {
        if (_store.Data.TillSessions.Any(t => !t.ClosedAt.HasValue)) { MessageBox.Show("The till is already open."); return; }
        _store.Data.TillSessions.Add(new TillSession
        {
            OpeningFloat = opening,
            OpenedBy = AppSession.CurrentUser?.UserName ?? "operator"
        });
        _store.Save();
        Refresh();
    }

    private void CloseTill_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(CountedCashBox.Text, out var counted) || counted < 0) { MessageBox.Show("Enter counted cash."); return; }
        EndTill(counted);
    }

    private void CloseShift_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ShiftCountedCashBox.Text, out var counted) || counted < 0) { MessageBox.Show("Enter counted cash."); return; }
        EndTill(counted);
    }

    private void EndTill(decimal counted)
    {
        var till = _store.Data.TillSessions.LastOrDefault(t => !t.ClosedAt.HasValue);
        if (till is null) { MessageBox.Show("Open the till first."); return; }
        var cashSales = _store.Data.Sales.Where(sale => sale.CreatedAt >= till.OpenedAt)
            .Sum(sale => sale.Payments.Count > 0
                ? sale.Payments.Where(payment => payment.Method == "Cash").Sum(payment => payment.Amount)
                : sale.PaymentMethod == "Cash" ? sale.Total : 0);
        var cashExpenses = _store.Data.Expenses.Where(expense =>
            expense.CreatedAt >= till.OpenedAt && expense.PaymentMethod == "Cash").Sum(expense => expense.Amount);
        var cashLedgerReceipts = _store.Data.LedgerTransactions.Where(transaction =>
            transaction.CreatedAt >= till.OpenedAt && transaction.PartyType == "Customer" &&
            transaction.PaymentMethod == "Cash").Sum(transaction => transaction.Amount);
        var cashSupplierPayments = _store.Data.LedgerTransactions.Where(transaction =>
            transaction.CreatedAt >= till.OpenedAt && transaction.PartyType == "Supplier" &&
            transaction.PaymentMethod == "Cash").Sum(transaction => transaction.Amount);
        till.ExpectedCash = till.OpeningFloat + cashSales + cashLedgerReceipts - cashSupplierPayments - cashExpenses -
                            _store.Data.Returns.Where(record => record.CreatedAt >= till.OpenedAt && record.PaymentMethod == "Cash")
                                .Sum(record => record.Amount);
        till.CountedCash = counted;
        till.ClosedAt = DateTime.Now;
        till.ClosedBy = AppSession.CurrentUser?.UserName ?? "operator";
        _store.Save();
        Refresh();
        MessageBox.Show($"Till closed. Variance: {till.Variance:C}");
    }

    private void RefreshShiftStatus()
    {
        if (CurrentShiftText is null || CurrentShiftSalesText is null) return;
        var activeShift = _store.Data.TillSessions.LastOrDefault(till => !till.ClosedAt.HasValue);
        if (activeShift is null)
        {
            CurrentShiftText.Text = "No shift is currently open.";
            CurrentShiftSalesText.Text = "Open a till to begin tracking a cashier shift.";
            return;
        }

        var sales = _store.Data.Sales.Where(sale => sale.CreatedAt >= activeShift.OpenedAt).ToList();
        var saleCash = sales.Sum(sale => sale.Payments.Count > 0
            ? sale.Payments.Where(payment => payment.Method == "Cash").Sum(payment => payment.Amount)
            : sale.PaymentMethod == "Cash" ? sale.Total : 0);
        var accountCash = _store.Data.LedgerTransactions.Where(transaction =>
            transaction.CreatedAt >= activeShift.OpenedAt && transaction.PartyType == "Customer" &&
            transaction.PaymentMethod == "Cash").Sum(transaction => transaction.Amount);
        CurrentShiftText.Text = $"Open since {activeShift.OpenedAt:g} · {activeShift.OpenedBy}";
        CurrentShiftSalesText.Text = $"{sales.Count} transactions · {sales.Sum(sale => sale.Total):C} gross sales · " +
                                     $"{saleCash + accountCash:C} cash received";
    }

    private void RefreshReport_Click(object sender, RoutedEventArgs e)
    {
        RefreshReport();
    }

    private void ReportPeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshReport();

    private void RefreshReport()
    {
        if (ReportSummaryText is null || ReportPeriodCombo is null) return;
        var periodStart = ReportPeriodStart;
        var sales = _store.Data.Sales.Where(sale => sale.CreatedAt >= periodStart && sale.CreatedAt <= DateTime.Now).ToList();
        var expenses = _store.Data.Expenses.Where(expense => expense.CreatedAt >= periodStart && expense.CreatedAt <= DateTime.Now).ToList();
        var refunds = _store.Data.Returns.Where(record => record.CreatedAt >= periodStart && record.CreatedAt <= DateTime.Now).ToList();
        var top = sales.SelectMany(sale => sale.Lines).GroupBy(line => line.Name)
            .OrderByDescending(group => group.Sum(line => line.Quantity)).Take(5)
            .Select(group => $"{group.Key} ({group.Sum(line => line.Quantity)})");
        var periodName = ReportPeriodCombo.SelectedIndex switch { 1 => "Last 7 days", 2 => "This month", _ => "Today" };
        ReportSummaryText.Text = $"{periodName}: {sales.Count} transactions, {sales.Sum(sale => sale.Total):C} sales, " +
                                 $"{sales.Sum(sale => sale.Tax):C} VAT, {expenses.Sum(expense => expense.Amount):C} expenses, " +
                                 $"{refunds.Sum(record => record.Amount):C} refunds, " +
                                 $"{sales.Sum(sale => sale.Total) - expenses.Sum(expense => expense.Amount) - refunds.Sum(record => record.Amount):C} " +
                                 "sales less expenses and refunds (before cost of goods).\n" +
                                 $"All time: {_store.Data.Sales.Count} transactions, {_store.Data.Sales.Sum(s => s.Total):C} sales, {_store.Data.Returns.Sum(r => r.Amount):C} refunds, {_store.Data.Expenses.Sum(expense => expense.Amount):C} expenses.\n" +
                                 $"Low stock: {_store.Data.Products.Count(IsLowStock)} (threshold {_store.Data.Settings.LowStockAlertThreshold:0.##}). Top products: {string.Join(", ", top)}";
    }

    private void ExportSalesCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseSavePath("Sales CSV|*.csv", "sales.csv"); if (path is null) return;
        var rows = new List<string> { "Receipt,Date,Customer,Items,Subtotal,Discount,VAT,Total,Payment" };
        rows.AddRange(_store.Data.Sales.Select(s => CsvRow(s.ReceiptNumber, s.CreatedAt.ToString("s"), CustomerName(s.CustomerId), s.ItemCount, s.Subtotal, s.Discount, s.Tax, s.Total, s.PaymentMethod)));
        File.WriteAllLines(path, rows, Encoding.UTF8); MessageBox.Show($"Exported {rows.Count - 1} sales.");
    }

    private void ExportInventoryCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseSavePath("Inventory CSV|*.csv", "inventory.csv"); if (path is null) return;
        File.WriteAllLines(path, new[] { "SKU,Name,Category,Price,Stock,ReorderLevel" }.Concat(_store.Data.Products.Select(p => CsvRow(p.Sku, p.Name, p.Category, p.Price, p.StockQuantity, p.ReorderLevel))), Encoding.UTF8);
        MessageBox.Show($"Exported {_store.Data.Products.Count} products.");
    }

    private void ExportExpensesCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseSavePath("Expenses CSV|*.csv", "expenses.csv");
        if (path is null) return;
        var rows = new List<string> { "Date,Category,Description,Amount,Payment,Reference,RecordedBy" };
        rows.AddRange(_store.Data.Expenses.OrderBy(expense => expense.CreatedAt)
            .Select(expense => CsvRow(expense.CreatedAt.ToString("s"), expense.Category, expense.Description,
                expense.Amount, expense.PaymentMethod, expense.Reference, expense.RecordedBy)));
        File.WriteAllLines(path, rows, Encoding.UTF8);
        MessageBox.Show($"Exported {rows.Count - 1} expenses.");
    }

    private static string Csv(object? value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
    private static string CsvRow(params object?[] values) => string.Join(",", values.Select(Csv));
    private string CustomerName(Guid? id) => _store.Data.Customers.FirstOrDefault(c => c.Id == id)?.Name ?? "";
    private static string? ChooseSavePath(string filter, string name) { var dialog = new SaveFileDialog { Filter = filter, FileName = name }; return dialog.ShowDialog() == true ? dialog.FileName : null; }

    private void LoadSettings()
    {
        var settings = _store.Data.Settings; HeaderSubtitle.Text = $"{settings.BusinessName} • {settings.BusinessType} • Offline-first operations";
        BusinessNameBox.Text = settings.BusinessName; BusinessEmailBox.Text = settings.Email;
        BusinessAddressBox.Text = settings.Address; BusinessCityBox.Text = settings.City;
        BusinessCountryBox.Text = settings.Country; BusinessPhoneBox.Text = settings.Phone;
        ReceiptFooterBox.Text = settings.ReceiptFooter;
        BusinessTypeCombo.SelectedValue = settings.BusinessType;
        CompanyLogoBox.Text = settings.CompanyLogoPath;
        CurrencySymbolCombo.SelectedValue = settings.CurrencySymbol;
        LowStockThresholdBox.Text = settings.LowStockAlertThreshold.ToString("0.##");
        OwnerEmailBox.Text = string.IsNullOrWhiteSpace(settings.LowStockOwnerEmail) ? settings.Reports.OwnerEmail : settings.LowStockOwnerEmail;
        SaleNotificationsBox.IsChecked = settings.Notifications.SaleTimestampNotifications;
        OwnerEmailAlertsBox.IsChecked = settings.Notifications.LowStockOwnerEmailAlerts;
        SyncEnabledBox.IsChecked = settings.Integrations.OnlineSyncEnabled;
        SyncEndpointBox.Text = settings.Integrations.SyncEndpoint;
        SyncApiKeyBox.Password = settings.Integrations.SyncApiKey;
        GoogleDriveFolderBox.Text = settings.Integrations.GoogleDriveFolderId;
        GoogleSheetsIdBox.Text = settings.Integrations.GoogleSheetsSpreadsheetId;
        GoogleSheetsTabBox.Text = settings.Integrations.GoogleSheetsWorksheet;
        WhatsAppWebhookBox.Text = settings.Integrations.WhatsAppWebhookUrl;
        SmsWebhookBox.Text = settings.Integrations.SmsWebhookUrl;
        EmailWebhookBox.Text = settings.Integrations.EmailWebhookUrl;
        if (DatabaseStatusText is not null) DatabaseStatusText.Text = $"Local JSON: {_store.DataPath}";
        if (SettingsStatusText is not null) SettingsStatusText.Text = $"Last saved: {_store.LastSavedAt.ToString("g", CultureInfo.CurrentCulture)}";
        TaxNameBox.Text = _store.Data.Tax.Name; TaxRateBox.Text = _store.Data.Tax.Rate.ToString("0.##"); TaxInclusiveBox.IsChecked = _store.Data.Tax.Inclusive;
        RoleCombo.SelectedIndex = 0;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var s = _store.Data.Settings;
        var businessEmail = BusinessEmailBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(BusinessNameBox.Text))
        { MessageBox.Show("Enter a business name before saving.", "Business name required"); return; }
        if (businessEmail.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(businessEmail, out _))
        { MessageBox.Show("Enter a valid business email address, or leave the field blank.", "Invalid email"); return; }
        if (!decimal.TryParse(LowStockThresholdBox.Text, out var threshold) || threshold < 0)
        { MessageBox.Show("Enter a valid low-stock threshold."); return; }
        if (CurrencySymbolCombo.SelectedValue is not string currency)
        { MessageBox.Show("Choose the currency used for sales.", "Currency required"); return; }
        var logoPath = CompanyLogoBox.Text.Trim();
        if (logoPath.Length > 0 &&
            (!File.Exists(logoPath) || !new[] { ".png", ".jpg", ".jpeg", ".bmp" }
                .Contains(System.IO.Path.GetExtension(logoPath), StringComparer.OrdinalIgnoreCase)))
        { MessageBox.Show("Choose an existing PNG, JPG or BMP image for the company logo.", "Invalid logo"); return; }
        s.BusinessName = BusinessNameBox.Text.Trim(); s.Email = businessEmail;
        s.Address = BusinessAddressBox.Text.Trim(); s.City = BusinessCityBox.Text.Trim();
        s.Country = BusinessCountryBox.Text.Trim(); s.Phone = BusinessPhoneBox.Text.Trim();
        s.ReceiptFooter = ReceiptFooterBox.Text.Trim(); s.CurrencySymbol = currency;
        s.BusinessType = BusinessTypeCombo.SelectedValue is BusinessType type ? type : BusinessType.Other;
        s.CompanyLogoPath = logoPath; s.LowStockAlertThreshold = threshold; s.LowStockOwnerEmail = OwnerEmailBox.Text.Trim();
        s.Reports.OwnerEmail = s.LowStockOwnerEmail;
        s.EnableSaleNotifications = SaleNotificationsBox.IsChecked == true;
        s.EnableOwnerEmailAlerts = OwnerEmailAlertsBox.IsChecked == true;
        s.Notifications.SaleTimestampNotifications = SaleNotificationsBox.IsChecked == true;
        s.Notifications.LowStockOwnerEmailAlerts = OwnerEmailAlertsBox.IsChecked == true;
        s.Integrations.OnlineSyncEnabled = SyncEnabledBox.IsChecked == true;
        s.Integrations.SyncEndpoint = SyncEndpointBox.Text.Trim(); s.Integrations.SyncApiKey = SyncApiKeyBox.Password.Trim();
        s.Integrations.GoogleDriveFolderId = GoogleDriveFolderBox.Text.Trim();
        s.Integrations.GoogleSheetsSpreadsheetId = GoogleSheetsIdBox.Text.Trim();
        s.Integrations.GoogleSheetsWorksheet = GoogleSheetsTabBox.Text.Trim();
        s.Integrations.WhatsAppWebhookUrl = WhatsAppWebhookBox.Text.Trim();
        s.Integrations.SmsWebhookUrl = SmsWebhookBox.Text.Trim();
        s.Integrations.EmailWebhookUrl = EmailWebhookBox.Text.Trim();
        s.Notifications.WhatsAppWebhookUrl = s.Integrations.WhatsAppWebhookUrl;
        s.Notifications.SmsWebhookUrl = s.Integrations.SmsWebhookUrl;
        s.Notifications.EmailWebhookUrl = s.Integrations.EmailWebhookUrl;
        s.Integrations.SyncStatus = s.Integrations.OnlineSyncEnabled && !string.IsNullOrWhiteSpace(s.Integrations.SyncEndpoint)
            ? IntegrationConnectionStatus.Ready : IntegrationConnectionStatus.Offline;
        s.Integrations.LastSyncMessage = s.Integrations.SyncStatus == IntegrationConnectionStatus.Ready
            ? "Configured; sync is opt-in and sales remain local until a sync is requested." : "Local-only mode; no online integration configured.";
        _store.Save();
        ApplyCurrencyCulture();
        LoadSettings();
        Refresh();
        MessageBox.Show("Workspace settings saved.", "Settings saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ApplyCurrencyCulture()
    {
        var symbol = _store.Data.Settings.CurrencySymbol;
        var cultureName = symbol switch
        {
            "₦" => "en-NG",
            "£" => "en-GB",
            "$" => "en-US",
            "€" => "fr-FR",
            "GH₵" => "en-GH",
            "KSh" => "sw-KE",
            "R" => "en-ZA",
            "C$" => "en-CA",
            _ => CultureInfo.CurrentCulture.Name
        };
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        culture.NumberFormat.CurrencySymbol = symbol;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        Language = XmlLanguage.GetLanguage(culture.Name);
    }

    private static string BusinessTypeDisplayName(BusinessType type) => type switch
    {
        BusinessType.Retail => "General retail",
        BusinessType.BeautySalon => "Beauty & salon",
        BusinessType.Boutique => "Fashion & apparel",
        BusinessType.Electrical => "Electrical",
        BusinessType.ComputerRepair => "Computer services",
        BusinessType.Electronics => "Electronics",
        BusinessType.Pharmacy => "Pharmacy",
        BusinessType.GasPetrolStation => "Fuel & gas",
        BusinessType.Grocery => "Grocery",
        BusinessType.Hospitality => "Cafe & hospitality",
        BusinessType.Service => "Services",
        BusinessType.Fuel => "Fuel services",
        _ => "Other business"
    };

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        SettingsStatusText.Text = "Settings reloaded from local data";
    }

    private void BrowseCompanyLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
            Title = "Choose a company logo"
        };
        if (dialog.ShowDialog(this) == true)
            CompanyLogoBox.Text = dialog.FileName;
    }

    private void OpenTaxSettings_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 7;
        MainPageTitle.Text = "Promotions & VAT";
        HeaderSubtitle.Text = "Manage tax rates and discount promotions";
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        if (AppSession.CurrentUser?.Role is not "SuperAdmin")
        {
            MessageBox.Show("Only a Super Admin can create users.", "Super Admin permission required");
            return;
        }
        if (string.IsNullOrWhiteSpace(UserNameBox.Text)) { MessageBox.Show("Enter a user name."); return; }
        _store.Data.Users.Add(new UserAccount { UserName = UserNameBox.Text.Trim(), Role = (RoleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cashier" });
        UserNameBox.Clear(); _store.Save(); Refresh();
    }

    private void AddWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (string.IsNullOrWhiteSpace(WorkflowNameBox.Text)) { MessageBox.Show("Enter a workflow name."); return; }
        _store.Data.Workflows.Add(new WorkflowScreen { Name = WorkflowNameBox.Text.Trim(), Description = WorkflowDescriptionBox.Text.Trim() });
        WorkflowNameBox.Clear(); WorkflowDescriptionBox.Clear(); _store.Save(); Refresh();
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseSavePath("TILVEX POS JSON|*.json", "tilvex-pos-export.json"); if (path is null) return;
        _store.ExportJson(path); DatabaseStatusText.Text = $"Exported {path}";
    }

    private void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "TILVEX POS JSON|*.json" };
        if (dialog.ShowDialog() != true) return;
        try { _store.ImportJson(dialog.FileName); _integration = new HttpIntegrationService(_store.Data.Settings.Integrations); LoadSettings(); Refresh(); DatabaseStatusText.Text = $"Imported {dialog.FileName}"; }
        catch (Exception ex) { MessageBox.Show($"Import failed: {ex.Message}"); }
    }

    private void BackupData_Click(object sender, RoutedEventArgs e) => MessageBox.Show($"Backup created at:\n{_store.Backup()}", "Backup complete");

    private bool IsLowStock(Product product) =>
        product.StockQuantity <= Math.Max(product.ReorderLevel, _store.Data.Settings.LowStockAlertThreshold);

    private bool RequireAdmin()
    {
        var currentUser = AppSession.CurrentUser;
        var isAdmin = currentUser is not null && _store.Data.Users.Any(u => u.Active &&
            u.Id == currentUser.Id &&
            (u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
             u.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)));
        if (!isAdmin)
            MessageBox.Show("Only an active Admin can change business, integration, tax, user or workflow settings.", "Admin permission required");
        return isAdmin;
    }

    private async Task NotifyLowStockAsync()
    {
        var settings = _store.Data.Settings;
        var lowStock = _store.Data.Products.Where(IsLowStock).ToList();
        if (!settings.Notifications.LowStockOwnerEmailAlerts || lowStock.Count == 0) return;
        var message = $"Low-stock alert for {settings.BusinessName}: " +
                      string.Join(", ", lowStock.Select(p => $"{p.Name} ({p.StockQuantity} {p.UnitLabel})"));
        var result = await _integration.SendNotificationAsync("email", message);
        _store.Data.Notifications.Add(new NotificationRecord
        {
            Type = "LowStock",
            Message = result.Success ? message : $"Low-stock email not sent: {result.Message}",
            Delivered = result.Success
        });
        _store.Data.Settings.Integrations.LastSyncMessage = result.Message;
        _store.Save();
        Dispatcher.Invoke(Refresh);
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var settings = _store.Data.Settings.Integrations;
        settings.SyncStatus = IntegrationConnectionStatus.Syncing;
        settings.LastSyncMessage = "Sync requested; local sales remain available offline.";
        Refresh();
        var result = await _integration.SyncAsync(_store.Data);
        settings.SyncStatus = result.Status; settings.LastSyncMessage = result.Message;
        if (result.Success) settings.LastSyncAt = DateTime.Now;
        _store.Save(); Refresh();
    }

    private void ExportPeriodCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseSavePath("CSV report|*.csv", "sales-report.csv"); if (path is null) return;
        var from = ReportPeriodStart; var to = DateTime.Now.AddSeconds(1);
        ReportExportService.ExportPeriodCsv(_store.Data, from, to, path);
        DatabaseStatusText.Text = $"Report exported: {path}";
    }

    private DateTime ReportPeriodStart => ReportPeriodCombo.SelectedIndex switch
    {
        1 => DateTime.Today.AddDays(-6),
        2 => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
        _ => DateTime.Today
    };

    private void PrintSale(Sale sale)
    {
        try
        {
            var print = new PrintDialog();
            if (print.ShowDialog() != true) return;
            var settings = _store.Data.Settings;
            var document = new FlowDocument { PagePadding = new Thickness(30), FontFamily = new System.Windows.Media.FontFamily("Segoe UI") };
            if (!string.IsNullOrWhiteSpace(settings.CompanyLogoPath))
            {
                var logoSource = new BitmapImage();
                logoSource.BeginInit();
                logoSource.CacheOption = BitmapCacheOption.OnLoad;
                logoSource.UriSource = new Uri(System.IO.Path.GetFullPath(settings.CompanyLogoPath));
                logoSource.EndInit();
                logoSource.Freeze();
                document.Blocks.Add(new BlockUIContainer(new Image
                {
                    Source = logoSource,
                    Height = 48,
                    MaxWidth = 180,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                }) { TextAlignment = TextAlignment.Center });
            }
            document.Blocks.Add(new Paragraph(new Run(settings.BusinessName)) { FontSize = 18, FontWeight = FontWeights.Bold });
            var businessDetails = string.Join(" • ", new[] { settings.Address, settings.City, settings.Country, settings.Phone, settings.Email }
                .Where(detail => !string.IsNullOrWhiteSpace(detail)));
            if (!string.IsNullOrWhiteSpace(businessDetails))
                document.Blocks.Add(new Paragraph(new Run(businessDetails)));
            document.Blocks.Add(new Paragraph(new Run($"Receipt {sale.ReceiptNumber}\n{sale.CreatedAt:g}")));
            foreach (var line in sale.Lines) document.Blocks.Add(new Paragraph(new Run($"{line.Quantity} {line.UnitLabel} x {line.Name}  {line.LineTotal:C}")));
            document.Blocks.Add(new Paragraph(new Run($"Subtotal: {sale.Subtotal:C}\nDiscount: {sale.Discount:C}\nVAT: {sale.Tax:C}\nTOTAL: {sale.Total:C}\n\n{settings.ReceiptFooter}")));
            print.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, sale.ReceiptNumber);
        }
        catch (Exception ex) { MessageBox.Show($"Printing failed: {ex.Message}"); }
    }
}
