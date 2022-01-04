using Avalonia.Controls.ApplicationLifetimes;

namespace iOSSteamGuardExtractor
{
    sealed class App2 : App
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow2();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
