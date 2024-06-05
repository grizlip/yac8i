using Avalonia;
using System;

namespace yac8i.gui.sdl
{
    public class Program
    {

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        static void Main(string[] args)
        {
           BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }
}
