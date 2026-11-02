namespace Mesa.Core;

public static class CloseSearch
{
    public static IReadOnlyList<SavedClose> Filter(IEnumerable<SavedClose> records, DateTime? from, DateTime? to)
    {
        if (from?.Date > to?.Date) throw new ArgumentException("La fecha Desde no puede ser posterior a Hasta.");
        return records.Where(close => (!from.HasValue || close.Summary.Date.Date >= from.Value.Date)
            && (!to.HasValue || close.Summary.Date.Date <= to.Value.Date))
            .OrderByDescending(close => close.Summary.Date).ToArray();
    }
}
