using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Mesa.Core;

namespace Mesa.Desktop;

public partial class PrintPreviewWindow : Window
{
    private readonly SaleSummary summary;
    public PrintPreviewWindow(SaleSummary summary)
    {
        InitializeComponent();
        this.summary = summary;
        Preview.Document = CreateDocument(summary);
    }
    private void Back_Click(object sender, RoutedEventArgs e) => Close();
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            var document = CreateDocument(summary);
            document.PageWidth = dialog.PrintableAreaWidth;
            document.PageHeight = dialog.PrintableAreaHeight;
            document.ColumnWidth = dialog.PrintableAreaWidth;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Mesa - Ventas {summary.Date:yyyy-MM-dd}");
        }
        catch (Exception)
        {
            MessageBox.Show(this, "No se pudo imprimir. Revisá la impresora seleccionada e intentá nuevamente. También podés elegir Microsoft Print to PDF.", "Mesa · Impresión", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    private static FlowDocument CreateDocument(SaleSummary summary)
    {
        var doc = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = Brushes.Black, Background = Brushes.White, PagePadding = new Thickness(38),
            ColumnWidth = 900, Language = System.Windows.Markup.XmlLanguage.GetLanguage("es-AR") };
        doc.Blocks.Add(new Paragraph(new Run("MESA · RESTAURANTE")) { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(166,64,37)) });
        doc.Blocks.Add(new Paragraph(new Run("Resumen de ventas del día")) { FontSize = 24, FontWeight = FontWeights.SemiBold });
        doc.Blocks.Add(new Paragraph(new Run($"Fecha del cierre: {summary.Date.ToString("dd/MM/yyyy", Sales.Argentina)}")) { Margin = new Thickness(0, 0, 0, 25) });
        var table = new Table { CellSpacing = 0 };
        foreach (var width in new[] { 2.4, 1.4, 0.9, 1.5 }) table.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Star) });
        var group = new TableRowGroup(); table.RowGroups.Add(group);
        TableRow AddRow(IEnumerable<string> values, bool header = false)
        {
            var row = new TableRow(); int i = 0;
            foreach (var value in values)
            {
                var cell = new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(0), TextAlignment = i++ == 0 ? TextAlignment.Left : TextAlignment.Right })
                { Padding = new Thickness(7,10,7,10), BorderThickness = new Thickness(0,0,0,0.5), BorderBrush = Brushes.LightGray };
                if (header) { cell.FontWeight = FontWeights.SemiBold; cell.Background = new SolidColorBrush(Color.FromRgb(248,243,239)); }
                row.Cells.Add(cell);
            }
            group.Rows.Add(row); return row;
        }
        AddRow(["Producto", "Precio unitario", "Cantidad", "Importe"], true);
        foreach (var line in summary.Lines) AddRow([line.Product.Name, Sales.Money(line.Product.Price), line.Quantity.ToString("N0", Sales.Argentina), Sales.Money(line.Amount)]);
        doc.Blocks.Add(table);
        doc.Blocks.Add(new Paragraph(new Run($"Total de ventas: {Sales.Money(summary.Total)}")) { FontSize = 20, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right, Margin = new Thickness(0,25,0,10) });
        var taxLabel = $"{(summary.TaxRate * 100m).ToString("0.##", Sales.Argentina)} %";
        doc.Blocks.Add(new Paragraph(new Run($"Impuesto · {taxLabel}: {Sales.Money(summary.Tax)}")) { TextAlignment = TextAlignment.Right });
        doc.Blocks.Add(new Paragraph(new Run($"Unidades vendidas: {summary.Units:N0}")) { TextAlignment = TextAlignment.Right });
        doc.Blocks.Add(new Paragraph(new Run($"El impuesto se calcula sobre las ventas y no se suma nuevamente al total.\nMoneda: pesos argentinos · Precios de ejemplo · Impuesto del cierre: {taxLabel}")) { FontSize = 10, Foreground = Brushes.DimGray, Margin = new Thickness(0,25,0,0) });
        return doc;
    }
}
