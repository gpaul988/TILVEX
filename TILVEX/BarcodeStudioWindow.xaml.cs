using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using ZXing.Common;
using BarcodeFormat = ZXing.BarcodeFormat;
using BarcodeWriterPixelData = ZXing.BarcodeWriterPixelData;
using WriterException = ZXing.WriterException;

namespace TILVEX;

public partial class BarcodeStudioWindow : Window
{
    private sealed record LabelDimensions(double WidthMm, double HeightMm);
    private sealed record LabelStyle(double PaddingDip, double FontSizeDip, int TextLimit);
    private sealed record SheetLayout(double MarginMm, double GapMm);
    private readonly PosStore _store;
    private readonly List<Product> _products;
    private bool _updatingFields;

    public BarcodeStudioWindow(PosStore store)
    {
        InitializeComponent();
        _store = store;
        _products = store.Data.Products.OrderBy(product => product.Name).ToList();
        ProductsList.ItemsSource = _products;
        ProductCountText.Text = $"{_products.Count} products available";
        FormatCombo.SelectedIndex = 0;
        SizeCombo.SelectedIndex = 1;
        if (_products.Count > 0)
            ProductsList.SelectedIndex = 0;
        else
            RefreshPreview();
    }

    private Product? SelectedProduct => SelectedProducts.FirstOrDefault();
    private IReadOnlyList<Product> SelectedProducts => ProductsList.SelectedItems.Cast<Product>().ToList();

    private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ProductsList is null)
            return;

        var query = ProductSearchBox.Text.Trim();
        var filtered = _products.Where(product =>
            (product.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (product.Sku ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (product.Barcode ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        ProductsList.ItemsSource = filtered;
        ProductCountText.Text = $"{filtered.Count} of {_products.Count} products";
    }

    private void ProductsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = SelectedProducts;
        SelectionSummaryText.Text = selected.Count switch
        {
            0 => "Choose a product to preview its label",
            1 => selected[0].Name,
            _ => $"{selected.Count} products selected for batch output"
        };

        if (selected.Count > 0)
            SetProductFields(selected[0]);
        else
            RefreshPreview();
    }

    private void SetProductFields(Product product)
    {
        _updatingFields = true;
        LabelNameBox.Text = product.Name ?? "";
        LabelDetailBox.Text = product.Sku ?? "";
        CodeBox.Text = string.IsNullOrWhiteSpace(product.Barcode) ? product.Sku ?? "" : product.Barcode;
        _updatingFields = false;
        RefreshPreview();
    }

    private void LabelSetting_Changed(object sender, RoutedEventArgs e) => RefreshPreview();

    private void LabelText_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFields)
            RefreshPreview();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewCanvas is null)
            return;

        PreviewCanvas.LayoutTransform = new ScaleTransform(e.NewValue, e.NewValue);
    }

    private void GenerateCode_Click(object sender, RoutedEventArgs e)
    {
        var product = SelectedProduct;
        if (product is null)
        {
            ShowStatus("Select a product before generating a code.", true);
            return;
        }

        if (IsNumericFormat())
        {
            var value = CodeBox.Text.Trim();
            var bodyLength = GetFormat() == "EAN-13" ? 12 : 11;
            if (value.Length == bodyLength + 1 && HasValidCheckDigit(value, bodyLength + 1))
            {
                ShowStatus("This GS1-assigned code already has a valid check digit.");
                return;
            }
            if (value.Length != bodyLength || !value.All(char.IsAsciiDigit))
            {
                ShowStatus($"Enter the {bodyLength}-digit number assigned to your business by GS1, then calculate its check digit.", true);
                return;
            }

            CodeBox.Text = value + CalculateCheckDigit(value, GetFormat() == "EAN-13");
            ShowStatus("Check digit added. Save this GS1-assigned barcode to the product.");
            return;
        }

        CodeBox.Text = CreateValidCode(product);
        ShowStatus("A new scanner-ready code was generated. Save it to attach it to this product.");
    }

    private void SaveCode_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedProducts;
        if (selected.Count == 0)
        {
            ShowStatus("Select at least one product to save barcode values.", true);
            return;
        }

        var selectedIds = selected.Select(product => product.Id).ToHashSet();
        var usedCodes = _products.Where(product => !selectedIds.Contains(product.Id))
            .SelectMany(product => new[] { product.Barcode, product.Sku })
            .Concat(selected.Skip(1).Select(product => product.Sku))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var firstCode = CodeBox.Text.Trim();
        if (!IsValidCode(firstCode, out var error))
        {
            ShowStatus(error, true);
            return;
        }

        if (usedCodes.Contains(firstCode))
        {
            ShowStatus("That barcode is already assigned to another product.", true);
            return;
        }

        var updates = new List<(Product Product, string Code)>();
        try
        {
            for (var index = 0; index < selected.Count; index++)
            {
                var product = selected[index];
                var value = index == 0 ? firstCode : product.Barcode;
                if (index > 0 && (!IsValidCode(value, out _) || usedCodes.Contains(value)))
                    value = CreateValidCode(product, usedCodes);
                updates.Add((product, value));
                usedCodes.Add(value);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            ShowStatus(exception.Message, true);
            return;
        }

        foreach (var update in updates)
            update.Product.Barcode = update.Code;
        _store.Save();
        ProductsList.Items.Refresh();
        ShowStatus($"Saved barcode values to {selected.Count} product(s).");
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var product = SelectedProduct;
        if (product is null)
        {
            ShowStatus("Select a product to export its label.", true);
            return;
        }

        if (!TryGetBarcode(product, true, out var barcode, out var error))
        {
            ShowStatus(error, true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export barcode label",
            Filter = "PNG image (*.png)|*.png",
            FileName = SafeFileName(product.Name) + "-label.png",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var dimensions = GetDimensions();
            var label = BuildLabel(product, barcode, dimensions, LabelNameBox.Text.Trim(), LabelDetailBox.Text.Trim());
            File.WriteAllBytes(dialog.FileName, RenderLabelPng(label, 300));
            ShowStatus($"300 DPI label exported to {dialog.FileName}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or ArgumentException or WriterException)
        {
            ShowStatus($"Could not export the label: {exception.Message}", true);
        }
    }

    private void PrintLabels_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildBatch(out var batch, out var selected, out var error))
        {
            ShowStatus(error, true);
            return;
        }

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var dimensions = GetDimensions();
            var pageWidth = dialog.PrintableAreaWidth;
            var pageHeight = dialog.PrintableAreaHeight;
            if (pageWidth <= 0 || pageHeight <= 0)
            {
                ShowStatus("The selected printer did not report a printable page size.", true);
                return;
            }

            var labelWidth = dimensions.WidthMm * 96 / 25.4;
            var labelHeight = dimensions.HeightMm * 96 / 25.4;
            var sheetLayout = GetSheetLayout();
            var margin = sheetLayout.MarginMm * 96 / 25.4;
            var gap = sheetLayout.GapMm * 96 / 25.4;
            var columns = (int)Math.Floor((pageWidth - margin * 2 + gap) / (labelWidth + gap));
            var rows = (int)Math.Floor((pageHeight - margin * 2 + gap) / (labelHeight + gap));
            if (columns < 1 || rows < 1)
            {
                ShowStatus("The selected label size does not fit the printer's printable area.", true);
                return;
            }

            var labelsPerPage = columns * rows;
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
            for (var pageIndex = 0; pageIndex * labelsPerPage < batch.Count; pageIndex++)
            {
                var page = new FixedPage { Width = pageWidth, Height = pageHeight };
                var firstLabelIndex = pageIndex * labelsPerPage;
                var lastLabelIndex = Math.Min(batch.Count, firstLabelIndex + labelsPerPage);
                for (var labelIndex = firstLabelIndex; labelIndex < lastLabelIndex; labelIndex++)
                {
                    var item = batch[labelIndex];
                    var isEditorProduct = item.Product.Id == selected[0].Id;
                    var label = BuildLabel(item.Product, item.Barcode, dimensions,
                        isEditorProduct ? LabelNameBox.Text.Trim() : item.Product.Name,
                        isEditorProduct ? LabelDetailBox.Text.Trim() : item.Product.Sku);
                    var cellIndex = labelIndex - firstLabelIndex;
                    var column = cellIndex % columns;
                    var row = cellIndex / columns;
                    FixedPage.SetLeft(label, margin + column * (labelWidth + gap));
                    FixedPage.SetTop(label, margin + row * (labelHeight + gap));
                    page.Children.Add(label);
                }

                page.Measure(new Size(pageWidth, pageHeight));
                page.Arrange(new Rect(0, 0, pageWidth, pageHeight));
                var content = new PageContent();
                ((System.Windows.Markup.IAddChild)content).AddChild(page);
                document.Pages.Add(content);
            }

            dialog.PrintDocument(document.DocumentPaginator, "TILVEX barcode labels");
            ShowStatus($"Sent {batch.Count} label(s) across {document.Pages.Count} sheet(s) to the selected printer.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException or WriterException)
        {
            ShowStatus($"Could not print the labels: {exception.Message}", true);
        }
    }

    private void CopyLabel_Click(object sender, RoutedEventArgs e)
    {
        var product = SelectedProduct;
        if (product is null)
        {
            ShowStatus("Select a product before copying its label.", true);
            return;
        }
        if (!TryGetBarcode(product, true, out var barcode, out var error))
        {
            ShowStatus(error, true);
            return;
        }

        try
        {
            var label = BuildLabel(product, barcode, GetDimensions(), LabelNameBox.Text.Trim(), LabelDetailBox.Text.Trim());
            Clipboard.SetImage(RenderLabelBitmap(label, 144));
            ShowStatus("Label image copied to the clipboard.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException or WriterException or System.Runtime.InteropServices.ExternalException)
        {
            ShowStatus($"Could not copy the label: {exception.Message}", true);
        }
    }

    private void PdfPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildBatch(out var batch, out _, out var error))
        {
            ShowStatus(error, true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export and preview barcode labels",
            Filter = "PDF document (*.pdf)|*.pdf",
            FileName = "TILVEX-barcode-labels.pdf",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var dimensions = GetDimensions();
            var sheetLayout = GetSheetLayout();
            var labelWidth = dimensions.WidthMm * 72 / 25.4;
            var labelHeight = dimensions.HeightMm * 72 / 25.4;
            var margin = sheetLayout.MarginMm * 72 / 25.4;
            var gap = sheetLayout.GapMm * 72 / 25.4;
            var pageWidth = 210 * 72 / 25.4;
            var pageHeight = 297 * 72 / 25.4;
            var columns = (int)Math.Floor((pageWidth - margin * 2 + gap) / (labelWidth + gap));
            var rows = (int)Math.Floor((pageHeight - margin * 2 + gap) / (labelHeight + gap));
            if (columns < 1 || rows < 1)
            {
                ShowStatus("The selected label size does not fit on an A4 page.", true);
                return;
            }

            var labelsPerPage = columns * rows;
            using var document = new PdfDocument();
            var imageCache = new Dictionary<Guid, XImage>();
            var imageStreams = new List<MemoryStream>();
            try
            {
                for (var pageIndex = 0; pageIndex * labelsPerPage < batch.Count; pageIndex++)
                {
                    var page = document.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    using var graphics = XGraphics.FromPdfPage(page);
                    var firstLabelIndex = pageIndex * labelsPerPage;
                    var lastLabelIndex = Math.Min(batch.Count, firstLabelIndex + labelsPerPage);
                    for (var labelIndex = firstLabelIndex; labelIndex < lastLabelIndex; labelIndex++)
                    {
                        var item = batch[labelIndex];
                        if (!imageCache.TryGetValue(item.Product.Id, out var image))
                        {
                            var isEditorProduct = item.Product.Id == SelectedProduct?.Id;
                            var label = BuildLabel(item.Product, item.Barcode, dimensions,
                                isEditorProduct ? LabelNameBox.Text.Trim() : item.Product.Name,
                                isEditorProduct ? LabelDetailBox.Text.Trim() : item.Product.Sku);
                            var stream = new MemoryStream(RenderLabelPng(label, 300));
                            imageStreams.Add(stream);
                            image = XImage.FromStream(stream);
                            imageCache.Add(item.Product.Id, image);
                        }

                        var cellIndex = labelIndex - firstLabelIndex;
                        var x = margin + (cellIndex % columns) * (labelWidth + gap);
                        var y = margin + (cellIndex / columns) * (labelHeight + gap);
                        graphics.DrawImage(image, x, y, labelWidth, labelHeight);
                    }
                }

                document.Save(dialog.FileName);
            }
            finally
            {
                foreach (var image in imageCache.Values)
                    image.Dispose();
                foreach (var stream in imageStreams)
                    stream.Dispose();
            }

            try
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                ShowStatus($"Created and opened the {batch.Count}-label PDF preview.");
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                ShowStatus($"PDF saved to {dialog.FileName}, but no PDF preview app could be opened: {exception.Message}", true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or FormatException or WriterException or PdfSharp.PdfSharpException)
        {
            ShowStatus($"Could not create the PDF labels: {exception.Message}", true);
        }
    }

    private bool TryBuildBatch(out List<(Product Product, string Barcode)> batch, out IReadOnlyList<Product> selected, out string error)
    {
        batch = [];
        selected = SelectedProducts;
        error = "";
        if (selected.Count == 0)
        {
            error = "Select at least one product to create labels.";
            return false;
        }
        if (!int.TryParse(QuantityBox.Text, out var copies) || copies is < 1 or > 500)
        {
            error = "Copies must be a whole number between 1 and 500.";
            return false;
        }
        if ((long)selected.Count * copies > 500)
        {
            error = "A label batch is limited to 500 labels. Reduce copies or select fewer products.";
            return false;
        }
        try
        {
            _ = GetLabelStyle();
            _ = GetSheetLayout();
        }
        catch (FormatException exception)
        {
            error = exception.Message;
            return false;
        }

        for (var index = 0; index < selected.Count; index++)
        {
            var product = selected[index];
            if (!TryGetBarcode(product, index == 0, out var barcode, out error))
            {
                error = $"{product.Name}: {error}";
                return false;
            }
            for (var copy = 0; copy < copies; copy++)
                batch.Add((product, barcode));
        }
        return true;
    }

    private static RenderTargetBitmap RenderLabelBitmap(Canvas label, int dpi)
    {
        label.Measure(new Size(label.Width, label.Height));
        label.Arrange(new Rect(0, 0, label.Width, label.Height));
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(label.Width * dpi / 96));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(label.Height * dpi / 96));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(label);
        return bitmap;
    }

    private static byte[] RenderLabelPng(Canvas label, int dpi)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderLabelBitmap(label, dpi)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void RefreshPreview()
    {
        if (PreviewCanvas is null)
            return;

        PreviewCanvas.Children.Clear();
        var dimensions = GetDimensions();
        var width = dimensions.WidthMm * 96 / 25.4;
        var height = dimensions.HeightMm * 96 / 25.4;
        PreviewCanvas.Width = width;
        PreviewCanvas.Height = height;
        PreviewSizeText.Text = $"{dimensions.WidthMm:0} × {dimensions.HeightMm:0} mm";

        var product = SelectedProduct;
        if (product is null)
        {
            PreviewStatusText.Text = _products.Count == 0
                ? "Add products in Product Management to create labels."
                : "Select a product to preview its label.";
            return;
        }

        if (!TryGetBarcode(product, true, out var barcode, out var error))
        {
            PreviewStatusText.Text = error;
            return;
        }

        try
        {
            var label = BuildLabel(product, barcode, dimensions);
            PreviewCanvas.Children.Add(label);
            label.Measure(new Size(label.Width, label.Height));
            label.Arrange(new Rect(0, 0, label.Width, label.Height));
            PreviewStatusText.Text = $"{GetFormat()} • Ready to export or print";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or WriterException)
        {
            PreviewStatusText.Text = $"Barcode preview failed: {exception.Message}";
        }
    }

    private Canvas BuildLabel(Product product, string barcode, LabelDimensions dimensions, string? name = null, string? detailText = null)
    {
        var width = dimensions.WidthMm * 96 / 25.4;
        var height = dimensions.HeightMm * 96 / 25.4;
        var canvas = new Canvas { Width = width, Height = height, Background = Brushes.White };
        var border = new Border
        {
            Width = width,
            Height = height,
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 226, 235)),
            BorderThickness = new Thickness(0.7),
            Background = Brushes.White
        };
        canvas.Children.Add(border);

        var style = GetLabelStyle();
        var margin = style.PaddingDip;
        var title = LimitText(string.IsNullOrWhiteSpace(name) ? product.Name : name, style.TextLimit);
        AddLabelText(canvas, title, margin, margin / 2, width - margin * 2, style.FontSizeDip, FontWeights.SemiBold);

        var isQrCode = GetFormat() == "QR Code";
        var barcodePixels = Math.Max(180, (int)(width * 5));
        var bitmap = CreateBarcodeBitmap(barcode, barcodePixels,
            isQrCode ? barcodePixels : Math.Max(80, (int)(height * 2.8)));
        var barcodeTop = Math.Max(height * 0.25, margin + style.FontSizeDip + 3);
        var codeTop = height - margin - 10;
        var priceTop = codeTop - Math.Max(9, style.FontSizeDip + 2);
        var maximumBarcodeHeight = Math.Max(12, priceTop - barcodeTop - 4);
        var barcodeWidth = isQrCode
            ? Math.Min(Math.Min(height * 0.53, width * 0.28), maximumBarcodeHeight)
            : width - margin * 2;
        var barcodeHeight = isQrCode ? barcodeWidth : Math.Min(height * 0.43, maximumBarcodeHeight);
        var barcodeImage = new Image
        {
            Source = bitmap,
            Width = barcodeWidth,
            Height = barcodeHeight,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        RenderOptions.SetBitmapScalingMode(barcodeImage, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(barcodeImage, (width - barcodeWidth) / 2);
        Canvas.SetTop(barcodeImage, barcodeTop);
        canvas.Children.Add(barcodeImage);

        var detail = LimitText(detailText ?? LabelDetailBox.Text.Trim(), style.TextLimit);
        var price = ShowPriceCheck.IsChecked == true
            ? $"{detail}{(detail.Length > 0 ? "   •   " : "")}{product.Price.ToString("C", System.Globalization.CultureInfo.CurrentCulture)}"
            : detail;
        AddLabelText(canvas, price, margin, priceTop,
            width - margin * 2, Math.Max(7, style.FontSizeDip - 1), FontWeights.Medium);
        AddLabelText(canvas, barcode, margin, codeTop,
            width - margin * 2, 7, FontWeights.Normal, TextAlignment.Center);
        return canvas;
    }

    private LabelStyle GetLabelStyle()
    {
        if (!double.TryParse(PaddingBox.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var paddingMm) ||
            paddingMm is < 0.5 or > 5)
            throw new FormatException("Label padding must be between 0.5 and 5 mm.");
        if (!double.TryParse(FontSizeBox.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var fontSizePt) ||
            fontSizePt is < 6 or > 14)
            throw new FormatException("Font size must be between 6 and 14 pt.");
        if (!int.TryParse(TextLimitBox.Text, out var textLimit) || textLimit is < 8 or > 60)
            throw new FormatException("Text limit must be a whole number between 8 and 60 characters.");

        return new LabelStyle(paddingMm * 96 / 25.4, fontSizePt * 96 / 72, textLimit);
    }

    private SheetLayout GetSheetLayout()
    {
        if (!double.TryParse(SheetGapBox.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var gapMm) ||
            gapMm is < 0 or > 10)
            throw new FormatException("Sheet gap must be between 0 and 10 mm.");
        if (!double.TryParse(PageMarginBox.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var marginMm) ||
            marginMm is < 0 or > 20)
            throw new FormatException("Page margin must be between 0 and 20 mm.");

        return new SheetLayout(marginMm, gapMm);
    }

    private static string LimitText(string? text, int limit)
    {
        var safeText = text ?? "";
        return safeText.Length <= limit ? safeText : safeText[..limit];
    }

    private static void AddLabelText(Canvas canvas, string text, double left, double top, double width, double fontSize,
        FontWeight weight, TextAlignment alignment = TextAlignment.Left)
    {
        var block = new TextBlock
        {
            Text = text,
            Width = width,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = Brushes.Black,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = alignment,
            LineHeight = fontSize + 1
        };
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, top);
        canvas.Children.Add(block);
    }

    private BitmapSource CreateBarcodeBitmap(string value, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = GetZXingFormat(),
            Options = new EncodingOptions { Width = width, Height = height, Margin = 6, PureBarcode = true }
        };
        var pixelData = writer.Write(value);
        var bitmap = new WriteableBitmap(pixelData.Width, pixelData.Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, pixelData.Width, pixelData.Height), pixelData.Pixels, pixelData.Width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private BarcodeFormat GetZXingFormat() => GetFormat() switch
    {
        "EAN-13" => BarcodeFormat.EAN_13,
        "UPC-A" => BarcodeFormat.UPC_A,
        "QR Code" => BarcodeFormat.QR_CODE,
        _ => BarcodeFormat.CODE_128
    };

    private bool TryGetBarcode(Product product, bool useEditorCode, out string value, out string error)
    {
        value = useEditorCode ? CodeBox.Text.Trim() : (product.Barcode ?? "").Trim();
        var format = GetFormat();
        if (!useEditorCode && string.IsNullOrWhiteSpace(value))
        {
            value = format switch
            {
                "Code 128" => product.Sku ?? "",
                "QR Code" => string.IsNullOrWhiteSpace(product.Sku) ? "" : $"TILVEX:{product.Sku}",
                _ => ""
            };
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "No code is assigned. Generate and save codes to all selected products before batch printing.";
            return false;
        }

        if (!IsValidCode(value, out error))
            return false;

        var barcodeValue = value;
        var matches = _products.Any(candidate => candidate.Id != product.Id &&
            (string.Equals(candidate.Barcode, barcodeValue, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(candidate.Sku, barcodeValue, StringComparison.OrdinalIgnoreCase)));
        if (matches)
        {
            error = "This code is already assigned to another product.";
            return false;
        }
        return true;
    }

    private bool IsValidCode(string value, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Enter or generate a barcode value.";
            return false;
        }

        var format = GetFormat();
        if (format == "EAN-13" && (value.Length != 13 || !value.All(char.IsAsciiDigit) || !HasValidCheckDigit(value, 13)))
            error = "EAN-13 requires 13 digits with a valid check digit. Use Generate valid code.";
        else if (format == "UPC-A" && (value.Length != 12 || !value.All(char.IsAsciiDigit) || !HasValidCheckDigit(value, 12)))
            error = "UPC-A requires 12 digits with a valid check digit. Use Generate valid code.";
        else if (format == "Code 128" &&
                 (value.Length > 48 || value.Any(character => character < 32 || character > 126)))
            error = "Code 128 accepts up to 48 printable ASCII characters.";
        return error.Length == 0;
    }

    private string CreateValidCode(Product product, HashSet<string>? reservedCodes = null)
    {
        reservedCodes ??= _products.Where(existing => existing.Id != product.Id)
            .SelectMany(existing => new[] { existing.Barcode, existing.Sku })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var format = GetFormat();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            string candidate;
            if (format == "Code 128")
                candidate = $"TVX-{RandomNumberGenerator.GetInt32(0, 100_000_000):D8}";
            else if (format == "QR Code")
                candidate = $"TILVEX:{product.Sku}:{RandomNumberGenerator.GetInt32(0, 100_000_000):D8}";
            else
                throw new FormatException($"{product.Name} needs a GS1-assigned {format} code before batch saving. Enter its number and calculate its check digit.");

            if (!reservedCodes.Contains(candidate))
                return candidate;
        }
        throw new InvalidOperationException("Could not create a unique barcode. Try again.");
    }

    private static bool HasValidCheckDigit(string value, int lengthWithCheckDigit)
    {
        if (value.Length != lengthWithCheckDigit || !value.All(char.IsAsciiDigit))
            return false;

        var body = value[..^1];
        var expected = CalculateCheckDigit(body, lengthWithCheckDigit == 13);
        return value[^1] - '0' == expected;
    }

    private static int CalculateCheckDigit(string digits, bool ean13)
    {
        var sum = 0;
        for (var index = 0; index < digits.Length; index++)
        {
            var positionFromRight = digits.Length - index;
            var weight = ean13
                ? positionFromRight % 2 == 0 ? 1 : 3
                : positionFromRight % 2 == 1 ? 3 : 1;
            sum += (digits[index] - '0') * weight;
        }
        return (10 - sum % 10) % 10;
    }

    private string GetFormat() => (FormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Code 128";
    private bool IsNumericFormat() => GetFormat() is "EAN-13" or "UPC-A";

    private LabelDimensions GetDimensions() => (SizeCombo.SelectedIndex) switch
    {
        0 => new LabelDimensions(50, 30),
        2 => new LabelDimensions(100, 50),
        _ => new LabelDimensions(60, 40)
    };

    private void ShowStatus(string message, bool isError = false)
    {
        PreviewStatusText.Text = message;
        PreviewStatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(185, 56, 56))
            : new SolidColorBrush(Color.FromRgb(8, 121, 86));
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        return cleaned.Length == 0 ? "product" : cleaned;
    }
}
