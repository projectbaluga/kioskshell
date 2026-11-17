using System.Text.Json;

namespace KioskShell;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Only one copy at a time.
        using var mutex = new Mutex(true, @"Local\KioskShell.SingleInstance", out bool created);
        if (!created) return;

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Write("UI exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Fatal: " + e.ExceptionObject);

        Application.Run(new KioskForm(KioskConfig.Load()));
    }
}

public sealed class KioskConfig
{
    public string Url { get; set; } = "https://example.com";
    public int EdgeZonePx { get; set; } = 28;          // touch must start this close to the top edge
    public int DragDistancePx { get; set; } = 120;     // how far down it must travel
    public int MenuTimeoutSeconds { get; set; } = 20;  // menu closes itself if untouched
    public int ShutdownDelaySeconds { get; set; } = 10;// countdown during which Cancel works
    public int OfflineRetrySeconds { get; set; } = 10;

    public static KioskConfig Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "kiosk.json");
            if (File.Exists(path))
            {
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                return JsonSerializer.Deserialize<KioskConfig>(File.ReadAllText(path), opts) ?? new();
            }
        }
        catch (Exception ex) { Log.Write("Config error: " + ex.Message); }
        return new();
    }
}

internal static class Log
{
    public static void Write(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KioskShell");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "log.txt"), $"{DateTime.Now:s}  {message}{Environment.NewLine}");
        }
        catch { /* never crash because of logging */ }
    }
}
