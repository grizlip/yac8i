using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

namespace yac8i.gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int PixelSize = 16;
        private WriteableBitmap writeableBitmap;
        private bool[,] surface = new bool[64, 32];
        public MainWindow()
        {
            InitializeComponent();
            RenderOptions.SetBitmapScalingMode(this.Surface, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this.Surface, EdgeMode.Aliased);

            writeableBitmap = new WriteableBitmap(
                1024,
                512,
                96,
                96,
                PixelFormats.BlackWhite,
                null);
            this.Surface.Source = writeableBitmap;
            DrawSurface();
            Chip8VM vm = new Chip8VM();
            vm.ScreenRefresh += OnScreenRefresh;
            vm.Load("simple_demo.ch8");
            Task.Factory.StartNew(() =>
                  {
                      vm.Start();
                  }, TaskCreationOptions.LongRunning);
        }

        private void OnScreenRefresh(object? sender, ScreenRefreshEventArgs args)
        {
            switch (args.RequestType)
            {
                case RefreshRequest.Clear:
                    Array.Clear(surface, 0, surface.Length);
                    break;
                case RefreshRequest.Draw:
                    surface = args.Surface;
                    break;
            }
            this.Dispatcher.BeginInvoke(() =>
              {
                  DrawSurface();
              });
        }

        private void DrawSurface()
        {
            for (int i = 0; i < surface.GetLength(0); i++)
            {
                for (int j = 0; j < surface.GetLength(1); j++)
                {
                    byte[] pixelBrush = new byte[32];
                    if (surface[i, j])
                    {
                        for (int k = 0; k < pixelBrush.Length; k++)
                        {
                            pixelBrush[k] = 0xFF;
                        }
                    }
                    
                    Int32Rect rect = new Int32Rect(i * PixelSize, j * PixelSize, PixelSize, PixelSize);
                    writeableBitmap.WritePixels(rect, pixelBrush, (rect.Width * writeableBitmap.Format.BitsPerPixel + 7) / 8, 0);
                }
            }
        }
    }
}
