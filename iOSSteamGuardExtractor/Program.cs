using Avalonia;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif

namespace iOSSteamGuardExtractor;

static class Program
{
    public static bool IsTest { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static void Main(string[] args)
    {
        try
        {
            IsTest = args.Contains("--test");
            SQLitePCL.Batteries_V2.Init();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
#if NETFRAMEWORK
        catch (Exception ex)
#else
        catch
#endif
        {
#if NETFRAMEWORK
            MessageBox.Show(ex.ToString(), nameof(iOSSteamGuardExtractor),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
#else
            throw;
#endif
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}