using Microsoft.Data.Sqlite;

namespace Mesa.Core;

internal static class MesaDatabase
{
    public static SqliteConnection Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, ForeignKeys = true, DefaultTimeout = 5, Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version";
            var version = (long)versionCommand.ExecuteScalar()!;
            if (version > 2) throw new InvalidDataException("Esta base de datos requiere una versión más nueva de Mesa.");
            if (version < 2)
            {
                using var transaction = connection.BeginTransaction();
                versionCommand.Transaction = transaction;
                version = (long)versionCommand.ExecuteScalar()!;
                if (version == 0)
                {
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
                        """;
                    schema.ExecuteNonQuery();
                }
                if (version < 2)
                {
                    using var schema = connection.CreateCommand();
                    schema.Transaction = transaction;
                    // Los detalles históricos son copias independientes: no apuntan al menú.
                    schema.CommandText = """
                        CREATE TABLE MenuProducts (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            NameKey TEXT NOT NULL UNIQUE,
                            PriceCents INTEGER NOT NULL CHECK (PriceCents BETWEEN 0 AND 10000000000)
                        );
                        """;
                    schema.ExecuteNonQuery();
                    foreach (var product in Sales.DefaultMenu)
                    {
                        using var seed = connection.CreateCommand();
                        seed.Transaction = transaction;
                        seed.CommandText = "INSERT INTO MenuProducts (Name, NameKey, PriceCents) VALUES ($name, $key, $price)";
                        seed.Parameters.AddWithValue("$name", product.Name);
                        seed.Parameters.AddWithValue("$key", MenuCatalog.NameKey(product.Name));
                        seed.Parameters.AddWithValue("$price", (long)(product.Price * 100m));
                        seed.ExecuteNonQuery();
                    }
                    schema.CommandText = "PRAGMA user_version = 2";
                    schema.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
}
