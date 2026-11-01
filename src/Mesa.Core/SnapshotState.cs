namespace Mesa.Core;

public static class SnapshotState
{
    public static bool IsCurrent(SaleSummary? summary, DateTime date, IReadOnlyList<string> quantities)
    {
        if (summary is null || summary.Date != date.Date || summary.Lines.Count != quantities.Count) return false;
        for (var i = 0; i < quantities.Count; i++)
            if (!Sales.TryQuantity(quantities[i], out var quantity) || quantity != summary.Lines[i].Quantity) return false;
        return true;
    }

    public static bool CanPrint(SaleSummary? summary, DateTime date, IReadOnlyList<string> quantities) =>
        summary is { Units: > 0 } && IsCurrent(summary, date, quantities);
}
