using System.Windows;
using Mesa.Core;

namespace Mesa.Desktop;

public partial class HistoryDetailWindow : Window
{
    private readonly SavedClose close;
    public HistoryDetailWindow(SavedClose close)
    {
        InitializeComponent();
        this.close = close;
        TitleText.Text = $"Ventas del {close.Summary.Date:dd/MM/yyyy}";
        SavedText.Text = $"Guardado el {close.SavedAt.ToLocalTime():dd/MM/yyyy HH:mm}";
        DetailsGrid.ItemsSource = close.Summary.Lines.Select(line => new
        {
            Name = line.Product.Name, Price = Sales.Money(line.Product.Price),
            Quantity = line.Quantity.ToString("N0", Sales.Argentina), Amount = Sales.Money(line.Amount)
        }).ToArray();
        TotalText.Text = Sales.Money(close.Summary.Total);
        TaxCaption.Text = $"Impuesto · {(close.Summary.TaxRate * 100m).ToString("0.##", Sales.Argentina)} %";
        TaxText.Text = Sales.Money(close.Summary.Tax);
        UnitsText.Text = close.Summary.Units.ToString("N0", Sales.Argentina);
    }
    private void Back_Click(object sender, RoutedEventArgs e) => Close();
    private void Print_Click(object sender, RoutedEventArgs e) => new PrintPreviewWindow(close.Summary) { Owner = this }.ShowDialog();
}
