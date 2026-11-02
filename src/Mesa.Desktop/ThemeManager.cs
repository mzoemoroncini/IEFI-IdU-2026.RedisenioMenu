using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Mesa.Desktop;

public enum MesaTheme { Light, Dark }

public static class ThemeManager
{
    public static MesaTheme Current { get; private set; } = MesaTheme.Light;
    private static readonly string[] Keys = ["Accent", "AccentInk", "AccentSoft", "Ink", "Muted", "Line", "Soft", "Surface", "Canvas", "Sidebar", "Summary", "OnSummary", "OnAccent", "TotalMuted", "Danger", "Focus", "Selection", "ControlBorder"];
    private static readonly string[] Light = ["#A9553B", "#8E452F", "#F8E7DE", "#302C28", "#6E6258", "#EAE3DB", "#F7F3ED", "#FFFFFF", "#F6F4EF", "#FCFBF8", "#8D472F", "#FFF8F1", "#FFFFFF", "#F0DBD0", "#BA4539", "#9E5E42", "#FAE9DF", "#8E8276"];
    private static readonly string[] Dark = ["#E4A37E", "#F3BDA0", "#3C3028", "#F1EEE7", "#B5B2A8", "#3B403B", "#2F332F", "#242724", "#181A18", "#1D201D", "#3C3028", "#FFF7EF", "#251D18", "#D7BCAB", "#F19388", "#DAAA88", "#45352B", "#7B857B"];

    public static void Apply(MesaTheme theme)
    {
        var colors = theme == MesaTheme.Dark ? Dark : Light;
        for (int i = 0; i < Keys.Length; i++)
            Application.Current.Resources[Keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        Current = theme;
        foreach (Window window in Application.Current.Windows) UpdateWindowChrome(window);
    }

    public static void UpdateWindowChrome(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = Current == MesaTheme.Dark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    public static MesaTheme Load(string path)
    {
        try
        {
            using var data = JsonDocument.Parse(File.ReadAllText(path));
            return data.RootElement.GetProperty("theme").GetString() == "dark" ? MesaTheme.Dark : MesaTheme.Light;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        { return MesaTheme.Light; }
    }

    public static bool Save(string path, MesaTheme theme)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { theme = theme == MesaTheme.Dark ? "dark" : "light" }));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
