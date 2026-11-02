using System.Globalization;
using System.Windows;

namespace Mesa.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = Core.Sales.Argentina;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((_, args) => { if (args.OriginalSource is Window window) ThemeManager.UpdateWindowChrome(window); }));
        base.OnStartup(e);
    }
}
