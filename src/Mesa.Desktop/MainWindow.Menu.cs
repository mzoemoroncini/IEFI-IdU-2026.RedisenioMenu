using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mesa.Core;

namespace Mesa.Desktop;

public partial class MainWindow
{
    private MenuCatalog catalog = null!;
    private readonly ObservableCollection<MenuProduct> menuProducts = new();
    private bool menuAvailable;

    private void LoadMenu(long? selectedId = null)
    {
        try
        {
            var products = catalog.ReadAll();
            selectedId ??= (MenuGrid.SelectedItem as MenuProduct)?.Id;
            if (!menuProducts.SequenceEqual(products))
            {
                var previousLoading = loading;
                loading = true;
                var quantities = rows.ToDictionary(row => row.MenuId, row => row.Quantity);
                foreach (var row in rows) row.PropertyChanged -= Row_Changed;
                rows.Clear();
                foreach (var product in products)
                {
                    var row = new SaleRow(product.Product, product.Id) { Quantity = quantities.GetValueOrDefault(product.Id, "0") };
                    row.PropertyChanged += Row_Changed;
                    rows.Add(row);
                }
                menuProducts.Clear();
                foreach (var product in products) menuProducts.Add(product);
                summary = null;
                loading = previousLoading;
            }
            menuAvailable = true;
            ProductsCount.Text = products.Count == 1 ? "1 producto" : $"{products.Count} productos";
            QuantityHelp.Text = products.Count == 0 ? "No hay productos. Agregá uno desde la pestaña Menú." : "Tab / Enter: siguiente cantidad  ·  Totales automáticos";
            MenuStatus.Text = $"{products.Count} productos disponibles" + (products.Count == MenuCatalog.MaximumProducts ? " · Se alcanzó el máximo de 100 productos." : "");
            EmptyMenu.Text = "No hay productos en el menú.\nElegí Agregar producto para empezar.";
            EmptyMenu.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MenuGrid.Visibility = Visibility.Visible;
            AddProductButton.IsEnabled = products.Count < MenuCatalog.MaximumProducts;
            MenuGrid.SelectedItem = menuProducts.FirstOrDefault(product => product.Id == selectedId) ?? menuProducts.FirstOrDefault();
            UpdateMenuSelection();
            RefreshState();
        }
        catch (Exception error) when (IsStorageError(error))
        {
            menuAvailable = false;
            AddProductButton.IsEnabled = false;
            UpdateMenuSelection();
            MenuGrid.Visibility = Visibility.Collapsed;
            EmptyMenu.Text = "No se pudo cargar el menú. Probá Actualizar.";
            EmptyMenu.Visibility = Visibility.Visible;
            MenuStatus.Text = "Menú no disponible";
            RefreshState();
            ShowStorageError(error);
        }
    }

    private void MenuRefresh_Click(object sender, RoutedEventArgs e) => LoadMenu();
    private void AddProduct_Click(object sender, RoutedEventArgs e) => EditProduct(null);
    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (MenuGrid.SelectedItem is MenuProduct product) EditProduct(product);
    }
    private void EditProduct(MenuProduct? product)
    {
        if (!menuAvailable) return;
        var editor = new ProductEditWindow(catalog, product) { Owner = this };
        if (editor.ShowDialog() != true || editor.SavedProduct is null) return;
        LoadMenu(editor.SavedProduct.Id);
        if (menuAvailable) MenuStatus.Text = $"Se guardó «{editor.SavedProduct.Name}». El historial conserva los datos anteriores.";
    }
    private void RemoveProduct_Click(object sender, RoutedEventArgs e)
    {
        if (!menuAvailable || MenuGrid.SelectedItem is not MenuProduct product) return;
        var draft = rows.FirstOrDefault(row => row.MenuId == product.Id);
        var message = $"«{product.Name}» dejará de aparecer en el menú. Los cierres guardados conservan este producto y su precio original.";
        if (draft is not null && draft.Quantity != "0")
            message += " También se quitará su cantidad del cierre que estás cargando. Las cantidades de los demás productos se conservan.";
        if (new ConfirmWindow("¿Querés quitar este producto del menú?", message, "Sí, quitar") { Owner = this }.ShowDialog() != true) return;
        try
        {
            catalog.Remove(product.Id);
            LoadMenu();
            if (menuAvailable) MenuStatus.Text = $"Se quitó «{product.Name}» del menú. Sus ventas anteriores siguen en el historial.";
        }
        catch (Exception error) when (IsStorageError(error) || error is InvalidOperationException)
        {
            MessageBox.Show(this, error is InvalidOperationException ? error.Message : "No se pudo quitar el producto. Intentá nuevamente.", "Mesa · Menú", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    private void UpdateMenuSelection()
    {
        if (EditProductButton is null || RemoveProductButton is null) return;
        EditProductButton.IsEnabled = RemoveProductButton.IsEnabled = menuAvailable && MenuGrid.SelectedItem is MenuProduct;
    }
    private void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateMenuSelection();
    private void Menu_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(MenuGrid, e.OriginalSource as DependencyObject) is DataGridRow)
            EditProduct_Click(sender, e);
    }
    private void Menu_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { EditProduct_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Delete) { RemoveProduct_Click(sender, e); e.Handled = true; }
    }
}
