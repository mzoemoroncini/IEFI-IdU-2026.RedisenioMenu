using Mesa.Core;

var date = new DateTime(2026, 10, 21);
var example = new[] { "14", "22", "18", "6", "8", "11", "4", "12" };
int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FALLÓ: {name}");
    Console.WriteLine($"OK · {name}"); passed++;
}

var result = Sales.Calculate(date, example);
Check(result.Total == 344800m && result.Tax == 62064m && result.Units == 95, "Ejemplo completo: ventas, impuesto y unidades");
Check(result.Lines.Select(l => l.Amount).SequenceEqual(new decimal[] {91000m,77000m,45000m,30000m,24000m,24200m,15200m,38400m}), "Importes por producto");
Check(result.Date == date, "El reporte conserva la fecha elegida");
Check(SnapshotState.CanPrint(result, date, example), "Impresión habilitada con cálculo válido y actualizado");
var changed = (string[])example.Clone(); changed[0] = "16";
Check(!SnapshotState.CanPrint(result, date, changed), "Cambiar cantidad bloquea impresión anterior");
Check(!SnapshotState.CanPrint(result, date.AddDays(1), example), "Cambiar fecha bloquea impresión anterior");
var recalculated = Sales.Calculate(date, changed);
Check(recalculated.Total == 357800m && recalculated.Tax == 64404m && recalculated.Units == 97, "Recalcular después de editar");
Check(SnapshotState.CanPrint(recalculated, date, changed), "Recalcular vuelve a habilitar impresión");
Check(result.Total == 344800m && result.Units == 95, "La edición no altera el reporte anterior");
Check(SnapshotState.IsCurrent(result, date, example), "Restaurar cantidades recupera la coincidencia");
Check(!SnapshotState.CanPrint(null, date, example), "No imprimir antes del primer cálculo");
var zeros = Enumerable.Repeat("0", 8).ToArray();
var empty = Sales.Calculate(date, zeros);
Check(empty.Total == 0m && empty.Tax == 0m && empty.Units == 0, "Cierre sin ventas");
Check(!SnapshotState.CanPrint(empty, date, zeros), "No imprimir un cierre sin ventas");
Check(Sales.TryQuantity("999999", out var max) && max == 999999, "Límite superior permitido");
Check(Sales.TryQuantity("00014", out var padded) && padded == 14, "Ceros iniciales válidos");
foreach (var text in new[] { "-3", "4.5", "4,5", "", " ", "+1", "1e3", "1.000", "1,000", "1000000", "99999999999999999999", "abc", "NaN", "∞" })
    Check(!Sales.TryQuantity(text, out _), $"Rechazo de cantidad inválida: [{text}]");
var invalid = (string[])example.Clone(); invalid[2] = "4.5";
Check(!SnapshotState.CanPrint(result, date, invalid), "Un decimal bloquea la impresión");
try { Sales.Calculate(date, invalid); throw new Exception("Se aceptó un decimal"); }
catch (ArgumentException) { passed++; Console.WriteLine("OK · No calcular con un decimal"); }
try { Sales.Calculate(date, ["1"]); throw new Exception("Se aceptó un menú incompleto"); }
catch (ArgumentException) { passed++; Console.WriteLine("OK · No calcular un menú incompleto"); }
var large = Sales.Calculate(date, Enumerable.Repeat("999999", 8).ToArray());
Check(large.Units == 7999992 && large.Total == 29699970300m, "Cantidades máximas sin desbordamiento");
Check(Sales.Money(6500m).Contains("6.500,00"), "Formato monetario argentino");
HistoryChecks.Run(Check);
Console.WriteLine($"\n{passed} verificaciones correctas.");
