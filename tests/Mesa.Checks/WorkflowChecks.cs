using Mesa.Core;
using Microsoft.Data.Sqlite;

internal static class WorkflowChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MesaWorkflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var history = new CloseHistory(Path.Combine(directory, "cierres.db"));
            var date = new DateTime(2026, 11, 1);
            var draft = Sales.Calculate(date, ["14", "22", "18", "6", "8", "11", "4", "12"]);
            var completed = DailyCloseWorkflow.SaveAndClear(history, draft);
            check(completed.EmptyDraft.Units == 0 && completed.EmptyDraft.Total == 0 && completed.EmptyDraft.Tax == 0,
                "Guardar correctamente entrega una nueva carga con cantidades, total e impuesto en cero");
            check(completed.EmptyDraft.Date == date && completed.EmptyDraft.Lines.Select(line => line.Product).SequenceEqual(draft.Lines.Select(line => line.Product)),
                "La limpieza conserva la fecha y los productos con sus precios");
            check(history.Find(date)!.Summary.Total == 344800m && history.Find(date)!.Summary.Units == 95 && draft.Total == 344800m,
                "Limpiar la carga no modifica el cierre guardado ni el borrador anterior");
            try { DailyCloseWorkflow.SaveAndClear(history, completed.EmptyDraft); throw new Exception("Se sobrescribió el día con ceros"); }
            catch (CloseAlreadyExistsException)
            { check(history.Find(date)!.Summary.Total == 344800m && history.ReadAll().Count == 1, "Repetir guardar después de limpiar no sobrescribe el día con cero ni lo duplica"); }
            var empty = Sales.Calculate(date.AddDays(1), Enumerable.Repeat("0", 8).ToArray());
            var emptySaved = DailyCloseWorkflow.SaveAndClear(history, empty);
            check(emptySaved.Saved.Summary.Units == 0 && history.Find(empty.Date)!.Summary.Total == 0, "Un día sin ventas también se guarda y pasa a carga vacía");

            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = history.DatabasePath, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TRIGGER FailSave BEFORE INSERT ON CloseLines BEGIN SELECT RAISE(ABORT, 'prueba'); END;";
                command.ExecuteNonQuery();
            }
            var unsaved = Sales.Calculate(date.AddDays(2), ["2", "0", "0", "0", "0", "0", "0", "0"]);
            try { DailyCloseWorkflow.SaveAndClear(history, unsaved); throw new Exception("No se provocó el fallo"); }
            catch (SqliteException)
            {
                check(unsaved.Units == 2 && unsaved.Total == 13000m, "Un fallo de guardado conserva las cantidades y totales de la carga");
                check(history.Find(unsaved.Date) is null && history.ReadAll().Count == 2, "Un fallo de guardado no agrega cierres parciales ni altera el historial");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
