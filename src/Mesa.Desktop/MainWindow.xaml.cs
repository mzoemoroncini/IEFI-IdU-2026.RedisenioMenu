using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mesa.Core;
using Microsoft.Data.Sqlite;

namespace Mesa.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SaleRow> rows = new();
    private SaleSummary? summary;
    private SavedClose? selectedClose;
    private DateTime? checkedDate;
    private DateTime? justSavedDate;
    private bool dateStorageFailed;
    private readonly CloseHistory history;
    private readonly ObservableCollection<HistoryRow> savedCloses = new();
    private readonly string themePreferencePath;
    private bool loading = true;
    private bool dateInvalid;
    private bool validated;
    private readonly DraftUndo clearedDraft = new();

    public MainWindow() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mesa", "cierres.db")) { }

    public MainWindow(string databasePath)
    {
        history = new CloseHistory(databasePath);
        themePreferencePath = Path.Combine(Path.GetDirectoryName(history.DatabasePath)!, "preferencias.json");
        ThemeManager.Apply(ThemeManager.Load(themePreferencePath));
        SourceInitialized += (_, _) => ThemeManager.UpdateWindowChrome(this);
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(() => WindowBounds.EnsureVisible(this, center: false)));
        InitializeComponent();
        RowsControl.DataContext = rows;
        SalesPanels.SizeChanged += (_, _) => UpdateSalesLayout();
        HistoryGrid.ItemsSource = savedCloses;
        catalog = new MenuCatalog(history.DatabasePath);
        MenuGrid.ItemsSource = menuProducts;
        HistoryFrom.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(HistoryFilter_TextChanged));
        HistoryTo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(HistoryFilter_TextChanged));
        ClosingDate.SelectedDate = DateTime.Today;
        ClosingDate.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Date_TextChanged));
        loading = false;
        LightThemeButton.IsChecked = ThemeManager.Current == MesaTheme.Light;
        DarkThemeButton.IsChecked = ThemeManager.Current == MesaTheme.Dark;
        RefreshState();
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (loading || sender is not RadioButton option) return;
        var theme = option.Tag?.ToString() == "Dark" ? MesaTheme.Dark : MesaTheme.Light;
        if (ThemeManager.Current == theme) return;
        ThemeManager.Apply(theme);
        var stored = ThemeManager.Save(themePreferencePath, theme);
        RefreshState();
        if (!stored) SetStatus("Se aplicó el tema, pero no se pudo recordar la preferencia para la próxima vez.");
    }

    private string[] Quantities() => rows.Select(row => row.Quantity).ToArray();
    private Product[] Products() => rows.Select(row => row.Product).ToArray();
    private bool HasInput => rows.Any(row => row.Quantity != "0");

    private bool TryDate(out DateTime date)
    {
        date = default;
        return !dateInvalid && DateTime.TryParse(DateText(), Sales.Argentina,
            DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private string DateText()
    {
        ClosingDate.ApplyTemplate();
        return ClosingDate.Template.FindName("PART_TextBox", ClosingDate) is DatePickerTextBox input
            ? input.Text : ClosingDate.Text;
    }

    private void Date_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (loading) return;
        clearedDraft.Clear();
        checkedDate = null;
        dateInvalid = !DateTime.TryParse(DateText(), Sales.Argentina, DateTimeStyles.AllowWhiteSpaces, out _);
        RefreshState();
    }

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(error ? "Danger" : "Muted");
        if (UIElementAutomationPeer.FromElement(StatusText) is AutomationPeer peer)
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void Row_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (loading || e.PropertyName != nameof(SaleRow.Quantity)) return;
        clearedDraft.Clear();
        validated = true;
        RefreshState();
    }

    private void RefreshState()
    {
        if (loading) return;
        RefreshRegisteredDay();
        UndoClearButton.Visibility = selectedClose is null && clearedDraft.CanRestore(DateText(), rows.Select(r => r.MenuId).ToArray()) ? Visibility.Visible : Visibility.Collapsed;
        var canCalculate = menuAvailable && rows.Count > 0;
        if (!canCalculate)
        {
            summary = null;
            PrintButton.IsEnabled = SaveButton.IsEnabled = false;
            ClearButton.IsEnabled = HasInput;
            TotalValue.Text = TaxValue.Text = Sales.Money(0);
            UnitsValue.Text = "0";
            SetStatus(menuAvailable ? "Agregá un producto desde Menú para empezar." : "No se pudo cargar el menú. Probá Actualizar en la pestaña Menú.", !menuAvailable);
            return;
        }
        bool valid = true;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            bool good = Sales.TryQuantity(row.Quantity, out var quantity);
            row.Error = !good && validated
                ? (long.TryParse(row.Quantity, out long n) && n > Sales.MaximumQuantity
                    ? "El máximo es 999.999 unidades." : "Ingresá un número entero mayor o igual a 0.") : "";
            valid &= good;
            row.Amount = good ? Sales.Money(row.Product.Price * quantity) : "—";
        }
        var goodDate = TryDate(out var date);
        summary = goodDate && valid ? Sales.Calculate(date, Products(), Quantities()) : null;
        ClearButton.IsEnabled = HasInput;
        PrintButton.IsEnabled = selectedClose is null && summary is { Units: > 0 };
        SaveButton.IsEnabled = summary is not null && selectedClose is null && !dateStorageFailed;
        SummaryCaption.Text = "Total de ventas";
        if (summary is null)
        {
            TotalValue.Text = TaxValue.Text = "—";
            UnitsValue.Text = "—";
            SetStatus(!goodDate ? "Ingresá una fecha válida (dd/mm/aaaa)." : "Revisá las cantidades señaladas.", true);
        }
        else
        {
            TotalValue.Text = Sales.Money(summary.Total);
            TaxValue.Text = Sales.Money(summary.Tax);
            UnitsValue.Text = summary.Units.ToString("N0", Sales.Argentina);
            SetStatus(dateStorageFailed ? "No se pudo comprobar si este día ya está registrado. Volvé a elegir la fecha para reintentar."
                : selectedClose is not null ? (justSavedDate == selectedClose.Summary.Date
                    ? $"Ventas del {selectedClose.Summary.Date:dd/MM/yyyy} guardadas correctamente. Se limpiaron las cantidades y los totales. Elegí otra fecha para una nueva carga."
                    : "Este día ya está registrado. Podés verlo en Historial o editar su registro.")
                : summary.Units > 0 ? "Totales actualizados automáticamente. Hay cambios sin guardar."
                : "Los totales se actualizan al cargar cantidades. También podés guardar un día sin ventas.");
        }
    }

    private void ChangeQuantity(SaleRow row, int step)
    {
        if (!Sales.TryQuantity(row.Quantity, out var quantity))
        {
            validated = true; RefreshState(); FocusRow(row); return;
        }
        row.Quantity = Math.Clamp(quantity + step, 0, Sales.MaximumQuantity).ToString(CultureInfo.InvariantCulture);
    }
    private void Decrease_Click(object sender, RoutedEventArgs e) => ChangeQuantity((SaleRow)((FrameworkElement)sender).DataContext, -1);
    private void Increase_Click(object sender, RoutedEventArgs e) => ChangeQuantity((SaleRow)((FrameworkElement)sender).DataContext, 1);

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (new ConfirmWindow("¿Querés borrar todas las cantidades?", "Las cantidades y los resultados volverán a cero. Los cierres guardados se conservan en el historial.", "Sí, limpiar") { Owner = this }.ShowDialog() != true) return;
        clearedDraft.Capture(DateText(), rows.Select(r => r.MenuId).ToArray(), Quantities());
        loading = true;
        foreach (var row in rows) row.Quantity = "0";
        summary = null;
        validated = false;
        loading = false;
        RefreshState();
        SetStatus("Se limpiaron las cantidades. Podés usar Deshacer limpieza hasta que cambies cantidades, fecha o menú, o guardes el cierre.");
        if (rows.Count > 0) FocusRow(rows[0]);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        RefreshState();
        if (PrintButton.IsEnabled && summary is not null)
            new PrintPreviewWindow(summary) { Owner = this }.ShowDialog();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RefreshState();
        if (!SaveButton.IsEnabled || summary is null) return;
        try
        {
            var completed = DailyCloseWorkflow.SaveAndClear(history, summary);
            clearedDraft.Clear();
            loading = true;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Quantity = completed.EmptyDraft.Lines[i].Quantity.ToString(CultureInfo.InvariantCulture);
                rows[i].Error = "";
            }
            validated = false;
            summary = null;
            justSavedDate = completed.Saved.Summary.Date;
            selectedClose = completed.Saved;
            checkedDate = completed.Saved.Summary.Date;
            dateStorageFailed = false;
            loading = false;
            RefreshState();
            SetStatus($"Ventas del {completed.Saved.Summary.Date:dd/MM/yyyy} guardadas correctamente. Se limpiaron las cantidades y los totales. Elegí otra fecha para una nueva carga.");
            ClosingDate.Focus();
        }
        catch (CloseAlreadyExistsException)
        {
            checkedDate = null;
            RefreshState();
            SetStatus("Este día ya está registrado. La carga sigue en pantalla; revisá el historial antes de editar el registro.");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            SetStatus("No se pudo guardar el cierre. Las cantidades siguen en pantalla.", true);
            ShowStorageError(error);
        }
    }

    private void RefreshRegisteredDay()
    {
        if (!TryDate(out var date))
        {
            selectedClose = null;
            checkedDate = null;
            dateStorageFailed = false;
        }
        else if (checkedDate != date.Date)
        {
            checkedDate = date.Date;
            try { selectedClose = history.Find(date); dateStorageFailed = false; }
            catch (Exception error) when (IsStorageError(error))
            { selectedClose = null; dateStorageFailed = true; }
        }
        if (!TryDate(out var selectedDate) || selectedDate.Date != justSavedDate) justSavedDate = null;
        RegisteredDayPanel.Visibility = selectedClose is null ? Visibility.Collapsed : Visibility.Visible;
        RegisteredDayText.Text = selectedClose is null ? "" : $"El {selectedClose.Summary.Date:dd/MM/yyyy} ya tiene un cierre guardado por {Sales.Money(selectedClose.Summary.Total)}.";
        RowsControl.IsEnabled = selectedClose is null;
    }

    private void ViewRegisteredDay_Click(object sender, RoutedEventArgs e)
    {
        if (selectedClose is not { } saved) return;
        MainTabs.SelectedIndex = 1;
        HistoryGrid.SelectedItem = savedCloses.FirstOrDefault(row => row.Close.Summary.Date == saved.Summary.Date);
        if (HistoryGrid.SelectedItem is not null) HistoryGrid.ScrollIntoView(HistoryGrid.SelectedItem);
    }

    private void EditRegisteredDay_Click(object sender, RoutedEventArgs e)
    {
        if (selectedClose is not { } saved) return;
        if (new HistoryEditWindow(history, saved) { Owner = this }.ShowDialog() != true) return;
        justSavedDate = null;
        checkedDate = null;
        RefreshState();
        SetStatus("Se actualizaron las ventas del día. Podés consultarlas en Historial.");
    }

    private static bool IsStorageError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException or FormatException or OverflowException;
    private void ShowStorageError(Exception error)
    {
        var detail = error is InvalidDataException ? error.Message
            : "Revisá que tu usuario pueda escribir en la carpeta de datos y que haya espacio disponible. Si otra ventana está guardando, intentá nuevamente.";
        MessageBox.Show(this, "No se pudo acceder a los datos de Mesa. No se borraron los cierres guardados.\n\n" + detail, "Mesa · Datos", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void Tabs_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || e.Source != MainTabs) return;
        if (MainTabs.SelectedIndex == 1) LoadHistory();
        else { checkedDate = null; LoadMenu(); }
    }
    private void RefreshHistory_Click(object sender, RoutedEventArgs e) => LoadHistory();
    private void LoadHistory()
    {
        try
        {
            var selectedDate = (HistoryGrid.SelectedItem as HistoryRow)?.Close.Summary.Date;
            allCloses = history.ReadAll();
            ApplyHistoryFilters(selectedDate);
        }
        catch (Exception error) when (IsStorageError(error))
        {
            savedCloses.Clear();
            EmptyHistory.Text = "No se pudo cargar el historial. Probá Actualizar.";
            EmptyHistory.Visibility = Visibility.Visible;
            HistoryStatus.Text = "Historial no disponible";
            ShowStorageError(error);
        }
    }
    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryDetailButton is null || HistoryPrintButton is null) return;
        HistoryEditButton.IsEnabled = HistoryDetailButton.IsEnabled = HistoryPrintButton.IsEnabled = HistoryGrid.SelectedItem is HistoryRow;
    }
    private void HistoryDetail_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is HistoryRow row)
            new HistoryDetailWindow(row.Close) { Owner = this }.ShowDialog();
    }
    private void HistoryPrint_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is HistoryRow row)
            new PrintPreviewWindow(row.Close.Summary) { Owner = this }.ShowDialog();
    }
    private void History_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(HistoryGrid, e.OriginalSource as DependencyObject) is DataGridRow)
            HistoryDetail_Click(sender, e);
    }
    private void History_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { HistoryDetail_Click(sender, e); e.Handled = true; }
    }

    private void Date_Changed(object sender, SelectionChangedEventArgs e)
    {
        clearedDraft.Clear();
        checkedDate = null;
        dateInvalid = ClosingDate.SelectedDate is null;
        // DatePicker actualiza Text después del evento SelectedDateChanged.
        if (!loading) Dispatcher.BeginInvoke(new Action(RefreshState));
    }
    private void Date_Invalid(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        dateInvalid = true;
        RefreshState();
    }
    private void Date_FocusLost(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (loading) return;
        dateInvalid = !DateTime.TryParse(DateText(), Sales.Argentina, DateTimeStyles.AllowWhiteSpaces, out _);
        RefreshState();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { Help_Click(sender, e); e.Handled = true; return; }
        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        { if (MainTabs.SelectedIndex == 1) HistoryPrint_Click(sender, e); else if (MainTabs.SelectedIndex == 0) Print_Click(sender, e); e.Handled = true; return; }
        if (MainTabs.SelectedIndex != 0) return;
        if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox quantityBox && quantityBox.DataContext is SaleRow)
        { quantityBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; }
        if (Keyboard.FocusedElement is TextBox box && box.DataContext is SaleRow row && Keyboard.Modifiers == ModifierKeys.None
            && (e.Key == Key.Up || e.Key == Key.Down))
        { ChangeQuantity(row, e.Key == Key.Up ? 1 : -1); e.Handled = true; }
    }
    private void Quantity_Focused(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        box.Dispatcher.BeginInvoke(new Action(box.SelectAll));
    }
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowBounds.EnsureVisible(this);
        LoadMenu();
        if (rows.Count > 0) FocusRow(rows[0]);
    }
    private void FocusRow(SaleRow row)
    {
        RowsControl.UpdateLayout();
        if (RowsControl.ItemContainerGenerator.ContainerFromItem(row) is DependencyObject container)
        {
            var box = FindTextBox(container);
            box?.BringIntoView(); box?.Focus();
        }
    }
    private static TextBox? FindTextBox(DependencyObject parent)
    {
        if (parent is TextBox box) return box;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindTextBox(VisualTreeHelper.GetChild(parent, i)) is TextBox child) return child;
        return null;
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!HasInput) return;
        e.Cancel = new ConfirmWindow("¿Querés salir sin guardar este cierre?", "Hay cantidades que no guardaste. Podés conservar la pantalla y usar Guardar cierre. Los cierres que ya guardaste siguen en el historial.", "Salir sin guardar") { Owner = this }.ShowDialog() != true;
    }
}
