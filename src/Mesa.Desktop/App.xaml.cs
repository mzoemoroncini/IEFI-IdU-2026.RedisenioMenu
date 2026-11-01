using System.Globalization;
using System.Windows;

namespace Mesa.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = Core.Sales.Argentina;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
        base.OnStartup(e);
    }
}
