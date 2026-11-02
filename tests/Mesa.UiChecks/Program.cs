using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Calendar = System.Windows.Controls.Calendar;
using Mesa.Core;
using Mesa.Desktop;

internal static class Program
{
    private static int passed;
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
            Check(window.WindowStyle == WindowStyle.SingleBorderWindow && window.ResizeMode == ResizeMode.CanResize, "La ventana conserva la barra nativa con cerrar, minimizar y maximizar");
            Check(WindowBounds.IsInsideWorkArea(window), "Al abrir Mesa toda la ventana queda dentro del área útil de la pantalla");
            foreach (double scale in new[] {1d, 1.25d, 1.5d, 2d})
            {
                var area = new Rect(0,0,1920,1015);
                var fitted = WindowBounds.FitInWorkArea(area,new Rect(100,-100,1280*scale,820*scale),scale);
                Check(area.Contains(fitted) && fitted.Top >= 12*scale, $"La barra superior queda visible con escala {scale*100:0} %");
            }
            var leftMonitor = new Rect(-1366,-300,1366,728);
            Check(leftMonitor.Contains(WindowBounds.FitInWorkArea(leftMonitor,new Rect(-1600,-600,1600,1100),1.5)), "Se respeta el área útil de un monitor situado a la izquierda");
            window.Top = -500; WindowBounds.EnsureVisible(window,center:false); Pump();
            Check(WindowBounds.IsInsideWorkArea(window), "Una ventana fuera de pantalla se recupera sin perder el tamaño disponible");
            var picker = (DatePicker)window.FindName("ClosingDate");
            var date = new DateTime(2026, 11, 1);
            picker.SelectedDate = date; Pump();
            var rows = (ObservableCollection<SaleRow>)((ItemsControl)window.FindName("RowsControl")).DataContext;
            rows[0].Quantity = "2"; Pump();
            var light = (RadioButton)window.FindName("LightThemeButton");
            var dark = (RadioButton)window.FindName("DarkThemeButton");
            Check(light.IsVisible && dark.IsVisible, "El selector de tema está disponible en la navegación");
            Check(((TabControl)window.FindName("MainTabs")).TabStripPlacement == Dock.Left, "Las tres secciones usan navegación lateral");
            Capture((FrameworkElement)window.Content, "Mesa-claro");
            var totalBeforeTheme = ((TextBlock)window.FindName("TotalValue")).Text;
            dark.IsChecked = true; Pump();
            Check(ThemeManager.Current == MesaTheme.Dark && ((SolidColorBrush)app.Resources["Canvas"]).Color == Color.FromRgb(24,26,24), "El selector aplica la paleta oscura");
            Check(rows[0].Quantity == "2" && ((TextBlock)window.FindName("TotalValue")).Text == totalBeforeTheme, "Cambiar de tema conserva la carga y los cálculos");
            Check(ThemeManager.Load(Path.Combine(directory,"preferencias.json")) == MesaTheme.Dark, "El tema elegido queda guardado fuera de la base de ventas");
            Capture((FrameworkElement)window.Content, "Mesa-oscuro");
            light.IsChecked = true; Pump();
            Check(ThemeManager.Current == MesaTheme.Light && ((SolidColorBrush)app.Resources["Surface"]).Color == Colors.White, "El selector vuelve al tema claro");
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
            Capture(calendar, "Mesa-calendario");
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
            Capture((FrameworkElement)editor.Content, "Mesa-editar-cierre");
            editor.Close();
            var navigation = (TabControl)window.FindName("MainTabs");
            navigation.SelectedIndex = 1; Pump();
            Capture((FrameworkElement)window.Content, "Mesa-historial");
            navigation.SelectedIndex = 2; Pump();
            Capture((FrameworkElement)window.Content, "Mesa-menu");
            navigation.SelectedIndex = 0; window.Width = window.MinWidth; window.Height = window.MinHeight; Pump();
            WindowBounds.EnsureVisible(window,center:false); Pump();
            Capture((FrameworkElement)window.Content, "Mesa-tamano-minimo");
            dark.IsChecked = true; Pump();
            var reopened = new MainWindow(path); reopened.Show(); Pump();
            Check(((RadioButton)reopened.FindName("DarkThemeButton")).IsChecked == true, "Al volver a abrir Mesa se recupera la preferencia oscura");
            reopened.Close(); window.Close();
            Console.WriteLine($"{passed} verificaciones de interfaz correctas en Windows.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { app.Shutdown(); Directory.Delete(directory, recursive: true); }
    }
    private static void Capture(FrameworkElement visual, string name)
    {
        visual.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle((Brush)Application.Current.Resources["Canvas"], null, new Rect(0,0,image.PixelWidth,image.PixelHeight));
        image.Render(background);
        image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        var directory = Path.GetFullPath("artifacts/Mesa-preview"); Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory,name+".png")); encoder.Save(stream);
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("OK · " + name); passed++; }
    private static T? Find<T>(DependencyObject? root, string? name = null) where T : FrameworkElement
    {
        if (root is null) return null;
        if (root is T result && (name is null || result.Name == name)) return result;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Find<T>(VisualTreeHelper.GetChild(root, i), name) is { } child) return child;
        return null;
    }
}
