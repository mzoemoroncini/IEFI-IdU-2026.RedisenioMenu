namespace Mesa.Core;

/// <summary>One reversible manual clear, tied to the current date and product identities.</summary>
public sealed class DraftUndo
{
    private string? context;
    private long[] ids = [];
    private string[] quantities = [];

    public void Capture(string dateContext, IReadOnlyList<long> productIds, IReadOnlyList<string> values)
    {
        if (productIds.Count != values.Count) throw new ArgumentException("Productos y cantidades no coinciden.");
        context = dateContext; ids = productIds.ToArray(); quantities = values.ToArray();
    }
    public bool CanRestore(string dateContext, IReadOnlyList<long> productIds) => context is not null && context == dateContext && ids.SequenceEqual(productIds);
    public string[]? Take(string dateContext, IReadOnlyList<long> productIds)
    {
        var result = CanRestore(dateContext, productIds) ? quantities.ToArray() : null;
        Clear(); return result;
    }
    public void Clear() { context = null; ids = []; quantities = []; }
}
