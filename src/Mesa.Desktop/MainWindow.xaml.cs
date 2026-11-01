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
    private readonly ObservableCollection<SaleRow> rows = new(Sales.Menu.Select(p => new SaleRow(p)));
    private SaleSummary? summary;
    private SaleSummary? lastSaved;
    private readonly CloseHistory history = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mesa", "cierres.db"));
    private readonly ObservableCollection<HistoryRow> savedCloses = new();
    private bool loading = true;
    private bool dateInvalid;
    private bool validated;

    public MainWindow()
    {
        InitializeComponent();
        RowsControl.DataContext = rows;
        HistoryGrid.ItemsSource = savedCloses;
        ClosingDate.SelectedDate = DateTime.Today;
        ClosingDate.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Date_TextChanged));
        foreach (var row in rows) row.PropertyChanged += Row_Changed;
        loading = false;
        RefreshState();
    }

    private string[] Quantities() => rows.Select(row => row.Quantity).ToArray();
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
        dateInvalid = !DateTime.TryParse(DateText(), Sales.Argentina, DateTimeStyles.AllowWhiteSpaces, out _);
        RefreshState();
    }

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(error ? "Accent" : "Muted");
        if (UIElementAutomationPeer.FromElement(StatusText) is AutomationPeer peer)
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void Row_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (loading || e.PropertyName != nameof(SaleRow.Quantity)) return;
        validated = true;
        RefreshState();
    }

    private void RefreshState()
    {
        if (loading) return;
        bool valid = true;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            bool good = Sales.TryQuantity(row.Quantity, out var quantity);
            row.Error = !good && validated
                ? (long.TryParse(row.Quantity, out long n) && n > Sales.MaximumQuantity
                    ? "El máximo es 999.999 unidades." : "Ingresá un número entero mayor o igual a 0.") : "";
            valid &= good;
            row.Amount = !good ? "—" : summary is null
                ? (quantity == 0 ? Sales.Money(0) : "Pendiente")
                : quantity == summary.Lines[i].Quantity ? Sales.Money(summary.Lines[i].Amount) : "Pendiente";
        }
        var goodDate = TryDate(out var date);
        var current = goodDate && valid && SnapshotState.IsCurrent(summary, date, Quantities());
        ClearButton.IsEnabled = HasInput || summary is not null;
        PrintButton.IsEnabled = goodDate && SnapshotState.CanPrint(summary, date, Quantities());
        SaveButton.IsEnabled = current && !CloseHistory.SameValues(summary, lastSaved);
        SummaryCaption.Text = summary is not null && !current ? "Último cálculo · Total de ventas" : "Total de ventas";
        if (!valid || !goodDate)
        {
            TotalValue.Text = TaxValue.Text = "—";
            UnitsValue.Text = "—";
            SetStatus(!goodDate ? "Ingresá una fecha válida (dd/mm/aaaa)." : "Revisá las cantidades señaladas.", true);
        }
        else if (summary is null)
        {
            TotalValue.Text = TaxValue.Text = Sales.Money(0);
            UnitsValue.Text = "0";
            SetStatus(HasInput ? "Hay cantidades pendientes de calcular." : "Ingresá las cantidades vendidas para calcular.");
        }
        else
        {
            TotalValue.Text = Sales.Money(summary.Total);
            TaxValue.Text = Sales.Money(summary.Tax);
            UnitsValue.Text = summary.Units.ToString("N0", Sales.Argentina);
            SetStatus(current ? (CloseHistory.SameValues(summary, lastSaved) ? "Cierre guardado. Podés consultarlo en Historial." : summary.Units > 0 ? "Cálculo actualizado. Ya podés guardar el cierre." : "Cálculo actualizado. Podés guardar este día sin ventas.")
                : "Hay cambios pendientes de calcular. El resumen corresponde al último cálculo.");
        }
    }

    private void Calculate_Click(object sender, RoutedEventArgs e) => Calculate();
    private void Calculate()
    {
        validated = true;
        // Confirmar la edición de fecha sin depender de perder el foco.
        dateInvalid = !DateTime.TryParse(DateText(), Sales.Argentina, DateTimeStyles.AllowWhiteSpaces, out _);
        RefreshState();
        if (!TryDate(out var date)) { ClosingDate.Focus(); return; }
        var firstError = rows.FirstOrDefault(row => row.HasError);
        if (firstError is not null) { FocusRow(firstError); return; }
        summary = Sales.Calculate(date, Quantities());
        RefreshState();
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
        loading = true;
        foreach (var row in rows) row.Quantity = "0";
        summary = null;
        lastSaved = null;
        validated = false;
        loading = false;
        RefreshState();
        SetStatus("Se limpiaron las cantidades y los resultados.");
        FocusRow(rows[0]);
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
            try { history.Save(summary); }
            catch (CloseAlreadyExistsException)
            {
                var previous = history.Find(summary.Date)!;
                var message = $"Ya hay un cierre guardado para el {summary.Date:dd/MM/yyyy} por {Sales.Money(previous.Summary.Total)}. Se reemplazará por el cierre actual de {Sales.Money(summary.Total)}.";
                if (new ConfirmWindow("¿Querés reemplazar el cierre de este día?", message, "Sí, reemplazar") { Owner = this }.ShowDialog() != true) return;
                history.Save(summary, replace: true);
            }
            lastSaved = summary;
            RefreshState();
        }
        catch (Exception error) when (IsStorageError(error))
        {
            SetStatus("No se pudo guardar el cierre. Las cantidades siguen en pantalla.", true);
            ShowStorageError(error);
        }
    }

    private static bool IsStorageError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException or FormatException or OverflowException;
    private void ShowStorageError(Exception error)
    {
        var detail = error is InvalidDataException ? error.Message
            : "Revisá que tu usuario pueda escribir en la carpeta de datos y que haya espacio disponible. Si otra ventana está guardando, intentá nuevamente.";
        MessageBox.Show(this, "No se pudo acceder al historial. No se borraron los cierres guardados.\n\n" + detail, "Mesa · Historial", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void Tabs_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && e.Source == MainTabs && MainTabs.SelectedIndex == 1) LoadHistory();
    }
    private void RefreshHistory_Click(object sender, RoutedEventArgs e) => LoadHistory();
    private void LoadHistory()
    {
        try
        {
            var selectedDate = (HistoryGrid.SelectedItem as HistoryRow)?.Close.Summary.Date;
            var records = history.ReadAll();
            EmptyHistory.Text = "Todavía no hay cierres guardados.\nCalculá las ventas y elegí Guardar cierre para empezar.";
            savedCloses.Clear();
            foreach (var record in records) savedCloses.Add(new HistoryRow(record));
            HistoryStatus.Text = records.Count == 1 ? "1 cierre guardado · Más reciente primero" : $"{records.Count} cierres guardados · Más recientes primero";
            EmptyHistory.Visibility = records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryGrid.SelectedItem = savedCloses.FirstOrDefault(row => row.Close.Summary.Date == selectedDate) ?? savedCloses.FirstOrDefault();
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
        HistoryDetailButton.IsEnabled = HistoryPrintButton.IsEnabled = HistoryGrid.SelectedItem is HistoryRow;
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
        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        { if (MainTabs.SelectedIndex == 1) HistoryPrint_Click(sender, e); else Print_Click(sender, e); e.Handled = true; return; }
        if (MainTabs.SelectedIndex != 0) return;
        if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox)
        { Calculate(); e.Handled = true; }
        if (Keyboard.FocusedElement is TextBox box && box.DataContext is SaleRow row && Keyboard.Modifiers == ModifierKeys.None
            && (e.Key == Key.Up || e.Key == Key.Down))
        { ChangeQuantity(row, e.Key == Key.Up ? 1 : -1); e.Handled = true; }
    }
    private void Quantity_Focused(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        box.Dispatcher.BeginInvoke(new Action(box.SelectAll));
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) => FocusRow(rows[0]);
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
        if (!HasInput || (TryDate(out var date) && SnapshotState.IsCurrent(summary, date, Quantities()) && CloseHistory.SameValues(summary, lastSaved))) return;
        e.Cancel = new ConfirmWindow("¿Querés salir sin guardar este cierre?", "Hay cantidades que no guardaste. Podés conservar la pantalla y usar Guardar cierre. Los cierres que ya guardaste siguen en el historial.", "Salir sin guardar") { Owner = this }.ShowDialog() != true;
    }
}
