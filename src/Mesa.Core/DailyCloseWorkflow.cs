namespace Mesa.Core;

public sealed record CompletedClose(SavedClose Saved, SaleSummary EmptyDraft);

public static class DailyCloseWorkflow
{
    public static CompletedClose SaveAndClear(CloseHistory history, SaleSummary draft)
    {
        // El borrador vacío solo se devuelve después de confirmar la transacción.
        var saved = history.Save(draft);
        var empty = new SaleSummary(draft.Date,
            Array.AsReadOnly(draft.Lines.Select(line => new SaleLine(line.Product, 0)).ToArray()), draft.TaxRate);
        return new CompletedClose(saved, empty);
    }
}
