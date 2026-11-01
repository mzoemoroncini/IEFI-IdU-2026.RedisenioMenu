using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mesa.Core;

namespace Mesa.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SaleRow> rows = new(Sales.Menu.Select(p => new SaleRow(p)));
    private SaleSummary? summary;
    private bool loading = true;
    private bool dateInvalid;
    private bool validated;

    public MainWindow()
    {
        InitializeComponent();
        RowsControl.DataContext = rows;
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
            SetStatus(current ? (summary.Units > 0 ? "Cálculo actualizado." : "Cálculo actualizado. No hay ventas registradas.")
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
        if (new ConfirmWindow("¿Querés borrar todas las cantidades?", "Las cantidades y los resultados volverán a cero.", "Sí, limpiar") { Owner = this }.ShowDialog() != true) return;
        loading = true;
        foreach (var row in rows) row.Quantity = "0";
        summary = null;
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
        { Print_Click(sender, e); e.Handled = true; return; }
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
        if (!HasInput) return;
        e.Cancel = new ConfirmWindow("¿Querés cerrar Mesa?", "Las cantidades de esta sesión no se guardan. Podés volver e imprimir el resumen antes de salir.", "Sí, salir") { Owner = this }.ShowDialog() != true;
    }
}
