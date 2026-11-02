using Mesa.Core;
internal static class UsabilityChecks
{
    public static void Run(Action<bool,string> check)
    {
        var undo = new DraftUndo();
        long[] ids = [1,2]; string[] quantities = ["14","4,5"];
        undo.Capture("02/11/2026",ids,quantities);
        quantities[0] = "99"; ids[0] = 8;
        check(undo.CanRestore("02/11/2026",[1,2]),"Deshacer conserva una copia de cantidades e identidades, sin compartir arreglos mutables");
        var restored=undo.Take("02/11/2026",[1,2]);
        check(restored!.SequenceEqual(new[]{"14","4,5"}),"Deshacer recupera incluso una entrada inválida para poder corregirla");
        check(undo.Take("02/11/2026",[1,2]) is null,"La última limpieza se deshace una sola vez");
        undo.Capture("02/11/2026",[1,2],["3","4"]);
        check(undo.Take("03/11/2026",[1,2]) is null,"Deshacer no mezcla cantidades de otra fecha");
        undo.Capture("02/11/2026",[1,2],["3","4"]);
        check(undo.Take("02/11/2026",[2,1]) is null,"Cambiar productos u orden impide asignar cantidades a productos incorrectos");
        undo.Capture("02/11/2026",[1,2],["3","4"]); undo.Clear();
        check(!undo.CanRestore("02/11/2026",[1,2]),"Un nuevo guardado o edición puede invalidar explícitamente Deshacer");
        var date=new DateTime(2026,11,2);
        SavedClose Record(int offset) => new(Sales.Calculate(date.AddDays(offset),Enumerable.Repeat("0",8).ToArray()),DateTimeOffset.UtcNow);
        SavedClose[] records=[Record(-1),Record(0),Record(1)];
        check(CloseSearch.Filter(records,date,date).Count==1,"El filtro del historial incluye ambos límites de la fecha");
        check(CloseSearch.Filter(records,date,null).Count==2 && CloseSearch.Filter(records,null,date).Count==2,"Se puede filtrar solo Desde o solo Hasta");
        check(CloseSearch.Filter(records,null,null).Select(c=>c.Summary.Date).SequenceEqual(new[]{date.AddDays(1),date,date.AddDays(-1)}),"Limpiar filtros recupera todos los cierres del más reciente al más antiguo");
        check(CloseSearch.Filter(records,date.AddDays(4),null).Count==0 && records.Length==3,"Una búsqueda sin coincidencias no altera el historial");
        try{CloseSearch.Filter(records,date.AddDays(1),date);throw new Exception("Se aceptó un período invertido");}catch(ArgumentException){check(true,"Un período invertido se rechaza sin cambiar registros");}
    }
}
