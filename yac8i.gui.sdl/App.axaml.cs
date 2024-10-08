using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using yac8i.gui.sdl.MVVM;

namespace yac8i.gui.sdl
{
    public partial class App : Application
    {
        private Chip8VM vm = new Chip8VM();
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            vm.NewMessage += (s, a) =>
                           {
                               Console.WriteLine(a);
                           };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();

                desktop.MainWindow.DataContext = new MainWindowViewModel(vm, desktop.MainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
