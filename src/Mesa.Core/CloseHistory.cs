using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Mesa.Core;

public sealed record SavedClose(SaleSummary Summary, DateTimeOffset SavedAt);
public sealed class CloseAlreadyExistsException : IOException
{
    public CloseAlreadyExistsException() : base("Ya hay un cierre guardado para este día.") { }
}

/// <summary>Base SQLite local. Cada guardado incluye cabecera y detalles en una transacción.</summary>
public sealed class CloseHistory(string databasePath)
{
    public string DatabasePath { get; } = Path.GetFullPath(databasePath);
    private static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static long Cents(decimal amount) => checked((long)(amount * 100m));

    public IReadOnlyList<SavedClose> ReadAll()
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = Read(connection, transaction, null);
        transaction.Commit();
        return result;
    }

    public SavedClose? Find(DateTime date)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = Read(connection, transaction, DateKey(date)).SingleOrDefault();
        transaction.Commit();
        return result;
    }

    public SavedClose Save(SaleSummary summary, bool replace = false)
    {
        Validate(summary);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var key = DateKey(summary.Date);
        using var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT COUNT(*) FROM DailyCloses WHERE CloseDate = $date";
        exists.Parameters.AddWithValue("$date", key);
        var found = (long)exists.ExecuteScalar()! != 0;
        if (found && !replace) throw new CloseAlreadyExistsException();
        if (found)
        {
            // Leer antes de reemplazar evita ocultar un cierre dañado.
            _ = Read(connection, transaction, key);
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM DailyCloses WHERE CloseDate = $date";
            delete.Parameters.AddWithValue("$date", key);
            delete.ExecuteNonQuery();
        }
        var savedAt = DateTimeOffset.Now;
        using var header = connection.CreateCommand();
        header.Transaction = transaction;
        header.CommandText = """
            INSERT INTO DailyCloses (CloseDate, SavedAt, TotalCents, TaxCents, Units, TaxRate)
            VALUES ($date, $saved, $total, $tax, $units, $rate)
            """;
        header.Parameters.AddWithValue("$date", key);
        header.Parameters.AddWithValue("$saved", savedAt.ToString("O", CultureInfo.InvariantCulture));
        header.Parameters.AddWithValue("$total", Cents(summary.Total));
        header.Parameters.AddWithValue("$tax", Cents(summary.Tax));
        header.Parameters.AddWithValue("$units", summary.Units);
        header.Parameters.AddWithValue("$rate", summary.TaxRate.ToString(CultureInfo.InvariantCulture));
        header.ExecuteNonQuery();
        for (var i = 0; i < summary.Lines.Count; i++)
        {
            var line = summary.Lines[i];
            using var detail = connection.CreateCommand();
            detail.Transaction = transaction;
            detail.CommandText = """
                INSERT INTO CloseLines (CloseDate, Position, ProductName, PriceCents, Quantity)
                VALUES ($date, $position, $name, $price, $quantity)
                """;
            detail.Parameters.AddWithValue("$date", key);
            detail.Parameters.AddWithValue("$position", i);
            detail.Parameters.AddWithValue("$name", line.Product.Name);
            detail.Parameters.AddWithValue("$price", Cents(line.Product.Price));
            detail.Parameters.AddWithValue("$quantity", line.Quantity);
            detail.ExecuteNonQuery();
        }
        transaction.Commit();
        return new SavedClose(summary, savedAt);
    }

    public static bool SameValues(SaleSummary? left, SaleSummary? right) =>
        left is not null && right is not null && left.Date.Date == right.Date.Date
        && left.TaxRate == right.TaxRate && left.Lines.SequenceEqual(right.Lines);

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, ForeignKeys = true, DefaultTimeout = 5, Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version";
            var version = (long)versionCommand.ExecuteScalar()!;
            if (version > 1) throw new InvalidDataException("Esta base de datos requiere una versión más nueva de Mesa.");
            if (version == 0)
            {
                using var transaction = connection.BeginTransaction();
                using var schema = connection.CreateCommand();
                schema.Transaction = transaction;
                schema.CommandText = """
                    CREATE TABLE IF NOT EXISTS DailyCloses (
                        CloseDate TEXT PRIMARY KEY NOT NULL,
                        SavedAt TEXT NOT NULL,
                        TotalCents INTEGER NOT NULL CHECK (TotalCents >= 0),
                        TaxCents INTEGER NOT NULL CHECK (TaxCents >= 0),
                        Units INTEGER NOT NULL CHECK (Units >= 0),
                        TaxRate TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS CloseLines (
                        CloseDate TEXT NOT NULL REFERENCES DailyCloses(CloseDate) ON DELETE CASCADE,
                        Position INTEGER NOT NULL CHECK (Position >= 0),
                        ProductName TEXT NOT NULL,
                        PriceCents INTEGER NOT NULL CHECK (PriceCents >= 0),
                        Quantity INTEGER NOT NULL CHECK (Quantity BETWEEN 0 AND 999999),
                        PRIMARY KEY (CloseDate, Position)
                    );
                    PRAGMA user_version = 1;
                    """;
                schema.ExecuteNonQuery();
                transaction.Commit();
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private static IReadOnlyList<SavedClose> Read(SqliteConnection connection, SqliteTransaction transaction, string? dateKey)
    {
        var headers = new List<(string Date, DateTimeOffset SavedAt, long Total, long Tax, int Units, decimal Rate)>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT CloseDate, SavedAt, TotalCents, TaxCents, Units, TaxRate FROM DailyCloses"
                + (dateKey is null ? "" : " WHERE CloseDate = $date") + " ORDER BY CloseDate DESC";
            if (dateKey is not null) command.Parameters.AddWithValue("$date", dateKey);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                headers.Add((reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                    reader.GetInt64(2), reader.GetInt64(3), reader.GetInt32(4), decimal.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        }
        var result = new List<SavedClose>();
        foreach (var header in headers)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT ProductName, PriceCents, Quantity FROM CloseLines WHERE CloseDate = $date ORDER BY Position";
            command.Parameters.AddWithValue("$date", header.Date);
            var lines = new List<SaleLine>();
            using (var reader = command.ExecuteReader())
                while (reader.Read()) lines.Add(new SaleLine(new Product(reader.GetString(0), reader.GetInt64(1) / 100m), reader.GetInt32(2)));
            var summary = new SaleSummary(DateTime.ParseExact(header.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Array.AsReadOnly(lines.ToArray()), header.Rate);
            Validate(summary);
            if (Cents(summary.Total) != header.Total || Cents(summary.Tax) != header.Tax || summary.Units != header.Units)
                throw new InvalidDataException($"Los datos del cierre {header.Date} no son consistentes. No se modificó el historial.");
            result.Add(new SavedClose(summary, header.SavedAt));
        }
        return result;
    }

    private static void Validate(SaleSummary summary)
    {
        if (summary.Date == default || summary.Date.TimeOfDay != TimeSpan.Zero
            || summary.TaxRate is < 0 or > 1
            || summary.Lines is null || summary.Lines.Count is < 1 or > 100
            || summary.Lines.Any(line => line is null || line.Product is null
                || string.IsNullOrWhiteSpace(line.Product.Name) || line.Product.Name.Length > 200
                || line.Product.Price is < 0 or > 1000000000m || decimal.Round(line.Product.Price, 2) != line.Product.Price
                || line.Quantity is < 0 or > Sales.MaximumQuantity))
            throw new InvalidDataException("El cierre contiene datos inválidos.");
    }
}
