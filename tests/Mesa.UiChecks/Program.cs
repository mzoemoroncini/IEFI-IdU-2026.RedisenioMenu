using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Calendar = System.Windows.Controls.Calendar;
using Mesa.Core;
using Mesa.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-AR");
        var directory = Path.Combine(Path.GetTempPath(), "MesaUI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/Mesa;component/Themes/Mesa.xaml", UriKind.Relative));
            var path = Path.Combine(directory, "cierres.db");
            var window = new MainWindow(path);
            window.Show(); Pump();
            var picker = (DatePicker)window.FindName("ClosingDate");
            var date = new DateTime(2026, 11, 1);
            picker.SelectedDate = date; Pump();
            var rows = (ObservableCollection<SaleRow>)((ItemsControl)window.FindName("RowsControl")).DataContext;
            rows[0].Quantity = "2"; Pump();
            var save = (Button)window.FindName("SaveButton");
            Check(save.IsEnabled, "Una fecha nueva permite guardar cantidades válidas");
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(rows.All(row => row.Quantity == "0") && ((TextBlock)window.FindName("TotalValue")).Text == Sales.Money(0), "La interfaz limpia cantidades y totales tras guardar");
            Check(new CloseHistory(path).Find(date)!.Summary.Units == 2, "Los datos se guardaron antes de limpiar la pantalla");
            Check(!save.IsEnabled && ((Border)window.FindName("RegisteredDayPanel")).IsVisible, "La fecha guardada bloquea una segunda carga y muestra sus acciones");
            Check(((TextBlock)window.FindName("StatusText")).Text.Contains("guardadas correctamente"), "El guardado informa el resultado al usuario");
            picker.SelectedDate = date.AddDays(1); Pump();
            Check(save.IsEnabled && rows.All(row => row.Quantity == "0"), "Otra fecha permite comenzar una carga vacía");
            picker.SelectedDate = date; Pump();
            Check(!save.IsEnabled, "Volver al día guardado conserva el bloqueo");
            picker.IsDropDownOpen = true; Pump();
            var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
            var calendar = Find<Calendar>(popup.Child) ?? throw new Exception("El calendario no abrió");
            Check(calendar.FirstDayOfWeek == DayOfWeek.Monday, "El calendario comienza el lunes");
            Check(Find<CalendarDayButton>(calendar) is not null, "La plantilla moderna genera los días del mes");
            calendar.DisplayMode = CalendarMode.Year; Pump();
            Check(Find<Grid>(calendar, "PART_YearView")?.IsVisible == true, "El calendario permite elegir el mes desde la vista anual");
            calendar.DisplayMode = CalendarMode.Month; Pump();
            calendar.DisplayDate = date.AddMonths(1); Pump();
            Check(calendar.DisplayDate.Month == 12, "La navegación del calendario cambia de mes");
            picker.IsDropDownOpen = false;
            var history = new CloseHistory(path);
            var editor = new HistoryEditWindow(history, history.Find(date)!) { Owner = window };
            editor.Show(); Pump();
            var editRows = (ObservableCollection<SaleRow>)((ItemsControl)editor.FindName("EditRows")).ItemsSource;
            Check(editRows[0].Quantity == "2" && !((Button)editor.FindName("SaveChangesButton")).IsEnabled, "Editar abre las cantidades históricas sin cambios pendientes");
            editor.Close(); window.Close();
            Console.WriteLine("12 verificaciones de interfaz correctas en Windows.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { app.Shutdown(); Directory.Delete(directory, recursive: true); }
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("OK · " + name); }
    private static T? Find<T>(DependencyObject? root, string? name = null) where T : FrameworkElement
    {
        if (root is null) return null;
        if (root is T result && (name is null || result.Name == name)) return result;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Find<T>(VisualTreeHelper.GetChild(root, i), name) is { } child) return child;
        return null;
    }
}
