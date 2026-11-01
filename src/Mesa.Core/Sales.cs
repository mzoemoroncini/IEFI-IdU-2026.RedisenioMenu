using System.Globalization;

namespace Mesa.Core;

public sealed record Product(string Name, decimal Price);
public sealed record SaleLine(Product Product, int Quantity)
{
    public decimal Amount => Product.Price * Quantity;
}
public sealed record SaleSummary(DateTime Date, IReadOnlyList<SaleLine> Lines, decimal TaxRate = 0.18m)
{
    public decimal Total => Lines.Sum(line => line.Amount);
    public decimal Tax => decimal.Round(Total * TaxRate, 2, MidpointRounding.AwayFromZero);
    public int Units => Lines.Sum(line => line.Quantity);
}

public static class Sales
{
    public const int MaximumQuantity = 999999;
    public static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");
    public static IReadOnlyList<Product> Menu { get; } = Array.AsReadOnly(new[]
    {
        new Product("Hamburguesa", 6500m), new Product("Cerveza", 3500m),
        new Product("Gaseosa", 2500m), new Product("Ensalada", 5000m),
        new Product("Pancho", 3000m), new Product("Agua saborizada", 2200m),
        new Product("Sopa", 3800m), new Product("Postre", 3200m)
    });
    public static string Money(decimal amount) => amount.ToString("C2", Argentina);

    // Sin decimales, signos, notación científica ni separadores de miles.
    public static bool TryQuantity(string? text, out int quantity)
    {
        quantity = 0;
        return !string.IsNullOrEmpty(text) && text.All(c => c is >= '0' and <= '9')
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)
            && quantity <= MaximumQuantity;
    }

    public static SaleSummary Calculate(DateTime date, IReadOnlyList<string> quantities)
    {
        if (quantities.Count != Menu.Count)
            throw new ArgumentException("Se requiere una cantidad por producto.");
        var lines = Menu.Select((product, index) =>
        {
            if (!TryQuantity(quantities[index], out var quantity))
                throw new ArgumentException($"Revisá la cantidad de {product.Name}.");
            return new SaleLine(product, quantity);
        }).ToArray();
        return new SaleSummary(date.Date, Array.AsReadOnly(lines));
    }
}
