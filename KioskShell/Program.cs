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

/// <summary>Everything lives relative to the exe, so the whole folder can be moved or copied anywhere.</summary>
internal static class AppPaths
{
    public static readonly string BaseDir =
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    public static string AssetsDir => Path.Combine(BaseDir, "assets");

    private static string? _dataDir;
    public static string DataDir => _dataDir ??= ResolveDataDir();

    private static string ResolveDataDir()
    {
        // Portable first: keep browser data and logs in .\data next to the exe if that is writable
        // (USB stick, a folder the kiosk user can write to).
        try
        {
            var portable = Path.Combine(BaseDir, "data");
            Directory.CreateDirectory(portable);
            var probe = Path.Combine(portable, ".write-test-" + Environment.ProcessId);
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return portable;
        }
        catch { /* read-only location such as C:\KioskShell for a standard user */ }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KioskShell");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}

public sealed class KioskConfig
{
    public string Url { get; set; } = "assets/index.html";
    public int EdgeZonePx { get; set; } = 28;           // touch must start this close to the top edge
    public int DragDistancePx { get; set; } = 120;      // how far down it must travel
    public int MenuTimeoutSeconds { get; set; } = 20;   // menu closes itself if untouched
    public int ShutdownDelaySeconds { get; set; } = 10; // countdown during which Cancel works
    public int OfflineRetrySeconds { get; set; } = 10;

    public static KioskConfig Load()
    {
        try
        {
            var path = Path.Combine(AppPaths.BaseDir, "kiosk.json");
            if (File.Exists(path))
            {
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
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
            File.AppendAllText(
                Path.Combine(AppPaths.DataDir, "log.txt"),
                $"{DateTime.Now:s}  {message}{Environment.NewLine}");
        }
        catch { /* never crash because of logging */ }
    }
}
