using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using yac8i.gui.sdl.MVVM;

namespace yac8i.gui.sdl
{

    public partial class App : Application
    {
        private Chip8VM vm = new Chip8VM();
        private Task sdlTask;
        private SDLFront sdlFront;
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

            sdlFront = new SDLFront(vm);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                MainWindowViewModel mwvm = new MainWindowViewModel(sdlFront, new Model(vm), desktop.MainWindow);

                desktop.MainWindow.DataContext = mwvm;


                if (!sdlFront.Initialize())
                {
                    Console.WriteLine("Error while initializing SDL front.");
                }
                else
                {
                    var audioDevices = sdlFront.GetAudioDevices();
                    sdlFront.ChooseAudioDevice(audioDevices[0]);
                    mwvm.UpdateAudioDevices();
                    sdlTask = sdlFront.Start();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}