using System.ComponentModel;
using System.Runtime.CompilerServices;
using Mesa.Core;

namespace Mesa.Desktop;

public sealed class SaleRow(Product product) : INotifyPropertyChanged
{
    private string quantity = "0";
    private string error = "";
    private string amount = Sales.Money(0);
    public Product Product { get; } = product;
    public string Name => Product.Name;
    public string Price => Sales.Money(Product.Price);
    public string Quantity
    {
        get => quantity;
        set { if (quantity == value) return; quantity = value; Changed(); }
    }
    public string Error { get => error; set { error = value; Changed(); Changed(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string Amount { get => amount; set { amount = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
