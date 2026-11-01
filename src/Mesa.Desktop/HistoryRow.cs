using Mesa.Core;

namespace Mesa.Desktop;

public sealed class HistoryRow(SavedClose close)
{
    public SavedClose Close { get; } = close;
    public string Date => Close.Summary.Date.ToString("dd/MM/yyyy", Sales.Argentina);
    public string Total => Sales.Money(Close.Summary.Total);
    public string Tax => Sales.Money(Close.Summary.Tax);
    public string Units => Close.Summary.Units.ToString("N0", Sales.Argentina);
    public string SavedAt => Close.SavedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Sales.Argentina);
}
