using Avalonia.Controls;
using yac8i.gui.sdl.MVVM;

namespace yac8i.gui.sdl
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if(DataContext is MainWindowViewModel mwvm)
            {
                mwvm.Dispose();
            }
            base.OnClosing(e);
        }
    }
}