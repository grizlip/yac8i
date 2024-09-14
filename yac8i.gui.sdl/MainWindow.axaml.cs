using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using yac8i.gui.sdl.MVVM;

namespace yac8i.gui.sdl
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        public void OnBreakpointRemove(object? sender, RoutedEventArgs? args)
        {
            if (sender is Button b && b.DataContext is BreakpointViewModel breakpointViewModel)
            {
                ViewModel?.RemoveBreakpoint(breakpointViewModel);
            }
        }

        public void OnNewBreakpointSet(object? sender, PointerPressedEventArgs? args)
        {
            if (args?.ClickCount == 2 &&
                sender is Grid g &&
                g.DataContext is InstructionViewModel ivm)
            {
                ViewModel?.AddNewBreakpoint(ivm.Address);
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            ViewModel?.Dispose();
            base.OnClosing(e);
        }
    }
}