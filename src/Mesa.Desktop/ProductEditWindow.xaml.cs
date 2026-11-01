using System.IO;
using System.Windows;
using Mesa.Core;
using Microsoft.Data.Sqlite;

namespace Mesa.Desktop;

public partial class ProductEditWindow : Window
{
    private readonly MenuCatalog catalog;
    private readonly MenuProduct? original;
    public MenuProduct? SavedProduct { get; private set; }

    public ProductEditWindow(MenuCatalog catalog, MenuProduct? original = null)
    {
        InitializeComponent();
        this.catalog = catalog; this.original = original;
        Heading.Text = original is null ? "Agregar producto" : "Editar producto";
        NameInput.Text = original?.Name ?? "";
        PriceInput.Text = original?.Price.ToString("0.00", Sales.Argentina) ?? "";
    }
    private void Window_Rendered(object? sender, EventArgs e) { NameInput.Focus(); NameInput.SelectAll(); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        NameError.Visibility = PriceError.Visibility = SaveError.Visibility = Visibility.Collapsed;
        var name = MenuCatalog.NormalizeName(NameInput.Text);
        if (name.Length is < 1 or > 200)
        { NameError.Text = "Ingresá un nombre de hasta 200 caracteres."; NameError.Visibility = Visibility.Visible; NameInput.Focus(); return; }
        if (!MenuCatalog.TryPrice(PriceInput.Text, out var price))
        { PriceError.Text = "Ingresá un precio entre 0 y 100.000.000, con hasta 2 decimales. Ej.: 6500,50."; PriceError.Visibility = Visibility.Visible; PriceInput.Focus(); return; }
        try
        {
            SavedProduct = catalog.Save(original?.Id, name, price);
            DialogResult = true;
        }
        catch (DuplicateProductException error)
        { NameError.Text = error.Message; NameError.Visibility = Visibility.Visible; NameInput.Focus(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            SaveError.Text = error is InvalidOperationException or InvalidDataException ? error.Message
                : "No se pudo guardar el producto. Revisá el acceso a los datos e intentá nuevamente.";
            SaveError.Visibility = Visibility.Visible;
        }
    }
}
