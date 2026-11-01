using Mesa.Core;
using Microsoft.Data.Sqlite;

internal static class HistoryChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MesaChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "cierres.db");
            var history = new CloseHistory(path);
            var date = new DateTime(2026, 10, 21);
            var quantities = new[] { "14", "22", "18", "6", "8", "11", "4", "12" };
            var sale = Sales.Calculate(date, quantities);
            check(history.ReadAll().Count == 0, "Base nueva: historial vacío");
            check(File.ReadAllBytes(path).Take(16).SequenceEqual("SQLite format 3\0"u8.ToArray()), "Se crea una base SQLite real");
            var saved = history.Save(sale);
            var reopened = new CloseHistory(path).Find(date)!;
            check(CloseHistory.SameValues(sale, reopened.Summary), "El cierre persiste al volver a abrir la base");
            check(reopened.SavedAt == saved.SavedAt, "Se conserva la fecha y hora de guardado");
            check(reopened.Summary.Total == 344800m && reopened.Summary.Tax == 62064m, "Ventas e impuesto se recuperan correctamente");
            try { history.Save(sale); throw new Exception("Se aceptó un cierre duplicado"); }
            catch (CloseAlreadyExistsException) { check(history.ReadAll().Count == 1, "Una fecha repetida exige reemplazo explícito"); }
            var changed = (string[])quantities.Clone(); changed[0] = "16";
            var replacement = Sales.Calculate(date, changed);
            history.Save(replacement, replace: true);
            check(history.ReadAll().Count == 1 && history.Find(date)!.Summary.Total == 357800m, "Reemplazar actualiza el cierre sin duplicar el día");
            history.Save(Sales.Calculate(date.AddDays(1), quantities));
            history.Save(Sales.Calculate(date.AddDays(-1), quantities));
            check(history.ReadAll().Select(close => close.Summary.Date).SequenceEqual(new[] { date.AddDays(1), date, date.AddDays(-1) }), "Orden de días del más reciente al más antiguo");
            check(history.Find(date.AddDays(20)) is null, "Consultar una fecha sin cierre");
            var historicDate = date.AddDays(-2);
            var historical = new SaleSummary(historicDate, Array.AsReadOnly(new[]
            {
                new SaleLine(new Product("Hamburguesa", 1234.56m), 3),
                new SaleLine(new Product("Postre 'especial'; DROP TABLE DailyCloses; --", 10.01m), 2)
            }), 0.21m);
            history.Save(historical);
            var restored = history.Find(historicDate)!.Summary;
            check(restored.Lines[0].Product.Price == 1234.56m && restored.Lines[0].Product.Price != Sales.Menu[0].Price, "Se guardan los precios originales, independientes del menú actual");
            check(restored.TaxRate == 0.21m && restored.Tax == historical.Tax, "Se conserva la tasa aplicada al cierre");
            check(restored.Lines[1].Product.Name == historical.Lines[1].Product.Name && history.ReadAll().Count == 4, "Nombres con comillas se almacenan mediante parámetros");
            var noSales = Sales.Calculate(date.AddDays(2), Enumerable.Repeat("0", 8).ToArray());
            history.Save(noSales);
            check(history.Find(noSales.Date)!.Summary.Units == 0, "Se puede registrar un día sin ventas");

            // Un error después de insertar la cabecera debe deshacer toda la transacción.
            Execute(path, "CREATE TRIGGER FailTest BEFORE INSERT ON CloseLines WHEN NEW.Quantity = 12345 BEGIN SELECT RAISE(ABORT, 'fallo de prueba'); END;");
            var failQuantities = (string[])quantities.Clone(); failQuantities[1] = "12345";
            try { history.Save(Sales.Calculate(date.AddDays(3), failQuantities)); throw new Exception("No se provocó el fallo"); }
            catch (SqliteException) { check(history.Find(date.AddDays(3)) is null, "Un fallo no deja una cabecera sin detalles"); }
            try { history.Save(Sales.Calculate(date, failQuantities), replace: true); throw new Exception("No se provocó el fallo de reemplazo"); }
            catch (SqliteException) { check(CloseHistory.SameValues(history.Find(date)!.Summary, replacement), "Un fallo al reemplazar conserva el cierre anterior completo"); }
            Execute(path, "DROP TRIGGER FailTest;");
            var invalid = new SaleSummary(date.AddDays(4), [new SaleLine(new Product("Inválido", -1), 1)]);
            try { history.Save(invalid); throw new Exception("Se guardó un precio negativo"); }
            catch (InvalidDataException) { check(history.Find(invalid.Date) is null, "No se guardan cierres con datos inválidos"); }
            check(history.ReadAll().Count == 5, "Los fallos no alteran la cantidad de cierres guardados");

            var newerPath = Path.Combine(directory, "futura.db");
            _ = new CloseHistory(newerPath).ReadAll();
            Execute(newerPath, "PRAGMA user_version = 99;");
            try { new CloseHistory(newerPath).ReadAll(); throw new Exception("Se aceptó una versión incompatible"); }
            catch (InvalidDataException) { check(true, "Una versión de base incompatible se informa y no se reinicia"); }
            var brokenPath = Path.Combine(directory, "danada.db");
            File.WriteAllText(brokenPath, "Esto no es una base de datos");
            try { new CloseHistory(brokenPath).ReadAll(); throw new Exception("Se aceptó un archivo dañado"); }
            catch (SqliteException) { check(File.ReadAllText(brokenPath) == "Esto no es una base de datos", "Una base dañada no se sobrescribe ni se borra"); }
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
