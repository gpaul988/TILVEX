using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace TILVEX;

public partial class SetupWizard : Window
{
    private readonly PosStore _store;
    private readonly Dictionary<BusinessType, Button> _typeButtons = [];
    private BusinessType _selectedType;
    private bool _showingDetails;

    private static readonly (string Name, string Symbol)[] Currencies =
    [
        ("Nigerian Naira", "₦"),
        ("British Pound", "£"),
        ("US Dollar", "$"),
        ("Euro", "€"),
        ("Ghanaian Cedi", "GH₵"),
        ("Kenyan Shilling", "KSh"),
        ("South African Rand", "R"),
        ("Canadian Dollar", "C$")
    ];

    public SetupWizard(PosStore store)
    {
        InitializeComponent();
        _store = store;
        var settings = _store.Data.Settings;
        _selectedType = settings.BusinessType;
        BusinessNameBox.Text = settings.BusinessName == "TILVEX" ? "" : settings.BusinessName;
        BusinessEmailBox.Text = string.IsNullOrWhiteSpace(settings.Email)
            ? AppSession.CurrentUser?.Email ?? ""
            : settings.Email;
        BusinessPhoneBox.Text = settings.Phone;
        BusinessAddressBox.Text = settings.Address;
        BusinessCityBox.Text = settings.City;
        BusinessCountryBox.Text = settings.Country;
        TaxRateBox.Text = _store.Data.Tax.Rate.ToString("0.##", CultureInfo.CurrentCulture);
        TaxInclusiveBox.IsChecked = _store.Data.Tax.Inclusive;
        foreach (var currency in Currencies)
            CurrencyCombo.Items.Add(new ComboBoxItem { Content = $"{currency.Name} ({currency.Symbol})", Tag = currency.Symbol });
        CurrencyCombo.SelectedValue = settings.CurrencySymbol;
        if (CurrencyCombo.SelectedIndex < 0)
            CurrencyCombo.SelectedIndex = 0;
        BuildBusinessTypeCards();
    }

    private void BuildBusinessTypeCards()
    {
        foreach (var design in BusinessTypeDesign.All)
        {
            var type = design.Type;
            var button = new Button
            {
                Tag = type,
                Style = (Style)FindResource("BusinessTypeCard"),
                Margin = new Thickness(0, 0, 12, 12),
                MinHeight = 112,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(button, design.Name);
            button.Click += BusinessType_Click;

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = design.Glyph,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(design.Accent)),
                FontSize = 25,
                FontWeight = FontWeights.SemiBold
            });
            content.Children.Add(new TextBlock
            {
                Text = design.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(23, 35, 52)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 0, 2)
            });
            content.Children.Add(new TextBlock
            {
                Text = design.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(104, 119, 139)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });
            button.Content = content;
            BusinessTypeGrid.Children.Add(button);
            _typeButtons[type] = button;
        }

        UpdateBusinessTypeSelection();
    }

    private void BusinessType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BusinessType type })
            return;

        _selectedType = type;
        UpdateBusinessTypeSelection();
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void UpdateBusinessTypeSelection()
    {
        var selectedDesign = BusinessTypeDesign.For(_selectedType);
        Resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selectedDesign.Accent));
        foreach (var (type, button) in _typeButtons)
        {
            var design = BusinessTypeDesign.For(type);
            var selected = type == _selectedType;
            button.BorderBrush = new SolidColorBrush(selected
                ? (Color)ColorConverter.ConvertFromString(design.Accent)
                : Color.FromRgb(225, 232, 240));
            button.BorderThickness = new Thickness(selected ? 2 : 1);
            button.Background = new SolidColorBrush(selected
                ? (Color)ColorConverter.ConvertFromString(design.Surface)
                : Colors.White);
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (!_showingDetails)
        {
            ShowBusinessDetails();
            return;
        }

        SaveWorkspace();
    }

    private void ShowBusinessDetails()
    {
        _showingDetails = true;
        BusinessTypeGrid.Visibility = Visibility.Collapsed;
        BusinessDetailsPanel.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        StepCounterText.Text = "STEP 2 OF 2";
        StepTitleText.Text = "Business details";
        PageHeadingText.Text = "A few details to get your workspace ready";
        PageDescriptionText.Text = $"Selected business: {BusinessTypeLabel(_selectedType)}. You can refine these details in Settings.";
        ContinueButton.Content = "Save setup and continue";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _showingDetails = false;
        BusinessTypeGrid.Visibility = Visibility.Visible;
        BusinessDetailsPanel.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;
        StepCounterText.Text = "STEP 1 OF 2";
        StepTitleText.Text = "Business type";
        PageHeadingText.Text = "What kind of business are you setting up?";
        PageDescriptionText.Text = "We’ll configure useful defaults while keeping every option editable.";
        ContinueButton.Content = "Continue";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void SaveWorkspace()
    {
        var name = BusinessNameBox.Text.Trim();
        var email = BusinessEmailBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Enter your business name to continue.");
            BusinessNameBox.Focus();
            return;
        }

        if (email.Length > 0 && !MailAddress.TryCreate(email, out _))
        {
            ShowError("Enter a valid business email address, or leave the field blank.");
            BusinessEmailBox.Focus();
            return;
        }

        if (CurrencyCombo.SelectedValue is not string currency)
        {
            ShowError("Choose the currency you use for sales.");
            return;
        }

        if (!decimal.TryParse(TaxRateBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var taxRate) ||
            taxRate is < 0 or > 100)
        {
            ShowError("Enter a tax rate between 0 and 100.");
            TaxRateBox.Focus();
            return;
        }

        var settings = _store.Data.Settings;
        settings.BusinessName = name;
        settings.Email = email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            settings.OwnerEmail = email;
            settings.LowStockOwnerEmail = email;
            settings.Reports.OwnerEmail = email;
        }
        else if (string.IsNullOrWhiteSpace(settings.OwnerEmail) &&
                 !string.IsNullOrWhiteSpace(AppSession.CurrentUser?.Email) &&
                 !AppSession.CurrentUser.Email.EndsWith("@tilvex.local", StringComparison.OrdinalIgnoreCase))
        {
            settings.OwnerEmail = AppSession.CurrentUser.Email;
            settings.LowStockOwnerEmail = settings.OwnerEmail;
            settings.Reports.OwnerEmail = settings.OwnerEmail;
        }
        settings.Reports.TimeZone = settings.TimeZone;
        settings.Phone = BusinessPhoneBox.Text.Trim();
        settings.Address = BusinessAddressBox.Text.Trim();
        settings.City = BusinessCityBox.Text.Trim();
        settings.Country = BusinessCountryBox.Text.Trim();
        settings.BusinessType = _selectedType;
        settings.CurrencySymbol = currency;
        settings.SetupCompleted = true;
        _store.Data.Tax.Rate = taxRate;
        _store.Data.Tax.Inclusive = TaxInclusiveBox.IsChecked == true;
        try
        {
            _store.Save();
            DialogResult = true;
        }
        catch (IOException ex)
        {
            ShowError($"Could not save your setup. Check that the local TILVEX data folder is writable. {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowError($"Could not save your setup because access to the local data file was denied. {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static string BusinessTypeLabel(BusinessType type) =>
        BusinessTypeDesign.For(type).Name;

    private void Exit_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
