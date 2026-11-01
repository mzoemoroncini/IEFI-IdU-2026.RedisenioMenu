using Mesa.Core;
using Microsoft.Data.Sqlite;

internal static class MenuChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MesaMenuChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "mesa.db");
            var catalog = new MenuCatalog(path);
            var history = new CloseHistory(path);
            var products = catalog.ReadAll();
            check(products.Count == 8 && products.Select(product => product.Product).SequenceEqual(Sales.DefaultMenu), "El menú inicial se carga una sola vez en SQLite");
            var sale = Sales.Calculate(new DateTime(2026, 10, 21), products.Select(product => product.Product).ToArray(), ["14", "22", "18", "6", "8", "11", "4", "12"]);
            history.Save(sale);
            var burger = products[0];
            var edited = catalog.Save(burger.Id, "  Hamburguesa   doble  ", 7200.50m);
            check(edited.Id == burger.Id && edited.Name == "Hamburguesa doble", "Editar conserva la identidad del producto y normaliza espacios");
            check(new MenuCatalog(path).ReadAll()[0].Price == 7200.50m, "El nombre y precio editados persisten al reabrir");
            check(history.Find(sale.Date)!.Summary.Lines[0].Product == new Product("Hamburguesa", 6500m), "Cambiar nombre y precio no modifica un cierre guardado");
            check(!SnapshotState.IsCurrent(sale, sale.Date, catalog.ReadAll().Select(product => product.Product).ToArray(), ["14", "22", "18", "6", "8", "11", "4", "12"]), "Un cambio de precio o nombre invalida el cálculo anterior");
            var added = catalog.Save(null, "Café", 1500.25m);
            check(catalog.ReadAll().Count == 9 && added.Id > products.Max(product => product.Id), "Agregar un producto asigna una identidad nueva");
            try { catalog.Save(null, "  CAFE ", 2000m); throw new Exception("Se aceptó un nombre duplicado"); }
            catch (DuplicateProductException) { check(catalog.ReadAll().Count == 9, "No se duplican nombres por mayúsculas, tildes o espacios"); }
            try { catalog.Save(burger.Id, "Café", 20m); throw new Exception("Se aceptó una edición duplicada"); }
            catch (DuplicateProductException) { check(catalog.ReadAll().First(product => product.Id == burger.Id) == edited, "Una edición con nombre duplicado conserva los datos anteriores"); }
            catalog.Remove(burger.Id);
            var original = history.Find(sale.Date)!.Summary;
            check(catalog.ReadAll().All(product => product.Id != burger.Id), "Quitar un producto lo elimina del menú actual");
            check(CloseHistory.SameValues(original, sale), "Quitar un producto conserva nombre, precio, cantidad, impuesto y total históricos");
            check(new CloseHistory(path).ReadAll().Count == 1, "Los cambios del menú conservan el único cierre del día");
            var nextProducts = catalog.ReadAll().Select(product => product.Product).ToArray();
            var nextSale = Sales.Calculate(sale.Date.AddDays(1), nextProducts, Enumerable.Repeat("1", nextProducts.Length).ToArray());
            history.Save(nextSale);
            check(nextSale.Lines.All(line => line.Product.Name != "Hamburguesa") && nextSale.Lines.Any(line => line.Product.Name == "Café"), "Los próximos cierres usan los productos actuales");
            try { catalog.Save(burger.Id, "Producto borrado", 1m); throw new Exception("Se editó un producto inexistente"); }
            catch (InvalidOperationException) { check(catalog.ReadAll().Count == 8, "No se recrea un producto eliminado al intentar editarlo"); }

            check(MenuCatalog.TryPrice("6500,50", out var price) && price == 6500.50m, "Precio con dos decimales y coma argentina");
            check(MenuCatalog.TryPrice("6500", out price) && price == 6500m, "Precio entero válido");
            check(MenuCatalog.TryPrice("0", out price) && price == 0m, "Se permite un producto sin cargo");
            foreach (var text in new[] { "-1", "6500.50", "6.500,50", "1,234", "", "NaN", ",5", "1,", "100000001" })
                check(!MenuCatalog.TryPrice(text, out _), $"Precio inválido rechazado: [{text}]");
            try { catalog.Save(null, " ", 10m); throw new Exception("Se aceptó un nombre vacío"); }
            catch (InvalidDataException) { check(true, "No se guardan productos sin nombre"); }
            try { catalog.Save(null, "Precio negativo", -1m); throw new Exception("Se aceptó un precio negativo"); }
            catch (InvalidDataException) { check(true, "La base recibe solamente precios válidos"); }
            try { catalog.Save(null, "Demasiados decimales", 1.001m); throw new Exception("Se perdió precisión"); }
            catch (InvalidDataException) { check(true, "No se truncan precios con más de dos decimales"); }

            // Simular la base v1 entregada: mismas tablas históricas, todavía sin menú.
            Execute(path, "DROP TABLE MenuProducts; PRAGMA user_version = 1;");
            check(new MenuCatalog(path).ReadAll().Count == 8, "La base anterior se actualiza automáticamente al agregar el menú");
            check(CloseHistory.SameValues(history.Find(sale.Date)!.Summary, sale) && history.ReadAll().Count == 2, "La actualización de base conserva todos los cierres anteriores");
            foreach (var product in catalog.ReadAll()) catalog.Remove(product.Id);
            check(new MenuCatalog(path).ReadAll().Count == 0, "Un menú vaciado no vuelve a cargar productos por defecto al reabrir");
            check(history.Find(sale.Date)!.Summary.Lines.Count == 8, "El historial conserva sus detalles aun con el menú vacío");
            try { Sales.Calculate(sale.Date, Array.Empty<Product>(), Array.Empty<string>()); throw new Exception("Se calculó un menú vacío"); }
            catch (ArgumentException) { check(true, "El cierre requiere al menos un producto disponible"); }
            var recreated = catalog.Save(null, "Hamburguesa", 9000m);
            check(history.Find(sale.Date)!.Summary.Lines[0].Product.Price == 6500m && recreated.Price == 9000m, "Recrear un nombre con otro precio no cambia su registro histórico");
            for (var i = 1; i < MenuCatalog.MaximumProducts; i++) catalog.Save(null, "Producto " + i, i);
            try { catalog.Save(null, "Producto extra", 1m); throw new Exception("Se superó el límite del menú"); }
            catch (InvalidOperationException) { check(catalog.ReadAll().Count == MenuCatalog.MaximumProducts, "El límite del menú evita cierres que no se pueden guardar"); }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
}
