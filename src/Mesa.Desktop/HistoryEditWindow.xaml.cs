using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Mesa.Core;
using Microsoft.Data.Sqlite;

namespace Mesa.Desktop;

public partial class HistoryEditWindow : Window
{
    private readonly CloseHistory history;
    private readonly SavedClose original;
    private readonly ObservableCollection<SaleRow> rows = new();
    private SaleSummary? candidate;
    private bool saved;

    public HistoryEditWindow(CloseHistory history, SavedClose original)
    {
        InitializeComponent();
        this.history = history;
        this.original = original;
        Heading.Text = $"Editar ventas del {original.Summary.Date:dd/MM/yyyy}";
        foreach (var line in original.Summary.Lines)
        {
            var row = new SaleRow(line.Product) { Quantity = line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            row.PropertyChanged += Row_Changed;
            rows.Add(row);
        }
        EditRows.ItemsSource = rows;
        Refresh();
    }

    private bool HasChanges => !SnapshotState.IsCurrent(original.Summary, original.Summary.Date,
        rows.Select(row => row.Product).ToArray(), rows.Select(row => row.Quantity).ToArray());

    private void Row_Changed(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SaleRow.Quantity)) Refresh(); }

    private void Refresh()
    {
        bool valid = true;
        foreach (var row in rows)
        {
            var good = Sales.TryQuantity(row.Quantity, out var quantity);
            row.Error = good ? "" : "Ingresá un entero entre 0 y 999.999.";
            row.Amount = good ? Sales.Money(row.Product.Price * quantity) : "—";
            valid &= good;
        }
        candidate = valid ? new SaleSummary(original.Summary.Date,
            Array.AsReadOnly(rows.Select(row => { Sales.TryQuantity(row.Quantity, out var quantity); return new SaleLine(row.Product, quantity); }).ToArray()), original.Summary.TaxRate) : null;
        SaveChangesButton.IsEnabled = candidate is not null && HasChanges;
        Totals.Text = candidate is null ? "Revisá las cantidades señaladas." : $"Ventas: {Sales.Money(candidate.Total)}   ·   Impuesto: {Sales.Money(candidate.Tax)}   ·   Unidades: {candidate.Units}";
        EditStatus.Text = HasChanges ? "Los cambios todavía no están guardados." : "Modificá una cantidad para guardar cambios.";
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
        if (!SaveChangesButton.IsEnabled || candidate is null) return;
        if (new ConfirmWindow("¿Querés actualizar el cierre de este día?", $"Se guardarán las nuevas cantidades del {candidate.Date:dd/MM/yyyy}, por {Sales.Money(candidate.Total)}. Seguirá habiendo un solo registro de esta fecha.", "Sí, guardar cambios") { Owner = this }.ShowDialog() != true) return;
        try
        {
            var latest = history.Find(candidate.Date);
            if (latest is null || latest.SavedAt != original.SavedAt || !CloseHistory.SameValues(latest.Summary, original.Summary))
            {
                EditStatus.Text = "El registro cambió desde que lo abriste. Conservá tus cantidades y volvé a consultar el día antes de guardar.";
                return;
            }
            history.Save(candidate, replace: true);
            saved = true;
            DialogResult = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or FormatException or OverflowException)
        { EditStatus.Text = "No se pudieron guardar los cambios. Las cantidades siguen en pantalla y el cierre anterior se conserva."; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (saved || !HasChanges) return;
        e.Cancel = new ConfirmWindow("¿Descartar los cambios de este cierre?", "El registro guardado seguirá igual. Podés conservar esta ventana para continuar editando.", "Sí, descartar") { Owner = this }.ShowDialog() != true;
    }
}
