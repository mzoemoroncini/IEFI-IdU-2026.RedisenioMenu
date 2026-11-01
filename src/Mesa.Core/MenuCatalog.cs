using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Mesa.Core;

public sealed record MenuProduct(long Id, string Name, decimal Price)
{
    public Product Product => new(Name, Price);
    public string FormattedPrice => Sales.Money(Price);
}

public sealed class DuplicateProductException : InvalidOperationException
{
    public DuplicateProductException() : base("Ya hay un producto con ese nombre en el menú.") { }
}

public sealed class MenuCatalog(string databasePath)
{
    public const int MaximumProducts = 100;
    public const decimal MaximumPrice = 100000000m;

    public IReadOnlyList<MenuProduct> ReadAll()
    {
        using var connection = MesaDatabase.Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, PriceCents FROM MenuProducts ORDER BY Id";
        using var reader = command.ExecuteReader();
        var products = new List<MenuProduct>();
        while (reader.Read())
        {
            var product = new MenuProduct(reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2) / 100m);
            Validate(product.Name, product.Price);
            products.Add(product);
        }
        if (products.Count > MaximumProducts) throw new InvalidDataException("El menú supera la cantidad máxima de productos.");
        return products;
    }

    public MenuProduct Save(long? id, string name, decimal price)
    {
        name = NormalizeName(name);
        Validate(name, price);
        using var connection = MesaDatabase.Open(databasePath);
        using var transaction = connection.BeginTransaction();
        if (id is null)
        {
            using var count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM MenuProducts";
            if ((long)count.ExecuteScalar()! >= MaximumProducts)
                throw new InvalidOperationException($"El menú admite hasta {MaximumProducts} productos.");
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = id is null
            ? "INSERT INTO MenuProducts (Name, NameKey, PriceCents) VALUES ($name, $key, $price) RETURNING Id"
            : "UPDATE MenuProducts SET Name = $name, NameKey = $key, PriceCents = $price WHERE Id = $id RETURNING Id";
        if (id is not null) command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$key", NameKey(name));
        command.Parameters.AddWithValue("$price", checked((long)(price * 100m)));
        object? result;
        try { result = command.ExecuteScalar(); }
        catch (SqliteException error) when (error.SqliteErrorCode == 19) { throw new DuplicateProductException(); }
        if (result is null) throw new InvalidOperationException("El producto ya no está en el menú. Actualizá el listado.");
        transaction.Commit();
        return new MenuProduct((long)result, name, price);
    }

    public void Remove(long id)
    {
        using var connection = MesaDatabase.Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM MenuProducts WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0) throw new InvalidOperationException("El producto ya no está en el menú. Actualizá el listado.");
    }

    public static string NormalizeName(string? value) => string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    internal static string NameKey(string name) => string.Concat(NormalizeName(name).Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();

    public static bool TryPrice(string text, out decimal price)
    {
        price = 0;
        var parts = text.Split(',');
        return parts.Length is 1 or 2 && parts[0].Length > 0
            && parts.All(part => part.All(c => c is >= '0' and <= '9'))
            && (parts.Length == 1 || parts[1].Length is 1 or 2)
            && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, Sales.Argentina, out price)
            && price <= MaximumPrice;
    }

    private static void Validate(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new InvalidDataException("Ingresá un nombre de producto de hasta 200 caracteres.");
        if (price < 0 || price > MaximumPrice || decimal.Round(price, 2) != price)
            throw new InvalidDataException("Ingresá un precio entre 0 y 100.000.000, con hasta dos decimales.");
    }
}
