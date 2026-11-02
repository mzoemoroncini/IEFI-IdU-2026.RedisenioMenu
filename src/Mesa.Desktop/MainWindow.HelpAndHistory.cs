using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Mesa.Core;

namespace Mesa.Desktop;

public partial class MainWindow
{
    private IReadOnlyList<SavedClose> allCloses = [];
    private bool? compactSales;
    private void UpdateSalesLayout()
    {
        bool compact = SalesPanels.ActualWidth < 900;
        if (compactSales == compact) return;
        compactSales = compact;
        SalesScroll.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ProductsPanel.Height = compact ? 420 : double.NaN;
        SalesGap.Width = new GridLength(compact ? 0 : 20);
        SummaryColumn.Width = new GridLength(compact ? 0 : 292);
        Grid.SetColumn(SummaryScroll, compact ? 0 : 2);
        Grid.SetRow(SummaryScroll, compact ? 1 : 0);
        SummaryScroll.Margin = compact ? new Thickness(0,18,0,0) : new Thickness(0);
        SummaryScroll.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }
    private void UndoClear_Click(object sender, RoutedEventArgs e)
    {
        if (selectedClose is not null) return;
        var previous = clearedDraft.Take(DateText(), rows.Select(r => r.MenuId).ToArray());
        if (previous is null) { RefreshState(); return; }
        loading = true;
        for (int i = 0; i < rows.Count; i++) rows[i].Quantity = previous[i];
        validated = true;
        loading = false;
        RefreshState();
        SetStatus("Se recuperaron las cantidades anteriores a la limpieza. El historial no se modificó.");
        if (rows.Count > 0) FocusRow(rows[0]);
    }
    private void Help_Click(object sender, RoutedEventArgs e) => new HelpWindow(MainTabs.SelectedIndex) { Owner = this }.ShowDialog();
    private void HistoryFilter_Changed(object sender, SelectionChangedEventArgs e)
    { if (!loading) Dispatcher.BeginInvoke(new Action(() => ApplyHistoryFilters())); }
    private void HistoryFilter_TextChanged(object sender, TextChangedEventArgs e)
    { if (!loading) Dispatcher.BeginInvoke(new Action(() => ApplyHistoryFilters())); }
    private void HistoryFilter_Invalid(object? sender, DatePickerDateValidationErrorEventArgs e)
    { e.ThrowException = false; if (!loading) Dispatcher.BeginInvoke(new Action(() => ApplyHistoryFilters())); }
    private static bool ReadFilterDate(DatePicker picker, out DateTime? date)
    {
        picker.ApplyTemplate();
        var text = picker.Template.FindName("PART_TextBox", picker) is DatePickerTextBox box ? box.Text : picker.Text;
        date = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!DateTime.TryParseExact(text.Trim(), ["d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy"], Sales.Argentina, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var parsed)) return false;
        date = parsed.Date; return true;
    }
    private void ApplyHistoryFilters(DateTime? selectedDate = null)
    {
        if (loading || HistoryGrid is null) return;
        if (!ReadFilterDate(HistoryFrom, out var from) || !ReadFilterDate(HistoryTo, out var to) || from > to)
        {
            HistoryStatus.Text = "El filtro no se aplicó. Usá fechas válidas (dd/mm/aaaa) y un Desde anterior o igual a Hasta.";
            HistoryStatus.Foreground = (Brush)FindResource("Danger");
            return;
        }
        selectedDate ??= (HistoryGrid.SelectedItem as HistoryRow)?.Close.Summary.Date;
        var records = CloseSearch.Filter(allCloses, from, to);
        savedCloses.Clear();
        foreach (var record in records) savedCloses.Add(new HistoryRow(record));
        HistoryStatus.Foreground = (Brush)FindResource("Muted");
        HistoryStatus.Text = $"{records.Count} de {allCloses.Count} cierres · Más recientes primero" + (from.HasValue || to.HasValue ? " · Filtros aplicados" : "");
        EmptyHistory.Text = allCloses.Count == 0 ? "Todavía no hay cierres guardados.\nCargá las cantidades y elegí Guardar cierre para empezar."
            : "No hay cierres en estas fechas.\nProbá otro período o elegí Limpiar filtros.";
        EmptyHistory.Visibility = records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryGrid.SelectedItem = savedCloses.FirstOrDefault(row => row.Close.Summary.Date == selectedDate) ?? savedCloses.FirstOrDefault();
    }
    private void ClearHistoryFilters_Click(object sender, RoutedEventArgs e)
    {
        HistoryFrom.SelectedDate = HistoryTo.SelectedDate = null;
        HistoryFrom.Text = HistoryTo.Text = "";
        ApplyHistoryFilters(); HistoryFrom.Focus();
    }
    private void HistoryEdit_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not HistoryRow row) return;
        if (new HistoryEditWindow(history, row.Close) { Owner = this }.ShowDialog() == true)
        {
            LoadHistory(); checkedDate = null; RefreshState();
            HistoryStatus.Text += " · Cambios guardados";
        }
        HistoryEditButton.Focus();
    }
}
