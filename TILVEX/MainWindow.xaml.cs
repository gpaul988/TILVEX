using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace TILVEX;

public partial class MainWindow : Window
{
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
        CartGrid.ItemsSource = _cart;
        UnitCombo.ItemsSource = Enum.GetValues<UnitOfMeasure>();
        BusinessTypeCombo.ItemsSource = Enum.GetValues<BusinessType>();
        LoadSettings();
        _integration = new HttpIntegrationService(_store.Data.Settings.Integrations);
        Refresh();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => RefreshDashboardCharts();

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var tabIndex))
            return;

        MainTabs.SelectedIndex = tabIndex;
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
        SaleProductsGrid.ItemsSource = products;
        InventoryGrid.ItemsSource = products;
        SalesHistoryGrid.ItemsSource = _store.Data.Sales.OrderByDescending(s => s.CreatedAt).ToList();
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
        UsersGrid.ItemsSource = _store.Data.Users.OrderBy(u => u.UserName).ToList();
        WorkflowsGrid.ItemsSource = _store.Data.Workflows.OrderBy(w => w.Name).ToList();
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

    private static readonly Color ChartAccent = Color.FromRgb(0x00, 0xD4, 0xAA);
    private static readonly Color ChartAccentDark = Color.FromRgb(0x00, 0x7A, 0x5E);
    private static readonly Color ChartInk = Color.FromRgb(0xE8, 0xED, 0xF3);
    private static readonly Color ChartMuted = Color.FromRgb(0x71, 0x80, 0x96);
    private static readonly Color ChartLine = Color.FromRgb(0x26, 0x30, 0x3D);

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
        var search = ProductSearchBox.Text.Trim();
        SaleProductsGrid.ItemsSource = _store.Data.Products
            .Where(p => string.IsNullOrWhiteSpace(search) ||
                        p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.Barcode.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name).ToList();
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
        var tendered = decimal.TryParse(TenderedBox.Text, out var amount) ? amount : 0;
        var sale = new Sale
        {
            ReceiptNumber = $"GG-{DateTime.Now:yyyyMMdd-HHmmss}",
            CustomerId = (CustomerCombo.SelectedItem as Customer)?.Id,
            Discount = CalculateDiscount(),
            TaxRate = _store.Data.Tax.Rate,
            TaxInclusive = _store.Data.Tax.Inclusive,
            PaymentMethod = (PaymentCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash",
            Tendered = tendered
        };
        sale.Lines.AddRange(_cart.Select(line => new SaleLine { ProductId = line.ProductId, Sku = line.Sku, Name = line.Name, UnitPrice = line.UnitPrice, Quantity = line.Quantity, Unit = line.Unit, UnitLabel = line.UnitLabel }));
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
        _ = NotifySaleAsync(sale);
        _ = NotifyLowStockAsync();
        MessageBox.Show($"Receipt {sale.ReceiptNumber}\n\nTOTAL: {sale.Total:C}\nChange: {sale.Change:C}", "Sale completed");
        PrintSale(sale);
    }

    private decimal CalculateDiscount()
    {
        var subtotal = _cart.Sum(l => l.LineTotal);
        return _selectedPromotion is null ? 0 : Math.Round(subtotal * _selectedPromotion.DiscountPercent / 100m, 2);
    }

    private void PromotionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPromotion = PromotionCombo.SelectedItem as Promotion;
        UpdateCartTotal();
    }

    private void TenderedBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCartTotal();

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
        var tendered = decimal.TryParse(TenderedBox.Text, out var t) ? t : 0;
        ChangeText.Text = Math.Max(0, tendered - total).ToString("C");
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
        var product = _selectedProduct ?? new Product();
        product.Sku = SkuBox.Text.Trim(); product.Barcode = string.IsNullOrWhiteSpace(BarcodeProductBox.Text) ? product.Sku : BarcodeProductBox.Text.Trim();
        product.Name = NameBox.Text.Trim(); product.Category = CategoryBox.Text.Trim();
        product.Unit = UnitCombo.SelectedItem is UnitOfMeasure unit ? unit : UnitOfMeasure.Each;
        product.UnitLabel = string.IsNullOrWhiteSpace(UnitLabelBox.Text) ? product.Unit.ToString() : UnitLabelBox.Text.Trim();
        product.Price = price; product.StockQuantity = stock; product.ReorderLevel = reorder;
        if (_selectedProduct is null) _store.Data.Products.Add(product);
        _store.Save(); Refresh(); InventoryGrid.SelectedItem = product;
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
        _draftPurchase ??= new PurchaseOrder { Number = $"PO-{DateTime.Now:yyyyMMdd-HHmmss}", SupplierId = supplier.Id };
        _draftPurchase.Lines.Add(new PurchaseOrderLine { ProductId = product.Id, ProductName = product.Name, Quantity = qty, UnitCost = cost, Unit = product.Unit });
        if (!_store.Data.PurchaseOrders.Contains(_draftPurchase)) _store.Data.PurchaseOrders.Add(_draftPurchase);
        _store.Save(); Refresh();
    }

    private void ReceivePurchase_Click(object sender, RoutedEventArgs e)
    {
        var order = _store.Data.PurchaseOrders.FirstOrDefault(p => p.Status == "Draft");
        if (order is null) { MessageBox.Show("Add a purchase order line first."); return; }
        foreach (var line in order.Lines)
        {
            var product = _store.Data.Products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product is not null) product.StockQuantity += line.Quantity;
        }
        order.Status = "Received"; _draftPurchase = null; _store.Save(); Refresh();
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
        _store.Data.Returns.Add(new ReturnRecord { ReceiptNumber = sale.ReceiptNumber, Reason = ReturnReasonBox.Text.Trim(), Amount = sale.Total });
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
        if (_store.Data.TillSessions.Any(t => !t.ClosedAt.HasValue)) { MessageBox.Show("The till is already open."); return; }
        if (!decimal.TryParse(OpeningFloatBox.Text, out var opening) || opening < 0) { MessageBox.Show("Enter a valid opening float."); return; }
        _store.Data.TillSessions.Add(new TillSession { OpeningFloat = opening }); _store.Save(); Refresh();
    }

    private void CloseTill_Click(object sender, RoutedEventArgs e)
    {
        var till = _store.Data.TillSessions.LastOrDefault(t => !t.ClosedAt.HasValue);
        if (till is null) { MessageBox.Show("Open the till first."); return; }
        if (!decimal.TryParse(CountedCashBox.Text, out var counted) || counted < 0) { MessageBox.Show("Enter counted cash."); return; }
        till.ExpectedCash = till.OpeningFloat + _store.Data.Sales.Where(s => s.CreatedAt >= till.OpenedAt && s.PaymentMethod == "Cash").Sum(s => s.Total) - _store.Data.Returns.Where(r => r.CreatedAt >= till.OpenedAt).Sum(r => r.Amount);
        till.CountedCash = counted; till.ClosedAt = DateTime.Now; _store.Save(); Refresh();
        MessageBox.Show($"Till closed. Variance: {till.Variance:C}");
    }

    private void RefreshReport_Click(object sender, RoutedEventArgs e)
    {
        RefreshReport();
    }

    private void RefreshReport()
    {
        var sales = _store.Data.Sales.Where(s => s.CreatedAt.Date == DateTime.Today).ToList();
        var top = _store.Data.Sales.SelectMany(s => s.Lines).GroupBy(l => l.Name).OrderByDescending(g => g.Sum(l => l.Quantity)).Take(5).Select(g => $"{g.Key} ({g.Sum(l => l.Quantity)})");
        ReportSummaryText.Text = $"Today: {sales.Count} transactions, {sales.Sum(s => s.Total):C} sales, {sales.Sum(s => s.Tax):C} VAT.\n" +
                                 $"All time: {_store.Data.Sales.Count} transactions, {_store.Data.Sales.Sum(s => s.Total):C} sales, {_store.Data.Returns.Sum(r => r.Amount):C} refunds.\n" +
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

    private static string Csv(object? value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
    private static string CsvRow(params object?[] values) => string.Join(",", values.Select(Csv));
    private string CustomerName(Guid? id) => _store.Data.Customers.FirstOrDefault(c => c.Id == id)?.Name ?? "";
    private static string? ChooseSavePath(string filter, string name) { var dialog = new SaveFileDialog { Filter = filter, FileName = name }; return dialog.ShowDialog() == true ? dialog.FileName : null; }

    private void LoadSettings()
    {
        var settings = _store.Data.Settings; HeaderSubtitle.Text = $"{settings.BusinessName} • {settings.BusinessType} • Offline-first operations";
        BusinessNameBox.Text = settings.BusinessName; BusinessAddressBox.Text = settings.Address; BusinessPhoneBox.Text = settings.Phone; ReceiptFooterBox.Text = settings.ReceiptFooter;
        BusinessTypeCombo.SelectedItem = settings.BusinessType;
        CompanyLogoBox.Text = settings.CompanyLogoPath;
        LowStockThresholdBox.Text = settings.LowStockAlertThreshold.ToString("0.##");
        OwnerEmailBox.Text = string.IsNullOrWhiteSpace(settings.LowStockOwnerEmail) ? settings.Reports.OwnerEmail : settings.LowStockOwnerEmail;
        SaleNotificationsBox.IsChecked = settings.Notifications.SaleTimestampNotifications;
        OwnerEmailAlertsBox.IsChecked = settings.Notifications.LowStockOwnerEmailAlerts;
        SyncEnabledBox.IsChecked = settings.Integrations.OnlineSyncEnabled;
        SyncEndpointBox.Text = settings.Integrations.SyncEndpoint;
        SyncApiKeyBox.Text = settings.Integrations.SyncApiKey;
        GoogleDriveFolderBox.Text = settings.Integrations.GoogleDriveFolderId;
        GoogleSheetsIdBox.Text = settings.Integrations.GoogleSheetsSpreadsheetId;
        GoogleSheetsTabBox.Text = settings.Integrations.GoogleSheetsWorksheet;
        WhatsAppWebhookBox.Text = settings.Integrations.WhatsAppWebhookUrl;
        SmsWebhookBox.Text = settings.Integrations.SmsWebhookUrl;
        EmailWebhookBox.Text = settings.Integrations.EmailWebhookUrl;
        if (DatabaseStatusText is not null) DatabaseStatusText.Text = $"Local JSON: {_store.DataPath}";
        TaxNameBox.Text = _store.Data.Tax.Name; TaxRateBox.Text = _store.Data.Tax.Rate.ToString("0.##"); TaxInclusiveBox.IsChecked = _store.Data.Tax.Inclusive;
        RoleCombo.SelectedIndex = 0;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var s = _store.Data.Settings;
        if (!decimal.TryParse(LowStockThresholdBox.Text, out var threshold) || threshold < 0)
        { MessageBox.Show("Enter a valid low-stock threshold."); return; }
        s.BusinessName = BusinessNameBox.Text.Trim(); s.Address = BusinessAddressBox.Text.Trim(); s.Phone = BusinessPhoneBox.Text.Trim(); s.ReceiptFooter = ReceiptFooterBox.Text.Trim();
        s.BusinessType = BusinessTypeCombo.SelectedItem is BusinessType type ? type : BusinessType.Other;
        s.CompanyLogoPath = CompanyLogoBox.Text.Trim(); s.LowStockAlertThreshold = threshold; s.LowStockOwnerEmail = OwnerEmailBox.Text.Trim();
        s.Reports.OwnerEmail = s.LowStockOwnerEmail;
        s.EnableSaleNotifications = SaleNotificationsBox.IsChecked == true;
        s.EnableOwnerEmailAlerts = OwnerEmailAlertsBox.IsChecked == true;
        s.Notifications.SaleTimestampNotifications = SaleNotificationsBox.IsChecked == true;
        s.Notifications.LowStockOwnerEmailAlerts = OwnerEmailAlertsBox.IsChecked == true;
        s.Integrations.OnlineSyncEnabled = SyncEnabledBox.IsChecked == true;
        s.Integrations.SyncEndpoint = SyncEndpointBox.Text.Trim(); s.Integrations.SyncApiKey = SyncApiKeyBox.Text.Trim();
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
        _store.Save(); LoadSettings(); MessageBox.Show("Business settings saved.");
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
        var currentUser = Environment.UserName;
        var isAdmin = _store.Data.Users.Any(u => u.Active &&
            u.UserName.Equals(currentUser, StringComparison.OrdinalIgnoreCase) &&
            (u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
             u.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)));
        if (!isAdmin)
            MessageBox.Show("Only an active Admin can change business, integration, tax, user or workflow settings.", "Admin permission required");
        return isAdmin;
    }

    private async Task NotifySaleAsync(Sale sale)
    {
        var settings = _store.Data.Settings;
        if (!settings.Notifications.SaleTimestampNotifications) return;
        var result = await _integration.SendNotificationAsync("email",
            $"Sale {sale.ReceiptNumber} at {sale.CreatedAt:O}: {sale.Total.ToString("C")}");
        var notification = _store.Data.Notifications.LastOrDefault(n => n.Type == "Sale" && n.Message.Contains(sale.ReceiptNumber));
        if (notification is not null) notification.Delivered = result.Success;
        settings.Integrations.LastSyncMessage = result.Message;
        _store.Save();
        Dispatcher.Invoke(Refresh);
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
        var from = DateTime.Today.AddDays(-ReportPeriodDays); var to = DateTime.Now.AddSeconds(1);
        ReportExportService.ExportPeriodCsv(_store.Data, from, to, path);
        DatabaseStatusText.Text = $"Report exported: {path}";
    }

    private int ReportPeriodDays => ReportPeriodCombo.SelectedIndex switch { 1 => 7, 2 => 30, _ => 1 };

    private void PrintSale(Sale sale)
    {
        try
        {
            var print = new PrintDialog();
            if (print.ShowDialog() != true) return;
            var document = new FlowDocument { PagePadding = new Thickness(30), FontFamily = new System.Windows.Media.FontFamily("Segoe UI") };
            document.Blocks.Add(new Paragraph(new Run(_store.Data.Settings.BusinessName)) { FontSize = 18, FontWeight = FontWeights.Bold });
            document.Blocks.Add(new Paragraph(new Run($"Receipt {sale.ReceiptNumber}\n{sale.CreatedAt:g}")));
            foreach (var line in sale.Lines) document.Blocks.Add(new Paragraph(new Run($"{line.Quantity} {line.UnitLabel} x {line.Name}  {line.LineTotal:C}")));
            document.Blocks.Add(new Paragraph(new Run($"Subtotal: {sale.Subtotal:C}\nDiscount: {sale.Discount:C}\nVAT: {sale.Tax:C}\nTOTAL: {sale.Total:C}\n\n{_store.Data.Settings.ReceiptFooter}")));
            print.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, sale.ReceiptNumber);
        }
        catch (Exception ex) { MessageBox.Show($"Printing failed: {ex.Message}"); }
    }
}
